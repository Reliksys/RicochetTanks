using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Логика главного меню. Вешается на Canvas сцены MainMenu.
// Кнопки и тексты ищутся ПО ИМЕНАМ объектов (вёрстку делает UI-шник). Если чего-то нет — в Console будет предупреждение.
// Имена: BestScoreText, QuickPlayButton, CustomButton, CustomPanel, MapPrevButton, MapNextButton, MapNameText,
//        MapPreviewImage, DifficultyEasyButton, DifficultyNormalButton, DifficultyHardButton, DifficultyCustomButton,
//        ModifierList, TotalMultiplierText, PlayButton, BackButton. Необязательно: DifficultyDescriptionText, MapDescriptionText.
public class MainMenuController : MonoBehaviour
{
    [SerializeField] GameDatabase database;
    [Tooltip("Префаб ModifierToggle: на корне Toggle, внутри тексты NameText, DescriptionText, MultiplierText")]
    [SerializeField] Toggle modifierTogglePrefab;
    [SerializeField] string gameSceneName = "Game";
    [SerializeField] AudioClip clickSound;

    [Header("Цвета")]
    [SerializeField] Color selectedButtonColor = new Color(1f, 0.78f, 0.25f);
    [SerializeField] Color normalButtonColor = Color.white;
    [SerializeField] Color harderColor = new Color(1f, 0.55f, 0.3f);  // множитель > 1
    [SerializeField] Color easierColor = new Color(0.55f, 0.8f, 1f);  // множитель < 1

    TMP_Text bestScoreText, mapNameText, mapDescriptionText, totalMultiplierText, difficultyDescriptionText;
    Image mapPreviewImage;
    GameObject customPanel;
    Transform modifierListContent;
    Button easyButton, normalButton, hardButton, customButton;

    readonly List<MapData> maps = new List<MapData>();
    readonly List<ModifierData> selected = new List<ModifierData>();
    readonly Dictionary<ModifierData, Toggle> toggles = new Dictionary<ModifierData, Toggle>();
    DifficultyPreset selectedPreset; // null = свой набор
    int mapIndex;
    AudioSource audioSource;
    bool ready; // чтобы при открытии меню не щёлкал звук

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        bestScoreText = Find<TMP_Text>("BestScoreText");
        mapNameText = Find<TMP_Text>("MapNameText");
        mapDescriptionText = Find<TMP_Text>("MapDescriptionText", optional: true);
        totalMultiplierText = Find<TMP_Text>("TotalMultiplierText");
        difficultyDescriptionText = Find<TMP_Text>("DifficultyDescriptionText", optional: true);
        mapPreviewImage = Find<Image>("MapPreviewImage", optional: true);
        Transform panel = FindTransform("CustomPanel");
        customPanel = panel != null ? panel.gameObject : null;

        Transform list = FindTransform("ModifierList");
        if (list != null)
        {
            // У Scroll View элементы кладутся в Viewport/Content, у обычной панели — прямо в неё
            Transform content = GameBootstrap.FindDeep(list, "Content");
            modifierListContent = content != null ? content : list;
        }

