using System;
using UnityEngine;

/// <summary>
/// Монетка: вращается и покачивается на месте. Когда игрок касается её триггера,
/// монета добавляется в кошелёк игрока, проигрывается эффект и объект уничтожается.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Coin : MonoBehaviour
{
    /// <summary>Монета собрана (монета, мировая позиция). Используется счётчиками и анимацией интерфейса.</summary>
    public static event Action<Coin, Vector3> Collected;

    [SerializeField] private int value = 1;

    [Header("Анимация на месте")]
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobFrequency = 2f;

    [Header("Эффект сбора")]
    [SerializeField] private GameObject pickupEffectPrefab;

    private Vector3 _startPosition;
    private Renderer _renderer;
    private float _bobOffset;
    private bool _collected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Collected = null; // для режима без перезагрузки домена

    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        _renderer = GetComponentInChildren<Renderer>();
        _startPosition = transform.position;
        _bobOffset = UnityEngine.Random.value * Mathf.PI * 2f; // чтобы монеты качались не синхронно
    }

    private void Update()
    {
        // Оптимизация: монеты вне поля зрения камеры не анимируются
        if (_renderer != null && !_renderer.isVisible)
            return;

        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        float y = Mathf.Sin(Time.time * bobFrequency + _bobOffset) * bobAmplitude;
        transform.position = _startPosition + Vector3.up * y;
    }

    private void OnTriggerEnter(Collider other)
    {
        var wallet = other.GetComponentInParent<Wallet>();
        if (wallet != null)
            TryCollect(wallet);
    }

    public bool IsCollected => _collected;

    /// <summary>Собрать монету в указанный кошелёк (используется триггером, отладкой и тестами).</summary>
    public bool TryCollect(Wallet wallet)
    {
        if (_collected || wallet == null)
            return false;

        _collected = true;
        wallet.AddCoins(value);
        Collected?.Invoke(this, transform.position);

        if (pickupEffectPrefab != null)
            EffectPool.Spawn(pickupEffectPrefab, transform.position);

        Destroy(gameObject);
        return true;
    }
}
