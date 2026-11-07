using UnityEngine;
using UnityEngine.UI;

/// <summary>Отображение баланса кошелька на экране с небольшой «пульсацией» при изменении.</summary>
public class WalletUI : MonoBehaviour
{
    [SerializeField] private Wallet wallet;
    [SerializeField] private Text label;
    [SerializeField] private string format = "Монеты: {0}";
    [SerializeField] private float pulseScale = 1.3f;
    [SerializeField] private float pulseDuration = 0.25f;

    private float _pulseTimer;
    private Vector3 _baseScale = Vector3.one;

    private void Awake()
    {
        if (label != null)
            _baseScale = label.rectTransform.localScale;
    }

    private void OnEnable()
    {
        if (wallet != null)
            wallet.CoinsChanged += OnCoinsChanged;
    }

    private void OnDisable()
    {
        if (wallet != null)
            wallet.CoinsChanged -= OnCoinsChanged;
    }

    private void Start()
    {
        if (wallet != null && label != null)
            label.text = string.Format(format, wallet.Coins);
    }

    private void OnCoinsChanged(int coins)
    {
        if (label == null)
            return;

        bool changed = label.text != string.Format(format, coins);
        label.text = string.Format(format, coins);
        if (changed)
            _pulseTimer = pulseDuration;
    }

    private void Update()
    {
        if (label == null || _pulseTimer <= 0f)
            return;

        _pulseTimer -= Time.deltaTime;
        float k = Mathf.Clamp01(_pulseTimer / pulseDuration);
        label.rectTransform.localScale = _baseScale * Mathf.Lerp(1f, pulseScale, k);
    }
}
