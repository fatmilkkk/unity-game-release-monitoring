using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Подготовка релиза: сборка под Windows, Android (APK / AAB) и WebGL одной командой.
/// Перед сборкой: настройка платформы, номер сборки, файл Resources/build_info.json (версия, дата, коммит).
/// После сборки: отчёт Builds/&lt;платформа&gt;/build_report.txt и строка в Builds/build_history.csv.
/// Меню: Tools → Release. Для CI: -executeMethod ReleaseBuildTool.BuildFromCommandLine.
/// </summary>
public static class ReleaseBuildTool
{
    public const string ReleaseVersion = "1.2.0";
    public const string AndroidPackage = "com.portfolio.ballcollector";

    private const string BuildsRoot = "Builds";
    private const string BuildInfoPath = "Assets/Resources/build_info.json";
    private const string BuildNumberFile = "ProjectSettings/BuildNumber.txt";

    // ───── Меню ─────

    [MenuItem("Tools/Release/Build Windows (x64)", false, 0)]
    private static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, false);

    [MenuItem("Tools/Release/Build Android (APK)", false, 1)]
    private static void BuildAndroidApk() => Build(BuildTarget.Android, false);

    [MenuItem("Tools/Release/Build Android (AAB для Google Play)", false, 2)]
    private static void BuildAndroidAab() => Build(BuildTarget.Android, false, true);

    [MenuItem("Tools/Release/Build WebGL", false, 3)]
    private static void BuildWebGL() => Build(BuildTarget.WebGL, false);

    [MenuItem("Tools/Release/Build All Platforms", false, 20)]
    private static void BuildAll()
    {
        BuildTarget[] targets = { BuildTarget.StandaloneWindows64, BuildTarget.WebGL, BuildTarget.Android };
        var sb = new StringBuilder();
        foreach (BuildTarget t in targets)
            sb.AppendLine($"{t}: {(Build(t, false, false, false) ? "успешно" : "ошибка / модуль не установлен")}");
        EditorUtility.DisplayDialog("Сборка всех платформ", sb.ToString(), "OK");
    }

    [MenuItem("Tools/Release/Development Build (Windows)", false, 40)]
    private static void BuildWindowsDev() => Build(BuildTarget.StandaloneWindows64, true);

    [MenuItem("Tools/Release/Повысить версию (patch)", false, 60)]
    private static void BumpPatch()
    {
        string next = BumpPatch(PlayerSettings.bundleVersion);
        PlayerSettings.bundleVersion = next;
        Debug.Log("[Release] Новая версия: " + next);
    }

    [MenuItem("Tools/Release/Открыть папку сборок", false, 61)]
    private static void OpenBuilds()
    {
        Directory.CreateDirectory(BuildsRoot);
        EditorUtility.RevealInFinder(Path.GetFullPath(BuildsRoot));
    }

    // ───── Сборка ─────

    public static bool Build(BuildTarget target, bool development, bool appBundle = false, bool showDialog = true)
    {
        BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
        if (!BuildPipeline.IsBuildTargetSupported(group, target))
        {
            string msg = $"Модуль сборки {target} не установлен.\nUnity Hub → Installs → ⚙ → Add modules.";
            Debug.LogError("[Release] " + msg);
            if (showDialog && !Application.isBatchMode)
                EditorUtility.DisplayDialog("Сборка невозможна", msg, "OK");
            return false;
        }

        EnsureScenes();
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("[Release] Нет сцен в Build Settings");
            return false;
        }

        ConfigurePlatform(target, appBundle);
        int buildNumber = NextBuildNumber();
        WriteBuildInfo(target, buildNumber, development);

        string path = OutputPath(target, appBundle, development);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = path,
            target = target,
            targetGroup = group,
            options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None
        };

        Debug.Log($"[Release] Сборка {target} v{PlayerSettings.bundleVersion} #{buildNumber} → {path}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        WriteReport(report, target, buildNumber, path, development);

        bool ok = report.summary.result == BuildResult.Succeeded;
        if (showDialog && !Application.isBatchMode)
            EditorUtility.DisplayDialog("Сборка " + target,
                ok ? $"Готово: {FormatSize(report.summary.totalSize)}, {report.summary.totalTime:mm\\:ss}\n{path}"
                   : $"Ошибка сборки ({report.summary.totalErrors} ошибок). Подробности в консоли.", "OK");
        if (ok)
            EditorUtility.RevealInFinder(Path.GetFullPath(path));
        return ok;
    }

    /// <summary>Точка входа для CI (GameCI): аргументы -customBuildTarget и -customBuildPath.</summary>
    public static void BuildFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        string targetName = ArgValue(args, "-customBuildTarget") ?? EditorUserBuildSettings.activeBuildTarget.ToString();
        if (!Enum.TryParse(targetName, out BuildTarget target))
        {
            Debug.LogError("[Release] Неизвестная платформа: " + targetName);
            EditorApplication.Exit(1);
            return;
        }

        bool ok = Build(target, false, ArgValue(args, "-androidAppBundle") == "true", false);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static void EnsureScenes()
    {
        // Сцены собираются кодом; на CI или в чистом клоне их ещё может не быть
        if (!File.Exists(DemoSceneBuilder.ScenePath) || !File.Exists(DemoSceneBuilder.MenuScenePath))
            DemoSceneBuilder.Build();
    }

    // ───── Настройки платформ ─────

    public static void ConfigurePlatform(BuildTarget target, bool appBundle)
    {
        if (VersionUtil.Compare(PlayerSettings.bundleVersion, ReleaseVersion) < 0)
            PlayerSettings.bundleVersion = ReleaseVersion;

        PlayerSettings.companyName = "Portfolio";
        PlayerSettings.productName = "Ball Collector";

        switch (target)
        {
            case BuildTarget.Android:
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidPackage);
                PlayerSettings.Android.bundleVersionCode = VersionCode(PlayerSettings.bundleVersion);
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                // Google Play требует 64-битные сборки: IL2CPP + ARM64
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
                EditorUserBuildSettings.buildAppBundle = appBundle;
                break;

            case BuildTarget.WebGL:
                // Brotli — минимальный размер; fallback позволяет запуск на хостингах без нужных заголовков (itch.io)
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.dataCaching = true;
                break;

            case BuildTarget.StandaloneWindows64:
            case BuildTarget.StandaloneWindows:
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                PlayerSettings.resizableWindow = true;
                break;
        }
    }

    /// <summary>1.2.3 → 10203 (versionCode Android должен расти с каждой версией).</summary>
    public static int VersionCode(string version)
    {
        string[] p = (version ?? "0").Split('.');
        return Part(p, 0) * 10000 + Part(p, 1) * 100 + Part(p, 2);
    }

    private static int Part(string[] parts, int index) =>
        index < parts.Length && int.TryParse(parts[index], out int v) ? v : 0;

    public static string BumpPatch(string version)
    {
        string[] p = (version ?? "1.0.0").Split('.');
        int major = p.Length > 0 && int.TryParse(p[0], out int a) ? a : 1;
        int minor = p.Length > 1 && int.TryParse(p[1], out int b) ? b : 0;
        int patch = p.Length > 2 && int.TryParse(p[2], out int c) ? c : 0;
        return $"{major}.{minor}.{patch + 1}";
    }

    // ───── Информация о сборке ─────

    private static int NextBuildNumber()
    {
        int number = 0;
        if (File.Exists(BuildNumberFile))
            int.TryParse(File.ReadAllText(BuildNumberFile).Trim(), out number);
        number++;
        File.WriteAllText(BuildNumberFile, number.ToString(CultureInfo.InvariantCulture));
        return number;
    }

    private static void WriteBuildInfo(BuildTarget target, int number, bool development)
    {
        var info = new BuildInfo
        {
            version = PlayerSettings.bundleVersion,
            buildNumber = number,
            buildDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
            commit = ReadGitCommit(),
            platform = target.ToString(),
            development = development
        };
        Directory.CreateDirectory(Path.GetDirectoryName(BuildInfoPath));
        File.WriteAllText(BuildInfoPath, JsonUtility.ToJson(info, true));
        AssetDatabase.ImportAsset(BuildInfoPath);
    }

    /// <summary>Короткий хэш текущего коммита (чтение .git без запуска git).</summary>
    public static string ReadGitCommit()
    {
        try
        {
            string head = File.ReadAllText(".git/HEAD").Trim();
            if (!head.StartsWith("ref:"))
                return head.Length >= 7 ? head.Substring(0, 7) : head;

            string refPath = head.Substring(4).Trim();
            string loose = Path.Combine(".git", refPath);
            if (File.Exists(loose))
                return File.ReadAllText(loose).Trim().Substring(0, 7);

            if (File.Exists(".git/packed-refs"))
                foreach (string line in File.ReadAllLines(".git/packed-refs"))
                    if (line.EndsWith(" " + refPath))
                        return line.Substring(0, 7);
        }
        catch (Exception)
        {
            // не git-репозиторий (например, проект скачан архивом)
        }
        return "nogit";
    }

    private static string OutputPath(BuildTarget target, bool appBundle, bool development)
    {
        string version = PlayerSettings.bundleVersion + (development ? "-dev" : "");
        string folder = Path.Combine(BuildsRoot, target.ToString(), "v" + version);
        Directory.CreateDirectory(folder);
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return Path.Combine(folder, "BallCollector.exe");
            case BuildTarget.Android:
                return Path.Combine(folder, $"BallCollector-{version}.{(appBundle ? "aab" : "apk")}");
            case BuildTarget.StandaloneOSX:
                return Path.Combine(folder, "BallCollector.app");
            default:
                return folder; // WebGL — папка
        }
    }

    private static void WriteReport(BuildReport report, BuildTarget target, int number, string path, bool development)
    {
        BuildSummary s = report.summary;
        var sb = new StringBuilder();
        sb.AppendLine("Отчёт о сборке");
        sb.AppendLine($"Платформа: {target}");
        sb.AppendLine($"Версия: {PlayerSettings.bundleVersion}, сборка #{number}{(development ? " (Development)" : "")}");
        sb.AppendLine($"Коммит: {ReadGitCommit()}");
        sb.AppendLine($"Результат: {s.result}");
        sb.AppendLine($"Размер: {FormatSize(s.totalSize)}");
        sb.AppendLine($"Время сборки: {s.totalTime:hh\\:mm\\:ss}");
        sb.AppendLine($"Ошибок: {s.totalErrors}, предупреждений: {s.totalWarnings}");
        sb.AppendLine($"Путь: {path}");

        string folder = Path.Combine(BuildsRoot, target.ToString());
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "build_report.txt"), sb.ToString());

        string history = Path.Combine(BuildsRoot, "build_history.csv");
        if (!File.Exists(history))
            File.WriteAllText(history, "date,platform,version,build,result,size_mb,seconds,errors,warnings\n");
        File.AppendAllText(history, string.Join(",",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), target, PlayerSettings.bundleVersion, number,
            s.result, (s.totalSize / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture),
            ((int)s.totalTime.TotalSeconds).ToString(CultureInfo.InvariantCulture), s.totalErrors, s.totalWarnings) + "\n");

        Debug.Log("[Release]\n" + sb);
    }

    private static string FormatSize(ulong bytes) => (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " МБ";

    private static string ArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }
}
