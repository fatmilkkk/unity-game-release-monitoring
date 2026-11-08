using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Модульные тесты логики (EditMode): не требуют запуска сцены и выполняются за доли секунды.
/// Window → General → Test Runner → EditMode → Run All.
/// </summary>
public class LogicTests
{
    private GameObject _go;

    [TearDown]
    public void TearDown()
    {
        if (_go != null)
            Object.DestroyImmediate(_go);
        MobileInput.Reset();
    }

    internal static void SetPrivate(object target, string field, object value)
    {
        Type type = target.GetType();
        FieldInfo info = null;
        while (type != null && info == null)
        {
            info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            type = type.BaseType;
        }
        Assert.IsNotNull(info, $"Поле {field} не найдено");
        info.SetValue(target, value);
    }

    // ───── Предметы ─────

    [Test]
    public void ItemDatabase_AllItemTypes_HaveNameDescriptionAndIcon()
    {
        foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
        {
            Assert.IsFalse(string.IsNullOrEmpty(ItemDatabase.GetName(type)), $"Нет названия у {type}");
            Assert.IsFalse(string.IsNullOrEmpty(ItemDatabase.GetDescription(type)), $"Нет описания у {type}");
            Assert.IsFalse(string.IsNullOrEmpty(ItemDatabase.GetIcon(type)), $"Нет значка у {type}");
            Assert.AreNotEqual(Color.white, ItemDatabase.GetColor(type), $"Нет цвета у {type}");
        }
    }

    [Test]
    public void Inventory_SameItems_StackInOneSlot()
    {
        _go = new GameObject("Inventory");
        var inventory = _go.AddComponent<Inventory>();

        inventory.Add(ItemType.Medkit);
        inventory.Add(ItemType.Medkit, 2);

        Assert.AreEqual(1, inventory.Items.Count);
        Assert.AreEqual(3, inventory.GetCount(ItemType.Medkit));
    }

    [Test]
    public void Inventory_RespectsCapacity()
    {
        _go = new GameObject("Inventory");
        var inventory = _go.AddComponent<Inventory>();
        SetPrivate(inventory, "capacity", 2);

        Assert.IsTrue(inventory.Add(ItemType.Medkit));
        Assert.IsTrue(inventory.Add(ItemType.Shield));
        Assert.IsFalse(inventory.Add(ItemType.SpeedBoost), "Третий тип предмета не должен поместиться");
        Assert.IsTrue(inventory.Add(ItemType.Medkit), "Стопка существующего предмета должна увеличиваться");
        Assert.AreEqual(2, inventory.Items.Count);
    }

    [Test]
    public void Inventory_AddZeroOrNegative_IsIgnored()
    {
        _go = new GameObject("Inventory");
        var inventory = _go.AddComponent<Inventory>();

        Assert.IsFalse(inventory.Add(ItemType.Medkit, 0));
        Assert.IsFalse(inventory.Add(ItemType.Medkit, -3));
        Assert.AreEqual(0, inventory.Items.Count);
    }

    [Test]
    public void Inventory_UseMissingItem_IsRejectedWithEvent()
    {
        _go = new GameObject("Inventory");
        var inventory = _go.AddComponent<Inventory>();
        int rejected = 0;
        inventory.UseRejected += (t, reason) => rejected++;

        Assert.IsFalse(inventory.Use(ItemType.Shield));
        Assert.AreEqual(1, rejected);
    }

    [Test]
    public void Inventory_AddRaisesEvents()
    {
        _go = new GameObject("Inventory");
        var inventory = _go.AddComponent<Inventory>();
        int changed = 0, added = 0;
        inventory.Changed += () => changed++;
        inventory.ItemAdded += t => added++;

        inventory.Add(ItemType.SpeedBoost);

        Assert.AreEqual(1, changed);
        Assert.AreEqual(1, added);
    }

    // ───── Кошелёк ─────

    [Test]
    public void Wallet_AddAndSpend()
    {
        _go = new GameObject("Wallet");
        var wallet = _go.AddComponent<Wallet>();
        SetPrivate(wallet, "saveBetweenSessions", false);
        int lastValue = -1;
        wallet.CoinsChanged += v => lastValue = v;

        wallet.AddCoins(5);
        Assert.AreEqual(5, wallet.Coins);
        Assert.AreEqual(5, lastValue);

        Assert.IsFalse(wallet.TrySpend(10), "Нельзя потратить больше, чем есть");
        Assert.IsTrue(wallet.TrySpend(3));
        Assert.AreEqual(2, wallet.Coins);
    }

