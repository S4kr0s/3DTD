using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Player settings from the Options screen (redesign B4). Stored as JSON in PlayerPrefs and pushed to Unity
// (screen, quality, URP asset, post-processing volumes, audio mixer) when applied and on every scene load.
public static class GameOptions
{
    private const string PrefsKey = "3DTD.Options";

    public static readonly int[] FrameLimits = { 30, 60, 120, -1 };
    public static readonly int[] MsaaSamples = { 1, 2, 4 };
    public static readonly float[] InterfaceScales = { 0.9f, 1f, 1.15f };

    [Serializable]
    public class Values
    {
        // General
        public bool autoWaveOnStart = false;
        public bool rangeOnHover = true;
        public int interfaceScale = 1;
        // Video
        public int windowMode = 0;          // 0 full screen, 1 borderless, 2 windowed
        public int resolutionWidth = 0;     // 0 = keep the current resolution
        public int resolutionHeight = 0;
        public int frameLimit = 1;          // index into FrameLimits
        public bool vSync = true;
        public int quality = -1;            // -1 = keep the project's default level
        public float renderScale = 1f;
        public bool bloom = true;
        public int antiAliasing = 0;        // index into MsaaSamples
        // Audio (0..1)
        public float masterVolume = 1f;
        public float towersVolume = 0.8f;
        // Controls
        public float cameraRotateSpeed = 1f;
        public float cameraZoomSpeed = 1f;
        public float cameraPanSpeed = 1f;
        public bool invertCameraY = false;

        public Values Clone()
        {
            return (Values)MemberwiseClone();
        }

        // Number of settings that differ, for the "n unsaved changes" hint
        public int CountDifferences(Values other)
        {
            if (other == null)
                return 0;
            int count = 0;
            if (autoWaveOnStart != other.autoWaveOnStart) count++;
            if (rangeOnHover != other.rangeOnHover) count++;
            if (interfaceScale != other.interfaceScale) count++;
            if (windowMode != other.windowMode) count++;
            if (resolutionWidth != other.resolutionWidth || resolutionHeight != other.resolutionHeight) count++;
            if (frameLimit != other.frameLimit) count++;
            if (vSync != other.vSync) count++;
            if (quality != other.quality) count++;
            if (!Mathf.Approximately(renderScale, other.renderScale)) count++;
            if (bloom != other.bloom) count++;
            if (antiAliasing != other.antiAliasing) count++;
            if (!Mathf.Approximately(masterVolume, other.masterVolume)) count++;
            if (!Mathf.Approximately(towersVolume, other.towersVolume)) count++;
            if (!Mathf.Approximately(cameraRotateSpeed, other.cameraRotateSpeed)) count++;
            if (!Mathf.Approximately(cameraZoomSpeed, other.cameraZoomSpeed)) count++;
            if (!Mathf.Approximately(cameraPanSpeed, other.cameraPanSpeed)) count++;
            if (invertCameraY != other.invertCameraY) count++;
            return count;
        }
    }

    private static Values current;
    private static AudioMixer mixer;
    private static bool sceneHookInstalled;

    public static event Action Changed;

    public static Values Current
    {
        get
        {
            if (current == null)
                current = Load();
            return current;
        }
    }

    public static bool AutoWaveOnStart => Current.autoWaveOnStart;
    public static bool RangeOnHover => Current.rangeOnHover;
    public static float InterfaceScale => InterfaceScales[Mathf.Clamp(Current.interfaceScale, 0, InterfaceScales.Length - 1)];

    public static Values Defaults()
    {
        Values values = new Values();
        values.quality = QualitySettings.GetQualityLevel();
        return values;
    }

