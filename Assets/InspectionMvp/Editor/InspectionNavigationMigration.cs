using System;
using System.Linq;
using SheIsNotHuman.CubeScreen;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheIsNotHuman.InspectionMvp.Editor
{
    /// <summary>Targeted, repeatable migration; never rebuilds the inspection scene.</summary>
    public sealed class InspectionNavigationMigration : OdinEditorWindow
    {
        private const string ScenePath = "Assets/Scenes/PerspectiveCubeViewPrototype.unity";

        [MenuItem("Tools/Inspection MVP/Navigation Migration")]
        private static void OpenWindow() => GetWindow<InspectionNavigationMigration>("Inspection Navigation");

        [Button("Apply navigation and corrected raycasters")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Navigation migration requires Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open the existing PerspectiveCubeViewPrototype scene first.");
            var navigation = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PerspectiveCubeViewController>(true)).Single();
            var camera = navigation.ViewCamera != null ? navigation.ViewCamera : navigation.GetComponentInChildren<Camera>(true);
            if (camera == null) throw new InvalidOperationException("Perspective view camera is missing.");
            var faces = scene.GetRootGameObjects().Single(root => root.name == "CubeFaces");
            var canvases = faces.GetComponentsInChildren<Canvas>(true);
            // Resolve all ownership before changing anything; ambiguous names must not open an input gate.
            var faceCanvases = canvases.Select(canvas => new
            {
                Canvas = canvas,
                Face = ResolveFace(canvas.transform, faces.transform)
            }).ToArray();
            foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
                if (!faceCanvases.Any(entry => entry.Face == face))
                    throw new InvalidOperationException($"Face_{face} has no Canvas to configure.");
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restore inspection navigation");
            foreach (var entry in faceCanvases)
            {
                var canvas = entry.Canvas;
                Undo.RecordObject(canvas, "Assign perspective UI camera");
                canvas.worldCamera = camera;
                InstallRaycaster(canvas, navigation, entry.Face);
                foreach (var button in canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    MatchVisibleGraphicBounds(button.targetGraphic);
            }

            var overlays = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CubeNavigationOverlay>(true)).ToArray();
            if (overlays.Length > 1)
                throw new InvalidOperationException("Multiple navigation overlays found; choose the existing overlay before migrating.");
            CubeNavigationOverlay overlay;
            Canvas overlayCanvas;
            if (overlays.Length == 0)
            {
                var go = new GameObject("InspectionNavigationOverlay", typeof(RectTransform), typeof(Canvas),
                    typeof(UnityEngine.UI.GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(go, "Create edge navigation overlay");
                overlayCanvas = go.GetComponent<Canvas>();
                overlay = Undo.AddComponent<CubeNavigationOverlay>(go);
            }
            else
            {
                overlay = overlays[0];
                overlayCanvas = overlay.GetComponentInParent<Canvas>();
                if (overlayCanvas == null)
                    throw new InvalidOperationException("Existing navigation overlay has no Canvas.");
            }
            Undo.RecordObject(overlayCanvas, "Configure edge overlay");
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 100;
            if (overlayCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                Undo.AddComponent<UnityEngine.UI.GraphicRaycaster>(overlayCanvas.gameObject);
            var font = faces.GetComponentsInChildren<TextMeshProUGUI>(true).Select(label => label.font)
                .FirstOrDefault(candidate => candidate != null);
            var serialized = new SerializedObject(overlay);
            serialized.FindProperty("controller").objectReferenceValue = null;
            serialized.FindProperty("perspectiveController").objectReferenceValue = navigation;
            serialized.FindProperty("canvasRect").objectReferenceValue = overlayCanvas.transform;
            // Only upgrade the previous default; later artist-authored thresholds survive reruns.
            var revealDistance = serialized.FindProperty("edgeRevealDistance");
            if (revealDistance.floatValue == 44f) revealDistance.floatValue = 96f;
            ConfigureButton(serialized, overlay.transform, "left", "<", font, new Vector2(0, 0), new Vector2(0, 1), new Vector2(.5f, .5f), new Vector2(22, 0), new Vector2(44, 0));
            ConfigureButton(serialized, overlay.transform, "right", ">", font, new Vector2(1, 0), new Vector2(1, 1), new Vector2(.5f, .5f), new Vector2(-22, 0), new Vector2(44, 0));
            ConfigureButton(serialized, overlay.transform, "up", "^", font, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, .5f), new Vector2(0, -22), new Vector2(0, 44));
            ConfigureButton(serialized, overlay.transform, "down", "v", font, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, .5f), new Vector2(0, 22), new Vector2(0, 44));
            serialized.ApplyModifiedProperties();
            overlay.gameObject.SetActive(true);
            overlay.enabled = true;
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Navigation migration saved: {canvases.Length} face canvases inspected; edge hover reveals buttons, click rotates.");
        }

        private static CubeFace ResolveFace(Transform target, Transform facesRoot)
        {
            for (var current = target; current != null && current != facesRoot; current = current.parent)
            {
                switch (current.name)
                {
                    case "Face_Front": return CubeFace.Front;
                    case "Face_Back": return CubeFace.Back;
                    case "Face_Left": return CubeFace.Left;
                    case "Face_Right": return CubeFace.Right;
                    case "Face_Top": return CubeFace.Top;
                    case "Face_Bottom": return CubeFace.Bottom;
                }
            }
            throw new InvalidOperationException($"Canvas {target.name} has no exact Face_<CubeFace> ancestor.");
        }

        private static void InstallRaycaster(Canvas canvas, PerspectiveCubeViewController controller, CubeFace face)
        {
            var existing = canvas.GetComponents<UnityEngine.UI.GraphicRaycaster>();
            var corrected = existing.OfType<DistortionCorrectedGraphicRaycaster>().FirstOrDefault();
            bool ignoreReversed = existing.Length == 0 || existing[0].ignoreReversedGraphics;
            var blocking = existing.Length == 0 ? UnityEngine.UI.GraphicRaycaster.BlockingObjects.None : existing[0].blockingObjects;
            LayerMask mask = existing.Length == 0 ? (LayerMask)(-1) : existing[0].blockingMask;
            foreach (var raycaster in existing)
                if (raycaster != corrected) Undo.DestroyObjectImmediate(raycaster);
            if (corrected == null) corrected = Undo.AddComponent<DistortionCorrectedGraphicRaycaster>(canvas.gameObject);
            Undo.RecordObject(corrected, "Configure lens-corrected raycaster");
            corrected.ignoreReversedGraphics = ignoreReversed;
            corrected.blockingObjects = blocking;
            corrected.blockingMask = mask;
            corrected.ConfigureFaceGate(controller, face);
            corrected.enabled = true;
            EditorUtility.SetDirty(corrected);
            if (PrefabUtility.IsPartOfPrefabInstance(corrected))
                PrefabUtility.RecordPrefabInstancePropertyModifications(corrected);
        }

        private static void MatchVisibleGraphicBounds(UnityEngine.UI.Graphic graphic)
        {
            if (graphic == null) return;
            var effects = graphic.GetComponents<UnityEngine.UI.Shadow>();
            if (effects.Length == 0) return;
            Vector4 extent = Vector4.zero; // Left, bottom, right, top in RectTransform units.
            foreach (var effect in effects)
            {
                // Include currently hidden modal graphics: their enabled effects render when opened.
                if (!effect.enabled || effect.effectColor.a <= 0) continue;
                Vector2 distance = effect.effectDistance;
                if (effect is UnityEngine.UI.Outline)
                    extent += new Vector4(Mathf.Abs(distance.x), Mathf.Abs(distance.y),
                        Mathf.Abs(distance.x), Mathf.Abs(distance.y));
                else
                    extent += new Vector4(Mathf.Max(0, -distance.x), Mathf.Max(0, -distance.y),
                        Mathf.Max(0, distance.x), Mathf.Max(0, distance.y));
            }
            Undo.RecordObject(graphic, "Match visible button outline hit area");
            graphic.raycastPadding = -extent;
            EditorUtility.SetDirty(graphic);
            if (PrefabUtility.IsPartOfPrefabInstance(graphic))
                PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
        }

        private static void ConfigureButton(SerializedObject overlay, Transform parent, string direction,
            string glyph, TMP_FontAsset font, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            var buttonProperty = overlay.FindProperty(direction + "Button");
            var button = buttonProperty.objectReferenceValue as UnityEngine.UI.Button;
            if (button == null)
            {
                var go = new GameObject(char.ToUpperInvariant(direction[0]) + direction.Substring(1) + "Button",
                    typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(CanvasGroup));
                Undo.RegisterCreatedObjectUndo(go, "Create edge arrow");
                go.transform.SetParent(parent, false);
                button = go.GetComponent<UnityEngine.UI.Button>();
                var rect = (RectTransform)go.transform;
                rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
                rect.anchoredPosition = position; rect.sizeDelta = size;
                var image = go.GetComponent<UnityEngine.UI.Image>();
                image.color = new Color(.08f, .12f, .14f, .68f);
                button.targetGraphic = image;
                var labelObject = new GameObject("Arrow", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(go.transform, false);
                var label = labelObject.GetComponent<TextMeshProUGUI>();
                label.font = font; label.text = glyph; label.fontSize = 28; label.color = Color.white;
                label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                buttonProperty.objectReferenceValue = button;
            }
            var group = button.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(button.gameObject);
            Undo.RecordObject(group, "Initialize hidden edge arrow");
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            overlay.FindProperty(direction + "Group").objectReferenceValue = group;
        }

        [Button("Check shader numeric reference vectors")]
        public static void CheckShaderCoordinates()
        {
            if (!LensDistortionCoordinates.ValidateShaderReferenceVectors(out string report))
                throw new InvalidOperationException(report);
            Debug.Log(report);
        }
    }
}
