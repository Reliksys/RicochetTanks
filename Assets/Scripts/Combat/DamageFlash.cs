using UnityEngine;

// Мигание спрайтов танка после попадания (пока действует неуязвимость).
// PlayerTank добавляет этот компонент сам, вручную вешать не нужно.
[RequireComponent(typeof(Health))]
public class DamageFlash : MonoBehaviour
{
    [SerializeField] float blinkInterval = 0.08f;
    [Tooltip("Длительность мигания, если у Health нет неуязвимости")]
    [SerializeField] float fallbackDuration = 0.3f;

    Health health;
    SpriteRenderer[] renderers;
    float flashUntil;
    bool wasFlashing;

    void Awake()
    {
        health = GetComponent<Health>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    void OnEnable() => health.Damaged += OnDamaged;
    void OnDisable() => health.Damaged -= OnDamaged;

    void OnDamaged(Health h, DamageInfo info)
    {
        float duration = h.InvulnerableDuration > 0f ? h.InvulnerableDuration : fallbackDuration;
        flashUntil = Time.time + duration;
    }

    void Update()
    {
        bool flashing = Time.time < flashUntil;
        if (!flashing && !wasFlashing) return;

        bool visible = !flashing || Mathf.Repeat(Time.time, blinkInterval * 2f) < blinkInterval;
        foreach (SpriteRenderer r in renderers)
            if (r != null) r.enabled = visible;
        wasFlashing = flashing;
    }
}
