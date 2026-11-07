using System.Collections;
using UnityEngine;

/// <summary>Короткая «пружинящая» анимация масштаба или тряска элемента (для отклика на события).</summary>
public class UIPunch : MonoBehaviour
{
    private Vector3 _baseScale = Vector3.one;
    private Vector2 _basePosition;
    private Coroutine _scaleRoutine;
    private Coroutine _shakeRoutine;
    private bool _initialized;

    private void Init()
    {
        if (_initialized) return;
        _baseScale = transform.localScale;
        if (transform is RectTransform rt) _basePosition = rt.anchoredPosition;
        _initialized = true;
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        transform.localScale = _baseScale;
        if (transform is RectTransform rt) rt.anchoredPosition = _basePosition;
        _scaleRoutine = null;
        _shakeRoutine = null;
    }

    public void PunchScale(float strength = 0.25f, float duration = 0.35f)
    {
        Init();
        if (!isActiveAndEnabled) return;
        if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
        _scaleRoutine = StartCoroutine(ScaleRoutine(strength, duration));
    }

    public void Shake(float strength = 10f, float duration = 0.3f)
    {
        Init();
        if (!isActiveAndEnabled || !(transform is RectTransform)) return;
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(strength, duration));
    }

    private IEnumerator ScaleRoutine(float strength, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = 1f + strength * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
            transform.localScale = _baseScale * s;
            yield return null;
        }
        transform.localScale = _baseScale;
        _scaleRoutine = null;
    }

    private IEnumerator ShakeRoutine(float strength, float duration)
    {
        var rt = (RectTransform)transform;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float damper = 1f - Mathf.Clamp01(t / duration);
            rt.anchoredPosition = _basePosition + Random.insideUnitCircle * (strength * damper);
            yield return null;
        }
        rt.anchoredPosition = _basePosition;
        _shakeRoutine = null;
    }
}
