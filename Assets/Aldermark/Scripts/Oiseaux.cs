using UnityEngine;
using UnityEngine.Rendering;

namespace Aldermark
{
    /// <summary>Quelques oiseaux qui tournent au-dessus de la vallée en battant des ailes.</summary>
    public class Oiseaux : MonoBehaviour
    {
        public int nombre = 10;
        public Vector3 centre = new Vector3(60, 45, 20);

        struct Oiseau { public Transform corps, aileG, aileD; public Vector3 c; public float r, v, ph; }
        Oiseau[] vol;

        void Start()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(.16f, .13f, .22f));
            vol = new Oiseau[nombre];
            for (int i = 0; i < nombre; i++)
            {
                var o = new Oiseau
                {
                    corps = new GameObject("Oiseau" + i).transform,
                    c = centre + new Vector3(Random.Range(-30f, 30f), Random.Range(-8f, 12f), Random.Range(-30f, 30f)),
                    r = Random.Range(18f, 55f),
                    v = Random.Range(.18f, .38f) * (i % 2 == 0 ? 1 : -1),
                    ph = Random.Range(0f, 6.28f)
                };
                o.corps.SetParent(transform, false);
                o.corps.localScale = Vector3.one * 1.4f;
                Piece(o.corps, Vector3.zero, new Vector3(.16f, .12f, .5f), m);
                o.aileG = new GameObject("AileG").transform; o.aileG.SetParent(o.corps, false);
                o.aileD = new GameObject("AileD").transform; o.aileD.SetParent(o.corps, false);
                Piece(o.aileG, new Vector3(-.45f, 0, 0), new Vector3(.9f, .03f, .32f), m);
                Piece(o.aileD, new Vector3(.45f, 0, 0), new Vector3(.9f, .03f, .32f), m);
                vol[i] = o;
            }
        }

        void Update()
        {
            float t = Time.time;
            foreach (var o in vol)
            {
                float a = o.ph + t * o.v;
                o.corps.position = o.c + new Vector3(Mathf.Cos(a) * o.r, Mathf.Sin(t * .5f + o.ph) * 2f, Mathf.Sin(a) * o.r);
                var dir = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * Mathf.Sign(o.v);
                o.corps.rotation = Quaternion.LookRotation(dir);
                float b = Mathf.Sin(t * 9f + o.ph * 3f) * 32f;
                o.aileG.localRotation = Quaternion.Euler(0, 0, b);
                o.aileD.localRotation = Quaternion.Euler(0, 0, -b);
            }
        }

        static void Piece(Transform parent, Vector3 pos, Vector3 echelle, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = echelle;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
