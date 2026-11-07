using UnityEngine;

/// <summary>
/// Панель быстрого доступа внизу экрана: по ячейке на каждый тип предмета,
/// использование клавишами 1, 2, 3 или кликом. Пустые ячейки затемнены.
/// </summary>
public class HotbarUI : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventorySlotUI[] slots = new InventorySlotUI[3];

    private static readonly ItemType[] SlotTypes = { ItemType.Medkit, ItemType.Shield, ItemType.SpeedBoost };
    private static readonly KeyCode[] Keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
    private static readonly KeyCode[] KeypadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3 };

    private void Awake()
    {
        for (int i = 0; i < slots.Length && i < SlotTypes.Length; i++)
        {
            if (slots[i] == null) continue;
            int index = i;
            slots[i].SetKey((i + 1).ToString());
            if (slots[i].Button != null)
                slots[i].Button.onClick.AddListener(() => UseSlot(index));
        }
    }

    private void OnEnable()
    {
        if (inventory == null) return;
        inventory.Changed += Refresh;
        inventory.ItemAdded += OnItemAdded;
        inventory.ItemUsed += OnItemUsed;
        inventory.UseRejected += OnUseRejected;
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory == null) return;
        inventory.Changed -= Refresh;
        inventory.ItemAdded -= OnItemAdded;
        inventory.ItemUsed -= OnItemUsed;
        inventory.UseRejected -= OnUseRejected;
    }

    private void Update()
    {
        if (GamePause.IsPaused)
            return;

        for (int i = 0; i < Keys.Length && i < slots.Length; i++)
            if (Input.GetKeyDown(Keys[i]) || Input.GetKeyDown(KeypadKeys[i]))
                UseSlot(i);
    }

    private void UseSlot(int index)
    {
        if (inventory == null || GamePause.IsPaused) return;
        inventory.Use(SlotTypes[index]);
    }

    private void Refresh()
    {
        for (int i = 0; i < slots.Length && i < SlotTypes.Length; i++)
            if (slots[i] != null)
                slots[i].SetItem(SlotTypes[i], inventory.GetCount(SlotTypes[i]), true);
    }

    private int IndexOf(ItemType type) => System.Array.IndexOf(SlotTypes, type);

    private void OnItemAdded(ItemType type)
    {
        int i = IndexOf(type);
        if (i >= 0 && i < slots.Length && slots[i] != null) slots[i].Pop();
    }

    private void OnItemUsed(ItemType type)
    {
        int i = IndexOf(type);
        if (i >= 0 && i < slots.Length && slots[i] != null) slots[i].Pop();
    }

    private void OnUseRejected(ItemType type, string reason)
    {
        int i = IndexOf(type);
        if (i >= 0 && i < slots.Length && slots[i] != null) slots[i].Shake();
    }
}
