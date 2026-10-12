using System.Collections.Generic;
using UnityEngine;

// Пресет сложности = готовый набор модификаторов. Create → Tanks → Difficulty Preset.
[CreateAssetMenu(menuName = "Tanks/Difficulty Preset", fileName = "Preset_New")]
public class DifficultyPreset : ScriptableObject
{
    public string displayName = "Обычный";
    [TextArea(2, 4)] public string description = "";
    public List<ModifierData> modifiers = new List<ModifierData>();

    public float TotalMultiplier => ModifierApplier.TotalMultiplier(modifiers);
}
