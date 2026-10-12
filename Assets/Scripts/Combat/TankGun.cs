using System;
using UnityEngine;

// Пушка танка. Общая для игрока и врагов: игрок управляет ей мышью, враг — из ИИ.
// Вешается на корневой объект танка. Башня (turret) должна вращаться вокруг центра танка.
public class TankGun : MonoBehaviour
{
    [SerializeField] Transform turret;        // что вращаем (башня с дулом)
    [SerializeField] Transform muzzle;        // точка на конце дула
    [SerializeField] Bullet bulletPrefab;
    [SerializeField] float fireCooldown = 0.3f;
    [SerializeField] int maxActiveBullets = 5;
    [Tooltip("Градусов в секунду. 0 = мгновенно (для игрока). Врагам лучше 120-200. В Суперхоте замедляется вместе со снарядами.")]
    [SerializeField] float turretTurnSpeed = 0f;

    public Vector2 AimDirection { get; private set; } = Vector2.up; // куда реально смотрит ствол
    public Bullet BulletPrefab => bulletPrefab;
    public float MuzzleDistance => muzzle ? Vector2.Distance(transform.position, muzzle.position) : 0.6f;
    public int MaxActiveBullets { get => maxActiveBullets; set => maxActiveBullets = Mathf.Max(1, value); }
    public bool CanFire => Time.time >= nextFireTime && Bullet.CountOwnedBy(gameObject) < maxActiveBullets + BonusBullets;

    // Бонус «Скорострел» (PlayerBuffs): >1 = стреляет чаще; BonusBullets — сколько снарядов сверх лимита можно держать на поле
    public float FireRateMultiplier { get; set; } = 1f;
    public int BonusBullets { get; set; } = 0;

    public event Action<Bullet> Fired; // для звука/вспышки/тряски камеры
    // Глобально: кто-то выстрелил (для звука и тряски камеры)
    public static event Action<TankGun, Bullet> AnyFired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => AnyFired = null;

    float nextFireTime;

    // Вызывать каждый кадр с желаемым направлением.
    public void AimAt(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion target = Ricochet.DirToRotation(direction);

        if (turret != null)
        {
            turret.rotation = turretTurnSpeed <= 0f
                ? target
                : Quaternion.RotateTowards(turret.rotation, target, turretTurnSpeed * Time.deltaTime * GameRules.BulletTimeScale);
            AimDirection = turret.up;
        }
        else
        {
            AimDirection = direction.normalized;
        }
    }

    public void AimAtPoint(Vector2 worldPoint) => AimAt(worldPoint - (Vector2)transform.position);

    public bool TryFire()
    {
        if (!CanFire || bulletPrefab == null) return false;

        nextFireTime = Time.time + fireCooldown / Mathf.Max(0.1f, FireRateMultiplier);
        Bullet bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        bullet.Launch(AimDirection, gameObject, MuzzleDistance);
        Fired?.Invoke(bullet);
        AnyFired?.Invoke(this, bullet);
        return true;
    }
}
