using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeToutesPieces.EditorTools
{
    /// <summary>
    /// Textures procédurales de l'atelier (Partie 4) : générées par code, donc originales et libres de diffusion.
    /// Chaque texture est répétable (bords raccordés) et n'est régénérée que si elle n'existe pas encore.
    /// </summary>
    public static class AmbianceTextures
    {
        public const string Folder = "Assets/_DeToutesPieces/Partie4_Interface_Louis/Textures";

        /// <summary>Cartes de normales désactivées (voir AmbianceKit.Lit) : le relief est porté par l'albédo.</summary>
        public const bool UseNormalMaps = false;

        // ------------------------------------------------------------------ bruit répétable

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791 + 1013904223);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; h *= 0x27d4eb2d; h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        private static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        /// <summary>Bruit de valeur dont le réseau se répète tous les px × py.</summary>
        public static float Noise(float x, float y, int px, int py, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, px), xb = Mod(x0 + 1, px), ya = Mod(y0, py), yb = Mod(y0 + 1, py);
            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Somme de bruits ; u, v dans [0,1[ ; périodes de base px, py (répétable sur la texture).</summary>
        public static float Fbm(float u, float v, int px, int py, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                int f = 1 << i;
                sum += amp * Noise(u * px * f, v * py * f, px * f, py * f, seed + i * 31);
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ bois

        /// <summary>Veinage de chêne : u le long du fil, v en travers. Renvoie la couleur, h = relief.</summary>
        private static Color Oak(float u, float v, int px, int py, int seed, Color light, Color dark, out float h)
        {
            float warp = Fbm(u, v, Mathf.Max(1, px / 2), Mathf.Max(1, py / 3), 4, seed);
            float rings = v * py * 2.2f + warp * 5f;
            float band = Mathf.Abs(Mathf.Sin(rings * Mathf.PI));
            float late = Mathf.Pow(1f - band, 5f);                       // fines lignes sombres
            float fibers = Noise(u * px * 3f, v * py * 40f, px * 3, py * 40, seed + 7);
            float tone = Fbm(u, v, Mathf.Max(1, px / 2), Mathf.Max(1, py / 4), 3, seed + 3);
            float pores = Noise(u * px * 12f, v * py * 90f, px * 12, py * 90, seed + 11);
            pores = pores > 0.82f ? (pores - 0.82f) * 3f : 0f;
            float t = Mathf.Clamp01(late * 0.5f + fibers * 0.16f + tone * 0.32f + pores * 0.3f);
            h = 1f - late * 0.5f - fibers * 0.2f - pores * 0.6f;
            return Color.Lerp(light, dark, t);
        }

        // ------------------------------------------------------------------ génération

        private delegate Color Painter(float u, float v, out float height);

        private static Texture2D Make(string name, int w, int hgt, Painter paint, float normalStrength, bool force, out Texture2D normal)
        {
            string path = $"{Folder}/{name}.png";
            string npath = $"{Folder}/{name}_N.png";
            normal = null;
            if (!UseNormalMaps) normalStrength = 0f;
            if (!force && File.Exists(path))
            {
                if (normalStrength > 0f) normal = AssetDatabase.LoadAssetAtPath<Texture2D>(npath);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            Directory.CreateDirectory(Folder);
            var px = new Color[w * hgt];
            var heights = new float[w * hgt];
            for (int y = 0; y < hgt; y++)
                for (int x = 0; x < w; x++)
                {
                    float hh;
                    var c = paint((x + 0.5f) / w, (y + 0.5f) / hgt, out hh);
                    c.a = 1f;
                    px[y * w + x] = c;
                    heights[y * w + x] = hh;
                }
            Write(path, px, w, hgt, false);
            if (normalStrength > 0f)
            {
                var n = new Color[w * hgt];
                for (int y = 0; y < hgt; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float l = heights[y * w + Mod(x - 1, w)], r = heights[y * w + Mod(x + 1, w)];
                        float d = heights[Mod(y - 1, hgt) * w + x], u = heights[Mod(y + 1, hgt) * w + x];
                        var nv = new Vector3((l - r) * normalStrength, (d - u) * normalStrength, 1f).normalized;
                        n[y * w + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
                    }
                Write(npath, n, w, hgt, true);
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(npath);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Write(string path, Color[] px, int w, int h, bool isNormal)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, isNormal);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !isNormal;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.anisoLevel = 4;
            imp.mipmapEnabled = true;
            imp.maxTextureSize = Mathf.Max(w, h);
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ catalogue

        public struct Pair { public Texture2D albedo, normal; }

        private static Pair P(Texture2D a, Texture2D n) => new Pair { albedo = a, normal = n };

        /// <summary>Chêne clair : la texture couvre 1 m × 1 m (fil selon u).</summary>
        public static Pair Oak(bool force = false)
        {
            var light = new Color(0.86f, 0.74f, 0.57f);
            var dark = new Color(0.64f, 0.48f, 0.31f);
            var a = Make("T_CheneClair", 1024, 1024, (float u, float v, out float h) => Oak(u, v, 2, 8, 17, light, dark, out h), 2.5f, force, out var n);
            return P(a, n);
        }

        /// <summary>Parquet de chêne : lames de 16 cm × 96 cm, la texture couvre 1,92 m.</summary>
        public static Pair Parquet(bool force = false)
        {
            var light = new Color(0.82f, 0.69f, 0.52f);
            var dark = new Color(0.60f, 0.45f, 0.30f);
            const int rows = 12;
            var a = Make("T_Parquet", 1024, 1024, (float u, float v, out float h) =>
            {
                int row = Mathf.FloorToInt(v * rows);
                float fv = v * rows - row;
                float shift = Hash(row, 3, 91);
                float su = u * 2f + shift;
                int plank = Mod(Mathf.FloorToInt(su), 2);
                float fu = su - Mathf.Floor(su);
                int id = row * 7 + plank;
                // Coordonnées de bois propres à la lame (fil le long de u).
                float wu = Mathf.Repeat(fu * 0.5f + Hash(id, 1, 5), 1f);
                float wv = Mathf.Repeat(fv * 0.125f + Hash(id, 2, 5), 1f);
                var c = Oak(wu, wv, 2, 8, 23 + (id % 5), light, dark, out float wh);
                float tint = Mathf.Lerp(0.9f, 1.07f, Hash(id, 4, 9));
                c *= tint;
                float edgeV = Mathf.Min(fv, 1f - fv) * 0.16f;            // en m
                float edgeU = Mathf.Min(fu, 1f - fu) * 0.96f;
                float edge = Mathf.Min(edgeV, edgeU);
                float bevel = Smooth(0.0004f, 0.0022f, edge);
                h = wh * 0.25f + bevel;
                c = Color.Lerp(new Color(0.30f, 0.22f, 0.15f), c, Mathf.Lerp(0.35f, 1f, bevel));
                return c;
            }, 3f, force, out var n);
            return P(a, n);
        }

        /// <summary>Enduit mat blanc cassé (la texture couvre 2 m), à teinter dans le matériau.</summary>
        public static Pair Plaster(bool force = false)
        {
            var a = Make("T_Enduit", 512, 512, (float u, float v, out float h) =>
            {
                float n1 = Fbm(u, v, 6, 6, 5, 41);
                float n2 = Noise(u * 160f, v * 160f, 160, 160, 43);
                h = n1 * 0.6f + n2 * 0.4f;
                float k = 0.965f + (n1 - 0.5f) * 0.05f + (n2 - 0.5f) * 0.015f;
                return new Color(k, k, k);
            }, 1.2f, force, out var n);
            return P(a, n);
        }

        /// <summary>Feutre / tissu acoustique (la texture couvre 0,5 m), niveaux de gris à teinter.</summary>
        public static Pair Felt(bool force = false)
        {
            var a = Make("T_Feutre", 512, 512, (float u, float v, out float h) =>
            {
                float n1 = Noise(u * 256f, v * 256f, 256, 256, 61);
                float n2 = Fbm(u, v, 8, 8, 4, 63);
                h = n1 * 0.7f + n2 * 0.3f;
                float k = 0.86f + (n1 - 0.5f) * 0.12f + (n2 - 0.5f) * 0.08f;
                return new Color(k, k, k);
            }, 2f, force, out var n);
            return P(a, n);
        }

        /// <summary>Grain fin de peinture époxy / plastique (normale seule, couvre 0,25 m).</summary>
        public static Texture2D FineGrainNormal(bool force = false)
        {
            Make("T_GrainFin", 256, 256, (float u, float v, out float h) =>
            {
                h = Noise(u * 128f, v * 128f, 128, 128, 71) * 0.7f + Noise(u * 64f, v * 64f, 64, 64, 73) * 0.3f;
                return Color.white;
            }, 1.5f, force, out var n);
            return n;
        }

        /// <summary>Panneau perforé blanc : trous de 6 mm tous les 25 mm (la texture couvre 0,4 m).</summary>
        public static Pair Pegboard(bool force = false)
        {
            const int holes = 16;
            var a = Make("T_PanneauPerfore", 512, 512, (float u, float v, out float h) =>
            {
                float cu = (u * holes) - Mathf.Floor(u * holes) - 0.5f;
                float cv = (v * holes) - Mathf.Floor(v * holes) - 0.5f;
                float d = Mathf.Sqrt(cu * cu + cv * cv) * (0.4f / holes);       // en m
                float hole = 1f - Smooth(0.0026f, 0.0034f, d);
                float n1 = Fbm(u, v, 8, 8, 3, 81);
                h = 1f - hole;
                float k = 0.93f + (n1 - 0.5f) * 0.03f;
                return Color.Lerp(new Color(k, k, k * 0.985f), new Color(0.09f, 0.09f, 0.1f), hole);
            }, 4f, force, out var n);
            return P(a, n);
        }

        /// <summary>Tapis de montage bleu ardoise quadrillé (1 cm / 5 cm), la texture couvre 0,64 m.</summary>
        public static Pair AssemblyMat(bool force = false)
        {
            const float size = 0.64f;
            var a = Make("T_TapisMontage", 1024, 1024, (float u, float v, out float h) =>
            {
                float xm = u * size, ym = v * size;
                float Line(float m, float step, float width)
                {
                    float f = m / step; f -= Mathf.Round(f);
                    return 1f - Smooth(width * 0.5f, width, Mathf.Abs(f) * step);
                }
                float fine = Mathf.Max(Line(xm, 0.01f, 0.0006f), Line(ym, 0.01f, 0.0006f));
                float bold = Mathf.Max(Line(xm, 0.05f, 0.0011f), Line(ym, 0.05f, 0.0011f));
                float n1 = Fbm(u, v, 16, 16, 3, 91);
                var baseC = new Color(0.16f, 0.24f, 0.32f) * (0.96f + (n1 - 0.5f) * 0.1f);
                var c = Color.Lerp(baseC, new Color(0.34f, 0.44f, 0.54f), fine * 0.6f);
                c = Color.Lerp(c, new Color(0.62f, 0.69f, 0.76f), bold * 0.8f);
                h = n1 * 0.3f - fine * 0.2f - bold * 0.3f;
                return c;
            }, 1.5f, force, out var n);
            return P(a, n);
        }

        /// <summary>Carton ondulé kraft (la texture couvre 0,5 m).</summary>
        public static Pair Cardboard(bool force = false)
        {
            var a = Make("T_Carton", 512, 512, (float u, float v, out float h) =>
            {
                float tone = Fbm(u, v, 4, 4, 4, 101);
                float fib = Noise(u * 220f, v * 60f, 220, 60, 103);
                float flute = Mathf.Sin(u * Mathf.PI * 2f * 64f) * 0.5f + 0.5f;
                h = flute * 0.25f + fib * 0.2f;
                var c = new Color(0.70f, 0.54f, 0.37f) * (0.92f + tone * 0.14f + (fib - 0.5f) * 0.06f);
                return c;
            }, 1.2f, force, out var n);
            return P(a, n);
        }

        /// <summary>Vue extérieure lumineuse (ciel, cime d'arbres, pelouse) pour les fenêtres.</summary>
        public static Texture2D OutsideView(bool force = false)
        {
            return Make("T_VueExterieure", 512, 256, (float u, float v, out float h) =>
            {
                h = 0f;
                var sky = Color.Lerp(new Color(0.93f, 0.95f, 0.96f), new Color(0.66f, 0.80f, 0.94f), Smooth(0.35f, 1f, v));
                float line = 0.34f + Fbm(u, 0.5f, 12, 1, 4, 111) * 0.22f;
                float leaves = Fbm(u, v, 24, 12, 4, 113);
                var tree = Color.Lerp(new Color(0.50f, 0.62f, 0.46f), new Color(0.70f, 0.78f, 0.62f), leaves);
                var lawn = Color.Lerp(new Color(0.68f, 0.76f, 0.52f), new Color(0.76f, 0.82f, 0.60f), Fbm(u, v, 30, 8, 3, 117));
                var c = v < line ? tree : sky;
                c = Color.Lerp(c, sky, Smooth(line - 0.015f, line + 0.015f, v) * (v < line ? 1f : 0f));
                if (v < 0.12f) c = Color.Lerp(lawn, c, Smooth(0.10f, 0.12f, v));
                return c;
            }, 0f, force, out _);
        }

        /// <summary>Régénère toutes les textures (menu).</summary>
        public static void GenerateAll(bool force)
        {
            Oak(force); Parquet(force); Plaster(force); Felt(force);
            Pegboard(force); AssemblyMat(force); Cardboard(force); OutsideView(force);
            AssetDatabase.SaveAssets();
        }
    }
}
