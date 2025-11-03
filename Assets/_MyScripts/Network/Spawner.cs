using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using UnityEngine.SocialPlatforms;
using TMPro;

public class Spawner : SimulationBehaviour, INetworkRunnerCallbacks
{
    [SerializeField]
    NetworkPlayer networkPlayerPrefab;

    public void OnConnectedToServer(NetworkRunner runner)
    {

    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        

    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {

    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {

    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {

    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {

    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (NetworkPlayer.Local != null)
            input.Set(NetworkPlayer.Local.GetNetworkInput());

    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {

    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {

    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {

    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            var label = GameObject.Find("JoinCodeText")?.GetComponent<TMP_Text>();
            if (label != null)
            label.text = $"Code: {runner.SessionInfo.Name}";
            Utils.DebugLog("OnPlayerJoined this is the server/host, spawning network player");

            runner.Spawn(networkPlayerPrefab.gameObject, Vector3.zero, Quaternion.identity, player);

        }
        else Utils.DebugLog("OnPlayerJoined this is the client");

    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {

    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {

    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {

    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {

    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {

    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {

    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {

    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {

    }
    
    void Awake()
    {
        var runner = FindObjectOfType<NetworkRunner>();
        if (runner) runner.AddCallbacks(this);
    }
}
