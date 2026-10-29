using System.Collections;
using LudoGame.Core;
using UnityEngine;

namespace LudoGame
{
    /// <summary>How pawns are drawn; tunable so the board stays readable on small phone screens.</summary>
    public static class PawnStyle
    {
        /// <summary>Pawn size relative to the mesh (a board cell is 1 unit).</summary>
        public static float Size = 1.25f;

        /// <summary>A dark cartoon outline around each pawn.</summary>
        public static bool Outline;

        /// <summary>A white coin with a dark rim under each pawn.</summary>
        public static bool Coin;

        /// <summary>Scale of pawns sharing a square.</summary>
        public static float StackScale = 0.75f;

        /// <summary>The colour-blind shape (circle, triangle, square, star) on each pawn's head.</summary>
        public static bool Shapes;

        /// <summary>Flat map pins for the 2D board instead of 3D pawns (the pawn look doesn't apply).</summary>
        public static bool Flat;

        /// <summary>Sets the style from the player's settings.</summary>
        public static void Apply(PawnLook look, bool shapes, bool flat = false)
        {
            bool classic = look == PawnLook.Classic;
            Flat = flat;
            Size = flat ? 1f : classic ? 1.25f : 1.45f;
            Outline = !flat && !classic;
            Coin = !flat && look == PawnLook.OutlineCoin;
            StackScale = flat ? 0.78f : classic ? 0.75f : 0.85f;
            Shapes = shapes;
        }
    }

    /// <summary>
    /// A glossy 3D pawn, or a flat map pin on the 2D board. The root slides over the board; the body hops, squashes
    /// and spins; a soft shadow (a dark ring on the 2D board) and a pulsing ring stay flat on the board.
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
        static Material outlineMaterial;
        static Material coinMaterial;
        static Material footMaterial;
        static readonly Material[] shapeMaterials = new Material[GameState.Players];
        static readonly Material[] pinMaterials = new Material[GameState.Players];
        static readonly Material[] pinShapeMaterials = new Material[GameState.Players];

        /// <summary>
        /// Draw order on the 2D board: pins above the board, the ring and the highlight; lower pins in front of higher
        /// ones (a pin leans up into the square above); pins in mid-hop in front of everything.
        /// </summary>
        const int FootOrder = 1, HighlightOrder = 2, PinOrder = 1000, AirborneOrder = 600;

        /// <summary>On the 2D board a hop lifts the pin up the screen by this much of its 3D height.</summary>
        const float FlatLift = 0.5f;

        public PlayerColor Color { get; private set; }
        public int Index { get; private set; }
        public Transform Body { get; private set; }

        static float Size => PawnStyle.Size;

        /// <summary>1 normally, smaller when sharing a square with other pawns.</summary>
        public float BaseScale { get; private set; } = 1f;

        Transform shadow;
        Transform ring;
        Transform decorations;
        Vector3 shadowOffset;
        MeshRenderer bodyRenderer;
        MeshRenderer shadowRenderer;
        MeshRenderer ringRenderer;
        MeshRenderer pinRenderer;
        MeshRenderer pinShapeRenderer;
        MaterialPropertyBlock block;
        bool selectable;

        public bool IsSelectable => selectable;

        /// <summary>The middle of the pawn's head (the pin's ball on the 2D board), in world space.</summary>
        public Vector3 HeadPosition => Body.position + (PawnStyle.Flat ? Vector3.forward * FlatBoard.PinHeadY : Vector3.up * 1.1f) * BaseScale;

        /// <summary>From a pawn's foot to the top of its head, at the given scale: up, or up the screen on the 2D board.</summary>
        public static Vector3 TopOffset(float scale) =>
            PawnStyle.Flat ? Vector3.forward * ((FlatBoard.PinHeadY + FlatBoard.PinRadius) * scale) : Vector3.up * (1.25f * PawnStyle.Size * scale);

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
            footMaterial = Materials.Unlit(SpriteFactory.Ring.texture, FlatBoard.FootRing, true, 5);
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
            bodyRenderer = body.AddComponent<MeshRenderer>();
            bodyRenderer.sharedMaterial = bodyMaterials[(int)color];
            Body = body.transform;

