using System;
using System.IO;
using System.Linq;
using Febucci.TextAnimatorCore.Typing;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using SheIsNotHuman.CubeScreen;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace SheIsNotHuman.InspectionMvp.Editor
{
    public sealed class InspectionMvpBuilder : OdinEditorWindow
    {
        private const string Root = "Assets/InspectionMvp";
        private const string ScenePath = "Assets/Scenes/PerspectiveCubeViewPrototype.unity";
        private static readonly Color Ink = new Color(.12f, .16f, .19f);
        private static readonly Color Paper = new Color(.96f, .96f, .94f);
        private static readonly Color Teal = new Color(.74f, .76f, .75f);

        [MenuItem("Tools/Inspection MVP/Builder")]
        private static void OpenWindow() => GetWindow<InspectionMvpBuilder>("Inspection MVP");

        [Button("Build wired scene (one NPC, no motion)", ButtonSizes.Large)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build requires Edit Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open PerspectiveCubeViewPrototype before building; this builder never discards another scene.");
            if (UnityEngine.Object.FindAnyObjectByType<InspectionMvpView>() != null)
            {
                Debug.Log("Inspection MVP already wired; existing hierarchy and edits preserved.");
                return;
            }
            var nav = UnityEngine.Object.FindAnyObjectByType<PerspectiveCubeViewController>();
            var front = GameObject.Find("CubeFaces/Face_Front/Canvas")?.GetComponent<Canvas>();
            var bottom = GameObject.Find("CubeFaces/Face_Bottom/Canvas")?.GetComponent<Canvas>();
            if (nav == null || front == null || bottom == null)
                throw new InvalidOperationException("Expected Perspective ViewRig and Front/Bottom world canvases are missing.");
            EnsureFolder(Root); EnsureFolder(Root + "/Samples"); EnsureFolder(Root + "/Fonts");
            var roster = CreateRoster();
            var font = CreateKoreanFont();
            var timing = GetOrCreate<TypingDelaysByCharacter>(Root + "/Samples/DialogueTiming.asset");
            timing.waitForNormalChars = .035f; timing.waitLong = .22f; timing.waitMiddle = .1f;
            timing.skipLastPunctuationWait = true;
            EditorUtility.SetDirty(timing);

            EnsureEventSystem();
            var camera = nav.GetComponentInChildren<Camera>();
            ConfigureCanvas(front, camera); ConfigureCanvas(bottom, camera);
            // Preserve the prototype objects for easy inspection/reversal, hide only obsolete demos on the two owned faces.
            HidePrototype(front); HidePrototype(bottom);
            var root = new GameObject("InspectionMvp");
            Undo.RegisterCreatedObjectUndo(root, "Build Inspection MVP");
            var view = root.AddComponent<InspectionMvpView>();
            var flow = root.AddComponent<InspectionFlowController>();
            view.navigation = nav;

            var reception = Rect("InspectionReception", front.transform, Vector2.zero, new Vector2(1200, 675));
            Panel("Backdrop", reception, Vector2.zero, new Vector2(1180, 650), Paper);
            Label("Header", reception, "검사대  /  방문자", new Vector2(0, 267), new Vector2(1020, 60), font, 30, Ink);
            view.npcRoot = Rect("Npc", reception, new Vector2(0, -10), new Vector2(420, 420));
            view.npcGroup = view.npcRoot.gameObject.AddComponent<CanvasGroup>();
            view.portrait = Panel("Portrait", view.npcRoot, new Vector2(0, -35), new Vector2(230, 190), Teal);
            Panel("Head", view.portrait.transform, new Vector2(0,150), new Vector2(120,125), Ink);
            view.npcName = Label("NpcName", view.npcRoot, "대기 중", new Vector2(0, -180), new Vector2(650, 64), font, 30, Ink);
            view.deskButton = Button("DeskButton", reception, "책상 보기", new Vector2(0, -273), new Vector2(270, 62), font, Teal);

            var desk = Rect("InspectionDesk", bottom.transform, Vector2.zero, new Vector2(1200, 675));
            Panel("DeskBackground", desk, Vector2.zero, new Vector2(1200, 675), new Color(.88f,.89f,.88f));
            view.stateLabel = Label("State", desk, "검수 창구", new Vector2(-120, 289), new Vector2(820, 45), font, 20, Ink);
            view.frontButton = Button("FrontButton", desk, "방문자 보기", new Vector2(410,289), new Vector2(235,48), font, Teal);
            view.dialogueButton = Button("DialogueButton", desk, "", new Vector2(0, 184), new Vector2(1060, 125), font, Paper);
            var dialogue = view.dialogueButton.GetComponentInChildren<TextMeshProUGUI>();
            dialogue.name = "Dialogue"; dialogue.fontSize = 29; dialogue.color = Ink;
            dialogue.alignment = TextAlignmentOptions.MidlineLeft; dialogue.margin = new Vector4(28, 12, 28, 12);
            dialogue.gameObject.AddComponent<TextAnimator_TMP>();
            view.dialogueWriter = dialogue.gameObject.AddComponent<TypewriterComponent>();
            view.dialogueWriter.localSettings = new UnityTypewriterSettings { useTypeWriter = true, startTypewriterMode = StartTypewriterMode.OnShowText };
            view.dialogueWriter.TimingSettings = timing;
            view.documentsRoot = Rect("Documents", desk, new Vector2(0, -47), new Vector2(1060, 295));
            view.documentsGroup = view.documentsRoot.gameObject.AddComponent<CanvasGroup>();
            view.identityButton = Button("IdentityButton", view.documentsRoot, "", new Vector2(-275, 30), new Vector2(480, 205), font, Paper);
            view.orderButton = Button("OrderButton", view.documentsRoot, "", new Vector2(275, 30), new Vector2(480, 205), font, Paper);
            view.identitySummary = Label("IdentitySummary", view.documentsRoot, "신분증", new Vector2(-275, 30), new Vector2(430, 175), font, 29, Ink);
            view.orderSummary = Label("OrderSummary", view.documentsRoot, "주문서", new Vector2(275, 30), new Vector2(430, 175), font, 29, Ink);
            view.inspectButton = Button("InspectButton", view.documentsRoot, "서류 펼쳐서 검사", new Vector2(0, -115), new Vector2(400, 65), font, Teal);
            view.hint = Label("Hint", desk, "대화를 눌러 계속", new Vector2(0, -242), new Vector2(1100, 72), font, 24, Ink);
            view.restartButton = Button("RestartButton", desk, "다시 시작", new Vector2(0, -125), new Vector2(340, 70), font, Teal);

            // Bottom is a full-viewport worldspace canvas when focused. This shield stays active through both modal tweens.
            var shield = Panel("ModalShield", desk, Vector2.zero, new Vector2(1200, 675), new Color(0,0,0,.78f));
            shield.raycastTarget = true;
            // Stretch beyond the face to absorb clicks in wider/taller aspect ratios as well.
            shield.rectTransform.sizeDelta = new Vector2(10000,10000);
            view.modalShield = shield.gameObject;
            view.modalGroup = shield.gameObject.AddComponent<CanvasGroup>();
            view.modalPanel = Rect("ModalPanel", shield.transform, Vector2.zero, new Vector2(1060, 595));
            Panel("Background", view.modalPanel, Vector2.zero, new Vector2(1060,595), new Color(.84f,.85f,.84f)).raycastTarget = true;
            Label("Title", view.modalPanel, "서류 검사  /  고객 코드를 비교하세요", new Vector2(-65, 244), new Vector2(820,60), font, 27, Ink);
            Panel("IdentityPaper", view.modalPanel, new Vector2(-255,25), new Vector2(460,340), Paper);
            Panel("OrderPaper", view.modalPanel, new Vector2(255,25), new Vector2(460,340), Paper);
            view.identityPortrait = Panel("IdentityPortrait", view.modalPanel, new Vector2(-390,35), new Vector2(110,100), Teal);
            Panel("Head", view.identityPortrait.transform, new Vector2(0,78), new Vector2(60,62), Ink);
            view.identityDetail = Label("IdentityDetail", view.modalPanel, "", new Vector2(-185,25), new Vector2(265,310), font, 26, Ink);
            view.orderDetail = Label("OrderDetail", view.modalPanel, "", new Vector2(255,25), new Vector2(410,310), font, 26, Ink);
            view.closeButton = Button("CloseButton", view.modalPanel, "닫기", new Vector2(442,245), new Vector2(130,60), font, Teal);
            view.passButton = Button("PassButton", view.modalPanel, "PASS / 통과", new Vector2(-260,-222), new Vector2(430,78), font, new Color(.70f,.76f,.71f));
            view.nonPassButton = Button("NonPassButton", view.modalPanel, "NON PASS / 거절", new Vector2(260,-222), new Vector2(430,78), font, new Color(.78f,.71f,.70f));
            view.resultStamp = Label("ResultStamp", desk, "", new Vector2(0,-50), new Vector2(680,100), font, 60, Color.white);
            view.resultStamp.fontStyle = FontStyles.Bold;
            view.resultStamp.gameObject.SetActive(false);
            shield.gameObject.SetActive(false);
            view.restartButton.gameObject.SetActive(false);
            view.npcGroup.alpha = view.documentsGroup.alpha = 0;
            view.documentsGroup.blocksRaycasts = view.documentsGroup.interactable = false;

            flow.animateTransitions = false; flow.npcLimit = 1;
            flow.Configure(view, roster);
            InspectionDeskMigration.Apply();
            EditorUtility.SetDirty(flow); EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("Inspection MVP wired and saved: initial gate npcLimit=1, animateTransitions=false.");
        }

        private static void ConfigureCanvas(Canvas canvas, Camera camera)
        {
            if (canvas.renderMode != RenderMode.WorldSpace) throw new InvalidOperationException("Expected worldspace Canvas.");
            canvas.worldCamera = camera;
            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        private static void HidePrototype(Canvas canvas)
        {
            foreach (Transform child in canvas.transform)
                if (child.name == "Title" || child.name == "Hint" || child.name == "ObjectLabel") child.gameObject.SetActive(false);
            var sample = canvas.transform.parent.Find("SampleObject");
            if (sample != null) sample.gameObject.SetActive(false);
        }
        private static void EnsureEventSystem()
        {
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            if (systems.Length > 1) throw new InvalidOperationException("Multiple EventSystems; resolve before building.");
            var system = systems.Length == 1 ? systems[0] : new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            system.gameObject.SetActive(true);
            if (system.GetComponent<BaseInputModule>() == null)
            {
#if ENABLE_INPUT_SYSTEM
                var module = system.gameObject.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
#else
                system.gameObject.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        /// <summary>Targeted migration for an already-built scene; does not rebuild hierarchy or touch NPC assets.</summary>
        public static void MigrateResultStampToDesk()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Migration requires Edit Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open the Perspective prototype scene before migrating.");
            var view = UnityEngine.Object.FindAnyObjectByType<InspectionMvpView>();
            if (view == null || view.resultStamp == null || view.documentsRoot == null)
                throw new InvalidOperationException("Wired Inspection MVP result stamp and documents are required.");
            RectTransform stamp = view.resultStamp.rectTransform;
            Undo.SetTransformParent(stamp, view.documentsRoot.parent, "Move result to inspection desk");
            Undo.RecordObject(stamp, "Place inspection result");
            stamp.anchorMin = stamp.anchorMax = stamp.pivot = new Vector2(.5f,.5f);
            stamp.anchoredPosition3D = new Vector3(0,-50,0);
            stamp.sizeDelta = new Vector2(680,100);
            stamp.localRotation = Quaternion.identity;
            stamp.localScale = Vector3.one;
            stamp.SetAsLastSibling();
            view.resultStamp.gameObject.SetActive(false);
            EditorUtility.SetDirty(stamp);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Inspection MVP result stamp moved to desk; scene saved and NPC data preserved.");
        }
        private static InspectionNpcData[] CreateRoster()
        {
            string[] names = { "김서윤", "이도현", "박하린" };
            string[] codes = { "C-101", "C-202", "C-303" };
            string[] products = { "식량 상자", "방한 장갑", "정수 필터" };
            string[][] lines = {
                new[] { "안녕하세요. 주문한 식량을 받으러 왔어요.", "제 신분증과 주문서입니다. 확인해 주세요." },
                new[] { "장갑을 찾으러 왔습니다. 조금 서둘러도 될까요?", "여기 서류가 있어요. 이름을 확인해 주세요." },
                new[] { "정수 필터 세 개를 주문했어요.", "고객 코드는 두 서류에 적혀 있습니다." }
            };
            string[] passLines = { "감사합니다. 잘 받았습니다.", "고맙습니다. 따뜻하게 쓸게요.", "확인해 주셔서 감사합니다." };
            string[] rejectLines = { "어떤 문제가 있나요? 다시 확인해 볼게요.", "코드가 다른가요? 서류를 다시 가져올게요.", "알겠습니다. 주문 내역을 확인하겠습니다." };
            var roster = new InspectionNpcData[3];
            for (int i = 0; i < 3; i++)
            {
                string id = "NPC_0" + (i + 1);
                string path = Root + "/Samples/" + id + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<InspectionNpcData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<InspectionNpcData>();
                    data.npcId = id; data.displayName = names[i];
                    data.portraitColor = new[] { new Color(.55f,.61f,.62f), new Color(.64f,.58f,.54f), new Color(.59f,.56f,.63f) }[i];
                    data.dialogue = lines[i];
                    data.identity = new IdentityDocument { displayName = names[i], customerCode = codes[i] };
                    data.order = new OrderDocument { orderNumber = "ORD-00" + (i+1), customerCode = i == 1 ? "C-209" : codes[i], productName = products[i], quantity = i+1 };
                    data.passReaction = passLines[i];
                    data.nonPassReaction = rejectLines[i];
                    AssetDatabase.CreateAsset(data, path);
                }
                roster[i] = data;
            }
            return roster;
        }

        [MenuItem("Tools/Inspection MVP/Rebuild Korean Atlas")]
        [Button("Rebuild atlas from current NPC text", ButtonSizes.Medium)]
        public static void RebuildKoreanAtlas()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Rebuild requires Edit Mode.");
            var font = CreateKoreanFont(true);
            foreach (var canvasName in new[] { "CubeFaces/Face_Front/Canvas/InspectionReception", "CubeFaces/Face_Bottom/Canvas/InspectionDesk" })
            {
                var root = GameObject.Find(canvasName);
                if (root == null) continue;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    Undo.RecordObject(text, "Assign rebuilt Korean atlas"); text.font = font; EditorUtility.SetDirty(text);
                }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("Korean atlas rebuilt from current source and NPC data; save the scene to retain assignments. Previous font asset preserved.");
        }

        private static TMP_FontAsset CreateKoreanFont(bool regenerate = false)
        {
            string path = Root + "/Fonts/InspectionKoreanStatic.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null && !regenerate) return existing;
            if (regenerate) path = AssetDatabase.GenerateUniqueAssetPath(path);
            string osFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf");
            if (!File.Exists(osFont)) throw new FileNotFoundException("Installed Malgun Gothic required to bake the sample atlas.", osFont);
            // Read OS font directly into the rasterizer. Only baked SDF glyphs are saved; never copy the font file.
            var font = TMP_FontAsset.CreateFontAsset(osFont, 0, 56, 7, GlyphRenderMode.SDFAA, 2048, 2048);
            if (font == null) throw new InvalidOperationException("Could not load installed Korean font.");
            string corpus = string.Concat(Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
            corpus += string.Concat(Directory.GetFiles(Root + "/Samples", "*.asset").Select(File.ReadAllText));
            foreach (string guid in AssetDatabase.FindAssets("t:InspectionNpcData", new[] { Root }))
                corpus += JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<InspectionNpcData>(AssetDatabase.GUIDToAssetPath(guid)));
            corpus += string.Concat(Enumerable.Range(32,95).Select(c => (char)c));
            string chars = new string(corpus.Where(c => !char.IsControl(c) && c != '\ufeff').Distinct().ToArray());
            if (!font.TryAddCharacters(chars, out string missing))
                throw new InvalidOperationException("Korean atlas missing characters: " + missing);
            font.name = "Inspection Korean Static";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.isMultiAtlasTexturesEnabled = false;
            var serialized = new SerializedObject(font);
            serialized.FindProperty("m_SourceFontFilePath").stringValue = string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(font, path);
            font.material.name = "Inspection Korean SDF";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
            {
                texture.name = "Inspection Korean Atlas";
                AssetDatabase.AddObjectToAsset(texture, font);
            }
            EditorUtility.SetDirty(font);
            return font;
        }
        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static UnityEngine.UI.Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var image = Rect(name,parent,pos,size).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false;
            if (name != "ModalShield")
            {
                var outline = image.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = Ink; outline.effectDistance = new Vector2(2,-2);
            }
            return image;
        }
        private static TextMeshProUGUI Label(string name, Transform parent, string value, Vector2 pos, Vector2 size, TMP_FontAsset font, float fontSize, Color color)
        {
            var text = Rect(name,parent,pos,size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = fontSize; text.color = color;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; return text;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, string label, Vector2 pos, Vector2 size, TMP_FontAsset font, Color color)
        {
            var image = Panel(name,parent,pos,size,color); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            Label("Label",image.transform,label,Vector2.zero,size - new Vector2(16,8),font,27,Ink);
            return button;
        }
    }
}
