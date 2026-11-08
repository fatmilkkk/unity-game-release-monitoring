using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Отзыв игрока.</summary>
[Serializable]
public class FeedbackEntry
{
    public string time;
    public string version;
    public string platform;
    public int rating;          // 1..5
    public string category;    // feedback / bug / idea
    public string text;
    public string tech;        // техническая информация (по желанию игрока)
}

/// <summary>
/// Отзывы игроков: сохраняются локально (feedback.jsonl), учитываются в аналитике (оценка и категория)
/// и могут быть отправлены разработчику — открывается страница нового обращения GitHub Issues
/// с заполненным заголовком и текстом. Серверная часть не требуется.
/// </summary>
public static class FeedbackService
{
    public static readonly string[] Categories = { "feedback", "bug", "idea" };
    public static readonly string[] CategoryNames = { "Отзыв", "Ошибка", "Предложение" };

    public static string FeedbackFile => Path.Combine(Application.persistentDataPath, "feedback", "feedback.jsonl");

    public static FeedbackEntry Create(int rating, int categoryIndex, string text, bool includeTech)
    {
        categoryIndex = Mathf.Clamp(categoryIndex, 0, Categories.Length - 1);
        return new FeedbackEntry
        {
            time = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            version = BuildInfo.Current.version,
            platform = Application.platform.ToString(),
            rating = Mathf.Clamp(rating, 1, 5),
            category = Categories[categoryIndex],
            text = (text ?? "").Trim(),
            tech = includeTech ? CollectTechInfo() : ""
        };
    }

    /// <summary>Сохранить отзыв локально и записать событие аналитики.</summary>
    public static void Save(FeedbackEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FeedbackFile));
            File.AppendAllText(FeedbackFile, JsonUtility.ToJson(entry) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Feedback] Не удалось сохранить отзыв: " + ex.Message);
        }

        AnalyticsService.Track("feedback",
            "rating", entry.rating.ToString(CultureInfo.InvariantCulture),
            "category", entry.category,
            "length", (entry.text ?? "").Length.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Ссылка на создание обращения в GitHub Issues с заполненным текстом.</summary>
    public static string BuildIssueUrl(FeedbackEntry entry, string repo)
    {
        string categoryName = CategoryNames[Mathf.Max(0, Array.IndexOf(Categories, entry.category))];
        string firstLine = string.IsNullOrEmpty(entry.text) ? "без текста" : entry.text.Split('\n')[0];
        if (firstLine.Length > 60) firstLine = firstLine.Substring(0, 60) + "…";

        string title = $"[{categoryName}] {firstLine}";
        var body = new StringBuilder();
        body.AppendLine($"**Оценка:** {new string('★', entry.rating)}{new string('☆', 5 - entry.rating)} ({entry.rating}/5)");
        body.AppendLine($"**Категория:** {categoryName}");
        body.AppendLine($"**Версия:** {entry.version} • {entry.platform}");
        body.AppendLine();
        body.AppendLine(string.IsNullOrEmpty(entry.text) ? "_(без текста)_" : entry.text);
        if (!string.IsNullOrEmpty(entry.tech))
        {
            body.AppendLine();
            body.AppendLine("<details><summary>Техническая информация</summary>");
            body.AppendLine();
            body.AppendLine("```");
            body.AppendLine(entry.tech);
            body.AppendLine("```");
            body.AppendLine("</details>");
        }

        string label = entry.category == "bug" ? "bug" : entry.category == "idea" ? "enhancement" : "feedback";
        string bodyText = body.ToString();
        if (bodyText.Length > 4000) bodyText = bodyText.Substring(0, 4000) + "…";

        return $"https://github.com/{repo}/issues/new" +
               $"?labels={Uri.EscapeDataString(label)}" +
               $"&title={Uri.EscapeDataString(title)}" +
               $"&body={Uri.EscapeDataString(bodyText)}";
    }

    public static string CollectTechInfo()
    {
        var sb = new StringBuilder();
        BuildInfo info = BuildInfo.Current;
        sb.AppendLine($"Version: {info.version} build {info.buildNumber} {info.commit}");
        sb.AppendLine($"Platform: {Application.platform}");
        sb.AppendLine($"OS: {SystemInfo.operatingSystem}");
        sb.AppendLine($"Device: {SystemInfo.deviceModel}");
        sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
        sb.AppendLine($"RAM: {SystemInfo.systemMemorySize} MB");
        sb.AppendLine($"Screen: {Screen.width}x{Screen.height}");
        sb.AppendLine($"Quality: {GameSettings.QualityNames[GameSettings.Quality]}, FPS limit: {GameSettings.FpsLimitNames[GameSettings.FpsLimitIndex]}");
        if (CrashReporter.RecentErrors.Count > 0)
        {
            sb.AppendLine("Recent errors:");
            foreach (string e in CrashReporter.RecentErrors)
                sb.AppendLine("  " + e);
        }
        return sb.ToString().TrimEnd();
    }
}
