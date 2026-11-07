using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Экран/окно интерфейса с анимацией появления и скрытия.
/// Анимации идут в нескалированном времени, поэтому работают и на паузе (Time.timeScale = 0).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    public enum AnimationType
    {
        FadeScale,
        SlideFromTop,
        SlideFromBottom,
        SlideFromRight,
        SlideFromLeft
    }

    [SerializeField] private AnimationType animationType = AnimationType.FadeScale;
    [SerializeField] private float duration = 0.25f;
    [Tooltip("Что анимировать (окно внутри затемнения). Если пусто — сам объект")]
    [SerializeField] private RectTransform content;
    [SerializeField] private float slideDistance = 500f;
    [SerializeField] private bool startHidden = true;
    [Tooltip("Звук при открытии")]
    [SerializeField] private bool playSound = true;

    private CanvasGroup _group;
    private Coroutine _routine;
    private Vector2 _shownPosition;
    private float _progress;
    private bool _initialized;

    public bool IsOpen { get; private set; }

    public event Action Opened;
    public event Action Closed;

    private void Awake()
    {
        Init();
        if (startHidden && !IsOpen)
        {
            ApplyProgress(0f);
            _group.interactable = false;
            _group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
        else if (!startHidden)
        {
            IsOpen = true;
            ApplyProgress(1f);
        }
    }

    private void Init()
    {
        if (_initialized)
            return;

        _group = GetComponent<CanvasGroup>();
        if (content == null)
            content = (RectTransform)transform;
        _shownPosition = content.anchoredPosition;
        _initialized = true;
    }

    public void Show()
    {
        Init();
        if (IsOpen && gameObject.activeSelf)
            return;

        IsOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (playSound)
            UISound.PlayOpen();
        StartAnimation(true);
        Opened?.Invoke();
    }

    public void Hide()
    {
        Init();
        if (!IsOpen)
            return;

        IsOpen = false;
        if (!gameObject.activeInHierarchy)
        {
            ApplyProgress(0f);
            gameObject.SetActive(false);
        }
        else
        {
            StartAnimation(false);
        }
        Closed?.Invoke();
    }

    public void Toggle()
    {
        if (IsOpen) Hide();
        else Show();
    }

    private void StartAnimation(bool show)
    {
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(Animate(show));
    }

    private IEnumerator Animate(bool show)
    {
        float from = _progress;
        float to = show ? 1f : 0f;
        float time = 0f;
        float length = Mathf.Max(0.01f, duration * Mathf.Abs(to - from));

        _group.interactable = false;
        _group.blocksRaycasts = show;

        while (time < length)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / length);
            float eased = show ? UIEasing.OutBack(k) : UIEasing.InCubic(k);
            ApplyProgress(Mathf.LerpUnclamped(from, to, eased));
            yield return null;
        }

        ApplyProgress(to);
        _group.interactable = show;
        _group.blocksRaycasts = show;
        _routine = null;

        if (!show)
            gameObject.SetActive(false);
    }

    private void ApplyProgress(float p)
    {
        _progress = p;
        _group.alpha = Mathf.Clamp01(p);

        switch (animationType)
        {
            case AnimationType.FadeScale:
                content.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, p);
                break;
            case AnimationType.SlideFromTop:
                content.anchoredPosition = _shownPosition + Vector2.up * (slideDistance * (1f - p));
                break;
            case AnimationType.SlideFromBottom:
                content.anchoredPosition = _shownPosition + Vector2.down * (slideDistance * (1f - p));
                break;
            case AnimationType.SlideFromRight:
                content.anchoredPosition = _shownPosition + Vector2.right * (slideDistance * (1f - p));
                break;
            case AnimationType.SlideFromLeft:
                content.anchoredPosition = _shownPosition + Vector2.left * (slideDistance * (1f - p));
                break;
        }
    }
}
