using System;
using System.Collections.Generic;
using SheIsNotHuman.CubeScreen;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp.Editor
{
    /// <summary>기존 책상을 부분 이관하는 Odin 도구다. 저장된 진행 설정·방문자·타이밍과 무관한 씬 오브젝트는 보존한다.</summary>
    public sealed class InspectionDeskMigration : OdinEditorWindow
    {
        private const string Prefabs = "Assets/InspectionMvp/Prefabs";
        private const string ScenePath = "Assets/Scenes/PerspectiveCubeViewPrototype.unity";
        private static readonly Color Ink = new Color(.12f, .16f, .19f);

        /// <summary>문서 프리팹 연결·물품 입력·원본 확대 계층을 구성한다. 에셋은 저장하지만 씬 저장은 검토 후 별도로 한다.</summary>
        [MenuItem("Tools/Inspection MVP/Migrate desk interactions")]
        [Button("Migrate current desk")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Desk migration requires Edit Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open the existing PerspectiveCubeViewPrototype scene first.");
            var view = UnityEngine.Object.FindAnyObjectByType<InspectionMvpView>();
            if (view == null || view.documentsRoot == null || view.modalPanel == null)
                throw new InvalidOperationException("Existing MVP wiring is required; this migration never rebuilds the scene.");
            if (!AssetDatabase.IsValidFolder(Prefabs)) AssetDatabase.CreateFolder("Assets/InspectionMvp", "Prefabs");
            Undo.RecordObject(view, "Migrate desk interactions");
            var desk = (RectTransform)view.documentsRoot.parent;
            var font = view.identitySummary.font;
            // 원근 카메라에서 면 가장자리가 일부 잘리므로 명목 해상도보다 안쪽을 작업 영역으로 쓴다.
            // 물품 전체가 대사 아래의 실제 보이는 영역에 머물게 한다.
            var bounds = desk.Find("VisibleItemBounds") as RectTransform;
            if (bounds == null) bounds = Rect("VisibleItemBounds", desk, Vector2.zero, Vector2.zero);
            ConfigureRect(bounds, new Vector2(0, -102.5f), new Vector2(1080, 415));
            ConfigureRect(view.hint.rectTransform, new Vector2(0, -215), new Vector2(1100, 32));

            // 기존 그래픽을 문서의 자식으로 옮기면서 월드 배치와 직렬화된 참조를 유지한다.
            view.identityDocument = Ensure<IdentityDocumentView>(view.identityButton.gameObject);
            Reparent(view.identitySummary.transform, view.identityButton.transform);
            view.identityDocument.content = view.identitySummary;
            view.identityDocument.expanded = false;
            var identityItem = Ensure<DeskInspectableItem>(view.identityButton.gameObject);
            identityItem.kind = DeskItemKind.Document;
            Connect(view.identityButton.gameObject, "IdentityDocument");

            view.orderDocument = Ensure<OrderDocumentView>(view.orderButton.gameObject);
            Reparent(view.orderSummary.transform, view.orderButton.transform);
            view.orderDocument.content = view.orderSummary;
            view.orderDocument.expanded = false;
            var orderItem = Ensure<DeskInspectableItem>(view.orderButton.gameObject);
            orderItem.kind = DeskItemKind.Document;
            Connect(view.orderButton.gameObject, "OrderDocument");

            var identityPaper = view.expandedIdentityDocument != null ? view.expandedIdentityDocument.transform
                : view.modalPanel.Find("IdentityPaper");
            var orderPaper = view.expandedOrderDocument != null ? view.expandedOrderDocument.transform
                : view.modalPanel.Find("OrderPaper");
            if (identityPaper == null || orderPaper == null)
                throw new InvalidOperationException("Existing expanded document paper nodes are missing.");
            Reparent(view.identityDetail.transform, identityPaper);
            Reparent(view.identityPortrait.transform, identityPaper);
            view.expandedIdentityDocument = Ensure<IdentityDocumentView>(identityPaper.gameObject);
            view.expandedIdentityDocument.content = view.identityDetail;
            view.expandedIdentityDocument.portrait = view.identityPortrait;
            view.expandedIdentityDocument.expanded = true;
            Connect(identityPaper.gameObject, "IdentityDocumentExpanded");
            Reparent(view.orderDetail.transform, orderPaper);
            view.expandedOrderDocument = Ensure<OrderDocumentView>(orderPaper.gameObject);
            view.expandedOrderDocument.content = view.orderDetail;
            view.expandedOrderDocument.expanded = true;
            Connect(orderPaper.gameObject, "OrderDocumentExpanded");

            var items = new List<DeskInspectableItem> { identityItem, orderItem };
            for (int i = 0; i < 3; i++)
            {
                string name = "DummyItem" + (i + 1);
                var existing = desk.Find(name);
                Vector2 size = i == 0 ? new Vector2(180, 50) : i == 1 ? new Vector2(140, 65) : new Vector2(210, 40);
                var rect = existing != null ? (RectTransform)existing
                    : Rect(name, desk, Vector2.zero, size);
                // 재실행 시 이미 이관된 소품에도 배치를 적용하여 저장된 기본 위치를 정렬한다.
                ConfigureRect(rect, new Vector2((i - 1) * 370, -270), size);
                var graphic = Ensure<UnityEngine.UI.Image>(rect.gameObject);
                Color color = i == 0 ? new Color(.72f, .77f, .78f)
                    : i == 1 ? new Color(.80f, .75f, .68f) : new Color(.73f, .78f, .69f);
                graphic.color = color; graphic.raycastTarget = true;
                var item = Ensure<DeskInspectableItem>(rect.gameObject);
                item.kind = DeskItemKind.Dummy;
                item.displayName = "물품 " + (i + 1);
                item.displayColor = color;
                var label = rect.Find("Label");
                if (label == null) Label("Label", rect, item.displayName, Vector2.zero, size - new Vector2(10, 4), font, 24);
                else ConfigureRect((RectTransform)label, Vector2.zero, size - new Vector2(10, 4));
                items.Add(item);
            }
            foreach (var item in items)
            {
                item.host = view; item.deskBounds = bounds;
                EditorUtility.SetDirty(item);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
            view.deskItems = items.ToArray();
            BuildDummyPanel(view, font);
            ConfigureFocus(view);
            // 자식을 옮기는 중에는 형제 인덱스가 달라지므로 모든 배치가 끝난 뒤 차단막을 맨 위로 확정한다.
            Undo.RecordObject(view.modalShield.transform, "Keep modal shield above desk items");
            view.modalShield.transform.SetAsLastSibling();
            foreach (var doc in new MonoBehaviour[] { view.identityDocument, view.orderDocument,
                         view.expandedIdentityDocument, view.expandedOrderDocument })
            {
                EditorUtility.SetDirty(doc);
                PrefabUtility.RecordPrefabInstancePropertyModifications(doc);
            }
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            // 씬 저장은 이관 결과를 확인한 단일 Editor 담당자가 수행한다.
            Debug.Log("Desk migrated in place: 4 connected document prefab instances and 5 shared desk items; flow/roster/timing preserved.", view);
        }

        /// <summary>문서와 책상 배치를 유지한 채 동일 원본을 확대하는 계층과 제어부만 추가·갱신한다.</summary>
        [MenuItem("Tools/Inspection MVP/Migrate original object focus")]
        [Button("Migrate original object focus only")]
        public static void ApplyFocus()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Focus migration requires Edit Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open the existing PerspectiveCubeViewPrototype scene first.");
            var view = UnityEngine.Object.FindAnyObjectByType<InspectionMvpView>();
            if (view == null || view.identityDocument == null || view.orderDocument == null)
                throw new InvalidOperationException("Run the existing desk migration before focus migration.");
            ConfigureFocus(view);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Original object focus migrated in place; compact prefab instances and saved desk positions preserved.", view);
        }

        // 런타임은 복제 문서가 아니라 기존 인스턴스를 이 캔버스로 옮겼다가 되돌린다.
        private static void ConfigureFocus(InspectionMvpView view)
        {
            Undo.RecordObject(view, "Configure original object focus");
            ConfigureIdentityPortrait(view.identityDocument);
            var existing = view.transform.Find("FocusCanvas") as RectTransform;
            var root = existing != null ? existing : Rect("FocusCanvas", view.transform, Vector2.zero, new Vector2(1200, 675));
            view.focusCanvas = Ensure<Canvas>(root.gameObject);
            view.focusCanvas.renderMode = RenderMode.WorldSpace;
            view.focusCanvas.worldCamera = view.navigation.ViewCamera;
            view.focusCanvas.overrideSorting = true;
            view.focusCanvas.sortingOrder = 100;
            var raycaster = Ensure<DistortionCorrectedGraphicRaycaster>(root.gameObject);
            raycaster.ConfigureFaceGate(view.navigation, CubeFace.Bottom);
            EditorUtility.SetDirty(view.focusCanvas); EditorUtility.SetDirty(raycaster);
            // 기존 차단막을 확대용 월드 평면으로 옮기고 캔버스의 마지막 형제로 유지한다.
            Reparent(view.modalShield.transform, root);
            ConfigureRect((RectTransform)view.modalShield.transform, Vector2.zero, new Vector2(10000, 10000));
            view.modalShield.transform.localScale = Vector3.one;
            view.modalShield.transform.localRotation = Quaternion.identity;
            ((RectTransform)view.modalShield.transform).anchoredPosition3D = Vector3.zero;
            var items = view.modalShield.transform.Find("FocusedOriginals") as RectTransform;
            view.focusItemsRoot = items != null ? items : Rect("FocusedOriginals", view.modalShield.transform, Vector2.zero, new Vector2(1200, 675));
            var controls = view.modalShield.transform.Find("FocusControls") as RectTransform;
            view.focusControls = controls != null ? controls : Rect("FocusControls", view.modalShield.transform, Vector2.zero, new Vector2(1200, 675));
            ConfigureControl(view.closeButton, view.focusControls, new Vector2(480, 260), new Vector2(130, 60));
            ConfigureControl(view.passButton, view.focusControls, new Vector2(-250, -240), new Vector2(430, 72));
            ConfigureControl(view.nonPassButton, view.focusControls, new Vector2(250, -240), new Vector2(430, 72));
            view.dummyCloseButton = view.closeButton;
            view.dialogueGroup = Ensure<CanvasGroup>(view.dialogueButton.gameObject);
            view.modalPanel.gameObject.SetActive(false);
            if (view.dummyPanel != null) view.dummyPanel.SetActive(false);
            view.modalShield.transform.SetAsLastSibling();
            view.focusControls.SetAsLastSibling();
            view.modalShield.SetActive(false);
            root.gameObject.SetActive(false);
        }

        private static void ConfigureIdentityPortrait(IdentityDocumentView document)
        {
            // 초상을 원본 문서의 영구 자식으로 두어 확대 중에도 같은 인스턴스를 따라가게 한다.
            var portraitRect = document.transform.Find("FocusedPortrait") as RectTransform;
            if (portraitRect == null)
                portraitRect = Rect("FocusedPortrait", document.transform, new Vector2(145, 0), new Vector2(74, 78));
            var portraitImage = Ensure<UnityEngine.UI.Image>(portraitRect.gameObject);
            portraitImage.color = new Color(.74f, .76f, .75f);
            portraitImage.raycastTarget = false;
            var head = portraitRect.Find("Head") as RectTransform;
            if (head == null) head = Rect("Head", portraitRect, new Vector2(0, 62), new Vector2(52, 52));
            var headImage = Ensure<UnityEngine.UI.Image>(head.gameObject);
            headImage.color = Ink;
            headImage.raycastTarget = false;
            portraitRect.gameObject.SetActive(false);
            Undo.RecordObject(document, "Bind original identity portrait");
            document.portrait = portraitImage;
            EditorUtility.SetDirty(document);
            if (!PrefabUtility.IsPartOfPrefabInstance(document)) return;
            PrefabUtility.RecordPrefabInstancePropertyModifications(document);
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(document.gameObject);
            if (path != Prefabs + "/IdentityDocument.prefab") return;
            // 새 초상과 그 참조만 프리팹에 반영하여 씬 전용 host/desk 오버라이드를 보존한다.
            if (PrefabUtility.IsAddedGameObjectOverride(portraitRect.gameObject))
                PrefabUtility.ApplyAddedGameObject(portraitRect.gameObject, path, InteractionMode.AutomatedAction);
            var serialized = new SerializedObject(document);
            PrefabUtility.ApplyPropertyOverride(serialized.FindProperty("portrait"), path, InteractionMode.AutomatedAction);
        }

        private static void ConfigureControl(UnityEngine.UI.Button button, RectTransform parent, Vector2 position, Vector2 size)
        {
            Reparent(button.transform, parent);
            var rect = (RectTransform)button.transform;
            ConfigureRect(rect, position, size);
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            rect.anchoredPosition3D = new Vector3(position.x, position.y, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }

        // 예상한 프리팹 연결은 재사용하고 다른 연결은 덮어쓰지 않아 기존 에셋 관계를 보존한다.
        private static void Connect(GameObject instance, string name)
        {
            string path = Prefabs + "/" + name + ".prefab";
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) == path) return;
            if (PrefabUtility.IsPartOfPrefabInstance(instance))
                throw new InvalidOperationException("Unexpected prefab association on " + instance.name + "; preserving it.");
            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, path, InteractionMode.AutomatedAction);
        }

        private static void BuildDummyPanel(InspectionMvpView view, TMP_FontAsset font)
        {
            var parent = view.modalShield.transform;
            var existing = parent.Find("DummyPanel");
            var panel = existing != null ? (RectTransform)existing
                : Rect("DummyPanel", parent, Vector2.zero, new Vector2(600, 440));
            var background = Ensure<UnityEngine.UI.Image>(panel.gameObject);
            background.color = new Color(.92f, .93f, .92f); background.raycastTarget = true;
            view.dummyPanel = panel.gameObject;
            view.dummyTitle = panel.Find("Title")?.GetComponent<TMP_Text>()
                ?? Label("Title", panel, "물품", new Vector2(0, 155), new Vector2(500, 60), font, 32);
            var icon = panel.Find("Item");
            var iconRect = icon != null ? (RectTransform)icon : Rect("Item", panel, new Vector2(0, 5), new Vector2(180, 170));
            view.dummyImage = Ensure<UnityEngine.UI.Image>(iconRect.gameObject);
            view.dummyImage.raycastTarget = false;
            var close = panel.Find("CloseButton");
            var closeRect = close != null ? (RectTransform)close : Rect("CloseButton", panel, new Vector2(0, -155), new Vector2(220, 60));
            var closeImage = Ensure<UnityEngine.UI.Image>(closeRect.gameObject);
            closeImage.color = new Color(.74f, .76f, .75f); closeImage.raycastTarget = true;
            view.dummyCloseButton = Ensure<UnityEngine.UI.Button>(closeRect.gameObject);
            view.dummyCloseButton.targetGraphic = closeImage;
            view.dummyCloseButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            if (closeRect.Find("Label") == null) Label("Label", closeRect, "닫기", Vector2.zero, new Vector2(200, 50), font, 27);
            panel.gameObject.SetActive(false);
        }

        private static T Ensure<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            // Unity의 네이티브 객체가 사라진 래퍼는 CLR null이 아닐 수 있으므로 Unity의 null 비교를 사용한다.
            return component != null ? component : Undo.AddComponent<T>(target);
        }
        private static void Reparent(Transform child, Transform parent)
        {
            if (child.parent != parent) Undo.SetTransformParent(child, parent, "Own document graphics");
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "Create desk item UI");
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        private static void ConfigureRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            Undo.RecordObject(rect, "Inset visible desk items");
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);
        }
        private static TMP_Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, TMP_FontAsset font, float fontSize)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = fontSize; text.color = Ink;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }
    }
}
