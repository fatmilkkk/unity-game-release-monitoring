using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Запрос согласия на сбор анонимной статистики при первом запуске.
/// До ответа игрока никакие события не записываются. Решение можно изменить в настройках.
/// </summary>
public class ConsentPanel : MonoBehaviour
{
    public const string PrivacyPolicyUrl =
        "https://github.com/fatmilkkk/unity-game-release-monitoring/blob/main/Docs/Release/PRIVACY_POLICY.md";

    [SerializeField] private UIPanel panel;
    [SerializeField] private Button allowButton;
    [SerializeField] private Button denyButton;
    [SerializeField] private Button policyButton;
    [SerializeField] private float showDelay = 0.8f;

    /// <summary>Игрок ответил (для показа следующих окон, например «Что нового»).</summary>
    public event System.Action Answered;

    public bool IsWaitingForAnswer => !AnalyticsService.HasAnsweredConsent;

    private void Awake()
    {
        if (allowButton != null) allowButton.onClick.AddListener(() => Answer(true));
        if (denyButton != null) denyButton.onClick.AddListener(() => Answer(false));
        if (policyButton != null) policyButton.onClick.AddListener(() => Application.OpenURL(PrivacyPolicyUrl));
    }

    private IEnumerator Start()
    {
        if (AnalyticsService.HasAnsweredConsent)
            yield break;
        yield return new WaitForSecondsRealtime(showDelay);
        if (panel != null)
            panel.Show();
    }

    private void Answer(bool allowed)
    {
        AnalyticsService.SetConsent(allowed);
        if (!allowed)
            AnalyticsService.DeleteLocalData();
        panel?.Hide();
        Answered?.Invoke();
    }
}
