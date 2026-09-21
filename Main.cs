using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.Scripting.GarbageCollector;


//************************************************************************************************************************************************************************************************************************************************************************
// Note: Steamworks is the ideal way to do multiplayer, but bc i'm dumb and lazy i set up the whole thing to work with fusion first instead of just learning steamworks.
// Fusion has a free version that works with unity, but it has a 20 player limit. Steamworks is free and has no player limit, thus why it was swaped to.
// If you wanted to implment multiplayer on non steam platforms steamworks would not work so thats where you'd use fusion. Steamworks is smaller, and fusion is bigger and harder to set up in a modding setting (in editor it works great and is easier than steamworks).
// If you want to use steamworks right you need to set the app ID to be the game's steam app ID (and thus wouldn't need m_MOD_FILTER_KEY in SteamNetworkManager)
// If you want to use fusion you need to set up a Photon account and get a photon app ID. Then set AppIdFusion in MainMultiThingy to your photon app ID. Read the Fusion docs for the rest of the setup if in editor.
// If modding you will need to setup a unity project to make everything work right (Fusion and steamworks). You will also need to set up refs to the game assembly (fusion and steamworks) and the weaved Dll from the unity project (fusion).
// Everything else should be set up, just set the build config to STEAMWORKS or FUSION.
// I've added some comments around the code to help make it easier to read.
// Steamworks Data layout:
// I have Steamworks configed to use 3 channels, 0 for unreliable movement, 1 for Object sync (reliable), 2 for sound (reliable). The first byte of a packet is the data ID
// On channel 0 you have Data ID of 1 for player Data and 2 for Ball Data.
// On channel 1 you have Data ID of 1 for SaveableObject Data.
// On channel 2 you have Data ID of 0 for Ball Sound and 1 for Player Sound.
// SteamSaveObjectSync is a bit different than the other syncs, it has only the host sending data to the clients. If the clients try to save the game while in story mode it will be blocked
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

        public static bool IsInLobby = false;
        public static bool IsLobbyHost = false;


#if STEAMWORKS
        public static float NetworkTickRate = 20f;
        public static SteamSaveObjectSync SteamSaveObjectSync;
        public static BallCollisionSounds[] BallSounds;
        public static Sound[] PlayerSounds;
        public static SteamBallPosSender LocalBallSender;
        public static SteamPlayerSender LocalPlayerSender;
#endif
    }

    [BepInPlugin("Mr-Milky-Way.NormalGolfGameMultiplayer", "NormalGolfGameMultiplayer", "0.1.0")]
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



    [HarmonyPatch(typeof(SteamManager), "Awake")]
    class SteamManagerPatch
    {
        static bool Prefix(SteamManager __instance)
        {
            if (Globals.SteamSaveObjectSync) return true;
            if (!Globals.IsModEnabled.Value) return true;


            GameObject roomsObject = new GameObject("__SteamNetworkManager");

#if FUSION
                MainMultiThingy roomsComponent = roomsObject.AddComponent<MainMultiThingy>();
#endif

#if STEAMWORKS
            SteamNetworkManager SNM = roomsObject.AddComponent<SteamNetworkManager>();
            SteamSaveObjectSync SSOS = roomsObject.AddComponent<SteamSaveObjectSync>();
            Globals.SteamSaveObjectSync = SSOS;
            UnityEngine.Object.DontDestroyOnLoad(roomsObject);
#endif
            return true;
        }
    }


    [HarmonyPatch(typeof(SaveManager), "SaveRun")]
    class SaveManPrefix
    {
        static bool Prefix(SaveManager __instance)
        {
            if (Globals.IsInLobby && !Globals.IsLobbyHost && SaveManager.instance.m_gamemodeState.mode != GameMode.JustGolf)
            {
                Debug.Log("[NormalGolfGameMultiplayer] Saving is disabled in lobbies which are not yours.");
                return false;
            }
#if STEAMWORKS
            if (Globals.IsLobbyHost && Globals.IsInLobby)
            {
                Globals.SteamSaveObjectSync.m_NeedsSending = true;
            }
#endif
            return true;
        }
    }

