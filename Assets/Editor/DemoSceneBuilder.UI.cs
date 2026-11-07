using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Часть сборщика, отвечающая за пользовательский интерфейс:
/// главное меню, HUD (счётчики, здоровье, панель предметов), пауза, инвентарь,
/// управление, настройки, экраны поражения и победы, уведомления.
/// </summary>
public static partial class DemoSceneBuilder
{
    // ───────────────────────────── Палитра ─────────────────────────────

    private static readonly Color PanelColor = new Color(0.09f, 0.11f, 0.15f, 0.97f);
    private static readonly Color CardColor = new Color(0.05f, 0.07f, 0.1f, 0.55f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color AccentColor = new Color(0.25f, 0.6f, 1f);
    private static readonly Color GoldColor = new Color(1f, 0.82f, 0.25f);
    private static readonly Color DangerColor = new Color(0.95f, 0.3f, 0.3f);
    private static readonly Color GoodColor = new Color(0.3f, 0.85f, 0.4f);
    private static readonly Color MutedText = new Color(0.75f, 0.8f, 0.9f);
    private static readonly Color ButtonColor = new Color(0.2f, 0.25f, 0.34f);

    private class UISprites
    {
        public Sprite Rounded, Background, Knob, Checkmark, Circle, Star;
    }

    // ───────────────────────────── Спрайты ─────────────────────────────

    private static UISprites CreateUISprites()
    {
        return new UISprites
        {
            Rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            Background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            Knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            Checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            Circle = CreateShapeSprite("UICircle", (x, y) => x * x + y * y <= 1f),
            Star = CreateShapeSprite("UIStar", InsideStar)
        };
    }

    /// <summary>Рисует белую фигуру со сглаживанием и сохраняет её как спрайт.</summary>
    private static Sprite CreateShapeSprite(string name, Func<float, float, bool> inside)
    {
        string path = $"{ArtFolder}/{name}.png";
        const int size = 128;
        const int sub = 4;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            int hits = 0;
            for (int sy = 0; sy < sub; sy++)
            for (int sx = 0; sx < sub; sx++)
            {
                float x = ((px + (sx + 0.5f) / sub) / size) * 2f - 1f;
                float y = ((py + (sy + 0.5f) / sub) / size) * 2f - 1f;
                if (inside(x * 1.04f, y * 1.04f)) hits++;
            }
            tex.SetPixel(px, py, new Color(1f, 1f, 1f, hits / (float)(sub * sub)));
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static bool InsideStar(float x, float y)
    {
        const int points = 5;
        const float outer = 1f, inner = 0.45f;
        var verts = new Vector2[points * 2];
        for (int i = 0; i < verts.Length; i++)
        {
            float angle = Mathf.PI / 2f + i * Mathf.PI / points;
            float r = i % 2 == 0 ? outer : inner;
            verts[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r - 0.05f);
        }

        bool result = false;
        for (int i = 0, j = verts.Length - 1; i < verts.Length; j = i++)
        {
            if ((verts[i].y > y) != (verts[j].y > y) &&
                x < (verts[j].x - verts[i].x) * (y - verts[i].y) / (verts[j].y - verts[i].y) + verts[i].x)
                result = !result;
        }
        return result;
    }

    private static DefaultControls.Resources ControlResources(UISprites s)
    {
        return new DefaultControls.Resources
        {
            standard = s.Rounded,
            background = s.Background,
            inputField = s.Background,
            knob = s.Knob,
            checkmark = s.Checkmark,
            dropdown = s.Checkmark,
            mask = s.Rounded
        };
    }

    // ───────────────────────────── Базовые элементы ─────────────────────────────

    private static Font UIFont()
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch { /* старые версии Unity */ }
        if (font == null)
        {
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { /* новые версии Unity */ }
        }
        return font;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    private static RectTransform NewStretch(string name, Transform parent, float inset = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    private static Image AddImage(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        if (sprite != null && sprite.border.sqrMagnitude > 0f)
            image.type = Image.Type.Sliced;
        return image;
    }

    private static Text NewText(string name, Transform parent, string content, int fontSize, TextAnchor alignment,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color? color = null, bool bold = false)
    {
        RectTransform rt = NewRect(name, parent, anchor, pivot, position, size);
        var text = rt.gameObject.AddComponent<Text>();
        StyleText(text, content, fontSize, alignment, color ?? Color.white, bold);
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(2f, -2f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        return text;
    }

    private static void StyleText(Text text, string content, int fontSize, TextAnchor alignment, Color color, bool bold)
    {
        text.font = UIFont();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.supportRichText = true;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private static Button NewButton(UISprites sprites, Transform parent, string label, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size, Color? color = null, int fontSize = 30)
    {
        GameObject go = DefaultControls.CreateButton(ControlResources(sprites));
        go.name = "Button_" + label.Replace(" ", "");
        SetLayerRecursive(go, 5);
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        go.GetComponent<Image>().color = color ?? ButtonColor;

        var button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.9f, 0.9f, 0.9f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.95f, 0.95f, 0.95f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        Text text = go.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            StyleText(text, label, fontSize, TextAnchor.MiddleCenter, Color.white, true);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        }

        go.AddComponent<UIButtonFeedback>();
        return button;
    }

    /// <summary>Затемнение на весь экран + окно по центру. Возвращает UIPanel, окно — через out.</summary>
    private static UIPanel NewWindow(UISprites sprites, Transform canvas, string name, string title, Vector2 size,
        UIPanel.AnimationType animation, out RectTransform window, Color? titleColor = null)
    {
        RectTransform root = NewStretch(name, canvas);
        AddImage(root, null, DimColor, true);
        root.gameObject.AddComponent<CanvasGroup>();
        var panel = root.gameObject.AddComponent<UIPanel>();

        window = NewRect("Window", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        AddImage(window, sprites.Rounded, PanelColor, true);
        var outline = window.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        NewText("Title", window, title, 54, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(size.x - 60f, 80f),
            titleColor ?? Color.white, true);

        RectTransform line = NewRect("TitleLine", window, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -110f), new Vector2(size.x - 120f, 3f));
        AddImage(line, null, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.5f));

        SetEnum(panel, "animationType", (int)animation);
        SetRef(panel, "content", window);
        return panel;
    }

    private static Slider NewSlider(UISprites sprites, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = DefaultControls.CreateSlider(ControlResources(sprites));
        go.name = "Slider";
        SetLayerRecursive(go, 5);
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        foreach (Image image in go.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "Background") image.color = new Color(0.2f, 0.23f, 0.3f);
            else if (image.name == "Fill") image.color = AccentColor;
            else if (image.name == "Handle") image.color = Color.white;
        }

        Transform handle = go.transform.Find("Handle Slide Area/Handle");
        if (handle != null)
            ((RectTransform)handle).sizeDelta = new Vector2(36f, 0f);

        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        var nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;
        return slider;
    }

    private static Toggle NewToggle(UISprites sprites, Transform parent, string label, Vector2 position, Vector2 size)
    {
        GameObject go = DefaultControls.CreateToggle(ControlResources(sprites));
        go.name = "Toggle_" + label.Replace(" ", "");
        SetLayerRecursive(go, 5);
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var background = (RectTransform)go.transform.Find("Background");
        background.anchorMin = background.anchorMax = new Vector2(0f, 0.5f);
        background.pivot = new Vector2(0f, 0.5f);
        background.anchoredPosition = Vector2.zero;
        background.sizeDelta = new Vector2(40f, 40f);
        background.GetComponent<Image>().color = new Color(0.2f, 0.23f, 0.3f);

        var check = (RectTransform)background.Find("Checkmark");
        check.anchorMin = Vector2.zero;
        check.anchorMax = Vector2.one;
        check.offsetMin = new Vector2(4f, 4f);
        check.offsetMax = new Vector2(-4f, -4f);
        check.GetComponent<Image>().color = AccentColor;

        var labelRt = (RectTransform)go.transform.Find("Label");
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(56f, 0f);
        labelRt.offsetMax = Vector2.zero;
        StyleText(labelRt.GetComponent<Text>(), label, 30, TextAnchor.MiddleLeft, Color.white, false);

        var toggle = go.GetComponent<Toggle>();
        var nav = toggle.navigation;
        nav.mode = Navigation.Mode.None;
        toggle.navigation = nav;
        go.AddComponent<UIButtonFeedback>();
        return toggle;
    }

    private static InventorySlotUI NewSlot(UISprites sprites, Transform parent, string name, Vector2 anchor,
        Vector2 position, float size, bool showKey)
    {
        RectTransform root = NewRect(name, parent, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
        Image background = AddImage(root, sprites.Rounded, new Color(0.1f, 0.12f, 0.16f, 0.7f), true);
        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        root.gameObject.AddComponent<UIButtonFeedback>();

        RectTransform content = NewStretch("Content", root);
        var punch = content.gameObject.AddComponent<UIPunch>();

        RectTransform selRt = NewStretch("Selection", content, -5f);
        Image selection = AddImage(selRt, sprites.Rounded, GoldColor);
        selection.fillCenter = false;
        selection.enabled = false;

        RectTransform iconRt = NewRect("Icon", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(size * 0.62f, size * 0.62f));
        Image icon = AddImage(iconRt, sprites.Circle, Color.white);

        Text iconText = NewText("IconText", iconRt, "?", Mathf.RoundToInt(size * 0.36f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), Color.white, true);

        Text count = NewText("Count", content, "", Mathf.RoundToInt(size * 0.22f), TextAnchor.LowerRight,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 4f), new Vector2(size, size * 0.3f), Color.white, true);

        Text key = null;
        if (showKey)
            key = NewText("Key", content, "", Mathf.RoundToInt(size * 0.2f), TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -4f), new Vector2(size * 0.4f, size * 0.3f), GoldColor, true);

        var slot = root.gameObject.AddComponent<InventorySlotUI>();
        SetRef(slot, "button", button);
        SetRef(slot, "background", background);
        SetRef(slot, "icon", icon);
        SetRef(slot, "iconText", iconText);
        SetRef(slot, "countText", count);
        if (key != null) SetRef(slot, "keyText", key);
        SetRef(slot, "selection", selection);
        SetRef(slot, "punch", punch);
        return slot;
    }

    private static Canvas NewCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = 5;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
            return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // ───────────────────────────── Общие окна ─────────────────────────────

    private static UIPanel CreateControlsWindow(UISprites sprites, Transform canvas, out Button backButton)
    {
        UIPanel panel = NewWindow(sprites, canvas, "ControlsPanel", "Управление", new Vector2(1000f, 760f),
            UIPanel.AnimationType.SlideFromBottom, out RectTransform window);

        string keys =
            "WASD / Стрелки\n" +
            "Пробел\n" +
            "1 / 2 / 3\n" +
            "I или Tab\n" +
            "Esc\n" +
            "Мышь";
        string actions =
            "Движение шара\n" +
            "Прыжок\n" +
            "Аптечка / Щит / Ускоритель\n" +
            "Открыть / закрыть инвентарь\n" +
            "Пауза / назад\n" +
            "Кнопки меню и ячейки инвентаря";

        Text keysText = NewText("Keys", window, keys, 32, TextAnchor.UpperRight,
            new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -140f), new Vector2(400f, 320f), GoldColor, true);
        keysText.lineSpacing = 1.25f;
        Text actionsText = NewText("Actions", window, actions, 32, TextAnchor.UpperLeft,
            new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(20f, -140f), new Vector2(440f, 320f), Color.white);
        actionsText.lineSpacing = 1.25f;

        NewText("Rules", window,
            "Соберите все монеты уровня. Бонусы дают предметы в инвентарь.\n" +
            "Красные препятствия наносят урон, а бочка взрывается при ударе на скорости от 7 м/с.",
            24, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -490f), new Vector2(880f, 110f), MutedText);

