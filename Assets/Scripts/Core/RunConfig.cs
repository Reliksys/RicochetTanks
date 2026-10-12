using System.Collections.Generic;
using UnityEngine;

// Настройки текущего забега: какая карта и какие модификаторы.
// Меню заполняет, сцена Game читает. Static переживает загрузку сцены.
public static class RunConfig
{
    public static MapData Map;
    public static readonly List<ModifierData> Modifiers = new List<ModifierData>();
    public static string ModeName = "";
    public static bool IsSet { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Map = null;
        Modifiers.Clear();
        ModeName = "";
        IsSet = false;
    }

    public static void Set(MapData map, IEnumerable<ModifierData> modifiers, string modeName)
    {
        Map = map;
        Modifiers.Clear();
        if (modifiers != null)
            foreach (ModifierData m in modifiers)
                if (m != null) Modifiers.Add(m);
        ModeName = modeName;
        IsSet = true;
    }

    // Кнопка «Быстрая игра»
    public static void SetQuickPlay(GameDatabase db)
    {
        Set(db.RandomMap(), db.RandomQuickPlayModifiers(), "Быстрая игра");
    }

    // Пресет сложности
    public static void SetPreset(GameDatabase db, MapData map, DifficultyPreset preset)
    {
        Set(map, preset != null ? preset.modifiers : null, preset != null ? preset.displayName : "Свой набор");
    }

    // Если игру запустили сразу со сцены Game (без меню) — берём значения по умолчанию.
    public static void EnsureDefaults(GameDatabase db, bool quickPlay)
    {
        if (IsSet || db == null) return;
        if (quickPlay) SetQuickPlay(db);
        else SetPreset(db, db.maps.Count > 0 ? db.maps[0] : null, db.defaultPreset);
    }
}
