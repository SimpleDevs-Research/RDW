Shader "Custom/ProximityShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0,1,1,1)

        _PlayerPosition ("Player Position", Vector) = (0,0,0)
        _AxisMask ("Axis Mask", Vector) = (1,1,1)

        _WarningDistance ("Warning Distance", Float) = 2.0
        _MaxOpacity ("Max Opacity", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _TintColor;

            float3 _PlayerPosition;
            float3 _AxisMask;
            float _WarningDistance;
            float _MaxOpacity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 delta =
                    (i.worldPos - _PlayerPosition) *
                    _AxisMask.xyz;

                float distanceToPlayer = length(delta);
                
                float proximity =
                    1.0 - saturate(distanceToPlayer / _WarningDistance);

                proximity = smoothstep(0.0, 1.0, proximity);

                fixed4 tex = tex2D(_MainTex, i.uv);

                fixed4 col = tex * _TintColor;

                col.a *= proximity * _MaxOpacity;

                return col;
            }
            ENDCG
        }
    }
}
