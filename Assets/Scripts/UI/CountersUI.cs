using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Счётчики уровня: таймер, собранные монеты уровня (X/Y), найденные бонусы,
/// активные эффекты (щит, ускорение) с оставшимся временем.
/// </summary>
public class CountersUI : MonoBehaviour
{
    [SerializeField] private LevelStats stats;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerController controller;

    [SerializeField] private Text timerText;
    [SerializeField] private Text levelCoinsText;
    [SerializeField] private Text bonusesText;
    [SerializeField] private Text effectsText;
    [SerializeField] private UIPunch levelCoinsPunch;
    [SerializeField] private UIPunch bonusesPunch;

    private int _lastCoins = -1;
    private int _lastBonuses = -1;

    private void OnEnable()
    {
        if (stats != null)
            stats.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (stats != null)
            stats.Changed -= Refresh;
    }

    private void Start() => Refresh();

    private void Refresh()
    {
        if (stats == null)
            return;

        if (levelCoinsText != null)
            levelCoinsText.text = $"Монеты уровня: {stats.CoinsCollected}/{stats.TotalCoins}";
        if (bonusesText != null)
            bonusesText.text = $"Бонусы: {stats.BonusesFound}/{stats.TotalBonuses}";

        if (_lastCoins >= 0 && stats.CoinsCollected > _lastCoins && levelCoinsPunch != null)
            levelCoinsPunch.PunchScale(0.15f, 0.25f);
        if (_lastBonuses >= 0 && stats.BonusesFound > _lastBonuses && bonusesPunch != null)
            bonusesPunch.PunchScale(0.3f, 0.4f);

        _lastCoins = stats.CoinsCollected;
        _lastBonuses = stats.BonusesFound;
    }

    private void Update()
    {
        if (stats != null && timerText != null)
            timerText.text = LevelStats.FormatTime(stats.ElapsedTime);

        if (effectsText == null)
            return;

        string text = string.Empty;
        if (health != null && health.IsShielded)
            text += $"<color=#66B3FF>Щит: {health.ShieldTimeLeft:0.0} с</color>   ";
        if (controller != null && controller.IsBoosted)
            text += $"<color=#FFC040>Ускорение: {controller.SpeedBoostTimeLeft:0.0} с</color>";
        effectsText.text = text;
    }
}
