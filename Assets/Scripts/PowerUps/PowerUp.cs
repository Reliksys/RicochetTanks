using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum PowerUpType { Repair, Shield, Speed, RapidFire }

// Бонус на поле (модификатор «Усиления на поле»). Создаётся КОДОМ из PowerUpSpawner — префаб не нужен.
// Подбирает только игрок: наехал — получил эффект (см. PlayerBuffs). Через lifetime секунд исчезает.
public class PowerUp : MonoBehaviour
{
    public static readonly List<PowerUp> Active = new List<PowerUp>();
    // Глобально: игрок подобрал бонус (для HUD)
    public static event Action<PowerUpType, Vector2> AnyPicked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active.Clear();
        AnyPicked = null;
    }

    public PowerUpType Type { get; private set; }

    const float BlinkBeforeEnd = 3f;
    float lifeLeft, age;
    bool picked;
    Transform visual;
    SpriteRenderer badge;
    TextMeshPro label;

    // ---------- Описание типов (название и цвет для поля и HUD) ----------
    public static string Title(PowerUpType t) => t switch
    {
        PowerUpType.Repair => "РЕМОНТ",
        PowerUpType.Shield => "ЩИТ",
        PowerUpType.Speed => "СКОРОСТЬ",
        _ => "СКОРОСТРЕЛ",
    };

    public static Color ColorOf(PowerUpType t) => t switch
    {
        PowerUpType.Repair => new Color(0.40f, 0.86f, 0.42f),
        PowerUpType.Shield => new Color(0.38f, 0.68f, 1.00f),
        PowerUpType.Speed => new Color(1.00f, 0.84f, 0.25f),
        _ => new Color(1.00f, 0.47f, 0.30f),
    };

    public static PowerUp Create(PowerUpType type, Vector2 position, float lifetime)
    {
        var go = new GameObject("PowerUp_" + type);
        go.transform.position = position;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;   // снаряды и поиск пути триггеры игнорируют
        col.radius = 0.45f;
        PowerUp p = go.AddComponent<PowerUp>();
        p.Init(type, lifetime);
        return p;
    }

    void Init(PowerUpType type, float lifetime)
    {
        Type = type;
        lifeLeft = lifetime;

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);
        badge = visual.gameObject.AddComponent<SpriteRenderer>();
        badge.sprite = ProcSprites.Badge(type);
        badge.sortingOrder = -1; // над полом, под танками

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, -0.62f, 0f);
        label = labelGo.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        label.text = Title(type);
        label.fontSize = 2.6f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = ColorOf(type);
        label.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);
        labelGo.GetComponent<MeshRenderer>().sortingOrder = 5; // поверх танков

        visual.localScale = Vector3.zero; // «выскакивает» в Update
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Update()
    {
        age += Time.deltaTime;
        lifeLeft -= Time.deltaTime;
        if (lifeLeft <= 0f) { Destroy(gameObject); return; }

        // появление + лёгкая «пульсация»
        float pop = Mathf.Clamp01(age / 0.25f);
        float pulse = 1f + 0.08f * Mathf.Sin(age * 5f);
        visual.localScale = Vector3.one * (pop * pulse);
        visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 2f) * 8f);

        // мигает перед исчезновением
        bool visible = lifeLeft > BlinkBeforeEnd || Mathf.Repeat(lifeLeft, 0.3f) > 0.12f;
        badge.enabled = visible;
        label.enabled = visible;
    }

    void OnTriggerEnter2D(Collider2D other) => TryPick(other);
    void OnTriggerStay2D(Collider2D other) => TryPick(other); // «Ремонт» при полной броне остаётся лежать — подберётся позже

    void TryPick(Collider2D other)
    {
        if (picked || GameManager.IsGameOver) return;
        PlayerTank tank = other.GetComponentInParent<PlayerTank>();
        if (tank == null) return;
        if (!PlayerBuffs.For(tank).Apply(Type)) return;

        picked = true;
        AnyPicked?.Invoke(Type, transform.position);
        Destroy(gameObject);
    }
}
