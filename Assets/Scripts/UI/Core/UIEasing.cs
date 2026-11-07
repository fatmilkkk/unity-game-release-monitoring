using UnityEngine;

/// <summary>Функции сглаживания для анимаций интерфейса (t от 0 до 1).</summary>
public static class UIEasing
{
    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    public static float InCubic(float t) => t * t * t;

    /// <summary>С небольшим «перелётом» — эффект пружины.</summary>
    public static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    public static float OutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        const float c4 = 2f * Mathf.PI / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }
}
