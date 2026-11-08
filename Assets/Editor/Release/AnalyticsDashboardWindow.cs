using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// Панель анализа статистики (Tools → Release → Analytics Dashboard).
/// Читает события аналитики из папки данных игры на этом компьютере и из папки AnalyticsData проекта
/// (туда складываются файлы, присланные тестировщиками или выгруженные с сервера).
/// Показывает метрики, воронку, причины гибели, производительность, ошибки, оценки,
/// рекомендации для обновления и карту мест гибели в окне Scene.
/// </summary>
public class AnalyticsDashboardWindow : EditorWindow
{
    private const string ImportFolder = "AnalyticsData";
    private const string DemoFile = "AnalyticsData/demo_data.jsonl";

    private AnalyticsSummary _summary;
    private readonly List<string> _feedbackTexts = new List<string>();
    private int _filesRead;
    private int _brokenLines;
    private bool _hasDemo;
    private bool _includeEditor = true;
    private bool _showDeathMap = true;
    private Vector2 _scroll;

    [MenuItem("Tools/Release/Analytics Dashboard", false, 100)]
    public static void Open()
    {
        var w = GetWindow<AnalyticsDashboardWindow>("Аналитика");
        w.minSize = new Vector2(520f, 400f);
        w.Refresh();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawDeathMap;
        Refresh();
    }

    private void OnDisable() => SceneView.duringSceneGui -= DrawDeathMap;

    // ───── Загрузка данных ─────

    private IEnumerable<string> DataFiles()
    {
        var files = new List<string>();
        if (Directory.Exists(AnalyticsService.DataFolder))
            files.AddRange(Directory.GetFiles(AnalyticsService.DataFolder, "*.jsonl"));
        if (Directory.Exists(ImportFolder))
            files.AddRange(Directory.GetFiles(ImportFolder, "*.jsonl", SearchOption.AllDirectories));
        return files;
    }

    private void Refresh()
    {
        var events = new List<AnalyticsEvent>();
        _filesRead = 0;
        _brokenLines = 0;
        foreach (string file in DataFiles())
        {
            _filesRead++;
            foreach (string line in File.ReadAllLines(file))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                AnalyticsEvent e = AnalyticsAggregator.ParseLine(line);
                if (e != null) events.Add(e);
                else _brokenLines++;
            }
        }
        _hasDemo = File.Exists(DemoFile);
        _summary = AnalyticsAggregator.Summarize(events, _includeEditor);

        _feedbackTexts.Clear();
        if (File.Exists(FeedbackService.FeedbackFile))
            foreach (string line in File.ReadAllLines(FeedbackService.FeedbackFile).Reverse().Take(10))
            {
                try
                {
                    var f = JsonUtility.FromJson<FeedbackEntry>(line);
                    if (f != null)
                        _feedbackTexts.Add($"{new string('★', f.rating)} [{f.category}] {f.text}");
                }
                catch (ArgumentException) { }
            }

