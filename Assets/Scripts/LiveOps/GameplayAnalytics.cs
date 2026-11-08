using System.Globalization;
using UnityEngine;

/// <summary>
/// Игровые события аналитики уровня: начало, прогресс по монетам (25/50/75 %),
/// найденные бонусы, использованные предметы, гибель (причина и место), победа, выход без завершения,
/// а также периодические замеры производительности.
/// </summary>
public class GameplayAnalytics : MonoBehaviour
{
    private const string AttemptsKey = "Analytics.LevelAttempts";

    [SerializeField] private PlayerHealth health;
    [SerializeField] private LevelStats stats;
    [SerializeField] private Inventory inventory;
    [SerializeField] private string levelName = "Main";
    [SerializeField] private float perfSampleInterval = 30f;

    private bool _ended;
    private int _milestone;          // последняя отправленная доля прогресса (0, 25, 50, 75)
    private float _perfTime;
    private int _perfFrames;
    private int _attempt;

    private void OnEnable()
    {
        if (health != null) health.Died += OnDied;
        if (stats != null)
        {
            stats.Victory += OnVictory;
            stats.Changed += OnProgress;
        }
        if (inventory != null) inventory.ItemUsed += OnItemUsed;
        BonusZone.RewardGiven += OnBonus;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
        if (stats != null)
        {
            stats.Victory -= OnVictory;
            stats.Changed -= OnProgress;
        }
        if (inventory != null) inventory.ItemUsed -= OnItemUsed;
        BonusZone.RewardGiven -= OnBonus;
    }

    private void Start()
    {
        _attempt = PlayerPrefs.GetInt(AttemptsKey, 0) + 1;
        PlayerPrefs.SetInt(AttemptsKey, _attempt);

        AnalyticsService.Track("level_start",
            "level", levelName,
            "attempt", _attempt.ToString(CultureInfo.InvariantCulture),
            "config", RemoteConfig.Current.configVersion.ToString(CultureInfo.InvariantCulture));
    }

    private void Update()
    {
        if (GamePause.IsPaused || _ended)
            return;

        _perfTime += Time.unscaledDeltaTime;
        _perfFrames++;
        if (_perfTime < perfSampleInterval)
            return;

        AnalyticsService.Track("perf",
            "fps", AnalyticsService.F(_perfFrames / _perfTime),
            "quality", GameSettings.Quality.ToString(CultureInfo.InvariantCulture),
            "screen", $"{Screen.width}x{Screen.height}");
        _perfTime = 0f;
        _perfFrames = 0;
    }

    private void OnProgress()
    {
        if (stats == null || stats.TotalCoins <= 0)
            return;

        int percent = stats.CoinsCollected * 100 / stats.TotalCoins;
        while (_milestone < 75 && percent >= _milestone + 25)
        {
            _milestone += 25;
            AnalyticsService.Track("progress",
                "level", levelName,
                "percent", _milestone.ToString(CultureInfo.InvariantCulture),
                "time", AnalyticsService.F(stats.ElapsedTime));
        }
    }

    private void OnBonus(BonusZone zone, ItemType item)
    {
        AnalyticsService.Track("bonus_found", "item", item.ToString(), "time", AnalyticsService.F(Elapsed));
    }

    private void OnItemUsed(ItemType item)
    {
        AnalyticsService.Track("item_used",
            "item", item.ToString(),
            "hp", health != null ? health.CurrentHealth.ToString(CultureInfo.InvariantCulture) : "");
    }

    private void OnDied()
    {
        if (_ended) return;
        _ended = true;
        Vector3 p = health.transform.position;
        AnalyticsService.Track("death",
            "level", levelName,
            "cause", string.IsNullOrEmpty(health.LastDamageSource) ? "unknown" : health.LastDamageSource,
            "x", AnalyticsService.F(p.x), "y", AnalyticsService.F(p.y), "z", AnalyticsService.F(p.z),
            "time", AnalyticsService.F(Elapsed),
            "coins", Coins());
        EndLevel("death");
    }

    private void OnVictory()
    {
        if (_ended) return;
        _ended = true;
        EndLevel("victory");
    }

    private void OnDestroy()
    {
        // Перезапуск или выход в меню без завершения уровня
        if (!_ended)
            EndLevel("abandoned");
    }

    private void EndLevel(string result)
    {
        AnalyticsService.Track("level_end",
            "level", levelName,
            "result", result,
            "time", AnalyticsService.F(Elapsed),
            "coins", Coins(),
            "total", stats != null ? stats.TotalCoins.ToString(CultureInfo.InvariantCulture) : "0",
            "stars", result == "victory" && stats != null ? stats.Stars.ToString(CultureInfo.InvariantCulture) : "0",
            "attempt", _attempt.ToString(CultureInfo.InvariantCulture));
    }

    private float Elapsed => stats != null ? stats.ElapsedTime : Time.timeSinceLevelLoad;

    private string Coins() => stats != null ? stats.CoinsCollected.ToString(CultureInfo.InvariantCulture) : "0";
}
