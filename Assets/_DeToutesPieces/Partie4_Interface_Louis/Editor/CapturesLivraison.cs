using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeToutesPieces.EditorTools
{
    /// <summary>
    /// Captures d'écran de livraison (Partie 4) : rendu depuis un point de vue donné, suréchantillonné 2×
    /// puis réduit, pour obtenir des images 1920×1080 nettes (sans l'aide clavier-souris à l'écran).
    /// </summary>
    public static class CapturesLivraison
    {
        public static string Folder => System.IO.Path.GetDirectoryName(Application.dataPath) + "/Livraison/Captures";

        public static string Snap(string name, Vector3 position, Vector3 lookAt, float fov = 72f, int width = 1920, int height = 1080)
        {
            System.IO.Directory.CreateDirectory(Folder);
            var go = new GameObject("Capture (temporaire)");
            var cam = go.AddComponent<Camera>();
            if (Camera.main != null) cam.CopyFrom(Camera.main);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            cam.enabled = false;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            go.transform.position = position;
            go.transform.LookAt(lookAt);

            var big = new RenderTexture(width * 2, height * 2, 24, RenderTextureFormat.ARGB32);
            var small = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            cam.targetTexture = big;
            cam.Render();
            cam.targetTexture = null;
            Graphics.Blit(big, small);

            RenderTexture.active = small;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string path = $"{Folder}/{name}.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            big.Release(); small.Release();
            Object.DestroyImmediate(go);
            return path;
        }
    }
}
