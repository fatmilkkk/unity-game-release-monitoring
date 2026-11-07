using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полоса здоровья игрока:
///  • основная полоса плавно меняет длину;
///  • за ней с задержкой «догоняет» светлая полоса потерянного HP (damage trail);
///  • при уроне полоса трясётся, при лечении «подпрыгивает»;
///  • при низком HP полоса краснеет и мигает.
/// </summary>
public class HealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private Text label;
    [SerializeField] private RectTransform fillBar;
    [SerializeField] private Image fillImage;
    [Tooltip("Полоса потерянного здоровья, догоняющая основную")]
    [SerializeField] private RectTransform trailBar;
    [Tooltip("Элемент, который трясётся при уроне")]
    [SerializeField] private UIPunch punchTarget;
    [SerializeField] private GameObject deathMessage;

    [SerializeField] private Color normalColor = new Color(0.3f, 0.85f, 0.35f);
    [SerializeField] private Color lowColor = new Color(0.95f, 0.2f, 0.2f);
    [SerializeField] private Color shieldColor = new Color(0.35f, 0.7f, 1f);
    [SerializeField] private float blinkSpeed = 6f;
    [SerializeField] private float fillSpeed = 8f;
    [SerializeField] private float trailDelay = 0.4f;
    [SerializeField] private float trailSpeed = 1.5f;

    private float _targetRatio = 1f;
    private float _displayRatio = 1f;
    private float _trailRatio = 1f;
    private float _trailHoldUntil;
    private int _lastHealth = -1;

    private void OnEnable()
    {
        if (health == null)
            return;
        health.HealthChanged += Refresh;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (health == null)
            return;
        health.HealthChanged -= Refresh;
        health.Died -= OnDied;
    }

    private void Start()
    {
        if (deathMessage != null)
            deathMessage.SetActive(false);

        if (health != null)
        {
            _targetRatio = _displayRatio = _trailRatio = health.MaxHealth > 0
                ? (float)health.CurrentHealth / health.MaxHealth
                : 0f;
            Refresh(health.CurrentHealth, health.MaxHealth);
        }
    }

    private void Refresh(int current, int max)
    {
        _targetRatio = max > 0 ? (float)current / max : 0f;

        if (label != null)
            label.text = $"HP: {current}/{max}";

        if (_lastHealth >= 0 && punchTarget != null)
        {
            if (current < _lastHealth)
            {
                punchTarget.Shake(12f, 0.35f);
                _trailHoldUntil = Time.unscaledTime + trailDelay;
            }
            else if (current > _lastHealth)
            {
                punchTarget.PunchScale(0.12f, 0.3f);
            }
        }

        if (current > _lastHealth)
            _trailRatio = Mathf.Max(_trailRatio, _targetRatio);

        _lastHealth = current;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        _displayRatio = Mathf.Lerp(_displayRatio, _targetRatio, 1f - Mathf.Exp(-fillSpeed * dt));
        SetBar(fillBar, _displayRatio);

        if (Time.unscaledTime >= _trailHoldUntil)
            _trailRatio = Mathf.MoveTowards(_trailRatio, _displayRatio, trailSpeed * dt);
        _trailRatio = Mathf.Max(_trailRatio, _displayRatio);
        SetBar(trailBar, _trailRatio);

        if (fillImage == null || health == null)
            return;

        if (health.IsShielded)
        {
            float k = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f;
            fillImage.color = Color.Lerp(shieldColor, Color.white, k * 0.3f);
        }
        else if (health.IsLowHealth && !health.IsDead)
        {
            float k = (Mathf.Sin(Time.unscaledTime * blinkSpeed) + 1f) * 0.5f;
            fillImage.color = Color.Lerp(lowColor, Color.white, k * 0.5f);
        }
        else
        {
            fillImage.color = normalColor;
        }
    }

    private static void SetBar(RectTransform bar, float ratio)
    {
        if (bar == null)
            return;
        bar.anchorMin = Vector2.zero;
        bar.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        bar.offsetMin = Vector2.zero;
        bar.offsetMax = Vector2.zero;
    }

    private void OnDied()
    {
        if (deathMessage != null)
            deathMessage.SetActive(true);
    }
}
