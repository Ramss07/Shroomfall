using System.Collections.Generic;
using Fusion.Sockets;
using System;
using Fusion;
using UnityEngine;
using TMPro;

public class Spawner : SimulationBehaviour, INetworkRunnerCallbacks
{
    [Header("Assign a prefab that has NetworkObject + NetworkPlayer on root")]
    [SerializeField] private NetworkObject networkPlayerPrefab;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawned = new();

    // ===== Helpers =====
    private void SpawnFor(NetworkRunner runner, PlayerRef player)
    {
        // Already has a PlayerObject? Don't double-spawn.
        if (runner.GetPlayerObject(player) != null)
        {
            Debug.Log($"[Spawner] Player {player} already has PlayerObject, skip.");
            return;
        }

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        var obj = runner.Spawn(networkPlayerPrefab, pos, rot, player);
        runner.SetPlayerObject(player, obj);
        _spawned[player] = obj;

        Debug.Log($"[Spawner] Spawned player {player} → {obj.name}");
    }

    private void DespawnFor(NetworkRunner runner, PlayerRef player)
    {
        var obj = runner.GetPlayerObject(player);
        if (obj != null)
        {
            runner.Despawn(obj);
            Debug.Log($"[Spawner] Despawned player {player}");
        }
        _spawned.Remove(player);
    }

    // ===== INetworkRunnerCallbacks =====
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            var label = GameObject.Find("JoinCodeText")?.GetComponent<TMP_Text>();
            if (label != null) label.text = $"Code: {runner.SessionInfo.Name}";
            Debug.Log("[Spawner] OnPlayerJoined (host) → spawn player");
            SpawnFor(runner, player);
        }
        else
        {
            Debug.Log("[Spawner] OnPlayerJoined (client)");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
            DespawnFor(runner, player);
    }

    // Called after Runner.LoadScene completes
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[Spawner] OnSceneLoadDone → ensure all players are spawned");
        if (!runner.IsServer) return;

        // Re-spawn every active player after the scene swap
        foreach (var p in runner.ActivePlayers)
            SpawnFor(runner, p);
    }

    // ===== Unused callbacks required by the interface =====
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (NetworkPlayer.Local != null)
            input.Set(NetworkPlayer.Local.GetNetworkInput());
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    private void Awake()
    {
        var runner = FindObjectOfType<NetworkRunner>();
        if (runner) runner.AddCallbacks(this);
    }
}
