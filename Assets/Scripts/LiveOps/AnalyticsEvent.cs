using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>Параметр события аналитики (ключ — значение строкой).</summary>
[Serializable]
public class AnalyticsParam
{
    public string k;
    public string v;

    public AnalyticsParam() { }

    public AnalyticsParam(string key, string value)
    {
        k = key;
        v = value;
    }
}

/// <summary>
/// Одно событие аналитики. Хранится как строка JSON в файле events.jsonl.
/// Не содержит персональных данных: только анонимный идентификатор установки и сессии.
/// </summary>
[Serializable]
public class AnalyticsEvent
{
    public string name;
    public string time;       // UTC, ISO 8601
    public string session;
    public string install;
    public string version;
    public string platform;
    public List<AnalyticsParam> data = new List<AnalyticsParam>();

    public string Get(string key, string fallback = "")
    {
        if (data == null) return fallback;
        foreach (AnalyticsParam p in data)
            if (p != null && p.k == key)
                return p.v;
        return fallback;
    }

    public float GetFloat(string key, float fallback = 0f)
    {
        return float.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    public int GetInt(string key, int fallback = 0)
    {
        return int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    public DateTime GetTime()
    {
        return DateTime.TryParse(time, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime t)
            ? t
            : DateTime.MinValue;
    }
}
