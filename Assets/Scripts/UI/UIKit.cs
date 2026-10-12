using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Набор помощников, чтобы строить интерфейс кодом (без вёрстки в редакторе).
// Все размеры — в пикселях эталонного экрана 1920×1080 (Canvas сам масштабируется).
public static class UIKit
{
    // ---------- Палитра (военный пиксель-арт: олива + жёлтый акцент) ----------
    public static readonly Color PanelColor = new Color(0.08f, 0.11f, 0.07f, 0.90f);
    public static readonly Color PanelLight = new Color(0.17f, 0.22f, 0.14f, 0.95f);
    public static readonly Color ButtonColor = new Color(0.27f, 0.34f, 0.21f, 1f);
    public static readonly Color Accent = new Color(0.96f, 0.77f, 0.30f, 1f);
    public static readonly Color AccentDim = new Color(0.45f, 0.37f, 0.14f, 1f);
    public static readonly Color TextColor = new Color(0.95f, 0.93f, 0.86f, 1f);
    public static readonly Color TextDim = new Color(0.72f, 0.73f, 0.64f, 1f);
    public static readonly Color Dark = new Color(0.07f, 0.08f, 0.06f, 1f);
    public static readonly Color Harder = new Color(1f, 0.62f, 0.36f, 1f);
    public static readonly Color Easier = new Color(0.56f, 0.80f, 1f, 1f);
    public static readonly Color Danger = new Color(0.88f, 0.32f, 0.27f, 1f);

    public static Canvas CreateCanvas(string name, int sortingOrder = 0)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        EnsureEventSystem();
        return canvas;
    }

    // Без EventSystem кнопки не нажимаются. Используем модуль нового Input System.
    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    public static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image Box(Transform parent, string name, Color color, bool raycast = false)
    {
        RectTransform rt = Node(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static Image Picture(Transform parent, string name, Sprite sprite)
    {
        Image img = Box(parent, name, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.enabled = sprite != null;
        return img;
    }

    public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
                                        TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
    {
        RectTransform rt = Node("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    public static Button Btn(Transform parent, string label, float fontSize, Color fill, Color textColor, Action onClick)
    {
        Image img = Box(parent, "Button_" + label, fill, raycast: true);
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock cb = button.colors;
        cb.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        cb.highlightedColor = Color.white;
        cb.selectedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        cb.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        cb.fadeDuration = 0.06f;
        button.colors = cb;

        var outline = img.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(3f, -3f);

        TextMeshProUGUI text = Label(img.transform, label, fontSize, textColor, TextAlignmentOptions.Center, bold: true);
        Stretch(text.rectTransform, 8, 8, 4, 4);

        if (onClick != null) button.onClick.AddListener(() => onClick());
        return button;
    }

    public static TextMeshProUGUI ButtonLabel(Button b) => b.GetComponentInChildren<TextMeshProUGUI>();

    // Растянуть на весь родитель с отступами
    public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // Поставить элемент: anchor — точка родителя (0..1), pivot — точка элемента, pos — смещение, size — размер
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static void Place(Component c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) =>
        Place((RectTransform)c.transform, anchor, pivot, pos, size);

    public static void Stretch(Component c, float left = 0, float right = 0, float top = 0, float bottom = 0) =>
        Stretch((RectTransform)c.transform, left, right, top, bottom);

    // Часто используемые точки привязки
    public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    public static readonly Vector2 Top = new Vector2(0.5f, 1f);
    public static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
    public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    public static readonly Vector2 TopRight = new Vector2(1f, 1f);
    public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    public static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    public static readonly Vector2 Left = new Vector2(0f, 0.5f);
    public static readonly Vector2 Right = new Vector2(1f, 0.5f);
}
