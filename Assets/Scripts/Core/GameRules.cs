using UnityEngine;

// Глобальные правила боя. Модификаторы выставляют эти значения ПЕРЕД стартом боя,
// а системы (игрок, враги, волны, очки) только ЧИТАЮТ их.
// Правило: всё, что может поменять модификатор, живёт здесь.
public static class GameRules
{
    // ---------- Снаряды ----------
    // Множитель скорости ВСЕХ снарядов. "Суперхот" меняет его каждый кадр (0, когда игрок стоит).
    public static float BulletTimeScale = 1f;
    // Свои снаряды после рикошета попадают в своего же владельца ("Бумеранг").
    public static bool OwnBulletsHurtOwner = false;
    // Дополнительные отскоки для всех снарядов ("Резиновые снаряды").
    public static int ExtraBounces = 0;

    // ---------- Игрок ----------
    public static int PlayerMaxHealth = 3;   // "Стеклянная броня" = 1, "Толстая броня" = 5
    public static bool ShowAimLine = true;   // "Вслепую" = false

    // ---------- Враги и волны (читает WaveManager / ИИ врагов) ----------
    public static float EnemyCountMultiplier = 1f;    // "Орда" = 1.5
    public static float EnemySpeedMultiplier = 1f;    // "Адреналин" = 1.3
    public static float EnemyFireRateMultiplier = 1f; // "Адреналин" = 1.3 (больше = чаще стреляют)

    // ---------- Бонусы ----------
    public static bool PowerUpsEnabled = false;       // "Усиления на поле" (если успеем)

    // ---------- Очки ----------
    // Произведение множителей всех выбранных модификаторов. Итог = очки × ScoreMultiplier.
    public static float ScoreMultiplier = 1f;

    // Сбрасывается автоматически при каждом нажатии Play (в проекте выключен Domain Reload).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetToDefaults()
    {
        BulletTimeScale = 1f;
        OwnBulletsHurtOwner = false;
        ExtraBounces = 0;
        PlayerMaxHealth = 3;
        ShowAimLine = true;
        EnemyCountMultiplier = 1f;
        EnemySpeedMultiplier = 1f;
        EnemyFireRateMultiplier = 1f;
        PowerUpsEnabled = false;
        ScoreMultiplier = 1f;
    }
}
