Shader "Custom/ProximityShader"
{
    Properties
    {
        _MainTex ("Pattern / Mask", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0,1,1,1)

        _PlayerPosition ("Player Position", Vector) = (0,0,0)
        _AxisMask ("Axis Mask", Vector) = (1,1,1)

        _WarningDistance ("Warning Distance", Float) = 2.0
        _MaxOpacity ("Max Opacity", Range(0,1)) = 1.0
        _GridScale ("Grid Scale (tiles/meter)", Float) = 2.0
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
            float _GridScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);

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

                // Sample texture as an opacity mask
                float3 p = i.worldPos * _GridScale;
                
                fixed4 texX = tex2D(_MainTex, p.zy);
                fixed4 texY = tex2D(_MainTex, p.xz);
                fixed4 texZ = tex2D(_MainTex, p.xy);

                float3 weights = abs(normalize(i.worldNormal));
                weights /= weights.x + weights.y + weights.z;

                fixed4 tex =
                    texX * weights.x +
                    texY * weights.y +
                    texZ * weights.z;

                // Convert RGB to grayscale:
                // black = 0
                // white = 1
                // gray = intermediate
                float mask = dot(tex.rgb, float3(0.299, 0.587, 0.114));

                // Color always comes from the tint
                fixed4 col = _TintColor;

                // Texture controls WHERE it is visible.
                // Proximity controls HOW visible it is.
                col.a *= mask * proximity * _MaxOpacity;

                return col;
            }
            ENDCG
        }
    }
}