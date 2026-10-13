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

    [Header("Рикошет")]
    [SerializeField] bool useRicochet = true;
    [Tooltip("Как часто искать выстрел с отскоком, с")]
    [SerializeField] float ricochetSearchInterval = 0.25f;
    [Tooltip("Сколько направлений перебирать (120 = шаг 3°)")]
    [SerializeField] int ricochetSamples = 120;
    [Tooltip("Штраф за каждый отскок (в единицах длины пути): чем больше, тем сильнее бот предпочитает меньше отскоков")]
    [SerializeField] float bouncePenalty = 10f;

    [Header("Движение (не для турели)")]
    [SerializeField] float moveSpeed = 2.5f;         // × GameRules.EnemySpeedMultiplier
    [SerializeField] float bodyTurnSpeed = 540f;
    [Tooltip("Желаемая дистанция до игрока")]
    [SerializeField] float preferredRangeMin = 4f;
    [SerializeField] float preferredRangeMax = 8f;
    [Tooltip("Сколько стоять на позиции, с")]
    [SerializeField] float holdTimeMin = 0.8f;
    [SerializeField] float holdTimeMax = 2f;
    [Tooltip("Вероятность после выстрела отъехать в укрытие")]
    [Range(0f, 1f)]
    [SerializeField] float retreatChance = 0.5f;
    [Tooltip("Как часто проверять, не убежал ли игрок от выбранной позиции, с")]
    [SerializeField] float repathInterval = 0.7f;
    [Tooltip("Сколько случайных клеток оценивать при выборе позиции")]
    [SerializeField] int candidateSamples = 40;

    [Header("Уклонение (только с модификатором «Ловкие враги»)")]
    [Tooltip("На сколько секунд вперёд смотреть на летящие снаряды")]
    [SerializeField] float dodgeLookAhead = 0.7f;
    [Tooltip("Снаряд опасен, если пройдёт ближе этого от центра танка")]
    [SerializeField] float dodgeDangerRadius = 0.7f;
    [Tooltip("Через сколько секунд бот замечает снаряд")]
    [SerializeField] float dodgeReaction = 0.15f;
    [Tooltip("Шанс, что бот вообще станет уворачиваться от конкретного снаряда")]
    [Range(0f, 1f)]
    [SerializeField] float dodgeChance = 0.7f;
    [SerializeField] float dodgeDuration = 0.35f;
    [SerializeField] float dodgeSpeedMultiplier = 1.4f;

    // Все живые враги (для WaveManager и проверки союзников)
    public static readonly List<EnemyAI> All = new List<EnemyAI>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => All.Clear();

    public bool Stationary => stationary;
    // Куда бот едет (или где стоит) — чтобы союзники не лезли в одну точку
    public Vector2 Destination => moving ? destination : (Vector2)transform.position;

    enum ShotMode { None, Direct, Ricochet, Bush }

    Rigidbody2D rb;
    TankGun gun;
    Bullet bulletPrefab;

    PlayerTank player;          // кешируем, чтобы не дёргать GetComponent каждый кадр
    Rigidbody2D playerRb;

    float fireTimer;            // до следующего разрешённого выстрела
    float aimReadyTimer;        // сколько линия огня уже держится
    float aimErrorCurrent;      // разброс прямого выстрела на текущий выстрел
    Vector2 moveDir;            // куда едем сейчас (нормализовано или ноль)

    // Движение: едем по path к выбранной позиции, потом стоим holdTimer
    readonly List<Vector2> path = new List<Vector2>();
    readonly List<int> reachedCells = new List<int>();
    int[] bfsDist, bfsParent;
    int pathIndex;
    bool moving;
    bool seekCover;             // сейчас ищем укрытие (после выстрела)
    float holdTimer;
    float repathTimer;
    float stuckTimer;
    Vector2 destination;
    Vector2 playerPosAtChoose;
    Vector2 lastPos;
    // Уклонение
    float dodgeTimer;
    Vector2 dodgeDir;
    readonly Dictionary<Bullet, float> noticedBullets = new Dictionary<Bullet, float>(); // сколько видим снаряд; <0 — решили не уворачиваться
    readonly List<Bullet> staleBullets = new List<Bullet>();

    const float ArriveDistance = 0.15f;
    const float PathRadius = 0.38f;   // радиус танка для сглаживания пути

    ShotMode mode;              // какой выстрел сейчас готовим
    Vector2 desiredDir;         // куда хотим навести ствол

    // Рикошет: найденный диапазон углов, где пуля попадает в игрока
    float ricochetTimer;
    bool hasRicochet;
    float ricochetFromDeg, ricochetToDeg;
    float ricochetPick;         // где внутри диапазона целимся (0..1), новый на каждый выстрел
    bool[] sampleHits;
    int[] sampleBounces;
    float[] sampleLengths;

    MultiBarrel multiBarrel;    // двухствольный танк (или null)
    readonly List<Vector2> barrelOffsets = new List<Vector2>();

    // Кусты: если к игроку не проехать или куст закрывает линию огня — простреливаем его
    bool trappedByBushes;       // игрок недостижим по сетке (заперт кустами)
    Bush bushTarget;
    float bushSearchTimer;
    const float BushSearchRadius = 7f;

    readonly List<Vector3> debugPath = new List<Vector3>();      // проверка перед выстрелом
    readonly List<Vector3> ricochetPath = new List<Vector3>();   // найденный рикошет

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        gun = GetComponent<TankGun>();
        bulletPrefab = gun.BulletPrefab;
        multiBarrel = GetComponent<MultiBarrel>();

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

        ricochetSamples = Mathf.Max(8, ricochetSamples);
        sampleHits = new bool[ricochetSamples];
        sampleBounces = new int[ricochetSamples];
        sampleLengths = new float[ricochetSamples];

        fireTimer = firstShotDelay;
        holdTimer = Random.Range(0.2f, 0.6f); // первая позиция — почти сразу
        lastPos = transform.position;
        RollAim();
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
            mode = ShotMode.None;
            aimReadyTimer = 0f;
            moveDir = Vector2.zero;
            return; // игрок мёртв — стоим, не стреляем
        }

        ChooseShot(dt);

        // Ствол доворачивается сам (с turretTurnSpeed, в Суперхоте — медленнее)
        gun.AimAt(desiredDir);

        aimReadyTimer = mode != ShotMode.None ? aimReadyTimer + dt : 0f;
        TryShoot();

        if (!stationary && !UpdateDodge(dt)) UpdateMovement(dt);
        RotateBody(dt);
    }

    void FixedUpdate()
    {
        if (stationary) return;
        float boost = dodgeTimer > 0f ? dodgeSpeedMultiplier : 1f;
        rb.linearVelocity = moveDir * (moveSpeed * boost * GameRules.EnemySpeedMultiplier * GameRules.BulletTimeScale);
    }

    // ---------- Уклонение ----------

    // true — сейчас уворачиваемся (обычное движение пропускаем)
    bool UpdateDodge(float dt)
    {
        if (!GameRules.EnemyDodge || !ArenaGrid.Ready) return false;

        if (dodgeTimer > 0f)
        {
            dodgeTimer -= dt;
            moveDir = dodgeDir;
            if (dodgeTimer <= 0f)
            {
                // Увернулись — сразу выбираем новую позицию
                moving = false;
                holdTimer = 0f;
            }
            return true;
        }

        if (!FindThreat(dt, out Vector2 away)) return false;
        dodgeDir = away;
        dodgeTimer = dodgeDuration;
        moveDir = away;
        return true;
    }

    // Ищем снаряд, который скоро пройдёт рядом. away — куда отъезжать (вбок от линии полёта).
    bool FindThreat(float dt, out Vector2 away)
    {
        away = Vector2.zero;
        Vector2 pos = transform.position;

        // Чистим уничтоженные снаряды
        staleBullets.Clear();
        foreach (Bullet b in noticedBullets.Keys)
            if (b == null) staleBullets.Add(b);
        foreach (Bullet b in staleBullets) noticedBullets.Remove(b);

        foreach (Bullet b in Bullet.Active)
        {
            // Свой снаряд опасен только после отскока и при Бумеранге
            if (b.Owner == gameObject && (b.Bounces == 0 || !GameRules.OwnBulletsHurtOwner)) continue;
            // Снаряд стоит (Суперхот) — сейчас не опасен. Velocity бывает 0, на него не делим.
            if (b.Velocity.sqrMagnitude < 0.0001f) continue;

            Vector2 dir = b.Direction;
            Vector2 rel = pos - (Vector2)b.transform.position;
            float along = Vector2.Dot(rel, dir);                 // сколько снаряду лететь до точки сближения
            if (along < 0f || along > b.Speed * dodgeLookAhead) continue; // уже пролетел или ещё далеко
            Vector2 offset = rel - dir * along;                  // от линии полёта до нас
            if (offset.magnitude > dodgeDangerRadius) continue;

            // Снаряд упрётся в стену раньше, чем долетит до нас
            if (Ricochet.Cast(b.transform.position, b.Radius, dir, along, b.HitMask, b.Owner, out RaycastHit2D hit)
                && ArenaGrid.IsWall(hit.collider)) continue;

            // Реакция и шанс: решаем один раз на снаряд
            if (!noticedBullets.TryGetValue(b, out float seen))
                seen = Random.value < dodgeChance ? 0f : -1f;
            if (seen < 0f) { noticedBullets[b] = seen; continue; }
            seen += dt;
            noticedBullets[b] = seen;
            if (seen < dodgeReaction) continue;

            // Уходим вбок от линии полёта; если там стена — в другую сторону
            Vector2 side = offset.sqrMagnitude > 0.0001f ? offset.normalized : new Vector2(-dir.y, dir.x);
            if (ArenaGrid.WallBetween(pos, pos + side, PathRadius)) side = -side;
            away = side;
            return true;
        }
        return false;
    }

    // ---------- Выбор выстрела ----------

    void ChooseShot(float dt)
    {
        Vector2 self = transform.position;
        Vector2 target = player.transform.position;
        Vector2 targetVel = playerRb != null ? playerRb.linearVelocity : Vector2.zero;

        // 1) Прямо с упреждением
        Vector2 lead = LeadPoint(self, target, targetVel);
        if (HasDirectLine(self, lead))
        {
            SetDirect(lead - self);
            return;
        }
        // 2) Прямо в текущую позицию (упреждённая точка, например, в стене)
        if (HasDirectLine(self, target))
        {
            SetDirect(target - self);
            return;
        }

        // 3) Рикошетом (по текущей позиции игрока)
        if (useRicochet && bulletPrefab.MaxBounces > 0) // лазер не рикошетит — не ищем
        {
            ricochetTimer -= dt;
            if (ricochetTimer <= 0f)
            {
                ricochetTimer = ricochetSearchInterval;
                SearchRicochet();
            }
            if (hasRicochet)
            {
                mode = ShotMode.Ricochet;
                desiredDir = DirFromAngle(Mathf.Lerp(ricochetFromDeg, ricochetToDeg, ricochetPick));
                return;
            }
        }

        // 4) Куст мешает: закрывает линию огня или запер бота — стреляем в куст
        if (FindBushTarget(dt, self, target, out Vector2 bushPos))
        {
            mode = ShotMode.Bush;
            desiredDir = bushPos - self;
            return;
        }

        // Выстрела нет — просто следим башней за игроком
        mode = ShotMode.None;
        desiredDir = target - self;
    }

    // Куст, в который стоит стрелять: (а) первый на прямой к игроку; (б) если бот заперт — ближайший видимый
    bool FindBushTarget(float dt, Vector2 self, Vector2 target, out Vector2 bushPos)
    {
        bushPos = default;

        // (а) прямая к игроку упирается в куст
        Vector2 toPlayer = target - self;
        float dist = toPlayer.magnitude;
        if (dist > 0.01f &&
            Ricochet.Cast(self, bulletPrefab.Radius, toPlayer / dist, dist, bulletPrefab.HitMask, gameObject, out RaycastHit2D hit) &&
            hit.collider.TryGetComponent(out Bush blocking))
        {
            bushTarget = blocking;
            bushPos = blocking.transform.position;
            return true;
        }

        // (б) заперт: ищем ближайший куст, до которого есть прямая линия (не каждый кадр)
        if (!trappedByBushes) { bushTarget = null; return false; }
        bushSearchTimer -= dt;
        if (bushTarget == null || bushSearchTimer <= 0f)
        {
            bushSearchTimer = 0.5f;
            bushTarget = NearestVisibleBush(self);
        }
        if (bushTarget == null) return false;
        bushPos = bushTarget.transform.position;
        return true;
    }

    Bush NearestVisibleBush(Vector2 self)
    {
        Bush best = null;
        float bestDist = float.MaxValue;
        foreach (Collider2D c in Physics2D.OverlapCircleAll(self, BushSearchRadius))
        {
            if (!c.TryGetComponent(out Bush bush)) continue;
            Vector2 d = (Vector2)c.transform.position - self;
            float len = d.magnitude;
            if (len >= bestDist || len < 0.01f) continue;
            // первым на пути должен быть именно этот куст
            if (!Ricochet.Cast(self, bulletPrefab.Radius, d / len, len + 0.5f, bulletPrefab.HitMask, gameObject, out RaycastHit2D h)
                || h.collider != c) continue;
            best = bush;
            bestDist = len;
        }
        return best;
    }

    void SetDirect(Vector2 dir)
    {
        mode = ShotMode.Direct;
        desiredDir = Rotate(dir.normalized, aimErrorCurrent);
        // Прямая линия есть — старый рикошет устарел; когда линия пропадёт, ищем сразу
        hasRicochet = false;
        ricochetTimer = 0f;
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

    // Перебираем направления по кругу, ищем подряд идущие углы, где первым попадаем в игрока.
    // Из таких «окон» берём лучшее (меньше отскоков, короче путь) и целимся внутрь окна —
    // так разброс прицела не уводит пулю мимо.
    void SearchRicochet()
    {
        int n = ricochetSamples;
        float step = 360f / n;

        int start = -1; // индекс-промах, с него начинаем обход, чтобы окна не рвались на 0°/360°
        for (int i = 0; i < n; i++)
        {
            IDamageable first = SimulateShot(DirFromAngle(i * step), out sampleLengths[i], out sampleBounces[i]);
            sampleHits[i] = IsPlayer(first);
            if (!sampleHits[i] && start < 0) start = i;
        }

        hasRicochet = false;
        if (start < 0) return; // попадаем во все стороны — так не бывает, пропускаем

        float bestScore = float.MaxValue;
        int runFrom = -1;
        for (int k = 1; k <= n; k++)
        {
            int idx = (start + k) % n;
            if (sampleHits[idx])
            {
                if (runFrom < 0) runFrom = k;
                continue;
            }
            if (runFrom < 0) continue;

            // Окно [runFrom; k-1] закончилось — оцениваем по середине
            int runTo = k - 1;
            int mid = (start + (runFrom + runTo) / 2) % n;
            float score = sampleBounces[mid] * bouncePenalty + sampleLengths[mid];
            if (score < bestScore)
            {
                bestScore = score;
                hasRicochet = true;
                ricochetFromDeg = (start + runFrom) * step;
                ricochetToDeg = (start + runTo) * step;
            }
            runFrom = -1;
        }

        if (hasRicochet)
            SimulateShot(DirFromAngle((ricochetFromDeg + ricochetToDeg) * 0.5f), out _, out _, ricochetPath);
        else
            ricochetPath.Clear();
    }

    // ---------- Выстрел ----------

    void TryShoot()
    {
        if (mode == ShotMode.None || fireTimer > 0f || aimReadyTimer < reactionTime || !gun.CanFire) return;

        // Ствол ещё не довернулся
        if (Vector2.Angle(gun.AimDirection, desiredDir) > fireAngleTolerance) return;

        // Проверяем реальное направление ствола: не попадём ли в себя или в союзника
        if (!ShotIsSafe(gun.AimDirection)) return;

        if (!gun.TryFire()) return;

        // Делить на множитель модификатора можно (он > 0), на BulletTimeScale — нельзя
        float rate = Mathf.Max(0.01f, GameRules.EnemyFireRateMultiplier);
        fireTimer = Random.Range(fireIntervalMin, fireIntervalMax) / rate;
        aimReadyTimer = 0f;
        RollAim();

        // Выстрелил — иногда отъезжаем в укрытие
        if (!stationary && Random.value < retreatChance && ArenaGrid.Ready)
        {
            seekCover = true;
            ChooseDestination();
        }
    }

    // ---------- Движение ----------

    void UpdateMovement(float dt)
    {
        if (!ArenaGrid.Ready)
        {
            moveDir = Vector2.zero;
            return;
        }

        Vector2 pos = transform.position;

        if (moving)
        {
            while (pathIndex < path.Count && Vector2.Distance(pos, path[pathIndex]) < ArriveDistance) pathIndex++;

            if (pathIndex >= path.Count)
            {
                // Приехали — стоим (в укрытии недолго)
                moving = false;
                moveDir = Vector2.zero;
                holdTimer = seekCover ? Random.Range(0.5f, 1.2f) : Random.Range(holdTimeMin, holdTimeMax);
            }
            else
            {
                moveDir = (path[pathIndex] - pos).normalized;

                // Игрок сильно сместился — выбираем позицию заново
                repathTimer -= dt;
                if (repathTimer <= 0f)
                {
                    repathTimer = repathInterval;
                    if (!seekCover && Vector2.Distance(player.transform.position, playerPosAtChoose) > 2.5f)
                        ChooseDestination();
                }

                // Застряли (упёрлись в танк или угол) — выбираем заново
                if (dt > 0f)
                {
                    float expected = moveSpeed * GameRules.EnemySpeedMultiplier * dt;
                    stuckTimer = Vector2.Distance(pos, lastPos) < expected * 0.2f ? stuckTimer + dt : 0f;
                    if (stuckTimer > 0.6f)
                    {
                        stuckTimer = 0f;
                        ChooseDestination();
                    }
                }
            }
        }
        else
        {
            moveDir = Vector2.zero;
            // Без линии огня стоять незачем — время на позиции идёт вдвое быстрее
            holdTimer -= mode == ShotMode.None ? dt * 2f : dt;
            if (holdTimer <= 0f)
            {
                seekCover = false;
                ChooseDestination();
            }
        }

        lastPos = pos;
    }

    // Выбор позиции: оцениваем случайные достижимые клетки и едем в лучшую
    void ChooseDestination()
    {
        Vector2 self = transform.position;
        int start = ArenaGrid.CellOf(self);
        if (start < 0)
        {
            moving = false;
            holdTimer = 1f;
            return;
        }

        int n = ArenaGrid.CellCount;
        if (bfsDist == null || bfsDist.Length != n)
        {
            bfsDist = new int[n];
            bfsParent = new int[n];
        }
        ArenaGrid.Bfs(start, bfsDist, bfsParent, reachedCells);

        Vector2 playerPos = player.transform.position;
        int playerCell = ArenaGrid.CellOf(playerPos);
        trappedByBushes = playerCell >= 0 && bfsDist[playerCell] < 0; // к игроку не проехать — будем стрелять по кустам
        int best = start;
        float bestScore = ScoreCell(start, 0, playerPos);
        for (int i = 0; i < candidateSamples && reachedCells.Count > 0; i++)
        {
            int cell = reachedCells[Random.Range(0, reachedCells.Count)];
            float s = ScoreCell(cell, bfsDist[cell], playerPos);
            if (s > bestScore) { bestScore = s; best = cell; }
        }

        playerPosAtChoose = playerPos;
        repathTimer = repathInterval;
        stuckTimer = 0f;

        if (best == start)
        {
            // Лучше остаться на месте
            moving = false;
            holdTimer = Random.Range(holdTimeMin, holdTimeMax);
            return;
        }

        ArenaGrid.BuildPath(best, bfsParent, self, PathRadius, path);
        pathIndex = 0;
        destination = ArenaGrid.Center(best);
        moving = true;
    }

    float ScoreCell(int cell, int steps, Vector2 playerPos)
    {
        Vector2 c = ArenaGrid.Center(cell);
        float d = Vector2.Distance(c, playerPos);
        float s = 0f;

        // Дистанция до игрока
        if (d < preferredRangeMin) s -= (preferredRangeMin - d) * 3f;
        else if (d > preferredRangeMax) s -= d - preferredRangeMax;

        // Линия огня: в атаке нужна, в укрытии — наоборот
        bool los = HasDirectLine(c, playerPos);
        s += seekCover ? (los ? -6f : 4f) : (los ? 6f : 0f);

        // Далеко ехать — хуже (в укрытие — тем более)
        s -= steps * ArenaGrid.CellSize * (seekCover ? 0.8f : 0.3f);

        // Не лезть в одну точку с союзниками
        foreach (EnemyAI e in All)
            if (e != this && (e.Destination - c).sqrMagnitude < 4f) s -= 3f;

        return s + Random.value * 1.5f; // немного случайности, чтобы боты не были одинаковыми
    }

    // Симуляция по каждому стволу: ни один снаряд не летит в себя/союзника,
    // для рикошета хотя бы один точно долетает до игрока
    bool ShotIsSafe(Vector2 dir)
    {
        barrelOffsets.Clear();
        if (multiBarrel != null) multiBarrel.GetBarrelOffsets(dir, barrelOffsets);
        else barrelOffsets.Add(Vector2.zero);

        bool hitsPlayer = false;
        for (int i = 0; i < barrelOffsets.Count; i++)
        {
            IDamageable first = SimulateShot(dir, out _, out _, i == 0 ? debugPath : null, barrelOffsets[i]);
            if (first is Bush && mode == ShotMode.Bush) continue; // стреляем по кусту — так и задумано
            if (first != null && !IsPlayer(first)) return false;
            if (first != null) hitsPlayer = true;
        }
        return mode != ShotMode.Ricochet || hitsPlayer;
    }

    // Новый разброс на следующий выстрел
    void RollAim()
    {
        aimErrorCurrent = Random.Range(-aimErrorDeg, aimErrorDeg);
        ricochetPick = Random.Range(0.3f, 0.7f);
    }

    // Точная копия логики полёта Bullet: снаряд из центра танка, свой танк игнорируется
    // до первого отскока (после — только если включён Бумеранг).
    // Возвращает первую цель на пути (или null), длину пути и число отскоков до неё.
    // originOffset — смещение ствола от центра (у многоствольных).
    public IDamageable SimulateShot(Vector2 dir, out float pathLength, out int bounceCount, List<Vector3> points = null,
                                    Vector2 originOffset = default)
    {
        Vector2 pos = (Vector2)transform.position + originOffset;
        dir = dir.normalized;
        float left = maxPathLength;
        int maxBounces = bulletPrefab.MaxBounces;
        pathLength = 0f;
        bounceCount = 0;
        points?.Clear();
        points?.Add(pos);

        for (int bounces = 0; bounces <= maxBounces && left > 0f; bounces++)
        {
            bounceCount = bounces;
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

    static Vector2 DirFromAngle(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    void OnDrawGizmosSelected()
    {
        // Найденный рикошет
        if (hasRicochet)
        {
            Gizmos.color = Color.yellow;
            for (int i = 1; i < ricochetPath.Count; i++)
                Gizmos.DrawLine(ricochetPath[i - 1], ricochetPath[i]);
        }

        // Последняя проверка перед выстрелом
        Gizmos.color = Color.red;
        for (int i = 1; i < debugPath.Count; i++)
            Gizmos.DrawLine(debugPath[i - 1], debugPath[i]);

        // Путь движения: голубой — в атаку, синий — в укрытие
        if (moving)
        {
            Gizmos.color = seekCover ? Color.blue : Color.cyan;
            Vector3 prev = transform.position;
            for (int i = pathIndex; i < path.Count; i++)
            {
                Gizmos.DrawLine(prev, path[i]);
                prev = path[i];
            }
            Gizmos.DrawWireSphere(destination, 0.25f);
        }

        // Куда хотим навести ствол: зелёный — прямой, жёлтый — рикошет, серый — нет выстрела
        Gizmos.color = mode == ShotMode.Direct ? Color.green : mode == ShotMode.Ricochet ? Color.yellow
                     : mode == ShotMode.Bush ? Color.magenta : Color.gray;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + desiredDir.normalized * 2f);
    }
}
