using UnityEngine;

/// <summary>
/// Подгоняет RectTransform под безопасную область экрана (Screen.safeArea),
/// чтобы элементы HUD не попадали под «чёлку», вырез камеры и скруглённые углы смартфонов.
/// Пересчитывается при изменении разрешения или ориентации.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform _rect;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreen;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea || Screen.width != _lastScreen.x || Screen.height != _lastScreen.y)
            Apply();
    }

    private void Apply()
    {
        Rect safe = Screen.safeArea;
        _lastSafeArea = safe;
        _lastScreen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        _rect.anchorMin = min;
        _rect.anchorMax = max;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }
}
