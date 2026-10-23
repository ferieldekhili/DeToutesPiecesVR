using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeToutesPieces.EditorTools
{
    /// <summary>
    /// Boîte à outils de l'habillage (Partie 4) : matériaux URP, maillages arrondis générés et
    /// fonctions de construction des accessoires. Tout est créé par code (aucun modèle externe).
    /// </summary>
    public class AmbianceKit
    {
        public const string P4 = "Assets/_DeToutesPieces/Partie4_Interface_Louis";
        public const string MaterialFolder = P4 + "/Materials/Ambiance";
        public const string MeshAsset = P4 + "/Meshes/Maillages_Atelier.asset";

        // ------------------------------------------------------------------ matériaux

        public Material Oak, OakSlat, Birch, Parquet, Wall, WallAccent, Ceiling, WhiteLacquer, BlackMetal, Aluminium, Chrome,
            Felt, FeltBlue, FeltSand, Rubber, Pegboard, Mat, Cardboard, Paper, Glass, Outside, Led, LedWarm,
            BluePlastic, YellowPlastic, BlackPlastic, GreyPlastic, Blue, Yellow, Charcoal, Leaf, LeafLight, Soil, Ceramic, Screen;

        public AmbianceKit()
        {
            var oak = AmbianceTextures.Oak();
            var parquet = AmbianceTextures.Parquet();
            var plaster = AmbianceTextures.Plaster();
            var felt = AmbianceTextures.Felt();
            Texture2D grain = null; // grain fin réservé aux cartes de normales (désactivées)
            var peg = AmbianceTextures.Pegboard();
            var mat = AmbianceTextures.AssemblyMat();
            var card = AmbianceTextures.Cardboard();
            var outside = AmbianceTextures.OutsideView();

            Oak = Lit("M_A_ChêneClair", Color.white, 0.42f, 0f, oak.albedo, oak.normal, 1f, 0.35f);
            // Tasseaux : teinte unie (une texture identique sur 70 tasseaux dessinerait des arcs parasites).
            OakSlat = Lit("M_A_ChêneTasseau", new Color(0.74f, 0.58f, 0.40f), 0.35f, 0f, null, null, 1f, 0f);
            Birch = Lit("M_A_Bouleau", new Color(1.06f, 1.04f, 1f), 0.38f, 0f, oak.albedo, oak.normal, 1.6f, 0.25f);
            Parquet = Lit("M_A_Parquet", Color.white, 0.5f, 0f, parquet.albedo, parquet.normal, 1f / 1.92f, 0.5f);
            Wall = Lit("M_A_EnduitBlancCassé", new Color(0.93f, 0.92f, 0.89f), 0.12f, 0f, plaster.albedo, plaster.normal, 0.5f, 0.3f);
            WallAccent = Lit("M_A_EnduitGrisBleu", new Color(0.70f, 0.75f, 0.79f), 0.12f, 0f, plaster.albedo, plaster.normal, 0.5f, 0.3f);
            Ceiling = Lit("M_A_Plafond", new Color(0.96f, 0.96f, 0.95f), 0.08f, 0f, plaster.albedo, null, 0.5f, 0f);
            WhiteLacquer = Lit("M_A_LaqueBlanche", new Color(0.92f, 0.92f, 0.90f), 0.55f, 0f, null, grain, 4f, 0.15f);
            BlackMetal = Lit("M_A_MétalNoirThermolaqué", new Color(0.075f, 0.077f, 0.082f), 0.42f, 0.15f, null, grain, 4f, 0.35f);
            Aluminium = Lit("M_A_Aluminium", new Color(0.78f, 0.79f, 0.80f), 0.62f, 0.9f, null, grain, 6f, 0.15f);
            Chrome = Lit("M_A_AcierBrillant", new Color(0.82f, 0.83f, 0.85f), 0.85f, 1f, null, null, 1f, 0f);
            Felt = Lit("M_A_FeutreAnthracite", new Color(0.24f, 0.25f, 0.27f), 0.05f, 0f, felt.albedo, felt.normal, 2f, 0.6f);
            FeltBlue = Lit("M_A_FeutreBleu", new Color(0.30f, 0.42f, 0.56f), 0.05f, 0f, felt.albedo, felt.normal, 2f, 0.6f);
            FeltSand = Lit("M_A_FeutreSable", new Color(0.80f, 0.74f, 0.64f), 0.05f, 0f, felt.albedo, felt.normal, 2f, 0.6f);
            Rubber = Lit("M_A_CaoutchoucGraphite", new Color(0.20f, 0.21f, 0.22f), 0.18f, 0f, felt.albedo, felt.normal, 3f, 0.8f);
            Pegboard = Lit("M_A_PanneauPerforé", Color.white, 0.3f, 0f, peg.albedo, peg.normal, 1f / 0.4f, 0.6f);
            Mat = Lit("M_A_TapisMontage", Color.white, 0.22f, 0f, mat.albedo, mat.normal, 1f / 0.64f, 0.4f);
            Cardboard = Lit("M_A_Carton", Color.white, 0.12f, 0f, card.albedo, card.normal, 2f, 0.5f);
            Paper = Lit("M_A_Papier", new Color(0.95f, 0.95f, 0.93f), 0.2f, 0f, null, null, 1f, 0f);
            Glass = Transparent("M_A_Vitre", new Color(0.85f, 0.92f, 0.95f, 0.12f), 0.96f);
            Outside = Unlit("M_A_VueExtérieure", Color.white, outside);
            Led = Emissive("M_A_DiffuseurLED", new Color(1f, 0.97f, 0.92f), 1.2f);
            LedWarm = Emissive("M_A_DiffuseurLEDChaud", new Color(1f, 0.90f, 0.76f), 1.4f);
            BluePlastic = Lit("M_A_PlastiqueBleu", new Color(0.05f, 0.26f, 0.52f), 0.45f, 0f, null, grain, 4f, 0.1f);
            YellowPlastic = Lit("M_A_PlastiqueJaune", new Color(0.98f, 0.78f, 0.12f), 0.45f, 0f, null, grain, 4f, 0.1f);
            BlackPlastic = Lit("M_A_PlastiqueNoir", new Color(0.05f, 0.05f, 0.055f), 0.4f, 0f, null, grain, 4f, 0.1f);
            GreyPlastic = Lit("M_A_PlastiqueGris", new Color(0.55f, 0.57f, 0.6f), 0.35f, 0f, null, grain, 4f, 0.1f);
            Blue = Lit("M_A_PeintureBleue", new Color(0.0f, 0.30f, 0.58f), 0.3f, 0f, null, null, 1f, 0f);
            Yellow = Lit("M_A_PeintureJaune", new Color(0.99f, 0.80f, 0.10f), 0.3f, 0f, null, null, 1f, 0f);
            Charcoal = Lit("M_A_Anthracite", new Color(0.16f, 0.17f, 0.19f), 0.25f, 0f, null, null, 1f, 0f);
            Leaf = Lit("M_A_Feuillage", new Color(0.13f, 0.27f, 0.15f), 0.45f, 0f, null, null, 1f, 0f);
            LeafLight = Lit("M_A_FeuillageClair", new Color(0.42f, 0.52f, 0.20f), 0.45f, 0f, null, null, 1f, 0f);
            Soil = Lit("M_A_Terreau", new Color(0.16f, 0.11f, 0.08f), 0.05f, 0f, null, null, 1f, 0f);
            Ceramic = Lit("M_A_CéramiqueBlanche", new Color(0.92f, 0.91f, 0.88f), 0.7f, 0f, null, null, 1f, 0f);
            Screen = Emissive("M_A_ÉcranBleu", new Color(0.35f, 0.65f, 1f), 1.2f);
            AssetDatabase.SaveAssets();
        }

        private static Material Load(string name, string shader)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                System.IO.Directory.CreateDirectory(MaterialFolder);
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader.name != shader) m.shader = Shader.Find(shader);
            return m;
        }

        public static Material Lit(string name, Color c, float smooth, float metal, Texture2D albedo, Texture2D normal, float tiling, float bump)
        {
            var m = Load(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", albedo);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            // Pas de carte de normales : avec l'éclairage précalculé, ces variantes d'URP/Lit
            // s'affichent saturées dans cet éditeur, et le Quest y gagne en performances.
            if (AmbianceTextures.UseNormalMaps && normal != null && bump > 0f)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }
            else
            {
                m.SetTexture("_BumpMap", null);
                m.DisableKeyword("_NORMALMAP");
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Transparent(string name, Color c, float smooth)
        {
            var m = Load(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Unlit(string name, Color c, Texture2D tex)
        {
            var m = Load(name, "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Emissive(string name, Color c, float intensity)
        {
            var m = Load(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.5f);
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            // Émission visuelle seulement : la lumière précalculée vient des sources surfaciques placées sous les dalles
            // (un diffuseur émissif collé au plafond y créerait un point chaud qui sature tout l'éclairage indirect).
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ maillages

        private Dictionary<string, Mesh> meshes;

        private Mesh Cached(string key, System.Func<Mesh> build)
        {
            if (meshes == null)
            {
                meshes = new Dictionary<string, Mesh>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MeshAsset))
                    if (o is Mesh mm && !meshes.ContainsKey(mm.name)) meshes[mm.name] = mm;
            }
            if (meshes.TryGetValue(key, out var m) && m != null) return m;

            m = build();
            m.name = key;
            Unwrapping.GenerateSecondaryUVSet(m);
            if (!System.IO.File.Exists(MeshAsset))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MeshAsset));
                var root = new Mesh { name = "_Maillages de l'atelier" };
                AssetDatabase.CreateAsset(root, MeshAsset);
            }
            AssetDatabase.AddObjectToAsset(m, MeshAsset);
            meshes[key] = m;
            return m;
        }

        public Mesh RoundedBoxMesh(Vector3 size, float radius, int seg = 3)
        {
            int mx = Mathf.RoundToInt(size.x * 1000f), my = Mathf.RoundToInt(size.y * 1000f), mz = Mathf.RoundToInt(size.z * 1000f);
            int mr = Mathf.RoundToInt(radius * 10000f);
            string key = $"Boite_{mx}x{my}x{mz}_r{mr}_s{seg}";
            return Cached(key, () => BuildRoundedBox(new Vector3(mx, my, mz) / 1000f, mr / 10000f, seg));
        }

        public Mesh CylinderMesh(int sides, bool smooth)
        {
            return Cached($"Cylindre_{sides}_{(smooth ? "lisse" : "facettes")}", () => BuildCylinder(sides, smooth));
        }

        public Mesh SphereMesh()
        {
            return Cached("Sphere_Basse", () =>
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var m = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(tmp);
                return m;
            });
        }

        private static float[] AxisCoords(float half, float r, int seg)
        {
            if (r <= 1e-5f) return new[] { -half, half };
            var list = new List<float>();
            for (int i = 0; i <= seg; i++)
                list.Add(-(half - r) - r * Mathf.Tan(Mathf.PI / 4f * (seg - i) / seg));
            for (int i = seg; i >= 0; i--)
                list.Add((half - r) + r * Mathf.Tan(Mathf.PI / 4f * (seg - i) / seg));
            // Supprime le doublon central quand le rayon couvre toute la demi-longueur.
            var outList = new List<float>();
            foreach (var v in list) if (outList.Count == 0 || Mathf.Abs(v - outList[outList.Count - 1]) > 1e-6f) outList.Add(v);
            return outList.ToArray();
        }

        public static Mesh BuildRoundedBox(Vector3 size, float radius, int seg)
        {
            var h = size * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
            var inner = h - Vector3.one * r;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Faces : normale, axe u, axe v.
            var faces = new (Vector3 n, Vector3 a, Vector3 b)[]
            {
                (Vector3.right, Vector3.forward, Vector3.up), (Vector3.left, Vector3.forward, Vector3.up),
                (Vector3.up, Vector3.right, Vector3.forward), (Vector3.down, Vector3.right, Vector3.forward),
                (Vector3.forward, Vector3.right, Vector3.up), (Vector3.back, Vector3.right, Vector3.up),
            };
            foreach (var f in faces)
            {
                float ha = Vector3.Dot(h, Abs(f.a)), hb = Vector3.Dot(h, Abs(f.b)), hn = Vector3.Dot(h, Abs(f.n));
                var ca = AxisCoords(ha, r, seg);
                var cb = AxisCoords(hb, r, seg);
                int start = verts.Count;
                for (int j = 0; j < cb.Length; j++)
                    for (int i = 0; i < ca.Length; i++)
                    {
                        var q = f.n * hn + f.a * ca[i] + f.b * cb[j];
                        var cl = new Vector3(Mathf.Clamp(q.x, -inner.x, inner.x), Mathf.Clamp(q.y, -inner.y, inner.y), Mathf.Clamp(q.z, -inner.z, inner.z));
                        var d = q - cl;
                        Vector3 n, p;
                        if (r <= 1e-5f || d.sqrMagnitude < 1e-12f) { n = f.n; p = q; }
                        else { n = d.normalized; p = cl + n * r; }
                        verts.Add(p);
                        norms.Add(n);
                        uvs.Add(new Vector2(Vector3.Dot(p, f.a), Vector3.Dot(p, f.b)));
                    }
                int w = ca.Length;
                for (int j = 0; j < cb.Length - 1; j++)
                    for (int i = 0; i < w - 1; i++)
                    {
                        int i0 = start + j * w + i, i1 = i0 + 1, i2 = i0 + w + 1, i3 = i0 + w;
                        var c = Vector3.Cross(f.a, f.b);
                        bool flip = Vector3.Dot(c, f.n) < 0f; // normale sortante = Cross(v1 - v0, v2 - v0)
                        if (flip) { tris.Add(i0); tris.Add(i2); tris.Add(i1); tris.Add(i0); tris.Add(i3); tris.Add(i2); }
                        else { tris.Add(i0); tris.Add(i1); tris.Add(i2); tris.Add(i0); tris.Add(i2); tris.Add(i3); }
                    }
            }
            var m = new Mesh();
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>Cylindre de rayon 0,5 et de hauteur 1 (axe Y), à mettre à l'échelle.</summary>
        public static Mesh BuildCylinder(int sides, bool smooth)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.5f;
                var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.5f;
                var n0 = smooth ? p0.normalized : ((p0 + p1) * 0.5f).normalized;
                var n1 = smooth ? p1.normalized : n0;
                int s = v.Count;
                v.Add(p0 + Vector3.down * 0.5f); v.Add(p1 + Vector3.down * 0.5f); v.Add(p1 + Vector3.up * 0.5f); v.Add(p0 + Vector3.up * 0.5f);
                n.Add(n0); n.Add(n1); n.Add(n1); n.Add(n0);
                float u0 = (float)i / sides * Mathf.PI, u1 = (float)(i + 1) / sides * Mathf.PI;
                uv.Add(new Vector2(u0, 0)); uv.Add(new Vector2(u1, 0)); uv.Add(new Vector2(u1, 1)); uv.Add(new Vector2(u0, 1));
                t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
                // Couvercles
                int c = v.Count;
                v.Add(Vector3.up * 0.5f); v.Add(p0 + Vector3.up * 0.5f); v.Add(p1 + Vector3.up * 0.5f);
                n.Add(Vector3.up); n.Add(Vector3.up); n.Add(Vector3.up);
                uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(p0.x + 0.5f, p0.z + 0.5f)); uv.Add(new Vector2(p1.x + 0.5f, p1.z + 0.5f));
                t.Add(c); t.Add(c + 2); t.Add(c + 1);
                int d = v.Count;
                v.Add(Vector3.down * 0.5f); v.Add(p0 + Vector3.down * 0.5f); v.Add(p1 + Vector3.down * 0.5f);
                n.Add(Vector3.down); n.Add(Vector3.down); n.Add(Vector3.down);
                uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(p0.x + 0.5f, p0.z + 0.5f)); uv.Add(new Vector2(p1.x + 0.5f, p1.z + 0.5f));
                t.Add(d); t.Add(d + 1); t.Add(d + 2);
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ construction

        /// <summary>Objets du décor fixe : statiques (lot statique, éclairage précalculé).</summary>
        public bool StaticMode = true;

        public static Transform Group(string name, Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = localPos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            return g;
        }

        private GameObject Renderable(string name, Transform parent, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        private void Finish(GameObject go, float area)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (StaticMode)
            {
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
                // Éclairage indirect précalculé lu dans les sondes de lumière (pas de lightmaps : leur décodage
                // est incohérent dans cet éditeur ; les sondes suffisent et ne coûtent presque rien sur Quest).
                if (mr != null) mr.receiveGI = ReceiveGI.LightProbes;
            }
            else if (mr != null) mr.receiveGI = ReceiveGI.LightProbes;
        }

        /// <summary>Boîte à arêtes adoucies (dimensions réelles, échelle 1, UV en mètres).</summary>
        public GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, float radius = 0.003f, bool shadows = true, Quaternion? rot = null)
        {
            var go = Renderable(name, parent, RoundedBoxMesh(size, radius, radius > 0.015f ? 4 : 2), mat, shadows);
            go.transform.localPosition = center;
            go.transform.localRotation = rot ?? Quaternion.identity;
            float area = Mathf.Max(size.x * size.y, Mathf.Max(size.x * size.z, size.y * size.z));
            Finish(go, area);
            return go;
        }

        /// <summary>Cylindre (axe Y local, puis rotation éventuelle).</summary>
        public GameObject Cyl(string name, Transform parent, Vector3 center, float diameter, float length, Material mat, Quaternion? rot = null, int sides = 20, bool shadows = true, bool smooth = true)
        {
            var go = Renderable(name, parent, CylinderMesh(sides, smooth), mat, shadows);
            go.transform.localPosition = center;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = new Vector3(diameter, length, diameter);
            Finish(go, diameter * length);
            return go;
        }

        public GameObject Sphere(string name, Transform parent, Vector3 center, Vector3 size, Material mat, Quaternion? rot = null, bool shadows = true)
        {
            var go = Renderable(name, parent, SphereMesh(), mat, shadows);
            go.transform.localPosition = center;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = size;
            Finish(go, 0f);
            return go;
        }

        public static TextMeshPro Text(string text, Transform parent, Vector3 pos, Quaternion rot, float height, Color color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center, float width = 1f, float spacing = 0f)
        {
            var go = new GameObject("Texte " + text.Split('\n')[0]);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = height * 25f;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.characterSpacing = spacing;
            t.rectTransform.sizeDelta = new Vector2(width, height * 4f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }

        /// <summary>Rotation pour qu'un texte (lu selon -Z local) regarde dans la direction donnée.</summary>
        public static Quaternion Facing(Vector3 towardViewer) => Quaternion.LookRotation(-towardViewer, Vector3.up);
    }
}
