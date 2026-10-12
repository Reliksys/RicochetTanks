using UnityEngine;

// Глобальные правила боя. Модификаторы выставляют эти значения ПЕРЕД стартом боя
// (ModifierApplier.Apply), а системы (игрок, враги, волны, очки) только ЧИТАЮТ их.
// Правило: всё, что может поменять модификатор, живёт здесь.
public static class GameRules
{
    // ---------- Снаряды ----------
    // Множитель скорости ВСЕХ снарядов. При "Суперхоте" GameBootstrap меняет его каждый кадр.
    public static float BulletTimeScale = 1f;
    // Свои снаряды после рикошета попадают в своего же владельца ("Бумеранг").
    public static bool OwnBulletsHurtOwner = false;
    // Дополнительные отскоки для всех снарядов ("Резиновые снаряды").
    public static int ExtraBounces = 0;
    // Снаряды двигаются, только когда игрок едет ("Суперхот").
    public static bool Superhot = false;

    // ---------- Игрок ----------
    public static int PlayerMaxHealth = 3;        // "Стеклянная броня" = 1, "Толстая броня" = 5
    public static int PlayerMaxActiveBullets = 0; // 0 = как в префабе; "Один патрон" = 1
    public static bool ShowAimLine = true;        // "Вслепую" = false
    public static int AimPreviewBounces = 1;      // сколько отскоков рисует прицел; "Снайпер" = 3

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
        Superhot = false;
        PlayerMaxHealth = 3;
        PlayerMaxActiveBullets = 0;
        ShowAimLine = true;
        AimPreviewBounces = 1;
        EnemyCountMultiplier = 1f;
        EnemySpeedMultiplier = 1f;
        EnemyFireRateMultiplier = 1f;
        PowerUpsEnabled = false;
        ScoreMultiplier = 1f;
    }
}