        SceneView.RepaintAll();
        Repaint();
    }

    // ───── Интерфейс ─────

    private void OnGUI()
    {
        DrawToolbar();
        if (_summary == null) return;

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (_hasDemo)
            EditorGUILayout.HelpBox("Загружены ДЕМОНСТРАЦИОННЫЕ данные (AnalyticsData/demo_data.jsonl). " +
                                    "Это сгенерированные события для проверки панели, а не статистика реальных игроков. " +
                                    "Удалите их перед анализом настоящих данных.", MessageType.Warning);

        EditorGUILayout.LabelField($"Файлов: {_filesRead}, событий: {_summary.Events}" +
                                   (_brokenLines > 0 ? $", повреждённых строк: {_brokenLines}" : ""), EditorStyles.miniLabel);

        Section("Аудитория");
        Row("Установок / сессий", $"{_summary.Installs} / {_summary.Sessions}");
        Row("Средняя сессия", $"{_summary.AvgSessionMinutes:0.0} мин");
        Row("Версии", Join(_summary.Versions));
        Row("Платформы", Join(_summary.Platforms));

        Section("Прохождение уровня");
        Row("Запусков / побед / гибелей / брошено",
            $"{_summary.LevelStarts} / {_summary.Victories} / {_summary.Deaths} / {_summary.Abandoned}");
        Row("Доля прохождения", $"{_summary.CompletionRate:P0}");
        Row("Среднее время победы", $"{_summary.AvgVictoryTime:0} с");
        Row("Звёзды 1★ / 2★ / 3★", $"{_summary.Stars[1]} / {_summary.Stars[2]} / {_summary.Stars[3]}");

        Section("Воронка");
        int top = Mathf.Max(1, _summary.Funnel[0]);
        for (int i = 0; i < _summary.Funnel.Length; i++)
            Bar(AnalyticsSummary.FunnelNames[i], _summary.Funnel[i], top, new Color(0.3f, 0.6f, 1f));

        Section("Причины гибели");
        BarsFor(_summary.DeathCauses, new Color(1f, 0.35f, 0.3f));
        _showDeathMap = EditorGUILayout.ToggleLeft($"Показать карту мест гибели в окне Scene ({_summary.DeathPositions.Count} точек)", _showDeathMap);

        Section("Предметы и бонусы");
        BarsFor(_summary.ItemsUsed, new Color(1f, 0.8f, 0.3f), "Использовано: ");
        BarsFor(_summary.BonusesFound, new Color(0.3f, 1f, 0.8f), "Найдено: ");

        Section("Производительность");
        Row("Замеров / средний FPS", $"{_summary.PerfSamples} / {_summary.AvgFps:0}");
        Row("Доля замеров ниже 30 FPS", $"{_summary.LowFpsShare:P0}");

        Section("Ошибки");
        if (_summary.Errors.Count == 0) EditorGUILayout.LabelField("Не обнаружено");
        foreach (KeyValuePair<string, int> e in _summary.Errors.OrderByDescending(p => p.Value).Take(8))
            EditorGUILayout.LabelField($"({e.Value}) {e.Key}", EditorStyles.wordWrappedLabel);

        Section("Отзывы");
        Row("Отзывов / средняя оценка", $"{_summary.FeedbackCount} / {_summary.AvgRating:0.0}");
        Row("Категории", Join(_summary.FeedbackCategories));
        foreach (string f in _feedbackTexts)
            EditorGUILayout.LabelField(f, EditorStyles.wordWrappedMiniLabel);

        Section("Рекомендации для обновления");
        foreach (string r in _summary.Recommendations)
            EditorGUILayout.HelpBox(r, MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Обновить", EditorStyles.toolbarButton)) Refresh();
        bool include = GUILayout.Toggle(_includeEditor, "Сессии из редактора", EditorStyles.toolbarButton);
        if (include != _includeEditor)
        {
            _includeEditor = include;
            Refresh();
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Папка данных", EditorStyles.toolbarButton))
        {
            Directory.CreateDirectory(AnalyticsService.DataFolder);
            EditorUtility.RevealInFinder(AnalyticsService.DataFolder);
        }
        if (GUILayout.Button("Папка импорта", EditorStyles.toolbarButton))
        {
            Directory.CreateDirectory(ImportFolder);
            EditorUtility.RevealInFinder(Path.GetFullPath(ImportFolder));
        }
        if (GUILayout.Button("Отчёт .md", EditorStyles.toolbarButton)) ExportReport();
        if (GUILayout.Button(_hasDemo ? "Удалить демо" : "Демо-данные", EditorStyles.toolbarButton))
        {
            if (_hasDemo) File.Delete(DemoFile);
            else GenerateDemoData();
            Refresh();
        }
        EditorGUILayout.EndHorizontal();
    }

    private static void Section(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private static void Row(string label, string value) => EditorGUILayout.LabelField(label, value);

    private static void Bar(string label, int value, int max, Color color)
    {
        Rect r = EditorGUILayout.GetControlRect(false, 18f);
        Rect labelRect = new Rect(r.x, r.y, 170f, r.height);
        Rect barRect = new Rect(r.x + 175f, r.y + 2f, Mathf.Max(0f, r.width - 230f), r.height - 4f);
        EditorGUI.LabelField(labelRect, label);
        EditorGUI.DrawRect(barRect, new Color(0f, 0f, 0f, 0.2f));
        EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * (max > 0 ? (float)value / max : 0f), barRect.height), color);
        EditorGUI.LabelField(new Rect(barRect.xMax + 5f, r.y, 50f, r.height), value.ToString());
    }

    private static void BarsFor(Dictionary<string, int> dict, Color color, string prefix = "")
    {
        if (dict.Count == 0)
        {
            EditorGUILayout.LabelField(prefix + "нет данных");
            return;
        }
        int max = dict.Values.Max();
        foreach (KeyValuePair<string, int> p in dict.OrderByDescending(p => p.Value))
            Bar(prefix + p.Key, p.Value, max, color);
    }

    private static string Join(Dictionary<string, int> dict) =>
        dict.Count == 0 ? "—" : string.Join(", ", dict.OrderByDescending(p => p.Value).Select(p => $"{p.Key} ({p.Value})"));

    // ───── Карта мест гибели ─────

    private void DrawDeathMap(SceneView view)
    {
        if (!_showDeathMap || _summary == null || _summary.DeathPositions.Count == 0)
            return;

        List<Vector3> points = _summary.DeathPositions;
        foreach (Vector3 p in points)
        {
            int neighbours = points.Count(q => (q - p).sqrMagnitude < 4f);
            float heat = Mathf.Clamp01(neighbours / 8f);
            Handles.color = Color.Lerp(new Color(1f, 0.9f, 0.2f, 0.25f), new Color(1f, 0.1f, 0.1f, 0.5f), heat);
            Vector3 ground = new Vector3(p.x, Mathf.Max(0.05f, p.y - 0.45f), p.z);
            Handles.DrawSolidDisc(ground, Vector3.up, 0.6f + heat * 0.6f);
        }
    }

    // ───── Отчёт и демо-данные ─────

    private void ExportReport()
    {
        Directory.CreateDirectory(ImportFolder);
        string path = Path.Combine(ImportFolder, $"report_{DateTime.Now:yyyyMMdd_HHmm}.md");
        string text = AnalyticsAggregator.ToMarkdown(_summary);
        if (_hasDemo)
            text = "> ВНИМАНИЕ: отчёт построен на демонстрационных данных.\n\n" + text;
        File.WriteAllText(path, text);
        EditorUtility.RevealInFinder(Path.GetFullPath(path));
    }

    /// <summary>
    /// Генерация демонстрационных событий для проверки работы панели (платформа помечена как Demo).
    /// </summary>
    private static void GenerateDemoData()
    {
        if (!EditorUtility.DisplayDialog("Демо-данные",
                "Будут созданы СГЕНЕРИРОВАННЫЕ события (60 сессий) для проверки панели аналитики. " +
                "Это не статистика реальных игроков — не используйте её в отчётах как настоящие данные.",
                "Создать", "Отмена"))
            return;

        Directory.CreateDirectory(ImportFolder);
        var rnd = new Random(42);
        var lines = new List<string>();
        DateTime t0 = DateTime.UtcNow.AddDays(-7);
        CultureInfo ci = CultureInfo.InvariantCulture;

        for (int s = 0; s < 60; s++)
        {
            var w = new DemoWriter
            {
                Lines = lines,
                Session = "demo" + s.ToString("000"),
                Install = "demo-inst" + (s % 40).ToString("00"),
                Version = s < 30 ? "1.1.1" : "1.2.0",
                Time = t0.AddMinutes(s * 97)
            };

            w.Add("session_start", "quality", "2");
            int attempts = 1 + rnd.Next(3);
            for (int a = 0; a < attempts; a++)
            {
                w.Add("level_start", "level", "Main");
                double roll = rnd.NextDouble();
                int reached = roll < 0.3 ? 25 : roll < 0.55 ? 50 : roll < 0.75 ? 75 : 100;
                for (int m = 25; m <= Math.Min(75, reached); m += 25)
                    w.Add("progress", "percent", m.ToString(ci));

                if (reached == 100)
                {
                    float time = 70f + (float)rnd.NextDouble() * 100f;
                    int stars = time <= 90 ? 3 : time <= 150 ? 2 : 1;
                    w.Add("level_end", "result", "victory", "time", time.ToString("0", ci), "stars", stars.ToString(ci));
                }
                else if (rnd.NextDouble() < 0.8)
                {
                    bool barrel = reached >= 75 || rnd.NextDouble() < 0.5;
                    float z = barrel ? 34f + (float)rnd.NextDouble() * 10f : 13f + (float)rnd.NextDouble() * 13f;
                    float x = -8f + (float)rnd.NextDouble() * 16f;
                    w.Add("death", "cause", barrel ? "barrel" : "obstacle",
                        "x", x.ToString("0.0", ci), "y", "0.5", "z", z.ToString("0.0", ci), "time", (30 + rnd.Next(90)).ToString(ci));
                    w.Add("level_end", "result", "death");
                }
                else
                {
                    w.Add("level_end", "result", "abandoned");
                }
                if (rnd.NextDouble() < 0.4) w.Add("item_used", "item", rnd.NextDouble() < 0.5 ? "Shield" : "SpeedBoost");
            }
            w.Add("perf", "fps", (35 + rnd.Next(45)).ToString(ci));
            if (s % 6 == 0) w.Add("feedback", "rating", (2 + rnd.Next(4)).ToString(ci), "category", s % 12 == 0 ? "bug" : "feedback");
            w.Add("session_end", "duration", (120 + rnd.Next(600)).ToString(ci));
        }

        File.WriteAllLines(DemoFile, lines);
    }

    /// <summary>Запись демонстрационных событий одной сессии.</summary>
    private class DemoWriter
    {
        public List<string> Lines;
        public string Session, Install, Version;
        public DateTime Time;

        public void Add(string name, params string[] kv)
        {
            var e = new AnalyticsEvent
            {
                name = name,
                time = Time.ToString("o", CultureInfo.InvariantCulture),
                session = Session,
                install = Install,
                version = Version,
                platform = "Demo"
            };
            for (int i = 0; i + 1 < kv.Length; i += 2)
                e.data.Add(new AnalyticsParam(kv[i], kv[i + 1]));
            Lines.Add(JsonUtility.ToJson(e));
            Time = Time.AddSeconds(5);
        }
    }
}
