using UnityEngine;

/// <summary>Равномерно вращает объект (используется для облёта камеры в главном меню).</summary>
public class SimpleRotator : MonoBehaviour
{
    [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 10f, 0f);

    private void Update()
    {
        transform.Rotate(degreesPerSecond * Time.unscaledDeltaTime, Space.World);
    }
}
