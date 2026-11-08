using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>Тесты сервисов выпуска и мониторинга: версии, удалённый конфиг, аналитика, отзывы, ошибки.</summary>
public class LiveOpsTests
{
    [TearDown]
    public void TearDown() => RemoteConfig.ResetToDefaults();

    // ───── Версии ─────

    [TestCase("1.2.0", "1.10.0", -1)]
    [TestCase("1.2.0", "1.2.0", 0)]
    [TestCase("2.0", "1.9.9", 1)]
    [TestCase("v1.3.0", "1.2.9", 1)]
    [TestCase("1.2.0-beta", "1.2.0", 0)]
    [TestCase("", "0.0.1", -1)]
    public void VersionUtil_Compare(string a, string b, int expected)
    {
        Assert.AreEqual(expected, VersionUtil.Compare(a, b));
    }

    [Test]
    public void VersionUtil_IsNewer()
    {
        Assert.IsTrue(VersionUtil.IsNewer("1.2.1", "1.2.0"));
        Assert.IsFalse(VersionUtil.IsNewer("1.2.0", "1.2.0"));
        Assert.IsFalse(VersionUtil.IsNewer("1.1.9", "1.2.0"));
    }

    // ───── Удалённый конфиг ─────

    [Test]
    public void RemoteConfig_DefaultsMatchBuiltInBalance()
    {
        var d = new RemoteConfigData();
        Assert.AreEqual(7f, d.barrelExplodeSpeed, 1e-4f);
        Assert.AreEqual(1f, d.obstacleDamageMultiplier, 1e-4f);
        Assert.AreEqual(90f, d.threeStarTime, 1e-4f);
        Assert.AreEqual(150f, d.twoStarTime, 1e-4f);
    }

    [Test]
    public void RemoteConfig_ParsesPartialJson_KeepingDefaults()
    {
        RemoteConfigData d = RemoteConfig.Parse("{\"barrelExplodeSpeed\": 9.5, \"newsTitle\": \"Привет\"}");
        Assert.IsNotNull(d);
        Assert.AreEqual(9.5f, d.barrelExplodeSpeed, 1e-4f);
        Assert.AreEqual("Привет", d.newsTitle);
        Assert.AreEqual(90f, d.threeStarTime, 1e-4f, "Отсутствующие поля остаются по умолчанию");
    }

