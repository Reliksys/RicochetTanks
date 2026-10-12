using System;
using System.Collections.Generic;
using UnityEngine;

// Волны врагов. Волна N: (startCount + (N-1)·perWave) × EnemyCountMultiplier врагов.
// Тип каждого врага — случайный (по весам) из доступных на текущем уровне сложности и номере волны.
// Волна зачищена → WaveCleared(N) → пауза → WaveStarted(N+1). Волны бесконечные.
// Точки спавна: дети объекта EnemySpawns на карте (ищем в Start: карту создаёт GameBootstrap в Awake).
// Суперхот: пауза и интервал спавна тикают Time.deltaTime * GameRules.BulletTimeScale.
public class WaveManager : MonoBehaviour
{
    [Serializable]
    public class EnemyType
    {
        public EnemyAI prefab;
        [Tooltip("С какого уровня сложности доступен: 1 — лёгкий, 2 — обычный, 3 — сложный")]
        [Range(1, 3)] public int minTier = 1;
        [Tooltip("С какой волны появляется")]
        public int fromWave = 1;
        [Tooltip("Вес при случайном выборе (больше — чаще)")]
        public float weight = 1f;
    }

    [Header("Типы врагов")]
    [SerializeField] List<EnemyType> enemyTypes = new List<EnemyType>();

    [Header("Уровень без пресета (по множителю очков)")]
    [SerializeField] float easyBelowMultiplier = 0.95f;  // множитель меньше — лёгкий
    [SerializeField] float hardAboveMultiplier = 1.5f;   // множитель больше — сложный

    [Header("Размер волны")]
    [SerializeField] int startCount = 2;             // врагов в первой волне
    [SerializeField] int perWave = 1;                // прибавка за волну
    [SerializeField] int maxPerWave = 10;

    [Header("Здоровье врагов")]
    [SerializeField] int toughFromWave = 4;          // с этой волны у врагов больше HP
    [SerializeField] int toughHealth = 2;

    [Header("Время")]
    [SerializeField] float firstWaveDelay = 1f;
    [SerializeField] float pauseBetweenWaves = 2f;
    [SerializeField] float spawnInterval = 0.3f;

    [Header("Спавн")]
    [SerializeField] float minPlayerDistance = 4f;   // не спавнить вплотную к игроку
    [SerializeField] float spawnCheckRadius = 0.5f;  // точка занята, если тут уже кто-то стоит

