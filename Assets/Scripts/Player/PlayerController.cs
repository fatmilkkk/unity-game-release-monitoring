using UnityEngine;

/// <summary>
/// Управление шаром игрока: движение относительно камеры (WASD / стрелки) и прыжок (Пробел).
/// Движение реализовано через физику (Rigidbody), поэтому скорость шара важна
/// для взаимодействия с бочками со взрывчаткой.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [Tooltip("Ускорение шара, м/с²")]
    [SerializeField] private float acceleration = 30f;
    [Tooltip("Максимальная горизонтальная скорость, которую можно набрать управлением, м/с")]
    [SerializeField] private float maxSpeed = 12f;
    [Tooltip("Вертикальная скорость при прыжке, м/с")]
    [SerializeField] private float jumpVelocity = 6f;

    [Header("Проверка земли")]
    [SerializeField] private float groundCheckExtra = 0.15f;

    [Header("Ссылки")]
    [Tooltip("Камера, относительно которой считается направление движения. Если пусто — Camera.main")]
    [SerializeField] private Transform cameraTransform;

    private Rigidbody _rb;
    private SphereCollider _sphere;
    private Vector3 _moveInput;
    private bool _jumpRequested;
    private float _speedMultiplier = 1f;
    private float _boostUntil;

    /// <summary>Стоит ли шар на поверхности.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Текущая скорость шара.</summary>
    public Vector3 Velocity => _rb != null ? _rb.velocity : Vector3.zero;

    /// <summary>Горизонтальная составляющая скорости, м/с.</summary>
    public float HorizontalSpeed
    {
        get
        {
            Vector3 v = Velocity;
            v.y = 0f;
            return v.magnitude;
        }
    }

    /// <summary>Сколько секунд осталось у ускорения (0 — ускорения нет).</summary>
    public float SpeedBoostTimeLeft => Mathf.Max(0f, _boostUntil - Time.time);
    public bool IsBoosted => SpeedBoostTimeLeft > 0f;

    /// <summary>Временно увеличить ускорение и предел скорости (предмет «Ускоритель»).</summary>
    public void ApplySpeedBoost(float multiplier, float duration)
    {
        _speedMultiplier = Mathf.Max(1f, multiplier);
        _boostUntil = Time.time + duration;
    }

    /// <summary>Расстояние от центра шара до его нижней точки.</summary>
    public float GroundOffset => _sphere != null ? _sphere.radius * transform.lossyScale.y : 0.5f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _sphere = GetComponent<SphereCollider>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (GamePause.IsPaused)
        {
            _moveInput = Vector3.zero;
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }

        _moveInput = Vector3.ClampMagnitude(forward * v + right * h, 1f);

        if (Input.GetButtonDown("Jump"))
            _jumpRequested = true;
    }

    private void FixedUpdate()
    {
        IsGrounded = Physics.Raycast(transform.position, Vector3.down,
            GroundOffset + groundCheckExtra, ~0, QueryTriggerInteraction.Ignore);

        float multiplier = IsBoosted ? _speedMultiplier : 1f;

        if (_moveInput.sqrMagnitude > 0.0001f)
        {
            Vector3 horizontal = _rb.velocity;
            horizontal.y = 0f;

            // Разгоняемся, пока скорость в направлении ввода меньше предела
            float speedAlongInput = Vector3.Dot(horizontal, _moveInput.normalized);
            if (speedAlongInput < maxSpeed * multiplier)
                _rb.AddForce(_moveInput * (acceleration * multiplier), ForceMode.Acceleration);
        }

        if (_jumpRequested && IsGrounded)
        {
            Vector3 vel = _rb.velocity;
            vel.y = jumpVelocity;
            _rb.velocity = vel;
        }

        _jumpRequested = false;
    }
}
