using System;
using System.Collections.Generic;
using DeToutesPieces.Core;
using DeToutesPieces.Feedback;
using DeToutesPieces.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace DeToutesPieces.UI
{
    /// <summary>
    /// Bascule entre le mode VR (casque ou simulateur XR) et le mode clavier-souris (touche B ou F2).
    /// En mode clavier-souris : le rig XR et le simulateur sont désactivés, une caméra de bureau prend
    /// le relais (regard, déplacement, saisie à la souris), les panneaux deviennent cliquables à la souris
    /// et une aide des commandes s'affiche à l'écran. La logique du montage est la même dans les deux modes.
    /// </summary>
    public class InputModeSwitcher : MonoBehaviour
    {
        public enum Mode { VR, ClavierSouris }

        [Tooltip("Dans une version compilée, démarre en clavier-souris si aucun casque n'est actif.")]
        [SerializeField] private bool desktopWhenNoHeadset = true;
        [Tooltip("Force le démarrage en mode clavier-souris (y compris dans l'éditeur).")]
        [SerializeField] private bool startInDesktop;
        [SerializeField] private float standingEyeHeight = 1.62f;
        [SerializeField] private float seatedEyeHeight = 1.25f;

        public static InputModeSwitcher Instance { get; private set; }
        public Mode Current { get; private set; } = Mode.VR;
        public event Action<Mode> ModeChanged;

        private GameObject xrOrigin, simulator, desktopRig;
        private bool simulatorWasActive;
        private Camera xrCamera, desktopCamera;
        private DesktopCameraController desktopLook;
        private InputAction toggleAction, helpAction;
        private RectTransform hud, helpPanel;
        private TMP_Text helpText;
        private float nextCanvasPass;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (DesktopInteractor.UiHitDistance == UiDistance) DesktopInteractor.UiHitDistance = null;
            SessionSettings.Changed -= OnSettings;
        }

        private void OnEnable()
        {
            toggleAction = new InputAction("Basculer le mode", InputActionType.Button, "<Keyboard>/b");
            toggleAction.AddBinding("<Keyboard>/f2");
            toggleAction.performed += _ => Toggle();
            toggleAction.Enable();
            helpAction = new InputAction("Aide clavier", InputActionType.Button, "<Keyboard>/h");
            helpAction.performed += _ => { if (Current == Mode.ClavierSouris && helpPanel != null) helpPanel.gameObject.SetActive(!helpPanel.gameObject.activeSelf); };
            helpAction.Enable();
        }

        private void OnDisable()
        {
            toggleAction?.Dispose();
            helpAction?.Dispose();
        }

        private void Start()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (xrOrigin == null && root.name.StartsWith("XR Origin")) xrOrigin = root;
                if (simulator == null && root.name.StartsWith("XR Interaction Simulator")) simulator = root;
            }
            if (xrOrigin != null) xrCamera = xrOrigin.GetComponentInChildren<Camera>(true);
            DesktopInteractor.UiHitDistance = UiDistance;
            SessionSettings.Changed += OnSettings;

            bool desktop = startInDesktop;
#if !UNITY_EDITOR
            if (desktopWhenNoHeadset && !UnityEngine.XR.XRSettings.isDeviceActive) desktop = true;
