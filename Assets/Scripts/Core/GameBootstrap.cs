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

        float size = Mathf.Max(bounds.extents.y, bounds.extents.x / cam.aspect) + cameraPadding;
        cam.orthographicSize = Mathf.Max(minCameraSize, size);
        cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, cam.transform.position.z);
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
