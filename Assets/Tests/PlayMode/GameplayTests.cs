using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Интеграционные тесты игровых механик (PlayMode): создают объекты, запускают физику и проверяют результат.
/// Window → General → Test Runner → PlayMode → Run All.
/// </summary>
public class GameplayTests
{
    private readonly List<GameObject> _created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in _created)
            if (go != null)
                Object.Destroy(go);
        _created.Clear();
        GamePause.Reset();
        MobileInput.Reset();
        EffectPool.Clear();
    }

    // ───── Помощники ─────

    private static void SetPrivate(object target, string field, object value)
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

    private GameObject Track(GameObject go)
    {
        _created.Add(go);
        return go;
    }

    private GameObject CreatePlayer(Vector3 position, Vector3 velocity)
    {
        GameObject player = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
        player.name = "TestPlayer";
        player.transform.position = position;

        var rb = player.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.velocity = velocity;

        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerHealth>();
        var wallet = player.AddComponent<Wallet>();
        SetPrivate(wallet, "saveBetweenSessions", false);
        player.AddComponent<Inventory>();
        return player;
    }

    private static IEnumerator WaitFixed(int steps)
    {
        for (int i = 0; i < steps; i++)
            yield return new WaitForFixedUpdate();
    }

    // ───── Здоровье ─────

    [UnityTest]
    public IEnumerator Health_TakeDamage_ReducesHp_WithInvulnerabilityWindow()
    {
        var health = CreatePlayer(Vector3.zero, Vector3.zero).GetComponent<PlayerHealth>();
        yield return null;

        Assert.IsTrue(health.TakeDamage(25));
        Assert.AreEqual(75, health.CurrentHealth);
        Assert.IsFalse(health.TakeDamage(25), "Сразу после удара игрок неуязвим");
        Assert.AreEqual(75, health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator Health_ZeroHp_KillsPlayer_AndRaisesDied()
    {
        var health = CreatePlayer(Vector3.zero, Vector3.zero).GetComponent<PlayerHealth>();
        SetPrivate(health, "autoRestart", false);
        int died = 0;
        health.Died += () => died++;
        yield return null;

        health.TakeDamage(1000);

        Assert.IsTrue(health.IsDead);
        Assert.AreEqual(0, health.CurrentHealth);
        Assert.AreEqual(1, died);
        Assert.IsFalse(health.TakeDamage(10), "Мёртвый игрок не получает урон");
    }

    [UnityTest]
    public IEnumerator Health_Shield_BlocksDamageAndBarrelKill_ButNotFall()
    {
        var health = CreatePlayer(Vector3.zero, Vector3.zero).GetComponent<PlayerHealth>();
        SetPrivate(health, "autoRestart", false);
        yield return null;

        health.GrantShield(5f);
        Assert.IsTrue(health.IsShielded);
        Assert.IsFalse(health.TakeDamage(50));
        health.Kill();
        Assert.IsFalse(health.IsDead, "Щит спасает от взрыва бочки");

        health.Kill(true);
        Assert.IsTrue(health.IsDead, "Падение за пределы уровня игнорирует щит");
    }

    [UnityTest]
    public IEnumerator Health_Heal_IsClampedToMax()
    {
        var health = CreatePlayer(Vector3.zero, Vector3.zero).GetComponent<PlayerHealth>();
        yield return null;

        health.TakeDamage(30);
        health.Heal(1000);
        Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
    }

    [UnityTest]
    public IEnumerator Health_FallingBelowKillHeight_KillsPlayer()
    {
        var health = CreatePlayer(new Vector3(0f, -50f, 0f), Vector3.zero).GetComponent<PlayerHealth>();
        SetPrivate(health, "autoRestart", false);
        yield return null;
        yield return null;

        Assert.IsTrue(health.IsDead);
    }

    // ───── Предметы ─────

    [UnityTest]
    public IEnumerator Inventory_Medkit_RejectedAtFullHp_HealsWhenHurt()
    {
        GameObject player = CreatePlayer(Vector3.zero, Vector3.zero);
        var health = player.GetComponent<PlayerHealth>();
        var inventory = player.GetComponent<Inventory>();
        yield return null;

        inventory.Add(ItemType.Medkit);
        Assert.IsFalse(inventory.Use(ItemType.Medkit), "При полном HP аптечка не тратится");
        Assert.AreEqual(1, inventory.GetCount(ItemType.Medkit));

        health.TakeDamage(60);
        Assert.IsTrue(inventory.Use(ItemType.Medkit));
        Assert.AreEqual(40 + ItemDatabase.MedkitHeal, health.CurrentHealth);
        Assert.AreEqual(0, inventory.Items.Count, "Пустая ячейка удаляется");
    }

    [UnityTest]
    public IEnumerator Inventory_ShieldAndSpeedBoost_ApplyEffects()
    {
        GameObject player = CreatePlayer(Vector3.zero, Vector3.zero);
        var inventory = player.GetComponent<Inventory>();
        yield return null;

        inventory.Add(ItemType.Shield);
        inventory.Add(ItemType.SpeedBoost);
        Assert.IsTrue(inventory.Use(ItemType.Shield));
        Assert.IsTrue(inventory.Use(ItemType.SpeedBoost));

        Assert.IsTrue(player.GetComponent<PlayerHealth>().IsShielded);
        Assert.IsTrue(player.GetComponent<PlayerController>().IsBoosted);
    }

    // ───── Монеты ─────

    [UnityTest]
    public IEnumerator Coin_PlayerTouch_AddsToWallet_AndRaisesEvent()
    {
        GameObject coinGo = Track(new GameObject("TestCoin"));
        var trigger = coinGo.AddComponent<SphereCollider>();
        trigger.radius = 0.6f;
        trigger.isTrigger = true;
        coinGo.AddComponent<Coin>();

        int events = 0;
        Action<Coin, Vector3> handler = (c, p) => events++;
        Coin.Collected += handler;

        GameObject player = CreatePlayer(new Vector3(-3f, 0f, 0f), new Vector3(10f, 0f, 0f));
        var wallet = player.GetComponent<Wallet>();
        int before = wallet.Coins;

        yield return WaitFixed(40);
        Coin.Collected -= handler;

        Assert.AreEqual(before + 1, wallet.Coins);
        Assert.AreEqual(1, events);
        Assert.IsTrue(coinGo == null, "Собранная монета удаляется");
    }

    [UnityTest]
    public IEnumerator Coin_CannotBeCollectedTwice()
    {
        GameObject coinGo = Track(new GameObject("TestCoin"));
        coinGo.AddComponent<SphereCollider>();
        var coin = coinGo.AddComponent<Coin>();
        var wallet = CreatePlayer(new Vector3(0f, 100f, 0f), Vector3.zero).GetComponent<Wallet>();
        int before = wallet.Coins;
        yield return null;

        Assert.IsTrue(coin.TryCollect(wallet));
        Assert.IsFalse(coin.TryCollect(wallet));
        Assert.AreEqual(before + 1, wallet.Coins);
    }

    // ───── Бочка со взрывчаткой ─────

    private GameObject CreateBarrel()
    {
        GameObject barrel = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
        barrel.name = "TestBarrel";
        var rb = barrel.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.mass = 3f;
        barrel.AddComponent<ExplosiveBarrel>();
        return barrel;
    }

    [UnityTest]
    public IEnumerator Barrel_FastImpact_ExplodesAndKillsPlayer()
    {
        GameObject barrel = CreateBarrel();
        var health = CreatePlayer(new Vector3(-3f, 0f, 0f), new Vector3(12f, 0f, 0f)).GetComponent<PlayerHealth>();
        SetPrivate(health, "autoRestart", false);

        yield return WaitFixed(40);

        Assert.IsTrue(health.IsDead, "Удар на скорости 12 м/с должен взорвать бочку");
        Assert.IsTrue(barrel == null, "Взорванная бочка удаляется");
    }

    [UnityTest]
    public IEnumerator Barrel_SlowImpact_OnlyPushesBarrel()
    {
        GameObject barrel = CreateBarrel();
        var health = CreatePlayer(new Vector3(-2f, 0f, 0f), new Vector3(3f, 0f, 0f)).GetComponent<PlayerHealth>();

        yield return WaitFixed(50);

        Assert.IsFalse(health.IsDead, "Удар на скорости 3 м/с не должен взрывать бочку");
        Assert.IsTrue(barrel != null);
        Assert.Greater(barrel.transform.position.x, 0.01f, "Бочку должно оттолкнуть");
    }

    // ───── Подвижное препятствие ─────

    [UnityTest]
    public IEnumerator MovingObstacle_Contact_DamagesAndKnocksBack()
    {
        GameObject obstacle = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        obstacle.name = "TestObstacle";
        obstacle.AddComponent<Rigidbody>();
        var moving = obstacle.AddComponent<MovingObstacle>();
        SetPrivate(moving, "moveOffset", Vector3.zero);

        GameObject player = CreatePlayer(new Vector3(-3f, 0f, 0f), new Vector3(8f, 0f, 0f));
        var health = player.GetComponent<PlayerHealth>();

        yield return WaitFixed(40);

        Assert.Less(health.CurrentHealth, health.MaxHealth, "Препятствие должно нанести урон");
        Assert.Less(player.GetComponent<Rigidbody>().velocity.x, 0f, "Игрока должно отбросить назад");
    }

    // ───── Бонус ─────

    [UnityTest]
    public IEnumerator BonusZone_GivesRewardOnlyOnce()
    {
        GameObject zoneGo = Track(new GameObject("TestBonus"));
        zoneGo.AddComponent<SphereCollider>().radius = 1.5f;
        var zone = zoneGo.AddComponent<BonusZone>();
        SetPrivate(zone, "rewardItem", ItemType.Shield);

        int rewards = 0;
        Action<BonusZone, ItemType> handler = (z, i) => rewards++;
        BonusZone.RewardGiven += handler;

        GameObject player = CreatePlayer(new Vector3(-4f, 0f, 0f), new Vector3(10f, 0f, 0f));
        var rb = player.GetComponent<Rigidbody>();
        yield return WaitFixed(50);   // пролетели насквозь
        rb.velocity = new Vector3(-10f, 0f, 0f);
        yield return WaitFixed(50);   // и обратно

        BonusZone.RewardGiven -= handler;

        Assert.AreEqual(1, rewards);
        Assert.AreEqual(1, player.GetComponent<Inventory>().GetCount(ItemType.Shield));
        Assert.IsTrue(zone.RewardCollected);
    }

    // ───── Уровень ─────

    [UnityTest]
    public IEnumerator LevelStats_AllCoinsCollected_TriggersVictory()
    {
        foreach (Coin leftover in Object.FindObjectsOfType<Coin>())
            Object.Destroy(leftover.gameObject);
        yield return null;

        var coins = new List<Coin>();
        for (int i = 0; i < 2; i++)
        {
            GameObject c = Track(new GameObject("Coin" + i));
            c.transform.position = new Vector3(i * 10f, 50f, 0f);
            c.AddComponent<SphereCollider>();
            coins.Add(c.AddComponent<Coin>());
        }

        var stats = Track(new GameObject("Stats")).AddComponent<LevelStats>();
        var wallet = CreatePlayer(new Vector3(0f, -5f, 30f), Vector3.zero).GetComponent<Wallet>();
        int victories = 0;
        stats.Victory += () => victories++;
        yield return null; // Start: подсчёт монет

        Assert.AreEqual(2, stats.TotalCoins);
        coins[0].TryCollect(wallet);
        Assert.IsFalse(stats.IsFinished);
        coins[1].TryCollect(wallet);

        Assert.IsTrue(stats.IsFinished);
        Assert.AreEqual(1, victories);
        Assert.AreEqual(2, stats.CoinsCollected);
    }

    [UnityTest]
    public IEnumerator Pause_StopsLevelTimer()
    {
        var stats = Track(new GameObject("Stats")).AddComponent<LevelStats>();
        yield return new WaitForSeconds(0.2f);
        float before = stats.ElapsedTime;
        Assert.Greater(before, 0f);

        GamePause.SetPaused(true);
        Assert.IsTrue(GamePause.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.AreEqual(before, stats.ElapsedTime, 1e-4f, "На паузе таймер уровня стоит");

        GamePause.SetPaused(false);
        Assert.AreEqual(1f, Time.timeScale);
    }

    // ───── Пул эффектов ─────

    [UnityTest]
    public IEnumerator EffectPool_AutoDestroy_ReturnsObjectToPool()
    {
        EffectPool.Clear();
        GameObject template = Track(new GameObject("FxTemplate"));
        template.SetActive(false);
        var auto = template.AddComponent<AutoDestroy>();
        SetPrivate(auto, "lifetime", 0.1f);

        GameObject fx = Track(EffectPool.Spawn(template, Vector3.zero));
        Assert.IsTrue(fx.activeSelf);

        yield return new WaitForSeconds(0.3f);

        Assert.IsTrue(fx != null, "Объект не уничтожается, а возвращается в пул");
        Assert.IsFalse(fx.activeSelf);
        Assert.AreEqual(1, EffectPool.FreeCount(template));

        GameObject again = EffectPool.Spawn(template, Vector3.up);
        Assert.AreSame(fx, again);
    }

    [UnityTest]
    public IEnumerator Coin_PickupEffect_UsesPool()
    {
        EffectPool.Clear();
        GameObject fxTemplate = Track(new GameObject("CoinFxTemplate"));
        fxTemplate.SetActive(false);
        SetPrivate(fxTemplate.AddComponent<AutoDestroy>(), "lifetime", 0.05f);

        var wallet = CreatePlayer(new Vector3(0f, 100f, 0f), Vector3.zero).GetComponent<Wallet>();
        int createdBefore = EffectPool.CreatedCount;

        for (int i = 0; i < 3; i++)
        {
            GameObject c = Track(new GameObject("Coin" + i));
            c.AddComponent<SphereCollider>();
            var coin = c.AddComponent<Coin>();
            SetPrivate(coin, "pickupEffectPrefab", fxTemplate);
            yield return null;
            coin.TryCollect(wallet);
            yield return new WaitForSeconds(0.15f);
        }

        Assert.AreEqual(createdBefore + 1, EffectPool.CreatedCount, "Три сбора монет используют один объект эффекта");
        Track(EffectPool.Spawn(fxTemplate, Vector3.zero)); // забираем объект из пула, чтобы удалить после теста
    }
}
