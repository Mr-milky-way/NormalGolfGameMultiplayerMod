#if STEAMWORKS
using Steamworks;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NormalGolfGameMultiplayerMod
{
    public class SteamBallPosSender : MonoBehaviour
    {

        private CSteamID m_ObjOwnerSteamId;
        private CSteamID m_CurrentLobbyID;
        private CSteamID m_HostSteamId;
        private uint m_CurrentNetworkTick = 0;

        private bool IsLocalPlayer => m_ObjOwnerSteamId == SteamUser.GetSteamID();
        private bool IsHost => m_HostSteamId == SteamUser.GetSteamID();


        private readonly byte[] _sendBuffer = new byte[17];

        private Vector3 m_NetworkedPosition;


        private Vector3 m_NetworkedPositionTarget;
        private Vector3 m_Velocity = Vector3.zero;


        private float m_TickInterval = 1 / Globals.NetworkTickRate;
        private float m_TickTimer = 0f;

        
        private Transform m_LocalBallTransform;


        public void SetPlayerData(CSteamID ObjOwnerID, CSteamID LobbyID, CSteamID HostId)
        {
            m_ObjOwnerSteamId = ObjOwnerID;
            m_CurrentLobbyID = LobbyID;
            m_HostSteamId = HostId;
        }


        void Start()
        {
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
                    renderer.material.color = Color.red;
                }
            }

            Ball player = GameObject.FindAnyObjectByType<Ball>();
            Debug.Log($"[NormalGolfGameMultiplayer] Found Ball: {player.name}");
            m_LocalBallTransform = player.transform;
        }

        void Update()
        {
            if (IsLocalPlayer)
            {
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


        // Data Sending and Receiving --------------------------------------------------------------------------------------------------------------------------
        private void SendStateToLobby(CSteamID lobbyId, uint TickNumber)
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            if (memberCount <= 1) return;

            _sendBuffer[0] = 2;

            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedPosition.x), 0, _sendBuffer, 1, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedPosition.y), 0, _sendBuffer, 5, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedPosition.z), 0, _sendBuffer, 9, 4);

            Buffer.BlockCopy(BitConverter.GetBytes(TickNumber), 0, _sendBuffer, 13, 4);

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

            uint Tick = BitConverter.ToUInt32(packet, 13);

            if (m_CurrentNetworkTick < Tick)
            {
                ApplyNetworkState(receivedPosition, Tick);
            }
        }

        private void ApplyNetworkState(Vector3 position, uint tick)
        {
            m_CurrentNetworkTick = tick;
            m_NetworkedPositionTarget = position;
        }
    }
}


#endif