    [Test]
    public void Wallet_IgnoresNonPositiveAmounts()
    {
        _go = new GameObject("Wallet");
        var wallet = _go.AddComponent<Wallet>();
        SetPrivate(wallet, "saveBetweenSessions", false);

        wallet.AddCoins(0);
        wallet.AddCoins(-5);
        Assert.AreEqual(0, wallet.Coins);
        Assert.IsFalse(wallet.TrySpend(0));
        Assert.IsFalse(wallet.TrySpend(-1));
    }

    // ───── Время, анимации, настройки ─────

    [TestCase(0f, "00:00")]
    [TestCase(59.9f, "00:59")]
    [TestCase(61f, "01:01")]
    [TestCase(3599f, "59:59")]
    [TestCase(-5f, "00:00")]
    public void LevelStats_FormatTime(float seconds, string expected)
    {
        Assert.AreEqual(expected, LevelStats.FormatTime(seconds));
    }

    [Test]
    public void UIEasing_StartsAtZeroAndEndsAtOne()
    {
        Assert.AreEqual(0f, UIEasing.OutCubic(0f), 1e-4f);
        Assert.AreEqual(1f, UIEasing.OutCubic(1f), 1e-4f);
        Assert.AreEqual(0f, UIEasing.InCubic(0f), 1e-4f);
        Assert.AreEqual(1f, UIEasing.InCubic(1f), 1e-4f);
        Assert.AreEqual(0f, UIEasing.OutBack(0f), 1e-4f);
        Assert.AreEqual(1f, UIEasing.OutBack(1f), 1e-4f);
        Assert.AreEqual(1f, UIEasing.OutElastic(1f), 1e-4f);
    }

    [Test]
    public void UIEasing_OutBack_Overshoots()
    {
        float max = 0f;
        for (float t = 0f; t <= 1f; t += 0.01f)
            max = Mathf.Max(max, UIEasing.OutBack(t));
        Assert.Greater(max, 1f, "OutBack должен давать «перелёт» больше 1");
    }

    [TestCase(0, 6, 0)]
    [TestCase(1, 6, 2)]
    [TestCase(2, 6, 5)]
    [TestCase(2, 1, 0)]
    [TestCase(1, 3, 1)]
    public void GameSettings_MapQualityLevel(int quality, int levels, int expected)
    {
        Assert.AreEqual(expected, GameSettings.MapQualityLevel(quality, levels));
    }

    [Test]
    public void GameSettings_FpsLimitTablesMatch()
    {
        Assert.AreEqual(GameSettings.FpsLimits.Length, GameSettings.FpsLimitNames.Length);
        Assert.AreEqual(3, GameSettings.QualityNames.Length);
    }

    [Test]
    public void MobileInput_JumpIsConsumedOnce()
    {
        MobileInput.PressJump();
        Assert.IsTrue(MobileInput.ConsumeJump());
        Assert.IsFalse(MobileInput.ConsumeJump());
    }

    [Test]
    public void PlatformVisibility_HidesOnConfiguredPlatforms()
    {
        _go = new GameObject("Element");
        var visibility = _go.AddComponent<PlatformVisibility>();
        SetPrivate(visibility, "hideOnWebGL", true);
        SetPrivate(visibility, "hideOnMobile", true);

        Assert.IsTrue(visibility.ShouldHide(RuntimePlatform.WebGLPlayer, false));
        Assert.IsTrue(visibility.ShouldHide(RuntimePlatform.Android, true));
        Assert.IsFalse(visibility.ShouldHide(RuntimePlatform.WindowsPlayer, false));
    }

    // ───── Пул эффектов ─────

    [Test]
    public void EffectPool_ReusesReleasedObjects()
    {
        EffectPool.Clear();
        _go = new GameObject("EffectTemplate");

        GameObject first = EffectPool.Spawn(_go, Vector3.zero);
        Assert.IsTrue(EffectPool.Release(first));
        Assert.IsFalse(first.activeSelf);
        Assert.AreEqual(1, EffectPool.FreeCount(_go));

        int reusedBefore = EffectPool.ReusedCount;
        GameObject second = EffectPool.Spawn(_go, Vector3.one);
        Assert.AreSame(first, second, "Должен вернуться тот же объект из пула");
        Assert.IsTrue(second.activeSelf);
        Assert.AreEqual(Vector3.one, second.transform.position);
        Assert.AreEqual(reusedBefore + 1, EffectPool.ReusedCount);

        Object.DestroyImmediate(second);
        EffectPool.Clear();
    }

    [Test]
    public void EffectPool_ReleaseOfForeignObject_ReturnsFalse()
    {
        _go = new GameObject("NotPooled");
        Assert.IsFalse(EffectPool.Release(_go));
        Assert.IsTrue(_go.activeSelf);
    }
}
