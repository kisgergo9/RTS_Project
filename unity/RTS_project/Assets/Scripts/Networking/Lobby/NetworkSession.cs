using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// A hálózati kapcsolat indítása és leállítása (host vagy kliens).
///
/// A NetworkManagert első használatkor kódból hozza létre (Unity Transport,
/// jelenetkezelés és csatlakozás-jóváhagyás bekapcsolva), így egyik
/// jelenetnek sem kell tartalmaznia; jelenetváltáskor megmarad. A kliens a
/// csatlakozás után automatikusan a host jelenetét (a lobbyt) tölti be.
/// A lobby üzenetkezelését az ugyanezen a GameObjecten lévő LobbyNetwork végzi.
/// </summary>
public static class NetworkSession
{
    public const ushort DefaultPort = 7777;

    // Sikertelen csatlakozásnál kb. 5 másodperc után jelez (az alapérték 60).
    private const int ConnectTimeoutMs = 1000;
    private const int MaxConnectAttempts = 5;

    public static NetworkManager Manager => NetworkManager.Singleton;

    public static LobbyNetwork Lobby =>
        Manager != null ? Manager.GetComponent<LobbyNetwork>() : null;

    public static bool IsRunning => Manager != null && Manager.IsListening;
    public static bool IsHost => IsRunning && Manager.IsHost;
    public static bool IsClientOnly => IsRunning && Manager.IsClient && !Manager.IsServer;

    /// <summary>
    /// A legutóbbi kapcsolatbontás oka (pl. „A lobby megtelt.”); a
    /// csatlakozási képernyő kiírja, majd törli.
    /// </summary>
    public static string LastDisconnectReason { get; set; }

    public static bool StartHost(ushort port = DefaultPort)
    {
        NetworkManager manager = EnsureManager();

        if (manager.IsListening)
            return manager.IsHost;

        // 0.0.0.0: a host minden hálózati csatolón fogadja a kapcsolatokat.
        manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", port, "0.0.0.0");

        // A host induláskor saját magát is jóváhagyja, ezért a
        // jóváhagyásnak már az indítás előtt be kell állítva lennie.
        manager.ConnectionApprovalCallback = manager.GetComponent<LobbyNetwork>().ApproveConnection;

        if (!manager.StartHost())
        {
            manager.ConnectionApprovalCallback = null;
            return false;
        }

        manager.GetComponent<LobbyNetwork>().OnStarted(manager);
        return true;
    }

    public static bool StartClient(string address, ushort port = DefaultPort)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("A host címét meg kell adni.", nameof(address));

        NetworkManager manager = EnsureManager();

        if (manager.IsListening)
            return false;

        manager.GetComponent<UnityTransport>().SetConnectionData(address.Trim(), port);

        if (!manager.StartClient())
            return false;

        manager.GetComponent<LobbyNetwork>().OnStarted(manager);
        return true;
    }

    /// <summary>
    /// A gép helyi hálózati IPv4-címei, amelyeken a kliensek elérhetik a hostot.
    /// </summary>
    public static List<string> GetLocalIPv4Addresses()
    {
        List<string> addresses = new List<string>();

        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                foreach (UnicastIPAddressInformation address in
                         networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                        addresses.Add(address.Address.ToString());
                }
            }
        }
        catch (NetworkInformationException exception)
        {
            Debug.LogWarning($"A helyi IP-címek nem kérdezhetők le: {exception.Message}");
        }

        return addresses;
    }

    public static void Shutdown()
    {
        NetworkManager manager = Manager;

        if (manager == null)
            return;

        manager.GetComponent<LobbyNetwork>().OnStopping();

        if (manager.IsListening)
            manager.Shutdown();
    }

    private static NetworkManager EnsureManager()
    {
        if (NetworkManager.Singleton != null)
            return NetworkManager.Singleton;

        GameObject managerObject = new GameObject("NetworkManager");

        UnityTransport transport = managerObject.AddComponent<UnityTransport>();
        transport.ConnectTimeoutMS = ConnectTimeoutMs;
        transport.MaxConnectAttempts = MaxConnectAttempts;

        NetworkManager manager = managerObject.AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = true,
            ConnectionApproval = true
        };

        managerObject.AddComponent<LobbyNetwork>();

        return manager;
    }
}
