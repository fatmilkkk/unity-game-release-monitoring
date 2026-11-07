using System.IO;
using Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Собирает демонстрационную сцену проекта «с нуля»: материалы, префабы (монета, бочка, бонус),
/// эффекты частиц, контроллер анимации бонуса (машина состояний), камеру Cinemachine, уровень и HUD.
///
/// Запускается автоматически при первом открытии проекта (если сцены ещё нет)
/// или вручную: меню Tools → Portfolio Demo → Build Demo Scene.
/// </summary>
public static partial class DemoSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Main.unity";
    public const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

    /// <summary>Версия сборки сцен. При увеличении сцены пересобираются автоматически.</summary>
    public const int BuildVersion = 2;
    public const string BuildVersionFile = "Library/PortfolioDemoBuildVersion.txt";

    private const string MaterialsFolder = "Assets/Materials";
    private const string PrefabsFolder = "Assets/Prefabs";
    private const string EffectsFolder = "Assets/Prefabs/Effects";
    private const string AnimationsFolder = "Assets/Animations";
    private const string ArtFolder = "Assets/Art";
    private const string ScenesFolder = "Assets/Scenes";

    private const string BonusParameter = "PlayerInZone";

    // ───────────────────────────── Меню ─────────────────────────────

    [MenuItem("Tools/Portfolio Demo/Build Demo Scene")]
    public static void BuildFromMenu()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Portfolio Demo",
                "Сцена уже существует. Пересобрать её (все ручные изменения в Main.unity будут потеряны)?",
                "Пересобрать", "Отмена"))
            return;

        Build();
    }

    public static void Build()
    {
        EnsureFolders();

        // Материалы
        var mats = new Materials
        {
            Ground = CreateLitMaterial("Ground", new Color(0.32f, 0.36f, 0.42f), 0f, 0.2f),
            Wall = CreateLitMaterial("Wall", new Color(0.22f, 0.25f, 0.3f), 0f, 0.1f),
            Player = CreateLitMaterial("Player", new Color(0.15f, 0.55f, 1f), 0.1f, 0.7f),
            PlayerStripe = CreateLitMaterial("PlayerStripe", Color.white, 0f, 0.5f),
            Gold = CreateLitMaterial("Gold", new Color(1f, 0.78f, 0.15f), 0.85f, 0.75f, new Color(0.35f, 0.25f, 0f)),
            Hazard = CreateLitMaterial("Hazard", new Color(0.9f, 0.18f, 0.12f), 0.2f, 0.4f),
            HazardDark = CreateLitMaterial("HazardDark", new Color(0.15f, 0.15f, 0.15f), 0.5f, 0.4f),
            Barrel = CreateLitMaterial("Barrel", new Color(0.75f, 0.1f, 0.08f), 0.3f, 0.5f),
            BarrelBand = CreateLitMaterial("BarrelBand", new Color(1f, 0.85f, 0.1f), 0.2f, 0.4f),
            Pedestal = CreateLitMaterial("Pedestal", new Color(0.55f, 0.55f, 0.6f), 0.2f, 0.5f),
            Gem = CreateLitMaterial("BonusGem", new Color(0.2f, 1f, 0.75f), 0.1f, 0.9f, new Color(0f, 0.6f, 0.45f)),
            Ramp = CreateLitMaterial("Ramp", new Color(0.95f, 0.6f, 0.15f), 0f, 0.3f),
            Zone = CreateUnlitTransparentMaterial("BonusZone", new Color(0.2f, 1f, 0.8f, 0.18f)),
            Shield = CreateUnlitTransparentMaterial("ShieldBubble", new Color(0.35f, 0.7f, 1f, 0.25f))
        };

        Texture2D softTexture = CreateSoftParticleTexture();
        Material particleAlpha = CreateParticleMaterial("Particle_AlphaBlended", softTexture, false);
        Material particleAdditive = CreateParticleMaterial("Particle_Additive", softTexture, true);

        // Эффекты (префабы)
        GameObject coinFx = SaveAsPrefab(CreateCoinPickupEffect(particleAdditive), EffectsFolder + "/CoinPickupFX.prefab");
        GameObject explosionFx = SaveAsPrefab(CreateExplosionEffect(particleAdditive, particleAlpha), EffectsFolder + "/ExplosionFX.prefab");
        GameObject deathFx = SaveAsPrefab(CreatePlayerDeathEffect(particleAdditive), EffectsFolder + "/PlayerDeathFX.prefab");

        // Анимация бонуса (машина состояний)
        AnimatorController bonusController = CreateBonusAnimatorController();

        // Префабы объектов
        GameObject coinPrefab = SaveAsPrefab(CreateCoin(mats, coinFx), PrefabsFolder + "/Coin.prefab");
        GameObject barrelPrefab = SaveAsPrefab(CreateBarrel(mats, explosionFx), PrefabsFolder + "/ExplosiveBarrel.prefab");
        GameObject bonusPrefab = SaveAsPrefab(CreateBonus(mats, bonusController), PrefabsFolder + "/Bonus.prefab");

        // Сцена
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        BuildEnvironment(mats);
        GameObject player = CreatePlayer(mats, deathFx, out PlayerController controller, out PlayerHealth health,
            out Wallet wallet, out Inventory inventory);
        CreateMovementParticles(controller, health, particleAlpha, particleAdditive);
        SetupCameras(player.transform, controller);
        PlaceCoins(coinPrefab);
        PlaceBonuses(bonusPrefab);
        PlaceMovingObstacles(mats);
        PlaceBarrels(barrelPrefab);
        var statsGo = new GameObject("LevelStats");
        var stats = statsGo.AddComponent<LevelStats>();
        SetRef(stats, "playerHealth", health);

        UISprites sprites = CreateUISprites();
        CreateGameUI(sprites, health, wallet, inventory, controller, stats);

        EditorSceneManager.SaveScene(scene, ScenePath);

        // Сцена главного меню
        var menuScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        BuildMenuScene(sprites, coinPrefab, bonusPrefab, barrelPrefab);
        EditorSceneManager.SaveScene(menuScene, MenuScenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(ScenePath, true)
        };
        File.WriteAllText(BuildVersionFile, BuildVersion.ToString());

        EditorSceneManager.OpenScene(MenuScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Portfolio Demo] Сцены собраны: " + MenuScenePath + " и " + ScenePath + ". Нажмите Play.");
    }

    private class Materials
    {
        public Material Ground, Wall, Player, PlayerStripe, Gold, Hazard, HazardDark,
            Barrel, BarrelBand, Pedestal, Gem, Ramp, Zone, Shield;
    }

    // ───────────────────────────── Папки и ассеты ─────────────────────────────

    private static void EnsureFolders()
    {
        EnsureFolder(MaterialsFolder);
        EnsureFolder(PrefabsFolder);
        EnsureFolder(EffectsFolder);
        EnsureFolder(AnimationsFolder);
        EnsureFolder(ArtFolder);
        EnsureFolder(ScenesFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void CreateOrReplaceAsset(Object asset, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    private static GameObject SaveAsPrefab(GameObject instance, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    private static GameObject Spawn(GameObject prefab, Vector3 position, Transform parent = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.transform.position = position;
        return go;
    }

    // ───────────────────────────── Сериализованные поля ─────────────────────────────

    private static SerializedProperty FindProp(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
            Debug.LogError($"[Portfolio Demo] Поле '{field}' не найдено у {so.targetObject.GetType().Name}");
        return p;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = FindProp(so, field);
        if (p == null) return;
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        var p = FindProp(so, field);
        if (p == null) return;
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var p = FindProp(so, field);
        if (p == null) return;
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        var p = FindProp(so, field);
        if (p == null) return;
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetVector3(Object target, string field, Vector3 value)
    {
        var so = new SerializedObject(target);
        var p = FindProp(so, field);
        if (p == null) return;
        p.vector3Value = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum(Object target, string field, int index)
    {
        var so = new SerializedObject(target);
        var p = FindProp(so, field);
        if (p == null) return;
        p.enumValueIndex = index;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────────── Материалы ─────────────────────────────

    private static bool UsesScriptableRenderPipeline => GraphicsSettings.currentRenderPipeline != null;

    private static Shader LitShader()
    {
        if (UsesScriptableRenderPipeline)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null) return urp;
        }
        return Shader.Find("Standard");
    }

    private static void SetMaterialColor(Material m, Color c)
    {
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
    }

    private static Material CreateLitMaterial(string name, Color color, float metallic, float smoothness, Color? emission = null)
    {
        var m = new Material(LitShader()) { name = name };
        SetMaterialColor(m, color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);

        if (emission.HasValue && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        CreateOrReplaceAsset(m, $"{MaterialsFolder}/{name}.mat");
        return m;
    }

    private static Material CreateUnlitTransparentMaterial(string name, Color color)
    {
        // Sprites/Default — неосвещённый шейдер с альфа-смешиванием, работает и в Built-in, и в URP
        var m = new Material(Shader.Find("Sprites/Default")) { name = name };
        m.color = color;
        CreateOrReplaceAsset(m, $"{MaterialsFolder}/{name}.mat");
        return m;
    }

    private static Texture2D CreateSoftParticleTexture()
    {
        string path = ArtFolder + "/SoftParticle.png";
        if (!File.Exists(path))
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material CreateParticleMaterial(string name, Texture2D texture, bool additive)
    {
        Shader shader = null;
        if (!UsesScriptableRenderPipeline)
            shader = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        var m = new Material(shader) { name = name, mainTexture = texture };
        CreateOrReplaceAsset(m, $"{MaterialsFolder}/{name}.mat");
        return m;
    }

    // ───────────────────────────── Частицы ─────────────────────────────

    private static ParticleSystem NewParticleSystem(string name, Material material, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null)
            go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    private static void FadeOverLifetime(ParticleSystem ps, Color start, Color end)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void SizeOverLifetime(ParticleSystem ps, float startScale, float endScale)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, startScale, 1f, endScale));
    }

    private static void ConfigureBurst(ParticleSystem ps, int count, Vector2 lifetime, Vector2 speed, Vector2 size, float gravity)
    {
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = Color.white;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count * 2;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
    }

    private static GameObject CreateCoinPickupEffect(Material additive)
    {
        var ps = NewParticleSystem("CoinPickupFX", additive);
        ConfigureBurst(ps, 30, new Vector2(0.3f, 0.6f), new Vector2(2f, 4f), new Vector2(0.1f, 0.25f), 0.5f);
        FadeOverLifetime(ps, new Color(1f, 0.95f, 0.5f), new Color(1f, 0.6f, 0f));
        SizeOverLifetime(ps, 1f, 0.1f);
        ps.gameObject.AddComponent<AutoDestroy>();
        SetFloat(ps.gameObject.GetComponent<AutoDestroy>(), "lifetime", 1.5f);
        return ps.gameObject;
    }

    private static GameObject CreatePlayerDeathEffect(Material additive)
    {
        var ps = NewParticleSystem("PlayerDeathFX", additive);
        ConfigureBurst(ps, 60, new Vector2(0.5f, 1.1f), new Vector2(3f, 7f), new Vector2(0.15f, 0.4f), 1f);
        FadeOverLifetime(ps, new Color(0.5f, 0.85f, 1f), new Color(0.1f, 0.3f, 1f));
        SizeOverLifetime(ps, 1f, 0f);
        var auto = ps.gameObject.AddComponent<AutoDestroy>();
        SetFloat(auto, "lifetime", 2f);
        return ps.gameObject;
    }

    private static GameObject CreateExplosionEffect(Material additive, Material alpha)
    {
        // Огненная вспышка
        var fire = NewParticleSystem("ExplosionFX", additive);
        ConfigureBurst(fire, 80, new Vector2(0.4f, 0.9f), new Vector2(4f, 10f), new Vector2(0.6f, 1.4f), -0.2f);
        FadeOverLifetime(fire, new Color(1f, 0.9f, 0.4f), new Color(1f, 0.25f, 0f));
        SizeOverLifetime(fire, 1f, 0.3f);
        var fireShape = fire.shape;
        fireShape.radius = 0.6f;

        // Дым
        var smoke = NewParticleSystem("Smoke", alpha, fire.transform);
        ConfigureBurst(smoke, 35, new Vector2(1.2f, 2.2f), new Vector2(1f, 3f), new Vector2(1.2f, 2.4f), -0.15f);
        FadeOverLifetime(smoke, new Color(0.35f, 0.33f, 0.3f), new Color(0.15f, 0.15f, 0.15f));
        SizeOverLifetime(smoke, 0.6f, 1.6f);
        var smokeShape = smoke.shape;
        smokeShape.radius = 0.8f;

        // Искры
        var sparks = NewParticleSystem("Sparks", additive, fire.transform);
        ConfigureBurst(sparks, 50, new Vector2(0.5f, 1.2f), new Vector2(8f, 15f), new Vector2(0.05f, 0.12f), 1.5f);
        FadeOverLifetime(sparks, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.4f, 0f));
        var sparksRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparksRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        sparksRenderer.velocityScale = 0.05f;
        sparksRenderer.lengthScale = 2f;

        // Вспышка света
        var lightGo = new GameObject("Flash");
        lightGo.transform.SetParent(fire.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.2f);
        light.range = 10f;
        light.intensity = 6f;

        var auto = fire.gameObject.AddComponent<AutoDestroy>();
        SetFloat(auto, "lifetime", 3f);
        var lightAuto = lightGo.AddComponent<AutoDestroy>();
        SetFloat(lightAuto, "lifetime", 0.15f);

        return fire.gameObject;
    }

    private static void CreateMovementParticles(PlayerController controller, PlayerHealth health,
        Material alpha, Material additive)
    {
        var root = new GameObject("PlayerMovementFX");
        root.transform.position = controller.transform.position;
        var fx = root.AddComponent<PlayerMovementParticles>();

        // Пыль из-под шара
        var dust = NewParticleSystem("Dust", alpha, root.transform);
        dust.transform.localPosition = new Vector3(0f, -0.45f, 0f);
        {
            var main = dust.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startColor = Color.white;
            main.gravityModifier = 0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var emission = dust.emission;
            emission.rateOverTime = 0f; // управляется скриптом в зависимости от скорости

            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // конус смотрит вверх

            FadeOverLifetime(dust, new Color(0.85f, 0.8f, 0.7f), new Color(0.6f, 0.55f, 0.5f));
            SizeOverLifetime(dust, 0.6f, 1.5f);
        }

        // Светящийся след на высокой скорости
        var trail = NewParticleSystem("SpeedTrail", additive, root.transform);
        {
            var main = trail.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.45f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = trail.emission;
            emission.rateOverTime = 0f;

            var shape = trail.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            FadeOverLifetime(trail, new Color(0.4f, 0.9f, 1f), new Color(0.1f, 0.3f, 1f));
            SizeOverLifetime(trail, 1f, 0f);
        }

        // Облачко при приземлении
        var landing = NewParticleSystem("LandingBurst", alpha, root.transform);
        landing.transform.localPosition = new Vector3(0f, -0.45f, 0f);
        {
            var main = landing.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;

            var emission = landing.emission;
            emission.rateOverTime = 0f; // частицы выпускаются скриптом через Emit()

            var shape = landing.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(90f, 0f, 0f); // круг лежит на земле

            FadeOverLifetime(landing, new Color(0.9f, 0.85f, 0.75f), new Color(0.6f, 0.55f, 0.5f));
            SizeOverLifetime(landing, 0.5f, 1.4f);
        }

        SetRef(fx, "player", controller);
        SetRef(fx, "health", health);
        SetRef(fx, "dust", dust);
        SetRef(fx, "speedTrail", trail);
        SetRef(fx, "landingBurst", landing);
    }

    // ───────────────────────────── Анимация бонуса ─────────────────────────────

    private static void SetCurve(AnimationClip clip, string path, System.Type type, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    private static AnimationCurve Keys(params float[] timeValuePairs)
    {
        var curve = new AnimationCurve();
        for (int i = 0; i + 1 < timeValuePairs.Length; i += 2)
            curve.AddKey(new Keyframe(timeValuePairs[i], timeValuePairs[i + 1]));
        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);
        return curve;
    }

    /// <summary>Создаёт клип, анимирующий дочерний объект Visual бонуса и его подсветку.</summary>
    private static AnimationClip CreateBonusClip(string name, bool loop, AnimationCurve posY, AnimationCurve scale,
        float duration, float rotationDegrees, AnimationCurve lightIntensity)
    {
        var clip = new AnimationClip { name = name, frameRate = 60f };

        SetCurve(clip, "Visual", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, duration, 0f));
        SetCurve(clip, "Visual", typeof(Transform), "m_LocalPosition.y", posY);
        SetCurve(clip, "Visual", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Constant(0f, duration, 0f));

        SetCurve(clip, "Visual", typeof(Transform), "m_LocalScale.x", scale);
        SetCurve(clip, "Visual", typeof(Transform), "m_LocalScale.y", scale);
        SetCurve(clip, "Visual", typeof(Transform), "m_LocalScale.z", scale);

        SetCurve(clip, "Visual", typeof(Transform), "localEulerAnglesRaw.x", AnimationCurve.Constant(0f, duration, 0f));
        SetCurve(clip, "Visual", typeof(Transform), "localEulerAnglesRaw.y", AnimationCurve.Linear(0f, 0f, duration, rotationDegrees));
        SetCurve(clip, "Visual", typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.Constant(0f, duration, 0f));

        SetCurve(clip, "Visual/Glow", typeof(Light), "m_Intensity", lightIntensity);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        CreateOrReplaceAsset(clip, $"{AnimationsFolder}/{name}.anim");
        return clip;
    }

    private static AnimatorController CreateBonusAnimatorController()
    {
        // Idle: бонус медленно вращается и покачивается
        AnimationClip idle = CreateBonusClip("Bonus_Idle", true,
            Keys(0f, 1.2f, 1f, 1.45f, 2f, 1.2f, 3f, 1.45f, 4f, 1.2f),
            Keys(0f, 1f, 4f, 1f),
            4f, 360f,
            Keys(0f, 1f, 2f, 1.4f, 4f, 1f));

        // Activate: игрок вошёл в зону — бонус подпрыгивает, увеличивается и вспыхивает
        AnimationClip activate = CreateBonusClip("Bonus_Activate", false,
            Keys(0f, 1.2f, 0.25f, 2.3f, 0.5f, 1.9f),
            Keys(0f, 1f, 0.25f, 1.7f, 0.5f, 1.4f),
            0.5f, 360f,
            Keys(0f, 1f, 0.25f, 8f, 0.5f, 4f));

        // Active: игрок в зоне — бонус быстро вращается и пульсирует
        AnimationClip active = CreateBonusClip("Bonus_Active", true,
            Keys(0f, 1.9f, 0.6f, 2.1f, 1.2f, 1.9f),
            Keys(0f, 1.4f, 0.6f, 1.5f, 1.2f, 1.4f),
            1.2f, 360f,
            Keys(0f, 4f, 0.6f, 5.5f, 1.2f, 4f));

        // Deactivate: игрок вышел из зоны — бонус «успокаивается» и возвращается на место
        AnimationClip deactivate = CreateBonusClip("Bonus_Deactivate", false,
            Keys(0f, 1.9f, 0.4f, 1.2f),
            Keys(0f, 1.4f, 0.25f, 0.85f, 0.4f, 1f),
            0.4f, 180f,
            Keys(0f, 4f, 0.4f, 1f));

        string path = AnimationsFolder + "/BonusAnimator.controller";
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter(BonusParameter, AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState sIdle = sm.AddState("Idle", new Vector3(300f, 0f));
        AnimatorState sActivate = sm.AddState("Activate", new Vector3(550f, -100f));
        AnimatorState sActive = sm.AddState("Active", new Vector3(800f, 0f));
        AnimatorState sDeactivate = sm.AddState("Deactivate", new Vector3(550f, 100f));
        sIdle.motion = idle;
        sActivate.motion = activate;
        sActive.motion = active;
        sDeactivate.motion = deactivate;
        sm.defaultState = sIdle;

        // Вход в зону
        AddTransition(sIdle, sActivate, false, 0.1f, AnimatorConditionMode.If);
        AddTransition(sDeactivate, sActivate, false, 0.1f, AnimatorConditionMode.If);
        // Анимация активации доиграла — переходим в цикл Active
        AddTransition(sActivate, sActive, true, 0.1f, null);
        // Выход из зоны
        AddTransition(sActive, sDeactivate, false, 0.15f, AnimatorConditionMode.IfNot);
        AddTransition(sActivate, sDeactivate, false, 0.15f, AnimatorConditionMode.IfNot);
        // Анимация деактивации доиграла — возвращаемся в Idle
        AddTransition(sDeactivate, sIdle, true, 0.15f, null);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, bool byExitTime, float duration,
        AnimatorConditionMode? condition)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = byExitTime;
        t.exitTime = 1f;
        t.hasFixedDuration = true;
        t.duration = duration;
        if (condition.HasValue)
            t.AddCondition(condition.Value, 0f, BonusParameter);
    }

    // ───────────────────────────── Объекты-префабы ─────────────────────────────

    private static GameObject CreatePrimitiveChild(PrimitiveType type, string name, Transform parent, Material material,
        Vector3 localPosition, Vector3 localScale, Vector3 localEuler, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!keepCollider)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.transform.localEulerAngles = localEuler;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject CreateCoin(Materials mats, GameObject pickupFx)
    {
        var root = new GameObject("Coin");
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;

        CreatePrimitiveChild(PrimitiveType.Cylinder, "Mesh", root.transform, mats.Gold,
            Vector3.zero, new Vector3(0.8f, 0.06f, 0.8f), new Vector3(90f, 0f, 0f), false);

        var coin = root.AddComponent<Coin>();
        SetRef(coin, "pickupEffectPrefab", pickupFx);
        return root;
    }

    private static GameObject CreateBarrel(Materials mats, GameObject explosionFx)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // с CapsuleCollider
        root.name = "ExplosiveBarrel";
        root.transform.localScale = new Vector3(0.9f, 0.7f, 0.9f);
        root.GetComponent<Renderer>().sharedMaterial = mats.Barrel;

        CreatePrimitiveChild(PrimitiveType.Cylinder, "BandTop", root.transform, mats.BarrelBand,
            new Vector3(0f, 0.55f, 0f), new Vector3(1.03f, 0.08f, 1.03f), Vector3.zero, false);
        CreatePrimitiveChild(PrimitiveType.Cylinder, "BandBottom", root.transform, mats.BarrelBand,
            new Vector3(0f, -0.55f, 0f), new Vector3(1.03f, 0.08f, 1.03f), Vector3.zero, false);
        CreatePrimitiveChild(PrimitiveType.Cube, "HazardSign", root.transform, mats.BarrelBand,
            new Vector3(0f, 0f, -0.5f), new Vector3(0.35f, 0.4f, 0.05f), new Vector3(0f, 0f, 45f), false);

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var barrel = root.AddComponent<ExplosiveBarrel>();
        SetRef(barrel, "explosionEffectPrefab", explosionFx);
        return root;
    }

    private static GameObject CreateBonus(Materials mats, AnimatorController controller)
    {
        var root = new GameObject("Bonus");
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 2.5f;
        trigger.center = new Vector3(0f, 1f, 0f);

        CreatePrimitiveChild(PrimitiveType.Cylinder, "Pedestal", root.transform, mats.Pedestal,
            new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.2f, 0.9f), Vector3.zero, true);
        CreatePrimitiveChild(PrimitiveType.Cylinder, "ZoneMarker", root.transform, mats.Zone,
            new Vector3(0f, 0.02f, 0f), new Vector3(5f, 0.01f, 5f), Vector3.zero, false);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        CreatePrimitiveChild(PrimitiveType.Cube, "Gem", visual.transform, mats.Gem,
            Vector3.zero, Vector3.one * 0.55f, new Vector3(45f, 0f, 45f), false);

        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(visual.transform, false);
        var glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.3f, 1f, 0.8f);
        glow.range = 4f;
        glow.intensity = 1f;

        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var zone = root.AddComponent<BonusZone>();
        SetRef(zone, "animator", animator);
        return root;
    }

    // ───────────────────────────── Сцена ─────────────────────────────

    private static GameObject CreateBlock(string name, Transform parent, Material material, Vector3 position, Vector3 scale,
        Vector3 euler = default)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.transform.eulerAngles = euler;
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.isStatic = true;
        return go;
    }

    private static void BuildEnvironment(Materials mats)
    {
        GameObject light = GameObject.Find("Directional Light");
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var l = light.GetComponent<Light>();
            l.shadows = LightShadows.Soft;
            l.intensity = 1.1f;
        }

        var level = new GameObject("Level").transform;

        // Пол: z от -18 до 50
        CreateBlock("Ground", level, mats.Ground, new Vector3(0f, -0.5f, 16f), new Vector3(32f, 1f, 68f));

        // Стены по краям
        CreateBlock("Wall_Left", level, mats.Wall, new Vector3(-16.5f, 0.75f, 16f), new Vector3(1f, 1.5f, 68f));
        CreateBlock("Wall_Right", level, mats.Wall, new Vector3(16.5f, 0.75f, 16f), new Vector3(1f, 1.5f, 68f));
        CreateBlock("Wall_Back", level, mats.Wall, new Vector3(0f, 0.75f, -18.5f), new Vector3(34f, 1.5f, 1f));
        CreateBlock("Wall_Front", level, mats.Wall, new Vector3(0f, 0.75f, 50.5f), new Vector3(34f, 1.5f, 1f));

        // Разделительные полоски зон
        CreateBlock("Line_Obstacles", level, mats.Ramp, new Vector3(0f, 0.01f, 11f), new Vector3(32f, 0.02f, 0.3f));
        CreateBlock("Line_Barrels", level, mats.Hazard, new Vector3(0f, 0.01f, 30f), new Vector3(32f, 0.02f, 0.3f));

        // Трамплин для разгона/прыжка
        CreateBlock("Ramp", level, mats.Ramp, new Vector3(-11f, 0.4f, 0f), new Vector3(4f, 0.3f, 6f), new Vector3(-12f, 0f, 0f));
    }

    private static GameObject CreatePlayer(Materials mats, GameObject deathFx,
        out PlayerController controller, out PlayerHealth health, out Wallet wallet, out Inventory inventory)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Sphere); // SphereCollider r = 0.5
        player.name = "Player";
        player.transform.position = new Vector3(0f, 0.6f, -12f);
        player.GetComponent<Renderer>().sharedMaterial = mats.Player;

        // Полоски, чтобы было видно вращение шара
        CreatePrimitiveChild(PrimitiveType.Cylinder, "Stripe_A", player.transform, mats.PlayerStripe,
            Vector3.zero, new Vector3(1.02f, 0.04f, 1.02f), Vector3.zero, false);
        CreatePrimitiveChild(PrimitiveType.Cylinder, "Stripe_B", player.transform, mats.PlayerStripe,
            Vector3.zero, new Vector3(1.02f, 0.04f, 1.02f), new Vector3(90f, 0f, 0f), false);

        var rb = player.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.drag = 0.4f;
        rb.angularDrag = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        controller = player.AddComponent<PlayerController>();
        health = player.AddComponent<PlayerHealth>();
        wallet = player.AddComponent<Wallet>();

        SetRef(health, "deathEffectPrefab", deathFx);
        SetRef(health, "bodyRenderer", player.GetComponent<Renderer>());
        SetBool(health, "autoRestart", false); // экран поражения показывает GameUIController

        // Сфера щита (видна, пока действует предмет «Щит»)
        GameObject shield = CreatePrimitiveChild(PrimitiveType.Sphere, "ShieldBubble", player.transform, mats.Shield,
            Vector3.zero, Vector3.one * 1.6f, Vector3.zero, false);
        shield.SetActive(false);
        SetRef(health, "shieldVisual", shield);

        inventory = player.AddComponent<Inventory>();
        SetRef(inventory, "health", health);
        SetRef(inventory, "controller", controller);
        return player;
    }

    private static void SetupCameras(Transform target, PlayerController controller)
    {
        GameObject camGo = GameObject.Find("Main Camera");
        if (camGo == null)
        {
            camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }

        var cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 55f;
        camGo.transform.position = target.position + new Vector3(0f, 7f, -9f);
        camGo.transform.LookAt(target);

        // Brain управляет реальной камерой, получая позицию от активной виртуальной камеры
        var brain = camGo.GetComponent<CinemachineBrain>();
        if (brain == null)
            brain = camGo.AddComponent<CinemachineBrain>();
        brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.SmartUpdate;
        brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 1f);

        // Виртуальная камера, следящая за игроком
        var vcamGo = new GameObject("CM vcam Player");
        vcamGo.transform.position = camGo.transform.position;
        vcamGo.transform.rotation = camGo.transform.rotation;

        var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
        vcam.Priority = 10;
        vcam.Follow = target;
        vcam.LookAt = target;
        vcam.m_Lens.FieldOfView = 55f;
        vcam.m_Lens.NearClipPlane = 0.1f;
        vcam.m_Lens.FarClipPlane = 500f;

        // Body: Transposer — камера держится на смещении от игрока в мировых координатах
        // (WorldSpace, чтобы камера не крутилась вместе с катящимся шаром)
        var transposer = vcam.AddCinemachineComponent<CinemachineTransposer>();
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        transposer.m_FollowOffset = new Vector3(0f, 7f, -9f);
        transposer.m_XDamping = 0.8f;
        transposer.m_YDamping = 1.2f;
        transposer.m_ZDamping = 0.8f;

        // Aim: Composer — плавно удерживает игрока в кадре
        var composer = vcam.AddCinemachineComponent<CinemachineComposer>();
        composer.m_TrackedObjectOffset = new Vector3(0f, 0.5f, 0f);
        composer.m_LookaheadTime = 0.2f;
        composer.m_LookaheadSmoothing = 5f;
        composer.m_HorizontalDamping = 0.6f;
        composer.m_VerticalDamping = 0.6f;
        composer.m_DeadZoneWidth = 0.1f;
        composer.m_DeadZoneHeight = 0.1f;
        composer.m_SoftZoneWidth = 0.6f;
        composer.m_SoftZoneHeight = 0.6f;

        SetRef(controller, "cameraTransform", camGo.transform);
    }

    private static void PlaceCoins(GameObject coinPrefab)
    {
        var parent = new GameObject("Coins").transform;

        // Дорожка от старта
        for (int i = 0; i < 6; i++)
            Spawn(coinPrefab, new Vector3(0f, 1f, -9f + i * 2f), parent);

        // Монеты на трамплине и за ним
        for (int i = 0; i < 3; i++)
            Spawn(coinPrefab, new Vector3(-11f, 1.6f + i * 0.4f, -1.5f + i * 1.5f), parent);

        // Дуги возле бонусов
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.Lerp(-60f, 60f, i / 4f) * Mathf.Deg2Rad;
            Spawn(coinPrefab, new Vector3(-9f + Mathf.Sin(a) * 4f, 1f, 4f - Mathf.Cos(a) * 4f), parent);
            Spawn(coinPrefab, new Vector3(9f + Mathf.Sin(a) * 4f, 1f, 4f - Mathf.Cos(a) * 4f), parent);
        }

        // В зоне подвижных препятствий (рискованно!)
        float[] xs = { -8f, -3f, 3f, 8f };
        foreach (float x in xs)
        {
            Spawn(coinPrefab, new Vector3(x, 1f, 15.5f), parent);
            Spawn(coinPrefab, new Vector3(x, 1f, 19.5f), parent);
        }
        Spawn(coinPrefab, new Vector3(0f, 1f, 25f), parent);

        // Возле бочек
        Spawn(coinPrefab, new Vector3(-6f, 1f, 37f), parent);
        Spawn(coinPrefab, new Vector3(6f, 1f, 37f), parent);
        Spawn(coinPrefab, new Vector3(0f, 1f, 46f), parent);
        Spawn(coinPrefab, new Vector3(-10f, 1f, 45f), parent);
        Spawn(coinPrefab, new Vector3(10f, 1f, 45f), parent);
    }

    private static void PlaceBonuses(GameObject bonusPrefab)
    {
        var parent = new GameObject("Bonuses").transform;
        SetBonusReward(Spawn(bonusPrefab, new Vector3(-9f, 0f, 4f), parent), ItemType.Medkit);
        SetBonusReward(Spawn(bonusPrefab, new Vector3(9f, 0f, 4f), parent), ItemType.Shield);
        SetBonusReward(Spawn(bonusPrefab, new Vector3(-12f, 0f, 27f), parent), ItemType.SpeedBoost);
        SetBonusReward(Spawn(bonusPrefab, new Vector3(12f, 0f, 27f), parent), ItemType.Medkit);
    }

    private static void SetBonusReward(GameObject bonus, ItemType item)
    {
        var zone = bonus.GetComponent<BonusZone>();
        SetEnum(zone, "rewardItem", (int)item);
        bonus.name = "Bonus_" + item;
    }

    private static void PlaceMovingObstacles(Materials mats)
    {
        var parent = new GameObject("MovingObstacles").transform;

        // Блоки, двигающиеся влево-вправо
        CreateSlider(parent, mats, new Vector3(-11f, 0.75f, 14f), new Vector3(22f, 0f, 0f), 0.9f, 0f, 20);
        CreateSlider(parent, mats, new Vector3(11f, 0.75f, 17.5f), new Vector3(-22f, 0f, 0f), 1.2f, 1.5f, 20);
        CreateSlider(parent, mats, new Vector3(-11f, 0.75f, 21f), new Vector3(22f, 0f, 0f), 1.5f, 3f, 25);

        // Вращающаяся балка
        var pivot = new GameObject("RotatingBar");
        pivot.transform.SetParent(parent, false);
        pivot.transform.position = new Vector3(0f, 0.6f, 25f);

        CreatePrimitiveChild(PrimitiveType.Cylinder, "Hub", pivot.transform, mats.HazardDark,
            Vector3.zero, new Vector3(1.2f, 0.6f, 1.2f), Vector3.zero, true);
        CreatePrimitiveChild(PrimitiveType.Cube, "Bar", pivot.transform, mats.Hazard,
            Vector3.zero, new Vector3(12f, 0.6f, 0.6f), Vector3.zero, true);

        var rb = pivot.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        var rotating = pivot.AddComponent<MovingObstacle>();
        SetEnum(rotating, "motion", (int)MovingObstacle.MotionType.Rotate);
        SetVector3(rotating, "rotationSpeed", new Vector3(0f, 70f, 0f));
        SetInt(rotating, "damage", 30);
        SetFloat(rotating, "knockback", 10f);
    }

    private static void CreateSlider(Transform parent, Materials mats, Vector3 start, Vector3 offset,
        float speed, float phase, int damage)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "SlidingBlock";
        block.transform.SetParent(parent, false);
        block.transform.position = start;
        block.transform.localScale = new Vector3(2.5f, 1.5f, 1.5f);
        block.GetComponent<Renderer>().sharedMaterial = mats.Hazard;

        CreatePrimitiveChild(PrimitiveType.Cube, "Stripe", block.transform, mats.HazardDark,
            new Vector3(0f, 0.35f, 0f), new Vector3(1.01f, 0.15f, 1.01f), Vector3.zero, false);

        var rb = block.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var obstacle = block.AddComponent<MovingObstacle>();
        SetEnum(obstacle, "motion", (int)MovingObstacle.MotionType.PingPong);
        SetVector3(obstacle, "moveOffset", offset);
        SetFloat(obstacle, "moveSpeed", speed);
        SetFloat(obstacle, "phase", phase);
        SetInt(obstacle, "damage", damage);
    }

    private static void PlaceBarrels(GameObject barrelPrefab)
    {
        var parent = new GameObject("ExplosiveBarrels").transform;
        const float y = 0.7f;

        // Группа для цепной реакции
        Spawn(barrelPrefab, new Vector3(-3f, y, 40f), parent);
        Spawn(barrelPrefab, new Vector3(-1.8f, y, 41f), parent);
        Spawn(barrelPrefab, new Vector3(-4.2f, y, 41.2f), parent);
        Spawn(barrelPrefab, new Vector3(-3f, y, 42.3f), parent);

        // Одиночные бочки
        Spawn(barrelPrefab, new Vector3(4f, y, 36f), parent);
        Spawn(barrelPrefab, new Vector3(8f, y, 41f), parent);
        Spawn(barrelPrefab, new Vector3(0f, y, 34f), parent);
        Spawn(barrelPrefab, new Vector3(-9f, y, 36f), parent);
    }

}

