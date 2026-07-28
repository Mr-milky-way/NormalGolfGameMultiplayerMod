using Steamworks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace NormalGolfGameMultiplayerMod
{
    public class SteamSaveObjectSync: MonoBehaviour
    {

        private CSteamID m_ObjOwnerSteamId;
        public CSteamID m_CurrentLobbyID;
        public CSteamID m_HostSteamId;

        public bool IsLocalPlayer => m_ObjOwnerSteamId == SteamUser.GetSteamID();
        public bool IsHost => m_HostSteamId == SteamUser.GetSteamID();


        private readonly byte[] _sendBuffer = new byte[22];

        float m_TickTimer = 0f;
        float m_sendRate = 1;
        public bool m_NeedsSending = false;

        SaveableObject[] Objects;

        int NumberOfObjects = -1;

        void Start()
        {
            Objects = GetSynchronizedArray();
            NumberOfObjects = Objects.Length;
            m_sendRate = 3f / NumberOfObjects;
        }

        int index = 0;


        void Update()
        {
            if (IsHost && m_NeedsSending)
            {
                if (m_TickTimer >= m_sendRate)
                {
                    if (NumberOfObjects != -1)
                    {
                        if (index < NumberOfObjects)
                        {
                            SendStateToLobby(m_CurrentLobbyID, index, Objects[index].gameObject.activeSelf, Objects[index].transform.rotation);
                            index++;
                        }
                        else
                        {
                            index = 0;
                            m_NeedsSending = false;
                        }
                    }
                    m_TickTimer = 0;
                }
            }
            m_TickTimer += Time.deltaTime;
        }


        private void SendStateToLobby(CSteamID lobbyId, int ObjectNumber, bool m_active, Quaternion m_rotation)
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            if (memberCount <= 1) return;

            _sendBuffer[0] = 1;

            Buffer.BlockCopy(BitConverter.GetBytes(ObjectNumber), 0, _sendBuffer, 1, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_active), 0, _sendBuffer, 5, 1);

            Buffer.BlockCopy(BitConverter.GetBytes(m_rotation.x), 0, _sendBuffer, 6, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_rotation.y), 0, _sendBuffer, 10, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_rotation.z), 0, _sendBuffer, 14, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(m_rotation.w), 0, _sendBuffer, 18, 4);

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
                            Constants.k_nSteamNetworkingSend_Reliable,
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


        public SaveableObject[] GetSynchronizedArray()
        {
            SaveableObject[] unsortedArray = GameObject.FindObjectsByType<SaveableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            SaveableObject[] synchronizedArray = unsortedArray
                .OrderBy(obj => obj.m_id)
                .ToArray();

            return synchronizedArray;
        }


        public void UnpackStatePayload(byte[] packet)
        {
            int ObjectNumber = BitConverter.ToInt32(packet, 1);
            bool active = BitConverter.ToBoolean(packet, 5);

            float x = BitConverter.ToSingle(packet, 6);
            float y = BitConverter.ToSingle(packet, 10);
            float z = BitConverter.ToSingle(packet, 14);
            float w = BitConverter.ToSingle(packet, 18);
            Quaternion Rot = new Quaternion(x, y, z, w);

            ApplyNetworkState(ObjectNumber, active, Rot);
        }

        private void ApplyNetworkState(int ObjectNumber, bool m_active, Quaternion m_rotation)
        {
            Objects[ObjectNumber].gameObject.SetActive(m_active);
            Objects[ObjectNumber].transform.rotation = m_rotation;
        }


    }
}
