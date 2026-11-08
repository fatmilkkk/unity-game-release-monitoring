using System;
using UnityEngine;

/// <summary>
/// Статистика прохождения уровня: время, собранные монеты уровня, найденные бонусы.
/// Уровень считается пройденным, когда собраны все монеты.
/// </summary>
public class LevelStats : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Время (с) для трёх и двух звёзд")]
    [SerializeField] private float threeStarTime = 90f;
    [SerializeField] private float twoStarTime = 150f;

    public float ElapsedTime { get; private set; }
    public int CoinsCollected { get; private set; }
    public int TotalCoins { get; private set; }
    public int BonusesFound { get; private set; }
    public int TotalBonuses { get; private set; }
    public bool IsFinished { get; private set; }

    public int Stars => ElapsedTime <= threeStarTime ? 3 : ElapsedTime <= twoStarTime ? 2 : 1;

    public event Action Changed;
    public event Action Victory;

    private void OnEnable()
    {
        Coin.Collected += OnCoinCollected;
        BonusZone.RewardGiven += OnBonusFound;
    }

    private void OnDisable()
    {
        Coin.Collected -= OnCoinCollected;
        BonusZone.RewardGiven -= OnBonusFound;
    }

    private void Start()
    {
        TotalCoins = FindObjectsOfType<Coin>().Length;

        int bonuses = 0;
        foreach (BonusZone zone in FindObjectsOfType<BonusZone>())
            if (zone.GivesReward)
                bonuses++;
        TotalBonuses = bonuses;

        Changed?.Invoke();
    }

    private void Update()
    {
        if (IsFinished || (playerHealth != null && playerHealth.IsDead))
            return;
        ElapsedTime += Time.deltaTime;
    }

    private void OnCoinCollected(Coin coin, Vector3 position)
    {
        if (IsFinished)
            return;

        CoinsCollected++;
        Changed?.Invoke();

        if (TotalCoins > 0 && CoinsCollected >= TotalCoins)
        {
            IsFinished = true;
            Victory?.Invoke();
        }
    }

    private void OnBonusFound(BonusZone zone, ItemType item)
    {
        BonusesFound++;
        Changed?.Invoke();
    }

    public static string FormatTime(float seconds) => FormatTime(Mathf.FloorToInt(Mathf.Max(0f, seconds)));

    public static string FormatTime(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
