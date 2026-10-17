using System.Collections;
using UnityEngine;

namespace LudoGame
{
    /// <summary>A rounded 3D die that tumbles, bounces and settles on the rolled value.</summary>
    public sealed class Dice3D : MonoBehaviour, IDie
    {
        public const float Size = 1.5f;

        /// <summary>Outward normal of the face showing each value (index = value - 1), matching MeshBuilder.RoundedBox.</summary>
        static readonly Vector3[] FaceNormals = { Vector3.up, Vector3.forward, Vector3.right, Vector3.left, Vector3.back, Vector3.down };

        Transform body;
        Transform shadow;
        Transform glow;
        MeshRenderer glowRenderer;
        MaterialPropertyBlock block;
        bool rollable;
        Coroutine moving;

        public int Value { get; private set; } = 6;

        public static Dice3D Create(Transform parent)
        {
            var go = new GameObject("Dice");
            go.transform.SetParent(parent, false);
            var dice = go.AddComponent<Dice3D>();
            dice.Init();
            return dice;
        }

        void Init()
        {
            block = new MaterialPropertyBlock();
            var uv = new Rect[6];
            for (int v = 1; v <= 6; v++) uv[v - 1] = new Rect((v - 1) % 3 / 3f, (v - 1) / 3 / 2f, 1f / 3f, 0.5f);
            var mesh = MeshBuilder.RoundedBox(Vector3.one * Size, Size * 0.16f, 4, uv);

            var b = new GameObject("Body");
            b.transform.SetParent(transform, false);
            b.AddComponent<MeshFilter>().sharedMesh = mesh;
            b.AddComponent<MeshRenderer>().sharedMaterial = Materials.Glossy(Color.white, SpriteFactory.DiceAtlas, 40f, 0.45f, 0.18f);
            body = b.transform;
            body.localPosition = new Vector3(0f, Size * 0.5f, 0f);
            body.localRotation = Facing(Value, 0f);

            var quad = MeshBuilder.FlatQuad();
            shadow = MakeQuad("Shadow", quad, Materials.Unlit(SpriteFactory.RadialShadow, new Color(0f, 0f, 0f, 0.5f), true, 5), Size * 1.5f);
            glow = MakeQuad("Glow", quad, Materials.Unlit(SpriteFactory.Ring.texture, Color.white, true, 6), Size * 1.9f);
            glowRenderer = glow.GetComponent<MeshRenderer>();
            glowRenderer.enabled = false;
        }

        Transform MakeQuad(string name, Mesh mesh, Material material, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            go.transform.localScale = new Vector3(size, 1f, size);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        static Quaternion Facing(int value, float yaw) =>
            Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.FromToRotation(FaceNormals[value - 1], Vector3.up);

        public void SetRollable(bool on)
        {
            rollable = on;
            glowRenderer.enabled = on;
        }

        public bool IsRollable => rollable;

        /// <summary>Slides the die to another player's spot on the table.</summary>
        public void MoveTo(Vector3 world, float duration)
        {
            if (moving != null) StopCoroutine(moving);
            moving = StartCoroutine(Slide(world, duration));
        }

        IEnumerator Slide(Vector3 to, float duration)
        {
            if (duration <= 0f || (transform.position - to).sqrMagnitude < 1e-4f)
            {
                transform.position = to;
                yield break;
            }
            FrameGovernor.Keep(duration + 0.1f);
            var from = transform.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.position = Vector3.Lerp(from, to, Ease.OutCubic(k)) + Vector3.up * Ease.Arc(k) * 0.8f;
                yield return null;
            }
            transform.position = to;
        }

        void Update()
        {
            if (!rollable) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            float s = Size * (1.75f + 0.25f * pulse);
            glow.localScale = new Vector3(s, 1f, s);
            block.SetColor("_Color", new Color(1f, 1f, 1f, 0.45f + 0.55f * pulse));
            glowRenderer.SetPropertyBlock(block);
        }

        /// <summary>Toss, tumble and bounce, landing on <paramref name="result"/>.</summary>
        public IEnumerator Roll(int result, float duration)
        {
            Value = result;
            var target = Facing(result, Random.Range(-20f, 20f));
            if (duration <= 0f)
            {
                body.localRotation = target;
                yield break;
            }
            FrameGovernor.Keep(duration + 0.3f);
            Sfx.Play("dice_shake", 0.7f, 0.05f);
            var axis = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(-1f, 1f)).normalized;
            float spin = 630f + Random.Range(0f, 360f);
            const float tossShare = 0.18f;
            float height = Size * 1.4f;
            bool landed = false;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                body.localRotation = Quaternion.AngleAxis(spin * (1f - Ease.OutCubic(k)), axis) * target;
                float h = k < tossShare
                    ? Ease.OutQuad(k / tossShare) * height
                    : (1f - Ease.OutBounce((k - tossShare) / (1f - tossShare))) * height;
                body.localPosition = new Vector3(0f, Size * 0.5f + h, 0f);
                float shadowSize = Size * 1.5f * (1f - 0.4f * h / height);
                shadow.localScale = new Vector3(shadowSize, 1f, shadowSize);
                if (!landed && k > tossShare + (1f - tossShare) * 0.36f)
                {
                    landed = true;
                    Sfx.Play("dice_land_" + Random.Range(1, 4), 0.8f, 0.05f);
                }
                yield return null;
            }
            body.localRotation = target;
            body.localPosition = new Vector3(0f, Size * 0.5f, 0f);
            shadow.localScale = new Vector3(Size * 1.5f, 1f, Size * 1.5f);
        }

        /// <summary>A happy pop for rolling a six.</summary>
        public IEnumerator Celebrate()
        {
            FrameGovernor.Keep(0.5f);
            Fx.Sparkle(body.position + Vector3.up * Size * 0.4f, Palette.Gold);
            float t = 0f;
            const float duration = 0.35f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                body.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(k * Mathf.PI) * (1f - k));
                yield return null;
            }
            body.localScale = Vector3.one;
        }

        public Vector3 TopWorld => body.position;

        public void MoveTo(Vector3 world, Vector2 canvas, float duration) => MoveTo(world, duration);

        /// <summary>A tap counts if it lands within about a die's width of the die on screen.</summary>
        public bool HitTest(Vector2 screen, CameraRig rig)
        {
            var top = rig.WorldToScreen(TopWorld);
            float radius = (top - rig.WorldToScreen(TopWorld + Vector3.right * Size * 0.5f)).magnitude;
            return (screen - top).magnitude < radius * 2f;
        }

        public void Remove() => Destroy(gameObject);
    }
}
