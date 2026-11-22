// Rendu peint d'Aldermark : l'image basse résolution de la caméra est
// postérisée, tramée, étalonnée (ombres violettes, lumières chaudes),
// avec rayons de soleil et superposition de la vue du Sceau.
Shader "Aldermark/PeintPixel"
{
    Properties
    {
        _MainTex ("Image", 2D) = "black" {}
        _SceauTex ("Sceau", 2D) = "black" {}
        _Peint ("Peint (0 = brut)", Range(0,1)) = 1
        _Sceau ("Sceau", Range(0,1)) = 0
        _Niveaux ("Niveaux de couleur", Float) = 9
        _Tramage ("Tramage", Range(0,1)) = 0
        _Res ("Résolution", Vector) = (480,270,0,0)
        _SoleilPos ("Soleil (écran)", Vector) = (0.5,0.5,0,0)
        _SoleilVis ("Soleil visible", Range(0,1)) = 0
        _Nuit ("Nuit", Range(0,1)) = 0
        _Crepuscule ("Crépuscule", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off ZWrite Off ZTest Always Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _SceauTex;
            float _Peint, _Sceau, _Niveaux, _Tramage, _SoleilVis, _Nuit, _Crepuscule;
            float4 _Res, _SoleilPos;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float b2(float2 a) { a = floor(a); return frac(dot(a, float2(.5, a.y * .75))); }
            float bayer(float2 a) { return b2(.5 * a) * .25 + b2(a); }
            float3 versPerceptuel(float3 c) { return pow(max(c, 0), 1.0 / 2.2); }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 c = versPerceptuel(tex2D(_MainTex, i.uv).rgb);

                // Rayons de soleil (diffusion radiale depuis le soleil)
                if (_SoleilVis > .01)
                {
                    float2 dv = (_SoleilPos.xy - i.uv) / 24.0, uu = i.uv, asp = float2(_Res.x / _Res.y, 1);
                    float r = 0;
                    [unroll] for (int k = 0; k < 24; k++)
                    {
                        uu += dv;
                        float3 s = versPerceptuel(tex2D(_MainTex, uu).rgb);
                        float pres = saturate(1. - length((uu - _SoleilPos.xy) * asp) * 4.);
                        r += smoothstep(.9, 1., dot(s, float3(.333, .333, .333))) * pres * (1. - k / 24.0);
                    }
                    float2 d = (i.uv - _SoleilPos.xy) * float2(_Res.x / _Res.y, 1);
                    r = r / 24.0 * _SoleilVis * (1. - smoothstep(0., 1., length(d)));
                    c += float3(1., .8, .52) * r * 1.5;
                }

                float l = dot(c, float3(.299, .587, .114));
                float3 brut = lerp(float3(l, l, l), c, .35) * float3(.92, .94, .98);

                float3 p = c;
                p = lerp(p, p * float3(.9, .96, 1.1), 1. - smoothstep(.05, .45, l));
                p = lerp(p, p * float3(1.04, 1.02, .95), smoothstep(.45, 1., l));
                p = lerp(float3(l, l, l), p, 1.3);
                float t = bayer(floor(i.uv * _Res.xy)) - .5;
                p = floor(p * _Niveaux + t * _Tramage + .5) / _Niveaux;

                c = lerp(brut, p, _Peint);

                // Crépuscule doré, puis nuit bleutée (lune)
                c *= lerp(float3(1, 1, 1), float3(1.1, .88, .72), _Crepuscule);
                c = lerp(c, c * float3(.3, .38, .7) + float3(.012, .016, .04), _Nuit);

                float3 o = versPerceptuel(tex2D(_SceauTex, i.uv).rgb);
                c = lerp(c, l * float3(.42, .46, .66), .72 * _Sceau) + o * 1.35 * _Sceau;

                float2 q = i.uv - .5;
                c *= 1. - dot(q, q) * .45;
                return fixed4(pow(saturate(c), 2.2), 1);
            }
            ENDCG
        }
    }
}
