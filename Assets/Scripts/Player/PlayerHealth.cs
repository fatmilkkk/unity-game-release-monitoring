using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Здоровье (HP) игрока. Получает урон от подвижных препятствий,
/// мгновенно уничтожается при сильном ударе о бочку со взрывчаткой.
/// После гибели уровень перезапускается.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье")]
    [SerializeField] private int maxHealth = 100;
    [Tooltip("Доля HP, ниже которой здоровье считается низким (для интерфейса)")]
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;
    [Tooltip("Время неуязвимости после получения урона, с")]
    [SerializeField] private float invulnerabilityTime = 0.6f;
    [Tooltip("Если игрок упал ниже этой высоты — он погибает")]
    [SerializeField] private float killHeight = -10f;

    [Header("Гибель")]
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private float restartDelay = 2.5f;

    [Header("Визуальный отклик")]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.15f, 0.15f);
    [SerializeField] private float flashDuration = 0.15f;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsLowHealth => CurrentHealth <= Mathf.CeilToInt(maxHealth * lowHealthThreshold);

    /// <summary>(текущее HP, максимальное HP)</summary>
    public event Action<int, int> HealthChanged;
    public event Action Died;

    private float _lastDamageTime = -999f;
    private Material _material;
    private Color _baseColor;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        CurrentHealth = maxHealth;

        if (bodyRenderer == null)
            bodyRenderer = GetComponent<Renderer>();

        if (bodyRenderer != null)
        {
            _material = bodyRenderer.material;
            if (_material.HasProperty("_Color"))
                _baseColor = _material.color;
        }
    }

    private void Start()
    {
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    private void Update()
    {
        if (!IsDead && transform.position.y < killHeight)
            Kill();
    }

    /// <summary>Нанести урон. Возвращает true, если урон был применён.</summary>
    public bool TakeDamage(int amount)
    {
        if (IsDead || amount <= 0)
            return false;

        if (Time.time - _lastDamageTime < invulnerabilityTime)
            return false;

        _lastDamageTime = Time.time;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth == 0)
            Die();
        else
            Flash();

        return true;
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
            return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>Мгновенное уничтожение игрока (например, взрыв бочки).</summary>
    public void Kill()
    {
        if (IsDead)
            return;

        CurrentHealth = 0;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        Die();
    }

    private void Die()
    {
        IsDead = true;

        if (deathEffectPrefab != null)
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);

        var controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;
        foreach (var rend in GetComponentsInChildren<Renderer>())
            rend.enabled = false;

        Died?.Invoke();
        StartCoroutine(RestartAfterDelay());
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(restartDelay);
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }

    private void Flash()
    {
        if (_material == null || !_material.HasProperty("_Color"))
            return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        _material.color = damageFlashColor;
        yield return new WaitForSeconds(flashDuration);
        _material.color = _baseColor;
        _flashRoutine = null;
    }
}
