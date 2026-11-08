using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сбор ошибок и исключений во время игры. Каждая уникальная ошибка отправляется в аналитику
/// один раз за сессию (с числом повторов в конце сессии), последние ошибки прикладываются к отзыву игрока.
/// </summary>
public static class CrashReporter
{
    private const int MaxRecent = 5;

    private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
    private static readonly List<string> Recent = new List<string>();
    private static bool _registered;

    public static int UniqueErrors => Counts.Count;

    public static int TotalErrors
    {
        get
        {
            int total = 0;
            foreach (int c in Counts.Values) total += c;
            return total;
        }
    }

    /// <summary>Последние ошибки (для отзыва с технической информацией).</summary>
    public static IReadOnlyList<string> RecentErrors => Recent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Counts.Clear();
        Recent.Clear();
        if (_registered)
            Application.logMessageReceived -= OnLog;
        _registered = false;
    }

    public static void Register()
    {
        if (_registered)
            return;
        Application.logMessageReceived += OnLog;
        _registered = true;
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
            return;
        Report(condition, stackTrace, type.ToString());
    }

    public static void Report(string message, string stackTrace, string type)
    {
        string firstStackLine = FirstLine(stackTrace);
        string key = type + "|" + Trim(message, 200) + "|" + firstStackLine;

        if (Counts.TryGetValue(key, out int count))
        {
            Counts[key] = count + 1;
            return;
        }

        Counts[key] = 1;
        Recent.Add($"[{type}] {Trim(message, 200)} @ {firstStackLine}");
        if (Recent.Count > MaxRecent)
            Recent.RemoveAt(0);

        AnalyticsService.Track("error",
            "type", type,
            "message", Trim(message, 300),
            "stack", Trim(stackTrace, 1000),
            "scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    /// <summary>Итоги по ошибкам за сессию (вызывается при выходе).</summary>
    public static void ReportSummary()
    {
        if (Counts.Count == 0)
            return;
        AnalyticsService.Track("error_summary",
            "unique", UniqueErrors.ToString(),
            "total", TotalErrors.ToString());
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        int nl = text.IndexOf('\n');
        return (nl >= 0 ? text.Substring(0, nl) : text).Trim();
    }

    private static string Trim(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text.Substring(0, max) + "…";
    }
}
