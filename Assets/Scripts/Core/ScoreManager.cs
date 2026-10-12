using System;
using UnityEngine;

// Очки за бой (до умножения на GameRules.ScoreMultiplier — это делает GameManager в итоге).
// Убийство врага игроком 100, рикошетом 250, враг убит врагом 50, зачистка волны 50 × N.
public class ScoreManager : MonoBehaviour
{
    [SerializeField] int killPoints = 100;
    [SerializeField] int ricochetKillPoints = 250;
    [SerializeField] int friendlyKillPoints = 50;     // враг убит врагом (или сам себя)
    [SerializeField] int waveClearPointsPerWave = 50; // × номер волны

    public static ScoreManager Instance { get; private set; }

    // Для HUD: новый счёт
    public static event Action<int> ScoreChanged;
    // Для всплывающих цифр: (сколько, где; Vector2.zero — бонус за волну)
    public static event Action<int, Vector2> PointsAdded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        ScoreChanged = null;
        PointsAdded = null;
    }

    public int Score { get; private set; }
    public int Kills { get; private set; }
    public int RicochetKills { get; private set; }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        Health.AnyDied += OnAnyDied;
        WaveManager.WaveCleared += OnWaveCleared;
    }

    void OnDisable()
    {
        Health.AnyDied -= OnAnyDied;
        WaveManager.WaveCleared -= OnWaveCleared;
    }

    void OnAnyDied(Health victim, DamageInfo info)
    {
        if (GameManager.IsGameOver) return;
        if (!IsEnemy(victim.gameObject)) return;

        bool byPlayer = info.source != null && info.source.GetComponent<PlayerTank>() != null;
        int points;
        if (!byPlayer) points = friendlyKillPoints;
        else if (info.bounces > 0) { points = ricochetKillPoints; RicochetKills++; }
        else points = killPoints;

        if (byPlayer) Kills++;
        Add(points, victim.transform.position);
    }

    void OnWaveCleared(int wave)
    {
        if (GameManager.IsGameOver) return;
        Add(waveClearPointsPerWave * wave, Vector2.zero);
    }

    void Add(int points, Vector2 where)
    {
        Score += points;
        PointsAdded?.Invoke(points, where);
        ScoreChanged?.Invoke(Score);
    }

    static bool IsEnemy(GameObject go) => go.GetComponent<EnemyAI>() != null || go.CompareTag("Enemy");
}
