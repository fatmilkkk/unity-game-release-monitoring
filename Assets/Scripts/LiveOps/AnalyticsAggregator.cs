using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Сводная статистика по событиям аналитики.</summary>
public class AnalyticsSummary
{
    public int Events;
    public int Sessions;
    public int Installs;
    public float AvgSessionMinutes;

    public int LevelStarts;
    public int Victories;
    public int Deaths;
    public int Abandoned;
    public float CompletionRate;
    public float AvgVictoryTime;
    public float AvgDeathTime;
    public readonly int[] Stars = new int[4];               // индекс = число звёзд
    /// <summary>Воронка: старт, 25 %, 50 %, 75 %, победа.</summary>
    public readonly int[] Funnel = new int[5];

    public readonly Dictionary<string, int> DeathCauses = new Dictionary<string, int>();
    public readonly List<Vector3> DeathPositions = new List<Vector3>();
    public readonly Dictionary<string, int> ItemsUsed = new Dictionary<string, int>();
    public readonly Dictionary<string, int> BonusesFound = new Dictionary<string, int>();

    public float AvgFps;
    public float LowFpsShare;
    public int PerfSamples;

    public readonly Dictionary<string, int> Errors = new Dictionary<string, int>();
    public readonly Dictionary<string, int> Versions = new Dictionary<string, int>();
    public readonly Dictionary<string, int> Platforms = new Dictionary<string, int>();

    public int FeedbackCount;
    public float AvgRating;
    public readonly Dictionary<string, int> FeedbackCategories = new Dictionary<string, int>();

    public readonly List<string> Recommendations = new List<string>();

    public static readonly string[] FunnelNames = { "Начали уровень", "Собрали 25 %", "Собрали 50 %", "Собрали 75 %", "Прошли уровень" };
}

/// <summary>
/// Анализ событий аналитики: подсчёт метрик (удержание в уровне, воронка прохождения, причины и места гибели,
/// производительность, ошибки, оценки) и автоматические рекомендации для следующего обновления.
/// Используется панелью Tools → Release → Analytics Dashboard.
/// </summary>
public static class AnalyticsAggregator
{
    public const int MinStartsForConclusions = 10;

