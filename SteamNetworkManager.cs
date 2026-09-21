#if STEAMWORKS
using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.TouchScreenKeyboard;

namespace NormalGolfGameMultiplayerMod
{
    public class SteamNetworkManager : MonoBehaviour
    {
        public static SteamNetworkManager instance;

        // Callbacks
        private Callback<LobbyCreated_t> m_LobbyCreated;
        private Callback<LobbyMatchList_t> m_LobbyMatchList;
        private Callback<LobbyEnter_t> m_LobbyEnter;
        private Callback<LobbyChatUpdate_t> m_LobbyChatUpdate;
        private Callback<GameLobbyJoinRequested_t> m_GameLobbyJoinRequested;
        private Callback<LobbyDataUpdate_t> m_LobbyDataUpdateCallback;

        // Room Generation/Search
        [SerializeField] private const string m_MOD_FILTER_KEY = "NGGMM1.0.0"; // This should be changed to the build data or version so people in different versions of the game can't join each other

        [SerializeField] private int m_RoomCodeLength = 6;
        private const string m_Characters = "abcdefghijklmnopqrstuvwxyz0123456789";

        private string m_CurrentLobbyCode;
        [SerializeField] private string m_CurrentLobbyCodeEnterThingy;

        [SerializeField] private bool m_showNetworkingMenu = false;


        // Player/Ball Management (shoud be set in the inspector but this is a mod so it's done in Start())
        [SerializeField] private GameObject m_BallPrefab;
        [SerializeField] private GameObject m_PlayerPrefab;


        private Dictionary<CSteamID, GameObject> m_activePlayerAvatars = new Dictionary<CSteamID, GameObject>();
        private Dictionary<CSteamID, SteamBallPosSender> m_activePlayerBalls = new Dictionary<CSteamID, SteamBallPosSender>();

        [SerializeField] private CSteamID m_CurrentLobbyID;
        [SerializeField] private CSteamID m_CurrentLobbyOwnerID;

        //[SerializeField] private CSteamID m_CurrentTurnPlayerID;


        private CSteamID m_PendingOverlayLobby = CSteamID.Nil;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
        }

