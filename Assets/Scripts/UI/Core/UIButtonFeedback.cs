using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Отклик кнопки: плавное увеличение при наведении, «нажатие» при клике и звук.
/// Работает в нескалированном времени (на паузе тоже).
/// </summary>
public class UIButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
    ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.07f;
    [SerializeField] private float pressScale = 0.94f;
    [SerializeField] private float speed = 14f;

    private Selectable _selectable;
    private Vector3 _baseScale;
    private bool _hovered;
    private bool _pressed;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        _hovered = false;
        _pressed = false;
        transform.localScale = _baseScale;
    }

    private bool Interactable => _selectable == null || _selectable.IsInteractable();

    private void Update()
    {
        float target = 1f;
        if (Interactable)
        {
            if (_pressed) target = pressScale;
            else if (_hovered) target = hoverScale;
        }

        Vector3 goal = _baseScale * target;
        transform.localScale = Vector3.Lerp(transform.localScale, goal, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        if (Interactable)
            UISound.PlayHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) => _pressed = true;

    public void OnPointerUp(PointerEventData eventData) => _pressed = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Interactable)
            UISound.PlayClick();
    }

    public void OnSelect(BaseEventData eventData) => _hovered = true;

    public void OnDeselect(BaseEventData eventData) => _hovered = false;
}
