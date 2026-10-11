using System.Collections;
using LudoGame.Core;
using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// A glossy 3D pawn. The root slides over the board; the body hops, squashes and spins; a soft shadow and
    /// a pulsing ring stay flat on the board.
    /// </summary>
    public sealed class Pawn3D : MonoBehaviour
    {
        /// <summary>Side profile of the pawn (radius, height); also used to draw the app icon.</summary>
        public static readonly Vector2[] Profile = BuildProfile();
        static Mesh bodyMesh;
        static Mesh quad;
        static Material[] bodyMaterials;
        static Material shadowMaterial;
        static Material ringMaterial;

        public PlayerColor Color { get; private set; }
        public int Index { get; private set; }
        public Transform Body { get; private set; }

        /// <summary>Overall pawn size relative to the mesh (a board cell is 1 unit).</summary>
        const float Size = 1.25f;

        /// <summary>1 normally, smaller when sharing a square with other pawns.</summary>
        public float BaseScale { get; private set; } = 1f;

        Transform shadow;
        Transform ring;
        MeshRenderer ringRenderer;
        MaterialPropertyBlock block;
        bool selectable;

        public bool IsSelectable => selectable;

        public static Pawn3D Create(Transform parent, PlayerColor color, int index)
        {
            EnsureShared();
            var go = new GameObject(color + "Pawn" + index);
            go.transform.SetParent(parent, false);
            var pawn = go.AddComponent<Pawn3D>();
            pawn.Init(color, index);
            return pawn;
        }

        static void EnsureShared()
        {
            if (bodyMesh != null) return;
            bodyMesh = MeshBuilder.Lathe(Profile, 28);
            quad = MeshBuilder.FlatQuad();
            bodyMaterials = new Material[GameState.Players];
            for (int p = 0; p < GameState.Players; p++)
            {
                bodyMaterials[p] = Materials.Glossy(Palette.Of((PlayerColor)p), null, 56f, 0.55f, 0.22f);
            }
            shadowMaterial = Materials.Unlit(SpriteFactory.RadialShadow, new UnityEngine.Color(0f, 0f, 0f, 0.42f), true, 5);
            ringMaterial = Materials.Unlit(SpriteFactory.Ring.texture, UnityEngine.Color.white, true, 6);
        }

        static Vector2[] BuildProfile()
        {
            // Classic Ludo pawn side profile: (radius, height), bottom to top, then a round head.
            var points = new System.Collections.Generic.List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.37f, 0f), new Vector2(0.39f, 0.03f), new Vector2(0.39f, 0.07f),
                new Vector2(0.36f, 0.1f), new Vector2(0.28f, 0.13f), new Vector2(0.2f, 0.18f), new Vector2(0.15f, 0.27f),
                new Vector2(0.13f, 0.38f), new Vector2(0.14f, 0.46f), new Vector2(0.21f, 0.5f), new Vector2(0.22f, 0.53f),
                new Vector2(0.19f, 0.56f), new Vector2(0.11f, 0.58f), new Vector2(0.1f, 0.62f),
            };
            const float headY = 0.8f, headR = 0.2f;
            for (int deg = -60; deg <= 90; deg += 15)
            {
                float a = deg * Mathf.Deg2Rad;
                points.Add(new Vector2(Mathf.Max(0f, headR * Mathf.Cos(a)), headY + headR * Mathf.Sin(a)));
            }
            points.Add(new Vector2(0f, headY + headR));
            return points.ToArray();
        }

        void Init(PlayerColor color, int index)
        {
            Color = color;
            Index = index;
            block = new MaterialPropertyBlock();

            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = bodyMaterials[(int)color];
            Body = body.transform;

            // The shadow falls away from the light, in world space regardless of how the board is turned.
            var away = new Vector3(-Materials.LightDir.x, 0f, -Materials.LightDir.z) * 0.12f;
            shadow = MakeQuad("Shadow", shadowMaterial, 0.05f, ShadowSize);
            shadow.localPosition += transform.parent != null ? transform.parent.InverseTransformDirection(away) : away;
            ring = MakeQuad("Ring", ringMaterial, 0.06f, 1.3f * Size);
            ringRenderer = ring.GetComponent<MeshRenderer>();
            ringRenderer.enabled = false;
            ApplyBaseScale(1f);
        }

        float ShadowSize => 1.05f * Size * BaseScale;

        Transform MakeQuad(string name, Material material, float y, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(size, 1f, size);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        public void SetSelectable(bool on)
        {
            selectable = on;
            ringRenderer.enabled = on;
            if (!on) Body.localPosition = Vector3.zero;
        }

        void Update()
        {
            if (!selectable) return;
            float t = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f);
            float s = (1.2f + 0.18f * pulse) * BaseScale * Size;
            ring.localScale = new Vector3(s, 1f, s);
            block.SetColor("_Color", new UnityEngine.Color(1f, 1f, 1f, 0.5f + 0.5f * pulse));
            ringRenderer.SetPropertyBlock(block);
            Body.localPosition = new Vector3(0f, 0.14f * Mathf.Abs(Mathf.Sin(t * 4f)), 0f);
        }

        void SetShape(float stretch)
        {
            float scale = BaseScale * Size;
            float xz = scale / Mathf.Sqrt(stretch);
            Body.localScale = new Vector3(xz, scale * stretch, xz);
        }

        /// <summary>One hop to the next square: an arc with a stretch in the air and a squash on landing.</summary>
        public IEnumerator Hop(Vector3 to, float duration, float height)
        {
            if (duration <= 0f)
            {
                transform.localPosition = to;
                yield break;
            }
            FrameGovernor.Keep(duration + 0.25f);
            var from = transform.localPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float arc = Ease.Arc(k);
                transform.localPosition = Vector3.Lerp(from, to, Ease.Smooth(k));
                Body.localPosition = new Vector3(0f, arc * height, 0f);
                SetShape(1f + 0.14f * arc);
                float shadowSize = ShadowSize * (1f - 0.35f * arc);
                shadow.localScale = new Vector3(shadowSize, 1f, shadowSize);
                yield return null;
            }
            transform.localPosition = to;
            Body.localPosition = Vector3.zero;
            shadow.localScale = new Vector3(ShadowSize, 1f, ShadowSize);
            StartCoroutine(Squash(0.22f, Mathf.Clamp(duration * 0.8f, 0.06f, 0.14f)));
        }

        IEnumerator Squash(float amount, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                SetShape(1f - amount * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
                yield return null;
            }
            SetShape(1f);
        }

        /// <summary>Knocked out: tumbles through the air back to its base slot.</summary>
        public IEnumerator FlyTo(Vector3 to, float duration, float height)
        {
            if (duration <= 0f)
            {
                transform.localPosition = to;
                yield break;
            }
            FrameGovernor.Keep(duration + 0.25f);
            var from = transform.localPosition;
            var axis = new Vector3(Random.Range(-1f, 1f), 0.2f, Random.Range(-1f, 1f)).normalized;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.localPosition = Vector3.Lerp(from, to, Ease.Smooth(k));
                Body.localPosition = new Vector3(0f, Ease.Arc(k) * height, 0f);
                Body.localRotation = Quaternion.AngleAxis(720f * Ease.OutQuad(k), axis);
                yield return null;
            }
            transform.localPosition = to;
            Body.localPosition = Vector3.zero;
            Body.localRotation = Quaternion.identity;
            StartCoroutine(Squash(0.3f, 0.16f));
        }

        /// <summary>Glides to a new spot and size, used when pawns share a square.</summary>
        public IEnumerator SlideTo(Vector3 to, float scale, float duration)
        {
            var from = transform.localPosition;
            float fromScale = BaseScale;
            if (duration <= 0f || (from - to).sqrMagnitude < 1e-6f && Mathf.Approximately(fromScale, scale))
            {
                transform.localPosition = to;
                ApplyBaseScale(scale);
                yield break;
            }
            FrameGovernor.Keep(duration + 0.1f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Ease.OutBack(Mathf.Clamp01(t / duration));
                transform.localPosition = Vector3.LerpUnclamped(from, to, k);
                ApplyBaseScale(Mathf.LerpUnclamped(fromScale, scale, k));
                yield return null;
            }
            transform.localPosition = to;
            ApplyBaseScale(scale);
        }

        /// <summary>Stops any animation and stands the pawn upright on the board.</summary>
        public void ResetPose()
        {
            StopAllCoroutines();
            Body.localPosition = Vector3.zero;
            Body.localRotation = Quaternion.identity;
            ApplyBaseScale(BaseScale);
        }

        public void ApplyBaseScale(float scale)
        {
            BaseScale = scale;
            SetShape(1f);
            shadow.localScale = new Vector3(ShadowSize, 1f, ShadowSize);
        }
    }
}
