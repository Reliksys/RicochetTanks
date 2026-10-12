using System.Collections.Generic;
using UnityEngine;

// Общая математика полёта снаряда. Используется и самим снарядом, и линией прицела,
// и (завтра) ИИ врагов, чтобы искать рикошетные выстрелы. Логика одна, значит прицел никогда не врёт.
public static class Ricochet
{
    const float Skin = 0.01f; // на сколько отодвигаемся от стены после отскока

    // Поворот спрайта так, чтобы его "верх" (transform.up) смотрел в direction.
    public static Quaternion DirToRotation(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        return Quaternion.Euler(0f, 0f, angle);
    }

    // Пускаем круг радиусом radius из origin в direction на distance.
    // Возвращает ближайшее попадание, игнорируя триггеры и объект ignore (с детьми).
    public static bool Cast(Vector2 origin, float radius, Vector2 direction, float distance,
                            LayerMask mask, GameObject ignore, out RaycastHit2D hit)
    {
        hit = default;
        bool found = false;
        float best = float.MaxValue;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, radius, direction, distance, mask);
        foreach (RaycastHit2D h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (h.distance <= 0f) continue; // уже касались на старте, это не новое попадание
            if (ignore != null && h.collider.transform.IsChildOf(ignore.transform)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                hit = h;
                found = true;
            }
        }
        return found;
    }

    // Точка, откуда продолжать движение после отскока.
    public static Vector2 PointAfterBounce(RaycastHit2D hit) => hit.centroid + hit.normal * Skin;

    // Предсказывает путь снаряда: заполняет points (первая точка = origin).
    // Останавливается на первом объекте с IDamageable, после bounces отскоков или через maxLength.
    // Возвращает IDamageable, в который прилетит снаряд (или null).
    public static IDamageable PredictPath(Vector2 origin, Vector2 direction, float radius, int bounces,
                                          float maxLength, LayerMask mask, GameObject ignore,
                                          List<Vector3> points)
    {
        points.Clear();
        points.Add(origin);
        direction = direction.normalized;
        float left = maxLength;

        for (int i = 0; i <= bounces && left > 0f; i++)
        {
            if (!Cast(origin, radius, direction, left, mask, ignore, out RaycastHit2D hit))
            {
                points.Add(origin + direction * left);
                return null;
            }

            points.Add(hit.centroid);

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null) return target;

            left -= hit.distance;
            origin = PointAfterBounce(hit);
            direction = Vector2.Reflect(direction, hit.normal).normalized;
        }
        return null;
    }
}
