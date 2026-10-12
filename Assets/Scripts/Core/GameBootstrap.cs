using System.Text;
using UnityEngine;

// Запуск боя в сцене Game. Вешается на объект GameManager (рядом с QuickRestart).
// 1) берёт настройки забега (RunConfig) — из меню или по умолчанию;
// 2) применяет модификаторы к GameRules;
// 3) создаёт карту, ставит игрока в PlayerSpawn, подгоняет камеру под размер карты;
// 4) каждый кадр крутит "Суперхот".
[DefaultExecutionOrder(-100)] // раньше всех остальных скриптов
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] GameDatabase database;
    [Tooltip("Тестовая арена в сцене. Выключается, если загружена настоящая карта.")]
    [SerializeField] GameObject fallbackArena;
    [Tooltip("Для теста без меню: включено = случайные модификаторы как в «Быстрой игре», выключено = пресет по умолчанию.")]
    [SerializeField] bool testAsQuickPlay = false;

    [Header("Камера")]
    [SerializeField] float cameraPadding = 1f;
    [SerializeField] float minCameraSize = 5f;

    [Header("Суперхот")]
    [Tooltip("Скорость снарядов, когда игрок стоит (0 = полностью замирают)")]
    [SerializeField] float superhotIdleScale = 0f;
    [SerializeField] float superhotBlendSpeed = 6f;

    [Header("Отладка")]
    [SerializeField] bool showDebugInfo = true;

    public static GameObject CurrentMap { get; private set; }

    [Header("Страховка: танк вылетел за карту")]
    [Tooltip("Насколько глубоко во внешнюю стену может зайти центр танка, прежде чем его вернут на арену")]
    [SerializeField] float outOfBoundsMargin = 0.6f;

    Bounds arenaBounds;
    bool hasArenaBounds;
    float nextBoundsCheck;

    void Awake()
    {
        RunConfig.EnsureDefaults(database, testAsQuickPlay);
        ModifierApplier.Apply(RunConfig.Modifiers);

        CurrentMap = SpawnMap();
        PlacePlayer(CurrentMap);
        FitCamera(CurrentMap);

        Debug.Log($"[Game] Режим: {RunConfig.ModeName} | Карта: {(RunConfig.Map ? RunConfig.Map.displayName : "тестовая")} | " +
                  $"Модификаторы: {ModifierApplier.Describe(RunConfig.Modifiers)} | Множитель ×{GameRules.ScoreMultiplier:0.##}");
    }

    void Update()
    {
        if (Time.time >= nextBoundsCheck)
        {
            nextBoundsCheck = Time.time + 0.25f;
            KeepTanksInsideArena();
        }

        if (!GameRules.Superhot) return;
        PlayerTank player = PlayerTank.Instance;
        float target = (player == null || player.IsMoving) ? 1f : superhotIdleScale;
        GameRules.BulletTimeScale = Mathf.MoveTowards(GameRules.BulletTimeScale, target, superhotBlendSpeed * Time.deltaTime);
    }

    GameObject SpawnMap()
    {
        MapData map = RunConfig.Map;
        if (map == null || map.prefab == null) return fallbackArena; // нет карт — играем на тестовой арене

        if (fallbackArena != null) fallbackArena.SetActive(false);
        GameObject instance = Instantiate(map.prefab, Vector3.zero, Quaternion.identity);
        instance.name = map.prefab.name;
        return instance;
    }

    void PlacePlayer(GameObject mapRoot)
    {
        if (mapRoot == null) return;
        PlayerTank player = FindAnyObjectByType<PlayerTank>();
        Transform spawn = FindDeep(mapRoot.transform, "PlayerSpawn");
        if (player == null || spawn == null) return;

        player.transform.position = new Vector3(spawn.position.x, spawn.position.y, 0f);
        Physics2D.SyncTransforms();
    }

    void FitCamera(GameObject mapRoot)
    {
        Camera cam = Camera.main;
        if (cam == null || mapRoot == null) return;

        bool found = false;
        Bounds bounds = default;
        foreach (Collider2D c in mapRoot.GetComponentsInChildren<Collider2D>())
        {
            if (c.isTrigger) continue;
            if (!found) { bounds = c.bounds; found = true; }
            else bounds.Encapsulate(c.bounds);
        }
        if (!found) return;

        arenaBounds = bounds;
        hasArenaBounds = true;

        float size = Mathf.Max(bounds.extents.y, bounds.extents.x / cam.aspect) + cameraPadding;
        cam.orthographicSize = Mathf.Max(minCameraSize, size);
        cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, cam.transform.position.z);
    }

    // Если танк выдавило сквозь стену за пределы карты — возвращаем его на арену,
    // иначе волна будет ждать врага, которого нельзя убить.
    void KeepTanksInsideArena()
    {
        if (!hasArenaBounds || CurrentMap == null) return;

        float minX = arenaBounds.min.x + outOfBoundsMargin, maxX = arenaBounds.max.x - outOfBoundsMargin;
        float minY = arenaBounds.min.y + outOfBoundsMargin, maxY = arenaBounds.max.y - outOfBoundsMargin;

        foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (h == null || h.IsDead) continue;
            Vector2 p = h.transform.position;
            if (p.x > minX && p.x < maxX && p.y > minY && p.y < maxY) continue;

            bool isPlayer = h.GetComponent<PlayerTank>() != null;
            Vector2? target = isPlayer ? PlayerSpawnPosition() : FreeEnemySpawnPosition();
            if (target == null)
            {
                if (!isPlayer) Destroy(h.gameObject); // некуда вернуть — убираем, чтобы волна не зависла
                continue;
            }

            Debug.LogWarning($"[Arena] {h.name} вылетел за карту в {p}, возвращаю в {target.Value}");
            if (h.TryGetComponent(out Rigidbody2D rb))
            {
                rb.position = target.Value;
                rb.linearVelocity = Vector2.zero;
            }
            h.transform.position = new Vector3(target.Value.x, target.Value.y, 0f);
            Physics2D.SyncTransforms();
        }
    }

    Vector2? PlayerSpawnPosition()
    {
        Transform t = FindDeep(CurrentMap.transform, "PlayerSpawn");
        return t != null ? (Vector2)t.position : (Vector2?)null;
    }

    // Точка спавна врагов подальше от игрока, на которой сейчас не стоит танк
    Vector2? FreeEnemySpawnPosition()
    {
        Transform root = FindDeep(CurrentMap.transform, "EnemySpawns");
        if (root == null) return null;
        PlayerTank player = PlayerTank.Instance;
        Vector2 playerPos = player != null ? (Vector2)player.transform.position : Vector2.zero;

        Vector2? best = null;
        float bestDist = -1f;
        foreach (Transform point in root)
        {
            bool occupied = false;
            foreach (Collider2D c in Physics2D.OverlapCircleAll(point.position, 0.6f))
                if (!c.isTrigger && c.GetComponentInParent<IDamageable>() != null) { occupied = true; break; }
            if (occupied) continue;

            float d = Vector2.Distance(point.position, playerPos);
            if (d > bestDist) { bestDist = d; best = point.position; }
        }
        return best;
    }

    // Поиск дочернего объекта по имени на любой глубине
    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // Временная отладочная надпись в углу (пока нет HUD). Выключить галочкой showDebugInfo.
    void OnGUI()
    {
        if (!showDebugInfo) return;
        var sb = new StringBuilder();
        sb.AppendLine($"{RunConfig.ModeName}  ×{GameRules.ScoreMultiplier:0.##}");
        sb.AppendLine(ModifierApplier.Describe(RunConfig.Modifiers));
        PlayerTank p = PlayerTank.Instance;
        if (p != null && p.TryGetComponent(out Health h)) sb.AppendLine($"HP: {h.Current}/{h.Max}");
        if (GameRules.Superhot) sb.AppendLine($"Время снарядов: {GameRules.BulletTimeScale:0.0}");
        GUI.Label(new Rect(10, 10, 600, 100), sb.ToString());
    }
}
