using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полоса здоровья игрока. При низком HP полоса становится красной и мигает.
/// При гибели показывает сообщение.
/// </summary>
public class HealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private Text label;
    [SerializeField] private RectTransform fillBar;
    [SerializeField] private Image fillImage;
    [SerializeField] private GameObject deathMessage;

    [SerializeField] private Color normalColor = new Color(0.3f, 0.85f, 0.35f);
    [SerializeField] private Color lowColor = new Color(0.95f, 0.2f, 0.2f);
    [SerializeField] private float blinkSpeed = 6f;

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
            Refresh(health.CurrentHealth, health.MaxHealth);
    }

    private void Refresh(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;

        if (label != null)
            label.text = $"HP: {current}/{max}";

        if (fillBar != null)
        {
            fillBar.anchorMax = new Vector2(ratio, 1f);
            fillBar.offsetMin = Vector2.zero;
            fillBar.offsetMax = Vector2.zero;
        }
    }

    private void Update()
    {
        if (fillImage == null || health == null)
            return;

        if (health.IsLowHealth && !health.IsDead)
        {
            float k = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f;
            fillImage.color = Color.Lerp(lowColor, Color.white, k * 0.5f);
        }
        else
        {
            fillImage.color = normalColor;
        }
    }

    private void OnDied()
    {
        if (deathMessage != null)
            deathMessage.SetActive(true);
    }
}
