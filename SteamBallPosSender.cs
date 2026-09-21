#if STEAMWORKS
using Steamworks;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NormalGolfGameMultiplayerMod
{
    public class SteamBallPosSender : MonoBehaviour
    {
        // Steamworks related fields -----------------------------------------------------------------------------------------------
        private CSteamID m_ObjOwnerSteamId;
        private CSteamID m_CurrentLobbyID;
        private CSteamID m_HostSteamId;
        private uint m_CurrentNetworkTick = 0;

        private bool IsLocalPlayer => m_ObjOwnerSteamId == SteamUser.GetSteamID();
        private bool IsHost => m_HostSteamId == SteamUser.GetSteamID();

        private float m_TickInterval = 1 / Globals.NetworkTickRate;
        private float m_TickTimer = 0f;
        //--------------------------------------------------------------------------------------------------------------------------


        // Data buffers for sending and receiving network data ---------------------------------------------------------------------
        private readonly byte[] _sendBuffer = new byte[21];
        private readonly byte[] _soundBuffer = new byte[6];
        //--------------------------------------------------------------------------------------------------------------------------

        private Vector3 m_NetworkedPosition;


        private Vector3 m_NetworkedPositionTarget;
        private Vector3 m_Velocity = Vector3.zero;


        public int CurrentShotNumber = 0;


        private Transform m_LocalBallTransform;

        private Ball PlayerBall;


        // These should be set in the inspector, but I have to get them in Start() because this is a mod
        [SerializeField] private TrailRenderer tr;
        [SerializeField] private AudioSource m_audioSource;


        public void SetPlayerData(CSteamID ObjOwnerID, CSteamID LobbyID, CSteamID HostId)
        {
            m_ObjOwnerSteamId = ObjOwnerID;
            m_CurrentLobbyID = LobbyID;
            m_HostSteamId = HostId;
        }


        void Start()
        {
            m_audioSource = gameObject.GetComponent<AudioSource>();
            if (IsLocalPlayer)
            {
                Globals.LocalBallSender = this;
            }
            if (TryGetComponent<MeshRenderer>(out var renderer))
            {
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
                    transform.GetChild(0).gameObject.SetActive(false);
                }
                else
                {
                    tr = transform.GetChild(0).GetComponent<TrailRenderer>();
                }
            }

            Ball player = GameObject.FindAnyObjectByType<Ball>();
            PlayerBall = player;
            Debug.Log($"[NormalGolfGameMultiplayer] Found Ball: {player.name}");
            m_LocalBallTransform = player.transform;
        }

        void Update()
        {
            if (IsLocalPlayer)
            {
                CurrentShotNumber = PlayerBall.m_currentShot;
                m_NetworkedPosition = m_LocalBallTransform.position;
                if (m_TickTimer >= m_TickInterval)
                {
                    m_TickTimer = 0f;
                    m_CurrentNetworkTick++;
                    SendStateToLobby(m_CurrentLobbyID, m_CurrentNetworkTick);
                }
                transform.position = m_LocalBallTransform.position;
                m_TickTimer += Time.deltaTime;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, m_NetworkedPositionTarget, ref m_Velocity, m_TickInterval);
            }
        }

        // Data Sending and Receiving ----------------------------------------------------------------------------------------------
        private void SendStateToLobby(CSteamID lobbyId, uint TickNumber)
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            if (memberCount <= 1) return;

            _sendBuffer[0] = 2;

            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedPosition.x), 0, _sendBuffer, 1, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedPosition.y), 0, _sendBuffer, 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedPosition.z), 0, _sendBuffer, 9, 4);

            Buffer.BlockCopy(BitConverter.GetBytes(CurrentShotNumber), 0, _sendBuffer, 13, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(TickNumber), 0, _sendBuffer, 17, 4);

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

            int CurrentShot = BitConverter.ToInt32(packet, 13);
            uint Tick = BitConverter.ToUInt32(packet, 17);

            if (m_CurrentNetworkTick < Tick)
            {
                ApplyNetworkState(receivedPosition, Tick, CurrentShot);
            }
        }

        private void ApplyNetworkState(Vector3 position, uint tick, int Shot)
        {
            /*
            if (CurrentShotNumber != Shot)
            {
                if (SaveManager.instance.m_gamemodeState.mode == GameMode.JustGolf)
                {
                    SteamNetworkManager.instance.CheckDisableBalls();
                }
            }
            */
            bool needtoClear = false;
            if (Vector3.Distance(position, m_NetworkedPositionTarget) > 10)
            {
                needtoClear = true;
            }
            CurrentShotNumber = Shot;
            m_CurrentNetworkTick = tick;
            m_NetworkedPositionTarget = position;

            if (needtoClear)
            {
                transform.position = m_NetworkedPositionTarget;
                tr.Clear();
            }
        }
        //--------------------------------------------------------------------------------------------------------------------------

        /* Not in use
        public void HideBall(bool yes = true)
        {
            if (m_LocalBallTransform)
                m_LocalBallTransform.gameObject.SetActive(!yes);
        }
        */




        // Sound Sending and Receiving ---------------------------------------------------------------------------------------------
        public void UnpackSoundPayload(byte[] packet)
        {
            byte soundID = packet[1];
            float impact = BitConverter.ToSingle(packet, 2);
            PlayBallSound(soundID, impact);
        }

        public void PlayBallSound(byte soundID, float impact)
        {
            BallCollisionSounds ballCollisionSounds = Globals.BallSounds[soundID];
            m_audioSource.resource = ballCollisionSounds.m_clip;
            m_audioSource.volume = Mathf.Clamp01(impact / 30f) * ballCollisionSounds.m_volumeMult;
            m_audioSource.pitch = UnityEngine.Random.Range(ballCollisionSounds.m_pitchRange.x, ballCollisionSounds.m_pitchRange.y);
            m_audioSource.Play();
        }

        public void SendBallSoundToLobby(byte soundID, float impact)
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(m_CurrentLobbyID);
            if (memberCount <= 1) return;

            _soundBuffer[0] = 0;
            _soundBuffer[1] = soundID;

            Buffer.BlockCopy(BitConverter.GetBytes(impact), 0, _soundBuffer, 2, 4);

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