using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Виртуальный джойстик для сенсорных экранов. Передаёт направление в MobileInput.Move.
/// Работает и мышью — так сенсорное управление можно проверить на ПК.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform knob;
    [Tooltip("Мёртвая зона, доля радиуса")]
    [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.12f;

    private float Radius => background != null ? background.rect.width * 0.5f : 100f;

    public Vector2 Value { get; private set; }

    public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position,
                eventData.pressEventCamera, out Vector2 local))
            return;

        Vector2 value = Vector2.ClampMagnitude(local / Radius, 1f);
        if (value.magnitude < deadZone)
            value = Vector2.zero;

        SetValue(value);
    }

    public void OnPointerUp(PointerEventData eventData) => SetValue(Vector2.zero);

    private void OnDisable() => SetValue(Vector2.zero);

    private void SetValue(Vector2 value)
    {
        Value = value;
        MobileInput.Move = value;
        if (knob != null)
            knob.anchoredPosition = value * Radius;
    }
}
