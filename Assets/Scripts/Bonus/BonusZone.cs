using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Зона-триггер бонуса. При входе игрока в зону включает в Animator параметр
/// PlayerInZone = true (бонус «оживает»: Idle → Activate → Active),
/// при выходе — PlayerInZone = false (Active → Deactivate → Idle).
/// Сами состояния и переходы настроены в машине состояний BonusAnimator.controller.
/// При первом входе бонус кладёт в инвентарь игрока предмет-награду.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BonusZone : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Имя bool-параметра в Animator")]
    [SerializeField] private string inZoneParameter = "PlayerInZone";

    [Header("Награда")]
    [SerializeField] private bool giveReward = true;
    [SerializeField] private ItemType rewardItem = ItemType.Medkit;

    [Header("События (по желанию)")]
    [SerializeField] private UnityEvent onPlayerEnter;
    [SerializeField] private UnityEvent onPlayerExit;

    /// <summary>Игрок получил награду бонуса (бонус, предмет).</summary>
    public static event Action<BonusZone, ItemType> RewardGiven;

    private int _parameterHash;
    private bool _rewardGiven;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => RewardGiven = null;
    private int _playersInside;

    public bool PlayerInside => _playersInside > 0;
    public bool GivesReward => giveReward;
    public bool RewardCollected => _rewardGiven;
    public ItemType RewardItem => rewardItem;

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

        TryGiveReward(other);
    }

    private void TryGiveReward(Collider other)
    {
        if (!giveReward || _rewardGiven)
            return;

        var inventory = other.GetComponentInParent<Inventory>();
        if (inventory == null || !inventory.Add(rewardItem))
            return;

        _rewardGiven = true;
        RewardGiven?.Invoke(this, rewardItem);
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
