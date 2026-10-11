// Cheap glossy-plastic look for pawns, dice and the board frame.
// One fixed light (_LudoLightDir, set from code) instead of Unity lights: wrapped diffuse, a specular highlight and a rim.
Shader "Ludo/Glossy"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Gloss ("Gloss", Range(4, 128)) = 48
        _Spec ("Specular", Range(0, 1)) = 0.5
        _Rim ("Rim", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Gloss;
            half _Spec;
            half _Rim;
            float4 _LudoLightDir; // world space, pointing towards the light
            fixed4 _LudoAmbient;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float3 normal : TEXCOORD1; float3 view : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half3 v = normalize(i.view);
                half3 l = normalize(_LudoLightDir.xyz);
                fixed4 albedo = tex2D(_MainTex, i.uv) * i.color;
                half diffuse = saturate(dot(n, l) * 0.5 + 0.5);
                half spec = pow(saturate(dot(n, normalize(l + v))), _Gloss) * _Spec;
                half rim = pow(1.0 - saturate(dot(n, v)), 3.0) * _Rim;
                half3 lit = albedo.rgb * (_LudoAmbient.rgb + diffuse * (1.0 - _LudoAmbient.rgb));
                return fixed4(lit + spec + rim * albedo.rgb, 1.0);
            }
            ENDCG
        }
    }
}
