using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка новостей в главном меню: заголовок и текст из удалённого конфига,
/// а также уведомление «Доступна новая версия» с кнопкой обновления, если latestVersion новее текущей.
/// </summary>
public class NewsBanner : MonoBehaviour
{
    [SerializeField] private GameObject newsRoot;
    [SerializeField] private Text newsTitle;
    [SerializeField] private Text newsText;
    [SerializeField] private GameObject updateRoot;
    [SerializeField] private Text updateText;
    [SerializeField] private Button updateButton;

    private void Awake()
    {
        if (updateButton != null)
            updateButton.onClick.AddListener(() =>
            {
                string url = RemoteConfig.Current.updateUrl;
                if (!string.IsNullOrEmpty(url))
                    Application.OpenURL(url);
                AnalyticsService.Track("update_clicked", "to", RemoteConfig.Current.latestVersion);
            });
    }

    private void OnEnable()
    {
        RemoteConfig.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => RemoteConfig.Changed -= Refresh;

    private void Refresh()
    {
        RemoteConfigData config = RemoteConfig.Current;

        bool hasNews = !string.IsNullOrEmpty(config.newsTitle) || !string.IsNullOrEmpty(config.newsText);
        if (newsRoot != null) newsRoot.SetActive(hasNews);
        if (newsTitle != null) newsTitle.text = config.newsTitle;
        if (newsText != null) newsText.text = config.newsText;

        bool hasUpdate = !string.IsNullOrEmpty(config.latestVersion) &&
                         VersionUtil.IsNewer(config.latestVersion, BuildInfo.Current.version);
        if (updateRoot != null) updateRoot.SetActive(hasUpdate);
        if (updateText != null && hasUpdate)
            updateText.text = $"Доступна версия {config.latestVersion} (у вас {BuildInfo.Current.version})";
    }
}
