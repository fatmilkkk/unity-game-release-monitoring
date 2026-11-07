using System;
using UnityEngine;

/// <summary>
/// Настройки игры: громкость, полноэкранный режим, показ подсказок.
/// Хранятся в PlayerPrefs и применяются при запуске игры.
/// </summary>
public static class GameSettings
{
    private const string VolumeKey = "Settings.Volume";
    private const string HintsKey = "Settings.ShowHints";
    private const string FullscreenKey = "Settings.Fullscreen";

    public static float Volume { get; private set; } = 0.8f;
    public static bool ShowHints { get; private set; } = true;
    public static bool Fullscreen { get; private set; } = true;

    public static event Action Changed;

    private static bool _loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        _loaded = false;
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
            return;

        Volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        ShowHints = PlayerPrefs.GetInt(HintsKey, 1) == 1;
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        _loaded = true;
        Apply();
    }

    public static void SetVolume(float value)
    {
        Volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, Volume);
        Apply();
        Changed?.Invoke();
    }

    public static void SetShowHints(bool value)
    {
        ShowHints = value;
        PlayerPrefs.SetInt(HintsKey, value ? 1 : 0);
        Changed?.Invoke();
    }

    public static void SetFullscreen(bool value)
    {
        Fullscreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        Apply();
        Changed?.Invoke();
    }

    private static void Apply()
    {
        AudioListener.volume = Volume;
        if (!Application.isEditor)
            Screen.fullScreen = Fullscreen;
        PlayerPrefs.Save();
    }
}
