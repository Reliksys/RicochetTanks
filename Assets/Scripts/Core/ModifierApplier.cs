using System.Collections.Generic;
using UnityEngine;

// Превращает список модификаторов в значения GameRules.
public static class ModifierApplier
{
    public static void Apply(IEnumerable<ModifierData> modifiers)
    {
        GameRules.ResetToDefaults();
        if (modifiers == null) return;

        float mult = 1f;
        foreach (ModifierData m in modifiers)
        {
            if (m == null) continue;
            mult *= m.scoreMultiplier;

            if (m.playerMaxHealth > 0) GameRules.PlayerMaxHealth = m.playerMaxHealth;
            if (m.playerMaxActiveBullets > 0) GameRules.PlayerMaxActiveBullets = m.playerMaxActiveBullets;
            if (m.aimPreviewBounces > 0) GameRules.AimPreviewBounces = Mathf.Max(GameRules.AimPreviewBounces, m.aimPreviewBounces);
            if (m.hideAimLine) GameRules.ShowAimLine = false;

            GameRules.ExtraBounces += m.extraBounces;
            if (m.ownBulletsHurtOwner) GameRules.OwnBulletsHurtOwner = true;
            if (m.superhot) GameRules.Superhot = true;

            GameRules.EnemyCountMultiplier *= m.enemyCountMultiplier;
            GameRules.EnemySpeedMultiplier *= m.enemySpeedMultiplier;
            GameRules.EnemyFireRateMultiplier *= m.enemyFireRateMultiplier;

            if (m.powerUps) GameRules.PowerUpsEnabled = true;
        }
        GameRules.ScoreMultiplier = mult;
    }

    public static float TotalMultiplier(IEnumerable<ModifierData> modifiers)
    {
        float mult = 1f;
        if (modifiers == null) return mult;
        foreach (ModifierData m in modifiers)
            if (m != null) mult *= m.scoreMultiplier;
        return mult;
    }

    // Можно ли добавить модификатор к уже выбранным (нет дубля и конфликта по группе)
    public static bool CanAdd(List<ModifierData> selected, ModifierData candidate)
    {
        if (candidate == null || selected.Contains(candidate)) return false;
        if (string.IsNullOrEmpty(candidate.group)) return true;
        foreach (ModifierData m in selected)
            if (m != null && m.group == candidate.group) return false;
        return true;
    }

    public static string Describe(IEnumerable<ModifierData> modifiers)
    {
        var names = new List<string>();
        if (modifiers != null)
            foreach (ModifierData m in modifiers)
                if (m != null) names.Add(m.displayName);
        return names.Count == 0 ? "без модификаторов" : string.Join(", ", names);
    }
}
