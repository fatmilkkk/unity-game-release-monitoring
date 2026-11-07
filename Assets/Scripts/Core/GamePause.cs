using System;
using UnityEngine;

/// <summary>
/// Глобальное состояние паузы. Пауза останавливает время игры (Time.timeScale = 0),
/// интерфейс при этом анимируется в «нескалированном» времени.
/// </summary>
public static class GamePause
{
    public static bool IsPaused { get; private set; }

    public static event Action<bool> PauseChanged;

    public static void SetPaused(bool paused)
    {
        if (IsPaused == paused)
            return;

        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        PauseChanged?.Invoke(paused);
    }

    /// <summary>Сбросить паузу (при загрузке сцены).</summary>
    public static void Reset()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }
}
