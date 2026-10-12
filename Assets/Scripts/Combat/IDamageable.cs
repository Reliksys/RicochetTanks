using UnityEngine;

// Информация о попадании. Пригодится для очков: рикошетное убийство = bounces > 0.
public struct DamageInfo
{
    public GameObject source; // кто выстрелил (может быть null, если стрелявший уже уничтожен)
    public int bounces;       // сколько раз снаряд отскочил до попадания
}

// Всё, во что может попасть снаряд и получить урон (танки, позже ящики и т.п.).
public interface IDamageable
{
    void TakeDamage(int amount, DamageInfo info);
}
