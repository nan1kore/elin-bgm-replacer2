using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using System.Collections;
using DG.Tweening;
using DG.Tweening.Core;

namespace ElinBgmReplacer
{
    [BepInPlugin("com.nan1kore.elinbgmreplacer2", "Elin BGM Replacer 2", "2.0.0")]
    public class BgmReplacer : BaseUnityPlugin
    {
        public static ConfigEntry<string> bgmDirectory;
        public static ConfigEntry<float> volume;
        public static string currentZoneId = "";
        public static List<BGMData> customBgmList = new List<BGMData>();
        public static int currentBgmIndex = 0;
        public static bool isCustomBgmPlaying = false;
        public static Playlist customPlaylist = null;
        public static int _lastLoggedNextIndex = -1;

        private void Awake()
        {
            string defaultBgmPath = Path.Combine(Directory.GetCurrentDirectory(), "Package", "Mod_NkBGMReplacer2", "BGM");
            bgmDirectory = Config.Bind("General", "BgmDirectory", defaultBgmPath, "Directory containing custom BGM files (e.g., C:/.../Elin/Package/Mod_NkBGMReplacer2/BGM)");
            volume = Config.Bind("General", "Volume", 1f, new ConfigDescription("BGM volume (0.0 to 1.0)", new AcceptableValueRange<float>(0f, 1f)));

            var harmony = new Harmony("com.nan1kore.elinbgmreplacer");
            harmony.PatchAll();
        }

        public static string GetCurrentBgmId()
        {
            if (EMono._zone == null) return "";

            if (EMono._zone.Boss != null && EMono._zone.Boss.ExistsOnMap)
            {
                return "boss";
            }

            string zoneId = EClass._zone.id;
            if (zoneId == "field" && EClass._zone.IsPCFaction)
            {
                return "home";
            }

            return zoneId;
        }

