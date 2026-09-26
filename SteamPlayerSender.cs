#if STEAMWORKS
using ECM2.Examples.FirstPerson;
using Fusion;
using Steamworks;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace NormalGolfGameMultiplayerMod
{
    public class SteamPlayerSender : MonoBehaviour
    {
        // Steamworks related fields -----------------------------------------------------------------------------------------------
        private CSteamID m_ObjOwnerSteamId;
        private CSteamID m_CurrentLobbyID;
        private CSteamID m_HostSteamId;
        private uint m_CurrentNetworkTick = 0;

        public bool IsLocalPlayer => m_ObjOwnerSteamId == SteamUser.GetSteamID();
        public bool IsHost => m_HostSteamId == SteamUser.GetSteamID();

        float m_TickInterval = 1 / Globals.NetworkTickRate;
        float m_TickTimer = 0f;
        //--------------------------------------------------------------------------------------------------------------------------


        // Data buffers for sending and receiving network data ---------------------------------------------------------------------
        private readonly byte[] _sendBuffer = new byte[29];
        private readonly byte[] _soundBuffer = new byte[2];
        //--------------------------------------------------------------------------------------------------------------------------

        // Player transforms -------------------------------------------------------------------------------------------------------
        private Transform localPlayerTransform;

        // This uses 2 rotation transforms because the first person character is disabled when the player is in the golfing menu. See GetPlayerRot()
        private Transform localPlayerTransformforROT;
        private Transform localPlayerTransformforROT1;
        //--------------------------------------------------------------------------------------------------------------------------

        // Wind
        private WindPanel windPanel;

        // Networked variables -----------------------------------------------------------------------------------------------------
        Vector2 m_NetworkedWind;
        Vector3 m_NetworkedPosition;
        Vector3 m_NetworkedRot;
        //--------------------------------------------------------------------------------------------------------------------------

        Vector2 m_NetworkedWindTarget;
        Vector3 m_NetworkedPositionTarget;


        private Vector3 m_Posvelocity = Vector3.zero;
        private Vector2 m_Windvelocity = Vector2.zero;

        public void SetPlayerData(CSteamID ObjOwnerID, CSteamID LobbyID, CSteamID HostId)
        {
            m_ObjOwnerSteamId = ObjOwnerID;
            m_CurrentLobbyID = LobbyID;
            m_HostSteamId = HostId;
        }

        void Start()
        {
            if (IsLocalPlayer)
            {
                Globals.LocalPlayerSender = this;
            }

            windPanel = GameObject.FindAnyObjectByType<WindPanel>();
            if (windPanel != null && !IsHost)
            {
                windPanel.StopAllCoroutines();
            }


            if (IsLocalPlayer)
            {
                if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                {
                    meshRenderer.enabled = false;
                }

                if (TryGetComponent<Collider>(out var collider))
                {
                    collider.enabled = false;
                }
                transform.Find("Plane").gameObject.SetActive(false);
                transform.Find("Plane (1)").gameObject.SetActive(false);
            }
            else
            {
                if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                {
                    meshRenderer.enabled = false;
                }
            }

            GameObject player = GameObject.Find("Valid Spot");
            localPlayerTransform = player.transform;



            GameObject ballPosition = GameObject.Find("BallPosition");
            if (ballPosition)
            {
                localPlayerTransformforROT1 = ballPosition.transform;
            }

            GameObject FPC = GameObject.Find("First Person Character");
            if (FPC)
            {
                localPlayerTransformforROT = FPC.transform;
            }
        }

        Vector3 GetPlayerRot()
        {
            if (!localPlayerTransformforROT)
            {
                GameObject FPC = GameObject.Find("First Person Character");
                if (FPC)
                {
                    localPlayerTransformforROT = FPC.transform;
                }
            }
            if (localPlayerTransformforROT && !localPlayerTransformforROT.gameObject.activeInHierarchy)
            {
                if (!localPlayerTransformforROT1)
                {
                    GameObject ballPosition = GameObject.Find("BallPosition");
                    if (ballPosition)
                    {
                        localPlayerTransformforROT1 = ballPosition.transform;
                    }

                    return localPlayerTransformforROT.rotation.eulerAngles;
                }
                return localPlayerTransformforROT1.rotation.eulerAngles;

            } else
            {
                return localPlayerTransformforROT.rotation.eulerAngles;
            }
            return Vector3.zero;
        }

        void Update()
        {
            if (IsHost)
            {
                m_NetworkedWind = windPanel.m_currentWind;
            }


            if (IsLocalPlayer)
            {

                m_NetworkedRot = GetPlayerRot();

                m_NetworkedPosition = localPlayerTransform.position;
                if (m_TickTimer >= m_TickInterval)
                {
                    m_TickTimer = 0f;
                    m_CurrentNetworkTick++;
                    SendStateToLobby(m_CurrentLobbyID, m_CurrentNetworkTick);
                }
                transform.position = localPlayerTransform.position;
                transform.eulerAngles = GetPlayerRot();
                m_TickTimer += Time.deltaTime;
            }
            else {
                transform.position = Vector3.SmoothDamp(transform.position, m_NetworkedPositionTarget, ref m_Posvelocity, m_TickInterval);

                transform.eulerAngles = m_NetworkedRot;
                windPanel.m_currentWind = Vector2.SmoothDamp(windPanel.m_currentWind, m_NetworkedWindTarget, ref m_Windvelocity, m_TickInterval);
            }
        }


        // Data Sending and Receiving ----------------------------------------------------------------------------------------------
        private void SendStateToLobby(CSteamID lobbyId, uint TickNumber)
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            if (memberCount <= 1) return;

            _sendBuffer[0] = 1;

            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedPosition.x), 0, _sendBuffer, 1, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedPosition.y), 0, _sendBuffer, 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedPosition.z), 0, _sendBuffer, 9, 4);

            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedWind.x), 0, _sendBuffer, 13, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedWind.y), 0, _sendBuffer, 17, 4);

            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedRot.y), 0, _sendBuffer, 21, 4);

            Buffer.BlockCopy(BitConverter.GetBytes(TickNumber), 0, _sendBuffer, 25, 4);

            GCHandle handle = GCHandle.Alloc(_sendBuffer, GCHandleType.Pinned);

            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                CSteamID mySteamId = SteamUser.GetSteamID();

                for (int i = 0; i < memberCount; i++)
                {
                    CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);


                    if (memberId != mySteamId)
                    {

                        SteamNetworkingIdentity targetIdentity = new SteamNetworkingIdentity();
                        targetIdentity.SetSteamID(memberId);

                        SteamNetworkingMessages.SendMessageToUser(
                            ref targetIdentity,
                            ptr,
                            (uint)_sendBuffer.Length,
                            Constants.k_nSteamNetworkingSend_Unreliable,
                            0
                        );
                    }
                }
            }
            finally
            {

                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }


        public void UnpackStatePayload(byte[] packet)
        {
            float posX = BitConverter.ToSingle(packet, 1);
            float posY = BitConverter.ToSingle(packet, 5);
            float posZ = BitConverter.ToSingle(packet, 9);
            Vector3 receivedPosition = new Vector3(posX, posY, posZ);


            float windX = BitConverter.ToSingle(packet, 13);
            float windY = BitConverter.ToSingle(packet, 17);
            Vector2 receivedWind = new Vector2(windX, windY);


            float PlayerY = BitConverter.ToSingle(packet, 21);

            uint Tick = BitConverter.ToUInt32(packet, 25);

            if (m_CurrentNetworkTick < Tick)
            {
                ApplyNetworkState(receivedPosition, receivedWind, Tick, PlayerY);
            }
        }

        private void ApplyNetworkState(Vector3 position, Vector2 wind, uint tick, float PlayerY)
        {
            m_CurrentNetworkTick = tick;
            m_NetworkedPositionTarget = position;
            m_NetworkedWindTarget = wind;
            m_NetworkedRot = new Vector3(0, PlayerY, 0);
        }
        //--------------------------------------------------------------------------------------------------------------------------



        // Sound Sending and Receiving ---------------------------------------------------------------------------------------------
        public void UnpackSoundPayload(byte[] packet)
        {
            Debug.Log("[NormalGolfGameMultiplayer] Received sound packet with ID: " + packet[1]);
            byte soundID = packet[1];
            PlayPlayerSound(soundID);
        }

        public void PlayPlayerSound(byte soundID)
        {
            Sound sound = Globals.PlayerSounds[soundID];
            Debug.Log("[NormalGolfGameMultiplayer] Playing sound: " + sound.m_name);
            switch (sound.m_name)
            {
                case "gong":
                    PlaySoundLocaly(sound, false); 
                    break;
                case "restart":
                    PlaySoundLocaly(sound);
                    break;
                case "golf_in":
                    PlaySoundLocaly(sound);
                    break;
                case "golf_out":
                    PlaySoundLocaly(sound);
                    break;
                case "cash":
                    PlaySoundLocaly(sound);
                    break;
                case "treeHit": // Ideally this should be played on the GhostBall, but I don't feel like coding that right now.
                    PlaySoundLocaly(sound);
                    break;
                case "ballInHole": // Ideally this should be played on the GhostBall, but I don't feel like coding that right now.
                    PlaySoundLocaly(sound);
                    break;
                case "splash": // Ideally this should be played on the GhostBall, but I don't feel like coding that right now. (this one is 2d bc it might sound better)
                    PlaySoundLocaly(sound, false);
                    break;
                case "error":
                    PlaySoundLocaly(sound);
                    break;
                case "ironHit":
                    PlaySoundLocaly(sound);
                    break;
                case "driverHit":
                    PlaySoundLocaly(sound);
                    break;
                case "hybridHit":
                    PlaySoundLocaly(sound);
                    break;
                case "putterHit":
                    PlaySoundLocaly(sound);
                    break;
                case "ironSwing":
                    PlaySoundLocaly(sound);
                    break;
                case "driverSwing":
                    PlaySoundLocaly(sound);
                    break;
                case "skim":
                    PlaySoundLocaly(sound, false);
                    break;
                case "par":
                    PlaySoundLocaly(sound, false);
                    break;
                case "birdie":
                    PlaySoundLocaly(sound, false);
                    break;
                case "eagle":
                    PlaySoundLocaly(sound, false);
                    break;
                case "doublebogey":
                    PlaySoundLocaly(sound, false);
                    break;
                case "bogey":
                    PlaySoundLocaly(sound, false);
                    break;
                case "teleStart":
                    PlaySoundLocaly(sound);
                    break;
                case "teleFinish":
                    PlaySoundLocaly(sound);
                    break;
                case "elevatorDing":
                    PlaySoundLocaly(sound);
                    break;
                case "elevatorDoor":
                    PlaySoundLocaly(sound);
                    break;
                case "padlockBreak":
                    PlaySoundLocaly(sound);
                    break;
                case "mulligan":
                    PlaySoundLocaly(sound);
                    break;
                case "serverCut":
                    PlaySoundLocaly(sound);
                    break;
                case "rangeFinderIn":
                    PlaySoundLocaly(sound);
                    break;
                case "rangeFinderOut":
                    PlaySoundLocaly(sound);
                    break;
                default:
                    return;
            }
        }

        private void PlaySoundLocaly(Sound sound, bool is3d = true)
        {
            if (is3d)
            {
                sound.m_source.transform.position = gameObject.transform.position;
            }

            sound.m_source.spatialBlend = is3d ? 1f : 0f;

            sound.m_source.rolloffMode = AudioRolloffMode.Linear;
            sound.m_source.minDistance = 1;
            sound.m_source.maxDistance = 30;
            // This should be multipied by m_SFXMasterLevelTweak but I can't access that from here.
            sound.m_source.volume = UnityEngine.Random.Range(sound.m_minVolume, sound.m_maxVolume);

            sound.m_source.pitch = UnityEngine.Random.Range(sound.m_minPitch, sound.m_maxPitch);
            sound.m_source.PlayOneShot(sound.m_clip);
        }

        public void SendPlayerSoundToLobby(byte soundID)
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(m_CurrentLobbyID);
            if (memberCount <= 1) return;

            _soundBuffer[0] = 1;
            _soundBuffer[1] = soundID;


            GCHandle handle = GCHandle.Alloc(_soundBuffer, GCHandleType.Pinned);

            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                CSteamID mySteamId = SteamUser.GetSteamID();

                for (int i = 0; i < memberCount; i++)
                {
                    CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(m_CurrentLobbyID, i);


                    if (memberId != mySteamId)
                    {

                        SteamNetworkingIdentity targetIdentity = new SteamNetworkingIdentity();
                        targetIdentity.SetSteamID(memberId);

                        SteamNetworkingMessages.SendMessageToUser(
                            ref targetIdentity,
                            ptr,
                            (uint)_soundBuffer.Length,
                            Constants.k_nSteamNetworkingSend_Reliable,
                            2
                        );
                    }
                }
            }
            finally
            {

                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }
        //--------------------------------------------------------------------------------------------------------------------------

    }
}


#endif