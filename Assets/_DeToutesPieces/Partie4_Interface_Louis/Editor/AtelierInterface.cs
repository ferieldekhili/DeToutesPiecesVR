using DeToutesPieces.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeToutesPieces.EditorTools
{
    /// <summary>
    /// Refonte visuelle de l'interface (Partie 4) : sprites arrondis de l'identité commune, habillage du
    /// panneau de consigne et de la notice virtuelle, pupitres physiques qui les intègrent à l'atelier.
    /// Ne modifie ni les références des scripts, ni les noms d'objets utilisés par le code.
    /// </summary>
    public static class AtelierInterface
    {
        private const string ResFolder = AmbianceKit.P4 + "/Resources/DeToutesPieces";

        [MenuItem("De Toutes Pièces/Ambiance/Appliquer l'identité visuelle de l'interface")]
        public static void ApplyMenu()
        {
            if (Apply(new AmbianceKit())) EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        public static bool Apply(AmbianceKit k)
        {
            EnsureSprites();
            var panel = GameObject.Find("Panneau de consigne");
            var notice = GameObject.Find("Notice virtuelle");
            if (panel == null || notice == null)
            {
                Debug.LogWarning("[De Toutes Pièces] Panneau de consigne ou notice virtuelle introuvable : interface non restylée.");
                return false;
            }
            StyleInstructionPanel((RectTransform)panel.transform);
            var board = notice.transform.Find("Tableau") as RectTransform;
            if (board != null) StyleNotice(board);

            var env = GameObject.Find("Environnement");
            var stands = env != null ? env.transform.Find("Pupitres") : null;
            if (stands != null) Object.DestroyImmediate(stands.gameObject);
            if (env != null)
            {
                stands = AmbianceKit.Group("Pupitres", env.transform, Vector3.zero);
                k.StaticMode = true;
                Stand(k, stands, "Pupitre du panneau de consigne", panel.transform, new Vector2(0.64f, 0.52f));
                if (board != null) Stand(k, stands, "Pupitre de la notice", board, new Vector2(0.56f, 0.62f));
            }
            EditorSceneManager.MarkSceneDirty(panel.scene);
            return true;
        }

        // ------------------------------------------------------------------ sprites

        private static void EnsureSprites()
        {
            System.IO.Directory.CreateDirectory(ResFolder);
            MakeSprite("UI_Arrondi", 96, 28, 0f);
            MakeSprite("UI_Contour", 96, 28, 2.5f);
            MakeSprite("UI_Bouton", 64, 14, 0f);
        }

        /// <summary>Rectangle blanc à coins arrondis (anticrénelé) ; stroke > 0 : contour seul.</summary>
        private static void MakeSprite(string name, int size, float radius, float stroke)
        {
            string path = $"{ResFolder}/{name}.png";
            if (!System.IO.File.Exists(path))
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color[size * size];
                float half = size / 2f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                        float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                        float a = Mathf.Clamp01(0.5f - outside);
                        if (stroke > 0f) a *= Mathf.Clamp01(stroke + 0.5f + outside);
                        px[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                tex.SetPixels(px);
                tex.Apply();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            int border = Mathf.CeilToInt(radius) + 2;
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder.x != border)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.spriteBorder = new Vector4(border, border, border, border);
                imp.spritePixelsPerUnit = 100f;
                imp.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ panneaux

        private static void StyleInstructionPanel(RectTransform panel)
        {
            UITheme.StyleBackground(panel.GetComponent<Image>());
            UITheme.Decorate(panel);
            var title = panel.Find("Titre étape")?.GetComponent<TMP_Text>();
            if (title != null) { UITheme.StyleTitle(title); title.color = UITheme.Yellow; }
            var instr = panel.Find("Consigne")?.GetComponent<TMP_Text>();
            if (instr != null) { instr.color = UITheme.TextMain; instr.lineSpacing = 6f; }
            var prog = panel.Find("Progression")?.GetComponent<TMP_Text>();
            if (prog != null) prog.color = UITheme.Label;
            var msg = panel.Find("Fond message")?.GetComponent<Image>();
            if (msg != null) UITheme.StyleButton(msg);
            Separator(panel, "Séparateur", 206f);
            StyleButtons(panel);
            Caption(panel, "DE TOUTES PIÈCES · CONSIGNE");
        }

        private static void StyleNotice(RectTransform board)
        {
            UITheme.StyleBackground(board.GetComponent<Image>(), UITheme.Paper);
            UITheme.Decorate(board, new Color(0.62f, 0.60f, 0.56f, 0.9f));
            var title = board.Find("Titre")?.GetComponent<TMP_Text>();
            if (title != null) { UITheme.StyleTitle(title); title.color = new Color(0.0f, 0.24f, 0.48f); }
            StyleButtons(board);
            Caption(board, "NOTICE VIRTUELLE · AA-2547698-1", new Color(0.45f, 0.44f, 0.42f));
        }

        private static void StyleButtons(RectTransform root)
        {
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                if (b.transform.parent != root) continue;      // les boutons du tiroir sont stylés à l'exécution
                var img = b.GetComponent<Image>();
                UITheme.StyleButton(img);
                var c = img.color;
                if (Mathf.Abs(c.r - 1f) < 0.05f && c.g > 0.7f) img.color = UITheme.Yellow;
                else if (c.b > c.r + 0.25f) img.color = UITheme.Blue;
                else img.color = UITheme.Neutral;
                var colors = b.colors;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.82f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.45f);
                b.colors = colors;
            }
        }

        private static void Separator(RectTransform panel, string name, float fromTop)
        {
            if (panel.Find(name) != null) return;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(panel, false);
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(24f, -fromTop - 1f); rt.offsetMax = new Vector2(-24f, -fromTop);
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.12f);
            img.raycastTarget = false;
        }

        private static void Caption(RectTransform panel, string text, Color? color = null)
        {
            var old = panel.Find("Mention");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Mention", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(panel, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -4f);
            rt.sizeDelta = new Vector2(420f, 18f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 12f;
            t.characterSpacing = 6f;
            t.alignment = TextAlignmentOptions.TopRight;
            t.color = color ?? new Color(0.62f, 0.66f, 0.72f);
            t.raycastTarget = false;
        }

        // ------------------------------------------------------------------ pupitres

        /// <summary>Pupitre en métal noir : socle au sol, mât, platine et cadre derrière le panneau.</summary>
        private static void Stand(AmbianceKit k, Transform parent, string name, Transform canvas, Vector2 sizeM)
        {
            var g = AmbianceKit.Group(name, parent, Vector3.zero);
            var rot = canvas.rotation;
            var up = rot * Vector3.up;
            var back = rot * Vector3.forward;         // le dos du panneau (côté opposé à l'utilisateur)
            var center = canvas.position;

            var frame = k.Box("Cadre", g, center + back * 0.011f, new Vector3(sizeM.x + 0.016f, sizeM.y + 0.016f, 0.016f), k.BlackMetal, 0.032f);
            frame.transform.rotation = rot;
            var plate = k.Box("Platine", g, center + back * 0.03f, new Vector3(0.16f, 0.12f, 0.016f), k.BlackMetal, 0.004f);
            plate.transform.rotation = rot;

            var top = center + back * 0.045f - up * 0.02f;
            var foot = new Vector3(top.x, 0f, top.z);
            float height = top.y - 0.02f;
            k.Cyl("Mât", g, new Vector3(foot.x, 0.02f + height / 2f, foot.z), 0.035f, height, k.BlackMetal, null, 16);
            k.Cyl("Socle", g, new Vector3(foot.x, 0.009f, foot.z), 0.34f, 0.018f, k.BlackMetal, null, 32);
            k.Cyl("Patin", g, new Vector3(foot.x, 0.0015f, foot.z), 0.30f, 0.003f, k.BlackPlastic, null, 32, false);
        }
    }
}
