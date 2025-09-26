using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Voice.Unity;
using Photon.Realtime;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    private NetworkRunner _runner;

    // This is the public function our UI will call
    public async Task StartGame(GameMode mode, string sessionName)
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        _runner.AddCallbacks(this);

        var startGameArgs = new StartGameArgs()
        {
            GameMode = mode,
            SessionName = sessionName,
            PlayerCount = 4,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        };

        var result = await _runner.StartGame(startGameArgs);

        if (result.Ok)
        {
            // >>> Link Photon Voice to the SAME ROOM as Fusion <<<
            await EnsureVoiceLinkedToFusionAsync(sessionName);

            if (_runner.IsServer)
            {
                _runner.LoadScene("GameScene");
            }
        }
        else
        {
            Debug.LogError($"Failed to Start Game: {result.ShutdownReason}");
        }
    }


    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var myInput = new NetworkInputData();

        myInput.direction = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        myInput.IsJumpPressed = Input.GetKey(KeyCode.Space);

        input.Set(myInput);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            var obj = runner.Spawn(_playerPrefab, new Vector3(0, 18, 0), Quaternion.identity, player);
            // Map this NetworkObject as the player's object so all peers can look it up.
            runner.SetPlayerObject(player, obj);
        }
    }


    private async Task EnsureVoiceLinkedToFusionAsync(string fusionRoomName)
    {
        // Connect Voice if needed
        if (!PhotonVoiceNetwork.Instance.Client.IsConnected)
        {
            PhotonVoiceNetwork.Instance.AutoConnectAndJoin = false; // we’ll control it
            PhotonVoiceNetwork.Instance.ConnectUsingSettings();      // uses Voice AppID in PhotonServerSettings
            // wait until connected
            while (!PhotonVoiceNetwork.Instance.Client.IsConnected)
                await Task.Yield();
        }

        // Join same room as Fusion
        if (!PhotonVoiceNetwork.Instance.Client.InRoom ||
            PhotonVoiceNetwork.Instance.Client.CurrentRoom?.Name != fusionRoomName)
        {
            PhotonVoiceNetwork.Instance.Client.OpJoinOrCreateRoom(
                new EnterRoomParams { RoomName = fusionRoomName });
            // wait until in room
            while (!PhotonVoiceNetwork.Instance.Client.InRoom)
                await Task.Yield();
        }
    }

    private void DisconnectVoice()
    {
        if (PhotonVoiceNetwork.Instance.Client.IsConnected)
        {
            if (PhotonVoiceNetwork.Instance.Client.InRoom)
                PhotonVoiceNetwork.Instance.Client.OpLeaveRoom(false);
            PhotonVoiceNetwork.Instance.Disconnect();
        }
    }


    // --- All other required empty functions ---
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}