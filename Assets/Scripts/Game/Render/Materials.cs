using UnityEngine;
using UnityEngine.Rendering;

namespace LudoGame
{
    /// <summary>Creates materials from the game's two shaders (kept in Resources so builds include them).</summary>
    public static class Materials
    {
        /// <summary>Direction towards the single fixed light: above, slightly left and towards the viewer.</summary>
        public static readonly Vector3 LightDir = new Vector3(-0.35f, 0.85f, -0.45f).normalized;

        static Shader unlit;
        static Shader glossy;

        static Shader UnlitShader => unlit != null ? unlit : unlit = Shader.Find("Ludo/Unlit");

        static Shader GlossyShader => glossy != null ? glossy : glossy = Shader.Find("Ludo/Glossy");

        public static void SetupLighting()
        {
            Shader.SetGlobalVector("_LudoLightDir", LightDir);
            Shader.SetGlobalColor("_LudoAmbient", new Color(0.52f, 0.52f, 0.58f));
        }

        public static Material Unlit(Texture texture, Color color, bool transparent, int queueOffset = 0)
        {
            var m = new Material(UnlitShader) { mainTexture = texture, color = color };
            if (transparent)
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)RenderQueue.Transparent + queueOffset;
            }
            else
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.renderQueue = (int)RenderQueue.Geometry + queueOffset;
            }
            return m;
        }

        public static Material Glossy(Color color, Texture texture = null, float gloss = 48f, float specular = 0.5f, float rim = 0.25f)
        {
            var m = new Material(GlossyShader) { color = color };
            if (texture != null) m.mainTexture = texture;
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_Spec", specular);
            m.SetFloat("_Rim", rim);
            return m;
        }
    }
}
