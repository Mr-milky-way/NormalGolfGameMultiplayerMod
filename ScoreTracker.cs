#if STEAMWORKS

using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NormalGolfGameMultiplayerMod
{
    public class ScoreTracker : MonoBehaviour
    {

        private int[] Par = { 3, 4, 3, 4, 4, 5, 4, 4, 3 };
        private float[] TimesForPars = { 0, 0, 150, 180, 210 };

        private bool m_showScoreCard = true;

        protected Callback<PersonaStateChange_t> PersonaStateChangeCallback;

        private Dictionary<CSteamID, string> Players = new Dictionary<CSteamID, string>();

        float baseWidth = 1920f;
        float baseHeight = 1080f;
        private Dictionary<string, Dictionary<int, int>> scores = new Dictionary<string, Dictionary<int, int>>();

        private List<CSteamID> SortedPlayers;

        private CSteamID localPlayerID;
        private readonly byte[] _sendBuffer = new byte[3];

        public float timeFromLastUpdate = 0f;

        float CurrentHoleTime = 0f;
        float TimeToGetToNextHole = 0f;

        bool PlayersLeftGolfing = false;
        bool StillGolfing = true;

        private Color[] PlayerColors = { Color.red, Color.green, Color.turquoise, Color.purple };

        int currentHole = 0;

        private void Start()
        {
            PersonaStateChangeCallback = Callback<PersonaStateChange_t>.Create(OnPersonaStateChangeHandler);
            string localPlayerName = SteamFriends.GetPersonaName();

            localPlayerID = SteamUser.GetSteamID();
        }


        public void StartNSSGMode()
        {

            if (Globals.IsLobbyHost)
            {
                SendScoreToLobby(Globals.m_CurrentLobbyID, 255, 0);
            }

            PanelManager.instance.m_LMUGCPanel.CompleteNormalGolfRound();

            PanelManager.instance.m_LMUGCPanel.StartChallenge();

            CurrentHoleTime = 0f;
            TimeToGetToNextHole = 0f;

            PlayersLeftGolfing = false;
            StillGolfing = true;

            currentHole = 0;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                m_showScoreCard = !m_showScoreCard;
            }

            if (timeFromLastUpdate > 5)
            {
                RefreshLobbyPlayers();
                timeFromLastUpdate = 0;
            }
            timeFromLastUpdate += Time.deltaTime;

            if (Globals.CurrentMode != MultiplayerMode.NSSG) return;

            PlayersLeftGolfing = false;

            foreach (KeyValuePair<CSteamID, string> player in Players)
            {
                int highestHole = 0;
                if (!scores.ContainsKey(player.Value))
                {
                    scores[player.Value] = new Dictionary<int, int>();
                }
                foreach (KeyValuePair<int, int> score in scores[player.Value])
                {
                    if (score.Key > highestHole)
                    {
                        highestHole = score.Key;
                    }
                }
                if (highestHole == currentHole)
                {
                    PlayersLeftGolfing = true;
                }
                if (highestHole == currentHole && player.Key == localPlayerID)
                {
                    StillGolfing = true;
                }

                if (highestHole > currentHole && player.Key == localPlayerID)
                {
                    StillGolfing = false;
                }
            }


            if (PlayersLeftGolfing && !StillGolfing)
            {
                HitManager.instance.m_ball.gameObject.SetActive(false);
            }
            if (!PlayersLeftGolfing)
            {
                TimeToGetToNextHole = 30f;
                PlayersLeftGolfing = true;
                currentHole++;
            }


            if (TimeToGetToNextHole > 0)
            {
                TimeToGetToNextHole -= Time.deltaTime;
            }
            if (TimeToGetToNextHole <= 0)
            {
                StillGolfing = true;
                HitManager.instance.m_ball.gameObject.SetActive(true);
                CurrentHoleTime = 0;
            }

            CurrentHoleTime += Time.deltaTime;
        }

        private void OnGUI()
        {
            if (!m_showScoreCard) return;
            if (!Globals.IsInLobby) return;

            float scaleX = (float)Screen.width / baseWidth;
            float scaleY = (float)Screen.height / baseHeight;

            Matrix4x4 svMat = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));


            GUILayout.BeginArea(new Rect(650, 20, 700, 50 + (Players.Count*50)), GUI.skin.box);
            GUILayout.Label("<b>Scorecards (F2): " + Globals.CurrentMode + "</b>", new GUIStyle(GUI.skin.label) { richText = true });

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("<b>Name</b>", GUILayout.Width(150));
            GUILayout.Label("<b>1</b>", GUILayout.Width(50));
            GUILayout.Label("<b>2</b>", GUILayout.Width(50));
            GUILayout.Label("<b>3</b>", GUILayout.Width(50));
            GUILayout.Label("<b>4</b>", GUILayout.Width(50));
            GUILayout.Label("<b>5</b>", GUILayout.Width(50));
            GUILayout.Label("<b>6</b>", GUILayout.Width(50));
            GUILayout.Label("<b>7</b>", GUILayout.Width(50));
            GUILayout.Label("<b>8</b>", GUILayout.Width(50));
            GUILayout.Label("<b>9</b>", GUILayout.Width(50));
            GUILayout.Label("<b>Total</b>", GUILayout.Width(100));
            GUILayout.EndHorizontal();

            for (int i = 0; SortedPlayers.Count > i; i++) {
                string playerName = Players.GetValueSafe(SortedPlayers[i]);

                if (!scores.ContainsKey(playerName))
                {
                    scores[playerName] = new Dictionary<int, int>();
                }
                DrawRow(playerName, PlayerColors[i]);
            }

            GUILayout.EndArea();

            if (Globals.CurrentMode != MultiplayerMode.NSSG) return;


        }

        private void RefreshLobbyPlayers()
        {
            Players.Clear();

            int memberCount =
                SteamMatchmaking.GetNumLobbyMembers(Globals.m_CurrentLobbyID);

            for (int i = 0; i < memberCount; i++)
            {
                CSteamID playerID =
                    SteamMatchmaking.GetLobbyMemberByIndex(Globals.m_CurrentLobbyID, i);

                AddPlayerToScoreCard(playerID);
            }

            List<string> keysToRemove = new List<string>();

            foreach (string key in scores.Keys)
            {
                if (!Players.ContainsValue(key))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (string key in keysToRemove)
            {
                scores.Remove(key);
            }

            List<CSteamID> currentIDs = new List<CSteamID>();
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(Globals.m_CurrentLobbyID, i);
                if (memberID != CSteamID.Nil)
                {
                    currentIDs.Add(memberID);
                }
            }

            List<CSteamID> sortedIDs = currentIDs
                .OrderByDescending(id => id == SteamNetworkManager.instance.m_CurrentLobbyOwnerID)
                .ThenBy(id => id.m_SteamID)
                .ToList();

            SortedPlayers = sortedIDs;
        }


        private void DrawRow(string name, Color c)
        {
            if (scores.TryGetValue(name, out var playerScores))
            {
                int totalScore = 0;
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(name, GUILayout.Width(150));
                for (int i = 0; i < 9; i++)
                {
                    string score = "-";
                    if(playerScores.TryGetValue(i, out int holeScore))
                    {
                        score = holeScore.ToString();
                        totalScore += holeScore;
                    }
                    GUILayout.Label(score, GUILayout.Width(50));
                }
                GUILayout.Label(totalScore.ToString(), GUILayout.Width(100));
                GUILayout.EndHorizontal();
            }
        }

        public void AddPlayerToScoreCard(CSteamID PlayerID)
        {
            if (!IsLobbyMember(PlayerID)) return;

            if (Players.TryGetValue(PlayerID, out string name))
            {
                return;
            }
            bool needsToRetreiveInformationFromInternet = SteamFriends.RequestUserInformation(PlayerID, true);
            if (!needsToRetreiveInformationFromInternet)
            {
                string SteamNickname = SteamFriends.GetFriendPersonaName(PlayerID);
                Players.Add(PlayerID, SteamNickname);
            }
        }

        private void OnPersonaStateChangeHandler(PersonaStateChange_t PersonaStateChange)
        {
            if (!IsLobbyMember((CSteamID)PersonaStateChange.m_ulSteamID)) return;

            if (Players.TryGetValue((CSteamID)PersonaStateChange.m_ulSteamID, out string name))
            {
                return;
            }
            string SteamNickname = SteamFriends.GetFriendPersonaName((CSteamID)PersonaStateChange.m_ulSteamID);
            Players.Add((CSteamID)PersonaStateChange.m_ulSteamID, SteamNickname);
        }

        private bool IsLobbyMember(CSteamID playerID)
        {
            if (Globals.m_CurrentLobbyID == CSteamID.Nil)
                return false;

            int memberCount =
                SteamMatchmaking.GetNumLobbyMembers(Globals.m_CurrentLobbyID);

            for (int i = 0; i < memberCount; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(Globals.m_CurrentLobbyID, i)
                    == playerID)
                {
                    return true;
                }
            }

            return false;
        }

        private void ChangeScoring(byte hole, byte score)
        {
            if (hole != 255 && score != 255)
            {
                if (Globals.CurrentMode == MultiplayerMode.NSSG)
                {
                    int ParForHole = Par[hole];
                    float PartimeForHole = TimesForPars[ParForHole - 1];
                    if (CurrentHoleTime >= PartimeForHole)
                    {
                        _sendBuffer[2] = 0;
                    }
                    else if ((score - ParForHole) == -3)
                    {
                        _sendBuffer[2] = 18;
                    }
                    else if ((score - ParForHole) == -2)
                    {
                        _sendBuffer[2] = 14;
                    }
                    else if ((score - ParForHole) == -1)
                    {
                        _sendBuffer[2] = 10;
                    }
                    else if ((score - ParForHole) == 0)
                    {
                        _sendBuffer[2] = 7;
                    }
                    else if ((score - ParForHole) == 1)
                    {
                        _sendBuffer[2] = 5;
                    }
                    else if ((score - ParForHole) == 2)
                    {
                        _sendBuffer[2] = 4;
                    }
                    else if ((score - ParForHole) == 3)
                    {
                        _sendBuffer[2] = 3;
                    }
                    else if ((score - ParForHole) == 4)
                    {
                        _sendBuffer[2] = 2;
                    }
                    else
                    {
                        _sendBuffer[2] = 0;
                    }
                }
                else
                {
                    _sendBuffer[2] = score;
                }
            }
            else
            {
                _sendBuffer[2] = score;
            }
        }

        public void SendScoreToLobby(CSteamID lobbyId, byte hole, byte score)
        {
            ChangeScoring(hole, score);
            ApplyScore(hole, _sendBuffer[2], localPlayerID);

            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            if (memberCount <= 1) return;

            _sendBuffer[0] = 0;

            _sendBuffer[1] = hole;


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
                            3
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

        public void UnpackScorePayload(byte[] packet, CSteamID PlayerID)
        {
            if (packet[0] == 0)
            {
                int hole = packet[1];
                int score = packet[2];
                ApplyScore(hole, score, PlayerID);
            }
        }

        private void ApplyScore(int hole, int score, CSteamID PlayerID)
        {
            if (hole == 255 && score == 0)
            {
                StartNSSGMode();
                return;
            }
            if (hole == 255 && score == 255)
            {
                scores[Players[PlayerID]] = new Dictionary<int, int>();
            }
            if (!scores.ContainsKey(Players[PlayerID]))
            {
                scores[Players[PlayerID]] = new Dictionary<int, int>();
            }
            scores[Players[PlayerID]][hole] = score;
        }
    }
}
#endif