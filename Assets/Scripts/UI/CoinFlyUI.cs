using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Анимация «монета летит в кошелёк»: при сборе монеты из её экранной позиции
/// вылетает значок и по дуге летит к счётчику монет. Счётчик «подпрыгивает» при прилёте.
/// Значки берутся из собственного пула (без создания объектов на каждую монету).
/// </summary>
public class CoinFlyUI : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private UIPunch targetPunch;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private Color coinColor = new Color(1f, 0.82f, 0.2f);
    [SerializeField] private float flyTime = 0.6f;
    [SerializeField] private float iconSize = 42f;
    [SerializeField] private float arcHeight = 160f;
    [SerializeField] private int prewarmCount = 8;

    private readonly Stack<RectTransform> _pool = new Stack<RectTransform>();
    private Camera _camera;

    private void Awake()
    {
        for (int i = 0; i < prewarmCount; i++)
            _pool.Push(CreateIcon());
    }

    private void OnEnable() => Coin.Collected += OnCoinCollected;

    private void OnDisable() => Coin.Collected -= OnCoinCollected;

    private RectTransform CreateIcon()
    {
        var go = new GameObject("FlyingCoin", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(iconSize, iconSize);

        var image = go.GetComponent<Image>();
        image.sprite = coinSprite;
        image.color = coinColor;
        image.raycastTarget = false;

        go.SetActive(false);
        return rt;
    }

    private void OnCoinCollected(Coin coin, Vector3 worldPosition)
    {
        UISound.PlayCoin();

        // Оптимизация: Camera.main кэшируется (раньше вызывался на каждую монету)
        if (_camera == null)
            _camera = Camera.main;
        if (_camera == null || target == null)
            return;

        Vector3 screen = _camera.WorldToScreenPoint(worldPosition);
        if (screen.z < 0f)
            return;

        RectTransform icon = _pool.Count > 0 ? _pool.Pop() : CreateIcon();
        icon.gameObject.SetActive(true);
        icon.position = screen;
        StartCoroutine(Fly(icon, screen));
    }

    private IEnumerator Fly(RectTransform icon, Vector3 start)
    {
        float t = 0f;
        while (t < flyTime)
        {
            t += Time.unscaledDeltaTime;
            float linear = Mathf.Clamp01(t / flyTime);
            float k = UIEasing.InCubic(linear) * 0.6f + linear * 0.4f;
            Vector3 end = target.position;
            Vector3 control = (start + end) * 0.5f + Vector3.up * arcHeight;

            // Квадратичная кривая Безье
            Vector3 a = Vector3.Lerp(start, control, k);
            Vector3 b = Vector3.Lerp(control, end, k);
            icon.position = Vector3.Lerp(a, b, k);
            icon.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.6f, k);
            icon.localRotation = Quaternion.Euler(0f, 0f, k * 360f);
            yield return null;
        }

        icon.gameObject.SetActive(false);
        _pool.Push(icon);

        if (targetPunch != null)
            targetPunch.PunchScale(0.3f, 0.25f);
    }

    /// <summary>Для тестов и статистики: сколько значков свободно в пуле.</summary>
    public int PooledIcons => _pool.Count;
}
