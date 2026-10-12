using System;
using UnityEngine;

// Здоровье. Вешается на корневой объект любого танка (игрок и враги).
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] int maxHealth = 1;
    [SerializeField] bool destroyOnDeath = true;
    [Tooltip("Секунды неуязвимости после попадания (игроку ~1, врагам 0)")]
    [SerializeField] float invulnerableAfterHit = 0f;

    public int Current { get; private set; }
    public int Max => maxHealth;
    public bool IsDead { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil;
    public float InvulnerableDuration => invulnerableAfterHit;

    // События этого конкретного объекта
    public event Action<Health, DamageInfo> Damaged;
    public event Action<Health, DamageInfo> Died;

    // Глобальное событие: кто-то умер. На него подписывается GameManager (очки, волны, поражение).
    public static event Action<Health, DamageInfo> AnyDied;
    // Глобальное событие: кто-то получил урон, но выжил (для HUD, звука, тряски камеры).
    public static event Action<Health, DamageInfo> AnyDamaged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        AnyDied = null;
        AnyDamaged = null;
    }

    float invulnerableUntil;

    void Awake() => Current = maxHealth;

    // Для модификаторов вроде "Толстая броня"
    public void SetMaxHealth(int value, bool refill = true)
    {
        maxHealth = Mathf.Max(1, value);
        if (refill) Current = maxHealth;
    }

    public void SetInvulnerableDuration(float seconds) => invulnerableAfterHit = Mathf.Max(0f, seconds);

    public void TakeDamage(int amount, DamageInfo info)
    {
        if (IsDead || IsInvulnerable) return;

        Current -= amount;

        if (Current <= 0)
        {
            IsDead = true;
            Damaged?.Invoke(this, info);
            Died?.Invoke(this, info);
            AnyDied?.Invoke(this, info);
            if (destroyOnDeath) Destroy(gameObject);
            return;
        }

        invulnerableUntil = Time.time + invulnerableAfterHit;
        Damaged?.Invoke(this, info);
        AnyDamaged?.Invoke(this, info);
    }
}
