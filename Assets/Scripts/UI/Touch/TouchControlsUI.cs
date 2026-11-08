using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Сенсорное управление: показывает джойстик и кнопку прыжка, если оно включено в настройках
/// (по умолчанию — на мобильных устройствах и сенсорных экранах). Подсказки по клавиатуре при этом скрываются.
/// </summary>
public class TouchControlsUI : MonoBehaviour
{
    [SerializeField] private GameObject controlsRoot;
    [SerializeField] private Button jumpButton;
    [SerializeField] private GameObject keyboardHints;

    private void Awake()
    {
        if (jumpButton != null)
            jumpButton.onClick.AddListener(MobileInput.PressJump);
    }

    private void OnEnable()
    {
        GameSettings.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= Apply;
        MobileInput.Move = Vector2.zero;
    }

    private void Apply()
    {
        bool touch = GameSettings.TouchControls;
        if (controlsRoot != null)
            controlsRoot.SetActive(touch);
        if (keyboardHints != null)
            keyboardHints.SetActive(GameSettings.ShowHints && !touch);
    }
}
