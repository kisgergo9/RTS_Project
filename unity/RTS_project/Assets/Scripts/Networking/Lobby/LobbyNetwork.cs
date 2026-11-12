using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// A lobby hálózati kezelése.
[DisallowMultipleComponent]
public sealed class LobbyNetwork : MonoBehaviour
{
    private const string StateMessageName = "RTS.Lobby.State";
    private const string MatchStartMessageName = "RTS.Match.Start";
    private const int MaxMessageSize = 16 * 1024;

    // A világüzenet a legnagyobb pályán kb. 6 KB, ezért tördelhető csatornán megy.
    private const int MaxMatchStartMessageSize = 64 * 1024;

    public const string LobbyFullReason = "A lobby megtelt.";
    public const string NoLobbyReason = "Nincs nyitott lobby, vagy a mérkőzés már elindult.";
    public const string ConnectionLostReason = "Megszakadt a kapcsolat a hosttal.";
    public const string HostClosedReason = "A host bezárta a lobbyt.";

    private readonly Dictionary<ulong, int> _clientPlayers = new Dictionary<ulong, int>();

    private NetworkManager _manager;
    private LobbyState _hostLobby;

    // A kliensen a legutóbb kapott pillanatkép (a hoston null).
    public LobbySnapshot LatestSnapshot { get; private set; }

    public event Action SnapshotReceived;

    // A kliensen: a kapcsolat megszakadt vagy a host elutasította.
    public event Action<string> Disconnected;

    // A hoston: a kliens kapcsolatazonosítójához tartozó játékosazonosító.
    public bool TryGetPlayerId(ulong clientId, out int playerId)
    {
        if (clientId == NetworkManager.ServerClientId)
        {
            playerId = LobbyState.HostPlayerId;
            return true;
        }

        return _clientPlayers.TryGetValue(clientId, out playerId);
    }

    internal void OnStarted(NetworkManager manager)
    {
        // Ha az előző kapcsolat magától szakadt meg, a feliratkozások még élnek.
        OnStopping();

        _manager = manager;
        _clientPlayers.Clear();
        LatestSnapshot = null;

        manager.CustomMessagingManager.RegisterNamedMessageHandler(StateMessageName, OnStateMessage);
        manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchStartMessageName, OnMatchStartMessage);
        manager.OnClientConnectedCallback += OnClientConnected;
        manager.OnClientDisconnectCallback += OnClientDisconnected;