        void Start ()
        {

            // Checks the command line arguments for a lobby ID to join, if present
            // This allows for joining a lobby directly from the Steam overlay via the little join button even when the game is closed
            var args = System.Environment.GetCommandLineArgs();

            if (args.Length >= 2)
            {
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i].ToLower() == "+connect_lobby")
                    {
                        if (ulong.TryParse(args[i + 1], out ulong lobbyID))
                        {
                            if (lobbyID > 0)
                            {
                                Debug.Log($"[NormalGolfGameMultiplayer] Attempting to join lobby from command line: {lobbyID}");
                                m_PendingOverlayLobby = new CSteamID(lobbyID);
                                SteamMatchmaking.RequestLobbyData(m_PendingOverlayLobby);
                            }
                        }
                        break;
                    }
                }
            }



            m_LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            m_LobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
            m_LobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            m_LobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            m_GameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            m_LobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);


            m_BallPrefab = Globals.Bundle.LoadAsset<GameObject>("FakeBall");
            m_PlayerPrefab = Globals.Bundle.LoadAsset<GameObject>("Capsule");

        }

        private void OnGUI()
        {
            if (!m_showNetworkingMenu) return;
            if (SaveManager.instance.m_gamemodeState.mode == GameMode.Story) return;


            GUI.Box(new Rect(10, 10, 220, 190), "Network Menu (F1)");


            GUI.Label(new Rect(20, 40, 200, 20), "Room Name:");
            m_CurrentLobbyCodeEnterThingy = GUI.TextField(new Rect(20, 60, 200, 30), m_CurrentLobbyCodeEnterThingy);


            if (GUI.Button(new Rect(20, 100, 200, 40), "Host Game"))
            {
                CreateNGGLobby();
            }


            if (GUI.Button(new Rect(20, 140, 200, 40), "Join Game"))
            {
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

        /* Currently doesnt work
        public void CheckDisableBalls()
        {
            if (m_activePlayerBalls.Count == 0)
                return;

            CSteamID LowestBallID = new CSteamID();
            SteamBallPosSender lowestBall = null;

            foreach (var (id, ball) in m_activePlayerBalls)
            {
                if (lowestBall == null)
                {
                    lowestBall = ball;
                    LowestBallID = id;
                }
                else
                {
                    if (ball.CurrentShotNumber < lowestBall.CurrentShotNumber)
                    {
                        lowestBall = ball;
                        LowestBallID = id;
                    }
                }
            }
            if (LowestBallID != m_CurrentTurnPlayerID)
            {
                if (m_activePlayerBalls.TryGetValue(m_CurrentTurnPlayerID, out SteamBallPosSender Ball)) {
                    if (Ball != null) {
                        if (lowestBall.CurrentShotNumber == Ball.CurrentShotNumber) {
                        } else
                        {
                            m_CurrentTurnPlayerID = LowestBallID;
                        }
                    }
                }
            }
            foreach (var (playerID, ball) in m_activePlayerBalls)
            {
                if (playerID == m_CurrentTurnPlayerID)
                {
                    ball.HideBall(false);
                } else
                {
                    ball.HideBall();
                }
            }
        }
        */


        private void Update()
        {
            if (SceneManager.GetActiveScene().name != "Main")
            {
                m_showNetworkingMenu = false;
                LeaveLobby();
                return;
            }


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

            SteamFriends.SetRichPresence("connect", $"+connect_lobby {lobbyID.m_SteamID}"); // Little join button in steam overlay/friends list

            Globals.IsLobbyHost = true;
            Globals.IsInLobby = true;

            UpdateLobbyMembers();
            SteamMatchmaking.SetLobbyData(lobbyID, "mod_identifier", m_MOD_FILTER_KEY);
            SteamMatchmaking.SetLobbyData(lobbyID, "host_name", SteamFriends.GetPersonaName());

            SteamMatchmaking.SetLobbyData(lobbyID, "mode", SaveManager.instance.m_gamemodeState.mode.ToString());

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

        // Lobby searching and joining via room code
        private void FindNGGLobbies(string roomCode)
        {
            Debug.Log("[NormalGolfGameMultiplayer] Searching for matches");
            SteamMatchmaking.AddRequestLobbyListStringFilter("mod_identifier", m_MOD_FILTER_KEY, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("room_code", roomCode, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("mode", SaveManager.instance.m_gamemodeState.mode.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
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

                Globals.SteamSaveObjectSync.m_NeedsSending = true;

                UpdateLobbyMembers();
            }
            if (stateChange == (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft)
            {
                Debug.Log("[NormalGolfGameMultiplayer] Player Left:" + callback.m_ulSteamIDUserChanged);
                RemovePlayerAvatar((CSteamID)callback.m_ulSteamIDUserChanged);
            }

        }

        // Steam Overlay Join Button Handling and leaving the lobby
        private void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
        {
            if (m_PendingOverlayLobby == CSteamID.Nil || callback.m_ulSteamIDLobby != m_PendingOverlayLobby.m_SteamID)
            {
                return;
            }

            CSteamID lobbyID = m_PendingOverlayLobby;

            m_PendingOverlayLobby = CSteamID.Nil;

            string hostName = SteamMatchmaking.GetLobbyData(lobbyID, "host_name");
            string mode = SteamMatchmaking.GetLobbyData(lobbyID, "mode");
            var ModIdent = SteamMatchmaking.GetLobbyData(lobbyID, "mod_identifier");

            if (ModIdent != m_MOD_FILTER_KEY)
            {
                Debug.Log($"[NormalGolfGameMultiplayer] Mod Filter Key is not the same! [{ModIdent}] [{m_MOD_FILTER_KEY}]");
                return;
            }

            if (mode != SaveManager.instance.m_gamemodeState.mode.ToString())
            {
                Debug.Log("[NormalGolfGameMultiplayer] Wrong GameMode! Changing it");
                SaveManager.instance.m_gamemodeState.mode = Enum.Parse<GameMode>(mode);
                SceneManager.LoadScene("Main");
            }

            if (SceneManager.GetActiveScene().name != "Main")
            {
                SaveManager.instance.m_gamemodeState.mode = Enum.Parse<GameMode>(mode);
                SceneManager.LoadScene("Main");
            }

            Debug.Log($"[NormalGolfGameMultiplayer] Trying to join {hostName}'s lobby");
            SteamMatchmaking.JoinLobby(lobbyID);
        }


        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback) // This is for when the game is open and the user clicks the join button in the steam overlay
        {
            Debug.Log($"[NormalGolfGameMultiplayer] Join requested via Steam overlay for lobby: {callback.m_steamIDLobby}");
            m_PendingOverlayLobby = callback.m_steamIDLobby;
            SteamMatchmaking.RequestLobbyData(callback.m_steamIDLobby);
        }

        public void LeaveLobby()
        {
            if (m_CurrentLobbyID.IsValid())
            {
                Debug.Log("[NormalGolfGameMultiplayer] Leaving Current Lobby");
                SteamMatchmaking.LeaveLobby(m_CurrentLobbyID);

                m_CurrentLobbyID.Clear();
                m_CurrentLobbyOwnerID.Clear();
                m_CurrentLobbyCode = null;

                m_activePlayerAvatars.Clear();
                m_activePlayerBalls.Clear();

                Globals.LocalBallSender = null;
                Globals.LocalPlayerSender = null;
                Globals.IsInLobby = false;
                Globals.SteamSaveObjectSync.Objects = Array.Empty<SaveableObject>();

                SteamFriends.SetRichPresence("connect", "");
            }
        }


        // Network Data Handling ----------------------------------------------------------------------------------------------------------------------------------
        // This is done here so all msg handling is centralized and not a big mess on each object
        private void ReceiveNetworkMessages()
        {
            IntPtr[] messagePtrs = new IntPtr[64];
            int messageCount = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, messagePtrs, messagePtrs.Length);

            for (int i = 0; i < messageCount; i++)
            {
                IntPtr msgPtr = messagePtrs[i];
                SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgPtr);

                if (message.m_cbSize == 29)
                {
                    byte[] packet = new byte[29];
                    Marshal.Copy(message.m_pData, packet, 0, 29);

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


                if (message.m_cbSize == 21)
                {
                    byte[] packet = new byte[21];
                    Marshal.Copy(message.m_pData, packet, 0, 21);

                    if (packet[0] == 2)
                    {
                        CSteamID senderSteamID = message.m_identityPeer.GetSteamID();
                        if (m_activePlayerBalls.TryGetValue(senderSteamID, out SteamBallPosSender Ball))
                        {
                            if (Ball != null)
                            {
                                Ball.UnpackStatePayload(packet);
                            }
                        }
                    }
                }
                SteamNetworkingMessage_t.Release(msgPtr);
            }

            messageCount = SteamNetworkingMessages.ReceiveMessagesOnChannel(1, messagePtrs, messagePtrs.Length);
            for (int i = 0; i < messageCount; i++)
            {
                IntPtr msgPtr = messagePtrs[i];
                SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgPtr);


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
            
            messageCount = SteamNetworkingMessages.ReceiveMessagesOnChannel(2, messagePtrs, messagePtrs.Length);

            for (int i = 0; i < messageCount; i++)
            {
                IntPtr msgPtr = messagePtrs[i];
                SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(msgPtr);


                if (message.m_cbSize == 6)
                {
                    byte[] packet = new byte[6];
                    Marshal.Copy(message.m_pData, packet, 0, 6);

                    if (packet[0] == 0)
                    {
                        CSteamID senderSteamID = message.m_identityPeer.GetSteamID();
                        if (m_activePlayerBalls.TryGetValue(senderSteamID, out SteamBallPosSender Ball))
                        {
                            if (Ball != null)
                            {
                                Ball.UnpackSoundPayload(packet);
                            }
                        }
                    }
                }

                if (message.m_cbSize == 2)
                {
                    byte[] packet = new byte[2];
                    Marshal.Copy(message.m_pData, packet, 0, 2);

                    if (packet[0] == 1)
                    {
                        CSteamID senderSteamID = message.m_identityPeer.GetSteamID();
                        if (m_activePlayerAvatars.TryGetValue(senderSteamID, out GameObject avatar))
                        {
                            SteamPlayerSender playerSender = avatar.GetComponent<SteamPlayerSender>();
                            if (playerSender != null)
                            {
                                playerSender.UnpackSoundPayload(packet);
                            }
                        }
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
                    GameObject capsule = Instantiate(m_PlayerPrefab);
                    capsule.name = $"Player_{SteamFriends.GetFriendPersonaName(memberID)}";

                    SteamPlayerSender playerSender = capsule.AddComponent<SteamPlayerSender>();
                    playerSender.SetPlayerData(memberID, m_CurrentLobbyID, m_CurrentLobbyOwnerID);

                    m_activePlayerAvatars.Add(memberID, capsule);

                    GameObject Ball = Instantiate(m_BallPrefab);
                    Ball.name = $"Ball_{SteamFriends.GetFriendPersonaName(memberID)}";

                    SteamBallPosSender BallSender = Ball.AddComponent<SteamBallPosSender>();
                    BallSender.SetPlayerData(memberID, m_CurrentLobbyID, m_CurrentLobbyOwnerID);

                    m_activePlayerBalls.Add(memberID, BallSender);
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
            if (m_activePlayerBalls.TryGetValue(steamID, out SteamBallPosSender ball))
            {
                Destroy(ball);
                m_activePlayerBalls.Remove(steamID);
            }
        }
    }
}

#endif