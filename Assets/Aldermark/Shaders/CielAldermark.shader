// Ciel d'Aldermark, direction artistique Daisi :
// dégradé bleu pervenche, gros cumulus aux formes nettes peints en aplats
// (dessous bleu-gris, bords blancs côté lumière), halo de soleil sans disque.
// Les deux soleils du lore s'affichent avec _Disques = 1.
// Le temps (_AldTemps) et la couverture (_Couverture) viennent de SoleilNuages,
// pour rester synchronisés avec le calcul CPU de l'ombre des nuages sur le soleil.
Shader "Aldermark/Ciel"
{
    Properties
    {
        _Soleil1 ("Direction soleil 1", Vector) = (-0.48,0.55,-0.68,0)
        _Soleil2 ("Direction soleil 2", Vector) = (-0.40,0.58,-0.71,0)
        _Disques ("Afficher les deux soleils (lore)", Range(0,1)) = 0
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
            float _AldTemps, _Disques, _Couverture;

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

            // Densité des nuages (même formule que Monde.DensiteNuages côté CPU)
            float densite(float2 p)
            {
                float2 q = float2(fb(p * 1.1), fb(p * 1.1 + 5.2));
                return fb(p * 1.6 + q * 1.2);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 s1 = normalize(_Soleil1.xyz), s2 = normalize(_Soleil2.xyz);

                // Dégradé bleu pervenche
                float3 col = lerp(float3(.80, .83, .95), float3(.56, .62, .90), smoothstep(0., .25, y));
                col = lerp(col, float3(.33, .38, .78), smoothstep(.25, .85, y));
                col = lerp(col, float3(.55, .58, .75), 1. - smoothstep(-.15, 0., y));

                float a = dot(d, s1), b = dot(d, s2);
                col += float3(1., .86, .62) * pow(max(a, 0.), 10.) * .22;
                col = lerp(col, float3(1., .97, .86), smoothstep(.9982, .9987, a) * _Disques);
                col = lerp(col, float3(1., .9, .84), smoothstep(.99945, .99958, b) * _Disques);

                // Cumulus
                float2 p = d.xz / (max(y, 0.) + .25) * .5 + float2(_AldTemps * .012, _AldTemps * .004);
                float n = densite(p);
                float seuil = 1. - _Couverture;
                float bande = smoothstep(0., .06, y) * (1. - smoothstep(.3, .75, y) * .85);
                float dens = smoothstep(seuil, seuil + .06, n) * bande;
                float2 versSoleil = normalize(s1.xz + 1e-4) * .09;
                float n2 = densite(p + versSoleil);
                float eclaire = saturate((n - n2) * 7. + .5);
                float epaisseur = smoothstep(seuil, seuil + .3, n);
                float t = saturate(eclaire * .8 + (1. - epaisseur) * .35);
                t = floor(t * 4.) / 3.;
                float3 sombre = float3(.33, .36, .62), moyen = float3(.55, .58, .83), clair = float3(.80, .82, .96), blanc = float3(.95, .95, 1.);
                float3 nuage = t < .34 ? lerp(sombre, moyen, t * 3.) : (t < .67 ? lerp(moyen, clair, (t - .34) * 3.) : lerp(clair, blanc, (t - .67) * 3.));
                float bord = (1. - smoothstep(seuil + .01, seuil + .05, n)) * step(.5, eclaire);
                nuage = lerp(nuage, blanc, bord * .8);
                col = lerp(col, nuage, dens);

                return fixed4(GammaToLinearSpace(col), 1);
            }
            ENDCG
        }
    }
}
