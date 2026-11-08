using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Всплывающие уведомления: выезжают справа, держатся пару секунд и уезжают.
/// Сообщения ставятся в очередь и показываются по одному.
/// </summary>
public class ToastUI : MonoBehaviour
{
    [SerializeField] private UIPanel panel;
    [SerializeField] private Text label;
    [SerializeField] private Image accent;
    [SerializeField] private float showTime = 2f;

    private readonly Queue<(string message, Color color)> _queue = new Queue<(string, Color)>();
    private Coroutine _routine;

    public static ToastUI Instance { get; private set; }

    private void Awake() => Instance = this;

    // Исправление: если объект выключили во время показа, корутина останавливается —
    // без сброса флага новые уведомления больше никогда бы не показались
    private void OnDisable() => _routine = null;

    private void OnEnable()
    {
        if (_queue.Count > 0 && _routine == null)
            _routine = StartCoroutine(Process());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Show(string message, Color color)
    {
        if (Instance != null)
            Instance.Enqueue(message, color);
    }

    public void Enqueue(string message, Color color)
    {
        _queue.Enqueue((message, color));
        if (_routine == null && isActiveAndEnabled)
            _routine = StartCoroutine(Process());
    }

    private IEnumerator Process()
    {
        while (_queue.Count > 0)
        {
            var (message, color) = _queue.Dequeue();
            if (label != null) label.text = message;
            if (accent != null) accent.color = color;

            panel.Show();
            yield return new WaitForSecondsRealtime(showTime);
            panel.Hide();
            yield return new WaitForSecondsRealtime(0.3f);
        }
        _routine = null;
    }
}
