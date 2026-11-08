using UnityEngine;
using UnityEngine.UI;

/// <summary>Подпись с версией сборки (берётся из BuildInfo) — помогает игрокам указывать версию в отзывах.</summary>
[RequireComponent(typeof(Text))]
public class VersionLabel : MonoBehaviour
{
    [SerializeField] private string prefix = "Практика • Unity 2022.3 LTS • ";

    private void Start()
    {
        BuildInfo info = BuildInfo.Current;
        string text = prefix + info.ShortLabel;
        if (info.development)
            text += " • DEV";
        GetComponent<Text>().text = text;
    }
}
