using Fusion;
using Fusion.Sockets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using UnityEngine.SceneManagement;
using System.Linq;
using TMPro;

public class NetworkRunnerHandler : MonoBehaviour
{
    [SerializeField]
    TMPro.TMP_Text joinErrorLabel;

    [SerializeField]
    NetworkRunner networkRunnerPrefab;

    NetworkRunner networkRunner;

    void Awake()
    {
        networkRunner = FindObjectOfType<NetworkRunner>();
    }


    // Start is called before the first frame update
    void Start()
    {
        if (networkRunner == null)
        {
            networkRunner = Instantiate(networkRunnerPrefab);
            networkRunner.name = "Network runner";
            DontDestroyOnLoad(networkRunner.gameObject);
        }

        Utils.DebugLog("InitializeNetworkRunner called");

    }

    INetworkSceneManager GetSceneManager(NetworkRunner runner)
    {
        INetworkSceneManager sceneManager = runner.GetComponents(typeof(MonoBehaviour)).OfType<INetworkSceneManager>().FirstOrDefault();

        if (sceneManager == null)
        {
            //Handle networked objects that already exits in the scene
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        return sceneManager;
    }

    protected virtual Task InitializeNetworkRunner(NetworkRunner networkRunner, GameMode gameMode, string sessionName, NetAddress address, SceneRef scene, Action<NetworkRunner> initialized)
    {
        INetworkSceneManager sceneManager = GetSceneManager(networkRunner);

        networkRunner.ProvideInput = true;

        var spawner = FindObjectOfType<Spawner>();
        if (spawner) networkRunner.AddCallbacks(spawner);


        return networkRunner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            Address = address,
            Scene = scene,
            SessionName = sessionName,
            CustomLobbyName = "OurLobbyID",
            SceneManager = sceneManager
        });
    }

        string MakeCode(int len = 6) {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // no O/0/I/1
        var rng = new System.Random();
        var s = new char[len];
        for (int i = 0; i < len; i++) s[i] = chars[rng.Next(chars.Length)];
        return new string(s);
        }

        public async void HostGame()
    {
            var code = MakeCode();
            if (!networkRunner) networkRunner = Instantiate(networkRunnerPrefab);
            networkRunner.name = "Network Runner";
            DontDestroyOnLoad(networkRunner.gameObject);
            networkRunner.ProvideInput = true;

            var sceneRef = SceneRef.FromIndex(1);     // Game scene index
            var sceneMgr = GetSceneManager(networkRunner); // you already have this
            // Make sure callbacks are added (Spawner will also self-register, see below)
            var spawner = FindObjectOfType<Spawner>();
            if (spawner) networkRunner.AddCallbacks(spawner);

            await networkRunner.StartGame(new StartGameArgs{
                GameMode        = GameMode.Host,
                SessionName     = code,
                CustomLobbyName = "CodeLobby",
                Address         = NetAddress.Any(),
                Scene           = sceneRef,
                SceneManager    = sceneMgr
            });
        }

    public async void JoinGame(string code)
    {
        code = new string((code ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (code.Length != 6)
        {
            Debug.Log("Invalid or blank code.");
            return;
        }

        //If we're already in a session, leave first (prevents weird auto-joins)
        if (networkRunner && networkRunner.IsRunning)
            await networkRunner.Shutdown();

        //Ensure runner exists
        if (!networkRunner)
        {
            networkRunner = Instantiate(networkRunnerPrefab);
            networkRunner.name = "Network Runner";
            DontDestroyOnLoad(networkRunner.gameObject);
        }

        networkRunner.ProvideInput = true;

        await networkRunner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = code,
            CustomLobbyName = "CodeLobby",
            Address = NetAddress.Any(),
            SceneManager = GetSceneManager(networkRunner)
        });
    }


}
