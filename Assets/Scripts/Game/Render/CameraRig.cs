using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// Looks down at the board at a slight tilt, fits it to any screen shape (leaving room above and below for
    /// the player cards), adds a short shake for captures, and can glide in to frame a few points ("zoom to choices").
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>Downward camera angle in degrees (90 = straight down). Lower shows more of the pawns' sides.</summary>
        public static float Pitch = 58f;

        /// <summary>The 2D board is seen straight from above.</summary>
        const float FlatPitch = 90f;
        const float FieldOfView = 30f;

        /// <summary>The furthest "zoom to choices" will move in: 1.7 times the normal size.</summary>
        const float MaxZoom = 1.7f;

        public Camera Cam { get; private set; }

        /// <summary>Raised after the camera is refitted (start-up, screen size or board size change).</summary>
        public event Action Reframed;

        /// <summary>True while the camera is gliding in or out.</summary>
        public bool IsAnimating => zooming != null;

        public bool IsZoomed { get; private set; }

        Vector3 restPosition;
        Vector3 homePosition;
        float homeDistance;
        float shakeUntil;
        float shakeDuration;
        float shakeAmplitude;
        int width, height;
        Coroutine zooming;

        public static CameraRig Create()
        {
            var go = new GameObject("Camera");
            var rig = go.AddComponent<CameraRig>();
            rig.Init();
            return rig;
        }

        void Init()
        {
            Cam = gameObject.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Palette.Background;
            Cam.fieldOfView = FieldOfView;
            Cam.allowHDR = false;
            Cam.allowMSAA = true;
            gameObject.AddComponent<AudioListener>();
            transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            Frame();

            var table = new GameObject("Table");
            table.transform.position = new Vector3(0f, Board3D.TableY, 0f);
            table.transform.localScale = new Vector3(140f, 1f, 140f);
            table.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.FlatQuad();
            table.AddComponent<MeshRenderer>().sharedMaterial = Materials.Unlit(SpriteFactory.Table, Color.white, false, -100);
        }

        /// <summary>Re-aims the camera after <see cref="Pitch"/> changes and refits the board.</summary>
        public void ApplyPitch() => Frame();

        /// <summary>
        /// Aims the camera (tilted at the 3D board, straight down at the 2D one), then moves it back until the whole
        /// board fits with room for the cards above and below it.
        /// </summary>
        public void Frame()
        {
            StopZoom();
            transform.rotation = Quaternion.Euler(Board3D.Flat ? FlatPitch : Pitch, 0f, 0f);
            width = Screen.width;
            height = Screen.height;
            float near = 5f, far = 400f;
            for (int i = 0; i < 32; i++)
            {
                float mid = (near + far) * 0.5f;
                Place(Vector3.zero, mid);
                if (BoardFits()) far = mid;
                else near = mid;
            }
            Place(Vector3.zero, far);
            homePosition = restPosition;
            homeDistance = far;
            // Leave room to zoom in without the near clip plane cutting into the board.
            Cam.nearClipPlane = Mathf.Max(0.3f, far / MaxZoom - 25f);
            Cam.farClipPlane = far + 60f;
            Reframed?.Invoke();
        }

        void Place(Vector3 focus, float distance)
        {
            restPosition = focus - transform.forward * distance;
            transform.position = restPosition;
        }

        bool BoardFits()
        {
            // Edge-to-edge leaves only a hairline at the sides; otherwise a small margin around the frame.
            float side = Board3D.EdgeToEdge ? 0.006f : 0.03f;
            float h = Board3D.HalfExtent + 0.05f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var v = Cam.WorldToViewportPoint(new Vector3(sx * h, 0f, sz * h));
                    if (v.x < side || v.x > 1f - side || v.y < 0.21f || v.y > 0.77f) return false;
                }
            }
            return true;
        }

        // ---------- Zoom to choices ----------

        /// <summary>
        /// Glides in so every point is on screen, clear of the player cards, at up to 1.7x. Does nothing when the
        /// points are already spread over most of the board (there would be nothing to gain).
        /// </summary>
        public void ZoomToFit(IList<Vector3> points, float duration)
        {
            if (points == null || points.Count == 0) return;
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            var focus = new Vector3(bounds.center.x, 0f, bounds.center.z);

            var saved = transform.position;
            float near = homeDistance / MaxZoom, far = homeDistance;
            for (int i = 0; i < 24; i++)
            {
                float mid = (near + far) * 0.5f;
                transform.position = focus - transform.forward * mid;
                if (PointsFit(points)) far = mid;
                else near = mid;
            }
            transform.position = saved;
            if (homeDistance / far < 1.08f) return;
            MoveTo(focus - transform.forward * far, duration, true);
        }

        /// <summary>Glides back out to the whole board.</summary>
        public void ResetZoom(float duration)
        {
            if (!IsZoomed && zooming == null) return;
            MoveTo(homePosition, duration, false);
        }

        bool PointsFit(IList<Vector3> points)
        {
            foreach (var p in points)
            {
                var v = Cam.WorldToViewportPoint(p);
                if (v.z <= 0f || v.x < 0.08f || v.x > 0.92f || v.y < 0.25f || v.y > 0.74f) return false;
            }
            return true;
        }

        void MoveTo(Vector3 target, float duration, bool zoomed)
        {
            StopZoom();
            IsZoomed = zoomed;
            if (duration <= 0f || !Application.isPlaying)
            {
                restPosition = target;
                transform.position = target;
                return;
            }
            zooming = StartCoroutine(Glide(target, duration));
        }

        IEnumerator Glide(Vector3 target, float duration)
        {
            FrameGovernor.Keep(duration + 0.1f);
            var from = restPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                restPosition = Vector3.Lerp(from, target, Ease.OutCubic(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            restPosition = target;
            zooming = null;
        }

        void StopZoom()
        {
            if (zooming != null) StopCoroutine(zooming);
            zooming = null;
            IsZoomed = false;
        }

        // ---------- Shake ----------

        public void Shake(float amplitude, float duration)
        {
            if (duration <= 0f) return;
            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeUntil = Time.unscaledTime + duration;
            FrameGovernor.Keep(duration + 0.05f);
        }

        void LateUpdate()
        {
            if (Screen.width != width || Screen.height != height) Frame();
            float left = shakeUntil - Time.unscaledTime;
            transform.position = left > 0f
                ? restPosition + UnityEngine.Random.insideUnitSphere * shakeAmplitude * (left / shakeDuration)
                : restPosition;
        }

        public Vector2 WorldToScreen(Vector3 world) => Cam.WorldToScreenPoint(world);

        public bool ScreenToPlane(Vector2 screen, float planeY, out Vector3 world)
        {
            var ray = Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                world = ray.GetPoint(distance);
                return true;
            }
            world = Vector3.zero;
            return false;
        }
    }
}
