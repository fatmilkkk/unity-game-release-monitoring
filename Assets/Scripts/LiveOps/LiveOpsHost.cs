using UnityEngine;

/// <summary>
/// Постоянный объект сервисов выпуска: создаётся при запуске игры, живёт между сценами.
/// Запускает сбор ошибок, сессию аналитики и загрузку удалённого конфига;
/// при сворачивании и выходе сохраняет события.
/// </summary>
public class LiveOpsHost : MonoBehaviour
{
    public static LiveOpsHost Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject("LiveOps");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.DontSave;
        Instance = go.AddComponent<LiveOpsHost>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CrashReporter.Register();
        if (AnalyticsService.IsEnabled)
            AnalyticsService.StartSession();
    }

    private void Start()
    {
        StartCoroutine(RemoteConfig.Load());
    }

    private void OnApplicationPause(bool paused)
    {
        // На мобильных при сворачивании приложение может быть выгружено без OnApplicationQuit
        if (paused)
        {
            AnalyticsService.Track("app_background");
            AnalyticsService.Flush();
        }
    }

    private void OnApplicationQuit()
    {
        CrashReporter.ReportSummary();
        AnalyticsService.EndSession();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
