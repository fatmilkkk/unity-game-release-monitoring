using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно отзыва: оценка звёздами (1–5), категория (отзыв / ошибка / предложение), текст,
/// флажок «приложить техническую информацию». Отзыв сохраняется локально, учитывается в аналитике
/// и открывается в браузере как новое обращение GitHub Issues.
/// </summary>
public class FeedbackPanel : MonoBehaviour
{
    [SerializeField] private UIPanel panel;
    [SerializeField] private Button[] starButtons = new Button[5];
    [SerializeField] private Image[] starImages = new Image[5];
    [SerializeField] private Button categoryButton;
    [SerializeField] private Text categoryLabel;
    [SerializeField] private InputField textInput;
    [SerializeField] private Toggle techToggle;
    [SerializeField] private Toggle openGitHubToggle;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text statusText;
    [SerializeField] private UIPunch starsPunch;

    [SerializeField] private Color starOn = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color starOff = new Color(1f, 1f, 1f, 0.2f);

    private int _rating;
    private int _category;
    private Action _onClosed;

    public bool IsOpen => panel != null && panel.IsOpen;

    /// <summary>Отладка и тесты: последняя сформированная ссылка на GitHub.</summary>
    public string LastIssueUrl { get; private set; }

    private void Awake()
    {
        for (int i = 0; i < starButtons.Length; i++)
        {
            int value = i + 1;
            if (starButtons[i] != null)
                starButtons[i].onClick.AddListener(() => SetRating(value));
        }
        if (categoryButton != null)
            categoryButton.onClick.AddListener(() =>
            {
                _category = (_category + 1) % FeedbackService.Categories.Length;
                UpdateCategory();
            });
        if (sendButton != null) sendButton.onClick.AddListener(Send);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
    }

    public void Open(Action onClosed = null)
    {
        _onClosed = onClosed;
        _rating = 0;
        _category = 0;
        if (textInput != null) textInput.text = "";
        if (techToggle != null) techToggle.isOn = true;
        if (openGitHubToggle != null) openGitHubToggle.isOn = true;
        if (statusText != null) statusText.text = "";
        UpdateStars();
        UpdateCategory();
        panel?.Show();
    }

    public void Close()
    {
        panel?.Hide();
        Action callback = _onClosed;
        _onClosed = null;
        callback?.Invoke();
    }

    private void SetRating(int value)
    {
        _rating = value;
        UpdateStars();
        starsPunch?.PunchScale(0.1f, 0.2f);
    }

    private void UpdateStars()
    {
        for (int i = 0; i < starImages.Length; i++)
            if (starImages[i] != null)
                starImages[i].color = i < _rating ? starOn : starOff;
    }

    private void UpdateCategory()
    {
        if (categoryLabel != null)
            categoryLabel.text = "Тип: " + FeedbackService.CategoryNames[_category];
    }

    private void Send()
    {
        if (_rating == 0)
        {
            if (statusText != null) statusText.text = "Поставьте оценку от 1 до 5 звёзд";
            starsPunch?.Shake(10f, 0.3f);
            UISound.PlayError();
            return;
        }

        FeedbackEntry entry = FeedbackService.Create(_rating, _category,
            textInput != null ? textInput.text : "", techToggle == null || techToggle.isOn);
        FeedbackService.Save(entry);

        LastIssueUrl = FeedbackService.BuildIssueUrl(entry, RemoteConfig.Current.feedbackRepo);
        if (openGitHubToggle == null || openGitHubToggle.isOn)
            Application.OpenURL(LastIssueUrl);

        ToastUI.Show("Спасибо за отзыв!", new Color(1f, 0.82f, 0.25f));
        UISound.PlayItem();
        Close();
    }
}
