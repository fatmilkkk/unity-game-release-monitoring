using UnityEngine;

/// <summary>
/// Визуализация перемещения игрока системами частиц:
///  • пыль из-под шара — интенсивность зависит от скорости, только на земле;
///  • светящийся след — появляется на высокой скорости (опасной для бочек);
///  • облачко при приземлении после прыжка/падения.
/// Объект с этим скриптом НЕ дочерний к шару (шар вращается),
/// поэтому он сам следует за позицией игрока.
/// </summary>
public class PlayerMovementParticles : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private ParticleSystem speedTrail;
    [SerializeField] private ParticleSystem landingBurst;

    [Header("Пыль")]
    [SerializeField] private float minSpeed = 0.6f;
    [SerializeField] private float minDustRate = 5f;
    [SerializeField] private float maxDustRate = 45f;
    [SerializeField] private float speedForMaxDust = 10f;

    [Header("След скорости")]
    [Tooltip("С какой скорости появляется след (совпадает с порогом взрыва бочки)")]
    [SerializeField] private float trailSpeedThreshold = 7f;
    [SerializeField] private float trailRate = 60f;

    [Header("Приземление")]
    [SerializeField] private float landingSpeed = 3f;
    [SerializeField] private int landingParticles = 25;

    private bool _wasGrounded = true;
    private float _previousVerticalSpeed;

    private void LateUpdate()
    {
        if (player == null)
            return;

        transform.position = player.transform.position;

        bool alive = health == null || !health.IsDead;
        bool grounded = alive && player.IsGrounded;
        float speed = alive ? player.HorizontalSpeed : 0f;

        if (dust != null)
        {
            var emission = dust.emission;
            emission.rateOverTime = grounded && speed > minSpeed
                ? Mathf.Lerp(minDustRate, maxDustRate, speed / speedForMaxDust)
                : 0f;
        }

        if (speedTrail != null)
        {
            var emission = speedTrail.emission;
            emission.rateOverTime = alive && speed >= trailSpeedThreshold ? trailRate : 0f;
        }

        if (landingBurst != null && grounded && !_wasGrounded && -_previousVerticalSpeed >= landingSpeed)
            landingBurst.Emit(landingParticles);

        _wasGrounded = grounded;
        _previousVerticalSpeed = alive ? player.Velocity.y : 0f;
    }
}
