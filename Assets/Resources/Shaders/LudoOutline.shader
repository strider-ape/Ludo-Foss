// Cartoon outline: the mesh drawn again slightly inflated along its normals, back faces only, in one flat colour.
// One extra cheap draw per pawn; no post-processing.
Shader "Ludo/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (0.08, 0.1, 0.2, 1)
        _Width ("Width", Float) = 0.03
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        Cull Front
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex + float4(normalize(v.normal) * _Width, 0));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
