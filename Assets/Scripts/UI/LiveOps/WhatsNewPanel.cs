using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно «Что нового»: список изменений текущей версии (Resources/changelog.txt).
/// Показывается автоматически один раз после обновления игры и по кнопке в главном меню.
/// </summary>
public class WhatsNewPanel : MonoBehaviour
{
    private const string LastSeenKey = "WhatsNew.LastSeenVersion";

    [SerializeField] private UIPanel panel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Button closeButton;

    public static bool HasUnseenUpdate => PlayerPrefs.GetString(LastSeenKey, "") != BuildInfo.Current.version;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        var changelog = Resources.Load<TextAsset>("changelog");
        if (titleText != null)
            titleText.text = $"Что нового в {BuildInfo.Current.ShortLabel}";
        if (bodyText != null)
            bodyText.text = changelog != null ? changelog.text : "Список изменений недоступен.";
        panel?.Show();
    }

    public void Close()
    {
        PlayerPrefs.SetString(LastSeenKey, BuildInfo.Current.version);
        PlayerPrefs.Save();
        panel?.Hide();
    }

    public bool IsOpen => panel != null && panel.IsOpen;
}
