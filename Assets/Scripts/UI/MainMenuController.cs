using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Главное меню, построенное КОДОМ. Вешается на любой пустой объект в сцене MainMenu.
// Экран 1: название, цель игры, рекорд, «Быстрая игра», «Настроить бой», подсказка управления.
// Экран 2: выбор карты, сложности (пресеты) и модификаторов с множителем очков, «Играть».
public class MainMenuController : MonoBehaviour
{
    [SerializeField] GameDatabase database;
    [Tooltip("Фон меню, например Art/ground_shaker_asset/menu_bg_1200x800")]
    [SerializeField] Sprite background;
    [SerializeField] AudioClip clickSound;
    [SerializeField] string gameSceneName = "Game";

    [Header("Тексты")]
    [SerializeField] string title = "РИКОШЕТ";
    [SerializeField] string subtitle = "танковая арена";
    [TextArea(2, 4)]
    [SerializeField] string goalText = "Уничтожай волны вражеских танков и выживи как можно дольше.\nСнаряды отскакивают от стен — убийство рикошетом даёт больше очков.";

    RectTransform mainScreen, customScreen;
    TextMeshProUGUI mapNameText, mapDescText, presetDescText, totalText;
    Image mapPreview;
    Button mapPrev, mapNext;

    readonly List<MapData> maps = new List<MapData>();
    readonly List<ModifierData> selected = new List<ModifierData>();
    readonly List<(DifficultyPreset preset, Button button)> presetButtons = new List<(DifficultyPreset, Button)>();
    Button customPresetButton;
    readonly Dictionary<ModifierData, (Image bg, Image check)> cards = new Dictionary<ModifierData, (Image, Image)>();

    DifficultyPreset selectedPreset; // null = свой набор
    int mapIndex;
    AudioSource audioSource;
    bool ready;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (database == null)
        {
            Debug.LogError("[Menu] Не указан GameDatabase в MainMenuController");
            return;
        }
        maps.AddRange(database.maps.FindAll(m => m != null && m.prefab != null));

        Canvas canvas = UIKit.CreateCanvas("MenuCanvas");
        BuildBackground(canvas.transform);
        mainScreen = BuildMainScreen(canvas.transform);
        customScreen = BuildCustomScreen(canvas.transform);
        BuildControlsBar(canvas.transform);

