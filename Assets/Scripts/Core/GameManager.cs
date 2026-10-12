using System;
using UnityEngine;

// Конец боя: смерть игрока → итог = очки × GameRules.ScoreMultiplier, рекорд в PlayerPrefs, событие GameOver.
// Живёт в префабе GameSystems (НЕ на объекте лида с GameBootstrap).
// Пока нет экрана итога от UI-шника — временный OnGUI (выключается галочкой).
public class GameManager : MonoBehaviour
{
    public const string BestScoreKey = "BestScore";

    [SerializeField] ScoreManager scoreManager;   // не задано — найдём сами
    [SerializeField] WaveManager waveManager;

    [Header("Временный интерфейс (пока нет HUD и экрана итога)")]
    [SerializeField] bool showTempHud = true;
    [SerializeField] bool showTempGameOver = true;

    public static GameManager Instance { get; private set; }
    public static bool IsGameOver { get; private set; }

    // (итог с множителем, рекорд, новый ли рекорд)
    public static event Action<int, int, bool> GameOver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        IsGameOver = false;
        GameOver = null;
    }

    public static int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);

    public int FinalScore { get; private set; }
    public bool NewRecord { get; private set; }

    void Awake()
    {
        Instance = this;
        IsGameOver = false; // Scene Reload выключен — сбрасываем при каждом старте боя
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
        if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable() => Health.AnyDied += OnAnyDied;
    void OnDisable() => Health.AnyDied -= OnAnyDied;

    void OnAnyDied(Health victim, DamageInfo info)
    {
        if (IsGameOver || victim.GetComponent<PlayerTank>() == null) return;
        EndGame();
    }

    void EndGame()
    {
        IsGameOver = true;
        if (waveManager != null) waveManager.Stop();

        int raw = scoreManager != null ? scoreManager.Score : 0;
        FinalScore = Mathf.RoundToInt(raw * GameRules.ScoreMultiplier);

        int best = BestScore;
        NewRecord = FinalScore > best;
        if (NewRecord)
        {
            best = FinalScore;
            PlayerPrefs.SetInt(BestScoreKey, best);
            PlayerPrefs.Save();
        }

        if (GameFeel.Instance != null) GameFeel.Instance.PlayGameOver();
        Debug.Log($"[Game] Конец боя: {raw} × {GameRules.ScoreMultiplier:0.##} = {FinalScore} (рекорд {best}{(NewRecord ? ", новый!" : "")})");
        GameOver?.Invoke(FinalScore, best, NewRecord);
    }

    // ---------- Временный интерфейс ----------

    GUIStyle bigStyle, hudStyle;

    void OnGUI()
    {
        if (bigStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperRight };
            bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
        }

        if (showTempHud && !IsGameOver)
        {
            int score = scoreManager != null ? scoreManager.Score : 0;
            string wave = "";
            if (waveManager != null)
            {
                wave = $"Волна {waveManager.CurrentWave}";
                if (waveManager.NextWaveIn > 0f) wave = $"Волна {waveManager.CurrentWave + 1} через {waveManager.NextWaveIn:0.0}";
            }
            GUI.Label(new Rect(Screen.width - 410, 10, 400, 60), $"Очки: {score}\n{wave}", hudStyle);
        }

        if (showTempGameOver && IsGameOver)
        {
            float w = 500, h = 260;
            Rect r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(r, GUIContent.none);
            int raw = scoreManager != null ? scoreManager.Score : 0;
            string text = $"Игра окончена\n\nОчки: {raw} × {GameRules.ScoreMultiplier:0.##} = {FinalScore}\n" +
                          (NewRecord ? "Новый рекорд!" : $"Рекорд: {BestScore}");
            GUI.Label(new Rect(r.x, r.y + 10, w, 170), text, bigStyle);
            if (GUI.Button(new Rect(r.x + w / 2 - 100, r.y + h - 60, 200, 40), "Ещё раз (R)"))
                QuickRestart.Restart();
        }
    }
}
