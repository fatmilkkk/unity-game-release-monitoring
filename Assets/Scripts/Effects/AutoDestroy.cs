using UnityEngine;

/// <summary>
/// Убирает объект эффекта через заданное время: возвращает его в пул EffectPool,
/// а если объект создан не пулом — уничтожает.
/// </summary>
public class AutoDestroy : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;

    private float _timeLeft;

    private void OnEnable()
    {
        _timeLeft = lifetime;
    }

    private void Update()
    {
        _timeLeft -= Time.deltaTime;
        if (_timeLeft > 0f)
            return;

        if (!EffectPool.Release(gameObject))
            Destroy(gameObject);
    }
}
