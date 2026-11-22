using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Aldermark
{
    /// <summary>
    /// Rendu peint : la caméra principale dessine dans une image basse résolution,
    /// affichée plein écran à travers le shader Aldermark/PeintPixel.
    /// Une seconde caméra dessine la couche « Sceau » (flux, veines) dans une autre image.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RenduPeint : MonoBehaviour
    {
        [Tooltip("Nombre de pixels sur le grand côté de l'image peinte.")]
        public int resolutionLongue = 600;
        public Material materiauPeint;
        public string coucheSceau = "Sceau";
        public bool peint = true;
        [Range(0, 1)] public float sceau;
        public Vector3 directionSoleil = new Vector3(-.48f, .55f, -.68f);
        [Range(0, 1)] public float soleilDegage = 1f;

        Camera cam, camSceau, camEcran;
        RenderTexture rt, rtSceau;
        UnityEngine.UI.RawImage image;
        Material mat;
        int largeurEcran, hauteurEcran;
        bool peintApplique;

        void Awake()
        {
            cam = GetComponent<Camera>();
            int masque = LayerMask.GetMask(coucheSceau);
            cam.cullingMask &= ~masque;

            mat = materiauPeint != null ? new Material(materiauPeint) : new Material(Shader.Find("Aldermark/PeintPixel"));

            // Caméra du Sceau : mêmes réglages, ne voit que la couche Sceau, fond noir.
            var goS = new GameObject("CameraSceau");
            goS.transform.SetParent(transform, false);
            camSceau = goS.AddComponent<Camera>();
            camSceau.cullingMask = masque;
            camSceau.clearFlags = CameraClearFlags.SolidColor;
            camSceau.backgroundColor = Color.black;
            camSceau.farClipPlane = 260f;
            camSceau.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camSceau.GetUniversalAdditionalCameraData().renderShadows = false;

            // Caméra d'écran : ne dessine rien, évite l'avertissement « aucune caméra ».
            var goE = new GameObject("CameraEcran");
            camEcran = goE.AddComponent<Camera>();
            camEcran.cullingMask = 0;
            camEcran.clearFlags = CameraClearFlags.SolidColor;
            camEcran.backgroundColor = Color.black;
            camEcran.depth = cam.depth + 10;
            camEcran.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            // Toile plein écran qui affiche l'image peinte.
            var goC = new GameObject("ToileRenduPeint");
            var canvas = goC.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            var goI = new GameObject("Image");
            goI.transform.SetParent(goC.transform, false);
            image = goI.AddComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            image.material = mat;
            var r = image.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

            Recreer();
        }

        void Recreer()
        {
            largeurEcran = Mathf.Max(1, Screen.width);
            hauteurEcran = Mathf.Max(1, Screen.height);
            peintApplique = peint;
            int w, h;
            if (peint)
            {
                float a = (float)largeurEcran / hauteurEcran;
                if (a >= 1) { w = resolutionLongue; h = Mathf.RoundToInt(resolutionLongue / a); }
                else { h = resolutionLongue; w = Mathf.RoundToInt(resolutionLongue * a); }
            }
            else { w = largeurEcran; h = hauteurEcran; }

            Liberer();
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "ImagePeinte" };
            rtSceau = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "ImageSceau" };
            rt.Create(); rtSceau.Create();
            cam.targetTexture = rt;
            camSceau.targetTexture = rtSceau;
            image.texture = rt;
            mat.SetTexture("_SceauTex", rtSceau);
            mat.SetVector("_Res", new Vector4(w, h, 0, 0));
        }

        void Liberer()
        {
            if (cam != null) cam.targetTexture = null;
            if (camSceau != null) camSceau.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (rtSceau != null) { rtSceau.Release(); Destroy(rtSceau); }
        }

        void LateUpdate()
        {
            if (Screen.width != largeurEcran || Screen.height != hauteurEcran || peint != peintApplique) Recreer();

            camSceau.fieldOfView = cam.fieldOfView;
            camSceau.nearClipPlane = cam.nearClipPlane;
            camSceau.enabled = sceau > .01f;

            mat.SetFloat("_Peint", peint ? 1f : 0f);
            mat.SetFloat("_Sceau", sceau);
            mat.SetFloat("_Nuit", SoleilNuages.Nuit);
            mat.SetFloat("_Crepuscule", SoleilNuages.Crepuscule);

            Vector3 vp = cam.WorldToViewportPoint(cam.transform.position + directionSoleil.normalized * 1000f);
            float vis = 0f;
            if (vp.z > 0)
            {
                float bord = Mathf.Max(Mathf.Abs(vp.x - .5f), Mathf.Abs(vp.y - .5f)) * 2f;
                vis = soleilDegage * (1f - Monde.Lisse(.9f, 1.6f, bord));
            }
            mat.SetVector("_SoleilPos", new Vector4(vp.x, vp.y, 0, 0));
            mat.SetFloat("_SoleilVis", vis);
        }

        void OnDestroy() => Liberer();
    }
}
