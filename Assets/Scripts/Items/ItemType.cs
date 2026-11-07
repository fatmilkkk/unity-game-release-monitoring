using UnityEngine;

/// <summary>Типы предметов, которые игрок получает от бонусов.</summary>
public enum ItemType
{
    Medkit = 0,
    Shield = 1,
    SpeedBoost = 2
}

/// <summary>Описание предметов: название, описание, цвет и значок для интерфейса, параметры эффекта.</summary>
public static class ItemDatabase
{
    public const int MedkitHeal = 40;
    public const float ShieldDuration = 6f;
    public const float SpeedMultiplier = 1.5f;
    public const float SpeedDuration = 6f;

    public static string GetName(ItemType type)
    {
        switch (type)
        {
            case ItemType.Medkit: return "Аптечка";
            case ItemType.Shield: return "Щит";
            case ItemType.SpeedBoost: return "Ускоритель";
            default: return type.ToString();
        }
    }

    public static string GetDescription(ItemType type)
    {
        switch (type)
        {
            case ItemType.Medkit:
                return $"Восстанавливает {MedkitHeal} HP. Нельзя использовать при полном здоровье.";
            case ItemType.Shield:
                return $"На {ShieldDuration:0} с защищает от урона препятствий и взрыва бочки.";
            case ItemType.SpeedBoost:
                return $"На {SpeedDuration:0} с увеличивает скорость шара в {SpeedMultiplier:0.#} раза. Осторожно с бочками!";
            default:
                return string.Empty;
        }
    }

    public static Color GetColor(ItemType type)
    {
        switch (type)
        {
            case ItemType.Medkit: return new Color(0.95f, 0.3f, 0.35f);
            case ItemType.Shield: return new Color(0.3f, 0.65f, 1f);
            case ItemType.SpeedBoost: return new Color(1f, 0.75f, 0.2f);
            default: return Color.white;
        }
    }

    /// <summary>Символ-значок предмета (отображается поверх цветного круга).</summary>
    public static string GetIcon(ItemType type)
    {
        switch (type)
        {
            case ItemType.Medkit: return "+";
            case ItemType.Shield: return "Щ";
            case ItemType.SpeedBoost: return "»";
            default: return "?";
        }
    }
}