/// <summary>
/// Автоматически собирает демо-сцены при первом открытии проекта
/// или если код сборки обновился (BuildVersion больше сохранённой).
/// </summary>
[InitializeOnLoad]
internal static class DemoSceneAutoBuilder
{
    private const string SessionKey = "PortfolioDemo.AutoBuildChecked";

    static DemoSceneAutoBuilder()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;
        EditorApplication.delayCall += TryBuild;
    }

    private static int StoredVersion()
    {
        try
        {
            if (File.Exists(DemoSceneBuilder.BuildVersionFile) &&
                int.TryParse(File.ReadAllText(DemoSceneBuilder.BuildVersionFile).Trim(), out int v))
                return v;
        }
        catch (IOException) { }
        return 0;
    }

    private static void TryBuild()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryBuild;
            return;
        }

        SessionState.SetBool(SessionKey, true);

        bool scenesExist = File.Exists(DemoSceneBuilder.ScenePath) && File.Exists(DemoSceneBuilder.MenuScenePath);
        if (scenesExist && StoredVersion() >= DemoSceneBuilder.BuildVersion)
        {
            if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(DemoSceneBuilder.MenuScenePath);
            return;
        }

        Debug.Log("[Portfolio Demo] Собираю сцены (версия сборки " + DemoSceneBuilder.BuildVersion + ")...");
        DemoSceneBuilder.Build();
    }
}
