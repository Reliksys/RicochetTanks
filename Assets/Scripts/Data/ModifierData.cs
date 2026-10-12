using System.Collections.Generic;
using UnityEngine;

// Один модификатор. Создаётся в окне Project: правой кнопкой → Create → Tanks → Modifier.
// Код не нужен: заполнил поля — модификатор работает (применяет ModifierApplier).
[CreateAssetMenu(menuName = "Tanks/Modifier", fileName = "Mod_New")]
public class ModifierData : ScriptableObject
{
    [Header("Описание (для меню)")]
    public string displayName = "Новый модификатор";
    [TextArea(2, 4)] public string description = "";
    public Sprite icon;
    [Tooltip("Множитель очков. >1 — модификатор усложняет игру, <1 — облегчает.")]
    public float scoreMultiplier = 1f;
    [Tooltip("Можно ли выпасть в «Быстрой игре». Запутанные модификаторы (Суперхот, Вслепую) — выключить.")]
    public bool allowInQuickPlay = true;
    [Tooltip("Модификаторы с одинаковой группой нельзя выбрать вместе (например, «armor»). Пусто = без группы.")]
    public string group = "";
    [Tooltip("Без этих модификаторов этот не имеет смысла: они включаются вместе с ним (и в меню, и в «Быстрой игре»). Пример: «Снайпер» требует «Резиновые снаряды».")]
    public List<ModifierData> requires = new List<ModifierData>();

    [Header("Игрок (0 = не менять)")]
    public int playerMaxHealth = 0;
    public int playerMaxActiveBullets = 0;
    public int aimPreviewBounces = 0;
    public bool hideAimLine = false;
    [Tooltip("Инерция танка игрока в секундах (занос). 0 = не менять")]
    public float playerSlide = 0f;

    [Header("Снаряды")]
    public int extraBounces = 0;
    public bool ownBulletsHurtOwner = false;
    public bool superhot = false;

    [Header("Враги (множители, 1 = не менять)")]
    public float enemyCountMultiplier = 1f;
    public float enemySpeedMultiplier = 1f;
    public float enemyFireRateMultiplier = 1f;
    [Tooltip("Враги уворачиваются от снарядов")]
    public bool enemyDodge = false;

    [Header("Прочее")]
    public bool powerUps = false;

    // Строка вида "×1.5" для UI
    public string MultiplierText => "×" + scoreMultiplier.ToString("0.##");
    public bool IsHarder => scoreMultiplier > 1f;
}
