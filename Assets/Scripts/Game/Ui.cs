using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LudoGame
{
    /// <summary>Small helpers for building uGUI elements from code.</summary>
    public static class Ui
    {
        static Font font;

        /// <summary>Lilita One (SIL Open Font License), falling back to Unity's built-in font.</summary>
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("Fonts/LilitaOne-Regular");
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

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
            TextAnchor anchor = TextAnchor.MiddleCenter, bool shadow = true)
        {
            var label = Rect("Label", parent, size, pos).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var s = label.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.35f);
                s.effectDistance = new Vector2(0f, -4f);
            }
            return label;
        }

        /// <summary>A chunky button with a darker edge underneath; it presses down when touched.</summary>
        public static Button Button(Transform parent, string text, Color bg, Vector2 size, Vector2 pos, Action onClick, int fontSize = 50)
        {
            var root = Rect("Button", parent, size, pos);
            var edge = Image("Edge", root, SpriteFactory.Rounded, Palette.Darken(bg, 0.68f), size, new Vector2(0f, -10f), sliced: true);
            var face = Image("Face", root, SpriteFactory.Rounded, bg, size, Vector2.zero, sliced: true);
            face.raycastTarget = true;
            Image("Shine", face.transform, SpriteFactory.Rounded, new Color(1f, 1f, 1f, 0.14f), new Vector2(size.x - 24f, size.y * 0.42f), new Vector2(0f, size.y * 0.22f), sliced: true);
            Label(face.transform, text, fontSize, Palette.Text, size, new Vector2(0f, 3f));

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<ButtonPress>().Init(face.rectTransform, edge);
            button.onClick.AddListener(() =>
            {
                Sfx.Play("click", 0.6f);
                Haptics.Tap(10, 60);
                onClick();
            });
            return button;
        }

        public static void SetText(Button button, string text) => button.GetComponentInChildren<Text>().text = text;

        public static void SetColor(Button button, Color color)
        {
            button.targetGraphic.color = color;
            var press = button.GetComponent<ButtonPress>();
            if (press != null) press.SetEdge(Palette.Darken(color, 0.68f));
        }

        /// <summary>A coloured circle with a friendly face, used for player avatars.</summary>
        public static RectTransform Avatar(Transform parent, Color color, float size, Vector2 pos)
        {
            var ring = Image("Avatar", parent, SpriteFactory.Circle, Color.white, new Vector2(size, size), pos);
            Image("Fill", ring.transform, SpriteFactory.Circle, color, new Vector2(size * 0.86f, size * 0.86f), Vector2.zero);
            Image("Face", ring.transform, SpriteFactory.Face, new Color(1f, 1f, 1f, 0.95f), new Vector2(size * 0.6f, size * 0.6f), new Vector2(0f, -size * 0.02f));
            return ring.rectTransform;
        }
    }

    /// <summary>Moves a button's face down onto its edge while it is held.</summary>
    public sealed class ButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        RectTransform face;
        Image edge;
        bool down;

        public void Init(RectTransform face, Image edge)
        {
            this.face = face;
            this.edge = edge;
        }

        public void SetEdge(Color color) => edge.color = color;

        public void OnPointerDown(PointerEventData eventData)
        {
            down = true;
            face.anchoredPosition = new Vector2(0f, -8f);
            FrameGovernor.Keep(0.3f);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        void Release()
        {
            if (!down) return;
            down = false;
            face.anchoredPosition = Vector2.zero;
            FrameGovernor.Keep(0.3f);
        }
    }
}