    private static Values Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                Values loaded = JsonUtility.FromJson<Values>(json);
                if (loaded != null)
                    return loaded;
            }
            catch (ArgumentException)
            {
                // Corrupt settings fall back to the defaults below
            }
        }
        return Defaults();
    }

    public static void Save(Values values)
    {
        current = values.Clone();
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(current));
        PlayerPrefs.Save();
        ApplyAll();
        Changed?.Invoke();
    }

    // Called by the HUD / menu so audio settings reach the project's mixer
    public static void RegisterMixer(AudioMixer audioMixer)
    {
        if (audioMixer != null)
            mixer = audioMixer;
        ApplyAudio(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (!sceneHookInstalled)
        {
            sceneHookInstalled = true;
            SceneManager.sceneLoaded += (scene, mode) => ApplySceneSettings();
#if UNITY_EDITOR
            Application.quitting += RestoreEditorAssets;
#endif
        }
        ApplyAll();
    }

    public static void ApplyAll()
    {
        Values values = Current;
        ApplyDisplay(values);

        if (values.quality >= 0 && values.quality < QualitySettings.names.Length && values.quality != QualitySettings.GetQualityLevel())
        {
#if UNITY_EDITOR
            RememberEditorQuality();
#endif
            QualitySettings.SetQualityLevel(values.quality, true);
        }

        QualitySettings.vSyncCount = values.vSync ? 1 : 0;
        Application.targetFrameRate = FrameLimits[Mathf.Clamp(values.frameLimit, 0, FrameLimits.Length - 1)];
        ApplyPipeline(values);
        ApplySceneSettings();
    }

    private static void ApplyDisplay(Values values)
    {
        if (Application.isEditor)
            return;

        FullScreenMode mode = values.windowMode == 0 ? FullScreenMode.ExclusiveFullScreen
            : values.windowMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        int width = values.resolutionWidth > 0 ? values.resolutionWidth : Screen.width;
        int height = values.resolutionHeight > 0 ? values.resolutionHeight : Screen.height;
        if (width != Screen.width || height != Screen.height || mode != Screen.fullScreenMode)
            Screen.SetResolution(width, height, mode);
    }

    private static void ApplyPipeline(Values values)
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp))
            return;
#if UNITY_EDITOR
        RememberEditorAsset(urp);
#endif
        urp.renderScale = Mathf.Clamp(values.renderScale, 0.5f, 1f);
        urp.msaaSampleCount = MsaaSamples[Mathf.Clamp(values.antiAliasing, 0, MsaaSamples.Length - 1)];
    }

    // Settings that live on scene objects: post-processing volumes, the audio mixer and the interface scale
    public static void ApplySceneSettings()
    {
        bool bloom = Current.bloom;
        foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
        {
            if (volume.sharedProfile == null || !volume.sharedProfile.Has<Bloom>())
                continue;
            // .profile gives this volume its own copy, so the shared asset is never edited at runtime
            if (volume.profile.TryGet(out Bloom bloomOverride))
                bloomOverride.active = bloom;
        }
        ApplyAudio(GameManager.Instance != null && GameManager.Instance.IsMainMenu);
    }

    private static void ApplyAudio(bool isMainMenu)
    {
        if (mixer == null)
            return;
        mixer.SetFloat("masterVolume", ToDecibel(Current.masterVolume));
        // The main menu backdrop plays a live match with muted towers
        mixer.SetFloat("towersVolume", isMainMenu ? -80f : ToDecibel(Current.towersVolume));
    }

    public static float ToDecibel(float volume01)
    {
        return volume01 <= 0.0001f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(volume01));
    }

#if UNITY_EDITOR
    // Play mode must not leave edited pipeline assets or quality levels behind in the project
    private static readonly Dictionary<UniversalRenderPipelineAsset, Vector2> editorOriginals = new Dictionary<UniversalRenderPipelineAsset, Vector2>();
    private static int editorQuality = -1;

    private static void RememberEditorAsset(UniversalRenderPipelineAsset asset)
    {
        if (!editorOriginals.ContainsKey(asset))
            editorOriginals[asset] = new Vector2(asset.renderScale, asset.msaaSampleCount);
    }

    private static void RememberEditorQuality()
    {
        if (editorQuality < 0)
            editorQuality = QualitySettings.GetQualityLevel();
    }

    private static void RestoreEditorAssets()
    {
        foreach (KeyValuePair<UniversalRenderPipelineAsset, Vector2> entry in editorOriginals)
        {
            if (entry.Key == null)
                continue;
            entry.Key.renderScale = entry.Value.x;
            entry.Key.msaaSampleCount = (int)entry.Value.y;
        }
        editorOriginals.Clear();
        if (editorQuality >= 0)
            QualitySettings.SetQualityLevel(editorQuality, true);
        editorQuality = -1;
    }
#endif
}
