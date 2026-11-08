using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Управляет экранами интерфейса во время игры:
/// пауза (Esc), инвентарь (I / Tab), управление, настройки, поражение и победа.
/// Также показывает уведомления о предметах, бонусах и низком здоровье.
/// </summary>
public class GameUIController : MonoBehaviour
{
    public const string MenuSceneName = "MainMenu";

    [Header("Ссылки на игру")]
    [SerializeField] private PlayerHealth health;
    [SerializeField] private Inventory inventory;
    [SerializeField] private LevelStats stats;

    [Header("Экраны")]
    [SerializeField] private UIPanel pausePanel;
    [SerializeField] private UIPanel inventoryPanel;
    [SerializeField] private UIPanel controlsPanel;
    [SerializeField] private UIPanel settingsPanel;
    [SerializeField] private UIPanel gameOverPanel;
    [SerializeField] private UIPanel victoryPanel;
    [SerializeField] private GameObject hintsRoot;

    [Header("Кнопки HUD")]
    [SerializeField] private Button hudPauseButton;
    [SerializeField] private Button hudInventoryButton;

    [Header("Кнопки паузы")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseInventoryButton;
    [SerializeField] private Button pauseControlsButton;
    [SerializeField] private Button pauseSettingsButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseMenuButton;

    [Header("Прочие кнопки")]
    [SerializeField] private Button inventoryCloseButton;
    [SerializeField] private Button controlsBackButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button gameOverRestartButton;
    [SerializeField] private Button gameOverMenuButton;
    [SerializeField] private Button victoryRestartButton;
    [SerializeField] private Button victoryMenuButton;

    [Header("Экран поражения")]
    [SerializeField] private Text gameOverStatsText;

    [Header("Экран победы")]
    [SerializeField] private Text victoryStatsText;
    [SerializeField] private Image[] victoryStars;
    [SerializeField] private Color starOnColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color starOffColor = new Color(1f, 1f, 1f, 0.15f);

    private bool _gameEnded;
    private bool _lowHealthWarned;

    private void Awake()
    {
        GamePause.Reset();

        Bind(hudPauseButton, OpenPause);
        Bind(hudInventoryButton, OpenInventory);

        Bind(resumeButton, ResumeGame);
        Bind(pauseInventoryButton, () => { pausePanel.Hide(); inventoryPanel.Show(); });
        Bind(pauseControlsButton, () => OpenSubmenu(controlsPanel));
        Bind(pauseSettingsButton, () => OpenSubmenu(settingsPanel));
        Bind(pauseRestartButton, RestartLevel);
        Bind(pauseMenuButton, GoToMainMenu);

        Bind(inventoryCloseButton, ResumeGame);
        Bind(controlsBackButton, BackToPause);
        Bind(settingsBackButton, BackToPause);
        Bind(gameOverRestartButton, RestartLevel);
        Bind(gameOverMenuButton, GoToMainMenu);
        Bind(victoryRestartButton, RestartLevel);
        Bind(victoryMenuButton, GoToMainMenu);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.AddListener(action);
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += OnPlayerDied;
            health.HealthChanged += OnHealthChanged;
        }
        if (inventory != null)
        {
            inventory.ItemAdded += OnItemAdded;
            inventory.ItemUsed += OnItemUsed;
            inventory.UseRejected += OnItemRejected;
        }
        if (stats != null)
            stats.Victory += OnVictory;

        GameSettings.Changed += ApplySettings;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= OnPlayerDied;
            health.HealthChanged -= OnHealthChanged;
        }
        if (inventory != null)
        {
            inventory.ItemAdded -= OnItemAdded;
            inventory.ItemUsed -= OnItemUsed;
            inventory.UseRejected -= OnItemRejected;
        }
        if (stats != null)
            stats.Victory -= OnVictory;

