using UnityEngine;

/// <summary>Непрерывная «живая» анимация элемента: покачивание, пульсация и лёгкий наклон (заголовки, иконки).</summary>
public class UIFloatAnimation : MonoBehaviour
{
    [SerializeField] private float bobAmplitude = 8f;
    [SerializeField] private float bobSpeed = 1.5f;
    [SerializeField] private float pulseAmplitude = 0.03f;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float tiltAmplitude = 2f;
    [SerializeField] private float tiltSpeed = 1f;

    private RectTransform _rect;
    private Vector2 _basePosition;
    private Vector3 _baseScale;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _basePosition = _rect.anchoredPosition;
        _baseScale = _rect.localScale;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        _rect.anchoredPosition = _basePosition + Vector2.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude);
        _rect.localScale = _baseScale * (1f + Mathf.Sin(t * pulseSpeed) * pulseAmplitude);
        _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * tiltSpeed) * tiltAmplitude);
    }
}
