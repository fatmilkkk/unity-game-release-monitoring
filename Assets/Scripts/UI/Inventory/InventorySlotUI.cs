using UnityEngine;
using UnityEngine.UI;

/// <summary>Ячейка инвентаря / панели быстрого доступа: значок предмета, количество, клавиша, выделение.</summary>
public class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private Text iconText;
    [SerializeField] private Text countText;
    [SerializeField] private Text keyText;
    [SerializeField] private Image selection;
    [SerializeField] private UIPunch punch;

    [SerializeField] private Color filledColor = new Color(0.16f, 0.19f, 0.25f, 0.95f);
    [SerializeField] private Color emptyColor = new Color(0.1f, 0.12f, 0.16f, 0.7f);

    public Button Button => button;
    public bool IsEmpty { get; private set; } = true;

    public void SetItem(ItemType type, int count, bool dimWhenZero = false)
    {
        bool hasItems = count > 0;
        IsEmpty = !hasItems && !dimWhenZero;

        if (icon != null)
        {
            icon.enabled = true;
            Color c = ItemDatabase.GetColor(type);
            if (!hasItems) c = Color.Lerp(c, Color.gray, 0.7f) * new Color(1f, 1f, 1f, 0.5f);
            icon.color = c;
        }
        if (iconText != null)
        {
            iconText.enabled = true;
            iconText.text = ItemDatabase.GetIcon(type);
            iconText.color = hasItems ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }
        if (countText != null)
            countText.text = count > 1 || dimWhenZero ? $"x{count}" : string.Empty;
        if (background != null)
            background.color = hasItems ? filledColor : emptyColor;
    }

    public void SetEmpty()
    {
        IsEmpty = true;
        if (icon != null) icon.enabled = false;
        if (iconText != null) iconText.enabled = false;
        if (countText != null) countText.text = string.Empty;
        if (background != null) background.color = emptyColor;
    }

    public void SetKey(string key)
    {
        if (keyText != null)
            keyText.text = key;
    }

    public void SetSelected(bool selected)
    {
        if (selection != null)
            selection.enabled = selected;
    }

    public void Pop()
    {
        if (punch != null)
            punch.PunchScale(0.25f, 0.3f);
    }

    public void Shake()
    {
        if (punch != null)
            punch.Shake(8f, 0.25f);
    }
}
