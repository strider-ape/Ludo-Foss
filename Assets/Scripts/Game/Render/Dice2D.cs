using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LudoGame
{
    /// <summary>
    /// A flat die drawn on screen: always face-on and large, so the number is easy to read. It hops, wobbles and
    /// flips through faces (slowing down), then lands on the result with a little pop.
    /// </summary>
    public sealed class Dice2D : MonoBehaviour, IDie
    {
        const float Size = 150f;

        RectTransform root;
        RectTransform faceRt;
        Image face;
        Image glow;
        Image shadow;
        Button button;
        Coroutine moving;
        bool rollable;

        public bool IsRollable => rollable;

        public static Dice2D Create(Transform parent, Action onTap)
        {
            var rt = Ui.Rect("Dice2D", parent, new Vector2(Size * 1.5f, Size * 1.5f), Vector2.zero);
            var die = rt.gameObject.AddComponent<Dice2D>();
            die.Init(onTap);
            return die;
        }

        void Init(Action onTap)
        {
            root = (RectTransform)transform;
            glow = Ui.Image("Glow", root, SpriteFactory.Rounded, Color.white, new Vector2(Size * 1.3f, Size * 1.3f), Vector2.zero, sliced: true);
            shadow = Ui.Image("Shadow", root, SpriteFactory.Rounded, new Color(0f, 0f, 0f, 0.35f), new Vector2(Size, Size), new Vector2(0f, -12f), sliced: true);
            face = Ui.Image("Face", root, SpriteFactory.DiceFace(6), Color.white, new Vector2(Size, Size), Vector2.zero);
            faceRt = face.rectTransform;
            // An invisible hit area a bit larger than the die makes it easy to tap.
            var hit = Ui.Image("Hit", root, SpriteFactory.Square, new Color(1f, 1f, 1f, 0f), new Vector2(Size * 1.5f, Size * 1.5f), Vector2.zero);
            hit.raycastTarget = true;
            button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (rollable) onTap();
            });
            SetRollable(false);
        }

        public void SetRollable(bool on)
        {
            rollable = on;
            glow.enabled = on;
            if (!on) faceRt.localRotation = Quaternion.identity;
        }

        public void MoveTo(Vector3 world, Vector2 canvas, float duration)
        {
            if (moving != null) StopCoroutine(moving);
            moving = StartCoroutine(Slide(canvas, duration));
        }

        IEnumerator Slide(Vector2 to, float duration)
        {
            if (duration <= 0f || (root.anchoredPosition - to).sqrMagnitude < 1f)
            {
                root.anchoredPosition = to;
                yield break;
            }
            FrameGovernor.Keep(duration + 0.1f);
            var from = root.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                root.anchoredPosition = Vector2.Lerp(from, to, Ease.OutCubic(k)) + Vector2.up * Ease.Arc(k) * 60f;
                yield return null;
            }
            root.anchoredPosition = to;
        }

        void Update()
        {
            if (!rollable) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            glow.color = new Color(1f, 1f, 1f, 0.35f + 0.55f * pulse);
            faceRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 3f) * 4f);
        }

        public IEnumerator Roll(int result, float duration)
        {
            if (duration <= 0f)
            {
                face.sprite = SpriteFactory.DiceFace(result);
                yield break;
            }
            FrameGovernor.Keep(duration + 0.4f);
            Sfx.Play("dice_shake", 0.7f, 0.05f);
            const float tossShare = 0.2f;
            float hopHeight = Size * 0.45f;
            int shown = 0;
            float nextFace = 0f;
            bool landed = false;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (t >= nextFace)
                {
                    // Flip through random faces, slowing down as the die settles.
                    int value;
                    do value = UnityEngine.Random.Range(1, 7); while (value == shown);
                    shown = value;
                    face.sprite = SpriteFactory.DiceFace(value);
                    nextFace = t + Mathf.Lerp(0.045f, 0.13f, k);
                }
                float hop = k < tossShare
                    ? Ease.OutQuad(k / tossShare) * hopHeight
                    : (1f - Ease.OutBounce((k - tossShare) / (1f - tossShare))) * hopHeight;
                faceRt.anchoredPosition = new Vector2(0f, hop);
                faceRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 7f) * 28f * (1f - k));
                float shadowScale = 1f - 0.3f * hop / hopHeight;
                shadow.rectTransform.localScale = new Vector3(shadowScale, shadowScale, 1f);
                if (!landed && k > tossShare + (1f - tossShare) * 0.36f)
                {
                    landed = true;
                    Sfx.Play("dice_land_" + UnityEngine.Random.Range(1, 4), 0.8f, 0.05f);
                }
                yield return null;
            }
            face.sprite = SpriteFactory.DiceFace(result);
            faceRt.anchoredPosition = Vector2.zero;
            faceRt.localRotation = Quaternion.identity;
            shadow.rectTransform.localScale = Vector3.one;
            StartCoroutine(Pop(0.12f, 0.2f));
        }

        IEnumerator Pop(float amount, float duration)
        {
            FrameGovernor.Keep(duration + 0.05f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                faceRt.localScale = Vector3.one * (1f + amount * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            faceRt.localScale = Vector3.one;
        }

        /// <summary>A gold flash and a burst of little stars for a six.</summary>
        public IEnumerator Celebrate()
        {
            const float duration = 0.6f;
            FrameGovernor.Keep(duration + 0.1f);
            StartCoroutine(Pop(0.25f, 0.35f));
            const int count = 10;
            var stars = new RectTransform[count];
            var dirs = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                var star = Ui.Image("Sparkle", root, SpriteFactory.Star, Palette.Gold, new Vector2(34f, 34f), Vector2.zero);
                stars[i] = star.rectTransform;
                float a = i * Mathf.PI * 2f / count + UnityEngine.Random.Range(-0.2f, 0.2f);
                dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(110f, 160f);
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                glow.enabled = true;
                glow.color = new Color(1f, 0.84f, 0.3f, 0.9f * (1f - k));
                for (int i = 0; i < count; i++)
                {
                    stars[i].anchoredPosition = dirs[i] * Ease.OutCubic(k);
                    stars[i].localRotation = Quaternion.Euler(0f, 0f, k * 220f);
                    stars[i].GetComponent<Image>().color = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 1f - k);
                }
                yield return null;
            }
            foreach (var star in stars) Destroy(star.gameObject);
            glow.enabled = rollable;
            glow.color = Color.white;
        }

        /// <summary>Taps on the flat die go through its button, so board taps never hit it.</summary>
        public bool HitTest(Vector2 screen, CameraRig rig) => false;

        public void Remove() => Destroy(gameObject);
    }
}
