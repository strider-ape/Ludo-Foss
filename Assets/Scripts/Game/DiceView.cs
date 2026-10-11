using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LudoTurbo
{
    public sealed class DiceView
    {
        public readonly RectTransform Rt;

        readonly Image face;
        readonly Image glow;
        readonly Button button;
        readonly System.Random rng = new System.Random();
        bool rollable;

        public DiceView(Transform parent, float size, Action onTap)
        {
            Rt = Ui.Rect("Dice", parent, new Vector2(size, size), Vector2.zero);
            glow = Ui.Image("Glow", Rt, SpriteFactory.Rounded, Palette.Glow, new Vector2(size * 1.25f, size * 1.25f), Vector2.zero, sliced: true);
            glow.enabled = false;
            Ui.Image("Shadow", Rt, SpriteFactory.Rounded, new Color(0f, 0f, 0f, 0.35f), new Vector2(size, size), new Vector2(0f, -size * 0.05f), sliced: true);
            face = Ui.Image("Face", Rt, SpriteFactory.DiceFace(6), Color.white, new Vector2(size, size), Vector2.zero);
            face.raycastTarget = true;
            button = face.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onTap());
            SetRollable(false);
        }

        public void SetRollable(bool on)
        {
            rollable = on;
            button.interactable = on;
            glow.enabled = on;
            face.rectTransform.localRotation = Quaternion.identity;
        }

        public void Show(int value) => face.sprite = SpriteFactory.DiceFace(value);

        public void Tick(float time)
        {
            if (!rollable) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 6f);
            glow.color = new Color(1f, 1f, 1f, 0.35f + 0.5f * pulse);
        }

        public IEnumerator Roll(int result, float duration)
        {
            float elapsed = 0f;
            float nextFace = 0f;
            while (elapsed < duration)
            {
                if (elapsed >= nextFace)
                {
                    Show(rng.Next(1, 7));
                    face.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rng.Next(-25, 26));
                    nextFace = elapsed + 0.06f;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            face.rectTransform.localRotation = Quaternion.identity;
            Show(result);
        }
    }
}