#endif
            if (desktop) SetMode(Mode.ClavierSouris, false);
        }

        public void Toggle() => SetMode(Current == Mode.VR ? Mode.ClavierSouris : Mode.VR, true);

        public void SetMode(Mode mode, bool announce)
        {
            if (mode == Mode.ClavierSouris)
            {
                EnsureDesktopRig();
                var from = xrCamera != null && xrCamera.isActiveAndEnabled ? xrCamera.transform : null;
                if (xrOrigin != null) xrOrigin.SetActive(false);   // relâche aussi les pièces tenues en VR
                if (simulator != null)
                {
                    simulatorWasActive = simulator.activeSelf;
                    simulator.SetActive(false);
                }
                desktopRig.SetActive(true);
                PlaceDesktopCamera(from);
            }
            else
            {
                if (desktopRig != null) desktopRig.SetActive(false);
                if (xrOrigin != null) xrOrigin.SetActive(true);
                if (simulator != null && simulatorWasActive) simulator.SetActive(true);
            }

            Current = mode;
            ConfigureUI();
            if (hud != null) hud.gameObject.SetActive(mode == Mode.ClavierSouris);
            ReplaceScreens(mode == Mode.ClavierSouris);
            ModeChanged?.Invoke(mode);

            if (!announce) return;
            if (mode == Mode.ClavierSouris)
                FeedbackBus.Emit(FeedbackKind.Info, null, "Mode clavier-souris : clic gauche pour saisir, clic droit maintenu pour regarder. B pour revenir en VR, H pour l'aide.");
            else
                FeedbackBus.Emit(FeedbackKind.Info, null, UnityEngine.XR.XRSettings.isDeviceActive || Application.isEditor
                    ? "Mode VR. Appuyez sur B pour repasser en clavier-souris."
                    : "Mode VR : aucun casque n'est détecté. Appuyez sur B pour revenir au clavier-souris.");
        }

        private void Update()
        {
            // Les écrans construits à l'exécution (accueil, pause, bilan, fenêtre d'outils) sont rattrapés ici.
            if (Current == Mode.ClavierSouris && Time.unscaledTime >= nextCanvasPass)
            {
                nextCanvasPass = Time.unscaledTime + 0.5f;
                ConfigureUI();
            }
        }

        /// <summary>Ramène devant la nouvelle caméra les écrans affichés (accueil, pause, bilan).</summary>
        private static void ReplaceScreens(bool desktop)
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (!(mb is WelcomeScreen || mb is PauseMenu || mb is SummaryScreen)) continue;
                var t = mb.transform;
                if (t.childCount > 0 && t.GetChild(0).gameObject.activeSelf) RuntimeUI.PlaceInFrontOfUser(t, desktop ? 1.0f : 0.75f, desktop ? -0.12f : -0.05f);
            }
        }

        // ---------------- Caméra de bureau ----------------

        private void EnsureDesktopRig()
        {
            if (desktopRig != null) return;
            desktopRig = new GameObject("Caméra clavier-souris");
            SceneManager.MoveGameObjectToScene(desktopRig, gameObject.scene);
            desktopRig.SetActive(false);
            desktopRig.tag = "MainCamera";
            desktopCamera = desktopRig.AddComponent<Camera>();
            desktopCamera.fieldOfView = 70f;
            desktopCamera.nearClipPlane = 0.02f;
            desktopCamera.farClipPlane = 60f;
            if (xrCamera != null)
            {
                desktopCamera.clearFlags = xrCamera.clearFlags;
                desktopCamera.backgroundColor = xrCamera.backgroundColor;
                desktopCamera.cullingMask = xrCamera.cullingMask;
            }
            var data = desktopCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            desktopRig.AddComponent<AudioListener>();
            desktopLook = desktopRig.AddComponent<DesktopCameraController>();
            desktopRig.AddComponent<DesktopInteractor>();
            BuildHud();
        }

        private void PlaceDesktopCamera(Transform from)
        {
            float eye = SessionSettings.Posture == WorkPosture.Seated ? seatedEyeHeight : standingEyeHeight;
            if (from != null && from.position.y > 0.5f)
                desktopLook.SetPose(new Vector3(from.position.x, eye, from.position.z), from.eulerAngles.y, 8f);
            else
                desktopLook.SetPose(new Vector3(0f, eye, -0.1f), 0f, 8f);
        }

        private void OnSettings()
        {
            if (Current == Mode.ClavierSouris && desktopLook != null)
                desktopLook.SetEyeHeight(SessionSettings.Posture == WorkPosture.Seated ? seatedEyeHeight : standingEyeHeight);
        }

        // ---------------- Interface à la souris ----------------

        private void ConfigureUI()
        {
            bool desktop = Current == Mode.ClavierSouris;
            var eventCam = desktop ? desktopCamera : xrCamera;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (c.renderMode != RenderMode.WorldSpace || !c.isRootCanvas) continue;
                if (eventCam != null) c.worldCamera = eventCam;
                var gr = c.GetComponent<GraphicRaycaster>();
                if (gr == null && desktop) gr = c.gameObject.AddComponent<GraphicRaycaster>();
                if (gr != null) gr.enabled = desktop;
            }
            var es = EventSystem.current;
            if (es != null && es.TryGetComponent<XRUIInputModule>(out var module)) module.enableMouseInput = desktop;
        }

        /// <summary>Distance du premier élément d'interface sous le pointeur (souris), infini sinon.</summary>
        private float UiDistance(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return float.PositiveInfinity;
            var data = new PointerEventData(es) { position = screen };
            uiHits.Clear();
            es.RaycastAll(data, uiHits);
            float best = float.PositiveInfinity;
            foreach (var h in uiHits)
                if (h.module is GraphicRaycaster && h.gameObject != null) best = Mathf.Min(best, h.distance > 0f ? h.distance : 0.01f);
            return best;
        }

        // ---------------- Aide à l'écran ----------------

        private void BuildHud()
        {
            var go = new GameObject("Aide clavier-souris", typeof(RectTransform));
            go.transform.SetParent(desktopRig.transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            hud = (RectTransform)go.transform;

            // Pastille de mode, en haut au centre.
            var chip = Panel(hud, "Pastille de mode", new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(520f, 46f));
            Label(chip, "MODE CLAVIER-SOURIS   ·   B : passer en VR   ·   H : aide", 17f, UITheme.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);

            // Aide des commandes, en bas à gauche.
            helpPanel = Panel(hud, "Commandes", new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(560f, 330f));
            helpText = Label(helpPanel, BuildHelp(), 17f, UITheme.TextMain, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            helpText.rectTransform.offsetMin = new Vector2(22f, 16f);
            helpText.rectTransform.offsetMax = new Vector2(-22f, -18f);
            helpText.lineSpacing = 8f;

            // Point de visée, utile quand le regard est capturé (clic droit).
            var dot = new GameObject("Point de visée", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dot.transform;
            drt.SetParent(hud, false);
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(6f, 6f);
            var dimg = dot.GetComponent<Image>();
            dimg.sprite = UITheme.ButtonSprite;
            dimg.color = new Color(1f, 1f, 1f, 0.7f);
            dimg.raycastTarget = false;
        }

        private static string KeyName(Key k)
        {
            var kb = Keyboard.current;
            string n = kb != null ? kb[k].displayName : k.ToString();
            return string.IsNullOrEmpty(n) ? k.ToString() : n.ToUpperInvariant();
        }

        private static string BuildHelp()
        {
            string move = KeyName(Key.W) + KeyName(Key.A) + KeyName(Key.S) + KeyName(Key.D);
            string down = KeyName(Key.Q), up = KeyName(Key.E);
            const string y = "<color=#FFCC1A>", b = "<color=#9EC5F5>", e = "</color>";
            return
                $"{y}<b>COMMANDES</b>{e}\n" +
                $"{b}Regarder{e}   clic droit maintenu + souris\n" +
                $"{b}Se déplacer{e}   {move} ou flèches · {down} / {up} : descendre / monter · Maj : plus vite\n" +
                $"{b}Saisir / poser{e}   clic gauche maintenu, relâcher pour poser\n" +
                $"{b}Distance{e}   molette · {b}Tourner{e}   R / T / Y (quart de tour), Ctrl + souris\n" +
                $"{b}Clé Allen{e}   visez la vis, puis molette vers l'avant pour serrer\n" +
                $"{b}Panneaux{e}   clic gauche sur les boutons\n" +
                $"{b}Raccourcis{e}   F indice · U annuler · Entrée suivant · O outils · Échap pause";
        }

        private static RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            UITheme.StyleBackground(img, new Color(UITheme.Panel.r, UITheme.Panel.g, UITheme.Panel.b, 0.88f));
            img.raycastTarget = false;
            UITheme.Decorate(rt, null, false);
            return rt;
        }

        private static TMP_Text Label(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
        {
            var go = new GameObject("Texte", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12f, 4f);
            rt.offsetMax = new Vector2(-12f, -4f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            return t;
        }
    }
}
