using System;
using UnityEngine;

/// <summary>
/// Кошелёк игрока: хранит собранные монеты и сообщает об изменениях интерфейсу.
/// При включённом сохранении баланс хранится в PlayerPrefs между запусками игры.
/// </summary>
public class Wallet : MonoBehaviour
{
    private const string SaveKey = "Wallet.Coins";

    [Tooltip("Сохранять баланс между запусками игры")]
    [SerializeField] private bool saveBetweenSessions = true;

    public int Coins { get; private set; }

    /// <summary>Вызывается при изменении баланса, передаёт новое значение.</summary>
    public event Action<int> CoinsChanged;

    private void Awake()
    {
        if (saveBetweenSessions)
            Coins = PlayerPrefs.GetInt(SaveKey, 0);
    }

    private void Start()
    {
        CoinsChanged?.Invoke(Coins);
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        Coins += amount;
        Save();
        CoinsChanged?.Invoke(Coins);
    }

    /// <summary>Потратить монеты. Возвращает false, если монет недостаточно.</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > Coins)
            return false;

        Coins -= amount;
        Save();
        CoinsChanged?.Invoke(Coins);
        return true;
    }

    [ContextMenu("Обнулить кошелёк")]
    public void ResetWallet()
    {
        Coins = 0;
        Save();
        CoinsChanged?.Invoke(Coins);
    }

    private void Save()
    {
        if (!saveBetweenSessions)
            return;

        PlayerPrefs.SetInt(SaveKey, Coins);
        PlayerPrefs.Save();
    }
}
