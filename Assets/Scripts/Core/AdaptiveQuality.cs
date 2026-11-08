using UnityEngine;

/// <summary>
/// Адаптивное качество: если средний FPS долго держится заметно ниже целевого,
/// качество графики автоматически понижается на одну ступень (с уведомлением).
/// Помогает игре оставаться плавной на слабых устройствах. Отключается в настройках.
/// </summary>
public class AdaptiveQuality : MonoBehaviour
{
    [Tooltip("Сколько секунд после загрузки не измерять (загрузка, прогрев)")]
    [SerializeField] private float warmup = 3f;
    [Tooltip("Окно усреднения FPS, с")]
    [SerializeField] private float window = 4f;
    [Tooltip("Понижать, если средний FPS ниже этой доли от целевого")]
    [SerializeField, Range(0.3f, 1f)] private float threshold = 0.75f;
    [SerializeField] private float cooldown = 10f;

    private float _elapsed;
    private float _windowTime;
    private int _windowFrames;
    private float _nextAllowed;

    public float LastAverageFps { get; private set; }

    private void Update()
    {
        if (GamePause.IsPaused)
            return;

        float dt = Time.unscaledDeltaTime;
        _elapsed += dt;
        if (_elapsed < warmup)
            return;

        _windowTime += dt;
        _windowFrames++;
        if (_windowTime < window)
            return;

        LastAverageFps = _windowFrames / _windowTime;
        _windowTime = 0f;
        _windowFrames = 0;

        if (!GameSettings.AutoQuality || Time.unscaledTime < _nextAllowed || GameSettings.Quality <= 0)
            return;

        int target = GameSettings.TargetFrameRate > 0 ? GameSettings.TargetFrameRate : 60;
        if (LastAverageFps < target * threshold)
        {
            GameSettings.SetQuality(GameSettings.Quality - 1);
            _nextAllowed = Time.unscaledTime + cooldown;
            ToastUI.Show($"Качество снижено до «{GameSettings.QualityNames[GameSettings.Quality]}» (FPS {LastAverageFps:0})",
                new Color(0.7f, 0.8f, 1f));
        }
    }
}
