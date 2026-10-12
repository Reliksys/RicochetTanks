using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Инструменты редактора для карт (папка Editor — в игру не попадает).
// 1) Меню Tanks → Проверить карты: проверяет все карты из GameDatabase и пишет ошибки в Console
//    (клик по ошибке подсвечивает префаб карты).
// 2) В окне Scene точки спавна рисуются кружками: синий — игрок, красный — враги, жёлтый крест — точка с ошибкой.
public static class MapValidator
{
    const float TankClearance = 0.9f;   // от центра точки до ближайшей стены (танк 0.75 + запас)
    const float MinPlayerDistance = 6f; // точки врагов не ближе к игроку
    const int MinEnemySpawns = 4;

    [MenuItem("Tanks/Проверить карты")]
    public static void ValidateAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:GameDatabase");
        if (guids.Length == 0) { Debug.LogError("[Карты] Не найден GameDatabase"); return; }
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));

        int errors = 0;
        if (db.maps.Count == 0) { Debug.LogError("[Карты] В GameDatabase пустой список Maps", db); errors++; }

        foreach (MapData map in db.maps)
        {
            if (map == null) { Debug.LogError("[Карты] Пустой элемент в списке Maps", db); errors++; continue; }
            if (map.prefab == null) { Debug.LogError($"[Карты] У «{map.displayName}» не указан Prefab", map); errors++; continue; }
            errors += Validate(map.prefab, map.displayName);
        }

        if (errors == 0) Debug.Log($"[Карты] Все {db.maps.Count} карт в порядке ✔");
        else Debug.LogWarning($"[Карты] Найдено проблем: {errors}");
    }

    // Возвращает число ошибок
    public static int Validate(GameObject mapRoot, string title)
    {
        int errors = 0;
        Tilemap walls = FindTilemap(mapRoot.transform, "Walls");
        Transform playerSpawn = GameBootstrap.FindDeep(mapRoot.transform, "PlayerSpawn");
        Transform enemySpawns = GameBootstrap.FindDeep(mapRoot.transform, "EnemySpawns");

        if (mapRoot.transform.position != Vector3.zero)
        { Err(mapRoot, title, "корень карты должен стоять в (0,0,0)"); errors++; }
        if (walls == null) { Err(mapRoot, title, "нет Tilemap «Walls»"); return errors + 1; }
        if (walls.GetComponent<CompositeCollider2D>() == null || walls.GetComponent<TilemapCollider2D>() == null)
        { Err(mapRoot, title, "на Walls нужны TilemapCollider2D + CompositeCollider2D"); errors++; }
        if (playerSpawn == null) { Err(mapRoot, title, "нет объекта «PlayerSpawn»"); errors++; }
        if (enemySpawns == null) { Err(mapRoot, title, "нет объекта «EnemySpawns»"); return errors + 1; }
        if (enemySpawns.childCount < MinEnemySpawns)
        { Err(mapRoot, title, $"точек врагов {enemySpawns.childCount}, нужно хотя бы {MinEnemySpawns}"); errors++; }

        if (playerSpawn != null && !SpotIsClear(walls, playerSpawn.position, out string why))
        { Err(mapRoot, title, $"PlayerSpawn {(Vector2)playerSpawn.position}: {why}"); errors++; }

        var seen = new HashSet<Vector2Int>();
        foreach (Transform p in enemySpawns)
        {
            Vector2 pos = p.position;
            if (!SpotIsClear(walls, pos, out string reason))
            { Err(mapRoot, title, $"{p.name} {pos}: {reason}"); errors++; }
            if (!seen.Add(Vector2Int.RoundToInt(pos * 2f)))
            { Err(mapRoot, title, $"{p.name} {pos}: совпадает с другой точкой"); errors++; }
            if (playerSpawn != null && Vector2.Distance(pos, playerSpawn.position) < MinPlayerDistance)
            { Err(mapRoot, title, $"{p.name} {pos}: слишком близко к игроку (< {MinPlayerDistance})"); errors++; }
        }
        return errors;
    }

    // Точка внутри арены и вокруг неё нет стен ближе TankClearance
    public static bool SpotIsClear(Tilemap walls, Vector2 pos, out string reason)
    {
        BoundsInt b = walls.cellBounds;
        Vector3 min = walls.CellToWorld(b.min), max = walls.CellToWorld(b.max);
        if (pos.x <= min.x || pos.x >= max.x || pos.y <= min.y || pos.y >= max.y)
        { reason = "за пределами карты"; return false; }

        Vector3Int c = walls.WorldToCell(pos);
        Vector3 cell = walls.cellSize;
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                var cc = new Vector3Int(c.x + dx, c.y + dy, 0);
                if (!walls.HasTile(cc)) continue;
                Vector3 cmin = walls.CellToWorld(cc);
                float nx = Mathf.Clamp(pos.x, cmin.x, cmin.x + cell.x);
                float ny = Mathf.Clamp(pos.y, cmin.y, cmin.y + cell.y);
                float dist = Vector2.Distance(pos, new Vector2(nx, ny));
                if (dist < TankClearance)
                {
                    reason = dist <= 0f ? "внутри стены" : $"слишком близко к стене ({dist:0.00})";
                    return false;
                }
            }
        reason = null;
        return true;
    }

    static void Err(GameObject map, string title, string text) =>
        Debug.LogError($"[Карты] «{title}» ({map.name}): {text}", map);

    static Tilemap FindTilemap(Transform root, string name)
    {
        Transform t = GameBootstrap.FindDeep(root, name);
        return t != null ? t.GetComponent<Tilemap>() : null;
    }

    // ---------- Подсветка точек в окне Scene ----------
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    static void DrawSpawnGizmo(Transform t, GizmoType type)
    {
        bool isPlayer = t.name == "PlayerSpawn";
        bool isEnemy = !isPlayer && t.name.StartsWith("EnemySpawn") && t.name != "EnemySpawns";
        if (!isPlayer && !isEnemy) return;

        // Плоские 2D-кружки (Handles), а не 3D-сферы: это только подсказка в окне Scene, в игре их нет
        Vector3 p = t.position;
        Color color = isPlayer ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.3f, 0.25f);
        Handles.color = color;
        Handles.DrawWireDisc(p, Vector3.forward, 0.4f, 2f);
        Handles.color = new Color(color.r, color.g, color.b, 0.35f);
        Handles.DrawSolidDisc(p, Vector3.forward, 0.4f);

        // Подсветка ошибки
        Transform mapRoot = t.root;
        Tilemap walls = FindTilemap(mapRoot, "Walls");
        if (walls != null && !SpotIsClear(walls, p, out _))
        {
            Handles.color = Color.yellow;
            Handles.DrawLine(p + new Vector3(-0.5f, -0.5f), p + new Vector3(0.5f, 0.5f), 3f);
            Handles.DrawLine(p + new Vector3(-0.5f, 0.5f), p + new Vector3(0.5f, -0.5f), 3f);
        }
    }
}
