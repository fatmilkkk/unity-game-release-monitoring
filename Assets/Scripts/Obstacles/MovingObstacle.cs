using UnityEngine;

/// <summary>
/// Подвижное препятствие. Двигается туда-обратно или вращается
/// и наносит урон игроку при касании (с отбрасыванием).
/// Когда HP игрока падает до нуля, игрок уничтожается (см. PlayerHealth).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MovingObstacle : MonoBehaviour
{
    public enum MotionType
    {
        PingPong,
        Rotate
    }

    [Header("Движение")]
    [SerializeField] private MotionType motion = MotionType.PingPong;
    [Tooltip("Смещение от стартовой точки (для PingPong)")]
    [SerializeField] private Vector3 moveOffset = new Vector3(8f, 0f, 0f);
    [SerializeField] private float moveSpeed = 1.5f;
    [Tooltip("Сдвиг фазы, чтобы препятствия двигались не синхронно")]
    [SerializeField] private float phase;
    [Tooltip("Скорость вращения, градусов в секунду (для Rotate)")]
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);

    [Header("Урон")]
    [SerializeField] private int damage = 25;
    [Tooltip("Сила отбрасывания игрока при ударе")]
    [SerializeField] private float knockback = 8f;

    private Rigidbody _rb;
    private Vector3 _startPosition;

    // Кэш: OnCollisionStay вызывается каждый шаг физики, GetComponentInParent — дорогой вызов
    private Collider _cachedCollider;
    private PlayerHealth _cachedHealth;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _startPosition = _rb.position;
    }

    private void FixedUpdate()
    {
        switch (motion)
        {
            case MotionType.PingPong:
                float t = (Mathf.Sin(Time.time * moveSpeed + phase) + 1f) * 0.5f;
                _rb.MovePosition(_startPosition + moveOffset * t);
                break;

            case MotionType.Rotate:
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(rotationSpeed * Time.fixedDeltaTime));
                break;
        }
    }

    private void OnCollisionEnter(Collision collision) => TryHit(collision);

    private void OnCollisionStay(Collision collision) => TryHit(collision);

    private void TryHit(Collision collision)
    {
        Collider other = collision.collider;
        if (other != _cachedCollider)
        {
            _cachedCollider = other;
            _cachedHealth = other.GetComponentInParent<PlayerHealth>();
        }

        PlayerHealth health = _cachedHealth;
        if (health == null || health.IsDead)
            return;

        // PlayerHealth сам учитывает время неуязвимости после удара
        if (!health.TakeDamage(damage) || health.IsDead)
            return;

        Rigidbody body = collision.rigidbody;
        if (body == null)
            return;

        Vector3 from = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        Vector3 direction = body.position - from;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = body.position - transform.position;
        direction.y = 0f;

        direction = (direction.normalized + Vector3.up * 0.4f).normalized;
        body.AddForce(direction * knockback, ForceMode.VelocityChange);
    }

    private void OnDrawGizmosSelected()
    {
        if (motion != MotionType.PingPong)
            return;

        Vector3 start = Application.isPlaying ? _startPosition : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, start + moveOffset);
        Gizmos.DrawWireCube(start + moveOffset, transform.lossyScale);
    }
}