    // Для UI: номер волны
    public static event Action<int> WaveStarted;
    public static event Action<int> WaveCleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        WaveStarted = null;
        WaveCleared = null;
    }

    public int CurrentWave { get; private set; }
    // Уровень врагов: 1 — лёгкий, 2 — обычный, 3 — сложный
    public int Tier { get; private set; }
    public int AliveCount => alive.Count;
    public int LeftToSpawn => queue.Count;
    // Сколько осталось до следующей волны (для надписи «Волна N через…»), 0 если идёт бой
    public float NextWaveIn => state == State.Pause ? timer : 0f;

    enum State { Pause, Spawning, Fighting, Stopped }
    State state = State.Pause;
    float timer;

    readonly List<Transform> spawnPoints = new List<Transform>();
    readonly List<Health> alive = new List<Health>();
    readonly Queue<EnemyAI> queue = new Queue<EnemyAI>();
    readonly List<Transform> candidates = new List<Transform>();

    void Start()
    {
        GameObject map = GameBootstrap.CurrentMap;
        Transform root = map != null ? GameBootstrap.FindDeep(map.transform, "EnemySpawns") : null;
        if (root != null)
            foreach (Transform child in root) spawnPoints.Add(child);

        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("[WaveManager] Нет точек спавна (CurrentMap или EnemySpawns не найдены) — волн не будет.");
            state = State.Stopped;
            return;
        }
        enemyTypes.RemoveAll(t => t == null || t.prefab == null);
        if (enemyTypes.Count == 0)
        {
            Debug.LogWarning("[WaveManager] Не назначены типы врагов (Enemy Types).");
            state = State.Stopped;
            return;
        }

        // Уровень: из пресета, а без пресета («Быстрая игра», свой набор) — по множителю очков
        if (RunConfig.EnemyTier > 0) Tier = Mathf.Clamp(RunConfig.EnemyTier, 1, 3);
        else if (GameRules.ScoreMultiplier < easyBelowMultiplier) Tier = 1;
        else if (GameRules.ScoreMultiplier > hardAboveMultiplier) Tier = 3;
        else Tier = 2;
        Debug.Log($"[WaveManager] Уровень врагов: {Tier}");

        state = State.Pause;
        timer = firstWaveDelay;
    }

    // Конец боя: больше не спавним
    public void Stop()
    {
        state = State.Stopped;
        queue.Clear();
    }

    void Update()
    {
        if (state == State.Stopped) return;
        float dt = Time.deltaTime * GameRules.BulletTimeScale; // время врагов (Суперхот)

        alive.RemoveAll(h => h == null || h.IsDead);

        switch (state)
        {
            case State.Pause:
                timer -= dt;
                if (timer <= 0f) BeginWave();
                break;

            case State.Spawning:
                timer -= dt;
                if (timer <= 0f && TrySpawnNext()) timer = spawnInterval;
                if (queue.Count == 0) state = State.Fighting;
                break;

            case State.Fighting:
                if (alive.Count == 0)
                {
                    WaveCleared?.Invoke(CurrentWave);
                    state = State.Pause;
                    timer = pauseBetweenWaves;
                }
                break;
        }
    }

    void BeginWave()
    {
        CurrentWave++;

        int count = Mathf.RoundToInt((startCount + (CurrentWave - 1) * perWave) * GameRules.EnemyCountMultiplier);
        count = Mathf.Clamp(count, 1, maxPerWave);
        queue.Clear();
        for (int i = 0; i < count; i++) queue.Enqueue(PickType());

        state = State.Spawning;
        timer = 0f;
        if (GameFeel.Instance != null) GameFeel.Instance.PlayWaveStart();
        WaveStarted?.Invoke(CurrentWave);
    }

    // Случайный тип по весам среди доступных на этом уровне и волне
    EnemyAI PickType()
    {
        float total = 0f;
        foreach (EnemyType t in enemyTypes)
            if (Allowed(t)) total += t.weight;

        if (total <= 0f)
        {
            Debug.LogWarning($"[WaveManager] Нет типов врагов для уровня {Tier}, волны {CurrentWave} — беру первый.");
            return enemyTypes[0].prefab;
        }

        float r = UnityEngine.Random.value * total;
        foreach (EnemyType t in enemyTypes)
        {
            if (!Allowed(t)) continue;
            r -= t.weight;
            if (r <= 0f) return t.prefab;
        }
        return enemyTypes[0].prefab;
    }

    bool Allowed(EnemyType t) => t.weight > 0f && t.minTier <= Tier && t.fromWave <= CurrentWave;

    // false — все точки заняты, попробуем в следующем кадре
    bool TrySpawnNext()
    {
        if (queue.Count == 0) return true;
        Transform point = PickSpawnPoint();
        if (point == null) return false;

        EnemyAI enemy = Instantiate(queue.Dequeue(), (Vector2)point.position, Quaternion.identity);
        Physics2D.SyncTransforms(); // чтобы следующая проверка точки уже видела этого врага

        if (enemy.TryGetComponent(out Health health))
        {
            if (CurrentWave >= toughFromWave) health.SetMaxHealth(toughHealth);
            alive.Add(health);
        }
        return true;
    }

    // Случайная свободная точка подальше от игрока; если таких нет — самая дальняя свободная
    Transform PickSpawnPoint()
    {
        PlayerTank player = PlayerTank.Instance;
        Vector2 playerPos = player != null ? (Vector2)player.transform.position : Vector2.zero;

        candidates.Clear();
        Transform farthest = null;
        float best = -1f;

        foreach (Transform p in spawnPoints)
        {
            if (p == null || !IsFree(p.position)) continue;
            float d = player != null ? Vector2.Distance(p.position, playerPos) : float.MaxValue;
            if (d >= minPlayerDistance) candidates.Add(p);
            if (d > best) { best = d; farthest = p; }
        }

        if (candidates.Count > 0) return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return farthest;
    }

    bool IsFree(Vector2 pos)
    {
        foreach (Collider2D c in Physics2D.OverlapCircleAll(pos, spawnCheckRadius))
            if (!c.isTrigger && c.GetComponentInParent<IDamageable>() != null) return false;
        return true;
    }
}
