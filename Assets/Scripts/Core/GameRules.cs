using UnityEngine;

// Глобальные правила боя. Модификаторы (завтра) будут просто менять эти значения перед стартом боя.
// Правило: всё, что может поменять модификатор, живёт здесь, а не размазано по скриптам.
public static class GameRules
{
    // Множитель скорости ВСЕХ снарядов. Модификатор "Суперхот" будет ставить 0, когда игрок стоит.
    public static float BulletTimeScale = 1f;

    // Могут ли твои снаряды после рикошета попасть в тебя самого (модификатор "Бумеранг").
    public static bool OwnBulletsHurtOwner = false;

    // Дополнительные отскоки для всех снарядов (модификатор "Резиновые снаряды").
    public static int ExtraBounces = 0;

    // Показывать ли линию прицела (модификатор "Вслепую" выключает).
    public static bool ShowAimLine = true;

    // Сбрасывается автоматически при каждом нажатии Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetToDefaults()
    {
        BulletTimeScale = 1f;
        OwnBulletsHurtOwner = false;
        ExtraBounces = 0;
        ShowAimLine = true;
    }
}