            // The shadow falls away from the light, in world space regardless of how the board is turned.
            var away = new Vector3(-Materials.LightDir.x, 0f, -Materials.LightDir.z) * 0.12f;
            shadowOffset = transform.parent != null ? transform.parent.InverseTransformDirection(away) : away;
            shadow = MakeQuad("Shadow", shadowMaterial, 0.05f, 1f);
            shadowRenderer = shadow.GetComponent<MeshRenderer>();
            ring = MakeQuad("Ring", ringMaterial, 0.06f, 1.3f * Size);
            ringRenderer = ring.GetComponent<MeshRenderer>();
            ringRenderer.enabled = false;
            BuildDecorations();
            ApplyBaseScale(1f);
        }

        /// <summary>
        /// Outline, coin and colour shape, all riding on the body so they hop and tumble with it. On the 2D board the
        /// body is a map pin instead, standing on a dark ring.
        /// </summary>
        void BuildDecorations()
        {
            decorations = new GameObject("Decorations").transform;
            decorations.SetParent(Body, false);
            bool flat = PawnStyle.Flat;
            bodyRenderer.enabled = !flat;
            shadowRenderer.sharedMaterial = flat ? footMaterial : shadowMaterial;
            shadow.localPosition = new Vector3(0f, 0.05f, 0f) + (flat ? Vector3.zero : shadowOffset);
            shadowRenderer.sortingOrder = flat ? FootOrder : 0;
            ringRenderer.sortingOrder = flat ? HighlightOrder : 0;
            pinRenderer = null;
            pinShapeRenderer = null;
            if (flat)
            {
                BuildPin();
                return;
            }
            if (PawnStyle.Outline)
            {
                if (outlineMaterial == null) outlineMaterial = Materials.Outline(new UnityEngine.Color(0.07f, 0.09f, 0.19f), 0.035f);
                var outline = new GameObject("Outline");
                outline.transform.SetParent(decorations, false);
                outline.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
                outline.AddComponent<MeshRenderer>().sharedMaterial = outlineMaterial;
            }
            if (PawnStyle.Coin)
            {
                if (coinMaterial == null) coinMaterial = Materials.Unlit(SpriteFactory.Puck, UnityEngine.Color.white, true, 4);
                // Just above the board's painted squares (which sit up to 0.04 high), a little wider than the base.
                AddDecal("Coin", coinMaterial, 0.035f, 0.95f);
            }
            if (PawnStyle.Shapes)
            {
                int c = (int)Color;
                if (shapeMaterials[c] == null) shapeMaterials[c] = Materials.Unlit(SpriteFactory.ShapeOf(Color).texture, UnityEngine.Color.white, true, 8);
                // A small white shape lying on top of the head.
                AddDecal("Shape", shapeMaterials[c], 1.01f, 0.32f);
            }
        }

        /// <summary>The map pin lying flat on the board, its tip on the pawn's spot and its head pointing up the screen.</summary>
        void BuildPin()
        {
            int c = (int)Color;
            if (pinMaterials[c] == null) pinMaterials[c] = Materials.Unlit(FlatBoard.Pin(Color), UnityEngine.Color.white, true, 10);
            var centre = new Vector3(FlatBoard.PinLeft + FlatBoard.PinWidth * 0.5f, 0.08f, FlatBoard.PinBottom + FlatBoard.PinHeight * 0.5f);
            pinRenderer = AddDecal("Pin", pinMaterials[c], centre, new Vector3(FlatBoard.PinWidth, 1f, FlatBoard.PinHeight));
            if (PawnStyle.Shapes)
            {
                // Same queue as the pin, so the draw order keeps the shape just above its own pin.
                if (pinShapeMaterials[c] == null) pinShapeMaterials[c] = Materials.Unlit(SpriteFactory.ShapeOf(Color).texture, UnityEngine.Color.white, true, 10);
                pinShapeRenderer = AddDecal("Shape", pinShapeMaterials[c], new Vector3(0f, 0.09f, FlatBoard.PinHeadY), new Vector3(0.34f, 1f, 0.34f));
            }
            SortPin();
        }

        void AddDecal(string name, Material material, float y, float size) =>
            AddDecal(name, material, new Vector3(0f, y, 0f), new Vector3(size, 1f, size));

        MeshRenderer AddDecal(string name, Material material, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(decorations, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>Rebuilds outline, coin, shape and size after the pawn style setting changes.</summary>
        public void RefreshStyle()
        {
            if (decorations != null) Destroy(decorations.gameObject);
            BuildDecorations();
            ApplyBaseScale(BaseScale);
        }

        /// <summary>Keeps lower pins in front of higher ones on the 2D board, and a pin in mid-hop in front of all.</summary>
        public void SortPin()
        {
            if (pinRenderer == null) return;
            int order = PinOrder + Mathf.RoundToInt(-transform.position.z * 20f) + (Body.localPosition.z > 0.1f ? AirborneOrder : 0);
            if (pinRenderer.sortingOrder == order) return;
            pinRenderer.sortingOrder = order;
            if (pinShapeRenderer != null) pinShapeRenderer.sortingOrder = order + 1;
        }

        void LateUpdate() => SortPin();

        /// <summary>Raises the body off the board: straight up, or up the screen on the 2D board.</summary>
        void Lift(float height) => Body.localPosition = PawnStyle.Flat ? new Vector3(0f, 0f, height * FlatLift) : new Vector3(0f, height, 0f);

        float ShadowSize => PawnStyle.Flat ? 0.85f * BaseScale : 1.05f * Size * BaseScale;

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
            // White glows on the 3D board's colours; the 2D board is mostly white, so the ring is dark there.
            var glow = PawnStyle.Flat ? new UnityEngine.Color(0.08f, 0.1f, 0.22f, 0.45f + 0.45f * pulse) : new UnityEngine.Color(1f, 1f, 1f, 0.5f + 0.5f * pulse);
            block.SetColor("_Color", glow);
            ringRenderer.SetPropertyBlock(block);
            Lift(0.14f * Mathf.Abs(Mathf.Sin(t * 4f)));
        }

        /// <summary>Stretches the body along its height: straight up, or up the screen for a pin on the 2D board.</summary>
        void SetShape(float stretch)
        {
            float scale = BaseScale * Size;
            float across = scale / Mathf.Sqrt(stretch);
            Body.localScale = PawnStyle.Flat ? new Vector3(across, scale, scale * stretch) : new Vector3(across, scale * stretch, across);
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
                Lift(arc * height);
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
            // A flat pin spins in the board's plane; a 3D pawn tumbles.
            var axis = PawnStyle.Flat ? Vector3.up : new Vector3(Random.Range(-1f, 1f), 0.2f, Random.Range(-1f, 1f)).normalized;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.localPosition = Vector3.Lerp(from, to, Ease.Smooth(k));
                Lift(Ease.Arc(k) * height);
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
