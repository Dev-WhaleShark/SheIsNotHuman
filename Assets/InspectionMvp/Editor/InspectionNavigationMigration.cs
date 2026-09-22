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
    /// <summary>기존 검수 씬의 화면 전환과 왜곡 보정 레이캐스터만 갱신하는 Odin 도구다. 씬을 재생성하지 않는다.</summary>
    public sealed class InspectionNavigationMigration : OdinEditorWindow
    {
        private const string ScenePath = "Assets/Scenes/PerspectiveCubeViewPrototype.unity";

        [MenuItem("Tools/Inspection MVP/Navigation Migration")]
        private static void OpenWindow() => GetWindow<InspectionNavigationMigration>("Inspection Navigation");

        /// <summary>지정 씬의 면별 입력 게이트와 가장자리 버튼을 연결하고 씬을 저장한다.</summary>
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
            // 면 이름이 모호한 캔버스에 입력을 열지 않도록 변경 전에 모든 캔버스의 소속을 확정한다.
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
            // 이전 기본값만 갱신하여 이후 사용자가 조정한 임계값은 재실행해도 보존한다.
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

        // 정확한 조상 이름으로 소속을 찾는다. 추측한 면으로 입력을 허용하면 반대편 UI도 클릭될 수 있다.
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

        // 일반/보정 레이캐스터가 함께 입력을 처리하지 않도록 하나만 유지하고 기존 차폐 설정은 이관한다.
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
            Vector4 extent = Vector4.zero; // RectTransform 단위의 왼쪽·아래·오른쪽·위 확장량.
            foreach (var effect in effects)
            {
                // 현재 숨겨진 모달도 열렸을 때 효과가 보이므로 활성 효과의 경계 계산에 포함한다.
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

        // 기존 버튼은 재사용하고 처음부터 클릭 가능하지 않도록 그룹을 숨김 상태로 연결한다.
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

        /// <summary>좌표 변환의 수치 기준값을 검사한다. 실제 화면이나 포인터 상호작용 검증을 대신하지 않는다.</summary>
        [Button("Check shader numeric reference vectors")]
        public static void CheckShaderCoordinates()
        {
            if (!LensDistortionCoordinates.ValidateShaderReferenceVectors(out string report))
                throw new InvalidOperationException(report);
            Debug.Log(report);
        }
    }
}
