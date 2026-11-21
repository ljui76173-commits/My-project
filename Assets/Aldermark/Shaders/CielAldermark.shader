// Ciel d'Aldermark : dégradé lavande, deux soleils, nuages en mouvement
// qui passent devant les soleils et s'éclairent en contre-jour.
// Le temps vient de _AldTemps (envoyé par SoleilNuages) pour rester
// synchronisé avec le calcul d'occultation côté CPU.
Shader "Aldermark/Ciel"
{
    Properties
    {
        _Soleil1 ("Direction soleil 1", Vector) = (-0.28,0.34,0.9,0)
        _Soleil2 ("Direction soleil 2", Vector) = (-0.18,0.38,0.92,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Soleil1, _Soleil2;
            float _AldTemps;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }

            float hs(float2 p) { float3 q = frac(float3(p.x, p.y, p.x) * .1031); q += dot(q, q.yzx + 33.33); return frac((q.x + q.y) * q.z); }
            float ns(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3. - 2. * f);
                return lerp(lerp(hs(i), hs(i + float2(1, 0)), f.x), lerp(hs(i + float2(0, 1)), hs(i + float2(1, 1)), f.x), f.y);
            }
            float fb(float2 p) { float v = 0, a = .5; [unroll] for (int i = 0; i < 5; i++) { v += a * ns(p); p *= 2.03; a *= .5; } return v; }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 col = lerp(float3(.80, .74, .86), float3(.56, .58, .84), smoothstep(0., .28, y));
                col = lerp(col, float3(.30, .32, .64), smoothstep(.28, .9, y));
                col = lerp(col, float3(.62, .58, .70), 1. - smoothstep(-.2, 0., y));

                float3 s1 = normalize(_Soleil1.xyz), s2 = normalize(_Soleil2.xyz);
                float a = dot(d, s1), b = dot(d, s2);
                col += float3(1., .82, .55) * pow(max(a, 0.), 24.) * .35;
                col = lerp(col, float3(1., .97, .86), smoothstep(.9982, .9987, a));
                col = lerp(col, float3(1., .86, .80), smoothstep(.99945, .99958, b));

                float2 p = d.xz / max(y + .08, .04) * .55;
                float n = fb(p + float2(_AldTemps * .02, _AldTemps * .007));
                float c = smoothstep(.48, .74, n) * smoothstep(0., .16, y);
                float fw = pow(max(a, 0.), 5.), bord = 1. - smoothstep(.5, .64, n);
                float3 sh = lerp(float3(.43, .42, .67), float3(.97, .95, 1.), smoothstep(.5, .95, n));
                sh = lerp(sh, sh * float3(.8, .78, .92), fw * smoothstep(.62, .9, n));
                sh += float3(1., .76, .46) * fw * (.25 + bord * 1.7);
                col = lerp(col, sh, c * .95);

                // Couleurs pensées en sRGB : conversion vers l'espace linéaire du projet
                return fixed4(GammaToLinearSpace(col), 1);
            }
            ENDCG
        }
    }
}
