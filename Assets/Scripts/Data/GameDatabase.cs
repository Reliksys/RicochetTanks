using System.Collections.Generic;
using UnityEngine;

// Единый список всего контента: карты, модификаторы, пресеты. Один ассет на проект: Assets/Data/GameDatabase.
// Новую карту/модификатор/пресет достаточно создать и перетащить в нужный список здесь.
[CreateAssetMenu(menuName = "Tanks/Game Database", fileName = "GameDatabase")]
public class GameDatabase : ScriptableObject
{
    public List<MapData> maps = new List<MapData>();
    public List<ModifierData> modifiers = new List<ModifierData>();
    public List<DifficultyPreset> presets = new List<DifficultyPreset>();
    [Tooltip("Пресет по умолчанию (если игру запустили сразу со сцены Game)")]
    public DifficultyPreset defaultPreset;

    [Header("Быстрая игра")]
    public int quickPlayMinModifiers = 1;
    public int quickPlayMaxModifiers = 2;

    public MapData RandomMap()
    {
        var valid = maps.FindAll(m => m != null && m.prefab != null);
        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : null;
    }

    // Случайные модификаторы для быстрой игры: только разрешённые, не больше одного из группы.
    public List<ModifierData> RandomQuickPlayModifiers()
    {
        var pool = modifiers.FindAll(m => m != null && m.allowInQuickPlay);
        // перемешиваем
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int count = Random.Range(quickPlayMinModifiers, quickPlayMaxModifiers + 1);
        var result = new List<ModifierData>();
        foreach (var m in pool)
        {
            if (result.Count >= count) break;
            if (!ModifierApplier.CanAdd(result, m)) continue;
            result.Add(m);
        }
        return result;
    }
}