        backButton = NewButton(sprites, window, "Назад", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 35f), new Vector2(320f, 80f));
        return panel;
    }

    private static UIPanel CreateSettingsWindow(UISprites sprites, Transform canvas, out Button backButton)
    {
        UIPanel panel = NewWindow(sprites, canvas, "SettingsPanel", "Настройки", new Vector2(820f, 700f),
            UIPanel.AnimationType.SlideFromBottom, out RectTransform window);

        NewText("VolumeLabel", window, "Громкость", 32, TextAnchor.MiddleLeft,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-130f, -170f), new Vector2(400f, 50f));
        Text volumeValue = NewText("VolumeValue", window, "80%", 32, TextAnchor.MiddleRight,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(230f, -170f), new Vector2(160f, 50f), GoldColor, true);
        Slider volume = NewSlider(sprites, window, new Vector2(0f, -230f), new Vector2(620f, 30f));

        Toggle fullscreen = NewToggle(sprites, window, "Полноэкранный режим", new Vector2(0f, -320f), new Vector2(620f, 50f));
        Toggle hints = NewToggle(sprites, window, "Показывать подсказки", new Vector2(0f, -390f), new Vector2(620f, 50f));

        Button reset = NewButton(sprites, window, "Обнулить кошелёк", new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -490f), new Vector2(460f, 70f), new Color(0.45f, 0.2f, 0.2f), 26);

        backButton = NewButton(sprites, window, "Назад", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 35f), new Vector2(320f, 80f));

        var settings = panel.gameObject.AddComponent<SettingsPanel>();
        SetRef(settings, "volumeSlider", volume);
        SetRef(settings, "volumeValueText", volumeValue);
        SetRef(settings, "fullscreenToggle", fullscreen);
        SetRef(settings, "hintsToggle", hints);
        SetRef(settings, "resetWalletButton", reset);
        SetRef(settings, "resetWalletLabel", reset.GetComponentInChildren<Text>(true));
        return panel;
    }

    // ───────────────────────────── Игровой интерфейс ─────────────────────────────

    private static void CreateGameUI(UISprites sprites, PlayerHealth health, Wallet wallet, Inventory inventory,
        PlayerController controller, LevelStats stats)
    {
        EnsureEventSystem();
        Canvas canvas = NewCanvas("GameUI");
        Transform root = canvas.transform;
        Vector2 topLeft = new Vector2(0f, 1f);
        Vector2 topRight = new Vector2(1f, 1f);

        RectTransform hud = NewStretch("HUD", root);

        // ── Левая карточка: кошелёк, здоровье, эффекты
        RectTransform leftCard = NewRect("PlayerCard", hud, topLeft, topLeft, new Vector2(30f, -30f), new Vector2(500f, 185f));
        AddImage(leftCard, sprites.Rounded, CardColor);

        RectTransform coinIcon = NewRect("CoinIcon", leftCard, topLeft, new Vector2(0.5f, 0.5f), new Vector2(50f, -48f), new Vector2(52f, 52f));
        AddImage(coinIcon, sprites.Circle, GoldColor);
        var coinPunch = coinIcon.gameObject.AddComponent<UIPunch>();
        NewText("CoinSymbol", coinIcon, "$", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(52f, 52f), new Color(0.55f, 0.35f, 0f), true);

        Text coinsText = NewText("CoinsText", leftCard, "Монеты: 0", 40, TextAnchor.MiddleLeft,
            topLeft, new Vector2(0f, 0.5f), new Vector2(92f, -48f), new Vector2(390f, 60f), GoldColor, true);

        RectTransform healthBar = NewRect("HealthBar", leftCard, topLeft, topLeft, new Vector2(24f, -92f), new Vector2(452f, 38f));
        AddImage(healthBar, sprites.Rounded, new Color(0f, 0f, 0f, 0.6f));
        var healthPunch = healthBar.gameObject.AddComponent<UIPunch>();
        RectTransform trail = NewStretch("Trail", healthBar);
        AddImage(trail, sprites.Rounded, new Color(1f, 1f, 1f, 0.55f));
        RectTransform fill = NewStretch("Fill", healthBar);
        Image fillImage = AddImage(fill, sprites.Rounded, GoodColor);
        Text hpText = NewText("HealthText", healthBar, "HP: 100/100", 26, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(452f, 38f), Color.white, true);

        Text effectsText = NewText("EffectsText", leftCard, "", 24, TextAnchor.MiddleLeft,
            topLeft, topLeft, new Vector2(24f, -140f), new Vector2(452f, 34f));

        // ── Правая карточка: таймер и счётчики уровня
        RectTransform rightCard = NewRect("LevelCard", hud, topRight, topRight, new Vector2(-30f, -30f), new Vector2(400f, 170f));
        AddImage(rightCard, sprites.Rounded, CardColor);
        Text timerText = NewText("TimerText", rightCard, "00:00", 52, TextAnchor.MiddleRight,
            topRight, topRight, new Vector2(-24f, -12f), new Vector2(350f, 64f), Color.white, true);
        Text levelCoins = NewText("LevelCoinsText", rightCard, "Монеты уровня: 0/0", 28, TextAnchor.MiddleRight,
            topRight, topRight, new Vector2(-24f, -78f), new Vector2(350f, 36f), GoldColor);
        var levelCoinsPunch = levelCoins.gameObject.AddComponent<UIPunch>();
        Text bonusesText = NewText("BonusesText", rightCard, "Бонусы: 0/0", 28, TextAnchor.MiddleRight,
            topRight, topRight, new Vector2(-24f, -118f), new Vector2(350f, 36f), new Color(0.4f, 1f, 0.85f));
        var bonusesPunch = bonusesText.gameObject.AddComponent<UIPunch>();

        // ── Кнопки HUD
        Button hudPause = NewButton(sprites, hud, "II", topRight, topRight, new Vector2(-30f, -215f), new Vector2(76f, 70f), null, 32);
        Button hudInventory = NewButton(sprites, hud, "Инвентарь (I)", topRight, topRight, new Vector2(-120f, -215f), new Vector2(250f, 70f), null, 26);

        // ── Панель быстрого доступа
        RectTransform hotbar = NewRect("Hotbar", hud, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(380f, 125f));
        AddImage(hotbar, sprites.Rounded, CardColor);
        var hotbarSlots = new InventorySlotUI[3];
        for (int i = 0; i < 3; i++)
            hotbarSlots[i] = NewSlot(sprites, hotbar, "HotbarSlot" + (i + 1), new Vector2(0.5f, 0.5f),
                new Vector2((i - 1) * 120f, 0f), 105f, true);
        var hotbarUi = hotbar.gameObject.AddComponent<HotbarUI>();
        SetRef(hotbarUi, "inventory", inventory);
        SetRefArray(hotbarUi, "slots", hotbarSlots);

        // ── Подсказки
        Text hints = NewText("Hints", hud,
            "WASD — движение   Пробел — прыжок   1/2/3 — предметы\nI — инвентарь   Esc — пауза",
            22, TextAnchor.LowerLeft, Vector2.zero, Vector2.zero, new Vector2(30f, 30f), new Vector2(700f, 70f), MutedText);

        // ── Уведомления
        RectTransform toast = NewRect("Toast", hud, topRight, topRight, new Vector2(-30f, -305f), new Vector2(560f, 76f));
        AddImage(toast, sprites.Rounded, PanelColor);
        toast.gameObject.AddComponent<CanvasGroup>();
        var toastPanel = toast.gameObject.AddComponent<UIPanel>();
        SetEnum(toastPanel, "animationType", (int)UIPanel.AnimationType.SlideFromRight);
        SetFloat(toastPanel, "slideDistance", 650f);
        SetBool(toastPanel, "playSound", false);
        RectTransform accent = NewRect("Accent", toast, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(10f, 56f));
        Image accentImage = AddImage(accent, sprites.Rounded, GoldColor);
        Text toastText = NewText("Message", toast, "", 26, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(510f, 70f), Color.white, true);
        var toastUi = hud.gameObject.AddComponent<ToastUI>();
        SetRef(toastUi, "panel", toastPanel);
        SetRef(toastUi, "label", toastText);
        SetRef(toastUi, "accent", accentImage);

        // ── Компоненты HUD
        var walletUi = hud.gameObject.AddComponent<WalletUI>();
        SetRef(walletUi, "wallet", wallet);
        SetRef(walletUi, "label", coinsText);

        var healthUi = hud.gameObject.AddComponent<HealthUI>();
        SetRef(healthUi, "health", health);
        SetRef(healthUi, "label", hpText);
        SetRef(healthUi, "fillBar", fill);
        SetRef(healthUi, "fillImage", fillImage);
        SetRef(healthUi, "trailBar", trail);
        SetRef(healthUi, "punchTarget", healthPunch);

        var counters = hud.gameObject.AddComponent<CountersUI>();
        SetRef(counters, "stats", stats);
        SetRef(counters, "health", health);
        SetRef(counters, "controller", controller);
        SetRef(counters, "timerText", timerText);
        SetRef(counters, "levelCoinsText", levelCoins);
        SetRef(counters, "bonusesText", bonusesText);
        SetRef(counters, "effectsText", effectsText);
        SetRef(counters, "levelCoinsPunch", levelCoinsPunch);
        SetRef(counters, "bonusesPunch", bonusesPunch);

        RectTransform flyLayer = NewStretch("CoinFlyLayer", root);
        var coinFly = flyLayer.gameObject.AddComponent<CoinFlyUI>();
        SetRef(coinFly, "target", coinIcon);
        SetRef(coinFly, "targetPunch", coinPunch);
        SetRef(coinFly, "coinSprite", sprites.Circle);

        // ── Окно паузы
        UIPanel pause = NewWindow(sprites, root, "PausePanel", "Пауза", new Vector2(560f, 760f),
            UIPanel.AnimationType.FadeScale, out RectTransform pauseWindow);
        RectTransform pauseButtons = NewRect("Buttons", pauseWindow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -140f), new Vector2(440f, 580f));
        pauseButtons.gameObject.AddComponent<UIStaggerReveal>();
        string[] pauseLabels = { "Продолжить", "Инвентарь", "Управление", "Настройки", "Заново", "Главное меню" };
        var pauseBtns = new Button[pauseLabels.Length];
        for (int i = 0; i < pauseLabels.Length; i++)
        {
            Color c = i == 0 ? AccentColor : i == pauseLabels.Length - 1 ? new Color(0.45f, 0.22f, 0.22f) : ButtonColor;
            pauseBtns[i] = NewButton(sprites, pauseButtons, pauseLabels[i], new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -i * 95f), new Vector2(420f, 80f), c);
        }

        // ── Инвентарь
        UIPanel inventoryPanel = NewWindow(sprites, root, "InventoryPanel", "Инвентарь", new Vector2(1180f, 700f),
            UIPanel.AnimationType.FadeScale, out RectTransform invWindow);
        var invSlots = new InventorySlotUI[6];
        for (int i = 0; i < invSlots.Length; i++)
        {
            int col = i % 3, row = i / 3;
            invSlots[i] = NewSlot(sprites, invWindow, "Slot" + (i + 1), new Vector2(0f, 1f),
                new Vector2(130f + col * 165f, -230f - row * 165f), 145f, false);
        }
        Text capacityText = NewText("Capacity", invWindow, "Ячейки: 0/6", 24, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(55f, -480f), new Vector2(480f, 40f), MutedText);

        RectTransform details = NewRect("Details", invWindow, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-45f, -140f), new Vector2(560f, 420f));
        AddImage(details, sprites.Rounded, new Color(1f, 1f, 1f, 0.04f));
        var detailsPunch = details.gameObject.AddComponent<UIPunch>();
        RectTransform preview = NewRect("Preview", details, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -25f), new Vector2(130f, 130f));
        Image previewImage = AddImage(preview, sprites.Circle, Color.white);
        preview.gameObject.AddComponent<UIFloatAnimation>();
        Text previewText = NewText("PreviewText", preview, "?", 64, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f), Color.white, true);
        Text itemName = NewText("ItemName", details, "", 38, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(520f, 56f), GoldColor, true);
        Text itemDescription = NewText("ItemDescription", details, "", 26, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -235f), new Vector2(500f, 120f), MutedText);
        Button useButton = NewButton(sprites, details, "Использовать (Enter)", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 20f), new Vector2(400f, 70f), GoodColor * new Color(0.75f, 0.75f, 0.75f, 1f), 26);
        Button invClose = NewButton(sprites, invWindow, "Закрыть (I / Esc)", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 30f), new Vector2(380f, 74f), null, 26);

        var inventoryUi = inventoryPanel.gameObject.AddComponent<InventoryUI>();
        SetRef(inventoryUi, "inventory", inventory);
        SetRefArray(inventoryUi, "slots", invSlots);
        SetRef(inventoryUi, "itemNameText", itemName);
        SetRef(inventoryUi, "itemDescriptionText", itemDescription);
        SetRef(inventoryUi, "itemPreview", previewImage);
        SetRef(inventoryUi, "itemPreviewText", previewText);
        SetRef(inventoryUi, "useButton", useButton);
        SetRef(inventoryUi, "capacityText", capacityText);
        SetRef(inventoryUi, "detailsPunch", detailsPunch);

        // ── Управление и настройки
        UIPanel controls = CreateControlsWindow(sprites, root, out Button controlsBack);
        UIPanel settings = CreateSettingsWindow(sprites, root, out Button settingsBack);

        // ── Поражение
        UIPanel gameOver = NewWindow(sprites, root, "GameOverPanel", "Вы уничтожены!", new Vector2(760f, 560f),
            UIPanel.AnimationType.SlideFromTop, out RectTransform goWindow, DangerColor);
        Text goStats = NewText("Stats", goWindow, "", 32, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(640f, 200f), Color.white);
        goStats.lineSpacing = 1.2f;
        Button goRestart = NewButton(sprites, goWindow, "Заново", new Vector2(0.5f, 0f), new Vector2(1f, 0f),
            new Vector2(-15f, 40f), new Vector2(300f, 80f), AccentColor);
        Button goMenu = NewButton(sprites, goWindow, "В меню", new Vector2(0.5f, 0f), new Vector2(0f, 0f),
            new Vector2(15f, 40f), new Vector2(300f, 80f));

        // ── Победа
        UIPanel victory = NewWindow(sprites, root, "VictoryPanel", "Уровень пройден!", new Vector2(800f, 680f),
            UIPanel.AnimationType.FadeScale, out RectTransform vWindow, GoldColor);
        var stars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            float size = i == 1 ? 150f : 120f;
            RectTransform starRt = NewRect("Star" + (i + 1), vWindow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2((i - 1) * 165f, i == 1 ? -215f : -200f), new Vector2(size, size));
            stars[i] = AddImage(starRt, sprites.Star, new Color(1f, 1f, 1f, 0.15f));
        }
        Text vStats = NewText("Stats", vWindow, "", 32, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(680f, 180f), Color.white);
        vStats.lineSpacing = 1.2f;
        Button vRestart = NewButton(sprites, vWindow, "Ещё раз", new Vector2(0.5f, 0f), new Vector2(1f, 0f),
            new Vector2(-15f, 40f), new Vector2(300f, 80f), AccentColor);
        Button vMenu = NewButton(sprites, vWindow, "В меню", new Vector2(0.5f, 0f), new Vector2(0f, 0f),
            new Vector2(15f, 40f), new Vector2(300f, 80f));

        // ── Контроллер экранов
        var ui = canvas.gameObject.AddComponent<GameUIController>();
        SetRef(ui, "health", health);
        SetRef(ui, "inventory", inventory);
        SetRef(ui, "stats", stats);
        SetRef(ui, "pausePanel", pause);
        SetRef(ui, "inventoryPanel", inventoryPanel);
        SetRef(ui, "controlsPanel", controls);
        SetRef(ui, "settingsPanel", settings);
        SetRef(ui, "gameOverPanel", gameOver);
        SetRef(ui, "victoryPanel", victory);
        SetRef(ui, "hintsRoot", hints.gameObject);
        SetRef(ui, "hudPauseButton", hudPause);
        SetRef(ui, "hudInventoryButton", hudInventory);
        SetRef(ui, "resumeButton", pauseBtns[0]);
        SetRef(ui, "pauseInventoryButton", pauseBtns[1]);
        SetRef(ui, "pauseControlsButton", pauseBtns[2]);
        SetRef(ui, "pauseSettingsButton", pauseBtns[3]);
        SetRef(ui, "pauseRestartButton", pauseBtns[4]);
        SetRef(ui, "pauseMenuButton", pauseBtns[5]);
        SetRef(ui, "inventoryCloseButton", invClose);
        SetRef(ui, "controlsBackButton", controlsBack);
        SetRef(ui, "settingsBackButton", settingsBack);
        SetRef(ui, "gameOverRestartButton", goRestart);
        SetRef(ui, "gameOverMenuButton", goMenu);
        SetRef(ui, "victoryRestartButton", vRestart);
        SetRef(ui, "victoryMenuButton", vMenu);
        SetRef(ui, "gameOverStatsText", goStats);
        SetRef(ui, "victoryStatsText", vStats);
        SetRefArray(ui, "victoryStars", stars);
    }

    // ───────────────────────────── Главное меню ─────────────────────────────

    private static void BuildMenuScene(UISprites sprites, GameObject coinPrefab, GameObject bonusPrefab, GameObject barrelPrefab)
    {
        // 3D-фон: площадка с бонусом, монетами и бочкой; камера медленно облетает её
        GameObject light = GameObject.Find("Directional Light");
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(45f, -40f, 0f);
            light.GetComponent<Light>().shadows = LightShadows.Soft;
        }

        Vector3 center = new Vector3(3f, 0f, 0f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.name = "Platform";
        ground.transform.position = center + Vector3.down * 0.05f;
        ground.transform.localScale = new Vector3(9f, 0.05f, 9f);
        ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Ground.mat");
        // CapsuleCollider сплющенного цилиндра превращается в большую сферу — заменяем на MeshCollider
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.AddComponent<MeshCollider>();

        Spawn(bonusPrefab, center, null);
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            Spawn(coinPrefab, center + new Vector3(Mathf.Cos(a) * 2.8f, 0.9f, Mathf.Sin(a) * 2.8f), null);
        }
        Spawn(barrelPrefab, center + new Vector3(2.2f, 0.7f, 2.6f), null);
        Spawn(barrelPrefab, center + new Vector3(-2.6f, 0.7f, 2.2f), null);

        var pivot = new GameObject("CameraPivot");
        pivot.transform.position = center;
        var rotator = pivot.AddComponent<SimpleRotator>();
        SetVector3(rotator, "degreesPerSecond", new Vector3(0f, 8f, 0f));

        GameObject camGo = GameObject.Find("Main Camera");
        if (camGo != null)
        {
            camGo.transform.SetParent(pivot.transform, true);
            camGo.transform.position = center + new Vector3(-4.5f, 3.2f, -7.5f);
            camGo.transform.LookAt(center + new Vector3(-1.5f, 0.8f, 0f));
        }

        // Интерфейс
        EnsureEventSystem();
        Canvas canvas = NewCanvas("MenuUI");
        Transform root = canvas.transform;

        RectTransform band = NewRect("LeftBand", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(820f, 2400f));
        AddImage(band, null, new Color(0.03f, 0.05f, 0.09f, 0.72f));

        RectTransform mainRoot = NewRect("MainPanel", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(640f, 900f));
        mainRoot.gameObject.AddComponent<CanvasGroup>();
        var mainPanel = mainRoot.gameObject.AddComponent<UIPanel>();
        SetEnum(mainPanel, "animationType", (int)UIPanel.AnimationType.SlideFromLeft);
        SetFloat(mainPanel, "slideDistance", 200f);
        SetFloat(mainPanel, "duration", 0.35f);
        SetBool(mainPanel, "playSound", false);

        Text title = NewText("Title", mainRoot, "ШАР-СОБИРАТЕЛЬ", 88, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -40f), new Vector2(760f, 120f), Color.white, true);
        var titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.8f);
        titleOutline.effectDistance = new Vector2(3f, -3f);
        title.gameObject.AddComponent<UIFloatAnimation>();
        NewText("Subtitle", mainRoot, "Собери монеты • Найди бонусы • Не взорвись", 30, TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -160f), new Vector2(700f, 50f), MutedText);

        RectTransform buttons = NewRect("Buttons", mainRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -260f), new Vector2(460f, 440f));
        buttons.gameObject.AddComponent<UIStaggerReveal>();
        Button play = NewButton(sprites, buttons, "Играть", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(440f, 96f), AccentColor, 40);
        Button controlsBtn = NewButton(sprites, buttons, "Управление", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -115f), new Vector2(440f, 82f));
        Button settingsBtn = NewButton(sprites, buttons, "Настройки", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -215f), new Vector2(440f, 82f));
        Button quit = NewButton(sprites, buttons, "Выход", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -315f), new Vector2(440f, 82f), new Color(0.45f, 0.22f, 0.22f));

        RectTransform walletCard = NewRect("WalletCard", mainRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 60f), new Vector2(440f, 70f));
        AddImage(walletCard, sprites.Rounded, CardColor);
        RectTransform walletIcon = NewRect("CoinIcon", walletCard, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(38f, 0f), new Vector2(44f, 44f));
        AddImage(walletIcon, sprites.Circle, GoldColor);
        walletIcon.gameObject.AddComponent<UIFloatAnimation>();
        Text walletText = NewText("WalletText", walletCard, "В кошельке: 0 мон.", 28, TextAnchor.MiddleLeft,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(75f, 0f), new Vector2(350f, 60f), GoldColor, true);

        NewText("Version", root, "Практика • Unity 2022.3 LTS • v1.1", 22, TextAnchor.LowerRight,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 25f), new Vector2(600f, 40f), MutedText);

        UIPanel controls = CreateControlsWindow(sprites, root, out Button controlsBack);
        UIPanel settings = CreateSettingsWindow(sprites, root, out Button settingsBack);

        RectTransform fade = NewStretch("FadeOverlay", root);
        AddImage(fade, null, Color.black, true);
        var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 1f;

        var menu = canvas.gameObject.AddComponent<MainMenuController>();
        SetRef(menu, "mainPanel", mainPanel);
        SetRef(menu, "controlsPanel", controls);
        SetRef(menu, "settingsPanel", settings);
        SetRef(menu, "fadeOverlay", fadeGroup);
        SetRef(menu, "playButton", play);
        SetRef(menu, "controlsButton", controlsBtn);
        SetRef(menu, "settingsButton", settingsBtn);
        SetRef(menu, "quitButton", quit);
        SetRef(menu, "controlsBackButton", controlsBack);
        SetRef(menu, "settingsBackButton", settingsBack);
        SetRef(menu, "walletText", walletText);
    }

    // ───────────────────────────── Массивы в сериализованных полях ─────────────────────────────

    private static void SetRefArray<T>(Object target, string field, T[] values) where T : Object
    {
        var so = new SerializedObject(target);
        SerializedProperty p = FindProp(so, field);
        if (p == null) return;
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
