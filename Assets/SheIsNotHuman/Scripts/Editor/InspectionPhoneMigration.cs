using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheIsNotHuman.InspectionMvp.Editor
{
    /// <summary>Updates the connected order-document prefab in place. The desk instance and its serialized links survive.</summary>
    public sealed class InspectionPhoneMigration : OdinEditorWindow
    {
        private const string ScenePath = "Assets/SheIsNotHuman/Scenes/PerspectiveCubeViewPrototype.unity";
        private const string PrefabPath = "Assets/SheIsNotHuman/Prefabs/OrderDocument.prefab";
        private static readonly Color Bezel = Hex("15171F");
        private static readonly Color Ink = Hex("20202A");
        private static readonly Color Muted = Hex("716F7D");
        private static readonly Color Purple = Hex("7355B5");
        private static readonly Color Cream = Hex("F8F5F0");
        private static readonly Color White = Color.white;
        private static readonly Color XLine = Hex("E8E8EA");

        [MenuItem("Tools/Inspection MVP/Migrate order document phone")]
        [Button("Migrate order document phone")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Phone migration requires Edit Mode.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open PerspectiveCubeViewPrototype before phone migration.");

            InspectionMvpView view = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<InspectionMvpView>(true);
                if (view != null) break;
            }
            if (view == null || view.orderDocument == null || view.orderButton == null ||
                view.orderDocument.gameObject != view.orderButton.gameObject)
                throw new InvalidOperationException("The connected original order document is required.");
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.orderDocument.gameObject) != PrefabPath)
                throw new InvalidOperationException("The existing order document must still be the expected prefab instance.");

            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var order = prefab.GetComponent<OrderDocumentView>();
                if (order == null || order.content == null || prefab.GetComponent<DeskInspectableItem>() == null ||
                    prefab.GetComponent<UnityEngine.UI.Button>() == null)
                    throw new InvalidOperationException("Original order document components or content are missing.");
                Build(prefab, order, order.content.font);
                VerifySampleValues(order);
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            // The scene owns the instance's desk placement, drag item, and controller references.
            // Do not apply the instance: that would bake its scene-only host/desk references into the asset.
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Order document phone authored on the existing prefab. Review and save this scene separately.", view.orderButton);
        }

        /// <summary>Explicit batch entry point; never opens a second interactive scene or discards a dirty one.</summary>
        public static void ApplyBatchAndSaveScene()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("ApplyBatchAndSaveScene is for a dedicated batch Editor only.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the migrated scene.");
        }

        private static void Build(GameObject prefab, OrderDocumentView order, TMP_FontAsset font)
        {
            if (font == null) throw new InvalidOperationException("The existing Korean TMP font is missing.");
            var root = (RectTransform)prefab.transform;
            if (root.sizeDelta != new Vector2(310, 480))
                throw new InvalidOperationException("Unexpected order-document root size; preserve desk and focus geometry.");

            var phone = Child(root, "PhoneRoot");
            Stretch(phone, 0, 0, 1, 1);
            phone.SetAsLastSibling();
            var group = Ensure<CanvasGroup>(phone.gameObject);
            group.alpha = 1;
            group.interactable = true;
            group.blocksRaycasts = false; // Runtime opens phone input only in a stable document focus.
            group.ignoreParentGroups = false;

            // The existing root button remains the compact click/drag target. Old paper art stays
            // serialized, with its object IDs and references intact, but no longer renders.
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child != phone) child.gameObject.SetActive(false);
            }
            var originalImage = prefab.GetComponent<UnityEngine.UI.Image>();
            originalImage.color = new Color(0, 0, 0, 0);
            originalImage.raycastTarget = true;

            Surface(phone, Bezel, true);
            Box(phone, "Display", 0, 0, 294, 464, Cream, true);
            var home = Box(phone, "HomeScreen", 0, 0, 294, 382, Hex("E4DCF4"));
            var witchform = Box(phone, "WitchformScreen", 0, 0, 294, 382, Cream);
            var x = Box(phone, "XScreen", 0, 0, 294, 382, White);

            BuildHome(home, font);
            BuildWitchform(witchform, order, font);
            BuildX(x, font);

            var status = Box(phone, "StatusBar", 0, 215, 294, 34, Color.clear);
            Label(status, "Time", "10:24", -104, 0, 75, 20, font, 12, Ink, TextAlignmentOptions.MidlineLeft);
            Label(status, "Signal", "4G  100%", 95, 0, 98, 20, font, 10, Ink, TextAlignmentOptions.MidlineRight);
            var camera = Box(phone, "Camera", 0, 215, 35, 7, Bezel, true);
            camera.SetAsLastSibling();

            var nav = Box(phone, "NavBar", 0, -215, 294, 34, Color.clear);
            Button(nav, "BackButton", "BACK", -87, 0, 87, 29, font, 11, Ink, Color.clear);
            Button(nav, "HomeButton", "HOME", 87, 0, 87, 29, font, 11, Ink, Color.clear);
            Box(nav, "GestureBar", 0, -15, 76, 3, Hex("545260"), true);

            home.gameObject.SetActive(true);
            witchform.gameObject.SetActive(false);
            x.gameObject.SetActive(false);
            phone.gameObject.SetActive(true);
        }

        private static void BuildHome(RectTransform home, TMP_FontAsset font)
        {
            Box(home, "WallpaperUpper", 0, 105, 294, 172, Hex("BFC4EB"));
            Box(home, "WallpaperAccent", 66, 74, 138, 98, Hex("C9B4E9"), true);
            Box(home, "WallpaperLower", 0, -104, 294, 208, Hex("E8D9E9"));
            Label(home, "Clock", "10:24", -3, 110, 236, 72, font, 51, White, TextAlignmentOptions.Center);
            Label(home, "Date", "MON, 28 SEP", 0, 69, 200, 24, font, 13, White, TextAlignmentOptions.Center);
            Label(home, "AppsCaption", "APPS", -99, -39, 85, 18, font, 10, Ink, TextAlignmentOptions.MidlineLeft);
            Button(home, "WitchformAppButton", "W", -65, -92, 65, 65, font, 37, White, Purple);
            Button(home, "XAppButton", "X", 65, -92, 65, 65, font, 34, White, Bezel);
            Label(home, "WitchformAppName", "WITCHFORM", -65, -139, 105, 20, font, 11, Ink, TextAlignmentOptions.Center);
            Label(home, "XAppName", "X", 65, -139, 65, 20, font, 11, Ink, TextAlignmentOptions.Center);
        }

        private static void BuildWitchform(RectTransform screen, OrderDocumentView order, TMP_FontAsset font)
        {
            Box(screen, "Header", 0, 164, 294, 54, Purple);
            Label(screen, "HeaderTitle", "WITCHFORM", -28, 165, 220, 32, font, 21, White, TextAlignmentOptions.MidlineLeft);
            Label(screen, "HeaderMark", "W", 121, 165, 33, 33, font, 22, White, TextAlignmentOptions.Center);
            Label(screen, "SectionTitle", "주문 정보", -1, 116, 254, 30, font, 18, Ink, TextAlignmentOptions.MidlineLeft);
            var card = Box(screen, "OrderCard", 0, -26, 270, 255, White, true);
            Label(card, "CardTitle", "ORDER DETAILS", -4, 102, 233, 24, font, 12, Purple, TextAlignmentOptions.MidlineLeft);
            Box(card, "TopRule", 0, 80, 238, 1, Hex("E8DFF4"));
            OrderRow(screen, card, "CustomerName", "고객 이름", 56, font, out order.customerNameText);
            OrderRow(screen, card, "CustomerCode", "고객 코드", 16, font, out order.customerCodeText);
            OrderRow(screen, card, "OrderNumber", "주문 번호", -24, font, out order.orderNumberText);
            OrderRow(screen, card, "Product", "상품", -64, font, out order.productText);
            OrderRow(screen, card, "Quantity", "수량", -104, font, out order.quantityText);
            Label(screen, "Footer", "WITCHFORM  /  ORDER", 0, -171, 240, 17, font, 10, Muted, TextAlignmentOptions.Center);
        }

        private static void OrderRow(RectTransform screen, RectTransform card, string name, string caption,
            float y, TMP_FontAsset font, out TMP_Text value)
        {
            Label(card, name + "Label", caption, -73, y, 84, 28, font, 12, Muted, TextAlignmentOptions.MidlineLeft);
            // Values are direct WitchformScreen children so runtime and integration checks can find them by path.
            value = Label(screen, name + "Value", string.Empty, 46, y - 26, 152, 30, font, 15, Ink,
                TextAlignmentOptions.MidlineLeft);
            value.enableAutoSizing = true;
            value.fontSizeMin = 12;
            value.fontSizeMax = 15;
            value.overflowMode = TextOverflowModes.Overflow;
            if (y > -100) Box(card, name + "Rule", 0, y - 20, 236, 1, XLine);
        }

        private static void BuildX(RectTransform screen, TMP_FontAsset font)
        {
            Box(screen, "Header", 0, 164, 294, 54, White);
            Label(screen, "HeaderTitle", "X", 0, 165, 100, 39, font, 29, Ink, TextAlignmentOptions.Center);
            Box(screen, "HeaderRule", 0, 135, 294, 1, XLine);
            var scrollRoot = Box(screen, "ScrollView", 0, -15, 288, 300, Color.clear);
            var scroll = Ensure<UnityEngine.UI.ScrollRect>(scrollRoot.gameObject);
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 24;
            scroll.horizontalScrollbar = null;
            scroll.verticalScrollbar = null;

            var viewport = Child(scrollRoot, "Viewport");
            Stretch(viewport, 0, 0, 1, 1);
            var viewportImage = Ensure<UnityEngine.UI.Image>(viewport.gameObject);
            viewportImage.color = White;
            viewportImage.raycastTarget = true; // ScrollRect needs a hit target inside its viewport.
            var mask = Ensure<UnityEngine.UI.Mask>(viewport.gameObject);
            mask.showMaskGraphic = false;

            var content = Child(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 0);
            content.localScale = Vector3.one;
            var layout = Ensure<UnityEngine.UI.VerticalLayoutGroup>(content.gameObject);
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = Ensure<UnityEngine.UI.ContentSizeFitter>(content.gameObject);
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            Post(content, "Post01", "@harbor_notice", "식량 상자 입고 완료. 주문 번호를 확인해 주세요.", font);
            Post(content, "Post02", "@north_gate", "정수 필터는 검사 책상에서 받을 수 있습니다.", font);
            Post(content, "Post03", "@mira_local", "방한 장갑을 주문한 분은 고객 코드를 확인해 주세요.", font);
            Post(content, "Post04", "@harbor_notice", "물품을 받았습니다. 감사합니다.", font);
            Post(content, "Post05", "@east_corner", "검사 전에 주문 정보를 다시 확인해 주세요.", font);
            Label(screen, "FeedFooter", "LOCAL FEED", 0, -180, 250, 16, font, 10, Muted, TextAlignmentOptions.Center);
        }

        private static void Post(RectTransform content, string name, string author, string body, TMP_FontAsset font)
        {
            var post = Child(content, name);
            post.sizeDelta = new Vector2(0, 112);
            var element = Ensure<UnityEngine.UI.LayoutElement>(post.gameObject);
            element.preferredHeight = 112;
            element.minHeight = 112;
            Surface(post, White);
            Label(post, "Author", author, -8, 36, 254, 20, font, 13, Ink, TextAlignmentOptions.MidlineLeft);
            var text = Label(post, "Body", body, -8, -11, 254, 59, font, 12, Ink, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            Box(post, "Rule", 0, -55, 270, 1, XLine);
        }

        private static void VerifySampleValues(OrderDocumentView order)
        {
            for (int i = 1; i <= 3; i++)
            {
                var npc = AssetDatabase.LoadAssetAtPath<InspectionNpcData>(
                    "Assets/SheIsNotHuman/Data/NPC_0" + i + ".asset");
                if (npc == null || npc.identity == null || npc.order == null)
                    throw new InvalidOperationException("Current NPC_0" + i + " order data is required for phone layout validation.");
                CheckValue(order.customerNameText, npc.identity.displayName);
                CheckValue(order.customerCodeText, npc.order.customerCode);
                CheckValue(order.orderNumberText, npc.order.orderNumber);
                CheckValue(order.productText, npc.order.productName);
                CheckValue(order.quantityText, npc.order.quantity.ToString());
            }
        }

        private static void CheckValue(TMP_Text label, string value)
        {
            RequireGlyphs(label.font, value);
            // All authored NPC values must fit at the minimum permitted size without ellipsis.
            float originalSize = label.fontSize;
            label.fontSize = label.fontSizeMin;
            var preferred = label.GetPreferredValues(value);
            label.fontSize = originalSize;
            if (preferred.x > label.rectTransform.sizeDelta.x - 4 || preferred.y > label.rectTransform.sizeDelta.y)
                throw new InvalidOperationException("Phone order value does not fit: " + value);
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                if (existing is RectTransform rect) return rect;
                throw new InvalidOperationException("Expected RectTransform: " + name);
            }
            var child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            child.SetParent(parent, false);
            return child;
        }

        private static T Ensure<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static RectTransform Box(Transform parent, string name, float x, float y,
            float width, float height, Color color, bool rounded = false)
        {
            var rect = Child(parent, name);
            Place(rect, x, y, width, height);
            Surface(rect, color, rounded);
            return rect;
        }

        private static void Surface(RectTransform rect, Color color, bool rounded = false)
        {
            var image = Ensure<UnityEngine.UI.Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = false;
            if (rounded)
            {
                var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.type = UnityEngine.UI.Image.Type.Sliced;
                }
            }
            else
            {
                image.sprite = null;
                image.type = UnityEngine.UI.Image.Type.Simple;
            }
        }

        private static UnityEngine.UI.Button Button(RectTransform parent, string name, string title,
            float x, float y, float width, float height, TMP_FontAsset font, float size, Color foreground, Color background)
        {
            var rect = Box(parent, name, x, y, width, height, background, background.a > 0);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            var button = Ensure<UnityEngine.UI.Button>(rect.gameObject);
            button.targetGraphic = image;
            button.interactable = true;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            Label(rect, "Title", title, 0, 0, width - 4, height - 4, font, size, foreground,
                TextAlignmentOptions.Center);
            return button;
        }

        private static TMP_Text Label(Transform parent, string name, string value, float x, float y,
            float width, float height, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            RequireGlyphs(font, value);
            var rect = Child(parent, name);
            Place(rect, x, y, width, height);
            var label = Ensure<TextMeshProUGUI>(rect.gameObject);
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Normal;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
            return label;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition3D = new Vector3(x, y, 0);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = Vector2.one * .5f;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition3D = Vector3.zero;
        }

        private static void RequireGlyphs(TMP_FontAsset font, string value)
        {
            foreach (char character in value)
            {
                if (!font.HasCharacter(character))
                    throw new InvalidOperationException("Korean font lacks U+" + ((int)character).ToString("X4") +
                        " for phone text: " + value + ". Font atlas regeneration requires assigned asset ownership.");
            }
        }

        private static Color Hex(string rgb)
        {
            if (ColorUtility.TryParseHtmlString("#" + rgb, out var color)) return color;
            throw new ArgumentException("Invalid RGB color: " + rgb);
        }
    }
}
