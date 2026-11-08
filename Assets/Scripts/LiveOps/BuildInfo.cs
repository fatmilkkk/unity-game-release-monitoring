using System;
using UnityEngine;

/// <summary>
/// Информация о сборке: версия, номер сборки, дата, коммит, платформа.
/// Файл Resources/build_info.json записывается инструментом релизной сборки (Tools → Release).
/// В редакторе, если файла нет, используются значения из Player Settings.
/// </summary>
[Serializable]
public class BuildInfo
{
    public string version = "1.0.0";
    public int buildNumber;
    public string buildDate = "";
    public string commit = "";
    public string platform = "";
    public bool development;

    private static BuildInfo _current;

    public static BuildInfo Current
    {
        get
        {
            if (_current == null)
                _current = Load();
            return _current;
        }
    }

    public string ShortLabel => buildNumber > 0 ? $"v{version} (сборка {buildNumber})" : $"v{version}";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _current = null;

    private static BuildInfo Load()
    {
        var asset = Resources.Load<TextAsset>("build_info");
        if (asset != null)
        {
            try
            {
                var info = JsonUtility.FromJson<BuildInfo>(asset.text);
                if (info != null && !string.IsNullOrEmpty(info.version))
                    return info;
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning("[BuildInfo] Не удалось прочитать build_info.json: " + e.Message);
            }
        }

        return new BuildInfo
        {
            version = Application.version,
            buildNumber = 0,
            buildDate = "",
            commit = "",
            platform = Application.platform.ToString(),
            development = Debug.isDebugBuild
        };
    }
}
