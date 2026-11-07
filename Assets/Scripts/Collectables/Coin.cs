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
    private float _bobOffset;
    private bool _collected;

    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        _startPosition = transform.position;
        _bobOffset = UnityEngine.Random.value * Mathf.PI * 2f; // чтобы монеты качались не синхронно
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        float y = Mathf.Sin(Time.time * bobFrequency + _bobOffset) * bobAmplitude;
        transform.position = _startPosition + Vector3.up * y;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected)
            return;

        var wallet = other.GetComponentInParent<Wallet>();
        if (wallet == null)
            return;

        _collected = true;
        wallet.AddCoins(value);
        Collected?.Invoke(this, transform.position);

        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