        SelectPreset(database.defaultPreset);
        ShowMap();
        Show(mainScreen);
        ready = true;
    }

    // ================= Построение =================

    void BuildBackground(Transform root)
    {
        Image bg = UIKit.Box(root, "Background", background != null ? new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(0.12f, 0.17f, 0.1f, 1f));
        bg.sprite = background;
        bg.preserveAspect = false;
        UIKit.Stretch(bg);
    }

    RectTransform BuildMainScreen(Transform root)
    {
        RectTransform screen = UIKit.Node("MainScreen", root);
        UIKit.Stretch(screen);

        Image panel = UIKit.Box(screen, "Panel", UIKit.PanelColor);
        UIKit.Place(panel, UIKit.Center, UIKit.Center, new Vector2(0, 30), new Vector2(820, 760));
        Transform p = panel.transform;

        var t = UIKit.Label(p, title, 120, UIKit.Accent, TextAlignmentOptions.Center, bold: true);
        UIKit.Place(t, UIKit.Top, UIKit.Top, new Vector2(0, -30), new Vector2(780, 140));
        t.characterSpacing = 12;

        var st = UIKit.Label(p, subtitle, 32, UIKit.TextDim);
        UIKit.Place(st, UIKit.Top, UIKit.Top, new Vector2(0, -165), new Vector2(780, 44));

        var goal = UIKit.Label(p, goalText, 25, UIKit.TextColor);
        UIKit.Place(goal, UIKit.Top, UIKit.Top, new Vector2(0, -225), new Vector2(720, 100));

        int best = PlayerPrefs.GetInt("BestScore", 0);
        var bestText = UIKit.Label(p, best > 0 ? $"Рекорд: {best}" : "Рекорда пока нет — поставь первый!", 30, UIKit.Accent);
        UIKit.Place(bestText, UIKit.Top, UIKit.Top, new Vector2(0, -340), new Vector2(720, 44));

        Button quick = UIKit.Btn(p, "БЫСТРАЯ ИГРА", 46, UIKit.Accent, UIKit.Dark, OnQuickPlay);
        UIKit.Place(quick, UIKit.Top, UIKit.Top, new Vector2(0, -410), new Vector2(560, 110));

        var quickHint = UIKit.Label(p, "случайная карта и 1–2 случайных модификатора", 22, UIKit.TextDim);
        UIKit.Place(quickHint, UIKit.Top, UIKit.Top, new Vector2(0, -526), new Vector2(720, 32));

        Button custom = UIKit.Btn(p, "НАСТРОИТЬ БОЙ", 34, UIKit.ButtonColor, UIKit.TextColor, () => Show(customScreen));
        UIKit.Place(custom, UIKit.Top, UIKit.Top, new Vector2(0, -580), new Vector2(560, 84));

        var customHint = UIKit.Label(p, "карта, сложность и модификаторы с множителем очков", 22, UIKit.TextDim);
        UIKit.Place(customHint, UIKit.Top, UIKit.Top, new Vector2(0, -670), new Vector2(720, 32));

        return screen;
    }

    RectTransform BuildCustomScreen(Transform root)
    {
        RectTransform screen = UIKit.Node("CustomScreen", root);
        UIKit.Stretch(screen);

        Image panel = UIKit.Box(screen, "Panel", UIKit.PanelColor);
        UIKit.Stretch(panel, 50, 50, 40, 110);
        Transform p = panel.transform;

        var header = UIKit.Label(p, "НАСТРОЙКА БОЯ", 54, UIKit.Accent, TextAlignmentOptions.Center, bold: true);
        UIKit.Place(header, UIKit.Top, UIKit.Top, new Vector2(0, -16), new Vector2(900, 70));

        // ---------- Левая колонка: карта и сложность ----------
        RectTransform left = UIKit.Node("Left", p);
        UIKit.Place(left, UIKit.TopLeft, UIKit.TopLeft, new Vector2(40, -100), new Vector2(620, 760));

        var mapLabel = UIKit.Label(left, "КАРТА", 30, UIKit.TextDim, TextAlignmentOptions.Left, bold: true);
        UIKit.Place(mapLabel, UIKit.TopLeft, UIKit.TopLeft, Vector2.zero, new Vector2(620, 40));

        mapPrev = UIKit.Btn(left, "<", 40, UIKit.ButtonColor, UIKit.TextColor, () => ChangeMap(-1));
        UIKit.Place(mapPrev, UIKit.TopLeft, UIKit.TopLeft, new Vector2(0, -50), new Vector2(70, 64));
        mapNext = UIKit.Btn(left, ">", 40, UIKit.ButtonColor, UIKit.TextColor, () => ChangeMap(+1));
        UIKit.Place(mapNext, UIKit.TopRight, UIKit.TopRight, new Vector2(0, -50), new Vector2(70, 64));
        mapNameText = UIKit.Label(left, "", 34, UIKit.TextColor, TextAlignmentOptions.Center, bold: true);
        UIKit.Place(mapNameText, UIKit.Top, UIKit.Top, new Vector2(0, -50), new Vector2(460, 64));

        Image previewFrame = UIKit.Box(left, "PreviewFrame", UIKit.Dark);
        UIKit.Place(previewFrame, UIKit.Top, UIKit.Top, new Vector2(0, -126), new Vector2(620, 330));
        mapPreview = UIKit.Picture(previewFrame.transform, "Preview", null);
        UIKit.Stretch(mapPreview, 6, 6, 6, 6);

        mapDescText = UIKit.Label(left, "", 22, UIKit.TextDim);
        UIKit.Place(mapDescText, UIKit.Top, UIKit.Top, new Vector2(0, -462), new Vector2(620, 56));

        var diffLabel = UIKit.Label(left, "СЛОЖНОСТЬ", 30, UIKit.TextDim, TextAlignmentOptions.Left, bold: true);
        UIKit.Place(diffLabel, UIKit.TopLeft, UIKit.TopLeft, new Vector2(0, -530), new Vector2(620, 40));

        RectTransform row = UIKit.Node("Presets", left);
        UIKit.Place(row, UIKit.TopLeft, UIKit.TopLeft, new Vector2(0, -578), new Vector2(620, 64));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        foreach (DifficultyPreset preset in database.presets)
        {
            if (preset == null) continue;
            DifficultyPreset captured = preset;
            Button b = UIKit.Btn(row, preset.displayName, 26, UIKit.ButtonColor, UIKit.TextColor, () => { SelectPreset(captured); Click(); });
            presetButtons.Add((preset, b));
        }
        customPresetButton = UIKit.Btn(row, "Свой", 26, UIKit.ButtonColor, UIKit.TextColor, () => { SelectPreset(null); Click(); });

        presetDescText = UIKit.Label(left, "", 22, UIKit.TextDim);
        UIKit.Place(presetDescText, UIKit.TopLeft, UIKit.TopLeft, new Vector2(0, -652), new Vector2(620, 70));

        // ---------- Правая колонка: модификаторы ----------
        RectTransform right = UIKit.Node("Right", p);
        right.anchorMin = new Vector2(0, 0);
        right.anchorMax = new Vector2(1, 1);
        right.pivot = new Vector2(0, 1);
        right.offsetMin = new Vector2(700, 140);
        right.offsetMax = new Vector2(-40, -100);

        var modLabel = UIKit.Label(right, "МОДИФИКАТОРЫ", 30, UIKit.TextDim, TextAlignmentOptions.Left, bold: true);
        UIKit.Place(modLabel, UIKit.TopLeft, UIKit.TopLeft, Vector2.zero, new Vector2(500, 40));
        var modHint = UIKit.Label(right, "<color=#FF9E5C>×1.3</color> — сложнее, больше очков    <color=#8FCCFF>×0.7</color> — легче, меньше очков", 22, UIKit.TextDim, TextAlignmentOptions.Right);
        UIKit.Place(modHint, UIKit.TopRight, UIKit.TopRight, Vector2.zero, new Vector2(620, 40));

        RectTransform grid = UIKit.Node("Grid", right);
        grid.anchorMin = new Vector2(0, 0);
        grid.anchorMax = new Vector2(1, 1);
        grid.offsetMin = new Vector2(0, 0);
        grid.offsetMax = new Vector2(0, -52);
        var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(530, 100);
        gl.spacing = new Vector2(14, 12);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gl.constraintCount = 2;

        foreach (ModifierData mod in database.modifiers)
            if (mod != null) BuildModifierCard(grid, mod);

        // ---------- Низ: множитель и кнопки ----------
        totalText = UIKit.Label(p, "", 40, UIKit.Accent, TextAlignmentOptions.Left, bold: true);
        UIKit.Place(totalText, UIKit.BottomLeft, UIKit.BottomLeft, new Vector2(40, 30), new Vector2(700, 70));

        Button back = UIKit.Btn(p, "НАЗАД", 30, UIKit.ButtonColor, UIKit.TextColor, () => Show(mainScreen));
        UIKit.Place(back, UIKit.BottomRight, UIKit.BottomRight, new Vector2(-420, 30), new Vector2(240, 80));
        Button play = UIKit.Btn(p, "ИГРАТЬ", 42, UIKit.Accent, UIKit.Dark, OnPlay);
        UIKit.Place(play, UIKit.BottomRight, UIKit.BottomRight, new Vector2(-40, 26), new Vector2(360, 90));

        return screen;
    }

    void BuildModifierCard(Transform grid, ModifierData mod)
    {
        Image bg = UIKit.Box(grid, "Mod_" + mod.name, UIKit.PanelLight, raycast: true);
        var button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.None;
        ModifierData captured = mod;
        button.onClick.AddListener(() => ToggleModifier(captured));

        Image check = UIKit.Box(bg.transform, "Check", UIKit.Dark);
        UIKit.Place(check, UIKit.Left, UIKit.Left, new Vector2(12, 0), new Vector2(22, 22));

        Image icon = UIKit.Picture(bg.transform, "Icon", mod.icon);
        UIKit.Place(icon, UIKit.Left, UIKit.Left, new Vector2(44, 0), new Vector2(76, 76));

        float textX = mod.icon != null ? 132 : 48;
        var name = UIKit.Label(bg.transform, mod.displayName, 27, UIKit.TextColor, TextAlignmentOptions.TopLeft, bold: true);
        UIKit.Place(name, UIKit.TopLeft, UIKit.TopLeft, new Vector2(textX, -8), new Vector2(300, 36));

        var desc = UIKit.Label(bg.transform, mod.description, 18, UIKit.TextDim, TextAlignmentOptions.TopLeft);
        UIKit.Place(desc, UIKit.TopLeft, UIKit.TopLeft, new Vector2(textX, -44), new Vector2(530 - textX - 90, 52));

        Color multColor = mod.scoreMultiplier > 1f ? UIKit.Harder : mod.scoreMultiplier < 1f ? UIKit.Easier : UIKit.TextColor;
        var mult = UIKit.Label(bg.transform, mod.MultiplierText, 32, multColor, TextAlignmentOptions.Right, bold: true);
        UIKit.Place(mult, UIKit.Right, UIKit.Right, new Vector2(-14, 0), new Vector2(90, 50));

        cards[mod] = (bg, check);
    }

    void BuildControlsBar(Transform root)
    {
        Image bar = UIKit.Box(root, "ControlsBar", UIKit.PanelColor);
        UIKit.Place(bar, UIKit.Bottom, UIKit.Bottom, new Vector2(0, 20), new Vector2(1500, 64));
        var text = UIKit.Label(bar.transform,
            "<color=#F5C44D>WASD</color> — ехать     <color=#F5C44D>Мышь</color> — целиться     <color=#F5C44D>ЛКМ / Пробел</color> — огонь     <color=#F5C44D>R</color> — заново     <color=#F5C44D>Esc</color> — в меню",
            26, UIKit.TextColor);
        UIKit.Stretch(text);
    }

    // ================= Логика =================

    void Show(RectTransform screen)
    {
        mainScreen.gameObject.SetActive(screen == mainScreen);
        customScreen.gameObject.SetActive(screen == customScreen);
        Click();
    }

    void OnQuickPlay()
    {
        RunConfig.SetQuickPlay(database);
        LoadGame();
    }

    void OnPlay()
    {
        string mode = selectedPreset != null ? selectedPreset.displayName : "Свой набор";
        RunConfig.Set(CurrentMap, selected, mode);
        LoadGame();
    }

    void LoadGame()
    {
        Click();
        SceneManager.LoadScene(gameSceneName);
    }

    MapData CurrentMap => maps.Count > 0 ? maps[mapIndex] : null;

    void ChangeMap(int delta)
    {
        if (maps.Count == 0) return;
        mapIndex = (mapIndex + delta + maps.Count) % maps.Count;
        ShowMap();
        Click();
    }

    void ShowMap()
    {
        MapData map = CurrentMap;
        mapNameText.text = map != null ? map.displayName : "Тестовая арена";
        mapDescText.text = map != null ? map.description : "";
        mapPreview.sprite = map != null ? map.preview : null;
        mapPreview.enabled = mapPreview.sprite != null;
        mapPrev.interactable = mapNext.interactable = maps.Count > 1;
    }

    void SelectPreset(DifficultyPreset preset)
    {
        selectedPreset = preset;
        if (preset != null)
        {
            selected.Clear();
            foreach (ModifierData m in preset.modifiers)
                if (m != null) selected.Add(m);
        }
        Refresh();
    }

    void ToggleModifier(ModifierData mod)
    {
        if (selected.Contains(mod))
        {
            selected.Remove(mod);
        }
        else
        {
            // Снимаем несовместимые (одна группа, например две брони)
            if (!string.IsNullOrEmpty(mod.group))
                selected.RemoveAll(m => m != null && m.group == mod.group);
            selected.Add(mod);
        }
        selectedPreset = null; // ручной выбор = свой набор
        Refresh();
        Click();
    }

    void Refresh()
    {
        foreach (var pair in cards)
        {
            bool on = selected.Contains(pair.Key);
            pair.Value.bg.color = on ? UIKit.AccentDim : UIKit.PanelLight;
            pair.Value.check.color = on ? UIKit.Accent : UIKit.Dark;
        }

        foreach (var (preset, button) in presetButtons)
            Highlight(button, preset == selectedPreset);
        Highlight(customPresetButton, selectedPreset == null);

        presetDescText.text = selectedPreset != null
            ? selectedPreset.description
            : "Свой набор: нажимай на модификаторы справа.";

        float mult = ModifierApplier.TotalMultiplier(selected);
        totalText.text = $"Множитель очков: ×{mult:0.##}";
    }

    static void Highlight(Button b, bool on)
    {
        if (b == null) return;
        b.image.color = on ? UIKit.Accent : UIKit.ButtonColor;
        TextMeshProUGUI label = UIKit.ButtonLabel(b);
        if (label != null) label.color = on ? UIKit.Dark : UIKit.TextColor;
    }

    void Click()
    {
        if (ready && clickSound != null) audioSource.PlayOneShot(clickSound);
    }
}
