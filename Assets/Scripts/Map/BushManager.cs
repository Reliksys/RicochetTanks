using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Разрушаемые кусты. При старте боя ищет на карте (GameBootstrap.CurrentMap) тайлы кустов из списка
// и создаёт под каждым объект Bush с коллайдером. Куст сломан → тайл заменяется на воронку.
// Карты переделывать не нужно: дизайнер просто рисует тайлы кустов на любом тайлмапе.
// Живёт в префабе GameSystems.
public class BushManager : MonoBehaviour
{
    [Serializable]
    public class BushTile
    {
        public TileBase bush;   // тайл куста
        public TileBase hole;   // чем заменить после разрушения
    }

    [SerializeField] List<BushTile> tiles = new List<BushTile>();
    [Tooltip("Размер коллайдера куста в клетках")]
    [SerializeField] float colliderSize = 0.9f;
    [Tooltip("Кусты ближе этого к точкам спавна убираются при старте (чтобы танк не появился в кусте)")]
    [SerializeField] float spawnClearance = 0.9f;
    [SerializeField] AudioClip breakSound;
    [Range(0f, 1f)]
    [SerializeField] float breakVolume = 0.6f;

    // Для эффектов (частицы и т.п.): где сломался куст
    public static event Action<Vector2> AnyBroken;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => AnyBroken = null;

    public int Count { get; private set; }

    readonly Dictionary<TileBase, TileBase> holeFor = new Dictionary<TileBase, TileBase>();
    readonly Dictionary<Bush, (Tilemap map, Vector3Int cell)> cellOf = new Dictionary<Bush, (Tilemap, Vector3Int)>();
    readonly List<Vector2> spawnPoints = new List<Vector2>();

    void Start()
    {
        GameObject map = GameBootstrap.CurrentMap;
        if (map == null) return;

        foreach (BushTile t in tiles)
            if (t != null && t.bush != null && !holeFor.ContainsKey(t.bush)) holeFor.Add(t.bush, t.hole);
        if (holeFor.Count == 0) return;

        CollectSpawnPoints(map.transform);

        int cleared = 0;
        foreach (Tilemap tm in map.GetComponentsInChildren<Tilemap>())
        {
            BoundsInt b = tm.cellBounds;
            foreach (Vector3Int cell in b.allPositionsWithin)
            {
                TileBase tile = tm.GetTile(cell);
                if (tile == null || !holeFor.TryGetValue(tile, out TileBase hole)) continue;

                Vector2 center = tm.GetCellCenterWorld(cell);
                if (NearSpawn(center))
                {
                    SetHole(tm, cell, hole); // куст на точке спавна — убираем молча
                    cleared++;
                    continue;
                }
                CreateBush(tm, cell, center);
            }
        }

        Physics2D.SyncTransforms();
        ArenaGrid.Invalidate(); // если сетку уже построили — перестроить с кустами
        Debug.Log($"[BushManager] Кустов: {Count}" + (cleared > 0 ? $", убрано у точек спавна: {cleared}" : ""));
    }

    void CollectSpawnPoints(Transform mapRoot)
    {
        spawnPoints.Clear();
        Transform player = GameBootstrap.FindDeep(mapRoot, "PlayerSpawn");
        if (player != null) spawnPoints.Add(player.position);
        Transform enemies = GameBootstrap.FindDeep(mapRoot, "EnemySpawns");
        if (enemies != null)
            foreach (Transform p in enemies) spawnPoints.Add(p.position);
    }

    bool NearSpawn(Vector2 pos)
    {
        foreach (Vector2 s in spawnPoints)
            if (Mathf.Abs(s.x - pos.x) < spawnClearance && Mathf.Abs(s.y - pos.y) < spawnClearance) return true;
        return false;
    }

    void CreateBush(Tilemap tm, Vector3Int cell, Vector2 center)
    {
        var go = new GameObject("Bush");
        go.transform.SetParent(tm.transform, false);
        go.transform.position = center;

        var col = go.AddComponent<BoxCollider2D>();
        Vector3 cellSize = tm.layoutGrid != null ? tm.layoutGrid.cellSize : Vector3.one;
        col.size = new Vector2(cellSize.x, cellSize.y) * colliderSize;

        Bush bush = go.AddComponent<Bush>();
        bush.Init(this);
        cellOf[bush] = (tm, cell);
        Count++;
    }

    // Вызывает Bush, когда в него попал снаряд
    public void OnBushBroken(Bush bush)
    {
        if (!cellOf.TryGetValue(bush, out var where)) return;
        cellOf.Remove(bush);
        Count--;

        TileBase tile = where.map.GetTile(where.cell);
        if (tile != null && holeFor.TryGetValue(tile, out TileBase hole)) SetHole(where.map, where.cell, hole);

        Vector2 pos = bush.transform.position;
        if (GameFeel.Instance != null) GameFeel.Instance.Play(breakSound, breakVolume);
        AnyBroken?.Invoke(pos);

        // Сетка ботов: клетка снова проходима
        ArenaGrid.RefreshArea(new Bounds(pos, Vector3.one * 2f));
    }

    // Заменить тайл, сохранив поворот/отражение
    static void SetHole(Tilemap tm, Vector3Int cell, TileBase hole)
    {
        Matrix4x4 m = tm.GetTransformMatrix(cell);
        tm.SetTile(cell, hole);
        tm.SetTransformMatrix(cell, m);
    }
}
