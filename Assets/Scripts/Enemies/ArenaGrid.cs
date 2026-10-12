using System.Collections.Generic;
using UnityEngine;

// Сетка проходимости арены для поиска пути ботов (BFS по 8 соседям).
// Строится один раз на карту (GameBootstrap.CurrentMap) при первом обращении.
// Препятствие = не-триггер коллайдер без IDamageable в родителях (танки препятствием не считаются).
public static class ArenaGrid
{
    public const float CellSize = 0.5f;
    const float ClearanceSize = 0.85f;   // клетка проходима, если тут помещается танк (0.75) с запасом

    static GameObject builtFor;
    static bool built;
    static Vector2 origin;               // центр клетки (0,0)
    static int width, height;
    static bool[] walkable;
    static readonly Queue<int> queue = new Queue<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        builtFor = null;
        built = false;
        walkable = null;
    }

    public static int CellCount => built ? walkable.Length : 0;

    // Сетка готова (строит при первом вызове или при смене карты)
    public static bool Ready
    {
        get
        {
            GameObject map = GameBootstrap.CurrentMap;
            if (map == null) return false;
            if (!built || builtFor != map) Build(map);
            return built;
        }
    }

    static void Build(GameObject map)
    {
        builtFor = map;
        built = false;

        bool found = false;
        Bounds bounds = default;
        foreach (Collider2D c in map.GetComponentsInChildren<Collider2D>())
        {
            if (c.isTrigger) continue;
            if (!found) { bounds = c.bounds; found = true; }
            else bounds.Encapsulate(c.bounds);
        }
        if (!found)
        {
            Debug.LogWarning("[ArenaGrid] У карты нет коллайдеров — боты не смогут ездить.");
            return;
        }

        width = Mathf.CeilToInt(bounds.size.x / CellSize);
        height = Mathf.CeilToInt(bounds.size.y / CellSize);
        origin = (Vector2)bounds.min + Vector2.one * (CellSize * 0.5f);
        walkable = new bool[width * height];

        Vector2 box = Vector2.one * ClearanceSize;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                walkable[y * width + x] = !HasWall(Physics2D.OverlapBoxAll(Center(y * width + x), box, 0f));

        built = true;
    }

    static bool HasWall(Collider2D[] hits)
    {
        foreach (Collider2D c in hits)
            if (IsWall(c)) return true;
        return false;
    }

    public static bool IsWall(Collider2D c) => c != null && !c.isTrigger && c.GetComponentInParent<IDamageable>() == null;

    public static bool Walkable(int cell) => cell >= 0 && cell < walkable.Length && walkable[cell];

    public static Vector2 Center(int cell) => origin + new Vector2(cell % width, cell / width) * CellSize;

    // Ближайшая проходимая клетка к точке (танк у стены может стоять в «непроходимой» клетке)
    public static int CellOf(Vector2 pos)
    {
        int cx = Mathf.RoundToInt((pos.x - origin.x) / CellSize);
        int cy = Mathf.RoundToInt((pos.y - origin.y) / CellSize);
        int best = -1;
        float bestDist = float.MaxValue;
        for (int r = 0; r <= 4 && best < 0; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue; // только кольцо радиуса r
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= width || y >= height) continue;
                    int cell = y * width + x;
                    if (!walkable[cell]) continue;
                    float d = (Center(cell) - pos).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; best = cell; }
                }
        return best;
    }

    // BFS из start: dist (в шагах, -1 = недостижимо), parent для восстановления пути, reached — все достижимые
    public static void Bfs(int start, int[] dist, int[] parent, List<int> reached)
    {
        for (int i = 0; i < dist.Length; i++) { dist[i] = -1; parent[i] = -1; }
        reached.Clear();
        if (!Walkable(start)) return;

        queue.Clear();
        queue.Enqueue(start);
        dist[start] = 0;
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            reached.Add(cur);
            int x = cur % width, y = cur / width;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int next = ny * width + nx;
                    if (!walkable[next] || dist[next] >= 0) continue;
                    // По диагонали — только если оба соседа свободны (не срезаем углы стен)
                    if (dx != 0 && dy != 0 && (!walkable[y * width + nx] || !walkable[ny * width + x])) continue;
                    dist[next] = dist[cur] + 1;
                    parent[next] = cur;
                    queue.Enqueue(next);
                }
        }
    }

    // Путь от start до target по parent из Bfs, сглаженный (выкидываем точки, если до дальней видно напрямую)
    public static void BuildPath(int target, int[] parent, Vector2 from, float radius, List<Vector2> path)
    {
        path.Clear();
        for (int c = target; c >= 0; c = parent[c]) path.Add(Center(c));
        path.Reverse();
        if (path.Count > 0) path.RemoveAt(0); // клетка, где стоим

        // Сглаживание
        int i = 0;
        Vector2 cur = from;
        var smooth = new List<Vector2>(path.Count);
        while (i < path.Count)
        {
            int far = i;
            for (int j = path.Count - 1; j > i; j--)
                if (!WallBetween(cur, path[j], radius)) { far = j; break; }
            smooth.Add(path[far]);
            cur = path[far];
            i = far + 1;
        }
        path.Clear();
        path.AddRange(smooth);
    }

    // Есть ли стена между точками для круга радиуса radius
    public static bool WallBetween(Vector2 a, Vector2 b, float radius)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return false;
        foreach (RaycastHit2D h in Physics2D.CircleCastAll(a, radius, d / len, len))
            if (IsWall(h.collider)) return true;
        return false;
    }
}
