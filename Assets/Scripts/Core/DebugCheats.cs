using UnityEngine;

/// <summary>
/// Отладочные клавиши для ручного тестирования (только в редакторе и Development Build):
///  F1 — счётчик FPS (обрабатывается FpsCounterUI)
///  F2 — выдать по одному предмету каждого типа
///  F3 — полностью восстановить здоровье
///  F4 — +10 монет в кошелёк
///  F5 — уничтожить игрока (проверка экрана поражения)
///  F6 — собрать все монеты уровня (проверка экрана победы)
///  F7 — бессмертие (щит на 9999 с)
///  F8 — замедление времени ×0.25 (проверка физики и анимаций)
/// </summary>
public class DebugCheats : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private Inventory inventory;
    [SerializeField] private Wallet wallet;

    private bool _slowMotion;

    public static bool Enabled => Application.isEditor || Debug.isDebugBuild;

    private void Awake()
    {
        if (!Enabled)
            enabled = false;
    }

    private void Update()
    {
        if (GamePause.IsPaused)
            return;

        if (Input.GetKeyDown(KeyCode.F2) && inventory != null)
        {
            inventory.Add(ItemType.Medkit);
            inventory.Add(ItemType.Shield);
            inventory.Add(ItemType.SpeedBoost);
            Log("выданы предметы");
        }
        else if (Input.GetKeyDown(KeyCode.F3) && health != null)
        {
            health.Heal(health.MaxHealth);
            Log("здоровье восстановлено");
        }
        else if (Input.GetKeyDown(KeyCode.F4) && wallet != null)
        {
            wallet.AddCoins(10);
            Log("+10 монет");
        }
        else if (Input.GetKeyDown(KeyCode.F5) && health != null)
        {
            health.Kill(true);
            Log("игрок уничтожен");
        }
        else if (Input.GetKeyDown(KeyCode.F6) && wallet != null)
        {
            foreach (Coin coin in FindObjectsOfType<Coin>())
                coin.TryCollect(wallet);
            Log("собраны все монеты");
        }
        else if (Input.GetKeyDown(KeyCode.F7) && health != null)
        {
            health.GrantShield(9999f);
            Log("бессмертие");
        }
        else if (Input.GetKeyDown(KeyCode.F8))
        {
            _slowMotion = !_slowMotion;
            Time.timeScale = _slowMotion ? 0.25f : 1f;
            Log(_slowMotion ? "замедление ×0.25" : "нормальная скорость");
        }
    }

    private static void Log(string message)
    {
        Debug.Log("[Debug] " + message);
        ToastUI.Show("Отладка: " + message, new Color(0.8f, 0.5f, 1f));
    }
}
