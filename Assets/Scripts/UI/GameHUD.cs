using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Интерфейс боя, построенный КОДОМ. Вешается на объект GameManager (лида) в сцене Game.
// Броня игрока, очки и множитель, номер волны, объявления «ВОЛНА N», всплывающие «+250 РИКОШЕТ!»,
// подсказка управления и экран итога после смерти.
// Данные берёт из событий ScoreManager / WaveManager / GameManager (код второго разработчика).
public class GameHUD : MonoBehaviour
{
    [SerializeField] float controlsHintSeconds = 10f;
    [SerializeField] int ricochetPointsThreshold = 200; // очки за убийство не меньше этого = рикошет

    Canvas canvas;
    RectTransform root;
    TextMeshProUGUI scoreText, multText, waveText, announceText, buffText;
    CanvasGroup announceGroup, hintGroup;
    RectTransform hpRow;
    readonly List<Image> hpPips = new List<Image>();
    GameObject resultPanel;

    WaveManager waves;
    float announceUntil;
    float hintUntil;
    int shownMaxHp = -1;

    class Popup { public RectTransform rt; public TextMeshProUGUI text; public float age; }
    readonly List<Popup> popups = new List<Popup>();

    void OnEnable()
    {
        ScoreManager.ScoreChanged += OnScoreChanged;
        ScoreManager.PointsAdded += OnPointsAdded;
        WaveManager.WaveStarted += OnWaveStarted;
        WaveManager.WaveCleared += OnWaveCleared;
        GameManager.GameOver += OnGameOver;
        PowerUp.AnyPicked += OnPowerUpPicked;
    }

    void OnDisable()
    {
        ScoreManager.ScoreChanged -= OnScoreChanged;
        ScoreManager.PointsAdded -= OnPointsAdded;
        WaveManager.WaveStarted -= OnWaveStarted;
        WaveManager.WaveCleared -= OnWaveCleared;
        GameManager.GameOver -= OnGameOver;
        PowerUp.AnyPicked -= OnPowerUpPicked;
    }

    void Start()
    {
        waves = FindAnyObjectByType<WaveManager>();
        Build();
        OnScoreChanged(0);
        hintUntil = Time.unscaledTime + controlsHintSeconds;
    }

    // ================= Построение =================

    void Build()
    {
        canvas = UIKit.CreateCanvas("HUDCanvas", 10);
        root = (RectTransform)canvas.transform;

        // --- Слева сверху: броня ---
        Image hpPanel = UIKit.Box(root, "HP", UIKit.PanelColor);
        UIKit.Place(hpPanel, UIKit.TopLeft, UIKit.TopLeft, new Vector2(20, -20), new Vector2(360, 70));
        var hpLabel = UIKit.Label(hpPanel.transform, "БРОНЯ", 24, UIKit.TextDim, TextAlignmentOptions.Left, bold: true);
        UIKit.Place(hpLabel, UIKit.Left, UIKit.Left, new Vector2(16, 0), new Vector2(110, 40));
        hpRow = UIKit.Node("Pips", hpPanel.transform);
        UIKit.Place(hpRow, UIKit.Left, UIKit.Left, new Vector2(126, 0), new Vector2(220, 34));
        var hl = hpRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 8;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        hl.childControlWidth = false;
        hl.childControlHeight = false;
        hl.childAlignment = TextAnchor.MiddleLeft;

        // Под бронёй: активные бонусы («Усиления на поле»)
        buffText = UIKit.Label(root, "", 26, UIKit.TextColor, TextAlignmentOptions.Left, bold: true);
        UIKit.Place(buffText, UIKit.TopLeft, UIKit.TopLeft, new Vector2(24, -96), new Vector2(900, 40));

        // --- По центру сверху: волна ---
        Image wavePanel = UIKit.Box(root, "Wave", UIKit.PanelColor);
        UIKit.Place(wavePanel, UIKit.Top, UIKit.Top, new Vector2(0, -20), new Vector2(420, 70));
        waveText = UIKit.Label(wavePanel.transform, "Готовься…", 32, UIKit.TextColor, TextAlignmentOptions.Center, bold: true);
        UIKit.Stretch(waveText);

        // --- Справа сверху: очки и множитель ---
        Image scorePanel = UIKit.Box(root, "Score", UIKit.PanelColor);
        UIKit.Place(scorePanel, UIKit.TopRight, UIKit.TopRight, new Vector2(-20, -20), new Vector2(420, 110));
        scoreText = UIKit.Label(scorePanel.transform, "0", 44, UIKit.Accent, TextAlignmentOptions.Right, bold: true);
        UIKit.Place(scoreText, UIKit.TopRight, UIKit.TopRight, new Vector2(-18, -6), new Vector2(390, 56));
        multText = UIKit.Label(scorePanel.transform, "", 20, UIKit.TextDim, TextAlignmentOptions.Right);
        UIKit.Place(multText, UIKit.TopRight, UIKit.TopRight, new Vector2(-18, -62), new Vector2(390, 42));
        string mods = ModifierApplier.Describe(RunConfig.Modifiers);
        multText.text = $"множитель ×{GameRules.ScoreMultiplier:0.##}\n<size=16>{mods}</size>";

        // --- Объявление по центру ---
        announceText = UIKit.Label(root, "", 84, UIKit.Accent, TextAlignmentOptions.Center, bold: true);
        UIKit.Place(announceText, UIKit.Center, UIKit.Center, new Vector2(0, 160), new Vector2(1400, 120));
        announceGroup = announceText.gameObject.AddComponent<CanvasGroup>();
        announceGroup.alpha = 0f;

        // --- Подсказка управления снизу ---
        Image hint = UIKit.Box(root, "ControlsHint", UIKit.PanelColor);
        UIKit.Place(hint, UIKit.Bottom, UIKit.Bottom, new Vector2(0, 20), new Vector2(1500, 64));
        var hintText = UIKit.Label(hint.transform,
            "<color=#F5C44D>WASD</color> — ехать     <color=#F5C44D>Мышь</color> — целиться     <color=#F5C44D>ЛКМ</color> — огонь     Снаряды отскакивают от стен — <color=#F5C44D>рикошет = ×2.5 очков</color>",
            24, UIKit.TextColor);
        UIKit.Stretch(hintText);
        hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
    }

