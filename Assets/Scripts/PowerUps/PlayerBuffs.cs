using System.Text;
using UnityEngine;

// Временные эффекты бонусов на танке игрока. Добавляется автоматически при первом подборе бонуса.
// Ремонт: +1 броня. Щит: неуязвимость. Скорость: быстрее едет. Скорострел: стреляет чаще и больше снарядов на поле.
[DisallowMultipleComponent]
public class PlayerBuffs : MonoBehaviour
{
    public const float ShieldTime = 5f;
    public const float SpeedTime = 7f;
    public const float RapidTime = 7f;
    const float SpeedMultiplier = 1.5f;
    const float RapidFireRate = 2.5f;
    const int RapidBonusBullets = 3;

    public static PlayerBuffs Instance { get; private set; }

    PlayerTank tank;
    TankGun gun;
    Health health;
    SpriteRenderer shieldRing;
    float shieldLeft, speedLeft, rapidLeft;

    public static PlayerBuffs For(PlayerTank tank)
    {
        PlayerBuffs b = tank.GetComponent<PlayerBuffs>();
        return b != null ? b : tank.gameObject.AddComponent<PlayerBuffs>();
    }

    void Awake()
    {
        Instance = this;
        tank = GetComponent<PlayerTank>();
        gun = GetComponent<TankGun>();
        health = GetComponent<Health>();

        var ring = new GameObject("ShieldRing");
        ring.transform.SetParent(transform, false);
        shieldRing = ring.AddComponent<SpriteRenderer>();
        shieldRing.sprite = ProcSprites.Ring();
        shieldRing.color = PowerUp.ColorOf(PowerUpType.Shield);
        shieldRing.sortingOrder = 5;
        shieldRing.enabled = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // false = бонус сейчас бесполезен (ремонт при полной броне) — тогда он остаётся на поле
    public bool Apply(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Repair:
                return health != null && health.Heal(1);
            case PowerUpType.Shield:
                shieldLeft = ShieldTime;
                if (health != null) health.GrantInvulnerability(ShieldTime);
                return true;
            case PowerUpType.Speed:
                speedLeft = SpeedTime;
                return true;
            default:
                rapidLeft = RapidTime;
                return true;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        shieldLeft = Mathf.Max(0f, shieldLeft - dt);
        speedLeft = Mathf.Max(0f, speedLeft - dt);
        rapidLeft = Mathf.Max(0f, rapidLeft - dt);

        if (tank != null) tank.SpeedMultiplier = speedLeft > 0f ? SpeedMultiplier : 1f;
        if (gun != null)
        {
            gun.FireRateMultiplier = rapidLeft > 0f ? RapidFireRate : 1f;
            gun.BonusBullets = rapidLeft > 0f ? RapidBonusBullets : 0;
        }

        // Кольцо щита: пульсирует, в последнюю секунду мигает
        bool shield = shieldLeft > 0f && (shieldLeft > 1f || Mathf.Repeat(shieldLeft, 0.2f) > 0.08f);
        shieldRing.enabled = shield;
        if (shield) shieldRing.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 8f));
    }

    // Строка для HUD: активные эффекты с оставшимся временем
    public string Describe()
    {
        var sb = new StringBuilder();
        Add(sb, PowerUpType.Shield, shieldLeft);
        Add(sb, PowerUpType.Speed, speedLeft);
        Add(sb, PowerUpType.RapidFire, rapidLeft);
        return sb.ToString();
    }

    static void Add(StringBuilder sb, PowerUpType type, float left)
    {
        if (left <= 0f) return;
        if (sb.Length > 0) sb.Append("    ");
        sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(PowerUp.ColorOf(type))).Append('>')
          .Append(PowerUp.Title(type)).Append(' ').Append(Mathf.CeilToInt(left)).Append("</color>");
    }
}
