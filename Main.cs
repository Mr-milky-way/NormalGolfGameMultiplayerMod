using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using UnityEngine;


//************************************************************************************************************************************************************************************************************************************************************************
// Note: Steamworks is the ideal way to do multiplayer, but bc i'm dumb and lazy i set up the whole thing to work with fusion first instead of just learning steamworks.
// Fusion has a free version that works with unity, but it has a 20 player limit. Steamworks is free and has no player limit, thus why it was swaped to.
// If you wanted to implment multiplayer on non steam platforms steamworks would not work so thats where you'd use fusion. Steamworks is smaller, and fusion is bigger and harder to set up in a modding setting (in editor it works great and is easier than steamworks).
// If you want to use steamworks right you need to set the app ID to be the game's steam app ID (and thus wouldn't need m_MOD_FILTER_KEY in SteamNetworkManager)
// Because it didn't seem like the game had steamworks set up so I just used the test app ID (480) so it will say that you are playing spacewar and not normal golf game.
// If you want to use fusion you need to set up a Photon account and get a photon app ID. Then set AppIdFusion in MainMultiThingy to your photon app ID. Read the Fusion docs for the rest of the setup if in editor.
// If modding you will need to setup a unity project to make everything work right (Fusion and steamworks). You will also need to set up refs to the game assembly (fusion and steamworks) and the weaved Dll from the unity project (fusion).
// Everything else should be set up, just set the build config to STEAMWORKS or FUSION.
// I've added some comments around the code to help make it easier to read.
//
// Also the code kinda sucks 
//
// Cheers,
// Mr-Milky-Way | Student Developer/Engineer
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣤
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢠⡞⡟
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣰⠋⠀⡇
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⡾⠁⠀⠀⣇
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣴⠋⠀⠀⡀⠀⣿⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣠⣴⠄
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣠⠞⠁⠀⠀⣼⡇⠀⣿⠀⠀⠀⠀⠀⠀⢀⣠⣴⠛⣷⡏
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣠⠞⠁⠀⠀⢀⡼⣹⠁⠀⡟⠀⠀⣀⣤⠴⢾⠉⡟⠸⡇⡾
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣠⠞⠁⠀⠀⠀⢀⡞⢁⡟⠀⢠⡇⡖⢻⠁⢴⠀⢸⠀⡇⠀⡿⠁
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣠⠞⠁⠀⠀⠀⠀⢀⡞⠀⡼⠁⠀⣼⢹⡆⢸⡀⢸⠀⢸⠀⣇⣼⠃
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣼⠃⠀⠀⠀⠀⠀⠀⣼⢁⡼⠁⠀⣰⠻⡄⣇⠈⣇⠈⡇⢸⡄⡽⠁
// ⠀⠀⠀⠀⠀⠀⠀⣰⣆⠀⢸⠃⠀⠀⠀⠀⠀⠀⠀⢉⣽⠞⠋⠉⠉⠙⠻⣽⡤⣿⠀⢷⣈⡿⠁
// ⠀⠀⠀⠀⠀⠀⠀⡏⠘⣦⡏⠀⠀⠀⠀⠀⠀⠀⣴⠏⠀⠀⠀⠀⢀⡴⠒⠻⡿⣿⣇⡼⠋
// ⠀⠀⠀⠀⠀⠀⢸⠇⠀⠘⠃⠀⠀⠀⠀⠀⠀⡼⠁⠀⠀⠀⠀⣴⠋⢀⡴⠚⢳⠸⣇
// ⠀⠀⠀⠀⠀⠀⢸⠀⠀⠀⠀⠀⠀⠀⠀⠀⣼⠃⠀⠀⠀⠀⣼⠃⠀⠘⠧⠴⠟⠀⠈⢳⡀⠀⠀⠀⠀⠀⢸⡇
// ⠀⠀⠀⠀⢠⡴⠞⠀⠰⣶⠖⠒⠋⠀⠀⢰⡇⣀⣀⣀⠀⡼⠁⠀⢀⣤⣶⣦⣄⠀⠀⠀⠙⢦⡀⠀⠀⠀⠘⣷⣤⣄⣴⣦⠀⣴⡦⢠⠒⢂
// ⠀⠀⠀⠀⠀⠙⠦⣄⡀⢈⣹⢶⡟⠓⢲⡾⠋⠁⠀⠈⢻⠇⠀⢰⣿⣿⣿⣿⣿⣧⡀⠀⠀⠀⠙⢦⡀⠀⠈⠻⠶⠟⠻⠿⠌⠻⠆⢸⠒⠃
// ⠀⠀⠀⠀⠀⣠⠴⢛⡽⠋⠁⣼⠀⢰⠏⠀⠀⠀⠀⠀⠈⢷⠀⢈⡻⠿⠿⣿⣿⣿⣷⠀⠀⠀⠀⠀⠉⠳⣤⠀⠀⠀⠀⠀⠀⠀⠀⠸
// ⠀⠀⢀⡴⠞⠁⢶⡋⠀⠀⠀⠹⡄⢻⠀⠀⠀⠀⠀⠀⠀⢈⡇⠈⠿⢷⠀⠀⠀⠀⠁⠀⠀⠰⣦⣤⠀⣴⠃
// ⠐⠾⣏⡀⠀⠀⠀⠙⢦⡀⠀⠀⠙⢾⣧⡀⠀⠀⠀⠀⢀⡞⠁⠀⠀⠘⣇⣀⡤⠖⢦⠀⠀⠀⠀⢀⡼⠃
// ⠀⠀⠀⢉⡷⠆⠀⠀⠀⠉⠓⠤⢤⣤⣬⠙⠳⠦⠴⠚⠉⠙⠒⠦⢤⣀⣀⠀⠀⠀⠈⠓⠶⠶⣤⠟
// ⠀⠰⣾⡉⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣿⠀⢀⡤⠴⠒⠚⠛⠛⠃⠀⠀⣽⠉⠙⠓⠒⠒⠒⠚⠁
// ⠀⠀⠀⠙⠳⢤⣀⣀⡀⠀⠀⠀⠀⠀⠘⠟⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠙⠳⠶⢺⡇
// ⠀⠀⠀⠀⠀⠀⣠⠟⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣤⣴⡋
// ⠀⠀⠀⠀⢀⣾⣁⣀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠳⣦
// ⠀⠀⠀⠀⠀⠉⠉⣽⠃⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣤⣤⣀⣈⣷
// ⠀⠀⠀⠀⠀⠀⠀⠿⠞⠁⠘⠒⠦⢤⣀⡀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠙⣆
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠓⠦⢤⣀⠀⠀⠀⠀⠀⠀⠀⡀⠀⠈⢧
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠙⠒⠦⣄⡀⠀⠀⢻⡙⠓⠛
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉⠓⠦⣄⣷
// ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣈⡙⠁
//
//
//************************************************************************************************************************************************************************************************************************************************************************

