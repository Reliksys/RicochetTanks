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
}
