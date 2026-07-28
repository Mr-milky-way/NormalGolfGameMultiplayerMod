#if STEAMWORKS
using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NormalGolfGameMultiplayerMod
{
    public class SteamNetworkManager : MonoBehaviour
    {


        // Callbacks
        private Callback<LobbyCreated_t> m_LobbyCreated;
        private Callback<LobbyMatchList_t> m_LobbyMatchList;
        private Callback<LobbyEnter_t> m_LobbyEnter;
        private Callback<LobbyChatUpdate_t> m_LobbyChatUpdate;

        // Room Generation/Search
        [SerializeField] private const string m_MOD_FILTER_KEY = "NGGMM1.0.0";

        [SerializeField] private int m_RoomCodeLength = 6;
        private const string m_Characters = "abcdefghijklmnopqrstuvwxyz0123456789";

        private string m_CurrentLobbyCode;
        [SerializeField] private string m_CurrentLobbyCodeEnterThingy;

        [SerializeField] private bool m_showNetworkingMenu = false;


        // Player/Ball Management
        [SerializeField] private GameObject m_BallPrefab;

        private Dictionary<CSteamID, GameObject> m_activePlayerAvatars = new Dictionary<CSteamID, GameObject>();
        private Dictionary<CSteamID, GameObject> m_activePlayerBalls = new Dictionary<CSteamID, GameObject>();

        [SerializeField] private CSteamID m_CurrentLobbyID;
        [SerializeField] private CSteamID m_CurrentLobbyOwnerID;


        void Start ()
        {
            m_LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            m_LobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
            m_LobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            m_LobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);


            m_BallPrefab = Globals.Bundle.LoadAsset<GameObject>("FakeBall");
        }


        private void OnGUI()
        {
            if (!m_showNetworkingMenu) return;


            GUI.Box(new Rect(10, 10, 220, 190), "Network Menu (F1)");


            GUI.Label(new Rect(20, 40, 200, 20), "Room Name:");
            m_CurrentLobbyCodeEnterThingy = GUI.TextField(new Rect(20, 60, 200, 30), m_CurrentLobbyCodeEnterThingy);


            if (GUI.Button(new Rect(20, 100, 200, 40), "Host Game"))
            {
                m_showNetworkingMenu = false;
                CreateNGGLobby();
            }


            if (GUI.Button(new Rect(20, 140, 200, 40), "Join Game"))
            {
                m_showNetworkingMenu = false;
                FindNGGLobbies(m_CurrentLobbyCodeEnterThingy);
            }

            if (m_CurrentLobbyCode != null)
            {
                GUI.Label(new Rect(20, 180, 200, 40), $"Room Code: {m_CurrentLobbyCode}");

                if (GUI.Button(new Rect(20, 220, 200, 40), "Copy"))
                {
                    GUIUtility.systemCopyBuffer = m_CurrentLobbyCode;
                }

            }
        }


        private void Update()
        {
            if (!Globals.SteamInitialized) return;
            SteamAPI.RunCallbacks();


            ReceiveNetworkMessages();

            if (Input.GetKeyDown(KeyCode.F1))
            {
                m_showNetworkingMenu = !m_showNetworkingMenu;
            }
        }



        // Lobby Creation and Joining ----------------------------------------------------------------------------------------------------------------------------------
        private void CreateNGGLobby()
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 4);
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"[NormalGolfGameMultiplayer] Lobby creation failed: {callback.m_eResult}");
                return;
            }

            CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);

            Globals.SteamSaveObjectSync.m_CurrentLobbyID = lobbyID;
            Globals.SteamSaveObjectSync.m_HostSteamId = SteamUser.GetSteamID();

            m_CurrentLobbyID = lobbyID;
            m_CurrentLobbyOwnerID = SteamUser.GetSteamID();

            Debug.Log($"[NormalGolfGameMultiplayer] Lobby created successfully, ID: {lobbyID}");

            // Join button thingy (untested)
            SteamFriends.SetRichPresence("connect", $"+connect_lobby {lobbyID.m_SteamID}");

            Globals.IsLobbyHost = true;
            Globals.IsInLobby = true;

            UpdateLobbyMembers();

            SteamMatchmaking.SetLobbyData(lobbyID, "mod_identifier", m_MOD_FILTER_KEY);
            SteamMatchmaking.SetLobbyData(lobbyID, "host_name", SteamFriends.GetPersonaName());

            m_CurrentLobbyCode = GenerateRoomCode(m_RoomCodeLength);
            SteamMatchmaking.SetLobbyData(lobbyID, "room_code", m_CurrentLobbyCode);
        }


        private string GenerateRoomCode(int length = 8)
        {
            StringBuilder result = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                result.Append(m_Characters[UnityEngine.Random.Range(0, m_Characters.Length)]);
            }
            return result.ToString();
        }




        private void FindNGGLobbies(string roomCode)
        {
            Debug.Log("[NormalGolfGameMultiplayer] Searching for matches");
            SteamMatchmaking.AddRequestLobbyListStringFilter("mod_identifier", m_MOD_FILTER_KEY, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("room_code", roomCode, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(20);
            SteamMatchmaking.RequestLobbyList();
        }

        private void OnLobbyMatchList(LobbyMatchList_t callback)
        {
            for (int i = 0; i < callback.m_nLobbiesMatching; i++)
            {
                CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
                string hostName = SteamMatchmaking.GetLobbyData(lobbyID, "host_name");
                if (i == 0)
                {
                    Debug.Log($"[NormalGolfGameMultiplayer] Attempting to join {hostName}'s lobby...");
                    SteamMatchmaking.JoinLobby(lobbyID);
                    m_CurrentLobbyCode = SteamMatchmaking.GetLobbyData(lobbyID, "room_code");
                }
            }
        }

        private void OnLobbyEnter(LobbyEnter_t callback)
        {
            CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);

            if (callback.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Debug.Log($"[NormalGolfGameMultiplayer] Successfully joined lobby: {lobbyID}");

                Globals.IsInLobby = true;

                m_CurrentLobbyID = lobbyID;
                m_CurrentLobbyOwnerID = SteamMatchmaking.GetLobbyOwner(lobbyID);

                Globals.SteamSaveObjectSync.m_CurrentLobbyID = lobbyID;
                Globals.SteamSaveObjectSync.m_HostSteamId = m_CurrentLobbyOwnerID;

                UpdateLobbyMembers();
            }
            else
            {
                Debug.LogError($"[NormalGolfGameMultiplayer] Failed to enter lobby. Response code: {callback.m_EChatRoomEnterResponse}");
            }
        }


        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            uint stateChange = callback.m_rgfChatMemberStateChange;

            if (stateChange == (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered)
            {
                Debug.Log("[NormalGolfGameMultiplayer] Player Joined:" + callback.m_ulSteamIDUserChanged);

                UpdateLobbyMembers();
            }
            if (stateChange == (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft)
            {
                Debug.Log("[NormalGolfGameMultiplayer] Player Left:" + callback.m_ulSteamIDUserChanged);
                RemovePlayerAvatar((CSteamID)callback.m_ulSteamIDUserChanged);
            }

        }



        // Network Data Handling ----------------------------------------------------------------------------------------------------------------------------------
        // This is done here so all msg handling is centralized and not a big mess on each object
        private void ReceiveNetworkMessages()
        {
            IntPtr[] messagePtrs = new IntPtr[64];
            int messageCount = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, messagePtrs, messagePtrs.Length);
            if (messageCount <= 0) return;

            for (int i = 0; i < messageCount; i++)
            {
                IntPtr msgPtr = messagePtrs[i];
                SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgPtr);

                if (message.m_cbSize == 25)
                {
                    byte[] packet = new byte[25];
                    Marshal.Copy(message.m_pData, packet, 0, 25);

                    if (packet[0] == 1)
                    {
                        CSteamID senderSteamID = message.m_identityPeer.GetSteamID();

                        if (m_activePlayerAvatars.TryGetValue(senderSteamID, out GameObject avatar))
                        {
                            var senderScript = avatar.GetComponent<SteamPlayerSender>();
                            if (senderScript != null)
                            {
                                senderScript.UnpackStatePayload(packet);
                            }
                        }
                    }
                }


                if (message.m_cbSize == 17)
                {
                    byte[] packet = new byte[17];
                    Marshal.Copy(message.m_pData, packet, 0, 17);

                    if (packet[0] == 2)
                    {
                        CSteamID senderSteamID = message.m_identityPeer.GetSteamID();
                        if (m_activePlayerBalls.TryGetValue(senderSteamID, out GameObject avatar))
                        {
                            var senderScript = avatar.GetComponent<SteamBallPosSender>();
                            if (senderScript != null)
                            {
                                senderScript.UnpackStatePayload(packet);
                            }
                        }
                    }
                }


                if (message.m_cbSize == 22)
                {
                    byte[] packet = new byte[22];
                    Marshal.Copy(message.m_pData, packet, 0, 22);

                    if (packet[0] == 1)
                    {
                        Globals.SteamSaveObjectSync.UnpackStatePayload(packet);
                    }
                }

                SteamNetworkingMessage_t.Release(msgPtr);
            }
        }



        // Player Management ----------------------------------------------------------------------------------------------------------------------------------
        private void UpdateLobbyMembers()
        {
            m_CurrentLobbyOwnerID = SteamMatchmaking.GetLobbyOwner(m_CurrentLobbyID);
            int currentMemberCount = SteamMatchmaking.GetNumLobbyMembers(m_CurrentLobbyID);

            for (int i = 0; i < currentMemberCount; i++)
            {
                CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(m_CurrentLobbyID, i);

                if (!m_activePlayerAvatars.ContainsKey(memberID))
                {
                    GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.name = $"Player_{SteamFriends.GetFriendPersonaName(memberID)}";

                    SteamPlayerSender playerSender = capsule.AddComponent<SteamPlayerSender>();
                    playerSender.SetPlayerData(memberID, m_CurrentLobbyID, m_CurrentLobbyOwnerID);

                    m_activePlayerAvatars.Add(memberID, capsule);

                    GameObject Ball = Instantiate(m_BallPrefab);
                    Ball.name = $"Ball_{SteamFriends.GetFriendPersonaName(memberID)}";
                    SteamBallPosSender BallSender = Ball.AddComponent<SteamBallPosSender>();
                    BallSender.SetPlayerData(memberID, m_CurrentLobbyID, m_CurrentLobbyOwnerID);

                    m_activePlayerBalls.Add(memberID, Ball);
                }
            }
        }

        private void RemovePlayerAvatar(CSteamID steamID)
        {
            if (m_activePlayerAvatars.TryGetValue(steamID, out GameObject avatar))
            {
                Destroy(avatar);
                m_activePlayerAvatars.Remove(steamID);
            }
            if (m_activePlayerBalls.TryGetValue(steamID, out GameObject ball))
            {
                Destroy(ball);
                m_activePlayerBalls.Remove(steamID);
            }
        }

        void OnDestroy()
        {
            SteamAPI.Shutdown();
        }

    }
}

#endif