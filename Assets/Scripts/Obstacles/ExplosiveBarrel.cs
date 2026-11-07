using System.Collections;
using UnityEngine;

/// <summary>
/// Бочка со взрывчаткой. Если игрок врезается в неё со скоростью не меньше порога,
/// бочка взрывается и игрок уничтожается. Слабый удар просто толкает бочку.
/// Взрыв отбрасывает физические объекты рядом и вызывает цепную реакцию соседних бочек.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ExplosiveBarrel : MonoBehaviour
{
    [Header("Порог срабатывания")]
    [Tooltip("Минимальная относительная скорость удара игрока (м/с), при которой бочка взрывается")]
    [SerializeField] private float explodeImpactSpeed = 7f;

    [Header("Взрыв")]
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float explosionForce = 900f;
    [SerializeField] private float upwardsModifier = 1.5f;
    [Tooltip("Урон игроку, оказавшемуся рядом со взрывом (но не врезавшемуся в бочку)")]
    [SerializeField] private int splashDamage = 40;
    [Tooltip("Задержка взрыва соседних бочек при цепной реакции, с")]
    [SerializeField] private float chainDelay = 0.2f;
    [SerializeField] private GameObject explosionEffectPrefab;

    private bool _exploded;

    public float ExplodeImpactSpeed => explodeImpactSpeed;
    public bool HasExploded => _exploded;

    private void OnCollisionEnter(Collision collision)
    {
        if (_exploded)
            return;

        var player = collision.collider.GetComponentInParent<PlayerHealth>();
        if (player == null)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed >= explodeImpactSpeed)
            Explode(player);
    }

    /// <summary>Взорвать бочку. directHit — игрок, который врезался в бочку (будет уничтожен).</summary>
    public void Explode(PlayerHealth directHit = null)
    {
        if (_exploded)
            return;
        _exploded = true;

        Vector3 center = transform.position;

        if (explosionEffectPrefab != null)
            Instantiate(explosionEffectPrefab, center, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(center, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            Rigidbody body = hit.attachedRigidbody;
            if (body != null && body.gameObject != gameObject && !body.isKinematic)
                body.AddExplosionForce(explosionForce, center, explosionRadius, upwardsModifier);

            var barrel = hit.GetComponentInParent<ExplosiveBarrel>();
            if (barrel != null && barrel != this)
                barrel.ExplodeDelayed(chainDelay);

            var health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null && health != directHit)
                health.TakeDamage(splashDamage);
        }

        if (directHit != null)
            directHit.Kill();

        Destroy(gameObject);
    }

    public void ExplodeDelayed(float delay)
    {
        if (_exploded || !isActiveAndEnabled)
            return;
        StartCoroutine(ExplodeAfter(delay));
    }

    private IEnumerator ExplodeAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Explode();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