    public static AnalyticsEvent ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;
        try
        {
            var e = JsonUtility.FromJson<AnalyticsEvent>(line);
            return e != null && !string.IsNullOrEmpty(e.name) ? e : null;
        }
        catch (ArgumentException)
        {
            return null; // повреждённая строка (например, при аварийном завершении записи)
        }
    }

    public static AnalyticsSummary Summarize(IEnumerable<AnalyticsEvent> source, bool includeEditor = true)
    {
        var s = new AnalyticsSummary();
        var sessions = new HashSet<string>();
        var installs = new HashSet<string>();
        float sessionSeconds = 0f;
        int sessionEnds = 0;
        float victoryTime = 0f, deathTime = 0f;
        float fpsSum = 0f;
        int lowFps = 0;
        float ratingSum = 0f;

        foreach (AnalyticsEvent e in source)
        {
            if (e == null) continue;
            if (!includeEditor && e.platform != null && e.platform.Contains("Editor")) continue;

            s.Events++;
            if (!string.IsNullOrEmpty(e.session)) sessions.Add(e.session);
            if (!string.IsNullOrEmpty(e.install)) installs.Add(e.install);

            switch (e.name)
            {
                case "session_start":
                    Inc(s.Versions, e.version);
                    Inc(s.Platforms, e.platform);
                    break;

                case "session_end":
                    sessionSeconds += e.GetFloat("duration");
                    sessionEnds++;
                    break;

                case "level_start":
                    s.LevelStarts++;
                    s.Funnel[0]++;
                    break;

                case "progress":
                    int percent = e.GetInt("percent");
                    if (percent == 25) s.Funnel[1]++;
                    else if (percent == 50) s.Funnel[2]++;
                    else if (percent == 75) s.Funnel[3]++;
                    break;

                case "level_end":
                    string result = e.Get("result");
                    if (result == "victory")
                    {
                        s.Victories++;
                        s.Funnel[4]++;
                        victoryTime += e.GetFloat("time");
                        int stars = Mathf.Clamp(e.GetInt("stars"), 0, 3);
                        s.Stars[stars]++;
                    }
                    else if (result == "abandoned")
                    {
                        s.Abandoned++;
                    }
                    break;

                case "death":
                    s.Deaths++;
                    deathTime += e.GetFloat("time");
                    Inc(s.DeathCauses, e.Get("cause", "unknown"));
                    s.DeathPositions.Add(new Vector3(e.GetFloat("x"), e.GetFloat("y"), e.GetFloat("z")));
                    break;

                case "item_used":
                    Inc(s.ItemsUsed, e.Get("item"));
                    break;

                case "bonus_found":
                    Inc(s.BonusesFound, e.Get("item"));
                    break;

                case "perf":
                    float fps = e.GetFloat("fps");
                    fpsSum += fps;
                    s.PerfSamples++;
                    if (fps < 30f) lowFps++;
                    break;

                case "error":
                    Inc(s.Errors, e.Get("message", "?"));
                    break;

                case "feedback":
                    s.FeedbackCount++;
                    ratingSum += e.GetFloat("rating");
                    Inc(s.FeedbackCategories, e.Get("category"));
                    break;
            }
        }

        s.Sessions = sessions.Count;
        s.Installs = installs.Count;
        s.AvgSessionMinutes = sessionEnds > 0 ? sessionSeconds / sessionEnds / 60f : 0f;
        s.CompletionRate = s.LevelStarts > 0 ? (float)s.Victories / s.LevelStarts : 0f;
        s.AvgVictoryTime = s.Victories > 0 ? victoryTime / s.Victories : 0f;
        s.AvgDeathTime = s.Deaths > 0 ? deathTime / s.Deaths : 0f;
        s.AvgFps = s.PerfSamples > 0 ? fpsSum / s.PerfSamples : 0f;
        s.LowFpsShare = s.PerfSamples > 0 ? (float)lowFps / s.PerfSamples : 0f;
        s.AvgRating = s.FeedbackCount > 0 ? ratingSum / s.FeedbackCount : 0f;

        s.Recommendations.AddRange(Recommend(s, RemoteConfig.Current));
        return s;
    }

    /// <summary>Правила, превращающие статистику в конкретные задачи на обновление.</summary>
    public static List<string> Recommend(AnalyticsSummary s, RemoteConfigData config)
    {
        var list = new List<string>();
        if (s.LevelStarts == 0)
        {
            list.Add("Нет данных о прохождении уровня. Соберите статистику плейтестов (игроки должны дать согласие на сбор).");
            return list;
        }

        if (s.LevelStarts < MinStartsForConclusions)
            list.Add($"Мало данных ({s.LevelStarts} запусков уровня из нужных {MinStartsForConclusions}+) — выводы ниже предварительные.");

        if (s.CompletionRate < 0.3f)
            list.Add($"Уровень проходят только {s.CompletionRate:P0} игроков — он слишком сложный. Смягчите баланс через remote_config.json.");
        else if (s.CompletionRate > 0.9f && s.LevelStarts >= MinStartsForConclusions)
            list.Add($"Уровень проходят {s.CompletionRate:P0} игроков — возможно, он слишком простой. Добавьте препятствия или усложните звёзды.");

        if (s.Deaths > 0)
        {
            float barrel = Share(s.DeathCauses, "barrel", s.Deaths) + Share(s.DeathCauses, "barrel_splash", s.Deaths);
            float obstacle = Share(s.DeathCauses, "obstacle", s.Deaths);
            float fall = Share(s.DeathCauses, "fall", s.Deaths);

            if (barrel > 0.4f)
                list.Add($"Бочки — причина {barrel:P0} гибелей. Увеличьте barrelExplodeSpeed (сейчас {config.barrelExplodeSpeed:0.#} м/с) на 1–2 м/с или сделайте предупреждение о скорости заметнее.");
            if (obstacle > 0.4f)
                list.Add($"Подвижные препятствия — причина {obstacle:P0} гибелей. Уменьшите obstacleDamageMultiplier (сейчас {config.obstacleDamageMultiplier:0.##}) до 0.7–0.8.");
            if (fall > 0.2f)
                list.Add($"Падения за пределы уровня — {fall:P0} гибелей. Проверьте карту мест гибели и добавьте ограждения.");
        }

        int worstStep = -1;
        float worstDrop = 0f;
        for (int i = 1; i < s.Funnel.Length; i++)
        {
            if (s.Funnel[i - 1] == 0) continue;
            float drop = 1f - (float)s.Funnel[i] / s.Funnel[i - 1];
            if (drop > worstDrop)
            {
                worstDrop = drop;
                worstStep = i;
            }
        }
        if (worstStep > 0 && worstDrop > 0.4f)
            list.Add($"Наибольший отток в воронке: «{AnalyticsSummary.FunnelNames[worstStep - 1]}» → «{AnalyticsSummary.FunnelNames[worstStep]}» ({worstDrop:P0} игроков). Изучите этот участок уровня.");

        if (s.LevelStarts > 0 && (float)s.Abandoned / s.LevelStarts > 0.3f)
            list.Add($"{(float)s.Abandoned / s.LevelStarts:P0} попыток брошены без завершения. Проверьте, понятна ли цель уровня, и сократите долгие участки.");

        if (s.Victories >= 5)
        {
            float three = (float)s.Stars[3] / s.Victories;
            if (three > 0.8f)
                list.Add($"{three:P0} побед на три звезды — норматив слишком лёгкий. Уменьшите threeStarTime (сейчас {config.threeStarTime:0} с).");
            else if (three < 0.1f)
                list.Add($"Только {three:P0} побед на три звезды — норматив слишком строгий. Увеличьте threeStarTime (сейчас {config.threeStarTime:0} с).");
        }

        if (s.Deaths >= 5 && !s.ItemsUsed.ContainsKey(ItemType.Medkit.ToString()))
            list.Add("Игроки гибнут, но не используют аптечки — добавьте подсказку о клавише 1 при низком здоровье или обучение.");

        if (s.PerfSamples > 0 && (s.AvgFps < 40f || s.LowFpsShare > 0.2f))
            list.Add($"Производительность: средний FPS {s.AvgFps:0}, доля замеров ниже 30 FPS — {s.LowFpsShare:P0}. Снизьте качество по умолчанию для слабых устройств.");

        if (s.Errors.Count > 0)
        {
            KeyValuePair<string, int> top = s.Errors.OrderByDescending(p => p.Value).First();
            list.Add($"Обнаружено {s.Errors.Count} уникальных ошибок. Самая частая ({top.Value} раз): «{top.Key}». Исправить в первую очередь.");
        }

        if (s.FeedbackCount >= 3 && s.AvgRating < 3.5f)
            list.Add($"Средняя оценка игроков {s.AvgRating:0.0}/5 — прочитайте отзывы с низкой оценкой в GitHub Issues и выделите повторяющиеся жалобы.");

        if (list.Count == 0 || (list.Count == 1 && s.LevelStarts < MinStartsForConclusions))
            list.Add("Критичных проблем не выявлено. Продолжайте сбор данных после следующего обновления.");

        return list;
    }

    private static float Share(Dictionary<string, int> counts, string key, int total) =>
        total > 0 && counts.TryGetValue(key, out int v) ? (float)v / total : 0f;

    private static void Inc(Dictionary<string, int> dict, string key)
    {
        key = string.IsNullOrEmpty(key) ? "unknown" : key;
        dict.TryGetValue(key, out int v);
        dict[key] = v + 1;
    }

    /// <summary>Отчёт в формате Markdown.</summary>
    public static string ToMarkdown(AnalyticsSummary s)
    {
        var sb = new StringBuilder();
        CultureInfo ci = CultureInfo.InvariantCulture;
        sb.AppendLine("# Отчёт аналитики");
        sb.AppendLine();
        sb.AppendLine($"Сформирован: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## Аудитория");
        sb.AppendLine($"- Установок: {s.Installs}, сессий: {s.Sessions}, событий: {s.Events}");
        sb.AppendLine($"- Средняя длительность сессии: {s.AvgSessionMinutes.ToString("0.0", ci)} мин");
        sb.AppendLine($"- Версии: {Join(s.Versions)}");
        sb.AppendLine($"- Платформы: {Join(s.Platforms)}");
        sb.AppendLine();
        sb.AppendLine("## Прохождение");
        sb.AppendLine($"- Запусков уровня: {s.LevelStarts}, побед: {s.Victories}, гибелей: {s.Deaths}, брошено: {s.Abandoned}");
        sb.AppendLine($"- Доля прохождения: {(s.CompletionRate * 100f).ToString("0", ci)} %");
        sb.AppendLine($"- Среднее время победы: {s.AvgVictoryTime.ToString("0", ci)} с; среднее время до гибели: {s.AvgDeathTime.ToString("0", ci)} с");
        sb.AppendLine($"- Звёзды: 1★ — {s.Stars[1]}, 2★ — {s.Stars[2]}, 3★ — {s.Stars[3]}");
        sb.AppendLine();
        sb.AppendLine("### Воронка");
        for (int i = 0; i < s.Funnel.Length; i++)
        {
            float pct = s.Funnel[0] > 0 ? s.Funnel[i] * 100f / s.Funnel[0] : 0f;
            sb.AppendLine($"{i + 1}. {AnalyticsSummary.FunnelNames[i]}: {s.Funnel[i]} ({pct.ToString("0", ci)} %)");
        }
        sb.AppendLine();
        sb.AppendLine("## Причины гибели");
        sb.AppendLine(s.DeathCauses.Count == 0 ? "- нет данных" : "- " + Join(s.DeathCauses));
        sb.AppendLine();
        sb.AppendLine("## Предметы и бонусы");
        sb.AppendLine($"- Использовано: {Join(s.ItemsUsed)}");
        sb.AppendLine($"- Найдено бонусов: {Join(s.BonusesFound)}");
        sb.AppendLine();
        sb.AppendLine("## Производительность");
        sb.AppendLine($"- Замеров: {s.PerfSamples}, средний FPS: {s.AvgFps.ToString("0", ci)}, ниже 30 FPS: {(s.LowFpsShare * 100f).ToString("0", ci)} %");
        sb.AppendLine();
        sb.AppendLine("## Ошибки");
        if (s.Errors.Count == 0) sb.AppendLine("- не обнаружено");
        foreach (KeyValuePair<string, int> e in s.Errors.OrderByDescending(p => p.Value).Take(10))
            sb.AppendLine($"- ({e.Value}) {e.Key}");
        sb.AppendLine();
        sb.AppendLine("## Отзывы");
        sb.AppendLine($"- Отзывов: {s.FeedbackCount}, средняя оценка: {s.AvgRating.ToString("0.0", ci)}/5");
        sb.AppendLine($"- Категории: {Join(s.FeedbackCategories)}");
        sb.AppendLine();
        sb.AppendLine("## Рекомендации для обновления");
        foreach (string r in s.Recommendations)
            sb.AppendLine("- " + r);
        return sb.ToString();
    }

    private static string Join(Dictionary<string, int> dict) =>
        dict.Count == 0 ? "нет данных" : string.Join(", ", dict.OrderByDescending(p => p.Value).Select(p => $"{p.Key}: {p.Value}"));
}
