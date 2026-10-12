using System;
using System.Collections.Generic;
using UnityEngine;

// Снаряд с рикошетом. НЕ использует Rigidbody: двигается сам и проверяет столкновения через CircleCast.
// Так отскок абсолютно предсказуемый и совпадает с линией прицела.
// На префабе снаряда НЕ должно быть коллайдера и Rigidbody2D.
public class Bullet : MonoBehaviour
{
    [SerializeField] float speed = 9f;
    [SerializeField] float radius = 0.1f;
    [SerializeField] int maxBounces = 1;
    [SerializeField] int damage = 1;
    [SerializeField] float lifetime = 10f;
    [SerializeField] LayerMask hitMask = ~0; // по умолчанию сталкиваемся со всем
    [Tooltip("Никогда не рикошетит, даже с «Резиновыми снарядами» (лазер врагов)")]
    [SerializeField] bool noRicochet = false;

    public float Radius => radius;
    public int MaxBounces => noRicochet ? 0 : maxBounces + GameRules.ExtraBounces;
    public int Damage => damage;
    public LayerMask HitMask => hitMask;
    public GameObject Owner { get; private set; }
    public Vector2 Direction => direction;                            // текущее направление полёта
    public float Speed => speed;                                      // базовая скорость (из префаба)
    public float CurrentSpeed => speed * GameRules.BulletTimeScale;   // с учётом Суперхота
    public Vector2 Velocity => direction * CurrentSpeed;              // для уклонения ИИ
    public int Bounces { get; private set; }

    // Все живые снаряды (нужно для лимита снарядов на экране и для ИИ уклонения)
    public static readonly List<Bullet> Active = new List<Bullet>();

    // Для звуков и эффектов: (снаряд, точка отскока)
    public static event Action<Bullet, Vector2> AnyBounced;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active.Clear();
        AnyBounced = null;
    }

    Vector2 direction = Vector2.up;
    float age;

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public static int CountOwnedBy(GameObject owner)
    {
        int n = 0;
        foreach (Bullet b in Active)
            if (b.Owner == owner) n++;
        return n;
    }

    // Вызывается сразу после Instantiate. Снаряд создаётся в центре танка
    // и сразу пролетает preStep (до дула): так он не появится внутри стены, если танк упёрся в неё.
    public void Launch(Vector2 dir, GameObject owner, float preStep)
    {
        direction = dir.normalized;
        Owner = owner;
        transform.rotation = Ricochet.DirToRotation(direction);
        Move(preStep);
    }

    void Update()
    {
        float dt = Time.deltaTime * GameRules.BulletTimeScale;
        age += dt;
        if (age > lifetime)
        {
            Destroy(gameObject);
            return;
        }
        Move(speed * dt);
    }

    void Move(float distance)
    {
        Vector2 pos = transform.position;

        // Цикл на случай, если за один кадр снаряд отскочит несколько раз (например, в углу)
        for (int i = 0; i < 5 && distance > 0f; i++)
        {
            // До первого отскока свой танк не задеваем. После: только если включён "Бумеранг".
            GameObject ignore = (Bounces == 0 || !GameRules.OwnBulletsHurtOwner) ? Owner : null;

            if (!Ricochet.Cast(pos, radius, direction, distance, hitMask, ignore, out RaycastHit2D hit))
            {
                pos += direction * distance;
                break;
            }

            // Попали в танк (или что-то с IDamageable)
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(damage, new DamageInfo { source = Owner, bounces = Bounces });
                Destroy(gameObject);
                return;
            }

            // Попали в стену, но отскоки закончились
            if (Bounces >= MaxBounces)
            {
                Destroy(gameObject);
                return;
            }

            // Отскок
            distance -= hit.distance;
            pos = Ricochet.PointAfterBounce(hit);
            direction = Vector2.Reflect(direction, hit.normal).normalized;
            Bounces++;
            AnyBounced?.Invoke(this, pos);
        }

        transform.position = pos;
        transform.rotation = Ricochet.DirToRotation(direction);
    }
}
