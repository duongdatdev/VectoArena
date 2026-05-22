Shader "Custom/MinimapOverlayShader"
{
    Properties
    {
        _DangerColor ("Danger Color (Outside)", Color) = (1, 0.5, 0, 0.4)
        _NextZoneColor ("Next Zone Line Color", Color) = (1, 1, 1, 1)
        _CurrentZoneLineColor ("Current Zone Line Color", Color) = (1, 0.3, 0, 1)
        _LineThickness ("Line Thickness", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        // Enable transparency blending
        Blend SrcAlpha OneMinusSrcAlpha
        
        // Disable depth writing to prevent rendering over other objects
        ZWrite Off
        
        // Render both front and back faces
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            // Inspector variables
            float4 _DangerColor;
            float4 _NextZoneColor;
            float4 _CurrentZoneLineColor;
            float _LineThickness;

            // Global variables assigned by ZoneController.cs
            float _GlobalZoneRadius;
            float4 _GlobalZoneCenter;
            float _GlobalNextZoneRadius;
            float4 _GlobalNextZoneCenter;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Calculate pixel position in world space
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Isolate X and Z coordinates (ignore Y altitude for top-down view)
                float2 currentCenterXZ = _GlobalZoneCenter.xz;
                float2 nextCenterXZ = _GlobalNextZoneCenter.xz;
                float2 pixelPosXZ = i.worldPos.xz;

                // Calculate distances from current pixel to zone centers
                float distToCurrent = distance(pixelPosXZ, currentCenterXZ);
                float distToNext = distance(pixelPosXZ, nextCenterXZ);

                // Default color is completely transparent
                float4 col = float4(0, 0, 0, 0);

                // DANGER ZONE
                // Outside the current zone: Apply danger color
                if (distToCurrent > _GlobalZoneRadius)
                {
                    col = _DangerColor;
                }

                // DRAW NEXT ZONE LINE
                float nextLineWidth = abs(distToNext - _GlobalNextZoneRadius);
                if (nextLineWidth < _LineThickness)
                {
                    // Overlay the next zone line color
                    col = lerp(col, _NextZoneColor, _NextZoneColor.a);
                }

                // DRAW CURRENT ZONE LINE
                float currentLineWidth = abs(distToCurrent - _GlobalZoneRadius);
                if (currentLineWidth < _LineThickness)
                {
                    // Overlay the current zone line color
                    col = lerp(col, _CurrentZoneLineColor, _CurrentZoneLineColor.a);
                }

                return col;
            }
            ENDCG
        }
    }
}
