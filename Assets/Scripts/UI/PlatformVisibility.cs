using UnityEngine;

/// <summary>
/// Скрывает элемент интерфейса на платформах, где он не имеет смысла:
/// например, кнопку «Выход» в браузере (WebGL) и на iOS, переключатель полноэкранного режима на телефонах.
/// </summary>
public class PlatformVisibility : MonoBehaviour
{
    [SerializeField] private bool hideOnMobile;
    [SerializeField] private bool hideOnWebGL;
    [SerializeField] private bool hideOnIOS;

    private void Awake()
    {
        if (ShouldHide(Application.platform, Application.isMobilePlatform))
            gameObject.SetActive(false);
    }

    public bool ShouldHide(RuntimePlatform platform, bool isMobile)
    {
        if (hideOnMobile && isMobile) return true;
        if (hideOnWebGL && platform == RuntimePlatform.WebGLPlayer) return true;
        if (hideOnIOS && platform == RuntimePlatform.IPhonePlayer) return true;
        return false;
    }
}
