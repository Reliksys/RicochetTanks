using System.Collections;
using UnityEngine;

// Анимация выстрела башни из кадров листа turret_01_mk1 (кадр 0 — покой, 1–7 — выстрел).
// Вешается на объект Turret (где SpriteRenderer). Работает и у игрока, и у врагов.
[RequireComponent(typeof(SpriteRenderer))]
public class TurretFireAnimation : MonoBehaviour
{
    [Tooltip("Все кадры по порядку: turret_01_mk1_0 … turret_01_mk1_7")]
    [SerializeField] Sprite[] frames;
    [SerializeField] float frameTime = 0.03f;

    SpriteRenderer sr;
    TankGun gun;
    Coroutine playing;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        gun = GetComponentInParent<TankGun>();
    }

    void OnEnable() { if (gun != null) gun.Fired += OnFired; }
    void OnDisable() { if (gun != null) gun.Fired -= OnFired; }

    void OnFired(Bullet bullet)
    {
        if (frames == null || frames.Length < 2) return;
        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        for (int i = 1; i < frames.Length; i++)
        {
            sr.sprite = frames[i];
            yield return new WaitForSeconds(frameTime);
        }
        sr.sprite = frames[0];
        playing = null;
    }
}
