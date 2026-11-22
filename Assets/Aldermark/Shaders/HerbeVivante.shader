// Herbe vivante d'Aldermark : touffes dessinées en instanciation autour d'Aldric.
// Le vent les fait onduler par rafales et elles s'écartent quand Aldric passe dedans.
// Couleur calculée ici à partir de la position (vert, doré, fleurs, blé), sans texture.
// _AldJoueur vient de HerbeVivante.cs, _AldTemps de SoleilNuages.cs.
Shader "Aldermark/HerbeVivante"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _AldJoueur;
            float _AldTemps;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 couleur : TEXCOORD1;
                float brouillard : TEXCOORD2;
            };

            float hash21(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            Varyings vert (Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                float3 pied = TransformObjectToWorld(float3(0, 0, 0));
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float hauteurInstance = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
                float h = saturate(i.positionOS.y);

                // Fondu en bord de zone pour masquer le recyclage des touffes
                float2 dd = pied.xz - _AldJoueur.xz;
                float dl = length(dd);
                float fondu = 1. - smoothstep(26., 34., dl);
                ws.y = pied.y + (ws.y - pied.y) * fondu;

                // Aldric écarte l'herbe
                float k = 1. - smoothstep(.3, 1.9, dl);
                float2 dir = dl > .001 ? dd / dl : float2(0, 0);
                ws.xz += dir * k * .6 * h;
                ws.y -= k * .3 * h * fondu;

                // Vent par rafales
                float rafale = .5 + .5 * sin(_AldTemps * .8 - pied.x * .06 - pied.z * .02);
                float2 vent = float2(sin(_AldTemps * 1.9 + pied.x * .4 + pied.z * .25) * .6 + rafale * .7,
                                     cos(_AldTemps * 1.4 + pied.z * .33) * .5 + rafale * .25);
                ws.xz += vent * (.03 + .07 * rafale) * h;

                // Couleur : prairie verte/dorée, blé (touffes hautes), quelques fleurs
                float g = .5 + .5 * sin(pied.x * .05 + cos(pied.z * .04) * 2.);
                half3 c = lerp(half3(.45, .64, .26), half3(.80, .68, .30), g * .55);
                float hs = hash21(floor(pied.xz * 3.));
                if (hauteurInstance > 1.3) c = lerp(half3(.86, .70, .30), half3(.93, .80, .40), hs);
                else if (hs < .045 && h > .75)
                {
                    float f = frac(hs * 97.);
                    c = f < .25 ? half3(.96, .94, 1.) : (f < .5 ? half3(.72, .62, 1.) : (f < .75 ? half3(1., .85, .3) : half3(.55, .75, 1.)));
                }
                c *= i.color.rgb;
                o.couleur = pow(max(c, 0.), 2.2);

                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.brouillard = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float4 coordOmbre = TransformWorldToShadowCoord(i.positionWS);
                Light soleil = GetMainLight(coordOmbre);
                half3 ambiant = SampleSH(half3(0, 1, 0));
                half3 c = i.couleur * (ambiant + soleil.color * soleil.shadowAttenuation * .85);
                c = MixFog(c, i.brouillard);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
