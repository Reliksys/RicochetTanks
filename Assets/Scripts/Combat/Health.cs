using System;
using UnityEngine;

// Здоровье. Вешается на корневой объект любого танка (игрок и враги).
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] int maxHealth = 1;
    [SerializeField] bool destroyOnDeath = true;

    public int Current { get; private set; }
    public int Max => maxHealth;
    public bool IsDead { get; private set; }

    // События этого конкретного объекта
    public event Action<Health, DamageInfo> Damaged;
    public event Action<Health, DamageInfo> Died;

    // Глобальное событие: кто-то умер. На него подписывается GameManager (очки, волны, поражение).
    public static event Action<Health, DamageInfo> AnyDied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => AnyDied = null;

    void Awake() => Current = maxHealth;

    // Для модификаторов вроде "Толстая броня"
    public void SetMaxHealth(int value, bool refill = true)
    {
        maxHealth = Mathf.Max(1, value);
        if (refill) Current = maxHealth;
    }

    public void TakeDamage(int amount, DamageInfo info)
    {
        if (IsDead) return;

        Current -= amount;
        Damaged?.Invoke(this, info);

        if (Current <= 0)
        {
            IsDead = true;
            Died?.Invoke(this, info);
            AnyDied?.Invoke(this, info);
            if (destroyOnDeath) Destroy(gameObject);
        }
    }
}
