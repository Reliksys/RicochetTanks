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
    [Tooltip("Тёмная обводка под линией, чтобы её было видно на светлых картах")]
    [SerializeField] Color outlineColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] float outlineWidth = 0.14f;
    [Tooltip("Прозрачность в конце линии (0 = совсем исчезает)")]
    [SerializeField] float endAlpha = 0.35f;

    LineRenderer line, outline;
    readonly List<Vector3> points = new List<Vector3>();

    void Awake()
    {
        if (gun == null) gun = GetComponent<TankGun>();
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, color.a * endAlpha);
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.sortingOrder = 6;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) line.material = new Material(shader);

        // Обводка: вторая линия пошире и потемнее, рисуется под основной
        var go = new GameObject("AimLineOutline");
        go.transform.SetParent(transform, false);
        outline = go.AddComponent<LineRenderer>();
        outline.useWorldSpace = true;
        outline.startWidth = outlineWidth;
        outline.endWidth = outlineWidth;
        outline.startColor = outlineColor;
        outline.endColor = new Color(outlineColor.r, outlineColor.g, outlineColor.b, outlineColor.a * endAlpha);
        outline.numCapVertices = 2;
        outline.numCornerVertices = 2;
        outline.sortingOrder = 5;
        outline.material = line.material;
    }

    void LateUpdate()
    {
        line.enabled = GameRules.ShowAimLine && gun != null && gun.BulletPrefab != null;
        outline.enabled = line.enabled;
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

        Vector3[] arr = points.ToArray();
        line.positionCount = arr.Length;
        line.SetPositions(arr);
        outline.positionCount = arr.Length;
        outline.SetPositions(arr);
    }
}
