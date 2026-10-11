using System;
using UnityEngine;
using UnityEngine.UI;

namespace LudoTurbo
{
    /// <summary>Small helpers for building uGUI elements from code.</summary>
    public static class Ui
    {
        static Font font;

        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static RectTransform Fill(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 pos, bool sliced = false)
        {
            var img = Rect(name, parent, size, pos).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text Label(Transform parent, string text, int fontSize, Color color, Vector2 size, Vector2 pos,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var label = Rect("Label", parent, size, pos).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button Button(Transform parent, string text, Color bg, Vector2 size, Vector2 pos, Action onClick, int fontSize = 46)
        {
            var img = Image("Button", parent, SpriteFactory.Rounded, bg, size, pos, sliced: true);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            Label(img.transform, text, fontSize, Palette.Text, size, Vector2.zero);
            return button;
        }

        public static void SetText(Button button, string text) => button.GetComponentInChildren<Text>().text = text;
    }
}
