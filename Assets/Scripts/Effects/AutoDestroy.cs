using UnityEngine;

/// <summary>Уничтожает объект эффекта через заданное время.</summary>
public class AutoDestroy : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
