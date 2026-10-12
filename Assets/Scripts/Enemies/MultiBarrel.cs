using System.Collections.Generic;
using UnityEngine;

// Несколько стволов: на каждый выстрел TankGun выпускает barrels параллельных снарядов.
// TankGun не меняем: ловим его событие Fired, переставляем первый снаряд на свой ствол и добавляем остальные.
// Вешается на корень танка рядом с TankGun.
[RequireComponent(typeof(TankGun))]
public class MultiBarrel : MonoBehaviour
{
    [SerializeField] int barrels = 2;
    [Tooltip("Расстояние между соседними стволами (подогнать под спрайт башни)")]
    [SerializeField] float spacing = 0.22f;

    TankGun gun;
    bool spawning;   // защита от повторного входа, пока создаём свои снаряды

    void Awake() => gun = GetComponent<TankGun>();
    void OnEnable() => gun.Fired += OnFired;
    void OnDisable() => gun.Fired -= OnFired;

    // Смещения стволов от центра танка для направления dir (для ИИ: симуляция каждого снаряда)
    public void GetBarrelOffsets(Vector2 dir, List<Vector2> offsets)
    {
        offsets.Clear();
        Vector2 perp = new Vector2(-dir.y, dir.x).normalized;
        for (int i = 0; i < barrels; i++)
            offsets.Add(perp * ((i - (barrels - 1) * 0.5f) * spacing));
    }

    readonly List<Vector2> offsets = new List<Vector2>();

    void OnFired(Bullet first)
    {
        if (spawning || barrels < 2) return;
        spawning = true;

        Vector2 center = transform.position;
        Vector2 dir = gun.AimDirection;
        GetBarrelOffsets(dir, offsets);

        // Первый снаряд — на первый ствол (если он уже успел отскочить у стены, не трогаем)
        if (first != null && first.Bounces == 0)
        {
            first.transform.position = center + offsets[0];
            first.Launch(dir, gameObject, gun.MuzzleDistance);
        }

        // Остальные стволы
        for (int i = 1; i < offsets.Count; i++)
        {
            Bullet b = Instantiate(gun.BulletPrefab, center + offsets[i], Quaternion.identity);
            b.Launch(dir, gameObject, gun.MuzzleDistance);
        }

        spawning = false;
    }
}
