using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Анимация «монета летит в кошелёк»: при сборе монеты из её экранной позиции
/// вылетает значок и по дуге летит к счётчику монет. Счётчик «подпрыгивает» при прилёте.
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

    private void OnEnable() => Coin.Collected += OnCoinCollected;

    private void OnDisable() => Coin.Collected -= OnCoinCollected;

    private void OnCoinCollected(Coin coin, Vector3 worldPosition)
    {
        UISound.PlayCoin();

        Camera cam = Camera.main;
        if (cam == null || target == null)
            return;

        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z < 0f)
            return;

        var go = new GameObject("FlyingCoin", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(iconSize, iconSize);
        rt.position = screen;

        var image = go.GetComponent<Image>();
        image.sprite = coinSprite;
        image.color = coinColor;
        image.raycastTarget = false;

        StartCoroutine(Fly(rt, screen));
    }

    private IEnumerator Fly(RectTransform icon, Vector3 start)
    {
        float t = 0f;
        while (t < flyTime && icon != null)
        {
            t += Time.unscaledDeltaTime;
            float k = UIEasing.InCubic(Mathf.Clamp01(t / flyTime)) * 0.6f + Mathf.Clamp01(t / flyTime) * 0.4f;
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

        if (icon != null)
            Destroy(icon.gameObject);
        if (targetPunch != null)
            targetPunch.PunchScale(0.3f, 0.25f);
    }
}
