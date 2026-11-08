using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Значения удалённого конфига. Значения по умолчанию совпадают с балансом, зашитым в игру.</summary>
[Serializable]
public class RemoteConfigData
{
    public int configVersion = 0;

    [Header("Обновления и новости")]
    public string latestVersion = "";
    public string updateUrl = "";
    public string newsTitle = "";
    public string newsText = "";

    [Header("Баланс")]
    public float barrelExplodeSpeed = 7f;
    public float obstacleDamageMultiplier = 1f;
    public float threeStarTime = 90f;
    public float twoStarTime = 150f;

    [Header("Сервисы")]
    public string analyticsEndpoint = "";
    public string feedbackRepo = "fatmilkkk/unity-game-release-monitoring";

    /// <summary>Защита от ошибок в конфиге: значения приводятся к безопасным диапазонам.</summary>
    public void Validate()
    {
        barrelExplodeSpeed = Mathf.Clamp(barrelExplodeSpeed, 2f, 30f);
        obstacleDamageMultiplier = Mathf.Clamp(obstacleDamageMultiplier, 0.1f, 5f);
        threeStarTime = Mathf.Clamp(threeStarTime, 10f, 3600f);
        twoStarTime = Mathf.Clamp(twoStarTime, threeStarTime, 7200f);
        if (latestVersion == null) latestVersion = "";
        if (updateUrl == null) updateUrl = "";
        if (newsTitle == null) newsTitle = "";
        if (newsText == null) newsText = "";
        if (analyticsEndpoint == null) analyticsEndpoint = "";
        if (string.IsNullOrEmpty(feedbackRepo)) feedbackRepo = "fatmilkkk/unity-game-release-monitoring";
    }
}

/// <summary>
/// Удалённый конфиг (Live Ops): позволяет менять баланс, новости и сообщение об обновлении
/// без выпуска новой версии игры. Конфиг — JSON-файл Config/remote_config.json в репозитории,
/// загружается по ссылке raw.githubusercontent.com. Последний удачно загруженный конфиг кэшируется,
/// поэтому без интернета применяется он, а при первом запуске — значения по умолчанию.
/// </summary>
public static class RemoteConfig
{
    public const string DefaultUrl =
        "https://raw.githubusercontent.com/fatmilkkk/unity-game-release-monitoring/main/Config/remote_config.json";

    private const string CacheKey = "RemoteConfig.Cache";

    public static RemoteConfigData Current { get; private set; } = new RemoteConfigData();

    /// <summary>Откуда взяты текущие значения: default, cache или remote.</summary>
    public static string Source { get; private set; } = "default";

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = new RemoteConfigData();
        Source = "default";
        Changed = null;
    }

    /// <summary>Разобрать JSON конфига. Возвращает null, если JSON некорректный.</summary>
    public static RemoteConfigData Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var data = new RemoteConfigData();
            JsonUtility.FromJsonOverwrite(json, data);
            data.Validate();
            return data;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static void Apply(RemoteConfigData data, string source)
    {
        if (data == null)
            return;
        Current = data;
        Source = source;
        Changed?.Invoke();
    }

    public static void ResetToDefaults() => Apply(new RemoteConfigData(), "default");

    /// <summary>Применить кэш (мгновенно), затем загрузить свежий конфиг из сети.</summary>
    public static IEnumerator Load(string url = DefaultUrl)
    {
        RemoteConfigData cached = Parse(PlayerPrefs.GetString(CacheKey, ""));
        if (cached != null)
            Apply(cached, "cache");

        if (string.IsNullOrEmpty(url))
            yield break;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 6;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("[RemoteConfig] Нет связи с сервером конфига, используются " +
                          (cached != null ? "сохранённые" : "встроенные") + " значения");
                yield break;
            }

            string json = request.downloadHandler.text;
            RemoteConfigData fresh = Parse(json);
            if (fresh == null)
            {
                Debug.LogWarning("[RemoteConfig] Получен некорректный конфиг — оставлены прежние значения");
                yield break;
            }

            PlayerPrefs.SetString(CacheKey, json);
            PlayerPrefs.Save();
            Apply(fresh, "remote");
            AnalyticsService.Track("config_loaded", "config_version", fresh.configVersion.ToString());
        }
    }
}