#if STEAMWORKS
    [HarmonyPatch(typeof(Ball), "PlayCollisionSound")]
    class BallSoundSyncPatch
    {
        static bool Prefix(BallCollisionSounds[] ___m_ballCollisionSoundPairs, PhysicsMaterial physicsMaterial, float impact, AudioSource ___m_audioSource)
        {
            BallCollisionSounds[] ballCollisionSoundPairs = ___m_ballCollisionSoundPairs;
            for (byte i = 0; i < ___m_ballCollisionSoundPairs.Length; i++)
            {
                BallCollisionSounds ballCollisionSounds = ballCollisionSoundPairs[i];
                if (physicsMaterial.name.Contains(ballCollisionSounds.m_material.name))
                {
                    ___m_audioSource.resource = ballCollisionSounds.m_clip;
                    ___m_audioSource.volume = Mathf.Clamp01(impact / 30f) * ballCollisionSounds.m_volumeMult;
                    ___m_audioSource.pitch = UnityEngine.Random.Range(ballCollisionSounds.m_pitchRange.x, ballCollisionSounds.m_pitchRange.y);
                    ___m_audioSource.Play();
                    if (Globals.IsInLobby)
                    {
                        Globals.LocalBallSender.SendBallSoundToLobby(i, impact);
                    }
                    break;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Ball), "Start")]
    class BallStartPatch
    {
        static void Postfix(BallCollisionSounds[] ___m_ballCollisionSoundPairs)
        {
            if (Globals.BallSounds == null)
            {
                Globals.BallSounds = ___m_ballCollisionSoundPairs;
            }
        }
    }

    [HarmonyPatch(typeof(AudioManager), "Awake")]
    class AudioManagerStartPatch
    {
        static void Postfix(Sound[] ___m_sounds)
        {
            if (Globals.PlayerSounds == null)
            {
                Globals.PlayerSounds = ___m_sounds;
            }
        }
    }


    [HarmonyPatch(typeof(AudioManager), "PlaySoundEffect", new Type[] { typeof(string), typeof(float) })]
    class AudioManagerSoundSyncPatch1
    {
        static bool Prefix(string name, float volumeMod, Sound[] ___m_sounds, float ___m_SFXMasterLevelTweak)
        {
            if (Globals.PlayerSounds.Length != ___m_sounds.Length)
            {
                Debug.Log("[NormalGolfGameMultiplayer] Sound array length mismatch");
                Globals.PlayerSounds = ___m_sounds;
            }
            byte soundID = 0;
            if (Time.timeSinceLevelLoad < 0.5f)
            {
                return false;
            }
            Sound sound = null;
            Sound[] sounds = ___m_sounds;
            foreach (Sound sound2 in sounds)
            {
                if (sound2.m_name == name)
                {
                    sound = sound2;
                    soundID = (byte)Array.IndexOf(___m_sounds, sound);
                    break;
                }
            }
            if (sound == null)
            {
                Debug.LogWarning("Failed to play sound " + name);
                return false;
            }
            sound.m_source.spatialBlend = 0f;
            sound.m_source.volume = UnityEngine.Random.Range(sound.m_minVolume, sound.m_maxVolume) * ___m_SFXMasterLevelTweak * volumeMod;
            sound.m_source.pitch = UnityEngine.Random.Range(sound.m_minPitch, sound.m_maxPitch);
            sound.m_source.PlayOneShot(sound.m_clip);
            if (Globals.IsInLobby)
            {
                Globals.LocalPlayerSender.SendPlayerSoundToLobby(soundID);
            }
            return false;
        }
    }


    [HarmonyPatch(typeof(AudioManager), "PlaySoundEffectNotOneShot")]
    class AudioManagerSoundSyncPatch2
    {
        static bool Prefix(string name, Sound[] ___m_sounds, float ___m_SFXMasterLevelTweak)
        {
            if (Globals.PlayerSounds.Length != ___m_sounds.Length)
            {
                Debug.Log("[NormalGolfGameMultiplayer] Sound array length mismatch");
                Globals.PlayerSounds = ___m_sounds;
            }
            byte soundID = 0;
            if (Time.timeSinceLevelLoad < 0.5f)
            {
                return false;
            }
            Sound sound = null;
            Sound[] sounds = ___m_sounds;
            foreach (Sound sound2 in sounds)
            {
                if (sound2.m_name == name)
                {
                    sound = sound2;
                    soundID = (byte)Array.IndexOf(___m_sounds, sound);
                    break;
                }
            }
            if (sound == null)
            {
                Debug.LogWarning("Failed to play sound " + name);
                return false;
            }
            else if (!sound.m_source.isPlaying)
            {
                sound.m_source.spatialBlend = 0f;
                sound.m_source.volume = UnityEngine.Random.Range(sound.m_minVolume, sound.m_maxVolume) * ___m_SFXMasterLevelTweak;
                sound.m_source.pitch = UnityEngine.Random.Range(sound.m_minPitch, sound.m_maxPitch);
                sound.m_source.Play();
                if (Globals.IsInLobby)
                {
                    Globals.LocalPlayerSender.SendPlayerSoundToLobby(soundID);
                }
            }
            return false;
        }
    }
#endif


}
