using UnityEngine;

namespace LudoGame
{
    /// <summary>Short particle bursts: confetti, sparkles and a falling celebration. Each one cleans itself up.</summary>
    public static class Fx
    {
        static Material confettiMaterial;
        static Material sparkleMaterial;

        static Material ConfettiMaterial => confettiMaterial != null ? confettiMaterial
            : confettiMaterial = Materials.Unlit(SpriteFactory.Square.texture, Color.white, true, 20);

        static Material SparkleMaterial => sparkleMaterial != null ? sparkleMaterial
            : sparkleMaterial = Materials.Unlit(SpriteFactory.Star.texture, Color.white, true, 21);

        /// <summary>A burst of tumbling paper squares shooting upwards.</summary>
        public static void Confetti(Vector3 position, Color[] colors, int count = 36, float speed = 7f)
        {
            var ps = NewSystem("Confetti", position, ConfettiMaterial, 2.2f, 1.6f);
            for (int i = 0; i < count; i++)
            {
                var dir = (Vector3.up * 1.6f + Random.insideUnitSphere).normalized;
                ps.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = dir * Random.Range(speed * 0.55f, speed),
                    startColor = colors[Random.Range(0, colors.Length)],
                    startSize = Random.Range(0.16f, 0.3f),
                    startLifetime = Random.Range(1.1f, 1.8f),
                    rotation = Random.Range(0f, 360f),
                    angularVelocity = Random.Range(-540f, 540f),
                }, 1);
            }
            ps.Play();
            FrameGovernor.Keep(1.9f);
        }

        /// <summary>A few twinkling stars, e.g. around the die after a six.</summary>
        public static void Sparkle(Vector3 position, Color color, int count = 12)
        {
            var ps = NewSystem("Sparkle", position, SparkleMaterial, 0f, 0.8f);
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y);
                ps.Emit(new ParticleSystem.EmitParams
                {
                    position = position + dir * 0.4f,
                    velocity = dir * Random.Range(2f, 4f),
                    startColor = color,
                    startSize = Random.Range(0.35f, 0.6f),
                    startLifetime = Random.Range(0.45f, 0.75f),
                    rotation = Random.Range(0f, 360f),
                    angularVelocity = Random.Range(-180f, 180f),
                }, 1);
            }
            ps.Play();
            FrameGovernor.Keep(0.8f);
        }

        /// <summary>Confetti falling over the whole board for a win.</summary>
        public static void Celebration(Color[] colors)
        {
            for (int i = 0; i < 5; i++)
            {
                var pos = new Vector3(Random.Range(-6f, 6f), 0.5f, Random.Range(-5f, 5f));
                Confetti(pos, colors, 40, 11f);
            }
        }

        static ParticleSystem NewSystem(string name, Vector3 position, Material material, float gravity, float fadeShare)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 2f;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f - fadeShare * 0.3f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }
    }
}
