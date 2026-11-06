using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A lobby hálózati kezelése. A NetworkSession hozza létre a NetworkManager
/// GameObjectjén, így jelenetváltáskor is megmarad.
///
/// Host: a csatlakozási kérést csak akkor fogadja el, ha van nyitott lobby
/// és szabad hely; a csatlakozott kliensnek lefoglalja az első szabad
/// helyet, kilépéskor felszabadítja. A lobby minden változásakor minden
/// kliensnek elküldi a pillanatképet (a saját játékosazonosítójával).
///
/// Kliens: fogadja a pillanatképet (LatestSnapshot), és jelzi, ha a
/// kapcsolat megszakadt.
/// </summary>
[DisallowMultipleComponent]
public sealed class LobbyNetwork : MonoBehaviour
{
    private const string StateMessageName = "RTS.Lobby.State";
    private const int MaxMessageSize = 16 * 1024;

    public const string LobbyFullReason = "A lobby megtelt.";
    public const string NoLobbyReason = "Nincs nyitott lobby, vagy a mérkőzés már elindult.";
    public const string ConnectionLostReason = "Megszakadt a kapcsolat a hosttal.";
    public const string HostClosedReason = "A host bezárta a lobbyt.";

    private readonly Dictionary<ulong, int> _clientPlayers = new Dictionary<ulong, int>();

    private NetworkManager _manager;
    private LobbyState _hostLobby;

    /// <summary>
    /// A kliensen a legutóbb kapott pillanatkép (a hoston null).
    /// </summary>
    public LobbySnapshot LatestSnapshot { get; private set; }

    public event Action SnapshotReceived;

    /// <summary>
    /// A kliensen: a kapcsolat megszakadt vagy a host elutasította. A
    /// paraméter az ok, amelyet a felület kiírhat.
    /// </summary>
    public event Action<string> Disconnected;

    /// <summary>
    /// A hoston: a kliens kapcsolatazonosítójához tartozó játékosazonosító.
    /// </summary>
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
        manager.OnClientConnectedCallback += OnClientConnected;
        manager.OnClientDisconnectCallback += OnClientDisconnected;

        if (manager.IsServer)
            manager.ConnectionApprovalCallback = ApproveConnection;
    }

    internal void OnStopping()
    {
        if (_manager == null)
            return;

        _manager.CustomMessagingManager?.UnregisterNamedMessageHandler(StateMessageName);
        _manager.OnClientConnectedCallback -= OnClientConnected;
        _manager.OnClientDisconnectCallback -= OnClientDisconnected;
        _manager.ConnectionApprovalCallback = null;
        _manager = null;

        DetachHostLobby(_hostLobby);
        _clientPlayers.Clear();
    }

    /// <summary>
    /// A host lobbyja. Amíg nincs csatolva (pl. a mérkőzés már elindult),
    /// a host minden csatlakozási kérést elutasít.
    /// </summary>
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

        // A TryOccupyFreeSlot már küldött pillanatképet, de akkor ez a
        // kliens még nem szerepelt a játékosai között.
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

    /// <summary>
    /// A host saját (magyar) elutasítási okai változatlanok maradnak; a
    /// Netcode angol üzenetei helyett magyar szöveg jelenik meg.
    /// </summary>
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
}
