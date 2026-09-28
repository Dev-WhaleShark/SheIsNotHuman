using System;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheIsNotHuman.InspectionMvp.Editor
{
    /// <summary>기존 주문서 프리팹과 직렬화 참조를 유지하며 휴대폰 화면만 구성한다.</summary>
    public sealed class InspectionPhoneLayout : OdinEditorWindow
    {
        private const string PrefabPath = "Assets/InspectionMvp/Prefabs/OrderDocument.prefab";
        private const string ScenePath = "Assets/Scenes/PerspectiveCubeViewPrototype.unity";
        private const string BaseFontPath = "Assets/InspectionMvp/Fonts/InspectionKoreanStatic.asset";
        private static readonly Color Frame = new Color(.10f, .13f, .17f);
        private static readonly Color Background = new Color(.91f, .94f, .97f);
        private static readonly Color White = Color.white;
        private static readonly Color Ink = new Color(.10f, .16f, .24f);
        private static readonly Color Muted = new Color(.36f, .43f, .53f);
        private static readonly Color Accent = new Color(.37f, .31f, .77f);

        /// <summary>대상 씬의 새 한글과 주문서 원본 프리팹을 한 번에 갱신한다. 저장 전 상태를 확인한다.</summary>
        [MenuItem("Tools/Inspection MVP/Install phone MVP")]
        [Button("Install phone MVP in current scene")]
        public static void InstallPhoneMvp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Open the target scene in clean Edit Mode before installing the phone MVP.");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved PerspectiveCubeViewPrototype scene without unsaved changes.");

            InspectionMvpView view = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<InspectionMvpView>(true);
                if (view != null) break;
            }
            if (view == null || view.orderDocument == null || view.orderDocument.content == null ||
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.orderDocument.gameObject) != PrefabPath)
                throw new InvalidOperationException("The target scene must contain the existing OrderDocument prefab instance.");

            string prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (string.IsNullOrEmpty(prefabGuid) || string.IsNullOrEmpty(sceneGuid) ||
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BaseFontPath) == null)
                throw new InvalidOperationException("The saved scene, OrderDocument prefab, or base Korean font is missing.");
            string osFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf");
            if (!File.Exists(osFont))
                throw new FileNotFoundException("Malgun Gothic is needed to bake the Korean TMP atlas.", osFont);

            string newFontPath = AssetDatabase.GenerateUniqueAssetPath(BaseFontPath);
            InspectionMvpBuilder.RebuildKoreanAtlas();
            var newFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(newFontPath);
            if (newFont == null)
                throw new InvalidOperationException("The rebuilt Korean atlas was not found: " + newFontPath);

            // Prefab instance의 글꼴 override도 씬에 명시적으로 보존한다.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.font != newFont) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(label))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                }
            if (view.orderDocument.content.font != newFont)
                throw new InvalidOperationException("The scene OrderDocument did not receive the rebuilt font.");

            Apply();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var phone = prefab != null ? prefab.GetComponent<PhoneDeviceView>() : null;
            if (phone == null || phone.homePage == null || phone.witchformPage == null || phone.xPage == null ||
                phone.witchformButton == null || phone.xButton == null || phone.homeButton == null || phone.backButton == null)
                throw new InvalidOperationException("The saved phone prefab has missing app or navigation references.");
            if (prefab.GetComponent<OrderDocumentView>().content.font != newFont ||
                AssetDatabase.AssetPathToGUID(PrefabPath) != prefabGuid ||
                AssetDatabase.AssetPathToGUID(ScenePath) != sceneGuid)
                throw new InvalidOperationException("The phone font or existing asset GUIDs changed unexpectedly.");

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the phone MVP scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("[PhoneMvp] Installed phone home, Witchform order viewer, and X sample feed. " +
                "scene=" + ScenePath + " prefab=" + PrefabPath + " font=" + newFontPath +
                " prefabGuid=" + prefabGuid + " sceneGuid=" + sceneGuid + " phoneRefs=7/7");
        }

        [MenuItem("Tools/Inspection MVP/Apply phone layout")]
        [Button("Apply phone layout to OrderDocument prefab")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Phone layout requires Edit Mode.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Layout(root, CurrentSceneFont());
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Layout(GameObject root) => Layout(root, null);

        public static void Layout(GameObject root, TMP_FontAsset fontOverride)
        {
            var order = root.GetComponent<OrderDocumentView>();
            var rootButton = root.GetComponent<UnityEngine.UI.Button>();
            var inspectable = root.GetComponent<DeskInspectableItem>();
            if (order == null || rootButton == null || inspectable == null || order.content == null ||
                (order.content.font == null && fontOverride == null))
                throw new InvalidOperationException("OrderDocument root components and Korean TMP font are required.");
            var font = fontOverride != null ? fontOverride : order.content.font;
            ValidatePhoneGlyphs(font);
            order.content.font = font;

            var rect = root.transform as RectTransform;
            if (rect == null) throw new InvalidOperationException("OrderDocument must have a RectTransform.");
            var legacy = rect.Find("LegacyOrderLayout") as RectTransform;
            if (legacy == null)
            {
                // 기존 요소를 파괴하지 않아 파일 ID와 OrderDocumentView의 TMP 참조가 보존된다.
                var original = new Transform[rect.childCount];
                for (int i = 0; i < original.Length; i++) original[i] = rect.GetChild(i);
                legacy = Child(rect, "LegacyOrderLayout");
                foreach (var child in original) child.SetParent(legacy, false);
            }
            legacy.gameObject.SetActive(false);

            rect.sizeDelta = new Vector2(310, 480);
            rect.localScale = Vector3.one;
            root.GetComponent<UnityEngine.UI.Image>().color = Frame;
            foreach (var shadow in root.GetComponents<UnityEngine.UI.Shadow>()) shadow.enabled = false;

            var screen = Panel(rect, "PhoneScreen", Background, .025f, .025f, .975f, .958f);
            screen.SetAsLastSibling();
            Panel(screen, "StatusBar", White, 0, .90f, 1, 1);
            Text(screen, "StatusTime", "09:41", order.content.font, 13, Ink, .055f, .917f, .36f, .962f);
            Text(screen, "StatusIcons", "LTE  85%", order.content.font, 11, Ink, .58f, .917f, .95f, .962f, TextAlignmentOptions.MidlineRight);
            Panel(screen, "CameraIsland", Frame, .39f, .959f, .61f, .977f);

            var home = Page(screen, "HomePage");
            var witchform = Page(screen, "WitchformPage");
            var xPage = Page(screen, "XPage");
            BuildHome(home, order.content.font, out var witchformButton, out var xButton);
            BuildWitchform(witchform, legacy, order);
            BuildX(xPage, order.content.font);

            var nav = Panel(screen, "NavigationBar", White, 0, 0, 1, .105f);
            var backButton = Button(nav, "BackButton", "<", order.content.font, 25, Ink,
                .10f, .12f, .37f, .88f, White);
            var homeButton = Button(nav, "HomeButton", "HOME", order.content.font, 12, Ink,
                .37f, .12f, .63f, .88f, White);
            Panel(nav, "GestureBar", Frame, .39f, .035f, .61f, .065f);

            var phone = root.GetComponent<PhoneDeviceView>();
            if (phone == null) phone = root.AddComponent<PhoneDeviceView>();
            var serialized = new SerializedObject(phone);
            Reference(serialized, "homePage", home.gameObject);
            Reference(serialized, "witchformPage", witchform.gameObject);
            Reference(serialized, "xPage", xPage.gameObject);
            Reference(serialized, "witchformButton", witchformButton);
            Reference(serialized, "xButton", xButton);
            Reference(serialized, "homeButton", homeButton);
            Reference(serialized, "backButton", backButton);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            home.gameObject.SetActive(true);
            witchform.gameObject.SetActive(false);
            xPage.gameObject.SetActive(false);
            order.expanded = false;
            // 프리팹에 남겨 둔 기존 주문 TMP도 새 atlas를 사용한다.
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
        }

        private static TMP_FontAsset CurrentSceneFont()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var view = root.GetComponentInChildren<InspectionMvpView>(true);
                if (view != null && view.orderDocument != null && view.orderDocument.content != null)
                    return view.orderDocument.content.font;
            }
            return null;
        }

        private static void BuildHome(RectTransform page, TMP_FontAsset font,
            out UnityEngine.UI.Button witchformButton, out UnityEngine.UI.Button xButton)
        {
            Text(page, "Welcome", "안녕하세요", font, 17, Muted, .07f, .84f, .93f, .90f);
            Text(page, "Clock", "09:41", font, 49, Ink, .07f, .68f, .93f, .83f);
            Text(page, "Date", "오늘의 소식을 확인해 보세요", font, 14, Muted, .07f, .62f, .93f, .69f);
            Panel(page, "SearchCard", White, .06f, .49f, .94f, .58f);
            Text(page, "SearchHint", "앱을 선택하세요", font, 14, Muted, .10f, .50f, .90f, .57f);
            witchformButton = Button(page, "WitchformButton", "윗치폼\n주문 확인", font, 16, White,
                .07f, .22f, .47f, .45f, Accent);
            xButton = Button(page, "XButton", "X\n피드", font, 18, White,
                .53f, .22f, .93f, .45f, Frame);
            Text(page, "HomeFooter", "오프라인 모드", font, 12, Muted, .07f, .07f, .93f, .14f);
        }

        private static void BuildWitchform(RectTransform page, RectTransform legacy, OrderDocumentView order)
        {
            var font = order.content.font;
            Text(page, "WitchformTitle", "윗치폼", font, 22, Ink, .07f, .88f, .93f, .97f);
            Text(page, "WitchformSubtitle", "현재 주문", font, 13, Muted, .07f, .82f, .93f, .88f);
            Panel(page, "OrderCard", White, .055f, .07f, .945f, .79f);
            Row(page, legacy, order, "OrderNumber", "주문 번호", .67f, .77f, 18, ref order.orderNumberText);
            Row(page, legacy, order, "CustomerName", "고객 이름", .55f, .65f, 18, ref order.customerNameText);
            Row(page, legacy, order, "CustomerCode", "고객 코드", .43f, .53f, 18, ref order.customerCodeText);
            Row(page, legacy, order, "Product", "상품", .29f, .41f, 18, ref order.productText);
            Row(page, legacy, order, "Quantity", "수량", .16f, .27f, 18, ref order.quantityText);
        }

        private static void Row(RectTransform page, RectTransform legacy, OrderDocumentView order,
            string name, string title, float y0, float y1, int valueSize, ref TMP_Text value)
        {
            var font = order.content.font;
            Text(page, name + "Caption", title, font, 11, Muted, .095f, y1 - .035f, .89f, y1 + .015f);
            if (value == null)
            {
                var existing = legacy.Find(name);
                value = existing != null ? existing.GetComponent<TMP_Text>() : null;
            }
            if (value == null) value = Text(page, name, "", font, valueSize, Ink, .095f, y0, .89f, y1 - .025f);
            value.transform.SetParent(page, false);
            Place(value.rectTransform, .095f, y0, .89f, y1 - .025f);
            Format(value, font, valueSize, Ink, TextAlignmentOptions.MidlineLeft);
            value.gameObject.SetActive(true);
        }

        private static void BuildX(RectTransform page, TMP_FontAsset font)
        {
            Text(page, "XTitle", "X", font, 26, Ink, .07f, .88f, .93f, .97f);
            Text(page, "XSubtitle", "추천 피드 · 오프라인", font, 12, Muted, .07f, .82f, .93f, .88f);
            Feed(page, font, "FeedOne", "@town_note", "오늘도 조용한 하루네요.\n창밖 풍경이 참 좋습니다.", .59f, .79f);
            Feed(page, font, "FeedTwo", "@daily_record", "새로 산 책을 다 읽었어요.\n다음엔 무엇을 읽을까요?", .36f, .56f);
            Feed(page, font, "FeedThree", "@night_walk", "밤공기가 조금 선선해졌네요.", .13f, .33f);
        }

        private static void Feed(RectTransform parent, TMP_FontAsset font, string name,
            string author, string body, float bottom, float top)
        {
            var card = Panel(parent, name, White, .055f, bottom, .945f, top);
            Text(card, "Author", author, font, 13, Ink, .06f, .64f, .94f, .92f);
            Text(card, "Body", body, font, 13, Ink, .06f, .12f, .94f, .65f);
        }

        private static RectTransform Page(RectTransform parent, string name)
            => Empty(parent, name, 0, .105f, 1, .90f);

        private static UnityEngine.UI.Button Button(Transform parent, string name, string caption,
            TMP_FontAsset font, int size, Color textColor, float x0, float y0, float x1, float y1, Color fill)
        {
            var rect = Panel(parent, name, fill, x0, y0, x1, y1);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            image.raycastPadding = Vector4.zero;
            var button = rect.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            Text(rect, "Caption", caption, font, size, textColor, 0, 0, 1, 1, TextAlignmentOptions.Center);
            return button;
        }

        private static RectTransform Panel(Transform parent, string name, Color color,
            float x0, float y0, float x1, float y1)
        {
            var rect = Empty(parent, name, x0, y0, x1, y1);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font,
            int size, Color color, float x0, float y0, float x1, float y1,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var rect = Empty(parent, name, x0, y0, x1, y1);
            var text = rect.GetComponent<TMP_Text>();
            if (text == null) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            Format(text, font, size, color, alignment);
            return text;
        }

        private static void Format(TMP_Text text, TMP_FontAsset font, int size, Color color,
            TextAlignmentOptions alignment)
        {
            text.font = font;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * .7f;
            text.fontSizeMax = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
        }

        private static RectTransform Empty(Transform parent, string name,
            float x0, float y0, float x1, float y1)
        {
            var rect = Child(parent, name);
            Place(rect, x0, y0, x1, y1);
            return rect;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect != null) return rect;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = Vector2.one * .5f;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition3D = Vector3.zero;
        }

        private static void Reference(SerializedObject target, string propertyName, UnityEngine.Object value)
        {
            var property = target.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException("PhoneDeviceView reference is missing: " + propertyName);
            property.objectReferenceValue = value;
        }

        private static void ValidatePhoneGlyphs(TMP_FontAsset font)
        {
            const string copy = "안녕하세요오늘의소식을확인해보세요앱을선택하세요윗치폼주문확인피드오프라인모드" +
                "현재주문번호고객이름코드상품수량추천오늘도조용한하루네요창밖풍경이참좋습니다" +
                "새로산책다읽었어요다음엔무엇을읽을까요밤공기가조금선선해졌네요";
            string missing = string.Empty;
            foreach (char ch in copy)
                if (!font.HasCharacter(ch) && missing.IndexOf(ch) < 0) missing += ch;
            if (missing.Length != 0)
                throw new InvalidOperationException("OrderDocument Korean TMP atlas lacks phone glyphs: " + missing +
                    ". Assign a rebuilt Korean font to the prefab's OrderSummary TMP before applying the phone layout.");
        }
    }
}
