using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Главное меню: «Играть», «Управление», «Настройки», «Выход» и баланс кошелька.</summary>
public class MainMenuController : MonoBehaviour
{
    public const string GameSceneName = "Main";

    [SerializeField] private UIPanel mainPanel;
    [SerializeField] private UIPanel controlsPanel;
    [SerializeField] private UIPanel settingsPanel;
    [SerializeField] private CanvasGroup fadeOverlay;

    [SerializeField] private Button playButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button controlsBackButton;
    [SerializeField] private Button settingsBackButton;

    [SerializeField] private Text walletText;

    [Header("Выпуск и обновления")]
    [SerializeField] private Button whatsNewButton;
    [SerializeField] private WhatsNewPanel whatsNewPanel;
    [SerializeField] private ConsentPanel consentPanel;

    private static MainMenuController _instance;
    private bool _loading;

    private void Awake()
    {
        _instance = this;
        GamePause.Reset();

        if (playButton != null) playButton.onClick.AddListener(Play);
        if (controlsButton != null) controlsButton.onClick.AddListener(() => Open(controlsPanel));
        if (settingsButton != null) settingsButton.onClick.AddListener(() => Open(settingsPanel));
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (controlsBackButton != null) controlsBackButton.onClick.AddListener(Back);
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(Back);
        if (whatsNewButton != null && whatsNewPanel != null) whatsNewButton.onClick.AddListener(whatsNewPanel.Open);
        if (consentPanel != null) consentPanel.Answered += ShowWhatsNewIfUpdated;
    }

    /// <summary>После обновления игры один раз показываем «Что нового» (но не поверх запроса согласия).</summary>
    private void ShowWhatsNewIfUpdated()
    {
        if (whatsNewPanel != null && WhatsNewPanel.HasUnseenUpdate && !whatsNewPanel.IsOpen)
            whatsNewPanel.Open();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Start()
    {
        RefreshWalletLabel();
        if (mainPanel != null)
            mainPanel.Show();
        if (fadeOverlay != null)
            StartCoroutine(Fade(1f, 0f, 0.5f));
        if (consentPanel == null || !consentPanel.IsWaitingForAnswer)
            Invoke(nameof(ShowWhatsNewIfUpdated), 0.8f);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (whatsNewPanel != null && whatsNewPanel.IsOpen)
                whatsNewPanel.Close();
            else if ((controlsPanel != null && controlsPanel.IsOpen) || (settingsPanel != null && settingsPanel.IsOpen))
                Back();
        }
    }

    public static void RefreshWalletLabel()
    {
        if (_instance != null && _instance.walletText != null)
            _instance.walletText.text = $"В кошельке: {Wallet.GetSavedCoins()} мон.";
    }

    private void Open(UIPanel panel)
    {
        mainPanel?.Hide();
        panel?.Show();
    }

    private void Back()
    {
        controlsPanel?.Hide();
        settingsPanel?.Hide();
        mainPanel?.Show();
        RefreshWalletLabel();
    }

    private void Play()
    {
        if (_loading) return;
        _loading = true;
        StartCoroutine(LoadGame());
    }

    private System.Collections.IEnumerator LoadGame()
    {
        mainPanel?.Hide();
        if (fadeOverlay != null)
            yield return Fade(0f, 1f, 0.4f);
        SceneManager.LoadScene(GameSceneName);
    }

    private System.Collections.IEnumerator Fade(float from, float to, float duration)
    {
        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.transform.SetAsLastSibling();
        fadeOverlay.blocksRaycasts = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeOverlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        fadeOverlay.alpha = to;
        fadeOverlay.blocksRaycasts = to > 0.5f;
        if (to <= 0f)
            fadeOverlay.gameObject.SetActive(false);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
