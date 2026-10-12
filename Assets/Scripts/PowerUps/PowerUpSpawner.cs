using System.Collections.Generic;
using UnityEngine;

// Раскладывает бонусы по арене, когда включён модификатор «Усиления на поле».
// GameBootstrap добавляет этот компонент сам — вручную вешать не нужно.
// Место выбирается по сетке ботов (ArenaGrid): только клетки, до которых игрок может доехать.
public class PowerUpSpawner : MonoBehaviour
{
    [SerializeField] float firstDelay = 6f;
    [SerializeField] float interval = 11f;
    [SerializeField] float intervalJitter = 3f;
    [SerializeField] int maxOnField = 2;
    [SerializeField] float lifetime = 15f;
    [SerializeField] float minDistanceFromPlayer = 3f;
    [SerializeField] float maxDistanceFromPlayer = 14f;
    [SerializeField] float minDistanceBetween = 2.5f;

    float timer;
    AudioClip pickupSound;
    int[] dist, parent;
    readonly List<int> reached = new List<int>();
    readonly List<int> candidates = new List<int>();

    void OnEnable() => PowerUp.AnyPicked += OnPicked;
    void OnDisable() => PowerUp.AnyPicked -= OnPicked;

    void Start()
    {
        timer = firstDelay;
        pickupSound = ProcSprites.PickupSound();
    }

    void Update()
    {
        if (GameManager.IsGameOver || PowerUp.Active.Count >= maxOnField) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = TrySpawn() ? interval + Random.Range(-intervalJitter, intervalJitter) : 1f;
    }

    bool TrySpawn()
    {
        PlayerTank player = PlayerTank.Instance;
        if (player == null || !ArenaGrid.Ready) return false;

        int start = ArenaGrid.CellOf(player.transform.position);
        if (start < 0) return false;

        int n = ArenaGrid.CellCount;
        if (dist == null || dist.Length != n) { dist = new int[n]; parent = new int[n]; }
        ArenaGrid.Bfs(start, dist, parent, reached);

        candidates.Clear();
        foreach (int cell in reached)
        {
            float d = dist[cell] * ArenaGrid.CellSize; // примерно расстояние по дороге
            if (d >= minDistanceFromPlayer && d <= maxDistanceFromPlayer) candidates.Add(cell);
        }

        for (int attempt = 0; attempt < 40 && candidates.Count > 0; attempt++)
        {
            Vector2 pos = ArenaGrid.Center(candidates[Random.Range(0, candidates.Count)]);
            if (!SpotIsFree(pos)) continue;
            PowerUp.Create(PickType(player), pos, lifetime);
            return true;
        }
        return false;
    }

    bool SpotIsFree(Vector2 pos)
    {
        foreach (Collider2D c in Physics2D.OverlapCircleAll(pos, 0.6f))
            if (!c.isTrigger) return false; // стена, куст или танк
        foreach (PowerUp p in PowerUp.Active)
            if (Vector2.Distance(p.transform.position, pos) < minDistanceBetween) return false;
        return true;
    }

    static PowerUpType PickType(PlayerTank player)
    {
        // Ремонт выпадает, только если броня не полная
        bool damaged = player.TryGetComponent(out Health h) && h.Current < h.Max;
        var options = new List<PowerUpType> { PowerUpType.Shield, PowerUpType.Speed, PowerUpType.RapidFire };
        if (damaged) { options.Add(PowerUpType.Repair); options.Add(PowerUpType.Repair); }
        return options[Random.Range(0, options.Count)];
    }

    void OnPicked(PowerUpType type, Vector2 where)
    {
        if (GameFeel.Instance != null) GameFeel.Instance.Play(pickupSound, 0.9f);
        else if (Camera.main != null) AudioSource.PlayClipAtPoint(pickupSound, Camera.main.transform.position, 0.7f);
    }
}
