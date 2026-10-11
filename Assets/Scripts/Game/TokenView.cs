using System;
using LudoTurbo.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LudoTurbo
{
    public sealed class TokenView
    {
        public readonly PlayerColor Color;
        public readonly int Index;
        public readonly RectTransform Rt;

        readonly Button button;
        readonly Image glow;
        bool selectable;

        public TokenView(Transform parent, PlayerColor color, int index, float size, Action<TokenView> onTap)
        {
            Color = color;
            Index = index;
            Rt = Ui.Rect(color + "Token" + index, parent, new Vector2(size, size), Vector2.zero);

            glow = Ui.Image("Glow", Rt, SpriteFactory.Ring, Palette.Glow, new Vector2(size * 1.45f, size * 1.45f), Vector2.zero);
            glow.enabled = false;

            var shadow = Ui.Image("Shadow", Rt, SpriteFactory.Circle, new UnityEngine.Color(0f, 0f, 0f, 0.3f), new Vector2(size, size), new Vector2(0f, -size * 0.06f));
            var body = Ui.Image("Body", Rt, SpriteFactory.Circle, Palette.DarkOf(color), new Vector2(size * 0.94f, size * 0.94f), Vector2.zero);
            Ui.Image("Fill", body.transform, SpriteFactory.Circle, Palette.Of(color), new Vector2(size * 0.74f, size * 0.74f), Vector2.zero);
            Ui.Image("Top", body.transform, SpriteFactory.Circle, new UnityEngine.Color(1f, 1f, 1f, 0.9f), new Vector2(size * 0.3f, size * 0.3f), Vector2.zero);

            // An invisible hit area slightly larger than the token makes tapping easier on phones.
            var hit = Ui.Image("Hit", Rt, SpriteFactory.Circle, new UnityEngine.Color(1f, 1f, 1f, 0f), new Vector2(size * 1.3f, size * 1.3f), Vector2.zero);
            hit.raycastTarget = true;
            button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onTap(this));
            SetSelectable(false);
            shadow.transform.SetAsFirstSibling();
            glow.transform.SetAsFirstSibling();
        }

        public bool IsSelectable => selectable;

        public void SetSelectable(bool on)
        {
            selectable = on;
            button.interactable = on;
            glow.enabled = on;
            if (on) Rt.SetAsLastSibling();
        }

        /// <summary>Called every frame by the match so selectable tokens pulse.</summary>
        public void Tick(float time)
        {
            if (!selectable) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 8f);
            glow.color = new UnityEngine.Color(1f, 1f, 1f, 0.45f + 0.55f * pulse);
            glow.rectTransform.localScale = Vector3.one * (0.92f + 0.12f * pulse);
        }
    }
}
