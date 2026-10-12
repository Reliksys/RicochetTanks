using System.Collections.Generic;
using UnityEngine;

// Пресет сложности = готовый набор модификаторов. Create → Tanks → Difficulty Preset.
[CreateAssetMenu(menuName = "Tanks/Difficulty Preset", fileName = "Preset_New")]
public class DifficultyPreset : ScriptableObject
{
    public string displayName = "Обычный";
    [TextArea(2, 4)] public string description = "";
    public List<ModifierData> modifiers = new List<ModifierData>();
    [Tooltip("Какие враги доступны: 1 — танк и турель, 2 — + двухствольный, 3 — все")]
    [Range(1, 3)] public int enemyTier = 2;

    public float TotalMultiplier => ModifierApplier.TotalMultiplier(modifiers);
}