namespace NormalGolfGameMultiplayerMod
{ 

    public static class Globals
    {
        public static AssetBundle Bundle;
        public static ConfigEntry<bool> IsModEnabled;

#if STEAMWORKS
        public static float NetworkTickRate = 20f;
        public static bool SteamInitialized = false;
        public static string SteamIdDemo = "4663130";
        public static string SteamId = "3510740";
        public static bool IsDemo = true;
#endif
    }

    [BepInPlugin("Mr-Milky-Way.NormalGolfGameMultiplayer", "NormalGolfGameMultiplayer", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        void Awake()
        {
            Globals.IsModEnabled = Config.Bind(
                "General",
                "EnableMod",
                true,
                "Enables/Disables the mod (Requires restart)"
            );

            if (!Globals.IsModEnabled.Value) return;

#if STEAMWORKS
            // This steamworks init should be changed if building into the actual engine
            if (Globals.IsDemo)
            {
                System.Environment.SetEnvironmentVariable("SteamAppId", Globals.SteamIdDemo);

                System.Environment.SetEnvironmentVariable("SteamGameId", Globals.SteamIdDemo);
            } else {
                System.Environment.SetEnvironmentVariable("SteamAppId", Globals.SteamId);

                System.Environment.SetEnvironmentVariable("SteamGameId", Globals.SteamId);
            }

            try
            {
                if (SteamAPI.Init())
                {
                    Logger.LogInfo("Steamworks has been manually initialized. IS DEMO? " + Globals.IsDemo);
                    string username = SteamFriends.GetPersonaName();
                    Logger.LogInfo($"Logged in as: {username}");
                    Globals.SteamInitialized = true;
                }
                else
                {
                    Logger.LogError("SteamAPI_Init failed. Is your Steam client open?");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to load Steamworks library: {ex.Message}");
            }
            #endif

            string path = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "NormalGolfGameMultiplayerMod", "fusionresources");

            Globals.Bundle = AssetBundle.LoadFromFile(path);


            if (Globals.Bundle == null)
            {
                Logger.LogError("Failed to load AssetBundle.");
            }

            Logger.LogInfo("Hook loaded");
            var harmony = new Harmony("Multi.player");
            harmony.PatchAll();

            Application.runInBackground = true;
        }
    }



    [HarmonyPatch(typeof(BuildSettings), "Setup")]
    class BuildSettingsPatch
    {
        static bool Prefix(BuildSettings __instance)
        {
            if (!Globals.IsModEnabled.Value) return true;


            GameObject roomsObject = new GameObject("MultiplayerRooms");

#if FUSION
                MainMultiThingy roomsComponent = roomsObject.AddComponent<MainMultiThingy>();
#endif

#if STEAMWORKS
                SteamNetworkManager SNM = roomsObject.AddComponent<SteamNetworkManager>();
#endif
            return true;
        }
    }
}
