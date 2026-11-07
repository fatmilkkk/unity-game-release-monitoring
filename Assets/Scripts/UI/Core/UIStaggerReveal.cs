using System.Collections;
using UnityEngine;

/// <summary>
/// Поочерёдное появление дочерних элементов (например, кнопок меню):
/// каждый следующий выезжает сбоку и проявляется с небольшой задержкой.
/// </summary>
public class UIStaggerReveal : MonoBehaviour
{
    [SerializeField] private float delayBetween = 0.07f;
    [SerializeField] private float itemDuration = 0.35f;
    [SerializeField] private Vector2 offset = new Vector2(-120f, 0f);
    [SerializeField] private float startDelay = 0.1f;

    private void OnEnable()
    {
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        int count = transform.childCount;
        var groups = new CanvasGroup[count];
        var rects = new RectTransform[count];
        var targets = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            rects[i] = transform.GetChild(i) as RectTransform;
            if (rects[i] == null) continue;
            groups[i] = rects[i].GetComponent<CanvasGroup>();
            if (groups[i] == null) groups[i] = rects[i].gameObject.AddComponent<CanvasGroup>();
            targets[i] = rects[i].anchoredPosition;
            groups[i].alpha = 0f;
            rects[i].anchoredPosition = targets[i] + offset;
        }

        float time = -startDelay;
        float total = (count - 1) * delayBetween + itemDuration;
        while (time < total)
        {
            time += Time.unscaledDeltaTime;
            for (int i = 0; i < count; i++)
            {
                if (rects[i] == null) continue;
                float k = Mathf.Clamp01((time - i * delayBetween) / itemDuration);
                float eased = UIEasing.OutBack(k);
                groups[i].alpha = k;
                rects[i].anchoredPosition = Vector2.LerpUnclamped(targets[i] + offset, targets[i], eased);
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (rects[i] == null) continue;
            groups[i].alpha = 1f;
            rects[i].anchoredPosition = targets[i];
        }
    }
}
