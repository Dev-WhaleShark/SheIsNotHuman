using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp.Editor
{
    /// <summary>실제 원본 프리팹을 편집하는 재실행 가능한 문서 서식 도구. 씬 저장은 검토 후 별도로 한다.</summary>
    public sealed class InspectionDocumentLayout : OdinEditorWindow
    {
        private const string Prefabs = "Assets/InspectionMvp/Prefabs/";
        private const string ScenePath = "Assets/Scenes/PerspectiveCubeViewPrototype.unity";
        private static readonly Color Ink = new Color(.19f, .23f, .21f);
        private static readonly Color Paper = new Color(.70f, .75f, .68f);
        private static readonly Color Screen = new Color(.76f, .81f, .73f);

        [MenuItem("Tools/Inspection MVP/Apply document layout")]
        [Button("Apply document layout to prefabs and current desk")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Document layout requires Edit Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open PerspectiveCubeViewPrototype first.");
            InspectionMvpView view = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<InspectionMvpView>(true);
                if (view != null) break;
            }
            if (view == null || view.identityDocument == null || view.orderDocument == null)
                throw new InvalidOperationException("Existing document references are required.");
            RequirePrefab(view.identityDocument.gameObject, "IdentityDocument");
            RequirePrefab(view.orderDocument.gameObject, "OrderDocument");
            EditPrefab("IdentityDocument", LayoutIdentity);
            EditPrefab("OrderDocument", LayoutOrder);
            PlaceOnDesk((RectTransform)view.identityDocument.transform, new Vector2(460, 280), new Vector2(-230, -20), .68f);
            PlaceOnDesk((RectTransform)view.orderDocument.transform, new Vector2(310, 480), new Vector2(210, -20), .55f);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Document prefabs updated in place. Review and save the current scene separately.", view);
        }

        private static void RequirePrefab(GameObject instance, string name)
        {
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) != Prefabs + name + ".prefab")
                throw new InvalidOperationException("Unexpected prefab connection on " + instance.name);
        }

        private static void EditPrefab(string name, Action<GameObject> layout)
        {
            string path = Prefabs + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                layout(root);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void LayoutIdentity(GameObject root)
        {
            var view = root.GetComponent<IdentityDocumentView>();
            if (view == null || view.content == null || view.portrait == null)
                throw new InvalidOperationException("Identity content and portrait must already exist.");
            var font = view.content.font;
            var rect = (RectTransform)root.transform;
            Surface(rect, new Vector2(460, 280), Paper);
            ConfigureText(view.content, "신분증", font, 32, .065f, .76f, .64f, .93f);
            Label(rect, "NameLabel", "이름", font, 15, .065f, .60f, .59f, .69f);
            view.displayNameText = Label(rect, "DisplayName", "", font, 27, .065f, .46f, .59f, .59f);
            Line(rect, "NameRule", .065f, .445f, .59f, .450f);
            Label(rect, "CustomerCodeLabel", "고객 코드", font, 15, .065f, .32f, .59f, .41f);
            view.customerCodeText = Label(rect, "CustomerCode", "", font, 26, .065f, .19f, .59f, .32f);
            Line(rect, "FooterRule", .065f, .155f, .935f, .160f);
            view.footerText = Label(rect, "Footer", "", font, 12, .065f, .045f, .93f, .135f);

            var frame = Fill(rect, "PortraitFrame", Ink, .65f, .23f, .93f, .89f);
            frame.SetSiblingIndex(1);
            var portrait = view.portrait;
            Stretch((RectTransform)portrait.transform, .659f, .245f, .921f, .875f);
            portrait.color = new Color(.58f, .64f, .57f);
            portrait.raycastTarget = false;
            portrait.gameObject.SetActive(true);
            portrait.transform.SetAsLastSibling();
            Fill(portrait.transform, "Head", Ink, .32f, .49f, .68f, .82f);
            Fill(portrait.transform, "Shoulders", Ink, .17f, .08f, .83f, .47f);
            DisableLegacyLabel(rect);
            view.expanded = false;
        }

        private static void LayoutOrder(GameObject root)
        {
            var view = root.GetComponent<OrderDocumentView>();
            if (view == null || view.content == null)
                throw new InvalidOperationException("Order content must already exist.");
            var font = view.content.font;
            var rect = (RectTransform)root.transform;
            Surface(rect, new Vector2(310, 480), Screen);
            Fill(rect, "Speaker", Ink, .39f, .970f, .61f, .976f);
            ConfigureText(view.content, "주문서", font, 29, .09f, .86f, .91f, .95f);
            view.content.alignment = TextAlignmentOptions.Center;
            Line(rect, "HeaderRule", .085f, .843f, .915f, .846f);
            Label(rect, "CustomerSection", "주문자 정보", font, 18, .09f, .745f, .90f, .807f);
            Box(rect, "CustomerNameBox", .085f, .636f, .915f, .734f);
            Label(rect, "CustomerNameLabel", "이름", font, 12, .11f, .687f, .89f, .723f);
            view.customerNameText = Label(rect, "CustomerName", "", font, 20, .11f, .643f, .89f, .691f);
            Box(rect, "CustomerCodeBox", .085f, .515f, .915f, .613f);
            Label(rect, "CustomerCodeLabel", "고객 코드", font, 12, .11f, .567f, .89f, .603f);
            view.customerCodeText = Label(rect, "CustomerCode", "", font, 20, .11f, .522f, .89f, .570f);
            Label(rect, "OrderSection", "주문 정보", font, 18, .09f, .416f, .90f, .478f);
            Box(rect, "OrderDetailsBox", .085f, .075f, .915f, .400f);
            Label(rect, "OrderNumberLabel", "주문 번호", font, 12, .11f, .350f, .89f, .387f);
            view.orderNumberText = Label(rect, "OrderNumber", "", font, 19, .11f, .300f, .89f, .350f);
            Line(rect, "ProductRule", .11f, .285f, .89f, .288f);
            Label(rect, "ProductLabel", "주문 상품", font, 12, .11f, .237f, .89f, .274f);
            view.productText = Label(rect, "Product", "", font, 20, .11f, .170f, .89f, .237f);
            Label(rect, "QuantityLabel", "수량", font, 12, .11f, .094f, .34f, .147f);
            view.quantityText = Label(rect, "Quantity", "", font, 20, .39f, .094f, .89f, .147f);
            view.quantityText.alignment = TextAlignmentOptions.MidlineRight;
            Fill(rect, "HomeIndicator", Ink, .39f, .031f, .61f, .036f);
            DisableLegacyLabel(rect);
            view.expanded = false;
        }

        private static void Surface(RectTransform root, Vector2 size, Color paper)
        {
            root.sizeDelta = size;
            root.localScale = Vector3.one;
            root.GetComponent<UnityEngine.UI.Image>().color = Ink;
            foreach (var effect in root.GetComponents<UnityEngine.UI.Shadow>()) effect.enabled = false;
            Fill(root, "PaperSurface", paper, .013f, .012f, .987f, .988f).SetAsFirstSibling();
        }

        private static void DisableLegacyLabel(Transform root)
        {
            var label = root.Find("Label");
            if (label != null) label.gameObject.SetActive(false);
        }

        private static void Box(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var frame = Fill(parent, name, Ink, x0, y0, x1, y1);
            // Border thickness stays approximately one authored pixel on each edge.
            var inner = Fill(frame, "Inset", Screen, 0, 0, 1, 1);
            inner.offsetMin = Vector2.one;
            inner.offsetMax = -Vector2.one;
        }

        private static void Line(Transform parent, string name, float x0, float y0, float x1, float y1)
            => Fill(parent, name, Ink, x0, y0, x1, y1);

        private static RectTransform Fill(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var rect = Child(parent, name);
            Stretch(rect, x0, y0, x1, y1);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text Label(Transform parent, string name, string value, TMP_FontAsset font,
            float size, float x0, float y0, float x1, float y1)
        {
            var rect = Child(parent, name);
            var text = rect.GetComponent<TMP_Text>();
            if (text == null) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(text, value, font, size, x0, y0, x1, y1);
            return text;
        }

        private static void ConfigureText(TMP_Text text, string value, TMP_FontAsset font,
            float size, float x0, float y0, float x1, float y1)
        {
            Stretch(text.rectTransform, x0, y0, x1, y1);
            text.font = font;
            text.text = value;
            text.color = Ink;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * .7f;
            text.fontSizeMax = size;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = Vector4.zero;
            text.raycastTarget = false;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var child = parent.Find(name) as RectTransform;
            if (child != null) return child;
            child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            child.SetParent(parent, false);
            return child;
        }

        private static void Stretch(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = Vector2.one * .5f;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            var position = rect.anchoredPosition3D;
            position.z = 0;
            rect.anchoredPosition3D = position;
        }

        private static void PlaceOnDesk(RectTransform rect, Vector2 size, Vector2 position, float scale)
        {
            Undo.RecordObject(rect, "Place authored document on desk");
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            rect.anchoredPosition3D = new Vector3(position.x, position.y, 0);
            rect.localScale = Vector3.one * scale;
            rect.localRotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }
    }
}
