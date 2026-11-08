using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно настроек: громкость, полноэкранный режим, подсказки, качество графики, ограничение FPS,
/// автокачество, счётчик FPS, сенсорное управление, сброс кошелька.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeValueText;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Toggle hintsToggle;
    [SerializeField] private Button resetWalletButton;
    [SerializeField] private Text resetWalletLabel;

    [Header("Графика и производительность")]
    [SerializeField] private Button qualityButton;
    [SerializeField] private Text qualityLabel;
    [SerializeField] private Button fpsLimitButton;
    [SerializeField] private Text fpsLimitLabel;
    [SerializeField] private Toggle autoQualityToggle;
    [SerializeField] private Toggle showFpsToggle;
    [SerializeField] private Toggle touchToggle;

    private bool _updating;
    private bool _listenersAdded;
    private float _resetConfirmUntil;

    private void Awake()
    {
        if (_listenersAdded) return;
        _listenersAdded = true;

        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        if (hintsToggle != null) hintsToggle.onValueChanged.AddListener(OnHintsChanged);
        if (resetWalletButton != null) resetWalletButton.onClick.AddListener(OnResetWallet);

        if (qualityButton != null)
            qualityButton.onClick.AddListener(() =>
            {
                GameSettings.SetQuality((GameSettings.Quality + 1) % GameSettings.QualityNames.Length);
                UpdateCycleLabels();
            });
        if (fpsLimitButton != null)
            fpsLimitButton.onClick.AddListener(() =>
            {
                GameSettings.SetFpsLimit((GameSettings.FpsLimitIndex + 1) % GameSettings.FpsLimits.Length);
                UpdateCycleLabels();
            });
        if (autoQualityToggle != null) autoQualityToggle.onValueChanged.AddListener(v => { if (!_updating) GameSettings.SetAutoQuality(v); });
        if (showFpsToggle != null) showFpsToggle.onValueChanged.AddListener(v => { if (!_updating) GameSettings.SetShowFps(v); });
        if (touchToggle != null) touchToggle.onValueChanged.AddListener(v => { if (!_updating) GameSettings.SetTouchControls(v); });
    }

    private void UpdateCycleLabels()
    {
        if (qualityLabel != null)
            qualityLabel.text = $"Качество: {GameSettings.QualityNames[GameSettings.Quality]}";
        if (fpsLimitLabel != null)
            fpsLimitLabel.text = $"Лимит FPS: {GameSettings.FpsLimitNames[GameSettings.FpsLimitIndex]}";
    }

    private void OnEnable()
    {
        GameSettings.EnsureLoaded();
        _updating = true;
        if (volumeSlider != null) volumeSlider.value = GameSettings.Volume;
        if (fullscreenToggle != null) fullscreenToggle.isOn = GameSettings.Fullscreen;
        if (hintsToggle != null) hintsToggle.isOn = GameSettings.ShowHints;
        if (autoQualityToggle != null) autoQualityToggle.isOn = GameSettings.AutoQuality;
        if (showFpsToggle != null) showFpsToggle.isOn = GameSettings.ShowFps;
        if (touchToggle != null) touchToggle.isOn = GameSettings.TouchControls;
        _updating = false;
        UpdateCycleLabels();
        UpdateVolumeLabel();
        _resetConfirmUntil = 0f;
        if (resetWalletLabel != null) resetWalletLabel.text = "Обнулить кошелёк";
    }

    private void Update()
    {
        if (_resetConfirmUntil > 0f && Time.unscaledTime > _resetConfirmUntil)
        {
            _resetConfirmUntil = 0f;
            if (resetWalletLabel != null) resetWalletLabel.text = "Обнулить кошелёк";
        }
    }

    private void OnVolumeChanged(float value)
    {
        if (_updating) return;
        GameSettings.SetVolume(value);
        UpdateVolumeLabel();
    }

    private void UpdateVolumeLabel()
    {
        if (volumeValueText != null)
            volumeValueText.text = $"{Mathf.RoundToInt(GameSettings.Volume * 100f)}%";
    }

    private void OnFullscreenChanged(bool value)
    {
        if (_updating) return;
        GameSettings.SetFullscreen(value);
    }

    private void OnHintsChanged(bool value)
    {
        if (_updating) return;
        GameSettings.SetShowHints(value);
    }

    /// <summary>Двухшаговое подтверждение: первый клик спрашивает, второй — обнуляет.</summary>
    private void OnResetWallet()
    {
        if (_resetConfirmUntil <= 0f)
        {
            _resetConfirmUntil = Time.unscaledTime + 3f;
            if (resetWalletLabel != null) resetWalletLabel.text = "Точно? Нажмите ещё раз";
            UISound.PlayError();
            return;
        }

        _resetConfirmUntil = 0f;
        var wallet = FindObjectOfType<Wallet>();
        if (wallet != null) wallet.ResetWallet();
        else Wallet.ClearSaved();

        if (resetWalletLabel != null) resetWalletLabel.text = "Кошелёк обнулён";
        ToastUI.Show("Кошелёк обнулён", new Color(1f, 0.8f, 0.2f));
        MainMenuController.RefreshWalletLabel();
    }
}
