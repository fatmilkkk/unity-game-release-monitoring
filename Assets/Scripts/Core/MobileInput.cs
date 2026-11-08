using UnityEngine;

/// <summary>
/// Ввод с сенсорного экрана (виртуальный джойстик и кнопка прыжка).
/// PlayerController складывает его с клавиатурным вводом.
/// </summary>
public static class MobileInput
{
    /// <summary>Направление джойстика, компоненты от -1 до 1.</summary>
    public static Vector2 Move { get; set; }

    private static bool _jumpPressed;

    public static void PressJump() => _jumpPressed = true;

    /// <summary>Вернуть true один раз после нажатия кнопки прыжка.</summary>
    public static bool ConsumeJump()
    {
        bool value = _jumpPressed;
        _jumpPressed = false;
        return value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Move = Vector2.zero;
        _jumpPressed = false;
    }
}
