#if FUSION
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NormalGolfGameMultiplayerMod
{
    public class MainMultiThingy : MonoBehaviour, INetworkRunnerCallbacks
    {


        private bool _showMenu = false;
        private string _roomName = "Room123";


        void Awake()
        {
            _modSpawner = gameObject.AddComponent<ModSpawner>();
            BallSpawner = gameObject.AddComponent<BallModSpawner>();
        }


        ModSpawner _modSpawner;
        BallModSpawner BallSpawner;
        private NetworkPrefabId _moddedPrefabId;
        private bool _isPrefabRegistered = false;

        Dictionary<int, GameObject> remotePlayers = new Dictionary<int, GameObject>();

        public struct ModPrefabResult
        {
            public GameObject PrefabObject;
            public NetworkPrefabId PrefabId;
        }

        private NetworkRunner _runner;

        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[NormalGolfGameMultiplayer] Player joined callback triggered for ID: {player.PlayerId}");

            if (player == runner.LocalPlayer)
            {
                try
                {
                    if (!_modSpawner._isPrefabRegistered)
                    {
                        _modSpawner.LoadAndRegisterPrefab(runner, "Capsule");
                    }

                    if (!BallSpawner._isPrefabRegistered)
                    {
                        BallSpawner.LoadAndRegisterPrefab(runner, "FakeBall");
                    }

                    Debug.Log("[NormalGolfGameMultiplayer] Local player joined room. Spawning mod object...");
                    _modSpawner.SpawnModdedObject(runner, new Vector3(0, 1, 0), Quaternion.identity);
                    BallSpawner.SpawnModdedObject(runner, new Vector3(0, 1, 0), Quaternion.identity);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NormalGolfGameMultiplayer] Fatal exception thrown inside Spawn block: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log("[NormalGolfGameMultiplayer] Player left");
            if (remotePlayers.TryGetValue(player.PlayerId, out GameObject playerObj))
            {
                remotePlayers.Remove(player.PlayerId);
            }
        }

        void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.LogError($"[NormalGolfGameMultiplayer] RUNNER SHUTDOWN! Reason: {shutdownReason}");
            _isPrefabRegistered = false;
        }
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"[NormalGolfGameMultiplayer] CONNECTION FAILED TO {remoteAddress}! Reason: {reason}");
        }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
        {
            if (!_modSpawner._isPrefabRegistered)
            {
                _modSpawner.LoadAndRegisterPrefab(runner, "Capsule");
            }
            if (!BallSpawner._isPrefabRegistered)
            {
                BallSpawner.LoadAndRegisterPrefab(runner, "FakeBall");
            }
        }

        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        async void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                _showMenu = !_showMenu;
            }
        }

        private void OnGUI()
        {
            if (!_showMenu || _runner != null) return;


            GUI.Box(new Rect(10, 10, 220, 190), "Network Menu (F1)");


            GUI.Label(new Rect(20, 40, 200, 20), "Room Name:");
            _roomName = GUI.TextField(new Rect(20, 60, 200, 30), _roomName);


            if (GUI.Button(new Rect(20, 100, 200, 40), "Host Game"))
            {
                _showMenu = false;
                StartGame(GameMode.Shared, _roomName);
            }

            if (GUI.Button(new Rect(20, 140, 200, 40), "Join Game"))
            {
                _showMenu = false;
                StartGame(GameMode.Shared, _roomName);
            }
        }

        public async Task StartGame(GameMode mode, string roomName)
        {
            if (_runner != null && _runner.IsRunning)
                return;

            if (Globals.Bundle == null)
            {
                Debug.LogError("[NormalGolfGameMultiplayer] AssetBundle failed to load");
                return;
            }

            NetworkProjectConfigAsset configAsset = Globals.Bundle.LoadAsset<NetworkProjectConfigAsset>("NetworkProjectConfig.fusion");
            if (configAsset == null)
            {
                Debug.LogError("[NormalGolfGameMultiplayer] NetworkProjectConfigAsset not found in bundle");
                return;
            }

            NetworkProjectConfig networkProjectConfig = configAsset.Config;


            var go = new GameObject("NetworkRunner");
            _runner = go.AddComponent<NetworkRunner>();
            DontDestroyOnLoad(go);

            _runner.AddCallbacks(this);
            _runner.ProvideInput = true;

            var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (scene.IsValid)
            {
                sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
            }

            var appSettings = new FusionAppSettings
            {
                AppIdFusion = "52fec629-3af2-437d-82ca-cf2868e4b07d",

                
                UseNameServer = true,

                
                FixedRegion = "us",

                
                AppVersion = "1.0.0-Mod"
            };



            var args = new StartGameArgs()
            {
                GameMode = mode,

                SessionName = roomName,
                CustomPhotonAppSettings = appSettings,
                Config = networkProjectConfig,
                Address = NetAddress.Any(),
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
                Scene = scene
            };

            try
            {
                Debug.Log($"[NormalGolfGameMultiplayer] Attempting to start Fusion game as {mode}...");

                StartGameResult result = await _runner.StartGame(args);

                if (result.Ok)
                {
                    Debug.Log($"[NormalGolfGameMultiplayer] Fusion {mode} started successfully! Session: {args.SessionName}");
                }
                else
                {

                    Debug.LogError($"[NormalGolfGameMultiplayer] Fusion failed to start game loop! Error details: {result.ShutdownReason} - {result.ErrorMessage}");

                    Destroy(go);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NormalGolfGameMultiplayer] Fatal exception during Fusion {mode} startup await block: {ex.Message}\n{ex.StackTrace}");
                if (go != null) Destroy(go);
                throw;
            }
        }
    }
}

#endif