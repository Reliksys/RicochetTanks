using UnityEngine;

// "Сочность" боя: звуки, тряска камеры и эффекты частиц на события игры.
// Вешается на объект GameManager в сцене Game. Ничего не знает о врагах/волнах — слушает глобальные события.
// Эффекты (префабы частиц от художника) просто перетащить в поля — код их сам создаст и удалит.
public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance { get; private set; }

    [Header("Звуки")]
    [SerializeField] AudioClip playerShoot;
    [SerializeField] AudioClip enemyShoot;
    [SerializeField] AudioClip ricochet;
    [SerializeField] AudioClip playerHit;
    [SerializeField] AudioClip explosion;
    [SerializeField] AudioClip waveStart;
    [SerializeField] AudioClip gameOver;
    [SerializeField, Range(0f, 1f)] float volume = 0.8f;
    [Tooltip("Не чаще одного звука рикошета за столько секунд (чтобы не трещало)")]
    [SerializeField] float ricochetSoundCooldown = 0.05f;

    [Header("Музыка (необязательно)")]
    [SerializeField] AudioClip music;
    [SerializeField, Range(0f, 1f)] float musicVolume = 0.35f;

    [Header("Эффекты (префабы частиц, необязательно)")]
    [SerializeField] GameObject muzzleFlashPrefab;
    [SerializeField] GameObject ricochetSparksPrefab;
    [SerializeField] GameObject explosionPrefab;
    [SerializeField] float effectLifetime = 2f;

    [Header("Тряска камеры")]
    [SerializeField] float shootShake = 0.04f;
    [SerializeField] float playerHitShake = 0.3f;
    [SerializeField] float enemyDeathShake = 0.15f;
    [SerializeField] float playerDeathShake = 0.5f;
    [SerializeField] float shakeDuration = 0.25f;

    AudioSource sfx;
    Camera cam;
    Vector3 cameraBase;
    float shakeTimeLeft;
    float shakeStrength;
    float nextRicochetSound;

    void Awake()
    {
        Instance = this;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        if (music != null)
        {
            AudioSource m = gameObject.AddComponent<AudioSource>();
            m.clip = music;
            m.loop = true;
            m.volume = musicVolume;
            m.spatialBlend = 0f;
            m.Play();
        }
    }

    void Start()
    {
        // Start — после GameBootstrap.Awake, камера уже подогнана под карту
        cam = Camera.main;
        if (cam != null) cameraBase = cam.transform.position;
    }

    void OnEnable()
    {
        TankGun.AnyFired += OnFired;
        Bullet.AnyBounced += OnBounced;
        Health.AnyDamaged += OnDamaged;
        Health.AnyDied += OnDied;
    }

    void OnDisable()
    {
        TankGun.AnyFired -= OnFired;
        Bullet.AnyBounced -= OnBounced;
        Health.AnyDamaged -= OnDamaged;
        Health.AnyDied -= OnDied;
        if (Instance == this) Instance = null;
    }

    // ---------- Публичное API (можно вызывать из WaveManager / GameManager) ----------
    public void PlayWaveStart() => Play(waveStart);
    public void PlayGameOver() => Play(gameOver);
    public void Shake(float strength)
    {
        if (strength >= shakeStrength * (shakeTimeLeft / Mathf.Max(0.01f, shakeDuration)))
        {
            shakeStrength = strength;
            shakeTimeLeft = shakeDuration;
        }
    }
    public void Play(AudioClip clip, float volumeScale = 1f)
    {
        if (clip != null) sfx.PlayOneShot(clip, volume * volumeScale);
    }

    // ---------- Реакции на события ----------
    void OnFired(TankGun gun, Bullet bullet)
    {
        bool isPlayer = gun.GetComponent<PlayerTank>() != null;
        Play(isPlayer ? playerShoot : enemyShoot, isPlayer ? 1f : 0.6f);
        if (isPlayer) Shake(shootShake);
        if (bullet != null) Spawn(muzzleFlashPrefab, bullet.transform.position, bullet.transform.rotation);
    }

    void OnBounced(Bullet bullet, Vector2 point)
    {
        Spawn(ricochetSparksPrefab, point, Quaternion.identity);
        if (Time.time < nextRicochetSound) return;
        nextRicochetSound = Time.time + ricochetSoundCooldown;
        Play(ricochet, 0.7f);
    }

    void OnDamaged(Health health, DamageInfo info)
    {
        if (health.GetComponent<PlayerTank>() == null) return;
        Play(playerHit);
        Shake(playerHitShake);
    }

    void OnDied(Health health, DamageInfo info)
    {
        bool isPlayer = health.GetComponent<PlayerTank>() != null;
        Play(explosion, isPlayer ? 1f : 0.8f);
        Shake(isPlayer ? playerDeathShake : enemyDeathShake);
        Spawn(explosionPrefab, health.transform.position, Quaternion.identity);
    }

    void Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, new Vector3(position.x, position.y, 0f), rotation);
        Destroy(fx, effectLifetime);
    }

    void LateUpdate()
    {
        if (cam == null) return;
        if (shakeTimeLeft > 0f)
        {
            shakeTimeLeft -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(shakeTimeLeft / shakeDuration);
            Vector2 offset = Random.insideUnitCircle * shakeStrength * k;
            cam.transform.position = cameraBase + new Vector3(offset.x, offset.y, 0f);
        }
        else
        {
            shakeStrength = 0f;
            cam.transform.position = cameraBase;
        }
    }
}