        GameSettings.Changed -= ApplySettings;
    }

    private void Start()
    {
        ApplySettings();
        ToastUI.Show("Соберите все монеты уровня!", new Color(1f, 0.8f, 0.2f));
    }

    private void Update()
    {
        if (_gameEnded)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsOpen(controlsPanel) || IsOpen(settingsPanel)) BackToPause();
            else if (IsOpen(inventoryPanel)) ResumeGame();
            else if (IsOpen(pausePanel)) ResumeGame();
            else OpenPause();
        }
        else if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            if (IsOpen(inventoryPanel)) ResumeGame();
            else if (!IsOpen(pausePanel) && !IsOpen(controlsPanel) && !IsOpen(settingsPanel)) OpenInventory();
        }
    }

    private static bool IsOpen(UIPanel panel) => panel != null && panel.IsOpen;

    // ───── Экраны ─────

    public void OpenPause()
    {
        if (_gameEnded) return;
        GamePause.SetPaused(true);
        inventoryPanel?.Hide();
        pausePanel?.Show();
    }

    public void OpenInventory()
    {
        if (_gameEnded) return;
        GamePause.SetPaused(true);
        pausePanel?.Hide();
        inventoryPanel?.Show();
    }

    public void ResumeGame()
    {
        pausePanel?.Hide();
        inventoryPanel?.Hide();
        controlsPanel?.Hide();
        settingsPanel?.Hide();
        GamePause.SetPaused(false);
    }

    private void OpenSubmenu(UIPanel panel)
    {
        pausePanel?.Hide();
        panel?.Show();
    }

    private void BackToPause()
    {
        controlsPanel?.Hide();
        settingsPanel?.Hide();
        pausePanel?.Show();
    }

    public void RestartLevel()
    {
        GamePause.Reset();
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }

    public void GoToMainMenu()
    {
        GamePause.Reset();
        SceneManager.LoadScene(MenuSceneName);
    }

    private void ApplySettings()
    {
        if (hintsRoot != null)
            hintsRoot.SetActive(GameSettings.ShowHints && !GameSettings.TouchControls);
    }

    // ───── События игры ─────

    private void OnPlayerDied()
    {
        if (_gameEnded) return;
        _gameEnded = true;
        StartCoroutine(ShowGameOver());
    }

    private IEnumerator ShowGameOver()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        ResumeGame();
        if (gameOverStatsText != null && stats != null)
            gameOverStatsText.text =
                $"Время: {LevelStats.FormatTime(stats.ElapsedTime)}\n" +
                $"Монеты уровня: {stats.CoinsCollected}/{stats.TotalCoins}\n" +
                $"Бонусы: {stats.BonusesFound}/{stats.TotalBonuses}";
        UISound.PlayError();
        gameOverPanel?.Show();
    }

    private void OnVictory()
    {
        if (_gameEnded) return;
        _gameEnded = true;
        StartCoroutine(ShowVictory());
    }

    private IEnumerator ShowVictory()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        ResumeGame();
        GamePause.SetPaused(true);

        int stars = stats != null ? stats.Stars : 1;
        if (victoryStatsText != null && stats != null)
            victoryStatsText.text =
                $"Время: {LevelStats.FormatTime(stats.ElapsedTime)}\n" +
                $"Монеты уровня: {stats.CoinsCollected}/{stats.TotalCoins}\n" +
                $"Бонусы: {stats.BonusesFound}/{stats.TotalBonuses}";

        if (victoryStars != null)
            foreach (Image star in victoryStars)
                if (star != null)
                {
                    star.color = starOffColor;
                    star.transform.localScale = Vector3.one;
                }

        victoryPanel?.Show();
        yield return new WaitForSecondsRealtime(0.4f);

        // Звёзды загораются по очереди с «пружинкой»
        for (int i = 0; victoryStars != null && i < victoryStars.Length && i < stars; i++)
        {
            Image star = victoryStars[i];
            if (star == null) continue;
            star.color = starOnColor;
            UISound.PlayCoin();
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.35f);
                star.transform.localScale = Vector3.one * Mathf.LerpUnclamped(1.8f, 1f, UIEasing.OutBack(k));
                yield return null;
            }
            star.transform.localScale = Vector3.one;
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        if (health == null || health.IsDead) return;

        if (health.IsLowHealth && !_lowHealthWarned)
        {
            _lowHealthWarned = true;
            ToastUI.Show("Мало здоровья! Используйте аптечку (1)", new Color(1f, 0.3f, 0.3f));
        }
        else if (!health.IsLowHealth)
        {
            _lowHealthWarned = false;
        }
    }

    private void OnItemAdded(ItemType type)
    {
        UISound.PlayItem();
        ToastUI.Show($"Получен предмет: {ItemDatabase.GetName(type)}", ItemDatabase.GetColor(type));
    }

    private void OnItemUsed(ItemType type)
    {
        ToastUI.Show($"Использовано: {ItemDatabase.GetName(type)}", ItemDatabase.GetColor(type));
    }

    private void OnItemRejected(ItemType type, string reason)
    {
        UISound.PlayError();
        ToastUI.Show($"{ItemDatabase.GetName(type)}: {reason}", new Color(0.7f, 0.7f, 0.7f));
    }
}