        if (manager.IsServer)
            manager.ConnectionApprovalCallback = ApproveConnection;
    }

    // Már futó hosthoz köti a lobbykezelést, ha még nincs hozzákötve.
    internal void EnsureStartedAsHost(NetworkManager manager)
    {
        if (_manager == manager)
            return;

        manager.ConnectionApprovalCallback = ApproveConnection;
        OnStarted(manager);
    }

    internal void OnStopping()
    {
        if (_manager == null)
            return;

        _manager.CustomMessagingManager?.UnregisterNamedMessageHandler(StateMessageName);
        _manager.CustomMessagingManager?.UnregisterNamedMessageHandler(MatchStartMessageName);
        _manager.OnClientConnectedCallback -= OnClientConnected;
        _manager.OnClientDisconnectCallback -= OnClientDisconnected;
        _manager.ConnectionApprovalCallback = null;
        _manager = null;

        DetachHostLobby(_hostLobby);
        _clientPlayers.Clear();
    }

    // A host lobbyja.
    public void AttachHostLobby(LobbyState lobby)
    {
        if (lobby == null)
            throw new ArgumentNullException(nameof(lobby));

        DetachHostLobby(_hostLobby);

        _hostLobby = lobby;
        _hostLobby.Changed += BroadcastState;
        BroadcastState();
    }

    public void DetachHostLobby(LobbyState lobby)
    {
        if (lobby == null || lobby != _hostLobby)
            return;

        _hostLobby.Changed -= BroadcastState;
        _hostLobby = null;
    }

    // A hoston: a mérkőzés indítása.
    public bool TryStartMatch(MatchSetup setup, string matchSceneName, out string error)
    {
        if (setup == null)
            throw new ArgumentNullException(nameof(setup));

        if (_manager == null || !_manager.IsServer)
        {
            error = "A mérkőzést csak a futó host indíthatja.";
            return false;
        }

        if (_clientPlayers.Count != setup.Players.Count - 1)
        {
            error = "A csatlakozott játékosok száma nem egyezik a játékoshelyekével.";
            return false;
        }

        foreach (KeyValuePair<ulong, int> client in _clientPlayers)
            SendMatchStart(client.Key, setup, client.Value);

        DetachHostLobby(_hostLobby);

        SceneEventProgressStatus status = _manager.SceneManager.LoadScene(
            matchSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);

        if (status != SceneEventProgressStatus.Started)
        {
            error = $"A mérkőzés jelenete nem tölthető be ({status}).";
            return false;
        }

        error = null;
        return true;
    }

    private void SendMatchStart(ulong clientId, MatchSetup setup, int playerId)
    {
        MatchStartMessage message = MatchStartMessage.FromSetup(setup, playerId);

        using FastBufferWriter writer = new FastBufferWriter(8 * 1024, Allocator.Temp, MaxMatchStartMessageSize);
        writer.WriteNetworkSerializable(message);
        _manager.CustomMessagingManager.SendNamedMessage(
            MatchStartMessageName, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    // Host

    internal void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false;

        if (request.ClientNetworkId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
            return;
        }

        if (_hostLobby == null)
        {
            Reject(response, NoLobbyReason);
            return;
        }

        if (_hostLobby.OccupiedSlotCount >= _hostLobby.PlayerCount)
        {
            Reject(response, LobbyFullReason);
            return;
        }

        response.Approved = true;
    }

    private static void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
    {
        response.Approved = false;
        response.Reason = reason;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (_manager == null || !_manager.IsServer || clientId == NetworkManager.ServerClientId)
            return;

        // Az elfogadás és a csatlakozás között a hely betelhetett.
        if (_hostLobby == null || !_hostLobby.TryOccupyFreeSlot(out int playerId))
        {
            _manager.DisconnectClient(clientId, _hostLobby == null ? NoLobbyReason : LobbyFullReason);
            return;
        }

        _clientPlayers[clientId] = playerId;

        // Az új kliens a korábbi pillanatképet még nem kapta meg.
        SendState(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (_manager == null)
            return;

        if (!_manager.IsServer)
        {
            Disconnected?.Invoke(TranslateDisconnectReason(_manager.DisconnectReason));
            return;
        }

        if (!_clientPlayers.TryGetValue(clientId, out int playerId))
            return;

        _clientPlayers.Remove(clientId);
        _hostLobby?.ReleaseSlot(playerId);
    }

    // A Netcode angol elutasítási üzenetei helyett magyar szöveg.
    public static string TranslateDisconnectReason(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return ConnectionLostReason;

        if (reason == LobbyFullReason || reason == NoLobbyReason)
            return reason;

        return reason.IndexOf("shutting down", StringComparison.OrdinalIgnoreCase) >= 0
            ? HostClosedReason
            : ConnectionLostReason;
    }

    private void BroadcastState()
    {
        if (_manager == null || !_manager.IsServer)
            return;

        foreach (ulong clientId in _clientPlayers.Keys)
            SendState(clientId);
    }

    private void SendState(ulong clientId)
    {
        if (_hostLobby == null || !_clientPlayers.TryGetValue(clientId, out int playerId))
            return;

        LobbySnapshot snapshot = LobbySnapshot.FromState(_hostLobby, playerId);

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, MaxMessageSize);
        writer.WriteNetworkSerializable(snapshot);
        _manager.CustomMessagingManager.SendNamedMessage(
            StateMessageName, clientId, writer, NetworkDelivery.ReliableSequenced);
    }

    // Kliens

    private void OnStateMessage(ulong senderClientId, FastBufferReader reader)
    {
        // Pillanatképet csak a hosttól fogadunk el.
        if (_manager == null || _manager.IsServer || senderClientId != NetworkManager.ServerClientId)
            return;

        try
        {
            reader.ReadNetworkSerializable(out LobbySnapshot snapshot);
            LatestSnapshot = snapshot;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Hibás lobbyüzenet: {exception.Message}", this);
            return;
        }

        SnapshotReceived?.Invoke();
    }

    private void OnMatchStartMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_manager == null || _manager.IsServer || senderClientId != NetworkManager.ServerClientId)
            return;

        try
        {
            reader.ReadNetworkSerializable(out MatchStartMessage message);
            MatchSetup.Current = message.ToSetup();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Hibás mérkőzésindító üzenet: {exception.Message}", this);
        }
    }
}
