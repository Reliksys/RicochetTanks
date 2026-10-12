using System.Collections.Generic;
using UnityEngine;

// Линия прицела с предсказанием рикошета. Вешается на игрока рядом с TankGun.
[RequireComponent(typeof(LineRenderer))]
public class AimLine : MonoBehaviour
{
    [SerializeField] TankGun gun;
    [SerializeField] int previewBounces = 1;   // сколько отскоков показывать
    [SerializeField] float maxLength = 25f;
    [SerializeField] float width = 0.06f;
    [SerializeField] Color color = new Color(1f, 1f, 1f, 0.6f);

    LineRenderer line;
    readonly List<Vector3> points = new List<Vector3>();

    void Awake()
    {
        if (gun == null) gun = GetComponent<TankGun>();
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0.1f);
        line.numCapVertices = 2;
        line.sortingOrder = 5;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) line.material = new Material(shader);
    }

    void LateUpdate()
    {
        line.enabled = GameRules.ShowAimLine && gun != null && gun.BulletPrefab != null;
        if (!line.enabled) return;

        Bullet b = gun.BulletPrefab;
        int bounces = Mathf.Max(previewBounces, GameRules.AimPreviewBounces);
        Ricochet.PredictPath(transform.position, gun.AimDirection, b.Radius, bounces,
                             maxLength, b.HitMask, gameObject, points);

        // Линию начинаем от дула, а не из центра танка
        if (points.Count >= 2)
        {
            float firstSegment = Vector3.Distance(points[0], points[1]);
            points[0] = Vector3.MoveTowards(points[0], points[1], Mathf.Min(gun.MuzzleDistance, firstSegment));
        }

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
    }
}
