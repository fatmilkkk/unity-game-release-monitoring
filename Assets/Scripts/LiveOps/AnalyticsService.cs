using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Анонимная аналитика игры.
///  • Работает только с согласия игрока (окно при первом запуске, переключатель в настройках).
///  • События пишутся построчно в JSON (persistentDataPath/analytics/events.jsonl) — их читает
///    панель Tools → Release → Analytics Dashboard.
///  • Если в удалённом конфиге задан analyticsEndpoint, накопленные события дополнительно
///    отправляются на сервер POST-запросом пачками.
/// Персональные данные не собираются: только случайный идентификатор установки и сессии.
/// </summary>
public static class AnalyticsService
{
    public const string ConsentKey = "Analytics.Consent";       // -1 — не спрашивали, 0 — отказ, 1 — согласие
    private const string InstallIdKey = "Analytics.InstallId";
    private const int FlushEvery = 10;
    private const long MaxFileBytes = 2 * 1024 * 1024;

    private static readonly List<string> Buffer = new List<string>();
    private static readonly List<string> UploadQueue = new List<string>();
    private static bool _uploading;

    public static string SessionId { get; private set; } = "";
    public static string InstallId { get; private set; } = "";
    public static float SessionStartRealtime { get; private set; }
    public static int EventsThisSession { get; private set; }

    /// <summary>Подписка для тестов и отладки: вызывается для каждого записанного события.</summary>
    public static event Action<AnalyticsEvent> EventTracked;

    public static string DataFolder => Path.Combine(Application.persistentDataPath, "analytics");
    public static string EventsFile => Path.Combine(DataFolder, "events.jsonl");

    public static int ConsentState => PlayerPrefs.GetInt(ConsentKey, -1);
    public static bool HasAnsweredConsent => ConsentState >= 0;
    public static bool IsEnabled => ConsentState == 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Buffer.Clear();
        UploadQueue.Clear();
        _uploading = false;
        SessionId = "";
        EventsThisSession = 0;
        EventTracked = null;
    }

    public static void SetConsent(bool allowed)
    {
        bool wasEnabled = IsEnabled;
        PlayerPrefs.SetInt(ConsentKey, allowed ? 1 : 0);
        PlayerPrefs.Save();

        if (allowed && !wasEnabled)
            StartSession();
        else if (!allowed)
            Buffer.Clear();
    }

    /// <summary>Начать сессию (вызывается при запуске игры).</summary>
    public static void StartSession()
    {
        InstallId = PlayerPrefs.GetString(InstallIdKey, "");
        if (string.IsNullOrEmpty(InstallId))
        {
            InstallId = Guid.NewGuid().ToString("N").Substring(0, 12);
            PlayerPrefs.SetString(InstallIdKey, InstallId);
            PlayerPrefs.Save();
        }

        SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
        SessionStartRealtime = Time.realtimeSinceStartup;
        EventsThisSession = 0;

        Track("session_start",
            "os", SystemInfo.operatingSystem,
            "device", SystemInfo.deviceModel,
            "gpu", SystemInfo.graphicsDeviceName,
            "ram_mb", SystemInfo.systemMemorySize.ToString(CultureInfo.InvariantCulture),
            "screen", $"{Screen.width}x{Screen.height}",
            "language", Application.systemLanguage.ToString(),
            "quality", GameSettings.Quality.ToString(CultureInfo.InvariantCulture),
            "touch", GameSettings.TouchControls ? "1" : "0");
    }

    public static void EndSession()
    {
        if (string.IsNullOrEmpty(SessionId))
            return;

        Track("session_end",
            "duration", F(Time.realtimeSinceStartup - SessionStartRealtime),
            "events", EventsThisSession.ToString(CultureInfo.InvariantCulture));
        Flush();
    }

    /// <summary>Записать событие. Параметры — пары «ключ, значение».</summary>
    public static void Track(string name, params string[] keyValues)
    {
        if (!IsEnabled || string.IsNullOrEmpty(name))
            return;

        if (string.IsNullOrEmpty(SessionId))
            StartSessionSilently();

        var e = new AnalyticsEvent
        {
            name = name,
            time = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            session = SessionId,
            install = InstallId,
            version = BuildInfo.Current.version,
            platform = Application.platform.ToString()
        };

        if (keyValues != null)
            for (int i = 0; i + 1 < keyValues.Length; i += 2)
                e.data.Add(new AnalyticsParam(keyValues[i], keyValues[i + 1] ?? ""));

        string line = JsonUtility.ToJson(e);
        Buffer.Add(line);
        UploadQueue.Add(line);
        EventsThisSession++;
        EventTracked?.Invoke(e);

        if (Buffer.Count >= FlushEvery || name == "error" || name == "level_end")
            Flush();
    }

    private static void StartSessionSilently()
    {
        InstallId = PlayerPrefs.GetString(InstallIdKey, "unknown");
        SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
        SessionStartRealtime = Time.realtimeSinceStartup;
    }

    /// <summary>Сбросить буфер в файл (и запустить отправку, если задан сервер).</summary>
    public static void Flush()
    {
        if (Buffer.Count == 0)
            return;

        try
        {
            Directory.CreateDirectory(DataFolder);
            RotateIfTooLarge();
            var sb = new StringBuilder();
            foreach (string line in Buffer)
                sb.AppendLine(line);
            File.AppendAllText(EventsFile, sb.ToString());
            Buffer.Clear();
        }
        catch (Exception ex)
        {
            // Ошибка записи не должна ломать игру (нет места, нет прав и т. п.)
            Debug.LogWarning("[Analytics] Не удалось записать события: " + ex.Message);
        }

        TryUpload();
    }

    private static void RotateIfTooLarge()
    {
        if (!File.Exists(EventsFile) || new FileInfo(EventsFile).Length < MaxFileBytes)
            return;
        string archive = Path.Combine(DataFolder, $"events_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jsonl");
        File.Move(EventsFile, archive);
    }

    private static void TryUpload()
    {
        string endpoint = RemoteConfig.Current.analyticsEndpoint;
        if (_uploading || UploadQueue.Count == 0 || string.IsNullOrEmpty(endpoint) || LiveOpsHost.Instance == null)
        {
            if (string.IsNullOrEmpty(endpoint))
                UploadQueue.Clear(); // сервер не настроен — данные остаются только локально
            return;
        }

        var batch = new List<string>(UploadQueue);
        UploadQueue.Clear();
        LiveOpsHost.Instance.StartCoroutine(Upload(endpoint, batch));
    }

    private static IEnumerator Upload(string endpoint, List<string> batch)
    {
        _uploading = true;
        string body = "[" + string.Join(",", batch) + "]";
        using (var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // Вернём события в очередь и попробуем при следующей отправке
                UploadQueue.InsertRange(0, batch);
                if (UploadQueue.Count > 500)
                    UploadQueue.RemoveRange(500, UploadQueue.Count - 500);
            }
        }
        _uploading = false;
    }

    /// <summary>Удалить все локальные данные аналитики (право игрока на удаление данных).</summary>
    public static void DeleteLocalData()
    {
        Buffer.Clear();
        UploadQueue.Clear();
        try
        {
            if (Directory.Exists(DataFolder))
                Directory.Delete(DataFolder, true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Analytics] Не удалось удалить данные: " + ex.Message);
        }
    }

    /// <summary>Число в строку с точкой в качестве разделителя (независимо от языка системы).</summary>
    public static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
