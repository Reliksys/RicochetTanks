using UnityEngine;

// Один куст: препятствие для танков, ломается от любого снаряда (снаряд при этом исчезает, без рикошета).
// Создаётся BushManager-ом под тайлом куста, сам по себе в префабы не кладётся.
public class Bush : MonoBehaviour, IDamageable
{
    BushManager owner;
    bool broken;

    public void Init(BushManager manager) => owner = manager;

    public void TakeDamage(int amount, DamageInfo info)
    {
        if (broken) return;
        broken = true;
        // Сначала выключаем коллайдер: сетка ботов пересчитается уже без куста
        if (TryGetComponent(out Collider2D col)) col.enabled = false;
        if (owner != null) owner.OnBushBroken(this);
        Destroy(gameObject);
    }
}
