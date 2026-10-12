using System.Collections.Generic;
using UnityEngine;

// ИИ вражеского танка. Управляет танком теми же средствами, что и игрок: TankGun.AimAt + TryFire,
// движение через Rigidbody2D.linearVelocity. Галочка stationary = турель (только вращает башню).
// Суперхот: все таймеры и движение идут через Time.deltaTime * GameRules.BulletTimeScale (на него не делим!).
[RequireComponent(typeof(Rigidbody2D), typeof(TankGun))]
public class EnemyAI : MonoBehaviour
{
    [Header("Тип")]
    [SerializeField] bool stationary = false;
    [SerializeField] Transform body;                 // спрайт корпуса, поворачивается по движению

    [Header("Стрельба")]
    [Tooltip("Сколько секунд линия огня должна держаться, прежде чем бот выстрелит")]
    [SerializeField] float reactionTime = 0.35f;
    [Tooltip("Случайный разброс прицела, градусы (новый на каждый выстрел)")]
    [SerializeField] float aimErrorDeg = 3f;
    [Tooltip("Насколько ствол может не довернуть до нужного направления, чтобы стрелять, градусы")]
    [SerializeField] float fireAngleTolerance = 4f;
    [SerializeField] float fireIntervalMin = 1.0f;   // ÷ GameRules.EnemyFireRateMultiplier
    [SerializeField] float fireIntervalMax = 1.6f;
    [Tooltip("Задержка первого выстрела после появления")]
    [SerializeField] float firstShotDelay = 1.0f;
    [Tooltip("Максимальное время упреждения, с")]
    [SerializeField] float maxLeadTime = 1.5f;
    [Tooltip("Максимальная длина пути снаряда в симуляции")]
    [SerializeField] float maxPathLength = 40f;

    [Header("Движение (не для турели)")]
    [SerializeField] float moveSpeed = 2.5f;         // × GameRules.EnemySpeedMultiplier
    [SerializeField] float bodyTurnSpeed = 540f;

    // Все живые враги (для WaveManager и проверки союзников)
    public static readonly List<EnemyAI> All = new List<EnemyAI>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => All.Clear();

    public bool Stationary => stationary;

    Rigidbody2D rb;
    TankGun gun;
    Bullet bulletPrefab;

    PlayerTank player;          // кешируем, чтобы не дёргать GetComponent каждый кадр
    Rigidbody2D playerRb;

    float fireTimer;            // до следующего разрешённого выстрела
    float aimReadyTimer;        // сколько линия огня уже держится
    float aimErrorCurrent;      // разброс на текущий выстрел
    Vector2 moveDir;            // куда едем (пока всегда ноль: движение — следующий шаг)

    bool hasShot;               // есть ли сейчас линия огня
    Vector2 desiredDir;         // куда хотим навести ствол

    readonly List<Vector3> debugPath = new List<Vector3>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        gun = GetComponent<TankGun>();
        bulletPrefab = gun.BulletPrefab;

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        if (stationary)
        {
            // Турель не сдвинуть: Kinematic не толкается игроком, но сталкивается с ним
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        fireTimer = firstShotDelay;
        aimErrorCurrent = Random.Range(-aimErrorDeg, aimErrorDeg);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        float dt = Time.deltaTime * GameRules.BulletTimeScale; // время врага (Суперхот)

        if (player != PlayerTank.Instance)
        {
            player = PlayerTank.Instance;
            playerRb = player != null ? player.GetComponent<Rigidbody2D>() : null;
        }

        fireTimer -= dt;

        if (player == null || bulletPrefab == null)
        {
            hasShot = false;
            aimReadyTimer = 0f;
            moveDir = Vector2.zero;
            return; // игрок мёртв — стоим, не стреляем
        }

        ChooseShot();

        // Ствол доворачивается сам (с turretTurnSpeed, в Суперхоте — медленнее)
        gun.AimAt(desiredDir);

        aimReadyTimer = hasShot ? aimReadyTimer + dt : 0f;
        TryShoot();

        RotateBody(dt);
    }

    void FixedUpdate()
    {
        if (stationary) return;
        rb.linearVelocity = moveDir * (moveSpeed * GameRules.EnemySpeedMultiplier * GameRules.BulletTimeScale);
    }

    // ---------- Выбор выстрела ----------

    void ChooseShot()
    {
        Vector2 self = transform.position;
        Vector2 target = player.transform.position;
        Vector2 targetVel = playerRb != null ? playerRb.linearVelocity : Vector2.zero;

        // 1) С упреждением
        Vector2 lead = LeadPoint(self, target, targetVel);
        if (HasDirectLine(self, lead))
        {
            SetShot(lead - self);
            return;
        }
        // 2) Прямо в текущую позицию (упреждённая точка, например, в стене)
        if (HasDirectLine(self, target))
        {
            SetShot(target - self);
            return;
        }
        // Линии огня нет — просто следим башней за игроком
        hasShot = false;
        desiredDir = target - self;
    }