        Bind("QuickPlayButton", OnQuickPlay);
        Bind("CustomButton", () => SetCustomPanel(true));
        Bind("BackButton", () => SetCustomPanel(false));
        Bind("PlayButton", OnPlay);
        Bind("MapPrevButton", () => ChangeMap(-1));
        Bind("MapNextButton", () => ChangeMap(+1));
        easyButton = Bind("DifficultyEasyButton", () => SelectPreset(PresetAt(0)));
        normalButton = Bind("DifficultyNormalButton", () => SelectPreset(PresetAt(1)));
        hardButton = Bind("DifficultyHardButton", () => SelectPreset(PresetAt(2)));
        customButton = Bind("DifficultyCustomButton", () => SelectPreset(null));
    }

    void Start()
    {
        if (database == null)
        {
            Debug.LogError("[Menu] Не указан GameDatabase в MainMenuController");
            return;
        }

        maps.Clear();
        maps.AddRange(database.maps.FindAll(m => m != null && m.prefab != null));

        BuildModifierToggles();
        SelectPreset(database.defaultPreset);
        ShowMap();
        SetCustomPanel(false);

        int best = PlayerPrefs.GetInt("BestScore", 0);
        if (bestScoreText != null) bestScoreText.text = best > 0 ? $"Рекорд: {best}" : "Рекорда пока нет";
        ready = true;
    }

    // ---------- Кнопки ----------
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

    void SetCustomPanel(bool open)
    {
        if (customPanel != null) customPanel.SetActive(open);
        Click();
    }

    // ---------- Карты ----------
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
        if (mapNameText != null) mapNameText.text = map != null ? map.displayName : "Тестовая арена";
        if (mapDescriptionText != null) mapDescriptionText.text = map != null ? map.description : "";
        if (mapPreviewImage != null)
        {
            mapPreviewImage.sprite = map != null ? map.preview : null;
            mapPreviewImage.enabled = mapPreviewImage.sprite != null;
        }
    }

    // ---------- Сложность и модификаторы ----------
    DifficultyPreset PresetAt(int index) =>
        index >= 0 && index < database.presets.Count ? database.presets[index] : null;

    void SelectPreset(DifficultyPreset preset)
    {
        selectedPreset = preset;
        selected.Clear();
        if (preset != null)
            foreach (ModifierData m in preset.modifiers)
                if (m != null) selected.Add(m);

        foreach (var pair in toggles)
            pair.Value.SetIsOnWithoutNotify(selected.Contains(pair.Key));

        Refresh();
        Click();
    }

    void BuildModifierToggles()
    {
        toggles.Clear();
        if (modifierTogglePrefab == null || modifierListContent == null)
        {
            Debug.LogWarning("[Menu] Нет префаба ModifierToggle или объекта ModifierList — список модификаторов не построен");
            return;
        }

        foreach (Transform child in modifierListContent) Destroy(child.gameObject);

        foreach (ModifierData mod in database.modifiers)
        {
            if (mod == null) continue;
            Toggle t = Instantiate(modifierTogglePrefab, modifierListContent);
            t.name = "Toggle_" + mod.name;
            SetText(t.transform, "NameText", mod.displayName);
            SetText(t.transform, "DescriptionText", mod.description);
            TMP_Text mult = FindIn<TMP_Text>(t.transform, "MultiplierText");
            if (mult != null)
            {
                mult.text = mod.MultiplierText;
                mult.color = mod.scoreMultiplier > 1f ? harderColor : mod.scoreMultiplier < 1f ? easierColor : Color.white;
            }
            t.SetIsOnWithoutNotify(false);
            ModifierData captured = mod;
            t.onValueChanged.AddListener(on => OnModifierToggled(captured, on));
            toggles[mod] = t;
        }
    }

    void OnModifierToggled(ModifierData mod, bool on)
    {
        if (on)
        {
            // Снимаем несовместимые (та же группа, например две брони)
            if (!string.IsNullOrEmpty(mod.group))
            {
                for (int i = selected.Count - 1; i >= 0; i--)
                {
                    ModifierData other = selected[i];
                    if (other != mod && other.group == mod.group)
                    {
                        selected.RemoveAt(i);
                        if (toggles.TryGetValue(other, out Toggle ot)) ot.SetIsOnWithoutNotify(false);
                    }
                }
            }
            if (!selected.Contains(mod)) selected.Add(mod);
        }
        else
        {
            selected.Remove(mod);
        }

        selectedPreset = null; // ручной выбор = свой набор
        Refresh();
        Click();
    }

    void Refresh()
    {
        float mult = ModifierApplier.TotalMultiplier(selected);
        if (totalMultiplierText != null) totalMultiplierText.text = $"Множитель очков: ×{mult:0.##}";

        if (difficultyDescriptionText != null)
            difficultyDescriptionText.text = selectedPreset != null
                ? selectedPreset.description
                : "Свой набор: выбери модификаторы сам";

        Highlight(easyButton, selectedPreset != null && selectedPreset == PresetAt(0));
        Highlight(normalButton, selectedPreset != null && selectedPreset == PresetAt(1));
        Highlight(hardButton, selectedPreset != null && selectedPreset == PresetAt(2));
        Highlight(customButton, selectedPreset == null);
    }

    void Highlight(Button button, bool on)
    {
        if (button == null || button.image == null) return;
        button.image.color = on ? selectedButtonColor : normalButtonColor;
    }

    // ---------- Помощники ----------
    void Click()
    {
        if (ready && clickSound != null) audioSource.PlayOneShot(clickSound);
    }

    Button Bind(string objectName, UnityEngine.Events.UnityAction action)
    {
        Button b = Find<Button>(objectName);
        if (b != null) b.onClick.AddListener(action);
        return b;
    }

    Transform FindTransform(string objectName, bool optional = false)
    {
        Transform t = GameBootstrap.FindDeep(transform, objectName);
        if (t == null && !optional) Debug.LogWarning($"[Menu] Не найден объект «{objectName}» внутри {name}");
        return t;
    }

    T Find<T>(string objectName, bool optional = false) where T : Component
    {
        Transform t = FindTransform(objectName, optional);
        if (t == null) return null;
        T c = t.GetComponent<T>();
        if (c == null && !optional) Debug.LogWarning($"[Menu] У «{objectName}» нет компонента {typeof(T).Name}");
        return c;
    }

    static T FindIn<T>(Transform root, string objectName) where T : Component
    {
        Transform t = GameBootstrap.FindDeep(root, objectName);
        return t != null ? t.GetComponent<T>() : null;
    }

    static void SetText(Transform root, string objectName, string value)
    {
        TMP_Text text = FindIn<TMP_Text>(root, objectName);
        if (text != null) text.text = value;
    }
}