    void RebuildPips(int max)
    {
        foreach (Image pip in hpPips) Destroy(pip.gameObject);
        hpPips.Clear();
        for (int i = 0; i < max; i++)
        {
            Image pip = UIKit.Box(hpRow, "Pip", UIKit.Accent);
            pip.rectTransform.sizeDelta = new Vector2(34, 34);
            var o = pip.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.7f);
            o.effectDistance = new Vector2(2, -2);
            hpPips.Add(pip);
        }
        shownMaxHp = max;
    }

    // ================= Обновление =================

    void Update()
    {
        // Броня
        PlayerTank player = PlayerTank.Instance;
        if (player != null && player.TryGetComponent(out Health hp))
        {
            if (hp.Max != shownMaxHp) RebuildPips(hp.Max);
            for (int i = 0; i < hpPips.Count; i++)
                hpPips[i].color = i < hp.Current ? UIKit.Accent : new Color(0.25f, 0.25f, 0.22f, 1f);
        }
        else
        {
            foreach (Image pip in hpPips) pip.color = new Color(0.25f, 0.25f, 0.22f, 1f);
        }

        // Бонусы
        buffText.text = PlayerBuffs.Instance != null ? PlayerBuffs.Instance.Describe() : "";

        // Волна
        if (waves != null && !GameManager.IsGameOver)
        {
            if (waves.NextWaveIn > 0f)
                waveText.text = waves.CurrentWave == 0
                    ? $"Волна 1 через {waves.NextWaveIn:0.0}"
                    : $"Волна {waves.CurrentWave + 1} через {waves.NextWaveIn:0.0}";
            else if (waves.CurrentWave > 0)
                waveText.text = $"Волна {waves.CurrentWave}   <size=24><color=#B8BAA3>врагов: {waves.AliveCount + waves.LeftToSpawn}</color></size>";
        }

        // Плавные исчезновения
        float now = Time.unscaledTime;
        announceGroup.alpha = Mathf.Clamp01((announceUntil - now) / 0.5f);
        hintGroup.alpha = Mathf.Clamp01((hintUntil - now) / 1f);

        for (int i = popups.Count - 1; i >= 0; i--)
        {
            Popup p = popups[i];
            p.age += Time.unscaledDeltaTime;
            p.rt.anchoredPosition += new Vector2(0, 70f * Time.unscaledDeltaTime);
            p.text.alpha = Mathf.Clamp01(1.2f - p.age);
            if (p.age > 1.2f)
            {
                Destroy(p.rt.gameObject);
                popups.RemoveAt(i);
            }
        }
    }

    void Announce(string text, Color color, float seconds = 1.6f)
    {
        announceText.text = text;
        announceText.color = color;
        announceUntil = Time.unscaledTime + seconds;
    }

    // ================= События =================

    void OnScoreChanged(int score)
    {
        if (scoreText != null) scoreText.text = score.ToString();
    }

    void OnWaveStarted(int wave) => Announce($"ВОЛНА {wave}", UIKit.Accent);

    void OnWaveCleared(int wave) { }

    void OnPointsAdded(int points, Vector2 where)
    {
        if (where == Vector2.zero)
        {
            Announce($"ВОЛНА ЗАЧИЩЕНА  +{points}", UIKit.Easier, 1.4f);
            return;
        }

        bool ricochet = points >= ricochetPointsThreshold;
        SpawnPopup(where, ricochet ? $"+{points}  РИКОШЕТ!" : $"+{points}", ricochet ? 40 : 32,
                   ricochet ? UIKit.Accent : UIKit.TextColor);
    }

    void OnPowerUpPicked(PowerUpType type, Vector2 where) =>
        SpawnPopup(where, "+" + PowerUp.Title(type), 36, PowerUp.ColorOf(type));

    // Всплывающий текст над точкой мира
    void SpawnPopup(Vector2 where, string text, float size, Color color)
    {
        Camera cam = Camera.main;
        if (cam == null || root == null) return;
        Vector2 screen = cam.WorldToScreenPoint(where);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);

        TextMeshProUGUI t = UIKit.Label(root, text, size, color, TextAlignmentOptions.Center, bold: true);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = UIKit.Center;
        rt.sizeDelta = new Vector2(500, 60);
        rt.anchoredPosition = local + new Vector2(0, 40);
        popups.Add(new Popup { rt = rt, text = t });
    }

    void OnGameOver(int finalScore, int best, bool newRecord)
    {
        if (resultPanel != null) return;
        hintGroup.alpha = 0f;
        hintUntil = 0f;

        Image dim = UIKit.Box(root, "Result", new Color(0, 0, 0, 0.65f), raycast: true);
        UIKit.Stretch(dim);
        resultPanel = dim.gameObject;

        Image panel = UIKit.Box(dim.transform, "Panel", UIKit.PanelColor);
        UIKit.Place(panel, UIKit.Center, UIKit.Center, Vector2.zero, new Vector2(820, 600));
        Transform p = panel.transform;

        var title = UIKit.Label(p, "ТАНК УНИЧТОЖЕН", 64, UIKit.Danger, TextAlignmentOptions.Center, bold: true);
        UIKit.Place(title, UIKit.Top, UIKit.Top, new Vector2(0, -30), new Vector2(780, 80));

        ScoreManager sm = ScoreManager.Instance;
        int raw = sm != null ? sm.Score : 0;
        int kills = sm != null ? sm.Kills : 0;
        int ricochets = sm != null ? sm.RicochetKills : 0;
        int wave = waves != null ? waves.CurrentWave : 0;

        var stats = UIKit.Label(p,
            $"Волна: <color=#F5C44D>{wave}</color>     Уничтожено: <color=#F5C44D>{kills}</color>     Рикошетом: <color=#F5C44D>{ricochets}</color>\n\n" +
            $"Очки: {raw} × {GameRules.ScoreMultiplier:0.##} =",
            30, UIKit.TextColor);
        UIKit.Place(stats, UIKit.Top, UIKit.Top, new Vector2(0, -130), new Vector2(780, 140));

        var total = UIKit.Label(p, finalScore.ToString(), 96, UIKit.Accent, TextAlignmentOptions.Center, bold: true);
        UIKit.Place(total, UIKit.Top, UIKit.Top, new Vector2(0, -270), new Vector2(780, 110));

        var record = UIKit.Label(p, newRecord ? "НОВЫЙ РЕКОРД!" : $"Рекорд: {best}", 34,
                                 newRecord ? UIKit.Accent : UIKit.TextDim, TextAlignmentOptions.Center, bold: newRecord);
        UIKit.Place(record, UIKit.Top, UIKit.Top, new Vector2(0, -385), new Vector2(780, 50));

        Button again = UIKit.Btn(p, "ЕЩЁ РАЗ  (R)", 34, UIKit.Accent, UIKit.Dark, QuickRestart.Restart);
        UIKit.Place(again, UIKit.Bottom, UIKit.Bottom, new Vector2(-185, 40), new Vector2(340, 90));
        Button menu = UIKit.Btn(p, "В МЕНЮ  (Esc)", 30, UIKit.ButtonColor, UIKit.TextColor, QuickRestart.ToMenu);
        UIKit.Place(menu, UIKit.Bottom, UIKit.Bottom, new Vector2(185, 40), new Vector2(340, 90));
    }
}
