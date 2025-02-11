Shader "Custom/LightingSmooth"
{
     Properties
    {
        _MainTex ("Light Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            // Enable standard alpha blending.
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            Lighting Off
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float2 resolution = float2(_MainTex_TexelSize.z, _MainTex_TexelSize.w);
    
                // Adjust by 0.5 to sample at texel centers.
                float2 gridCoord = i.uv * resolution - 0.5;
    
                float2 tileIndex = floor(gridCoord);
                float2 frac = gridCoord - tileIndex;
    
                // When computing the UVs for texel lookup, add 0.5 back to get to the center.
                float2 uv00 = (tileIndex + 0.5) / resolution;
                float2 uv10 = (tileIndex + float2(1, 0) + 0.5) / resolution;
                float2 uv01 = (tileIndex + float2(0, 1) + 0.5) / resolution;
                float2 uv11 = (tileIndex + float2(1, 1) + 0.5) / resolution;
    
                fixed4 c00 = tex2D(_MainTex, uv00);
                fixed4 c10 = tex2D(_MainTex, uv10);
                fixed4 c01 = tex2D(_MainTex, uv01);
                fixed4 c11 = tex2D(_MainTex, uv11);
    
                fixed4 interpX0 = lerp(c00, c10, frac.x);
                fixed4 interpX1 = lerp(c01, c11, frac.x);
                fixed4 result = lerp(interpX0, interpX1, frac.y);
    
                return result;
            }
            ENDCG
        }
    }
}