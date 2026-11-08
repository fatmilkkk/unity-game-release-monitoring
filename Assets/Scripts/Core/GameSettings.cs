using System;
using UnityEngine;

/// <summary>
/// Настройки игры: звук, экран, графика, производительность, управление.
/// Хранятся в PlayerPrefs и применяются при запуске игры.
/// Значения по умолчанию зависят от платформы (ПК / мобильное устройство).
/// </summary>
public static class GameSettings
{
    private const string VolumeKey = "Settings.Volume";
    private const string HintsKey = "Settings.ShowHints";
    private const string FullscreenKey = "Settings.Fullscreen";
    private const string QualityKey = "Settings.Quality";
    private const string FpsLimitKey = "Settings.FpsLimit";
    private const string ShowFpsKey = "Settings.ShowFps";
    private const string TouchKey = "Settings.TouchControls";
    private const string AutoQualityKey = "Settings.AutoQuality";

    public static readonly string[] QualityNames = { "Низкое", "Среднее", "Высокое" };
    public static readonly int[] FpsLimits = { 30, 60, 120, -1 };
    public static readonly string[] FpsLimitNames = { "30", "60", "120", "Без ограничения" };

    public static float Volume { get; private set; } = 0.8f;
    public static bool ShowHints { get; private set; } = true;
    public static bool Fullscreen { get; private set; } = true;
    /// <summary>0 — низкое, 1 — среднее, 2 — высокое.</summary>
    public static int Quality { get; private set; } = 2;
    /// <summary>Индекс в FpsLimits.</summary>
    public static int FpsLimitIndex { get; private set; } = 1;
    public static bool ShowFps { get; private set; }
    public static bool TouchControls { get; private set; }
    public static bool AutoQuality { get; private set; } = true;

    public static int TargetFrameRate => FpsLimits[Mathf.Clamp(FpsLimitIndex, 0, FpsLimits.Length - 1)];

    public static event Action Changed;

    private static bool _loaded;

    public static bool IsMobile => Application.isMobilePlatform;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        _loaded = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init() => EnsureLoaded();

    public static void EnsureLoaded()
    {
        if (_loaded)
            return;

        Volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        ShowHints = PlayerPrefs.GetInt(HintsKey, 1) == 1;
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        Quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, IsMobile ? 1 : 2), 0, QualityNames.Length - 1);
        FpsLimitIndex = Mathf.Clamp(PlayerPrefs.GetInt(FpsLimitKey, 1), 0, FpsLimits.Length - 1);
        ShowFps = PlayerPrefs.GetInt(ShowFpsKey, 0) == 1;
        TouchControls = PlayerPrefs.GetInt(TouchKey, IsMobile || Input.touchSupported ? 1 : 0) == 1;
        AutoQuality = PlayerPrefs.GetInt(AutoQualityKey, 1) == 1;
        _loaded = true;
        Apply();
    }

    public static void SetVolume(float value)
    {
        Volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, Volume);
        ApplyAndNotify();
    }

    public static void SetShowHints(bool value)
    {
        ShowHints = value;
        PlayerPrefs.SetInt(HintsKey, value ? 1 : 0);
        ApplyAndNotify();
    }

    public static void SetFullscreen(bool value)
    {
        Fullscreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        ApplyAndNotify();
    }

    public static void SetQuality(int value)
    {
        Quality = Mathf.Clamp(value, 0, QualityNames.Length - 1);
        PlayerPrefs.SetInt(QualityKey, Quality);
        ApplyAndNotify();
    }

    public static void SetFpsLimit(int index)
    {
        FpsLimitIndex = Mathf.Clamp(index, 0, FpsLimits.Length - 1);
        PlayerPrefs.SetInt(FpsLimitKey, FpsLimitIndex);
        ApplyAndNotify();
    }

    public static void SetShowFps(bool value)
    {
        ShowFps = value;
        PlayerPrefs.SetInt(ShowFpsKey, value ? 1 : 0);
        ApplyAndNotify();
    }

    public static void SetTouchControls(bool value)
    {
        TouchControls = value;
        PlayerPrefs.SetInt(TouchKey, value ? 1 : 0);
        ApplyAndNotify();
    }

    public static void SetAutoQuality(bool value)
    {
        AutoQuality = value;
        PlayerPrefs.SetInt(AutoQualityKey, value ? 1 : 0);
        ApplyAndNotify();
    }

    private static void ApplyAndNotify()
    {
        Apply();
        Changed?.Invoke();
    }

    /// <summary>Уровень Quality Settings проекта, соответствующий выбранному качеству.</summary>
    public static int MapQualityLevel(int quality, int projectLevels)
    {
        if (projectLevels <= 1) return 0;
        switch (quality)
        {
            case 0: return 0;
            case 1: return (projectLevels - 1) / 2;
            default: return projectLevels - 1;
        }
    }

    private static void Apply()
    {
        AudioListener.volume = Volume;

        if (!Application.isEditor && !IsMobile)
            Screen.fullScreen = Fullscreen;

        int levels = QualitySettings.names.Length;
        int level = MapQualityLevel(Quality, levels);
        if (levels > 0 && QualitySettings.GetQualityLevel() != level)
            QualitySettings.SetQualityLevel(level, true);

        // Ограничение FPS: vSync выключается, иначе он перекрывает targetFrameRate
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;

        PlayerPrefs.Save();
    }
}
