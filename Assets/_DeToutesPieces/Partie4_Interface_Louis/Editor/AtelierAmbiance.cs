using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeToutesPieces.EditorTools
{
    /// <summary>
    /// Refonte visuelle de l'atelier (Partie 4) : pièce, mobilier, établi habillé, éclairage précalculé.
    /// Ne touche ni à la logique du montage, ni aux pièces du kit, ni aux objets référencés par le code :
    /// seuls l'environnement et des groupes d'habillage nommés sont (re)construits, ce qui rend l'opération répétable.
    /// </summary>
    public static class AtelierAmbiance
    {
        // Volume intérieur de la pièce (m).
        private const float X = 3.4f, ZF = 2.15f, ZB = -3.4f, H = 2.9f, T = 0.25f;
        private const float SillY = 0.85f, HeadY = 2.45f;
        private static readonly Vector2[] Windows = { new Vector2(-1.9f, -0.5f), new Vector2(0.3f, 1.7f) };
        private static readonly Quaternion TextRot = Quaternion.Euler(0f, 180f, 0f);
        private static readonly Color Ink = new Color(0.15f, 0.16f, 0.18f);
        private static readonly Color InkSoft = new Color(0.38f, 0.40f, 0.44f);
        private static readonly Color IkeaBlue = new Color(0.0f, 0.30f, 0.58f);

        private const string MenuRoot = "De Toutes Pièces/Ambiance/";

        [MenuItem(MenuRoot + "Appliquer la refonte visuelle de l'atelier")]
        public static void ApplyMenu() => Apply(false);

        [MenuItem(MenuRoot + "Appliquer la refonte et précalculer l'éclairage")]
        public static void ApplyAndBakeMenu() => Apply(true);

        [MenuItem(MenuRoot + "Précalculer l'éclairage uniquement")]
        public static void BakeMenu() => Bake();

        [MenuItem(MenuRoot + "Régénérer les textures procédurales")]
        public static void TexturesMenu() => AmbianceTextures.GenerateAll(true);

        public static bool Apply(bool bake, bool save = true)
        {
            var station = GameObject.Find("Poste de montage");
            var adjustable = GameObject.Find("Plan réglable");
            if (station == null || adjustable == null)
            {
                Debug.LogError("[De Toutes Pièces] Ouvrez la scène Atelier (Poste de montage / Plan réglable introuvables).");
                return false;
            }

            var env = GameObject.Find("Environnement");
            if (env == null) env = new GameObject("Environnement");
            var old = new List<GameObject>();
            foreach (Transform c in env.transform) old.Add(c.gameObject);
            foreach (var go in old) Object.DestroyImmediate(go);

            var k = new AmbianceKit { StaticMode = true };
            BuildRoom(k, env.transform);
            BuildWindows(k, env.transform);
            BuildFrontWall(k, env.transform);
            BuildRightWall(k, env.transform);
            BuildBackWall(k, env.transform);
            BuildLeftWall(k, env.transform);
            BuildCart(k, env.transform);
            BuildCeiling(k, env.transform);
            BuildZone(k, env.transform);
            BuildBench(k, station.transform, adjustable.transform);
            RestyleKit(k, adjustable.transform);
            BuildLighting(env.transform);
            ConfigureRendering(env.transform);

            var scene = env.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            if (save) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[De Toutes Pièces] Refonte visuelle de l'atelier appliquée.");
            if (bake && save) Bake();
            return true;
        }

        // =====================================================================
        // Pièce
        // =====================================================================

        private static void BuildRoom(AmbianceKit k, Transform env)
        {
            var room = AmbianceKit.Group("Pièce", env, Vector3.zero);
            float zc = (ZF + ZB) * 0.5f, depth = ZF - ZB;

            // Sol, plafond et murs découpés en dalles : chaque dalle reçoit l'éclairage précalculé
            // de la sonde de lumière la plus proche (dégradés), et la largeur d'une dalle est un multiple
            // de la période de sa texture pour que le motif reste continu.
            const float parquetTile = 1.92f, plasterTile = 2f;
            var floorGroup = AmbianceKit.Group("Sol (parquet chêne)", room, Vector3.zero);
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    var tile = k.Box("Lame de parquet", floorGroup, new Vector3(-3.84f + parquetTile * (i + 0.5f), -0.05f, -3.7f + parquetTile * (j + 0.5f)), new Vector3(parquetTile, 0.1f, parquetTile), k.Parquet, 0f);
                    tile.AddComponent<BoxCollider>();
                }
            // Plafond d'un seul tenant : un éclairage uniforme y est plus naturel qu'un damier de dalles.
            k.Box("Plafond", room, new Vector3(0, H + 0.15f, zc), new Vector3(2 * X + 2 * T, 0.3f, depth + 2 * T), k.Ceiling, 0f);

            var front = AmbianceKit.Group("Mur avant", room, Vector3.zero);
            var back = AmbianceKit.Group("Mur arrière (teinte gris bleu)", room, Vector3.zero);
            for (int i = 0; i < 4; i++)
            {
                float x = -4f + plasterTile * (i + 0.5f);
                k.Box("Pan de mur", front, new Vector3(x, H / 2, ZF + T / 2), new Vector3(plasterTile, H, T), k.Wall, 0f);
                k.Box("Pan de mur", back, new Vector3(x, H / 2, ZB - T / 2), new Vector3(plasterTile, H, T), k.WallAccent, 0f);
            }
            var right = AmbianceKit.Group("Mur droit", room, Vector3.zero);
            for (int j = 0; j < 3; j++)
                k.Box("Pan de mur", right, new Vector3(X + T / 2, H / 2, ZB + plasterTile * (j + 0.5f)), new Vector3(T, H, plasterTile), k.Wall, 0f);

            // Mur gauche percé de deux fenêtres.
            var left = AmbianceKit.Group("Mur gauche", room, Vector3.zero);
            float lx = -X - T / 2;
            float prev = ZB;
            foreach (var w in Windows)
            {
                k.Box("Trumeau", left, new Vector3(lx, H / 2, (prev + w.x) / 2), new Vector3(T, H, w.x - prev), k.Wall, 0f);
                k.Box("Allège", left, new Vector3(lx, SillY / 2, (w.x + w.y) / 2), new Vector3(T, SillY, w.y - w.x), k.Wall, 0f);
                k.Box("Linteau", left, new Vector3(lx, (HeadY + H) / 2, (w.x + w.y) / 2), new Vector3(T, H - HeadY, w.y - w.x), k.Wall, 0f);
                prev = w.y;
            }
            k.Box("Trumeau", left, new Vector3(lx, H / 2, (prev + ZF) / 2), new Vector3(T, H, ZF - prev), k.Wall, 0f);

            // Enveloppe opaque des deux côtés pour les ombres du soleil (évite les fuites de lumière).
            foreach (var r in room.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.TwoSided;

            // Plinthes et corniche.
            var trim = AmbianceKit.Group("Plinthes et corniches", room, Vector3.zero);
            foreach (float y in new[] { 0.035f, H - 0.0175f })
            {
                bool skirting = y < 1f;
                var s = skirting ? new Vector2(0.07f, 0.015f) : new Vector2(0.035f, 0.035f);
                string n = skirting ? "Plinthe" : "Corniche";
                k.Box(n, trim, new Vector3(0, y, ZF - s.y / 2), new Vector3(2 * X, s.x, s.y), k.WhiteLacquer, 0.002f);
                k.Box(n, trim, new Vector3(0, y, ZB + s.y / 2), new Vector3(2 * X, s.x, s.y), k.WhiteLacquer, 0.002f);
                k.Box(n, trim, new Vector3(X - s.y / 2, y, zc), new Vector3(s.y, s.x, depth), k.WhiteLacquer, 0.002f);
                k.Box(n, trim, new Vector3(-X + s.y / 2, y, zc), new Vector3(s.y, s.x, depth), k.WhiteLacquer, 0.002f);
            }
        }

        private static void BuildWindows(AmbianceKit k, Transform env)
        {
            var g = AmbianceKit.Group("Fenêtres", env, Vector3.zero);
            float xf = -X - 0.15f;
            int i = 1;
            foreach (var w in Windows)
            {
                var f = AmbianceKit.Group($"Fenêtre {i++}", g, Vector3.zero);
                float zc = (w.x + w.y) / 2, width = w.y - w.x, mid = (SillY + HeadY) / 2, hh = HeadY - SillY;
                k.Box("Montant", f, new Vector3(xf, mid, w.x + 0.025f), new Vector3(0.07f, hh, 0.05f), k.BlackMetal, 0.003f);
                k.Box("Montant", f, new Vector3(xf, mid, w.y - 0.025f), new Vector3(0.07f, hh, 0.05f), k.BlackMetal, 0.003f);
                k.Box("Traverse haute", f, new Vector3(xf, HeadY - 0.025f, zc), new Vector3(0.07f, 0.05f, width), k.BlackMetal, 0.003f);
                k.Box("Traverse basse", f, new Vector3(xf, SillY + 0.025f, zc), new Vector3(0.07f, 0.05f, width), k.BlackMetal, 0.003f);
                k.Box("Meneau", f, new Vector3(xf, mid, zc), new Vector3(0.06f, hh, 0.04f), k.BlackMetal, 0.003f);
                k.Box("Imposte", f, new Vector3(xf, 2.05f, zc), new Vector3(0.06f, 0.035f, width), k.BlackMetal, 0.003f);
                var glass = k.Box("Vitrage", f, new Vector3(xf, mid, zc), new Vector3(0.008f, hh - 0.06f, width - 0.06f), k.Glass, 0f, false);
                GameObjectUtility.SetStaticEditorFlags(glass, StaticEditorFlags.BatchingStatic);
                glass.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
                k.Box("Appui intérieur (chêne)", f, new Vector3(-X - 0.04f, SillY - 0.015f, zc), new Vector3(0.2f, 0.03f, width + 0.12f), k.Oak, 0.004f);

                // Lumière du ciel : source surfacique précalculée (aucun coût à l'exécution).
                var sky = new GameObject("Lumière du ciel (précalculée)").AddComponent<Light>();
                sky.transform.SetParent(f, false);
                sky.transform.position = new Vector3(-X - 0.02f, mid, zc);
                sky.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                sky.type = LightType.Rectangle;
                sky.areaSize = new Vector2(width - 0.1f, hh - 0.1f);
                sky.color = new Color(0.86f, 0.92f, 1f);
                sky.intensity = 1.1f;
                sky.lightmapBakeType = LightmapBakeType.Baked;
            }

            // Vue extérieure : un simple quad non éclairé loin derrière les vitres.
            var view = GameObject.CreatePrimitive(PrimitiveType.Quad);
            view.name = "Vue extérieure";
            Object.DestroyImmediate(view.GetComponent<Collider>());
            view.transform.SetParent(g, false);
            view.transform.position = new Vector3(-X - 4f, 1.5f, -0.1f);
            view.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            view.transform.localScale = new Vector3(14f, 7f, 1f);
            var vr = view.GetComponent<MeshRenderer>();
            vr.sharedMaterial = k.Outside;
            vr.shadowCastingMode = ShadowCastingMode.Off;
            vr.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(view, StaticEditorFlags.BatchingStatic);
        }

        // Repères muraux : origine au nu du mur, +Z local vers la pièce.
        private static Transform FrontGroup(string n, Transform p, float worldX) => AmbianceKit.Group(n, p, new Vector3(worldX, 0, ZF), 180f);
        private static Transform BackGroup(string n, Transform p, float worldX) => AmbianceKit.Group(n, p, new Vector3(worldX, 0, ZB), 0f);
        private static Transform RightGroup(string n, Transform p, float worldZ) => AmbianceKit.Group(n, p, new Vector3(X, 0, worldZ), -90f);
        private static Transform LeftGroup(string n, Transform p, float worldZ) => AmbianceKit.Group(n, p, new Vector3(-X, 0, worldZ), 90f);

        // =====================================================================
        // Mur avant : claustra en chêne, enseigne, armoire, affiche, outillage
        // =====================================================================

        private static void BuildFrontWall(AmbianceKit k, Transform env)
        {
            var wall = AmbianceKit.Group("Mur avant (habillage)", env, Vector3.zero);

            // Claustra : tasseaux de chêne sur feutre anthracite, derrière l'établi.
            var slats = FrontGroup("Claustra chêne", wall, 0f);
            k.Box("Fond feutre", slats, new Vector3(0, 1.2f, 0.01f), new Vector3(3.24f, 2.3f, 0.02f), k.Felt, 0.002f);
            int n = 0;
            for (float x = -1.6f + 0.0135f; x < 1.6f; x += 0.045f, n++)
                k.Box("Tasseau", slats, new Vector3(x, 1.2f, 0.03f), new Vector3(2.28f, 0.027f, 0.02f), k.OakSlat, 0.002f,
                    true, Quaternion.Euler(n % 2 == 0 ? 0f : 180f, 0f, 90f));
            k.Box("Couvre-joint haut", slats, new Vector3(0, 2.356f, 0.022f), new Vector3(3.24f, 0.012f, 0.044f), k.OakSlat, 0.003f);

            // Enseigne : pictogramme « tête six pans » + nom de l'application.
            var sign = FrontGroup("Enseigne De Toutes Pièces", wall, 0f);
            k.Box("Plaque logo", sign, new Vector3(0.98f, 2.6f, 0.008f), new Vector3(0.15f, 0.15f, 0.016f), k.Blue, 0.024f);
            var hexRot = Quaternion.Euler(0f, 0f, 30f) * Quaternion.Euler(90f, 0f, 0f);
            k.Cyl("Six pans", sign, new Vector3(0.98f, 2.6f, 0.019f), 0.09f, 0.006f, k.Yellow, hexRot, 6, true, false);
            k.Cyl("Empreinte", sign, new Vector3(0.98f, 2.6f, 0.0215f), 0.038f, 0.002f, k.Blue, hexRot, 6, false, false);
            AmbianceKit.Text("DE TOUTES PIÈCES", sign, new Vector3(-0.17f, 2.625f, 0.004f), TextRot, 0.068f, Ink, FontStyles.Bold, TextAlignmentOptions.Left, 2f, 10f);
            AmbianceKit.Text("ATELIER D'ASSEMBLAGE  ·  MONTAGE GUIDÉ", sign, new Vector3(-0.17f, 2.552f, 0.004f), TextRot, 0.026f, IkeaBlue, FontStyles.Bold, TextAlignmentOptions.Left, 2f, 18f);

            // Armoire haute (caisson chêne, portes laquées blanches).
            var cab = FrontGroup("Armoire de rangement", wall, -2.9f);
            k.Box("Caisson", cab, new Vector3(0, 1.04f, 0.225f), new Vector3(0.8f, 1.92f, 0.45f), k.Oak, 0.004f);
            k.Box("Socle", cab, new Vector3(0, 0.04f, 0.21f), new Vector3(0.74f, 0.08f, 0.4f), k.BlackMetal, 0.002f);
            foreach (float s in new[] { -1f, 1f })
            {
                k.Box("Porte", cab, new Vector3(0.2f * s, 1.04f, 0.459f), new Vector3(0.392f, 1.9f, 0.018f), k.WhiteLacquer, 0.003f);
                k.Box("Poignée", cab, new Vector3(0.035f * s, 1.05f, 0.478f), new Vector3(0.012f, 0.24f, 0.012f), k.BlackMetal, 0.004f);
            }

            // Affiche « Bonnes pratiques ».
            var poster = FrontGroup("Affiche bonnes pratiques", wall, -2.05f);
            k.Box("Cadre", poster, new Vector3(0, 1.5f, 0.012f), new Vector3(0.5f, 0.68f, 0.02f), k.BlackMetal, 0.002f);
            k.Box("Passe-partout", poster, new Vector3(0, 1.5f, 0.023f), new Vector3(0.46f, 0.64f, 0.003f), k.Paper, 0f);
            float pz = 0.0255f;
            AmbianceKit.Text("BONNES PRATIQUES", poster, new Vector3(0, 1.74f, pz), TextRot, 0.03f, IkeaBlue, FontStyles.Bold, TextAlignmentOptions.Center, 0.42f, 6f);
            k.Box("Filet jaune", poster, new Vector3(0, 1.705f, pz), new Vector3(0.12f, 0.006f, 0.0008f), k.Yellow, 0f, false);
            string[] lines = { "1   Dégagez le plan de travail", "2   Vérifiez le contenu du kit", "3   Suivez la notice pas à pas", "4   Serrez sans forcer", "5   Rangez les outils après usage" };
            for (int i = 0; i < lines.Length; i++)
                AmbianceKit.Text(lines[i], poster, new Vector3(0.0f, 1.63f - i * 0.075f, pz), TextRot, 0.02f, Ink, FontStyles.Normal, TextAlignmentOptions.Left, 0.36f);
            AmbianceKit.Text("De Toutes Pièces VR", poster, new Vector3(0, 1.23f, pz), TextRot, 0.013f, InkSoft, FontStyles.Normal, TextAlignmentOptions.Center, 0.4f, 4f);

            BuildToolWall(k, wall);
        }

        private static void BuildToolWall(AmbianceKit k, Transform wall)
        {
            // Meuble bas à tiroirs.
            var unit = FrontGroup("Meuble bas à tiroirs", wall, 2.5f);
            k.Box("Socle", unit, new Vector3(0, 0.04f, 0.24f), new Vector3(1.26f, 0.08f, 0.46f), k.BlackMetal, 0.002f);
            k.Box("Caisson", unit, new Vector3(0, 0.465f, 0.25f), new Vector3(1.3f, 0.77f, 0.5f), k.WhiteLacquer, 0.003f);
            foreach (float y in new[] { 0.208f, 0.465f, 0.722f })
                foreach (float s in new[] { -1f, 1f })
                {
                    k.Box("Façade de tiroir", unit, new Vector3(0.3275f * s, y, 0.509f), new Vector3(0.641f, 0.248f, 0.018f), k.WhiteLacquer, 0.003f);
                    k.Box("Poignée", unit, new Vector3(0.3275f * s, y + 0.085f, 0.526f), new Vector3(0.16f, 0.012f, 0.012f), k.BlackMetal, 0.004f);
                }
            k.Box("Plateau chêne", unit, new Vector3(0, 0.865f, 0.255f), new Vector3(1.34f, 0.03f, 0.52f), k.Oak, 0.004f);

            // Sur le meuble : notices, mallette, visseuse.
            float top = 0.88f;
            for (int i = 0; i < 4; i++)
                k.Box("Notice", unit, new Vector3(0.45f, top + 0.004f + i * 0.0085f, 0.27f), new Vector3(0.21f, 0.008f, 0.297f), k.Paper, 0.001f, true, Quaternion.Euler(0, (i * 7) % 11 - 5, 0));
            k.Box("Bandeau bleu", unit, new Vector3(0.45f, top + 0.0345f, 0.21f), new Vector3(0.21f, 0.0006f, 0.05f), k.Blue, 0f, false, Quaternion.Euler(0, 1, 0));
            var box = AmbianceKit.Group("Mallette", unit, new Vector3(-0.05f, top, 0.26f), 8f);
            k.Box("Coque", box, new Vector3(0, 0.04f, 0), new Vector3(0.36f, 0.08f, 0.25f), k.BluePlastic, 0.014f);
            k.Box("Poignée", box, new Vector3(0, 0.088f, 0), new Vector3(0.12f, 0.014f, 0.022f), k.BlackPlastic, 0.006f);
            foreach (float s in new[] { -1f, 1f })
                k.Box("Fermoir", box, new Vector3(0.11f * s, 0.05f, 0.128f), new Vector3(0.035f, 0.025f, 0.01f), k.YellowPlastic, 0.004f);
            var drill = AmbianceKit.Group("Visseuse", unit, new Vector3(-0.45f, top, 0.25f), 200f);
            k.Box("Batterie", drill, new Vector3(0, 0.03f, 0), new Vector3(0.08f, 0.06f, 0.11f), k.BlackPlastic, 0.01f);
            k.Box("Poignée", drill, new Vector3(0, 0.12f, 0.01f), new Vector3(0.045f, 0.13f, 0.06f), k.YellowPlastic, 0.018f, true, Quaternion.Euler(-12f, 0, 0));
            k.Box("Corps", drill, new Vector3(0, 0.205f, 0.045f), new Vector3(0.07f, 0.075f, 0.18f), k.YellowPlastic, 0.028f);
            k.Cyl("Mandrin", drill, new Vector3(0, 0.205f, 0.16f), 0.032f, 0.05f, k.BlackPlastic, Quaternion.Euler(90f, 0, 0));
            k.Cyl("Embout", drill, new Vector3(0, 0.205f, 0.205f), 0.006f, 0.045f, k.Chrome, Quaternion.Euler(90f, 0, 0), 6, true, false);

            // Panneau perforé encadré de chêne, avec outils suspendus.
            var peg = FrontGroup("Panneau perforé et outils", wall, 2.5f);
            k.Box("Panneau perforé", peg, new Vector3(0, 1.5f, 0.022f), new Vector3(1.2f, 0.86f, 0.012f), k.Pegboard, 0.002f);
            foreach (float s in new[] { -1f, 1f })
            {
                k.Box("Cadre", peg, new Vector3(0, 1.5f + 0.445f * s, 0.016f), new Vector3(1.24f, 0.03f, 0.032f), k.Oak, 0.003f);
                k.Box("Cadre", peg, new Vector3(0.605f * s, 1.5f, 0.016f), new Vector3(0.03f, 0.89f, 0.032f), k.Oak, 0.003f);
            }
            const float face = 0.028f;
            void Hook(float x, float y) => k.Cyl("Crochet", peg, new Vector3(x, y, face + 0.02f), 0.005f, 0.04f, k.Chrome, Quaternion.Euler(90f, 0, 0), 8, false);

            // Marteau.
            k.Box("Tête de marteau", peg, new Vector3(0.42f, 1.82f, face + 0.03f), new Vector3(0.11f, 0.028f, 0.028f), k.BlackMetal, 0.004f);
            k.Box("Manche de marteau", peg, new Vector3(0.42f, 1.67f, face + 0.03f), new Vector3(0.028f, 0.28f, 0.022f), k.Oak, 0.009f, true, Quaternion.Euler(0, 0, 90f));
            Hook(0.39f, 1.80f); Hook(0.45f, 1.80f);
            // Tournevis sur leur râtelier.
            k.Box("Râtelier", peg, new Vector3(0.06f, 1.77f, face + 0.02f), new Vector3(0.28f, 0.016f, 0.04f), k.BlackMetal, 0.003f);
            Material[] grips = { k.YellowPlastic, k.BluePlastic, k.BlackPlastic, k.YellowPlastic, k.BluePlastic };
            for (int i = 0; i < 5; i++)
            {
                float x = -0.05f + i * 0.055f;
                k.Cyl("Manche de tournevis", peg, new Vector3(x, 1.83f, face + 0.025f), 0.028f, 0.1f, grips[i], null, 12);
                k.Cyl("Tige", peg, new Vector3(x, 1.71f, face + 0.025f), 0.006f, 0.12f + (i % 3) * 0.03f, k.Chrome, null, 8, false);
            }
            // Pince.
            foreach (float s in new[] { -1f, 1f })
                k.Box("Branche de pince", peg, new Vector3(-0.2f + 0.018f * s, 1.69f, face + 0.02f), new Vector3(0.018f, 0.13f, 0.01f), k.BluePlastic, 0.005f, true, Quaternion.Euler(0, 0, 7f * s));
            k.Box("Mors de pince", peg, new Vector3(-0.2f, 1.785f, face + 0.02f), new Vector3(0.03f, 0.06f, 0.012f), k.BlackMetal, 0.004f);
            Hook(-0.2f, 1.83f);
            // Mètre ruban.
            k.Box("Mètre ruban", peg, new Vector3(-0.42f, 1.79f, face + 0.03f), new Vector3(0.075f, 0.075f, 0.04f), k.YellowPlastic, 0.014f);
            k.Cyl("Enjoliveur", peg, new Vector3(-0.42f, 1.79f, face + 0.051f), 0.045f, 0.002f, k.BlackPlastic, Quaternion.Euler(90f, 0, 0));
            Hook(-0.42f, 1.84f);
            // Niveau à bulle.
            k.Box("Niveau à bulle", peg, new Vector3(0.18f, 1.56f, face + 0.02f), new Vector3(0.6f, 0.05f, 0.025f), k.Aluminium, 0.003f);
            foreach (float x in new[] { -0.29f, 0.29f })
                k.Box("Embout", peg, new Vector3(0.18f + x, 1.56f, face + 0.02f), new Vector3(0.024f, 0.054f, 0.029f), k.BlackPlastic, 0.004f);
            k.Box("Fiole", peg, new Vector3(0.18f, 1.56f, face + 0.033f), new Vector3(0.05f, 0.02f, 0.002f), k.Yellow, 0f, false);
            Hook(-0.02f, 1.52f); Hook(0.38f, 1.52f);
            // Équerre.
            k.Box("Équerre (lame)", peg, new Vector3(-0.38f, 1.52f, face + 0.012f), new Vector3(0.3f, 0.04f, 0.004f), k.Aluminium, 0.001f);
            k.Box("Équerre (talon)", peg, new Vector3(-0.51f, 1.43f, face + 0.016f), new Vector3(0.04f, 0.2f, 0.012f), k.Oak, 0.002f);
            // Bacs de rangement suspendus (vides : aucune visserie hors kit n'est représentée).
            k.Box("Rail", peg, new Vector3(0f, 1.21f, face + 0.008f), new Vector3(0.9f, 0.025f, 0.012f), k.BlackMetal, 0.002f);
            for (int i = 0; i < 6; i++)
            {
                var bin = AmbianceKit.Group("Bac", peg, new Vector3(-0.375f + i * 0.15f, 1.12f, face + 0.012f));
                var m = i == 2 ? k.YellowPlastic : k.BluePlastic;
                k.Box("Dos", bin, new Vector3(0, 0.05f, 0.003f), new Vector3(0.12f, 0.1f, 0.006f), m, 0.002f);
                k.Box("Fond", bin, new Vector3(0, 0.003f, 0.065f), new Vector3(0.12f, 0.006f, 0.13f), m, 0.002f);
                foreach (float s in new[] { -1f, 1f })
                    k.Box("Flanc", bin, new Vector3(0.057f * s, 0.04f, 0.065f), new Vector3(0.006f, 0.08f, 0.13f), m, 0.002f);
                k.Box("Façade", bin, new Vector3(0, 0.025f, 0.127f), new Vector3(0.12f, 0.05f, 0.006f), m, 0.002f);
            }

            var plate = FrontGroup("Signalétique Outillage", wall, 2.5f);
            Plaque(k, plate, new Vector3(0, 2.07f, 0f), "OUTILLAGE", 0.36f);
        }

        private static void Plaque(AmbianceKit k, Transform p, Vector3 pos, string text, float width)
        {
            k.Box("Plaque", p, pos + new Vector3(0, 0, 0.006f), new Vector3(width, 0.085f, 0.012f), k.Blue, 0.012f);
            k.Box("Filet jaune", p, pos + new Vector3(-width / 2 + 0.03f, 0, 0.0125f), new Vector3(0.008f, 0.045f, 0.001f), k.Yellow, 0f, false);
            AmbianceKit.Text(text, p, pos + new Vector3(0, -0.002f, 0.0135f), TextRot, 0.033f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center, width, 12f);
        }

        // =====================================================================
        // Mur droit : rayonnage de stockage des kits
        // =====================================================================

        private static void BuildRightWall(AmbianceKit k, Transform env)
        {
            var g = RightGroup("Rayonnage de stockage des kits", env, 0f);
            float[] posts = { -2.8f, -1.6f, -0.4f };
            float[] shelves = { 0.12f, 0.6f, 1.08f, 1.56f, 2.0f };
            foreach (float x in posts)
                foreach (float d in new[] { 0.02f, 0.43f })
                    k.Box("Montant", g, new Vector3(x, 1.01f, d), new Vector3(0.03f, 2.02f, 0.03f), k.BlackMetal, 0.003f);
            for (int b = 0; b < 2; b++)
            {
                float xc = (posts[b] + posts[b + 1]) / 2;
                foreach (float y in shelves)
                    k.Box("Tablette bouleau", g, new Vector3(xc, y, 0.225f), new Vector3(1.17f, 0.025f, 0.44f), k.Birch, 0.003f);
                foreach (float s in new[] { -1f, 1f })
                    k.Box("Croisillon", g, new Vector3(xc, 1.06f, 0.008f), new Vector3(2.23f, 0.008f, 0.004f), k.BlackMetal, 0f, true, Quaternion.Euler(0, 0, 57.4f * s));
            }

            // Cartons de meubles en kit (noms génériques, aucune marque).
            string[] names = { "Table de chevet", "Étagère murale", "Banc d'entrée", "Table basse", "Commode 3 tiroirs", "Bureau compact", "Tabouret", "Console", "Desserte", "Meuble TV" };
            var rnd = new System.Random(7);
            int label = 0;
            for (int b = 0; b < 2; b++)
            {
                float x0 = posts[b] + 0.04f, x1 = posts[b + 1] - 0.04f;
                for (int s = 0; s < shelves.Length - 1; s++)
                {
                    float y = shelves[s] + 0.0125f;
                    float x = x0;
                    while (x < x1 - 0.25f)
                    {
                        float len = Mathf.Min(0.35f + (float)rnd.NextDouble() * 0.5f, x1 - x);
                        int stack = 1 + rnd.Next(3);
                        float yy = y;
                        bool blueBox = rnd.NextDouble() < 0.18;
                        for (int i = 0; i < stack && yy < shelves[s + 1] - 0.18f; i++)
                        {
                            float hgt = blueBox ? 0.22f : 0.06f + (float)rnd.NextDouble() * 0.09f;
                            float dep = blueBox ? 0.36f : 0.32f + (float)rnd.NextDouble() * 0.08f;
                            float w = blueBox ? Mathf.Min(0.4f, len) : len - i * 0.03f;
                            var c = AmbianceKit.Group(blueBox ? "Boîte de rangement" : "Carton de meuble", g,
                                new Vector3(x + w / 2, yy, 0.03f + dep / 2), (float)(rnd.NextDouble() * 4 - 2));
                            k.Box("Volume", c, new Vector3(0, hgt / 2, 0), new Vector3(w, hgt, dep), blueBox ? k.BluePlastic : k.Cardboard, blueBox ? 0.012f : 0.004f);
                            if (!blueBox && label < names.Length && i == stack - 1 || (!blueBox && i == 0 && rnd.NextDouble() < 0.5 && label < names.Length))
                            {
                                k.Box("Étiquette", c, new Vector3(-w / 2 + 0.09f, hgt / 2, dep / 2 + 0.0006f), new Vector3(0.14f, Mathf.Min(0.05f, hgt - 0.015f), 0.001f), k.Paper, 0f, false);
                                AmbianceKit.Text(names[label % names.Length], c, new Vector3(-w / 2 + 0.09f, hgt / 2, dep / 2 + 0.0013f), TextRot, 0.014f, Ink, FontStyles.Bold, TextAlignmentOptions.Center, 0.13f);
                                label++;
                            }
                            yy += hgt;
                            if (blueBox) break;
                        }
                        x += (blueBox ? Mathf.Min(0.4f, len) : len) + 0.03f;
                    }
                }
            }
            Plaque(k, g, new Vector3(-1.6f, 2.25f, 0f), "STOCKAGE DES KITS", 0.5f);
        }

        // =====================================================================
        // Mur arrière : porte, rangement à cases, panneaux acoustiques
        // =====================================================================

        private static void BuildBackWall(AmbianceKit k, Transform env)
        {
            var door = BackGroup("Porte", env, 2.2f);
            k.Box("Vantail chêne", door, new Vector3(0, 1.03f, 0.012f), new Vector3(0.9f, 2.05f, 0.04f), k.Oak, 0.003f);
            foreach (float s in new[] { -1f, 1f })
                k.Box("Chambranle", door, new Vector3(0.49f * s, 1.07f, 0.015f), new Vector3(0.07f, 2.14f, 0.03f), k.WhiteLacquer, 0.003f);
            k.Box("Chambranle", door, new Vector3(0, 2.135f, 0.015f), new Vector3(1.05f, 0.07f, 0.03f), k.WhiteLacquer, 0.003f);
            k.Cyl("Rosace", door, new Vector3(-0.38f, 1.0f, 0.036f), 0.05f, 0.01f, k.BlackMetal, Quaternion.Euler(90f, 0, 0));
            k.Box("Béquille", door, new Vector3(-0.33f, 1.0f, 0.055f), new Vector3(0.12f, 0.018f, 0.018f), k.BlackMetal, 0.008f);
            k.Box("Plaque sortie", door, new Vector3(0, 2.32f, 0.006f), new Vector3(0.28f, 0.075f, 0.012f), k.Paper, 0.01f);
            AmbianceKit.Text("SORTIE", door, new Vector3(0, 2.318f, 0.0125f), TextRot, 0.03f, new Color(0.1f, 0.5f, 0.28f), FontStyles.Bold, TextAlignmentOptions.Center, 0.26f, 14f);

            // Rangement à cases 4 × 2.
            var cub = BackGroup("Rangement à cases", env, -1.7f);
            const float cell = 0.335f, th = 0.035f, dep = 0.39f;
            float w = 4 * cell + 5 * th, hh = 2 * cell + 3 * th;
            for (int i = 0; i < 3; i++)
                k.Box("Tablette", cub, new Vector3(0, 0.03f + th / 2 + i * (cell + th), dep / 2), new Vector3(w, th, dep), k.WhiteLacquer, 0.003f);
            for (int j = 0; j < 5; j++)
                k.Box("Séparation", cub, new Vector3(-w / 2 + th / 2 + j * (cell + th), 0.03f + hh / 2, dep / 2), new Vector3(th, hh, dep), k.WhiteLacquer, 0.003f);
            k.Box("Socle", cub, new Vector3(0, 0.015f, dep / 2), new Vector3(w - 0.06f, 0.03f, dep - 0.06f), k.BlackMetal, 0.002f);
            Material[] fabric = { k.FeltBlue, k.FeltSand, null, k.Felt, k.FeltSand, null, k.FeltBlue, k.Cardboard };
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 4; c++)
                {
                    int idx = r * 4 + c;
                    float cx = -w / 2 + th + cell / 2 + c * (cell + th);
                    float cy = 0.03f + th + r * (cell + th);
                    var m = fabric[idx];
                    if (m != null)
                    {
                        k.Box("Boîte tissu", cub, new Vector3(cx, cy + 0.155f, dep / 2 + 0.005f), new Vector3(0.315f, 0.31f, 0.37f), m, 0.012f);
                        if (m != k.Cardboard)
                            k.Box("Poignée découpée", cub, new Vector3(cx, cy + 0.25f, dep / 2 + 0.191f), new Vector3(0.09f, 0.025f, 0.002f), k.Charcoal, 0.01f, false);
                    }
                    else if (idx == 2)
                    {
                        Material[] spines = { k.Blue, k.Paper, k.Charcoal, k.Yellow, k.Paper, k.Blue };
                        for (int b = 0; b < 6; b++)
                            k.Box("Classeur", cub, new Vector3(cx - 0.13f + b * 0.05f, cy + 0.15f, dep / 2), new Vector3(0.045f, 0.3f, 0.3f), spines[b], 0.003f);
                    }
                    else
                    {
                        k.Cyl("Vase", cub, new Vector3(cx, cy + 0.1f, dep / 2), 0.12f, 0.2f, k.Ceramic, null, 24);
                        for (int b = 0; b < 3; b++)
                            k.Box("Livre", cub, new Vector3(cx + 0.11f, cy + 0.012f + b * 0.024f, dep / 2), new Vector3(0.1f, 0.022f, 0.15f), b == 1 ? k.Blue : k.Paper, 0.002f);
                    }
                }
            Plant(k, cub, new Vector3(0.5f, 0.03f + hh, dep / 2), 0.7f, 3);
            for (int b = 0; b < 2; b++)
                k.Box("Carton", cub, new Vector3(-0.4f, 0.03f + hh + 0.06f + b * 0.12f, dep / 2), new Vector3(0.45f - b * 0.08f, 0.12f, 0.33f), k.Cardboard, 0.004f);

            // Panneaux acoustiques en feutre.
            var felt = BackGroup("Panneaux acoustiques", env, 0.45f);
            k.Box("Panneau feutre bleu", felt, new Vector3(-0.32f, 1.5f, 0.012f), new Vector3(0.5f, 0.9f, 0.024f), k.FeltBlue, 0.03f);
            k.Box("Panneau feutre sable", felt, new Vector3(0.25f, 1.72f, 0.012f), new Vector3(0.5f, 0.9f, 0.024f), k.FeltSand, 0.03f);
            k.Box("Panneau feutre anthracite", felt, new Vector3(0.82f, 1.42f, 0.012f), new Vector3(0.5f, 0.9f, 0.024f), k.Felt, 0.03f);

            Plant(k, AmbianceKit.Group("Plante (angle)", env, new Vector3(-3.1f, 0f, -3.1f)), Vector3.zero, 1f, 11);
        }

        /// <summary>Sansevieria : pot en céramique et feuilles verticales.</summary>
        private static void Plant(AmbianceKit k, Transform parent, Vector3 pos, float scale, int seed)
        {
            var g = AmbianceKit.Group("Plante", parent, pos);
            g.localScale = Vector3.one * scale;
            k.Cyl("Pot", g, new Vector3(0, 0.15f, 0), 0.26f, 0.3f, k.Ceramic, null, 24);
            k.Cyl("Terreau", g, new Vector3(0, 0.292f, 0), 0.235f, 0.01f, k.Soil, null, 16, false);
            var rnd = new System.Random(seed);
            for (int i = 0; i < 11; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = (float)rnd.NextDouble() * 0.07f;
                float h = 0.45f + (float)rnd.NextDouble() * 0.4f;
                var rot = Quaternion.Euler((float)rnd.NextDouble() * 12f - 6f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 12f - 6f);
                k.Sphere("Feuille", g, new Vector3(Mathf.Cos(a) * r, 0.29f + h * 0.45f, Mathf.Sin(a) * r), new Vector3(0.07f, h, 0.016f), i % 3 == 0 ? k.LeafLight : k.Leaf, rot);
            }
        }

        // =====================================================================
        // Mur gauche : buffet bas, radiateur, horloge
        // =====================================================================

        private static void BuildLeftWall(AmbianceKit k, Transform env)
        {
            var low = LeftGroup("Buffet bas", env, -1.2f);
            k.Box("Caisson", low, new Vector3(0, 0.26f, 0.2f), new Vector3(1.5f, 0.38f, 0.4f), k.WhiteLacquer, 0.003f);
            k.Box("Plateau chêne", low, new Vector3(0, 0.465f, 0.205f), new Vector3(1.52f, 0.03f, 0.42f), k.Oak, 0.004f);
            foreach (float x in new[] { -0.7f, 0.7f })
                foreach (float d in new[] { 0.05f, 0.35f })
                    k.Cyl("Pied", low, new Vector3(x, 0.035f, d), 0.03f, 0.07f, k.BlackMetal, null, 12);
            for (int i = -1; i <= 1; i++)
            {
                k.Box("Façade", low, new Vector3(i * 0.5f, 0.26f, 0.408f), new Vector3(0.495f, 0.37f, 0.016f), k.WhiteLacquer, 0.003f);
                k.Box("Poignée chêne", low, new Vector3(i * 0.5f, 0.41f, 0.422f), new Vector3(0.12f, 0.018f, 0.014f), k.Oak, 0.005f);
            }
            Plant(k, low, new Vector3(-0.5f, 0.48f, 0.2f), 0.6f, 5);
            for (int i = 0; i < 3; i++)
                k.Box("Notice", low, new Vector3(0.35f, 0.484f + i * 0.0085f, 0.2f), new Vector3(0.21f, 0.008f, 0.297f), i == 2 ? k.Blue : k.Paper, 0.001f, true, Quaternion.Euler(0, 6 * i - 4, 0));

            var rad = LeftGroup("Radiateur", env, 1.0f);
            k.Box("Corps", rad, new Vector3(0, 0.42f, 0.06f), new Vector3(1.0f, 0.5f, 0.04f), k.WhiteLacquer, 0.004f);
            for (int i = 0; i < 24; i++)
                k.Box("Ailette", rad, new Vector3(-0.48f + i * 0.04174f, 0.42f, 0.085f), new Vector3(0.012f, 0.48f, 0.012f), k.WhiteLacquer, 0.003f);
            foreach (float s in new[] { -1f, 1f })
                k.Cyl("Raccord", rad, new Vector3(0.45f * s, 0.12f, 0.06f), 0.018f, 0.12f, k.Chrome, null, 12);

            var clock = LeftGroup("Horloge", env, -0.1f);
            var axis = Quaternion.Euler(90f, 0, 0);
            k.Cyl("Lunette", clock, new Vector3(0, 2.05f, 0.02f), 0.34f, 0.04f, k.BlackMetal, axis, 40);
            k.Cyl("Cadran", clock, new Vector3(0, 2.05f, 0.041f), 0.31f, 0.002f, k.Paper, axis, 40, false);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                bool major = i % 3 == 0;
                k.Box("Index", clock, new Vector3(Mathf.Sin(a) * 0.13f, 2.05f + Mathf.Cos(a) * 0.13f, 0.0425f), new Vector3(major ? 0.008f : 0.004f, major ? 0.03f : 0.018f, 0.001f), k.Charcoal, 0f, false, Quaternion.Euler(0, 0, -i * 30f));
            }
            void Hand(float deg, float len, float wid)
            {
                float a = deg * Mathf.Deg2Rad;
                k.Box("Aiguille", clock, new Vector3(Mathf.Sin(a) * len * 0.4f, 2.05f + Mathf.Cos(a) * len * 0.4f, 0.045f), new Vector3(wid, len, 0.0015f), k.Charcoal, 0f, false, Quaternion.Euler(0, 0, -deg));
            }
            Hand(-60f, 0.08f, 0.009f);   // 10 h
            Hand(60f, 0.12f, 0.006f);    // 10
            k.Box("Trotteuse", clock, new Vector3(0, 2.05f - 0.04f, 0.0465f), new Vector3(0.002f, 0.13f, 0.001f), k.Yellow, 0f, false);
            k.Cyl("Axe", clock, new Vector3(0, 2.05f, 0.047f), 0.012f, 0.004f, k.Charcoal, axis, 12, false);
        }

        // =====================================================================
        // Desserte d'atelier
        // =====================================================================

        private static void BuildCart(AmbianceKit k, Transform env)
        {
            var c = AmbianceKit.Group("Desserte d'atelier", env, new Vector3(1.75f, 0f, 1.35f), -25f);
            foreach (float y in new[] { 0.12f, 0.45f, 0.78f })
            {
                k.Box("Plateau", c, new Vector3(0, y, 0), new Vector3(0.5f, 0.008f, 0.38f), k.Blue, 0.004f);
                foreach (float s in new[] { -1f, 1f })
                {
                    k.Box("Rebord", c, new Vector3(0, y + 0.035f, 0.186f * s), new Vector3(0.5f, 0.07f, 0.008f), k.Blue, 0.003f);
                    k.Box("Rebord", c, new Vector3(0.246f * s, y + 0.035f, 0), new Vector3(0.008f, 0.07f, 0.38f), k.Blue, 0.003f);
                }
            }
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    k.Cyl("Montant", c, new Vector3(0.235f * sx, 0.47f, 0.175f * sz), 0.018f, 0.8f, k.BlackMetal, null, 12);
                    k.Box("Chape", c, new Vector3(0.22f * sx, 0.06f, 0.16f * sz), new Vector3(0.03f, 0.03f, 0.03f), k.BlackMetal, 0.004f);
                    k.Cyl("Roulette", c, new Vector3(0.22f * sx, 0.026f, 0.16f * sz), 0.05f, 0.02f, k.BlackPlastic, Quaternion.Euler(0, 0, 90f), 16);
                }
            k.Cyl("Poignée", c, new Vector3(0.29f, 0.9f, 0), 0.02f, 0.38f, k.BlackMetal, Quaternion.Euler(90f, 0, 0), 12);
            foreach (float s in new[] { -1f, 1f })
                k.Box("Bras", c, new Vector3(0.265f, 0.9f, 0.175f * s), new Vector3(0.05f, 0.016f, 0.016f), k.BlackMetal, 0.004f);

            // Contenu.
            k.Cyl("Rouleau d'adhésif", c, new Vector3(-0.12f, 0.83f, -0.08f), 0.1f, 0.05f, k.FeltSand, null, 24);
            k.Box("Boîte grise", c, new Vector3(0.07f, 0.825f, 0.06f), new Vector3(0.22f, 0.07f, 0.16f), k.GreyPlastic, 0.008f);
            k.Box("Cutter", c, new Vector3(0.1f, 0.79f, -0.11f), new Vector3(0.15f, 0.018f, 0.03f), k.YellowPlastic, 0.006f, true, Quaternion.Euler(0, 20f, 0));
            for (int i = 0; i < 3; i++)
                k.Box("Notice", c, new Vector3(0f, 0.458f + i * 0.0085f, 0f), new Vector3(0.21f, 0.008f, 0.297f), k.Paper, 0.001f, true, Quaternion.Euler(0, 90 + i * 5, 0));
            k.Box("Carton", c, new Vector3(0f, 0.19f, 0f), new Vector3(0.4f, 0.14f, 0.3f), k.Cardboard, 0.004f);
        }

        // =====================================================================
        // Plafond : dalles LED et suspension au-dessus de l'établi
        // =====================================================================

        private static void BuildCeiling(AmbianceKit k, Transform env)
        {
            var g = AmbianceKit.Group("Luminaires", env, Vector3.zero);
            var panels = new[] { new Vector2(-1.9f, -1.9f), new Vector2(1.9f, -1.9f), new Vector2(0f, -1.9f), new Vector2(-1.9f, 0.7f), new Vector2(1.9f, 0.7f) };
            foreach (var p in panels)
            {
                var d = AmbianceKit.Group("Dalle LED", g, new Vector3(p.x, H, p.y));
                k.Box("Cadre", d, new Vector3(0, -0.006f, 0), new Vector3(0.64f, 0.012f, 0.64f), k.WhiteLacquer, 0.002f);
                k.Box("Diffuseur", d, new Vector3(0, -0.0125f, 0), new Vector3(0.6f, 0.002f, 0.6f), k.Led, 0f, false);
                var l = new GameObject("Lumière de la dalle (précalculée)").AddComponent<Light>();
                l.transform.SetParent(d, false);
                l.transform.localPosition = new Vector3(0, -0.02f, 0);
                l.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                l.type = LightType.Rectangle;
                l.areaSize = new Vector2(0.6f, 0.6f);
                l.color = new Color(1f, 0.95f, 0.88f);
                l.intensity = 1.6f;
                l.lightmapBakeType = LightmapBakeType.Baked;
            }

            // Suspension linéaire noire au-dessus du poste de montage.
            var s = AmbianceKit.Group("Suspension de l'établi", g, new Vector3(0, 0, 0.6f));
            const float y = 2.15f;
            foreach (float x in new[] { -0.45f, 0.45f })
            {
                k.Cyl("Patère", s, new Vector3(x, H - 0.008f, 0), 0.07f, 0.016f, k.BlackMetal, null, 20);
                k.Cyl("Câble", s, new Vector3(x, (H + y) / 2, 0), 0.003f, H - y, k.BlackMetal, null, 6, false);
            }
            k.Box("Corps", s, new Vector3(0, y, 0), new Vector3(1.25f, 0.045f, 0.09f), k.BlackMetal, 0.01f);
            k.Box("Diffuseur", s, new Vector3(0, y - 0.0235f, 0), new Vector3(1.2f, 0.003f, 0.06f), k.LedWarm, 0f, false);
            var spot = new GameObject("Éclairage du poste (temps réel)").AddComponent<Light>();
            spot.transform.SetParent(s, false);
            spot.transform.localPosition = new Vector3(0, y - 0.03f, 0.02f);
            spot.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            spot.type = LightType.Spot;
            spot.spotAngle = 85f;
            spot.innerSpotAngle = 50f;
            spot.range = 4f;
            spot.intensity = 1.3f;
            spot.color = new Color(1f, 0.93f, 0.84f);
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.7f;
            spot.lightmapBakeType = LightmapBakeType.Mixed;
        }

        // =====================================================================
        // Zone d'évolution (exigence du cahier des charges) : tapis anti-fatigue
        // =====================================================================

        private static void BuildZone(AmbianceKit k, Transform env)
        {
            var zone = AmbianceKit.Group("Zone d'évolution (2 m × 1,5 m)", env, Vector3.zero);
            float zMin = -1.3f, zMax = 0.2f, xHalf = 1f, zc = (zMin + zMax) / 2;
            k.Box("Tapis anti-fatigue", zone, new Vector3(0, 0.005f, zc), new Vector3(2 * xHalf, 0.01f, zMax - zMin), k.Rubber, 0.004f, false);
            float y = 0.0103f;
            k.Box("Liseré gauche", zone, new Vector3(-xHalf + 0.03f, y, zc), new Vector3(0.025f, 0.0006f, zMax - zMin - 0.035f), k.Yellow, 0f, false);
            k.Box("Liseré droit", zone, new Vector3(xHalf - 0.03f, y, zc), new Vector3(0.025f, 0.0006f, zMax - zMin - 0.035f), k.Yellow, 0f, false);
            k.Box("Liseré arrière", zone, new Vector3(0, y, zMin + 0.03f), new Vector3(2 * xHalf - 0.035f, 0.0006f, 0.025f), k.Yellow, 0f, false);
            k.Box("Liseré avant", zone, new Vector3(0, y, zMax - 0.03f), new Vector3(2 * xHalf - 0.035f, 0.0006f, 0.025f), k.Yellow, 0f, false);
            var t = AmbianceKit.Text("ZONE D'ÉVOLUTION  ·  2 m × 1,5 m", zone, new Vector3(0, 0.0108f, zMin + 0.12f), Quaternion.Euler(90f, 0, 0), 0.045f, new Color(0.72f, 0.74f, 0.76f), FontStyles.Bold, TextAlignmentOptions.Center, 1.6f, 10f);
            t.name = "Étiquette Zone d'évolution minimale : 2 m × 1,5 m";
        }

        // =====================================================================
        // Établi : plateau chêne arrondi, piètement télescopique, tapis, rangements
        // =====================================================================

        private static void BuildBench(AmbianceKit k, Transform station, Transform adjustable)
        {
            const float top = 0.75f, thick = 0.045f;
            var size = new Vector3(1.6f, thick, 0.8f);

            // 1. Plateau existant conservé (nom, collider) : seul son maillage devient arrondi.
            var plateau = adjustable.Find("Plateau de l'établi");
            if (plateau != null)
            {
                plateau.localScale = Vector3.one;
                plateau.localPosition = new Vector3(0, top - thick / 2, 0);
                plateau.GetComponent<MeshFilter>().sharedMesh = k.RoundedBoxMesh(size, 0.008f, 3);
                var r = plateau.GetComponent<MeshRenderer>();
                r.sharedMaterial = k.Oak;
                r.receiveGI = ReceiveGI.LightProbes;
                var bc = plateau.GetComponent<BoxCollider>();
                if (bc != null) { bc.center = Vector3.zero; bc.size = size; }
            }

            // 2. Ancien piètement et ancien habillage supprimés.
            var remove = new List<GameObject>();
            foreach (Transform c in adjustable)
                if (c.name == "Pied d'établi" || c.name == "Habillage de l'établi") remove.Add(c.gameObject);
            foreach (Transform c in station)
                if (c.name == "Piètement fixe") remove.Add(c.gameObject);
            foreach (var go in remove) Object.DestroyImmediate(go);

            // 3. Partie mobile (suit le réglage de hauteur) : non statique.
            k.StaticMode = false;
            var dress = AmbianceKit.Group("Habillage de l'établi", adjustable, Vector3.zero);
            float apronY = top - thick - 0.025f;
            k.Box("Ceinture avant", dress, new Vector3(0, apronY, -0.345f), new Vector3(1.42f, 0.05f, 0.025f), k.BlackMetal, 0.004f);
            k.Box("Ceinture arrière", dress, new Vector3(0, apronY, 0.345f), new Vector3(1.42f, 0.05f, 0.025f), k.BlackMetal, 0.004f);
            foreach (float s in new[] { -1f, 1f })
                k.Box("Ceinture latérale", dress, new Vector3(0.72f * s, apronY, 0), new Vector3(0.025f, 0.05f, 0.67f), k.BlackMetal, 0.004f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    k.Box("Platine", dress, new Vector3(0.72f * sx, top - thick - 0.005f, 0.30f * sz), new Vector3(0.08f, 0.01f, 0.08f), k.BlackMetal, 0.003f);
                    k.Box("Colonne télescopique", dress, new Vector3(0.72f * sx, 0.39f, 0.30f * sz), new Vector3(0.045f, 0.62f, 0.045f), k.BlackMetal, 0.004f);
                }
            // Boîtier de réglage électrique (décor) sous le chant avant.
            k.Box("Boîtier de réglage", dress, new Vector3(0.52f, top - thick - 0.018f, -0.375f), new Vector3(0.13f, 0.03f, 0.04f), k.BlackPlastic, 0.008f);
            k.Box("Afficheur", dress, new Vector3(0.495f, top - thick - 0.018f, -0.3955f), new Vector3(0.04f, 0.014f, 0.001f), k.Screen, 0f, false);
            foreach (float x in new[] { 0.54f, 0.563f })
                k.Cyl("Touche", dress, new Vector3(x, top - thick - 0.018f, -0.396f), 0.012f, 0.004f, k.GreyPlastic, Quaternion.Euler(90f, 0, 0), 12, false);

            // Réglette arrière (porte-étiquettes) et petite tablette.
            k.Box("Réglette porte-étiquettes", dress, new Vector3(0, top + 0.055f, 0.38f), new Vector3(1.56f, 0.11f, 0.03f), k.Oak, 0.004f);
            k.Box("Tablette haute", dress, new Vector3(0, top + 0.119f, 0.367f), new Vector3(1.56f, 0.018f, 0.066f), k.Oak, 0.004f);
            float shelfTop = top + 0.128f;
            k.Cyl("Pot à crayons", dress, new Vector3(-0.66f, shelfTop + 0.045f, 0.367f), 0.06f, 0.09f, k.BlackMetal, null, 20);
            for (int i = 0; i < 3; i++)
                k.Cyl("Crayon", dress, new Vector3(-0.66f + (i - 1) * 0.012f, shelfTop + 0.1f, 0.367f + (i % 2) * 0.01f), 0.007f, 0.17f, k.YellowPlastic, Quaternion.Euler((i - 1) * 6f, 0, (i - 1) * 8f), 6, true, false);
            k.Cyl("Rouleau d'adhésif", dress, new Vector3(0.66f, shelfTop + 0.025f, 0.367f), 0.09f, 0.05f, k.FeltSand, null, 24);
            k.Box("Mètre ruban", dress, new Vector3(0.53f, shelfTop + 0.035f, 0.367f), new Vector3(0.065f, 0.065f, 0.035f), k.YellowPlastic, 0.012f);

            // Tapis de montage et feutrines des zones de rangement des pièces.
            k.Box("Tapis de montage", dress, new Vector3(0, top + 0.0008f, -0.025f), new Vector3(0.58f, 0.0016f, 0.70f), k.Mat, 0f, false);
            k.Box("Feutrine rangement pieds", dress, new Vector3(-0.53f, top + 0.0005f, 0.09f), new Vector3(0.47f, 0.001f, 0.44f), k.Felt, 0f, false);
            k.Box("Feutrine rangement panneaux", dress, new Vector3(0.51f, top + 0.0005f, 0f), new Vector3(0.40f, 0.001f, 0.62f), k.Felt, 0f, false);
            k.Cyl("Socle du plateau tournant", dress, new Vector3(0, top + 0.004f, 0), 0.48f, 0.007f, k.BlackMetal, null, 48);

            // 4. Piètement fixe (fourreaux, pieds, tablette basse) : ne bouge pas avec le plan.
            k.StaticMode = true;
            var fixedBase = AmbianceKit.Group("Piètement fixe", station, Vector3.zero);
            foreach (float sx in new[] { -1f, 1f })
            {
                k.Box("Pied", fixedBase, new Vector3(0.72f * sx, 0.02f, 0), new Vector3(0.07f, 0.04f, 0.76f), k.BlackMetal, 0.006f);
                foreach (float sz in new[] { -1f, 1f })
                    k.Box("Fourreau", fixedBase, new Vector3(0.72f * sx, 0.18f, 0.30f * sz), new Vector3(0.06f, 0.30f, 0.06f), k.BlackMetal, 0.004f);
                k.Box("Support de tablette", fixedBase, new Vector3(0.69f * sx, 0.10f, 0.06f), new Vector3(0.03f, 0.02f, 0.56f), k.BlackMetal, 0.003f);
            }
            k.Box("Entretoise arrière", fixedBase, new Vector3(0, 0.24f, 0.30f), new Vector3(1.38f, 0.04f, 0.035f), k.BlackMetal, 0.004f);
            k.Box("Tablette basse", fixedBase, new Vector3(0, 0.12f, 0.06f), new Vector3(1.38f, 0.02f, 0.52f), k.Oak, 0.003f);
            var kit = AmbianceKit.Group("Carton du kit", fixedBase, new Vector3(-0.32f, 0.13f, 0.1f), 3f);
            k.Box("Carton", kit, new Vector3(0, 0.045f, 0), new Vector3(0.55f, 0.09f, 0.42f), k.Cardboard, 0.004f);
            k.Box("Étiquette", kit, new Vector3(0.12f, 0.045f, -0.2106f), new Vector3(0.2f, 0.06f, 0.001f), k.Paper, 0f, false);
            AmbianceKit.Text("Kit · table de chevet", kit, new Vector3(0.12f, 0.05f, -0.2113f), Quaternion.identity, 0.016f, Ink, FontStyles.Bold, TextAlignmentOptions.Center, 0.19f);
            var tb = AmbianceKit.Group("Caisse à outils", fixedBase, new Vector3(0.38f, 0.13f, 0.1f), -4f);
            k.Box("Caisse", tb, new Vector3(0, 0.06f, 0), new Vector3(0.42f, 0.12f, 0.2f), k.BluePlastic, 0.012f);
            k.Box("Poignée", tb, new Vector3(0, 0.13f, 0), new Vector3(0.18f, 0.018f, 0.025f), k.BlackPlastic, 0.007f);
            foreach (float s in new[] { -1f, 1f })
                k.Box("Fermoir", tb, new Vector3(0.13f * s, 0.085f, -0.103f), new Vector3(0.035f, 0.025f, 0.01f), k.YellowPlastic, 0.004f);
        }

        /// <summary>Ajuste les éléments du kit qui relèvent de l'apparence (sans toucher à leur logique).</summary>
        private static void RestyleKit(AmbianceKit k, Transform adjustable)
        {
            // Rangements : bleu (le jaune reste réservé au guidage : flèche, repères, silhouettes).
            var tray = AssetDatabase.LoadAssetAtPath<Material>(AmbianceKit.P4 + "/Materials/M_Rangement.mat");
            if (tray != null)
            {
                tray.SetColor("_BaseColor", new Color(0.05f, 0.26f, 0.52f));
                tray.SetFloat("_Smoothness", 0.45f);
                EditorUtility.SetDirty(tray);
            }
            var turntable = adjustable.Find("Support tournant/Plateau tournant");
            if (turntable != null && turntable.TryGetComponent<MeshRenderer>(out var tr)) tr.sharedMaterial = k.Birch;

            // Étiquettes des zones de rangement : posées à plat sur la réglette arrière.
            foreach (Transform c in adjustable)
            {
                if (!c.name.StartsWith("Étiquette") || c.localPosition.z < 0.3f) continue;
                c.localPosition = new Vector3(c.localPosition.x, 0.805f, 0.3635f);
                c.localRotation = Quaternion.identity;
                if (c.TryGetComponent<TextMeshPro>(out var t)) { t.color = Ink; t.fontStyle = FontStyles.Bold; }
            }
            foreach (var r in adjustable.GetComponentsInChildren<MeshRenderer>(true))
                if (!GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.ContributeGI))
                    r.receiveGI = ReceiveGI.LightProbes;
        }

        // =====================================================================
        // Éclairage et rendu
        // =====================================================================

        private static void BuildLighting(Transform env)
        {
            var g = AmbianceKit.Group("Éclairage", env, Vector3.zero);

            var sun = new GameObject("Soleil (mixte)").AddComponent<Light>();
            sun.transform.SetParent(g, false);
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(1f, -0.8f, 0.45f));
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            RenderSettings.sun = sun;

            // Sonde de réflexion de la pièce (projection en boîte).
            var probe = new GameObject("Sonde de réflexion").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(g, false);
            probe.transform.position = new Vector3(0, H / 2, (ZF + ZB) / 2);
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(2 * X, H, ZF - ZB);
            probe.boxProjection = true;
            probe.resolution = 128;
            probe.hdr = false;   // cubemap LDR : le décodage HDR des sondes blanchit URP/Lit dans cet éditeur

            // Sondes de lumière : grille de la pièce + maillage serré autour de l'établi.
            var lp = new GameObject("Sondes de lumière").AddComponent<LightProbeGroup>();
            lp.transform.SetParent(g, false);
            var pts = new List<Vector3>();
            for (float x = -3.2f; x <= 3.21f; x += 0.8f)
                for (float z = -3.2f; z <= 1.95f; z += 0.73f)
                    foreach (float y in new[] { 0.15f, 0.9f, 1.7f, 2.45f, 2.75f })
                        pts.Add(new Vector3(x, y, z));
            for (float x = -0.9f; x <= 0.91f; x += 0.3f)
                for (float z = 0.15f; z <= 1.06f; z += 0.3f)
                    foreach (float y in new[] { 0.55f, 0.85f, 1.15f, 1.5f })
                        pts.Add(new Vector3(x, y, z));
            lp.probePositions = pts.ToArray();

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.60f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.44f, 0.41f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.23f, 0.20f);
            // Reflet par défaut : cubemap neutre (LDR) de la pièce plutôt qu'un reflet « ciel » précalculé.
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = RoomCubemap();
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = false;

            const string lsPath = AmbianceKit.P4 + "/Rendering/Eclairage_Atelier.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "Eclairage_Atelier" };
                AssetDatabase.CreateAsset(ls, lsPath);
            }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.lightmapResolution = 14f;
            ls.lightmapPadding = 2;
            ls.lightmapMaxSize = 1024;
            ls.directSampleCount = 32;
            ls.indirectSampleCount = 256;
            ls.environmentSampleCount = 64;
            ls.maxBounces = 3;
            ls.indirectScale = 1.25f;
            ls.ao = true;
            ls.aoMaxDistance = 0.5f;
            ls.directionalityMode = LightmapsMode.NonDirectional;
            ls.lightmapCompression = LightmapCompression.NormalQuality;
            EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;

            // Même encodage de lightmaps (RGBM, « Normal ») sur ordinateur et sur Quest : l'encodage HDR
            // (BC6H) donne ici un rendu incohérent des surfaces précalculées.
            var setEncoding = typeof(PlayerSettings).GetMethod("SetLightmapEncodingQualityForPlatform",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (setEncoding != null)
            {
                var normal = System.Enum.Parse(setEncoding.GetParameters()[1].ParameterType, "Normal");
                foreach (var bt in new[] { BuildTarget.StandaloneOSX, BuildTarget.StandaloneWindows64, BuildTarget.Android })
                    setEncoding.Invoke(null, new object[] { bt, normal });
            }
        }

        /// <summary>Cubemap de reflet neutre (plafond clair, murs blanc cassé, sol chêne), générée une fois.</summary>
        private static Cubemap RoomCubemap()
        {
            const string path = AmbianceKit.P4 + "/Rendering/Reflet_Atelier.cubemap";
            var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (existing != null) return existing;
            const int n = 32;
            var cube = new Cubemap(n, TextureFormat.RGBA32, false);
            for (int f = 0; f < 6; f++)
            {
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / (n / 2f) - 1f, v = (y + 0.5f) / (n / 2f) - 1f;
                        Vector3 d;
                        switch (f)
                        {
                            case 0: d = new Vector3(1, -v, -u); break;
                            case 1: d = new Vector3(-1, -v, u); break;
                            case 2: d = new Vector3(u, 1, v); break;
                            case 3: d = new Vector3(u, -1, -v); break;
                            case 4: d = new Vector3(u, -v, 1); break;
                            default: d = new Vector3(-u, -v, -1); break;
                        }
                        float t = d.normalized.y;
                        var wall = new Color(0.62f, 0.61f, 0.58f);
                        px[y * n + x] = t > 0 ? Color.Lerp(wall, new Color(0.85f, 0.85f, 0.83f), t) : Color.Lerp(wall, new Color(0.36f, 0.29f, 0.22f), -t);
                    }
                cube.SetPixels(px, (CubemapFace)f);
            }
            cube.Apply();
            AssetDatabase.CreateAsset(cube, path);
            return cube;
        }

        private static void ConfigureRendering(Transform env)
        {
            const string path = AmbianceKit.P4 + "/Rendering/Profil_Atelier.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            T Comp<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c))
                {
                    c = profile.Add<T>(true);
                    c.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(c, profile);
                }
                return c;
            }
            var tm = Comp<Tonemapping>();
            tm.mode.Override(TonemappingMode.Neutral);
            var ca = Comp<ColorAdjustments>();
            ca.postExposure.Override(0.35f);
            ca.contrast.Override(8f);
            ca.saturation.Override(4f);
            ca.colorFilter.Override(new Color(1f, 0.985f, 0.96f));
            var bloom = Comp<Bloom>();
            bloom.intensity.Override(0.15f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.6f);
            EditorUtility.SetDirty(profile);

            var vol = new GameObject("Rendu (post-traitement)").AddComponent<Volume>();
            vol.transform.SetParent(env, false);
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                data.renderPostProcessing = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.9f, 0.9f, 0.88f);
            }

            // Qualité : MSAA 4× (indispensable en VR), ombres resserrées sur la pièce.
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (!(QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset a)) continue;
                a.msaaSampleCount = 4;
                a.shadowDistance = 9f;
                bool mobile = QualitySettings.names[i] == "Mobile";
                a.shadowCascadeCount = mobile ? 1 : 2;
                if (mobile) a.supportsHDR = false;
                EditorUtility.SetDirty(a);
            }
        }

        /// <summary>Précalcul (synchrone) de l'éclairage indirect et des sondes de réflexion.</summary>
        public static void Bake()
        {
            var t0 = System.DateTime.Now;
            bool ok = Lightmapping.Bake();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[De Toutes Pièces] Précalcul de l'éclairage {(ok ? "terminé" : "échoué")} en {(System.DateTime.Now - t0).TotalSeconds:F0} s.");
        }
    }
}