    void SetShot(Vector2 dir)
    {
        hasShot = true;
        desiredDir = Rotate(dir.normalized, aimErrorCurrent);
    }

    // Точка упреждения: решаем |D + V·t| = d0 + s·t (снаряд стартует из центра и сразу пролетает d0).
    // Скорость снаряда — базовая Bullet.Speed (CurrentSpeed бывает 0 в Суперхоте).
    Vector2 LeadPoint(Vector2 self, Vector2 target, Vector2 vel)
    {
        float s = bulletPrefab.Speed;
        float d0 = gun.MuzzleDistance;
        Vector2 d = target - self;

        float a = Vector2.Dot(vel, vel) - s * s;
        float b = 2f * (Vector2.Dot(d, vel) - d0 * s);
        float c = Vector2.Dot(d, d) - d0 * d0;

        float t;
        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) < 0.0001f) return target;
            t = -c / b;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return target;
            float sq = Mathf.Sqrt(disc);
            float t1 = (-b - sq) / (2f * a);
            float t2 = (-b + sq) / (2f * a);
            t = Mathf.Min(t1, t2);
            if (t < 0f) t = Mathf.Max(t1, t2);
        }
        if (t < 0f) return target;
        return target + vel * Mathf.Min(t, maxLeadTime);
    }

    // Прямая линия до точки свободна (первым на пути — игрок или ничего)
    bool HasDirectLine(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return true;
        dir /= dist;
        if (!Ricochet.Cast(from, bulletPrefab.Radius, dir, dist, bulletPrefab.HitMask, gameObject, out RaycastHit2D hit))
            return true;
        return IsPlayer(hit.collider);
    }

    // ---------- Выстрел ----------

    void TryShoot()
    {
        if (!hasShot || fireTimer > 0f || aimReadyTimer < reactionTime || !gun.CanFire) return;

        // Ствол ещё не довернулся
        if (Vector2.Angle(gun.AimDirection, desiredDir) > fireAngleTolerance) return;

        // Проверяем реальное направление ствола: не попадём ли в себя или в союзника
        IDamageable first = SimulateShot(gun.AimDirection, out _, debugPath);
        if (first != null && !IsPlayer(first)) return;

        if (!gun.TryFire()) return;

        // Делить на множитель модификатора можно (он > 0), на BulletTimeScale — нельзя
        float rate = Mathf.Max(0.01f, GameRules.EnemyFireRateMultiplier);
        fireTimer = Random.Range(fireIntervalMin, fireIntervalMax) / rate;
        aimReadyTimer = 0f;
        aimErrorCurrent = Random.Range(-aimErrorDeg, aimErrorDeg);
    }

    // Точная копия логики полёта Bullet: снаряд из центра танка, свой танк игнорируется
    // до первого отскока (после — только если включён Бумеранг).
    // Возвращает первую цель на пути (или null) и длину пути до конца/цели.
    public IDamageable SimulateShot(Vector2 dir, out float pathLength, List<Vector3> points = null)
    {
        Vector2 pos = transform.position;
        dir = dir.normalized;
        float left = maxPathLength;
        int maxBounces = bulletPrefab.MaxBounces;
        pathLength = 0f;
        points?.Clear();
        points?.Add(pos);

        for (int bounces = 0; bounces <= maxBounces && left > 0f; bounces++)
        {
            GameObject ignore = (bounces == 0 || !GameRules.OwnBulletsHurtOwner) ? gameObject : null;
            if (!Ricochet.Cast(pos, bulletPrefab.Radius, dir, left, bulletPrefab.HitMask, ignore, out RaycastHit2D hit))
            {
                pathLength += left;
                points?.Add(pos + dir * left);
                return null;
            }

            pathLength += hit.distance;
            points?.Add(hit.centroid);

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null) return target;
            if (bounces == maxBounces) return null; // стена после последнего отскока — снаряд исчезает

            left -= hit.distance;
            pos = Ricochet.PointAfterBounce(hit);
            dir = Vector2.Reflect(dir, hit.normal).normalized;
        }
        return null;
    }

    // ---------- Вспомогательное ----------

    void RotateBody(float dt)
    {
        if (body == null || moveDir.sqrMagnitude < 0.01f) return;
        body.rotation = Quaternion.RotateTowards(body.rotation, Ricochet.DirToRotation(moveDir), bodyTurnSpeed * dt);
    }

    bool IsPlayer(Collider2D c) => player != null && c != null && c.transform.IsChildOf(player.transform);

    bool IsPlayer(IDamageable d) => player != null && d is Component comp && comp.transform.IsChildOf(player.transform);

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    void OnDrawGizmosSelected()
    {
        // Последняя проверенная траектория выстрела
        Gizmos.color = Color.red;
        for (int i = 1; i < debugPath.Count; i++)
            Gizmos.DrawLine(debugPath[i - 1], debugPath[i]);

        // Куда хотим навести ствол
        Gizmos.color = hasShot ? Color.green : Color.gray;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + desiredDir.normalized * 2f);
    }
}
