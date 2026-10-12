using System;
using System.Collections.Generic;
using UnityEngine;

// Волны врагов. Волна N: (startCount + (N-1)·perWave) × EnemyCountMultiplier врагов, часть — турели.
// Волна зачищена → WaveCleared(N) → пауза → WaveStarted(N+1). Волны бесконечные.
// Точки спавна: дети объекта EnemySpawns на карте (ищем в Start: карту создаёт GameBootstrap в Awake).
// Суперхот: пауза и интервал спавна тикают Time.deltaTime * GameRules.BulletTimeScale.
public class WaveManager : MonoBehaviour
{
    [Header("Префабы")]
    [SerializeField] EnemyAI tankPrefab;
    [SerializeField] EnemyAI turretPrefab;

    [Header("Размер волны")]
    [SerializeField] int startCount = 2;             // врагов в первой волне
    [SerializeField] int perWave = 1;                // прибавка за волну
    [SerializeField] int maxPerWave = 10;
    [Range(0f, 1f)]
    [SerializeField] float turretShare = 0.3f;       // доля турелей

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
        if (tankPrefab == null && turretPrefab == null)
        {
            Debug.LogWarning("[WaveManager] Не назначены префабы врагов.");
            state = State.Stopped;
            return;
        }

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
        int turrets = turretPrefab != null ? Mathf.RoundToInt(count * turretShare) : 0;
        if (tankPrefab == null) turrets = count;

        // Перемешиваем порядок, чтобы турели не шли всегда последними
        var list = new List<EnemyAI>(count);
        for (int i = 0; i < count; i++) list.Add(i < turrets ? turretPrefab : tankPrefab);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        queue.Clear();
        foreach (EnemyAI e in list) queue.Enqueue(e);

        state = State.Spawning;
        timer = 0f;
        if (GameFeel.Instance != null) GameFeel.Instance.PlayWaveStart();
        WaveStarted?.Invoke(CurrentWave);
    }

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
