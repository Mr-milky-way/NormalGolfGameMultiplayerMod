#if FUSION
using Fusion;
using UnityEngine;

namespace NormalGolfGameMultiplayerMod
{
    public class BallModSpawner : MonoBehaviour
    {
        private static NetworkObjectBaker _baker;
        private static NetworkObjectBaker Baker => _baker ??= new NetworkObjectBaker();


        private NetworkPrefabId _moddedPrefabId;
        public bool _isPrefabRegistered = false;

        public void LoadAndRegisterPrefab(NetworkRunner runner, string assetName)
        {
            if (_isPrefabRegistered) return;


            AssetBundle bundle = Globals.Bundle;
            if (bundle == null)
            {
                Debug.LogError("[NormalGolfGameMultiplayer] Failed to load AssetBundle!");
                return;
            }

            GameObject rawPrefab = bundle.LoadAsset<GameObject>(assetName);
            if (rawPrefab == null)
            {
                Debug.LogError($"[NormalGolfGameMultiplayer] Asset '{assetName}' not found in bundle.");
                bundle.Unload(false);
                return;
            }


            NetworkObject no = rawPrefab.GetComponent<NetworkObject>();
            if (no == null)
            {
                no = rawPrefab.AddComponent<NetworkObject>();
            }

            PlayerBallPosSender pbps = rawPrefab.GetComponent<PlayerBallPosSender>();
            if (pbps == null)
            {
                pbps = rawPrefab.AddComponent<PlayerBallPosSender>();
            }

            Baker.Bake(rawPrefab);


            ModPrefabSource customSource = new ModPrefabSource(no);





            if (runner.Config.PrefabTable.TryAddSource(customSource, out _moddedPrefabId))
            {
                _isPrefabRegistered = true;
                Debug.Log($"[NormalGolfGameMultiplayer] Successfully registered runtime prefab. ID: {_moddedPrefabId}");
            }
            else
            {
                Debug.LogError("[NormalGolfGameMultiplayer] Fusion rejected the dynamic prefab source registration.");
            }

        }


        public void SpawnModdedObject(NetworkRunner runner, Vector3 position, Quaternion rotation)
        {
            try
            {
                if (!_isPrefabRegistered)
                {
                    Debug.LogError("[NormalGolfGameMultiplayer] Cannot spawn. Prefab registration has not completed.");
                    return;
                }


                if (runner.GameMode == GameMode.Shared)
                {
                    Debug.Log("[NormalGolfGameMultiplayer] Shared Mode detected. Proceeding with client-side spawn.");
                }
                else if (!runner.IsServer)
                {
                    Debug.LogWarning("[NormalGolfGameMultiplayer] Client ignored spawn request. Only Host/Server can spawn in this mode.");
                    return;
                }


                runner.Spawn(
                    _moddedPrefabId,
                    position: position,
                    rotation: rotation,
                    inputAuthority: runner.LocalPlayer,
                    onBeforeSpawned: (runnerInstance, obj) =>
                    {
                        Debug.Log("[NormalGolfGameMultiplayer] Network instantiation complete. Initializing pre-spawn state.");
                        PlayerBallPosSender pbps = obj.GetComponent<PlayerBallPosSender>();
                        if (pbps == null)
                        {
                            Debug.LogError("[NormalGolfGameMultiplayer] PlayerBallPosSender component missing on spawned object.");
                        }
                    }

                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NormalGolfGameMultiplayer] Exception during spawn: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}


#endif