using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ячейка инвентаря: тип предмета и количество.</summary>
[Serializable]
public class InventoryEntry
{
    public ItemType Type;
    public int Count;

    public InventoryEntry(ItemType type, int count)
    {
        Type = type;
        Count = count;
    }
}

/// <summary>
/// Инвентарь игрока. Хранит предметы (одинаковые складываются в одну ячейку),
/// применяет их эффект при использовании и сообщает интерфейсу об изменениях.
/// </summary>
public class Inventory : MonoBehaviour
{
    [SerializeField] private int capacity = 6;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerController controller;

    private readonly List<InventoryEntry> _items = new List<InventoryEntry>();

    public IReadOnlyList<InventoryEntry> Items => _items;
    public int Capacity => capacity;

    public event Action Changed;
    public event Action<ItemType> ItemAdded;
    public event Action<ItemType> ItemUsed;
    /// <summary>Предмет нельзя использовать сейчас (тип, причина).</summary>
    public event Action<ItemType, string> UseRejected;

    private void Awake()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (controller == null) controller = GetComponent<PlayerController>();
    }

    public int GetCount(ItemType type)
    {
        InventoryEntry entry = Find(type);
        return entry != null ? entry.Count : 0;
    }

    public bool Add(ItemType type, int amount = 1)
    {
        if (amount <= 0)
            return false;

        InventoryEntry entry = Find(type);
        if (entry != null)
        {
            entry.Count += amount;
        }
        else
        {
            if (_items.Count >= capacity)
                return false;
            _items.Add(new InventoryEntry(type, amount));
        }

        ItemAdded?.Invoke(type);
        Changed?.Invoke();
        return true;
    }

    public bool Use(ItemType type)
    {
        InventoryEntry entry = Find(type);
        if (entry == null || entry.Count <= 0)
        {
            UseRejected?.Invoke(type, "Нет в инвентаре");
            return false;
        }

        if (health != null && health.IsDead)
            return false;

        if (!ApplyEffect(type, out string reason))
        {
            UseRejected?.Invoke(type, reason);
            return false;
        }

        entry.Count--;
        if (entry.Count <= 0)
            _items.Remove(entry);

        ItemUsed?.Invoke(type);
        Changed?.Invoke();
        return true;
    }

    private bool ApplyEffect(ItemType type, out string reason)
    {
        reason = string.Empty;
        switch (type)
        {
            case ItemType.Medkit:
                if (health == null) return false;
                if (health.CurrentHealth >= health.MaxHealth)
                {
                    reason = "Здоровье уже полное";
                    return false;
                }
                health.Heal(ItemDatabase.MedkitHeal);
                return true;

            case ItemType.Shield:
                if (health == null) return false;
                health.GrantShield(ItemDatabase.ShieldDuration);
                return true;

            case ItemType.SpeedBoost:
                if (controller == null) return false;
                controller.ApplySpeedBoost(ItemDatabase.SpeedMultiplier, ItemDatabase.SpeedDuration);
                return true;
        }
        return false;
    }

    private InventoryEntry Find(ItemType type)
    {
        foreach (InventoryEntry entry in _items)
            if (entry.Type == type)
                return entry;
        return null;
    }
}
