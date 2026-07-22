#if STEAMWORKS
using Steamworks;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace NormalGolfGameMultiplayerMod
{
    internal class SteamPlayerSender : MonoBehaviour
    {

        private CSteamID m_ObjOwnerSteamId;
        private CSteamID m_CurrentLobbyID;
        private CSteamID m_HostSteamId;
        private uint m_CurrentNetworkTick = 0;

        public bool IsLocalPlayer => m_ObjOwnerSteamId == SteamUser.GetSteamID();
        public bool IsHost => m_HostSteamId == SteamUser.GetSteamID();


        private readonly byte[] _sendBuffer = new byte[25];


        private Transform localPlayerTransform;

        private WindPanel windPanel;


        Vector2 m_NetworkedWind;
        Vector3 m_NetworkedPosition;

        Vector2 m_NetworkedWindTarget;
        Vector3 m_NetworkedPositionTarget;


        private Vector3 m_Posvelocity = Vector3.zero;
        private Vector2 m_Windvelocity = Vector2.zero;

        float m_TickInterval = 1 / Globals.NetworkTickRate;
        float m_TickTimer = 0f;



        public void SetPlayerData(CSteamID ObjOwnerID, CSteamID LobbyID, CSteamID HostId)
        {
            m_ObjOwnerSteamId = ObjOwnerID;
            m_CurrentLobbyID = LobbyID;
            m_HostSteamId = HostId;
        }

        void Start()
        {
            windPanel = GameObject.FindAnyObjectByType<WindPanel>();

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
                }
                else
                {
                    Shader gameShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (gameShader == null)
                    {
                        gameShader = Shader.Find("Standard");
                    }

                    if (gameShader != null)
                    {
                        Material newMat = new Material(gameShader);

                        if (newMat.HasProperty("_BaseColor"))
                            newMat.SetColor("_BaseColor", Color.orange);
                        else
                            newMat.SetColor("_Color", Color.orange);

                        renderer.material = newMat;
                    }
                    else
                    {
                        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Material defaultMat = tempCube.GetComponent<MeshRenderer>().sharedMaterial;
                        Destroy(tempCube);

                        Material instantiatedMat = new Material(defaultMat);
                        if (instantiatedMat.HasProperty("_BaseColor"))
                            instantiatedMat.SetColor("_BaseColor", Color.orange);
                        else
                            instantiatedMat.SetColor("_Color", Color.orange);

                        renderer.material = instantiatedMat;
                    }
                }
            }

            GameObject player = GameObject.Find("Valid Spot");
            localPlayerTransform = player.transform;

            if (windPanel != null && !IsHost)
            {
                windPanel.StopAllCoroutines();
            }
        }



        void Update()
        {
            if (IsHost)
            {
                m_NetworkedWind = windPanel.m_currentWind;
            }


            if (IsLocalPlayer)
            {
                m_NetworkedPosition = localPlayerTransform.position;
                if (m_TickTimer >= m_TickInterval)
                {
                    m_TickTimer = 0f;
                    m_CurrentNetworkTick++;
                    SendStateToLobby(m_CurrentLobbyID, m_CurrentNetworkTick);
                }
                transform.position = localPlayerTransform.position;
                m_TickTimer += Time.deltaTime;
            }
            else {
                transform.position = Vector3.SmoothDamp(transform.position, m_NetworkedPositionTarget, ref m_Posvelocity, m_TickInterval);

                windPanel.m_currentWind = Vector2.SmoothDamp(windPanel.m_currentWind, m_NetworkedWindTarget, ref m_Windvelocity, m_TickInterval);
            }
        }




        // Data Sending and Receiving --------------------------------------------------------------------------------------------------------------------------
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

            Buffer.BlockCopy(BitConverter.GetBytes(TickNumber), 0, _sendBuffer, 21, 4);

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

            uint Tick = BitConverter.ToUInt32(packet, 21);

            if (m_CurrentNetworkTick < Tick)
            {
                ApplyNetworkState(receivedPosition, receivedWind, Tick);
            }
        }

        private void ApplyNetworkState(Vector3 position, Vector2 wind, uint tick)
        {
            m_CurrentNetworkTick = tick;
            m_NetworkedPositionTarget = position;
            m_NetworkedWindTarget = wind;
        }

    }
}


#endif