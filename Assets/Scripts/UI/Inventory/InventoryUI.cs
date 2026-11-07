using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран инвентаря: сетка ячеек, панель описания выбранного предмета и кнопка «Использовать».
/// Выбор — кликом мышью или стрелками, использование — кнопкой или Enter.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventorySlotUI[] slots;
    [SerializeField] private Text itemNameText;
    [SerializeField] private Text itemDescriptionText;
    [SerializeField] private Image itemPreview;
    [SerializeField] private Text itemPreviewText;
    [SerializeField] private Button useButton;
    [SerializeField] private Text capacityText;
    [SerializeField] private UIPunch detailsPunch;

    private int _selected;
    private bool _listenersAdded;

    private void Awake() => AddListeners();

    private void AddListeners()
    {
        if (_listenersAdded)
            return;
        _listenersAdded = true;

        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            if (slots[i] != null && slots[i].Button != null)
                slots[i].Button.onClick.AddListener(() => Select(index));
        }

        if (useButton != null)
            useButton.onClick.AddListener(UseSelected);
    }

    private void OnEnable()
    {
        AddListeners();
        if (inventory != null)
        {
            inventory.Changed += Refresh;
            inventory.UseRejected += OnUseRejected;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.Changed -= Refresh;
            inventory.UseRejected -= OnUseRejected;
        }
    }

    private void Update()
    {
        if (inventory == null || inventory.Items.Count == 0)
            return;

        int count = inventory.Items.Count;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            Select((_selected + 1) % count);
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            Select((_selected - 1 + count) % count);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            UseSelected();
    }

    private void Refresh()
    {
        if (inventory == null)
            return;

        var items = inventory.Items;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (i < items.Count) slots[i].SetItem(items[i].Type, items[i].Count);
            else slots[i].SetEmpty();
        }

        if (capacityText != null)
            capacityText.text = $"Ячейки: {items.Count}/{inventory.Capacity}";

        _selected = items.Count == 0 ? -1 : Mathf.Clamp(_selected < 0 ? 0 : _selected, 0, items.Count - 1);
        UpdateSelection(false);
    }

    private void Select(int index)
    {
        if (inventory == null || index < 0 || index >= inventory.Items.Count)
            return;
        _selected = index;
        UpdateSelection(true);
    }

    private void UpdateSelection(bool animate)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null)
                slots[i].SetSelected(i == _selected);

        bool hasSelection = inventory != null && _selected >= 0 && _selected < inventory.Items.Count;
        if (useButton != null)
            useButton.interactable = hasSelection;

        if (!hasSelection)
        {
            if (itemNameText != null) itemNameText.text = "Инвентарь пуст";
            if (itemDescriptionText != null)
                itemDescriptionText.text = "Подойдите к бонусу на уровне, чтобы получить предмет.";
            if (itemPreview != null) itemPreview.color = new Color(1f, 1f, 1f, 0.1f);
            if (itemPreviewText != null) itemPreviewText.text = "?";
            return;
        }

        InventoryEntry entry = inventory.Items[_selected];
        if (itemNameText != null) itemNameText.text = $"{ItemDatabase.GetName(entry.Type)}  x{entry.Count}";
        if (itemDescriptionText != null) itemDescriptionText.text = ItemDatabase.GetDescription(entry.Type);
        if (itemPreview != null) itemPreview.color = ItemDatabase.GetColor(entry.Type);
        if (itemPreviewText != null) itemPreviewText.text = ItemDatabase.GetIcon(entry.Type);

        if (animate)
        {
            slots[_selected]?.Pop();
            detailsPunch?.PunchScale(0.05f, 0.2f);
        }
    }

    private void UseSelected()
    {
        if (inventory == null || _selected < 0 || _selected >= inventory.Items.Count)
            return;

        inventory.Use(inventory.Items[_selected].Type);
    }

    private void OnUseRejected(ItemType type, string reason)
    {
        if (_selected >= 0 && _selected < slots.Length && slots[_selected] != null)
            slots[_selected].Shake();
    }
}
