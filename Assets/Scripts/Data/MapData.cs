using UnityEngine;

// Описание карты для меню. Create → Tanks → Map.
// prefab — префаб карты по формату из CONTEXT.md (Grid/Walls + PlayerSpawn + EnemySpawns).
[CreateAssetMenu(menuName = "Tanks/Map", fileName = "MapData_New")]
public class MapData : ScriptableObject
{
    public string displayName = "Новая карта";
    [TextArea(1, 3)] public string description = "";
    public GameObject prefab;
    public Sprite preview;
    [Tooltip("Может ли карта выпасть в «Быстрой игре». Необычные карты (Коридоры) — выключить, они доступны в «Настроить бой».")]
    public bool allowInQuickPlay = true;
}
