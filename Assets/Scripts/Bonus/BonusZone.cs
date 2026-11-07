using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Зона-триггер бонуса. При входе игрока в зону включает в Animator параметр
/// PlayerInZone = true (бонус «оживает»: Idle → Activate → Active),
/// при выходе — PlayerInZone = false (Active → Deactivate → Idle).
/// Сами состояния и переходы настроены в машине состояний BonusAnimator.controller.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BonusZone : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Имя bool-параметра в Animator")]
    [SerializeField] private string inZoneParameter = "PlayerInZone";

    [Header("События (по желанию)")]
    [SerializeField] private UnityEvent onPlayerEnter;
    [SerializeField] private UnityEvent onPlayerExit;

    private int _parameterHash;
    private int _playersInside;

    public bool PlayerInside => _playersInside > 0;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        GetComponent<Collider>().isTrigger = true;
        _parameterHash = Animator.StringToHash(inZoneParameter);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        _playersInside++;
        if (_playersInside == 1)
        {
            if (animator != null)
                animator.SetBool(_parameterHash, true);
            onPlayerEnter?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        _playersInside = Mathf.Max(0, _playersInside - 1);
        if (_playersInside == 0)
        {
            if (animator != null)
                animator.SetBool(_parameterHash, false);
            onPlayerExit?.Invoke();
        }
    }

    private static bool IsPlayer(Collider other)
    {
        return other.GetComponentInParent<PlayerController>() != null;
    }
}