        public static IEnumerator LoadCustomBgm(string filePath, Action<AudioClip> onLoaded)
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    onLoaded?.Invoke(clip);
                }
                else
                {
                    UnityEngine.Debug.LogError($"[ElinBgmReplacer] Failed to load MP3: {filePath}, Error: {www.error}");
                    onLoaded?.Invoke(null);
                }
            }
        }

        public static void LoadBgmForZone(string zoneId)
        {
            customBgmList.Clear();
            currentBgmIndex = 0;
            isCustomBgmPlaying = false;
            customPlaylist = null;

            string bgmDir = bgmDirectory.Value;
            if (!Directory.Exists(bgmDir))
            {
                UnityEngine.Debug.LogWarning($"[ElinBgmReplacer] BGM directory not found: {bgmDir}");
                return;
            }

            List<string> bgmFiles = new List<string>();
            string mainBgm = Path.Combine(bgmDir, $"{zoneId}.mp3");
            if (File.Exists(mainBgm))
            {
                bgmFiles.Add(mainBgm);
            }

            int index = 1;
            while (true)
            {
                string numberedBgm = Path.Combine(bgmDir, $"{zoneId}_{index}.mp3");
                if (File.Exists(numberedBgm))
                {
                    bgmFiles.Add(numberedBgm);
                    index++;
                }
                else
                {
                    break;
                }
            }

            UnityEngine.Debug.Log($"[ElinBgmReplacer] Found {bgmFiles.Count} BGM files for zone: {zoneId}");

            SoundManager.current.StartCoroutine(LoadAllBgmFiles(bgmFiles, zoneId));
        }

        private static IEnumerator LoadAllBgmFiles(List<string> bgmFiles, string zoneId)
        {
            customBgmList.Clear();
            currentBgmIndex = 0;
            isCustomBgmPlaying = false;
            customPlaylist = null;

            foreach (string filePath in bgmFiles)
            {
                bool loadCompleted = false;
                yield return SoundManager.current.StartCoroutine(LoadCustomBgm(filePath, clip =>
                {
                    if (clip != null)
                    {
                        BGMData data = ScriptableObject.CreateInstance<BGMData>();
                        data.clip = clip;
                        data.volume = volume.Value;
                        data.loop = -1; // 無限ループ（単一ファイル用）
                        data.name = Path.GetFileNameWithoutExtension(filePath);
                        data.type = SoundData.Type.BGM;
                        data.output = SoundManager.current.defaultOutput;
                        data.song = new BGMData.SongData { fadeIn = 0.5f, fadeOut = 0.5f };
                        customBgmList.Add(data);
                        UnityEngine.Debug.Log($"[ElinBgmReplacer] Loaded BGM: {filePath}");
                    }
                    loadCompleted = true;
                }));

                yield return new WaitUntil(() => loadCompleted);
            }

            UnityEngine.Debug.Log($"[ElinBgmReplacer] Completed loading {customBgmList.Count} BGMs for zone: {zoneId}");

            if (customBgmList.Count > 0)
            {
                BgmReplacer.customPlaylist = ScriptableObject.CreateInstance<Playlist>();
                BgmReplacer.customPlaylist.name = $"Custom_{zoneId}";
                BgmReplacer.customPlaylist.list = customBgmList.Select(data => new Playlist.Item { data = data }).ToList();
                BgmReplacer.customPlaylist.interval = 0f;
                BgmReplacer.customPlaylist.fadeInTime = 0.5f;
                BgmReplacer.customPlaylist.fadeOutTime = 0.5f;
                BgmReplacer.customPlaylist.nextBGMOnSwitch = true;
                BgmReplacer.customPlaylist.ignoreLoop = true; // プレイリストで連番ループ
                BgmReplacer.customPlaylist.nextIndex = 0;
                BgmReplacer.customPlaylist.resumed = false;
                BgmReplacer.customPlaylist.Reset();
                SoundManager.current.ResetPlaylist();
                SoundManager.current.SwitchPlaylist(BgmReplacer.customPlaylist, true);
                BgmReplacer.isCustomBgmPlaying = true;
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Switched to custom playlist for zone: {zoneId}, items: {BgmReplacer.customPlaylist.list.Count}, nextIndex: {BgmReplacer.customPlaylist.nextIndex}");
            }
            else
            {
                BgmReplacer.isCustomBgmPlaying = false;
                UnityEngine.Debug.Log($"[ElinBgmReplacer] No BGMs loaded for zone: {zoneId}, falling back to default");
            }
        }
    }

    [HarmonyPatch(typeof(Scene), nameof(Scene.Init))]
    public class SceneInitPatch
    {
        static void Postfix(Scene __instance, Scene.Mode newMode)
        {
            if (newMode == Scene.Mode.Title)
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Scene.Init: Entered title screen, resetting BGM state");
                BgmReplacer.currentZoneId = "";
                BgmReplacer.isCustomBgmPlaying = false;
                BgmReplacer.customBgmList.Clear();
                BgmReplacer.customPlaylist = null;
            }
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Load))]
    public class GameLoadPatch
    {
        static void Prefix(string id, bool cloud)
        {
            UnityEngine.Debug.Log($"[ElinBgmReplacer] Game.Load started: id={id}, cloud={cloud}. Resetting currentZoneId");
            BgmReplacer.currentZoneId = "";
            BgmReplacer.isCustomBgmPlaying = false;
        }
    }

    [HarmonyPatch(typeof(Zone), nameof(Zone.SetBGM), new[] { typeof(List<int>), typeof(bool) })]
    public class ZoneSetBgmPatch
    {
        static bool Prefix(Zone __instance, List<int> ids, bool refresh)
        {
            if (ids != null && ids.Count == 1 && ids[0] == 114)
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Detected boss victory BGM change (ID: 114) in zone: {__instance.id}. Overriding with victory.mp3");
                SoundManager.current.StartCoroutine(DelayedVictoryBgm(__instance));
                return false;
            }

            if (BgmReplacer.isCustomBgmPlaying)
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Custom BGM is playing. Preventing Zone.SetBGM for zone: {__instance.id}");
                return false;
            }

            if (!string.IsNullOrEmpty(BgmReplacer.currentZoneId))
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Custom BGM for '{BgmReplacer.currentZoneId}' is loading/pending. Preventing Zone.SetBGM for zone: {__instance.id}");
                return false;
            }

            UnityEngine.Debug.Log($"[ElinBgmReplacer] Allowing Zone.SetBGM for zone: {__instance.id}, ids={string.Join(",", ids)}");
            return true;
        }

        private static IEnumerator DelayedVictoryBgm(Zone zone)
        {
            UnityEngine.Debug.Log($"[ElinBgmReplacer] Waiting 8 seconds for victory BGM in zone: {zone.id}");
            yield return new WaitForSeconds(8f);
            BgmReplacer.currentZoneId = "victory";
            BgmReplacer.isCustomBgmPlaying = false;
            UnityEngine.Debug.Log($"[ElinBgmReplacer] Loading victory BGM after delay");
            BgmReplacer.LoadBgmForZone("victory");
        }
    }

    [HarmonyPatch(typeof(Zone), nameof(Zone.RefreshBGM))]
    public class ZoneRefreshBgmPatch
    {
        static bool Prefix(Zone __instance)
        {
            if (BgmReplacer.isCustomBgmPlaying)
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Custom BGM is playing. Preventing Zone.RefreshBGM for zone: {__instance.id}");
                return false;
            }

            if (!string.IsNullOrEmpty(BgmReplacer.currentZoneId))
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Custom BGM for '{BgmReplacer.currentZoneId}' is loading/pending. Preventing Zone.RefreshBGM for zone: {__instance.id}");
                return false;
            }

            UnityEngine.Debug.Log($"[ElinBgmReplacer] No custom BGM active. Allowing Zone.RefreshBGM for zone: {__instance.id}");
            return true;
        }
    }

    [HarmonyPatch(typeof(Zone), nameof(Zone.Activate))]
    public class ZoneActivatePatch
    {
        static void Postfix(Zone __instance)
        {
            string requiredBgmId = BgmReplacer.GetCurrentBgmId();
            if (requiredBgmId != BgmReplacer.currentZoneId)
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] BGM change detected on Zone.Activate. Required: '{requiredBgmId}', Current: '{BgmReplacer.currentZoneId}'");
                BgmReplacer.currentZoneId = requiredBgmId;
                BgmReplacer.isCustomBgmPlaying = false;
                BgmReplacer.LoadBgmForZone(requiredBgmId);
            }
            else if (BgmReplacer.isCustomBgmPlaying && BgmReplacer.customPlaylist != null && SoundManager.current.currentPlaylist != BgmReplacer.customPlaylist)
            {
                SoundManager.current.SwitchPlaylist(BgmReplacer.customPlaylist, true);
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Re-applied custom playlist '{BgmReplacer.customPlaylist.name}' on Zone.Activate");
            }
        }
    }

    [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.SetBGMPlaylist))]
    public class SetBgmPlaylistPatch
    {
        static bool Prefix(SoundManager __instance, Playlist pl)
        {
            string playlistName = pl != null ? pl.name : "null";
            if (BgmReplacer.isCustomBgmPlaying && !playlistName.StartsWith("Custom_"))
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Game is trying to set playlist to: {playlistName}. Custom playlist might be overridden.");
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.SwitchPlaylist))]
    public class SwitchPlaylistPatch
    {
        static bool Prefix(SoundManager __instance, Playlist pl, bool stopBGM)
        {
            string playlistName = pl != null ? pl.name : "null";
            if (BgmReplacer.isCustomBgmPlaying && !playlistName.StartsWith("Custom_"))
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Game is trying to switch playlist to: {playlistName}. Custom playlist might be overridden.");
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.PlayBGM), new[] { typeof(string) })]
    public class PlayBgmPatch
    {
        static bool Prefix(SoundManager __instance, string id, ref SoundManager.BGM __result)
        {
            if (BgmReplacer.isCustomBgmPlaying && BgmReplacer.customBgmList.Count > 0)
            {
                if (__instance.currentPlaylist != null && __instance.currentPlaylist.name.StartsWith("Custom_"))
                {
                    __instance.currentPlaylist.Play();
                    __result = __instance.currentBGM;
                    UnityEngine.Debug.Log($"[ElinBgmReplacer] Custom playlist active, playing: {__instance.currentPlaylist.currentItem?.data?.name}, nextIndex: {__instance.currentPlaylist.nextIndex}");
                    return false;
                }
                UnityEngine.Debug.Log($"[ElinBgmReplacer] No valid custom playlist, using default BGM: {id}");
                return true;
            }
            UnityEngine.Debug.Log($"[ElinBgmReplacer] No valid custom BGM, using default BGM: {id}");
            return true;
        }
    }

    [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.UpdateBGM))]
    public class UpdateBgmPatch
    {
        static void Prefix(SoundManager __instance)
        {
            if (BgmReplacer.isCustomBgmPlaying && BgmReplacer.customBgmList.Count > 0)
            {
                if (__instance.tweenFade != null && __instance.tweenFade.IsPlaying())
                {
                    __instance.tweenFade.Complete();
                    UnityEngine.Debug.Log($"[ElinBgmReplacer] Forced tweenFade completion");
                }
            }
        }

        static void Postfix(SoundManager __instance)
        {
            if (BgmReplacer.isCustomBgmPlaying && BgmReplacer.customBgmList.Count > 0)
            {
                int currentNextIndex = __instance.currentPlaylist?.nextIndex ?? -1;
                if (currentNextIndex != BgmReplacer._lastLoggedNextIndex)
                {
                    UnityEngine.Debug.Log($"[ElinBgmReplacer] UpdateBGM: playlist={__instance.currentPlaylist?.name}, nextIndex={currentNextIndex}, isPlaying={__instance.sourceBGM.isPlaying}, time={__instance.sourceBGM.time}, length={__instance.currentBGM?.length}, remainingLoop={__instance.currentBGM?.remainingLoop}");
                    BgmReplacer._lastLoggedNextIndex = currentNextIndex;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.NextBGM))]
    public class NextBgmPatch
    {
        static void Prefix(SoundManager __instance)
        {
            if (BgmReplacer.isCustomBgmPlaying && BgmReplacer.customBgmList.Count > 0 && __instance.currentPlaylist != null && __instance.currentPlaylist.name.StartsWith("Custom_"))
            {
                UnityEngine.Debug.Log($"[ElinBgmReplacer] Forcing playlist play: {__instance.currentPlaylist.name}, nextIndex: {__instance.currentPlaylist.nextIndex}");
                __instance.currentPlaylist.Play();
            }
        }
    }
}