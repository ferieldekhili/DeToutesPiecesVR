using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Data;
using DeToutesPieces.Feedback;
using DeToutesPieces.Guidance;
using DeToutesPieces.Interaction;
using DeToutesPieces.Parts;
using DeToutesPieces.UI;
using DeToutesPieces.Workshop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace DeToutesPieces.EditorTools
{
    /// <summary>
    /// Outils de génération de la scène « Atelier ».
    /// - « Construire la scène Atelier » : crée la scène complète (refuse d'écraser une scène existante).
    /// - « Reconstruire le kit KNARREVIK » : remplace uniquement les pièces, silhouettes, vis et étapes
    ///   de la scène ouverte, sans toucher à l'environnement, au panneau, au rig XR ni aux systèmes.
    /// Géométrie : notice AA-2547698-1. Dimensions hors-tout approximatives (≈ 37 × 28 × 48 cm),
    /// la notice ne donnant pas de cotes.
    /// </summary>
    public static class WorkshopSceneBuilder
    {
        private const string Root = "Assets/_DeToutesPieces";
        public const string ScenePath = Root + "/Scenes/Atelier.unity";
        private const string XROriginPath = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private const string SimulatorPath = "Assets/Samples/XR Interaction Toolkit/3.6.1/XR Interaction Simulator/XR Interaction Simulator.prefab";

        private const float BenchHeight = 0.75f;

        // ---- Géométrie KNARREVIK (repère du meuble debout, origine au sol, centre) ----
        private const float Tube = 0.02f;                  // section des pieds
        private const float LegLength = 0.48f;             // hauteur totale
        private const float LegX = 0.175f, LegZ = 0.13f;   // axes des pieds -> encombrement ≈ 0.37 × 0.28
        private const float PanelW = 0.33f, PanelD = 0.24f, Rim = 0.02f;
        private const float ShelfTop = 0.30f;              // dessus de la tablette
        private const float TopTop = LegLength;            // dessus du plateau (affleurant le haut des pieds)
        private const float ShelfScrewY = ShelfTop - Rim / 2f;
        private const float TopScrewY = TopTop - Rim / 2f;

        private struct Box
        {
            public string name; public Vector3 pos; public Vector3 size; public int mat; // 0 = principal, 1 = secondaire
            public Box(string n, Vector3 p, Vector3 s, int m = 0) { name = n; pos = p; size = s; mat = m; }
        }

        // Pied canonique : pivot au sol ; trous sur les faces -X et -Z (pied avant gauche).
        private static readonly Box[] LegBoxes =
        {
            new Box("Tube", new Vector3(0, (LegLength + 0.012f) / 2f, 0), new Vector3(Tube, LegLength - 0.012f, Tube)),
            new Box("Embout plastique", new Vector3(0, 0.006f, 0), new Vector3(Tube + 0.001f, 0.012f, Tube + 0.001f), 1),
        };

        // Panneaux : pivot au centre de la face supérieure, bords repliés vers le bas.
        private static readonly Box[] PanelBoxes =
        {
            new Box("Tôle", new Vector3(0, -0.002f, 0), new Vector3(PanelW, 0.004f, PanelD)),
            new Box("Bord avant", new Vector3(0, -Rim / 2f, -PanelD / 2f + 0.001f), new Vector3(PanelW, Rim, 0.002f)),
            new Box("Bord arrière", new Vector3(0, -Rim / 2f, PanelD / 2f - 0.001f), new Vector3(PanelW, Rim, 0.002f)),
            new Box("Bord gauche", new Vector3(-PanelW / 2f + 0.001f, -Rim / 2f, 0), new Vector3(0.002f, Rim, PanelD)),
            new Box("Bord droit", new Vector3(PanelW / 2f - 0.001f, -Rim / 2f, 0), new Vector3(0.002f, Rim, PanelD)),
        };

        private struct LegSpec
        {
            public string id; public string label; public Vector3 pos; public float yaw;
            public LegSpec(string i, string l, Vector3 p, float y) { id = i; label = l; pos = p; yaw = y; }
        }

        // L'utilisateur fait face à +Z : l'avant du meuble est côté -Z.
        private static readonly LegSpec[] Legs =
        {
            new LegSpec("A", "pied avant gauche", new Vector3(-LegX, 0, -LegZ), 0f),
            new LegSpec("B", "pied avant droit", new Vector3(LegX, 0, -LegZ), -90f),
            new LegSpec("C", "pied arrière gauche", new Vector3(-LegX, 0, LegZ), 90f),
            new LegSpec("D", "pied arrière droit", new Vector3(LegX, 0, LegZ), 180f),
        };

        // =====================================================================
        // Menus
        // =====================================================================

        [MenuItem("De Toutes Pièces/Construire la scène Atelier")]
        public static void BuildMenu()
        {
            if (System.IO.File.Exists(ScenePath))
            {
                Debug.LogWarning($"[De Toutes Pièces] {ScenePath} existe déjà : rien n'est reconstruit.");
                return;
            }
            Build();
        }

        [MenuItem("De Toutes Pièces/Ajouter le serrage final (phase 2) à la scène ouverte")]
        public static void AddPhase2InOpenScene()
        {
            var adjustable = GameObject.Find("Plan réglable");
            var manager = Object.FindFirstObjectByType<AssemblyManager>();
            if (adjustable == null || manager == null)
            {
                Debug.LogError("[De Toutes Pièces] Ouvrez la scène Atelier.");
                return;
            }
            BuildPhase2(adjustable.transform, new Materials(), manager);
            var scene = adjustable.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[De Toutes Pièces] Serrage final (étape 6) ajouté : clé Allen, guide de rotation, 16 serrages, contrôle d'équerre.");
        }

        [MenuItem("De Toutes Pièces/Reconstruire le kit KNARREVIK (scène ouverte)")]
        public static void RebuildKitInOpenScene()
        {
            var adjustable = GameObject.Find("Plan réglable");
            var manager = Object.FindFirstObjectByType<AssemblyManager>();
            if (adjustable == null || manager == null)
            {
                Debug.LogError("[De Toutes Pièces] Ouvrez la scène Atelier (Plan réglable / Gestionnaire de montage introuvables).");
                return;
            }

            // Ne supprime que le contenu du kit ; l'établi, l'environnement et les systèmes sont conservés.
            var toDelete = new List<GameObject>();
            foreach (Transform c in adjustable.transform)
            {
                string n = c.name;
                if (n == "Support du meuble" || n == "Support tournant" || n.StartsWith("Assemblage") ||
                    n.StartsWith("Rangement") || n == "Bac à vis" || n == "Emplacement clé Allen" || n.StartsWith("Étiquette"))
                    toDelete.Add(c.gameObject);
            }
            foreach (var go in toDelete) Object.DestroyImmediate(go);

            KitDataBuilder.EnsureKitData();
            BuildKit(adjustable.transform, new Materials(), manager);

            var scene = adjustable.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[De Toutes Pièces] Kit KNARREVIK reconstruit d'après la notice AA-2547698-1.");
        }

        public static string Build()
        {
            KitDataBuilder.EnsureKitData();
            var mats = new Materials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var env = new GameObject("Environnement").transform;
            BuildEnvironment(env, mats);

            var station = new GameObject("Poste de montage").transform;
            station.position = new Vector3(0f, 0f, 0.6f);
            var adjustable = new GameObject("Plan réglable").transform;
            adjustable.SetParent(station, false);
            var workstation = station.gameObject.AddComponent<AdjustableWorkstation>();
            SetField(workstation, "adjustableRoot", adjustable);
            BuildBench(adjustable, mats);

            var systems = new GameObject("Systèmes").transform;
            var feedback = new GameObject("Retours (son, haptique, visuel)").AddComponent<FeedbackService>();
            feedback.transform.SetParent(systems, false);
            SetField(feedback, "pulseMaterial", mats.Pulse);
            var manager = new GameObject("Gestionnaire de montage").AddComponent<AssemblyManager>();
            manager.transform.SetParent(systems, false);
            new GameObject("Raccourcis manettes").AddComponent<ControllerShortcuts>().transform.SetParent(systems, false);

            BuildKit(adjustable, mats, manager);

            var arrowGo = new GameObject("Flèche de guidage");
            var arrow = arrowGo.AddComponent<GuidanceArrow>();
            var arrowVisual = new GameObject("Visuel");
            arrowVisual.transform.SetParent(arrowGo.transform, false);
            arrowVisual.AddComponent<MeshFilter>().sharedMesh = mats.ArrowMesh;
            var ar = arrowVisual.AddComponent<MeshRenderer>();
            ar.sharedMaterial = mats.Arrow;
            ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SetField(arrow, "visual", arrowVisual.transform);

            Camera xrCamera = BuildXR();
            BuildEventSystem();
            BuildInstructionPanel(xrCamera);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.78f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.26f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            return ScenePath;
        }

        // =====================================================================
        // Kit KNARREVIK : support tournant, silhouettes, trous, rangement, étapes
        // =====================================================================

        private static void BuildKit(Transform adjustable, Materials m, AssemblyManager manager)
        {
            // Support tournant (transcription des retournements de la notice).
            var turntable = new GameObject("Support tournant").transform;
            turntable.SetParent(adjustable, false);
            turntable.localPosition = new Vector3(0, BenchHeight, 0);
            turntable.gameObject.AddComponent<AssemblyTurntable>();
            Cube("Plateau tournant", turntable, new Vector3(0, 0.015f, 0), new Vector3(0.46f, 0.03f, 0.40f), m.Stand);
            Cube("Repère avant", turntable, new Vector3(0, 0.031f, -0.19f), new Vector3(0.08f, 0.002f, 0.015f), m.Tray, false);

            var assembly = new GameObject("Assemblage (silhouettes)").transform;
            assembly.SetParent(turntable, false);
            assembly.localPosition = new Vector3(0, 0.03f, 0);

            // Cales de maintien de la tablette (aide virtuelle, visibles pendant les étapes 1 à 3).
            var calesRoot = new GameObject("Cales de maintien (étapes 1 à 3)").transform;
            calesRoot.SetParent(assembly, false);
            var cales = new GameObject("Cales").transform;
            cales.SetParent(calesRoot, false);
            float caleH = ShelfTop - Rim;
            foreach (var c in new[] { new Vector2(-0.12f, -0.08f), new Vector2(0.12f, -0.08f), new Vector2(-0.12f, 0.08f), new Vector2(0.12f, 0.08f) })
                Cube("Cale", cales, new Vector3(c.x, caleH / 2f, c.y), new Vector3(0.03f, caleH, 0.03f), m.Cale, false);
            var scoped = calesRoot.gameObject.AddComponent<StepScopedObject>();
            var sso = new SerializedObject(scoped);
            sso.FindProperty("target").objectReferenceValue = cales.gameObject;
            sso.FindProperty("fromStep").intValue = 0;
            sso.FindProperty("toStep").intValue = 2;
            sso.ApplyModifiedPropertiesWithoutUndo();

            var gShelf = Ghost("Silhouette tablette inférieure", assembly, new Vector3(0, ShelfTop, 0), 0f, PanelBoxes, KitDataBuilder.LowerShelf, m.Ghost, true);
            var gTop = Ghost("Silhouette plateau", assembly, new Vector3(0, TopTop, 0), 0f, PanelBoxes, KitDataBuilder.Top, m.Ghost, true);

            var legGhosts = new Dictionary<string, GhostSnapTarget>();
            var shelfSlots = new Dictionary<string, ScrewSlot[]>();
            var topSlots = new Dictionary<string, ScrewSlot[]>();
            foreach (var l in Legs)
            {
                var g = Ghost($"Silhouette {l.label} ({l.id})", assembly, l.pos, l.yaw, LegBoxes, KitDataBuilder.Leg, m.Ghost, false);
                legGhosts[l.id] = g;
                shelfSlots[l.id] = new[]
                {
                    Slot(g, $"{l.id} tablette, face avant/arrière", $"{l.label}, tablette", new Vector3(0, ShelfScrewY, -Tube / 2f), Vector3.forward, m),
                    Slot(g, $"{l.id} tablette, face latérale", $"{l.label}, tablette (côté)", new Vector3(-Tube / 2f, ShelfScrewY, 0), Vector3.right, m),
                };
                topSlots[l.id] = new[]
                {
                    Slot(g, $"{l.id} plateau, face avant/arrière", $"{l.label}, plateau", new Vector3(0, TopScrewY, -Tube / 2f), Vector3.forward, m),
                    Slot(g, $"{l.id} plateau, face latérale", $"{l.label}, plateau (côté)", new Vector3(-Tube / 2f, TopScrewY, 0), Vector3.right, m),
                };
            }

            // ---------- Rangement ----------
            var storage = new GameObject("Rangement des pièces").transform;
            storage.SetParent(adjustable, false);
            var lying = Quaternion.Euler(0, 0, 90f); // pieds posés à plat : à redresser et orienter
            float[] legZ = { 0.27f, 0.15f, 0.03f, -0.09f };
            for (int i = 0; i < 4; i++)
                Part($"Pied {i + 1}", storage, new Vector3(-0.25f, BenchHeight + Tube / 2f + 0.002f, legZ[i]), lying, LegBoxes, KitDataBuilder.Leg, m.Metal, m.Plastic, 0.6f, true);
            Part("Tablette inférieure", storage, new Vector3(0.5f, BenchHeight + Rim + 0.002f, 0.17f), Quaternion.identity, PanelBoxes, KitDataBuilder.LowerShelf, m.Panel, m.Panel, 1.0f, true);
            Part("Plateau", storage, new Vector3(0.5f, BenchHeight + Rim + 0.002f, -0.17f), Quaternion.identity, PanelBoxes, KitDataBuilder.Top, m.Panel, m.Panel, 1.2f, true);

            // Bac à vis : 16 vis de l'unique référence du kit.
            var trayRoot = new GameObject("Bac à vis").transform;
            trayRoot.SetParent(adjustable, false);
            trayRoot.localPosition = new Vector3(0.15f, BenchHeight, -0.28f);
            Cube("Fond", trayRoot, new Vector3(0, 0.002f, 0), new Vector3(0.17f, 0.004f, 0.12f), m.Tray);
            Cube("Paroi avant", trayRoot, new Vector3(0, 0.013f, -0.059f), new Vector3(0.17f, 0.022f, 0.002f), m.Tray);
            Cube("Paroi arrière", trayRoot, new Vector3(0, 0.013f, 0.059f), new Vector3(0.17f, 0.022f, 0.002f), m.Tray);
            Cube("Paroi gauche", trayRoot, new Vector3(-0.084f, 0.013f, 0), new Vector3(0.002f, 0.022f, 0.12f), m.Tray);
            Cube("Paroi droite", trayRoot, new Vector3(0.084f, 0.013f, 0), new Vector3(0.002f, 0.022f, 0.12f), m.Tray);
            var screws = new GameObject("Vis").transform;
            screws.SetParent(trayRoot, false);
            for (int ix = 0; ix < 4; ix++)
                for (int iz = 0; iz < 4; iz++)
                    Screw($"Vis {ix * 4 + iz + 1:00}", screws, new Vector3(-0.0525f + ix * 0.035f, 0.004f + 0.013f, -0.036f + iz * 0.024f), KitDataBuilder.KitScrew, m);

            var keySpot = Cube("Emplacement clé Allen", adjustable, new Vector3(-0.14f, BenchHeight + 0.003f, -0.3f), new Vector3(0.12f, 0.006f, 0.06f), m.Tray);

            Label3D("Pieds ×4", adjustable, new Vector3(-0.49f, BenchHeight + 0.06f, 0.36f), 0.02f);
            Label3D("Tablette inférieure / Plateau", adjustable, new Vector3(0.5f, BenchHeight + 0.06f, 0.36f), 0.02f);
            Label3D("Vis 10118490 ×16\n(une seule référence)", adjustable, trayRoot.localPosition + new Vector3(0.09f, 0.06f, 0), 0.012f);
            Label3D("Clé Allen 100006\n(serrage : phase 2)", adjustable, keySpot.transform.localPosition + new Vector3(-0.1f, 0.06f, 0), 0.012f);

            ConfigureSteps(manager, gShelf, gTop, legGhosts, shelfSlots, topSlots);
            BuildPhase2(adjustable, m, manager);
        }

        // ---------- Phase 2 : clé Allen, guide de rotation, étape 6 (p. 12) ----------

        /// <summary>Idempotent : remplace les éléments de phase 2 existants.</summary>
        private static void BuildPhase2(Transform adjustable, Materials m, AssemblyManager manager)
        {
            var turntable = adjustable.Find("Support tournant");
            var assembly = turntable != null ? turntable.Find("Assemblage (silhouettes)") : null;
            var keySpot = adjustable.Find("Emplacement clé Allen");
            if (assembly == null || keySpot == null) { Debug.LogError("[De Toutes Pièces] Kit introuvable : reconstruisez d'abord le kit."); return; }

            // Nettoyage d'une éventuelle version précédente.
            foreach (var n in new[] { "Clé Allen" })
            { var old = adjustable.Find(n); if (old) Object.DestroyImmediate(old.gameObject); }
            var oldGaze = assembly.Find("Contrôle d'équerre");
            if (oldGaze) Object.DestroyImmediate(oldGaze.gameObject);
            var oldGuide = GameObject.Find("Guide de rotation");
            if (oldGuide) Object.DestroyImmediate(oldGuide);
            foreach (var tr in assembly.GetComponentsInChildren<TightenRequirement>(true)) Object.DestroyImmediate(tr);

            BuildAllenKey(adjustable, keySpot.localPosition + new Vector3(0f, 0.006f, 0f), m);
            BuildRotationGuide(m);

            // 16 serrages, pied par pied (A, B face à l'utilisateur ; C, D après rotation automatique).
            var reqs = new List<StepRequirement>();
            foreach (var id in new[] { "A", "B", "C", "D" })
            {
                foreach (var suffix in new[] { "tablette, face avant/arrière", "tablette, face latérale", "plateau, face avant/arrière", "plateau, face latérale" })
                {
                    var slotTr = FindDeep(assembly, $"Trou {id} {suffix}");
                    if (slotTr == null) { Debug.LogError($"Trou {id} {suffix} introuvable"); continue; }
                    var tr = slotTr.gameObject.AddComponent<TightenRequirement>();
                    var tso = new SerializedObject(tr);
                    tso.FindProperty("slot").objectReferenceValue = slotTr.GetComponent<ScrewSlot>();
                    tso.ApplyModifiedPropertiesWithoutUndo();
                    reqs.Add(tr);
                }
            }

            var gazeGo = new GameObject("Contrôle d'équerre");
            gazeGo.transform.SetParent(assembly, false);
            gazeGo.transform.localPosition = new Vector3(0f, LegLength * 0.5f, -LegZ);
            reqs.Add(gazeGo.AddComponent<GazeCheckRequirement>());

            var so = new SerializedObject(manager);
            var steps = so.FindProperty("steps");
            if (steps.arraySize < 5) { Debug.LogError("[De Toutes Pièces] Les étapes 1 à 5 sont absentes."); return; }
            steps.arraySize = 6;
            var s6 = steps.GetArrayElementAtIndex(5);
            FillStep(s6, "Serrage final et contrôle",
                "Prenez la clé Allen, engagez son embout dans chaque vis désignée et tournez dans le sens des aiguilles d'une montre jusqu'au serrage complet (16 vis). Vérifiez ensuite que les pieds sont d'équerre.",
                12, 16, 0f, reqs.ToArray(),
                new[] { "Approchez l'embout court de la clé de la tête de vis, dans son axe : la clé s'emboîte.",
                        "Tournez le poignet par petits quarts de tour, sens horaire. Vous pouvez relâcher puis reprendre la clé : l'avancement est conservé." });
            s6.FindPropertyRelative("turntableFollowsGuidance").boolValue = true;
            for (int i = 0; i < 5; i++) steps.GetArrayElementAtIndex(i).FindPropertyRelative("turntableFollowsGuidance").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildAllenKey(Transform parent, Vector3 localPos, Materials m)
        {
            const float bar = 0.004f, shortArm = 0.03f, longArm = 0.075f;
            var key = new GameObject("Clé Allen");
            key.transform.SetParent(parent, false);
            key.transform.localPosition = localPos + new Vector3(0f, bar / 2f, 0f);
            key.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // posée à plat

            // Repère de la clé : coude à l'origine, bras court vers +Z (embout), bras long vers -Y.
            var visual = new GameObject("Visuel").transform;
            visual.SetParent(key.transform, false);
            Cube("Bras court", visual, new Vector3(0, 0, shortArm / 2f), new Vector3(bar, bar, shortArm), m.Key, false);
            Cube("Bras long", visual, new Vector3(0, -longArm / 2f, 0), new Vector3(bar, longArm, bar), m.Key, false);
            Cube("Repère embout", visual, new Vector3(0, 0, shortArm - 0.002f), new Vector3(bar * 1.15f, bar * 1.15f, 0.004f), m.Tray, false);

            var tip = new GameObject("Embout").transform;
            tip.SetParent(key.transform, false);
            tip.localPosition = new Vector3(0, 0, shortArm);

            var col = key.AddComponent<BoxCollider>(); // volume de prise élargi pour la manette
            col.center = new Vector3(0f, -longArm / 2f + 0.005f, shortArm / 2f - 0.005f);
            col.size = new Vector3(0.02f, longArm + 0.02f, shortArm + 0.02f);

            var rb = key.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            key.AddComponent<AssemblyPart>();
            key.AddComponent<PartHighlighter>();
            var grab = key.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;
            var bridge = key.AddComponent<XRPartGrabBridge>();
            var bso = new SerializedObject(bridge);
            bso.FindProperty("allowTwoHands").boolValue = false;
            bso.ApplyModifiedPropertiesWithoutUndo();

            var allen = key.AddComponent<AllenKey>();
            var aso = new SerializedObject(allen);
            aso.FindProperty("tip").objectReferenceValue = tip;
            aso.FindProperty("visualRoot").objectReferenceValue = visual;
            aso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRotationGuide(Materials m)
        {
            var go = new GameObject("Guide de rotation");
            var guide = go.AddComponent<RotationGuide>();

            var arrow = new GameObject("Flèche circulaire");
            arrow.transform.SetParent(go.transform, false);
            arrow.AddComponent<MeshFilter>().sharedMesh = m.RotationArrowMesh;
            var ar = arrow.AddComponent<MeshRenderer>();
            ar.sharedMaterial = m.Arrow;
            ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var prog = new GameObject("Progression du serrage");
            prog.transform.SetParent(go.transform, false);
            var pf = prog.AddComponent<MeshFilter>();
            var pr = prog.AddComponent<MeshRenderer>();
            pr.sharedMaterial = m.Progress;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var so = new SerializedObject(guide);
            so.FindProperty("arrowRenderer").objectReferenceValue = ar;
            so.FindProperty("progressFilter").objectReferenceValue = pf;
            so.FindProperty("progressRenderer").objectReferenceValue = pr;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ---------- Étapes de la notice (p. 7 à 11) ----------

        private static void ConfigureSteps(AssemblyManager manager, GhostSnapTarget shelf, GhostSnapTarget top,
            Dictionary<string, GhostSnapTarget> legs, Dictionary<string, ScrewSlot[]> shelfSlots, Dictionary<string, ScrewSlot[]> topSlots)
        {
            var so = new SerializedObject(manager);
            var steps = so.FindProperty("steps");
            steps.arraySize = 5;

            FillStep(steps.GetArrayElementAtIndex(0), "Premier pied sur la tablette",
                "Posez la tablette inférieure sur les cales. Redressez un pied, trous vers l'extérieur et embout en bas, placez-le à l'avant gauche, puis engagez 2 vis sans les serrer.",
                7, 2, 0f,
                new StepRequirement[] { shelf, legs["A"], shelfSlots["A"][0], shelfSlots["A"][1] },
                new[] { "Les pieds sont à plat : saisissez-en un et redressez-le, l'embout plastique vers le bas.",
                        "Tournez le pied pour que ses trous soient face à vous et sur le côté extérieur : la silhouette devient verte." });

            FillStep(steps.GetArrayElementAtIndex(1), "Deuxième pied",
                "Placez un deuxième pied à l'avant droit et engagez 2 vis, sans serrer.",
                8, 2, 0f,
                new StepRequirement[] { legs["B"], shelfSlots["B"][0], shelfSlots["B"][1] },
                new[] { "Les trous du pied doivent être vers l'avant et vers la droite." });

            FillStep(steps.GetArrayElementAtIndex(2), "Troisième et quatrième pieds",
                "L'ouvrage a été retourné. Placez les deux derniers pieds et engagez 4 vis, sans serrer.",
                9, 4, 180f,
                new StepRequirement[] { legs["C"], shelfSlots["C"][0], shelfSlots["C"][1], legs["D"], shelfSlots["D"][0], shelfSlots["D"][1] },
                new[] { "Comme pour les premiers pieds : embout en bas, trous vers l'extérieur." });

            FillStep(steps.GetArrayElementAtIndex(3), "Plateau",
                "Posez le plateau en haut des pieds, puis engagez 4 vis dans les deux pieds face à vous, sans serrer.",
                10, 4, 180f,
                new StepRequirement[] { top, topSlots["C"][0], topSlots["C"][1], topSlots["D"][0], topSlots["D"][1] },
                new[] { "Le plateau s'insère entre le haut des quatre pieds, affleurant leur extrémité." });

            FillStep(steps.GetArrayElementAtIndex(4), "Fixations du plateau, autre côté",
                "L'ouvrage a été retourné. Engagez les 4 dernières vis du plateau, sans serrer.",
                11, 4, 0f,
                new StepRequirement[] { topSlots["A"][0], topSlots["A"][1], topSlots["B"][0], topSlots["B"][1] },
                new[] { "Suivez la flèche jaune : elle indique chaque trou à garnir." });

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillStep(SerializedProperty step, string title, string instruction, int page, int screws, float yaw, StepRequirement[] reqs, string[] hints)
        {
            step.FindPropertyRelative("title").stringValue = title;
            step.FindPropertyRelative("instruction").stringValue = instruction;
            step.FindPropertyRelative("noticePage").intValue = page;
            step.FindPropertyRelative("expectedScrewCount").intValue = screws;
            step.FindPropertyRelative("turntableYaw").floatValue = yaw;
            step.FindPropertyRelative("verifiedAgainstNotice").boolValue = true;
            var r = step.FindPropertyRelative("requirements");
            r.arraySize = reqs.Length;
            for (int i = 0; i < reqs.Length; i++) r.GetArrayElementAtIndex(i).objectReferenceValue = reqs[i];
            var h = step.FindPropertyRelative("textHints");
            h.arraySize = hints.Length;
            for (int i = 0; i < hints.Length; i++) h.GetArrayElementAtIndex(i).stringValue = hints[i];
        }

        // ---------- Fabriques ----------

        private static GameObject Part(string name, Transform parent, Vector3 localPos, Quaternion localRot, Box[] boxes, PartDefinition def, Material main, Material secondary, float mass, bool twoHands)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            foreach (var b in boxes) Cube(b.name, go.transform, b.pos, b.size, b.mat == 0 ? main : secondary);
            if (def == KitDataBuilder.Leg) AddHoleMarkers(go.transform, secondary);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            go.AddComponent<AssemblyPart>().definition = def;
            go.AddComponent<PartHighlighter>();
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;
            var bridge = go.AddComponent<XRPartGrabBridge>();
            var bso = new SerializedObject(bridge);
            bso.FindProperty("allowTwoHands").boolValue = twoHands;
            bso.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>Repères visuels des trous (faces -X et -Z, au niveau tablette et plateau).</summary>
        private static void AddHoleMarkers(Transform leg, Material mat)
        {
            foreach (float y in new[] { ShelfScrewY, TopScrewY })
            {
                var a = Disc("Trou", leg, new Vector3(0, y, -Tube / 2f - 0.0004f), Vector3.back, 0.007f, mat);
                var b = Disc("Trou", leg, new Vector3(-Tube / 2f - 0.0004f, y, 0), Vector3.left, 0.007f, mat);
            }
        }

        private static void Screw(string name, Transform parent, Vector3 localPos, FastenerDefinition def, Materials m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // Pivot sous la tête ; tête vers +Y, tige vers -Y.
            var head = Cylinder("Tête", go.transform, new Vector3(0, 0.002f, 0), new Vector3(0.013f, 0.002f, 0.013f), m.Screw);
            Cylinder("Empreinte six pans", go.transform, new Vector3(0, 0.0041f, 0), new Vector3(0.005f, 0.0002f, 0.005f), m.Plastic);
            Cylinder("Tige", go.transform, new Vector3(0, -0.006f, 0), new Vector3(0.005f, 0.006f, 0.005f), m.Screw);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, -0.003f, 0);
            col.size = new Vector3(0.02f, 0.018f, 0.02f); // plus large que la vis : prise facilitée à la manette

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            go.AddComponent<AssemblyPart>();
            go.AddComponent<FastenerItem>().definition = def;
            go.AddComponent<PartHighlighter>();
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;
            var bridge = go.AddComponent<XRPartGrabBridge>();
            var bso = new SerializedObject(bridge);
            bso.FindProperty("allowTwoHands").boolValue = false;
            bso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GhostSnapTarget Ghost(string name, Transform parent, Vector3 localPos, float yaw, Box[] boxes, PartDefinition def, Material ghostMat, bool symmetric)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);

            var visual = new GameObject("Silhouette");
            visual.transform.SetParent(go.transform, false);
            foreach (var b in boxes)
            {
                var c = Cube(b.name, visual.transform, b.pos, b.size * 1.01f, ghostMat, false);
                c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (def == KitDataBuilder.Leg) AddHoleMarkers(visual.transform, ghostMat);

            var target = go.AddComponent<GhostSnapTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("expectedPart").objectReferenceValue = def;
            so.FindProperty("ghostVisual").objectReferenceValue = visual;
            so.FindProperty("symmetric180").boolValue = symmetric;
            so.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }

        private static ScrewSlot Slot(GhostSnapTarget host, string name, string label, Vector3 localPos, Vector3 insertDir, Materials m)
        {
            var go = new GameObject("Trou " + name);
            go.transform.SetParent(host.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(insertDir, Vector3.up);

            var marker = new GameObject("Repère cible");
            marker.transform.SetParent(go.transform, false);
            var ring = Disc("Anneau", marker.transform, Vector3.back * 0.0015f, Vector3.back, 0.016f, m.SlotRing);
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var slot = go.AddComponent<ScrewSlot>();
            var so = new SerializedObject(slot);
            so.FindProperty("expectedFastener").objectReferenceValue = KitDataBuilder.KitScrew;
            so.FindProperty("host").objectReferenceValue = host;
            so.FindProperty("locationLabel").stringValue = label;
            so.FindProperty("targetMarker").objectReferenceValue = marker;
            so.ApplyModifiedPropertiesWithoutUndo();
            return slot;
        }

        // =====================================================================
        // Environnement, établi, XR, panneau
        // =====================================================================

        private static void BuildEnvironment(Transform env, Materials m)
        {
            Cube("Sol", env, new Vector3(0, -0.05f, 0), new Vector3(7f, 0.1f, 7f), m.Floor);
            Cube("Mur avant", env, new Vector3(0, 1.5f, 2.2f), new Vector3(7f, 3f, 0.1f), m.Wall);
            Cube("Mur gauche", env, new Vector3(-3.5f, 1.5f, 0), new Vector3(0.1f, 3f, 7f), m.Wall);
            Cube("Mur droit", env, new Vector3(3.5f, 1.5f, 0), new Vector3(0.1f, 3f, 7f), m.Wall);
            Cube("Mur arrière", env, new Vector3(0, 1.5f, -3.5f), new Vector3(7f, 3f, 0.1f), m.Wall);
            Cube("Plinthe avant", env, new Vector3(0, 0.05f, 2.14f), new Vector3(7f, 0.1f, 0.02f), m.Stand, false);

            var zone = new GameObject("Zone d'évolution (2 m × 1,5 m)").transform;
            zone.SetParent(env, false);
            float zMin = -1.3f, zMax = 0.2f, xHalf = 1f;
            Cube("Tapis", zone, new Vector3(0, 0.001f, (zMin + zMax) / 2f), new Vector3(2 * xHalf, 0.002f, zMax - zMin), m.Zone, false);
            Cube("Bord gauche", zone, new Vector3(-xHalf, 0.003f, (zMin + zMax) / 2f), new Vector3(0.03f, 0.004f, zMax - zMin), m.ZoneBorder, false);
            Cube("Bord droit", zone, new Vector3(xHalf, 0.003f, (zMin + zMax) / 2f), new Vector3(0.03f, 0.004f, zMax - zMin), m.ZoneBorder, false);
            Cube("Bord arrière", zone, new Vector3(0, 0.003f, zMin), new Vector3(2 * xHalf, 0.004f, 0.03f), m.ZoneBorder, false);
            Cube("Bord avant", zone, new Vector3(0, 0.003f, zMax), new Vector3(2 * xHalf, 0.004f, 0.03f), m.ZoneBorder, false);
            var lbl = Label3D("Zone d'évolution minimale : 2 m × 1,5 m", zone, new Vector3(0, 0.006f, zMin + 0.15f), 0.06f);
            lbl.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            var sun = new GameObject("Lumière principale").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.SetParent(env, false);
            sun.transform.rotation = Quaternion.Euler(55f, -25f, 0);

            var lamp = new GameObject("Lampe d'atelier").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 3.5f;
            lamp.intensity = 1.5f;
            lamp.color = new Color(1f, 0.95f, 0.88f);
            lamp.transform.SetParent(env, false);
            lamp.transform.position = new Vector3(0, 2.2f, 0.5f);
        }

        private static void BuildBench(Transform adjustable, Materials m)
        {
            Cube("Plateau de l'établi", adjustable, new Vector3(0, BenchHeight - 0.02f, 0), new Vector3(1.5f, 0.04f, 0.75f), m.Wood);
            float legH = BenchHeight - 0.04f;
            foreach (var p in new[] { new Vector2(-0.7f, -0.33f), new Vector2(0.7f, -0.33f), new Vector2(-0.7f, 0.33f), new Vector2(0.7f, 0.33f) })
                Cube("Pied d'établi", adjustable, new Vector3(p.x, legH / 2f, p.y), new Vector3(0.05f, legH, 0.05f), m.Metal);
        }

        private static Camera BuildXR()
        {
            Camera cam = null;
            var originPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPath);
            if (originPrefab != null)
            {
                var origin = (GameObject)PrefabUtility.InstantiatePrefab(originPrefab);
                origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var t in origin.GetComponentsInChildren<Transform>(true))
                {
                    if (t == origin.transform) continue;
                    if (t.name.Contains("Locomotion") || t.name.Contains("Teleport")) t.gameObject.SetActive(false);
                }
                cam = origin.GetComponentInChildren<Camera>(true);
            }
            else
            {
                var c = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = c.AddComponent<Camera>();
                c.transform.position = new Vector3(0, 1.6f, 0);
            }

            var simPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPath);
            if (simPrefab != null)
                ((GameObject)PrefabUtility.InstantiatePrefab(simPrefab)).AddComponent<EditorOnlyObject>();
            return cam;
        }

        private static void BuildEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<XRUIInputModule>();
        }

        private static void BuildInstructionPanel(Camera cam)
        {
            var go = new GameObject("Panneau de consigne");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(640, 520);
            rt.localScale = Vector3.one * 0.001f;
            rt.position = new Vector3(-0.95f, 1.38f, 0.65f);
            rt.rotation = Quaternion.Euler(8f, -50f, 0f);

            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var bg = go.AddComponent<Image>();
            bg.sprite = sprite; bg.type = Image.Type.Sliced;
            bg.color = new Color(0.07f, 0.09f, 0.13f, 0.96f);

            var title = Text(go.transform, "Titre étape", "Étape", 30, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -70), new Vector2(-24, -18), TextAlignmentOptions.TopLeft, new Color(1f, 0.8f, 0.1f), FontStyles.Bold);
            var instr = Text(go.transform, "Consigne", "", 24, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -200), new Vector2(-24, -78), TextAlignmentOptions.TopLeft, Color.white, FontStyles.Normal);
            var prog = Text(go.transform, "Progression", "", 21, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -330), new Vector2(-24, -208), TextAlignmentOptions.TopLeft, new Color(0.8f, 0.88f, 1f), FontStyles.Normal);

            var msgBgGo = new GameObject("Fond message", typeof(RectTransform), typeof(Image));
            msgBgGo.transform.SetParent(go.transform, false);
            var msgRt = (RectTransform)msgBgGo.transform;
            msgRt.anchorMin = new Vector2(0, 0); msgRt.anchorMax = new Vector2(1, 0);
            msgRt.offsetMin = new Vector2(16, 96); msgRt.offsetMax = new Vector2(-16, 180);
            var msgBg = msgBgGo.GetComponent<Image>();
            msgBg.sprite = sprite; msgBg.type = Image.Type.Sliced;
            msgBg.color = new Color(0.2f, 0.55f, 1f, 0.35f);
            msgBg.enabled = false;
            var msg = Text(msgBgGo.transform, "Message", "", 21, Vector2.zero, Vector2.one, new Vector2(12, 6), new Vector2(-12, -6), TextAlignmentOptions.MidlineLeft, Color.white, FontStyles.Normal);

            var hint = Button(go.transform, "Bouton Indice", "Indice (A)", new Vector2(0f, 0), new Vector2(0.3f, 0), new Color(0.2f, 0.45f, 0.85f), Color.white, out _);
            var undo = Button(go.transform, "Bouton Annuler", "Annuler (B)", new Vector2(0.3f, 0), new Vector2(0.6f, 0), new Color(0.35f, 0.38f, 0.45f), Color.white, out _);
            var next = Button(go.transform, "Bouton Étape suivante", "Étape suivante", new Vector2(0.6f, 0), new Vector2(1f, 0), new Color(1f, 0.8f, 0.1f), new Color(0.1f, 0.1f, 0.1f), out var nextLabel);

            var panel = go.AddComponent<InstructionPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("stepTitle").objectReferenceValue = title;
            so.FindProperty("instruction").objectReferenceValue = instr;
            so.FindProperty("progress").objectReferenceValue = prog;
            so.FindProperty("message").objectReferenceValue = msg;
            so.FindProperty("messageBackground").objectReferenceValue = msgBg;
            so.FindProperty("hintButton").objectReferenceValue = hint;
            so.FindProperty("undoButton").objectReferenceValue = undo;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("nextButtonLabel").objectReferenceValue = nextLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        // Utilitaires
        // =====================================================================

        private static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, TextAlignmentOptions align, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align; t.color = color; t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        private static Button Button(Transform parent, string name, string label, Vector2 aMin, Vector2 aMax, Color bg, Color fg, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = new Vector2(aMax.x, 0);
            rt.offsetMin = new Vector2(10, 16); rt.offsetMax = new Vector2(-10, 84);
            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = bg;
            var b = go.GetComponent<Button>();
            var colors = b.colors;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            b.colors = colors;
            labelText = Text(go.transform, "Libellé", label, 22, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, fg, FontStyles.Bold);
            return b;
        }

        private static TextMeshPro Label3D(string text, Transform parent, Vector3 localPos, float size)
        {
            var go = new GameObject("Étiquette " + text.Split('\n')[0]);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(35f, 0, 0);
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size * 25f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.12f, 0.12f, 0.14f);
            t.rectTransform.sizeDelta = new Vector2(0.6f, 0.15f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Disque plat (cylindre aplati) dont la normale est « normal ».</summary>
        private static GameObject Disc(string name, Transform parent, Vector3 localPos, Vector3 normal, float diameter, Material mat)
        {
            var go = Cylinder(name, parent, localPos, new Vector3(diameter, 0.0004f, diameter), mat);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
            return go;
        }

        private static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"Champ {field} introuvable sur {target}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        private class Materials
        {
            public readonly Material Floor, Wall, Wood, Metal, Panel, Plastic, Screw, Stand, Tray, Cale, Zone, ZoneBorder, Ghost, Pulse, Arrow, SlotRing, Key, Progress;
            public readonly Mesh ArrowMesh, RotationArrowMesh;

            public Materials()
            {
                Floor = Mat("M_Sol", new Color(0.55f, 0.47f, 0.38f), 0.25f);
                Wall = Mat("M_Mur", new Color(0.88f, 0.88f, 0.86f), 0.1f);
                Wood = Mat("M_Etabli", new Color(0.72f, 0.57f, 0.38f), 0.35f);
                Metal = Mat("M_MetalNoir", new Color(0.13f, 0.13f, 0.14f), 0.55f, 0.6f);
                Panel = Mat("M_PanneauNoir", new Color(0.17f, 0.17f, 0.18f), 0.4f, 0.3f);
                Plastic = Mat("M_PlastiqueNoir", new Color(0.04f, 0.04f, 0.045f), 0.2f);
                Screw = Mat("M_VisZinguee", new Color(0.72f, 0.73f, 0.75f), 0.7f, 0.9f);
                Stand = Mat("M_Support", new Color(0.32f, 0.36f, 0.42f), 0.3f);
                Tray = Mat("M_Rangement", new Color(0.85f, 0.7f, 0.15f), 0.3f);
                Cale = Mat("M_Cale", new Color(0.3f, 0.6f, 1f, 0.35f), 0f, 0f, true, true);
                Zone = Mat("M_ZoneEvolution", new Color(0.36f, 0.42f, 0.5f), 0.1f);
                ZoneBorder = Mat("M_ZoneBord", new Color(1f, 0.8f, 0.1f), 0.2f);
                Ghost = Mat("M_Silhouette", new Color(1f, 1f, 1f, 0.25f), 0f, 0f, true, true);
                Pulse = Mat("M_Pulsation", new Color(0.2f, 0.85f, 0.35f, 0.5f), 0f, 0f, true, true);
                Arrow = Mat("M_FlecheGuidage", new Color(1f, 0.8f, 0.1f), 0f, 0f, false, true);
                SlotRing = Mat("M_RepereTrou", new Color(1f, 0.8f, 0.1f), 0f, 0f, false, true);
                Key = Mat("M_CleAllen", new Color(0.25f, 0.26f, 0.28f), 0.6f, 0.9f);
                Progress = Mat("M_ProgressionSerrage", new Color(0.2f, 0.85f, 0.35f), 0f, 0f, false, true);

                string rotPath = Root + "/Data/RotationArrowMesh.asset";
                RotationArrowMesh = AssetDatabase.LoadAssetAtPath<Mesh>(rotPath);
                if (RotationArrowMesh == null)
                {
                    RotationArrowMesh = RotationGuide.BuildArc(new Mesh { name = "RotationArrow" }, 0.026f, 0.004f, 300f, 48, true);
                    AssetDatabase.CreateAsset(RotationArrowMesh, rotPath);
                }

                string meshPath = Root + "/Data/GuidanceArrowMesh.asset";
                ArrowMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (ArrowMesh == null)
                {
                    ArrowMesh = GuidanceArrow.BuildArrowMesh();
                    AssetDatabase.CreateAsset(ArrowMesh, meshPath);
                }
            }

            private static Material Mat(string name, Color c, float smooth, float metallic = 0f, bool transparent = false, bool unlit = false)
            {
                string path = $"{Root}/Materials/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(m, path);
                }
                m.SetColor("_BaseColor", c);
                if (!unlit) { m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metallic); }
                if (transparent)
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                EditorUtility.SetDirty(m);
                return m;
            }
        }
    }
}
