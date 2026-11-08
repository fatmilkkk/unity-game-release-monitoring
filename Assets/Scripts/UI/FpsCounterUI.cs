using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

/// <summary>
/// Счётчик производительности для тестирования: средний FPS, среднее и максимальное время кадра,
/// занятая память, разрешение экрана и уровень качества. Обновляется 2 раза в секунду.
/// Включается в настройках («Показывать FPS») или клавишей F1.
/// </summary>
public class FpsCounterUI : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private GameObject root;
    [SerializeField] private float refreshInterval = 0.5f;

    private float _time;
    private int _frames;
    private float _worstFrame;
    private bool _forced;

    private void OnEnable()
    {
        GameSettings.Changed += ApplyVisibility;
        ApplyVisibility();
    }

    private void OnDisable() => GameSettings.Changed -= ApplyVisibility;

    private void ApplyVisibility()
    {
        if (root != null)
            root.SetActive(GameSettings.ShowFps || _forced);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            _forced = !_forced;
            ApplyVisibility();
        }

        float dt = Time.unscaledDeltaTime;
        _time += dt;
        _frames++;
        if (dt > _worstFrame)
            _worstFrame = dt;

        if (_time < refreshInterval)
            return;

        if (label != null && root != null && root.activeSelf)
        {
            float fps = _frames / _time;
            float avgMs = _time / _frames * 1000f;
            long memoryMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            string color = fps >= 55f ? "#7CFF7C" : fps >= 30f ? "#FFD25A" : "#FF6060";
            label.text =
                $"<color={color}><b>{fps:0} FPS</b></color>  {avgMs:0.0} мс (макс {_worstFrame * 1000f:0.0})\n" +
                $"Память: {memoryMb} МБ   {Screen.width}×{Screen.height}   " +
                $"Качество: {GameSettings.QualityNames[GameSettings.Quality]}\n" +
                $"Пул эффектов: создано {EffectPool.CreatedCount}, повторно {EffectPool.ReusedCount}";
        }

        _time = 0f;
        _frames = 0;
        _worstFrame = 0f;
    }
}
