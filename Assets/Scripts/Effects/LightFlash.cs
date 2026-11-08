using UnityEngine;

/// <summary>Короткая вспышка света, плавно гаснущая. Перезапускается при каждом включении (совместимо с пулом).</summary>
[RequireComponent(typeof(Light))]
public class LightFlash : MonoBehaviour
{
    [SerializeField] private float duration = 0.25f;

    private Light _light;
    private float _startIntensity;
    private float _time;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _startIntensity = _light.intensity;
    }

    private void OnEnable()
    {
        _time = 0f;
        if (_light != null)
        {
            _light.intensity = _startIntensity;
            _light.enabled = true;
        }
    }

    private void Update()
    {
        _time += Time.deltaTime;
        float k = Mathf.Clamp01(_time / duration);
        _light.intensity = Mathf.Lerp(_startIntensity, 0f, k);
        if (k >= 1f)
            _light.enabled = false;
    }
}
