using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NormalGolfGameMultiplayerMod
{
    public class SteamWindSync : MonoBehaviour
    {
        private Vector2 m_Windvelocity = Vector2.zero;
        Vector2 m_NetworkedWindTarget;

        Vector2 m_NetworkedWind;

        private WindPanel windPanel;

        private uint m_CurrentNetworkTick = 0;

        private readonly byte[] _sendBuffer = new byte[13];

        float m_TickInterval = 1 / Globals.NetworkTickRate;
        float m_TickTimer = 0f;

        private void Start()
        {
            Debug.Log("[NormalGolfGameMultiplayer] Wind Sync Start");
            windPanel = GameObject.FindAnyObjectByType<WindPanel>();
        }

        private void Update()
        {
            if (!Globals.IsInLobby)
            {
                return;
            }
            if (!Globals.IsLobbyHost)
            {
                if (windPanel != null)
                {
                    windPanel.StopAllCoroutines();
                    windPanel.m_currentWind = Vector2.SmoothDamp(windPanel.m_currentWind, m_NetworkedWindTarget, ref m_Windvelocity, m_TickInterval);
                }
            }
            if (Globals.IsLobbyHost)
            {
                m_NetworkedWind = windPanel.m_currentWind;
                if (m_TickTimer >= m_TickInterval)
                {
                    m_TickTimer = 0f;
                    m_CurrentNetworkTick++;
                    SendStateToLobby(m_CurrentNetworkTick);
                }
                m_TickTimer += Time.deltaTime;
            }
        }

        private void SendStateToLobby(uint TickNumber)
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(Globals.m_CurrentLobbyID);
            if (memberCount <= 1) return;

            _sendBuffer[0] = 0;

            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedWind.x), 0, _sendBuffer, 1, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_NetworkedWind.y), 0, _sendBuffer, 5, 4);

            Buffer.BlockCopy(BitConverter.GetBytes(TickNumber), 0, _sendBuffer, 9, 4);

            GCHandle handle = GCHandle.Alloc(_sendBuffer, GCHandleType.Pinned);

            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                CSteamID mySteamId = SteamUser.GetSteamID();

                for (int i = 0; i < memberCount; i++)
                {
                    CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(Globals.m_CurrentLobbyID, i);


                    if (memberId != mySteamId)
                    {

                        SteamNetworkingIdentity targetIdentity = new SteamNetworkingIdentity();
                        targetIdentity.SetSteamID(memberId);

                        SteamNetworkingMessages.SendMessageToUser(
                            ref targetIdentity,
                            ptr,
                            (uint)_sendBuffer.Length,
                            Constants.k_nSteamNetworkingSend_Unreliable,
                            4
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
            float windX = BitConverter.ToSingle(packet, 1);
            float windY = BitConverter.ToSingle(packet, 5);
            Vector2 receivedWind = new Vector2(windX, windY);

            uint Tick = BitConverter.ToUInt32(packet, 9);

            SetWind(Tick, receivedWind);
        }
        private void SetWind(uint tick, Vector2 wind)
        {
            if (tick > m_CurrentNetworkTick)
            {
                m_NetworkedWindTarget = wind;
            }
        }
    }
}
