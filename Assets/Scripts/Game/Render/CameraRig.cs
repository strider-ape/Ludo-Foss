using System;
using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// Looks down at the board at a slight tilt, fits it to any screen shape (leaving room above and below for
    /// the player cards) and adds a short shake for captures.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        const float Pitch = 66f;
        const float FieldOfView = 30f;

        public Camera Cam { get; private set; }

        /// <summary>Raised after the camera is refitted (start-up or screen size change).</summary>
        public event Action Reframed;

        Vector3 restPosition;
        float shakeUntil;
        float shakeDuration;
        float shakeAmplitude;
        int width, height;

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

        /// <summary>Moves the camera back until the whole board fits with room for the cards above and below it.</summary>
        public void Frame()
        {
            width = Screen.width;
            height = Screen.height;
            float near = 5f, far = 400f;
            for (int i = 0; i < 32; i++)
            {
                float mid = (near + far) * 0.5f;
                Place(mid);
                if (Fits()) far = mid;
                else near = mid;
            }
            Place(far);
            Reframed?.Invoke();
        }

        void Place(float distance)
        {
            restPosition = -transform.forward * distance;
            transform.position = restPosition;
            Cam.nearClipPlane = Mathf.Max(0.3f, distance - 30f);
            Cam.farClipPlane = distance + 60f;
        }

        bool Fits()
        {
            float h = Board3D.HalfExtent + 0.1f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var v = Cam.WorldToViewportPoint(new Vector3(sx * h, 0f, sz * h));
                    if (v.x < 0.03f || v.x > 0.97f || v.y < 0.21f || v.y > 0.77f) return false;
                }
            }
            return true;
        }

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