    [Test]
    public void RemoteConfig_ClampsDangerousValues()
    {
        RemoteConfigData d = RemoteConfig.Parse(
            "{\"barrelExplodeSpeed\": 0, \"obstacleDamageMultiplier\": 100, \"threeStarTime\": 200, \"twoStarTime\": 50}");
        Assert.AreEqual(2f, d.barrelExplodeSpeed, 1e-4f);
        Assert.AreEqual(5f, d.obstacleDamageMultiplier, 1e-4f);
        Assert.GreaterOrEqual(d.twoStarTime, d.threeStarTime, "Норматив двух звёзд не может быть строже трёх");
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not json at all")]
    public void RemoteConfig_InvalidJson_ReturnsNull(string json)
    {
        Assert.IsNull(RemoteConfig.Parse(json));
    }

    [Test]
    public void RemoteConfig_RepositoryFile_IsValid()
    {
        string path = System.IO.Path.Combine(Application.dataPath, "..", "Config", "remote_config.json");
        Assert.IsTrue(System.IO.File.Exists(path), "Config/remote_config.json должен лежать в репозитории");
        RemoteConfigData d = RemoteConfig.Parse(System.IO.File.ReadAllText(path));
        Assert.IsNotNull(d, "Файл конфига должен быть корректным JSON");
        Assert.Greater(d.configVersion, 0);
    }

    [Test]
    public void RemoteConfig_Apply_RaisesChanged()
    {
        int changed = 0;
        RemoteConfig.Changed += () => changed++;
        RemoteConfig.Apply(new RemoteConfigData { obstacleDamageMultiplier = 0.5f }, "test");
        Assert.AreEqual(1, changed);
        Assert.AreEqual("test", RemoteConfig.Source);
        Assert.AreEqual(0.5f, RemoteConfig.Current.obstacleDamageMultiplier, 1e-4f);
    }

    // ───── Аналитика ─────

    private static AnalyticsEvent E(string name, string session, params string[] kv)
    {
        var e = new AnalyticsEvent { name = name, session = session, install = "i-" + session, platform = "Test", version = "1.2.0" };
        for (int i = 0; i + 1 < kv.Length; i += 2)
            e.data.Add(new AnalyticsParam(kv[i], kv[i + 1]));
        return e;
    }

    [Test]
    public void AnalyticsEvent_RoundTripsThroughJson()
    {
        AnalyticsEvent e = E("death", "s1", "cause", "barrel", "x", "1.5");
        AnalyticsEvent back = AnalyticsAggregator.ParseLine(JsonUtility.ToJson(e));
        Assert.IsNotNull(back);
        Assert.AreEqual("death", back.name);
        Assert.AreEqual("barrel", back.Get("cause"));
        Assert.AreEqual(1.5f, back.GetFloat("x"), 1e-4f);
        Assert.AreEqual(0, back.GetInt("missing"));
    }

    [TestCase("")]
    [TestCase("{broken")]
    [TestCase("{\"name\":\"\"}")]
    public void AnalyticsAggregator_SkipsBrokenLines(string line)
    {
        Assert.IsNull(AnalyticsAggregator.ParseLine(line));
    }

    [Test]
    public void AnalyticsAggregator_ComputesCoreMetrics()
    {
        var events = new List<AnalyticsEvent>
        {
            E("session_start", "a"), E("level_start", "a"), E("progress", "a", "percent", "25"),
            E("death", "a", "cause", "barrel", "x", "1", "y", "0", "z", "40", "time", "30"),
            E("level_end", "a", "result", "death"),
            E("level_start", "a"), E("progress", "a", "percent", "25"), E("progress", "a", "percent", "50"),
            E("progress", "a", "percent", "75"), E("level_end", "a", "result", "victory", "time", "80", "stars", "3"),
            E("session_end", "a", "duration", "120"),
            E("session_start", "b"), E("level_start", "b"), E("level_end", "b", "result", "abandoned"),
            E("perf", "b", "fps", "20"), E("perf", "b", "fps", "60"),
            E("feedback", "b", "rating", "4", "category", "bug"),
            E("error", "b", "message", "NullReference"), E("error", "b", "message", "NullReference")
        };

        AnalyticsSummary s = AnalyticsAggregator.Summarize(events);

        Assert.AreEqual(2, s.Sessions);
        Assert.AreEqual(3, s.LevelStarts);
        Assert.AreEqual(1, s.Victories);
        Assert.AreEqual(1, s.Deaths);
        Assert.AreEqual(1, s.Abandoned);
        Assert.AreEqual(1f / 3f, s.CompletionRate, 1e-3f);
        Assert.AreEqual(new[] { 3, 2, 1, 1, 1 }, s.Funnel);
        Assert.AreEqual(1, s.DeathCauses["barrel"]);
        Assert.AreEqual(40f, s.DeathPositions[0].z, 1e-4f);
        Assert.AreEqual(1, s.Stars[3]);
        Assert.AreEqual(2f, s.AvgSessionMinutes, 1e-3f);
        Assert.AreEqual(40f, s.AvgFps, 1e-3f);
        Assert.AreEqual(0.5f, s.LowFpsShare, 1e-3f);
        Assert.AreEqual(4f, s.AvgRating, 1e-3f);
        Assert.AreEqual(2, s.Errors["NullReference"]);
        Assert.Greater(s.Recommendations.Count, 0);
    }

    [Test]
    public void AnalyticsAggregator_ExcludesEditorSessionsOnRequest()
    {
        AnalyticsEvent editor = E("level_start", "e");
        editor.platform = "WindowsEditor";
        var events = new List<AnalyticsEvent> { editor, E("level_start", "p") };

        Assert.AreEqual(2, AnalyticsAggregator.Summarize(events, true).LevelStarts);
        Assert.AreEqual(1, AnalyticsAggregator.Summarize(events, false).LevelStarts);
    }

    [Test]
    public void Recommendations_FlagBarrelsAsMainDeathCause()
    {
        var events = new List<AnalyticsEvent>();
        for (int i = 0; i < 12; i++)
        {
            events.Add(E("level_start", "s" + i));
            events.Add(E("death", "s" + i, "cause", i < 9 ? "barrel" : "obstacle"));
            events.Add(E("level_end", "s" + i, "result", "death"));
        }

        AnalyticsSummary s = AnalyticsAggregator.Summarize(events);

        Assert.IsTrue(s.Recommendations.Any(r => r.Contains("barrelExplodeSpeed")),
            "75 % гибелей от бочек должны дать рекомендацию изменить порог взрыва");
        Assert.IsTrue(s.Recommendations.Any(r => r.Contains("слишком сложный")),
            "0 % прохождений — уровень слишком сложный");
    }

    [Test]
    public void Recommendations_WarnAboutSmallSample()
    {
        var events = new List<AnalyticsEvent> { E("level_start", "x"), E("level_end", "x", "result", "victory", "stars", "2") };
        AnalyticsSummary s = AnalyticsAggregator.Summarize(events);
        Assert.IsTrue(s.Recommendations.Any(r => r.Contains("Мало данных")));
    }

    [Test]
    public void Markdown_ContainsAllSections()
    {
        string md = AnalyticsAggregator.ToMarkdown(AnalyticsAggregator.Summarize(new List<AnalyticsEvent>()));
        foreach (string section in new[] { "Аудитория", "Прохождение", "Воронка", "Причины гибели", "Производительность", "Ошибки", "Отзывы", "Рекомендации" })
            StringAssert.Contains(section, md);
    }

    [Test]
    public void Analytics_DoesNotTrackWithoutConsent()
    {
        int previous = PlayerPrefs.GetInt(AnalyticsService.ConsentKey, -1);
        try
        {
            PlayerPrefs.SetInt(AnalyticsService.ConsentKey, 0);
            int tracked = 0;
            Action<AnalyticsEvent> handler = e => tracked++;
            AnalyticsService.EventTracked += handler;
            AnalyticsService.Track("level_start");
            AnalyticsService.EventTracked -= handler;
            Assert.AreEqual(0, tracked, "Без согласия события не записываются");
        }
        finally
        {
            if (previous < 0) PlayerPrefs.DeleteKey(AnalyticsService.ConsentKey);
            else PlayerPrefs.SetInt(AnalyticsService.ConsentKey, previous);
        }
    }

    // ───── Отзывы и ошибки ─────

    [Test]
    public void Feedback_Create_ClampsValues()
    {
        FeedbackEntry f = FeedbackService.Create(9, 99, "  текст  ", false);
        Assert.AreEqual(5, f.rating);
        Assert.AreEqual("idea", f.category);
        Assert.AreEqual("текст", f.text);
        Assert.AreEqual("", f.tech);
    }

    [Test]
    public void Feedback_IssueUrl_IsEncodedAndLabeled()
    {
        FeedbackEntry f = FeedbackService.Create(2, 1, "Бочка взорвалась & игра зависла", true);
        string url = FeedbackService.BuildIssueUrl(f, "owner/repo");

        StringAssert.StartsWith("https://github.com/owner/repo/issues/new?", url);
        StringAssert.Contains("labels=bug", url);
        Assert.IsFalse(url.Contains(" "), "Пробелы должны быть закодированы");
        Assert.IsFalse(url.Contains("& "), "Спецсимволы текста должны быть закодированы");
        StringAssert.Contains(Uri.EscapeDataString("[Ошибка]"), url);
    }

    [Test]
    public void CrashReporter_DeduplicatesSameError()
    {
        string message = "TestError-" + Guid.NewGuid();
        int unique = CrashReporter.UniqueErrors;
        int total = CrashReporter.TotalErrors;

        CrashReporter.Report(message, "at Foo.Bar()\nat Baz()", "Exception");
        CrashReporter.Report(message, "at Foo.Bar()\nat Other()", "Exception");

        Assert.AreEqual(unique + 1, CrashReporter.UniqueErrors);
        Assert.AreEqual(total + 2, CrashReporter.TotalErrors);
        Assert.IsTrue(CrashReporter.RecentErrors.Any(e => e.Contains(message)));
    }

    [Test]
    public void BuildInfo_HasVersion()
    {
        Assert.IsFalse(string.IsNullOrEmpty(BuildInfo.Current.version));
        StringAssert.StartsWith("v", BuildInfo.Current.ShortLabel);
    }
}
