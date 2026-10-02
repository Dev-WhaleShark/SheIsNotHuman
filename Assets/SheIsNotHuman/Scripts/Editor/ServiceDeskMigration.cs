using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WhaleShark.Gameplay;
using WhaleShark.Interaction;
using WhaleShark.Rendering;
using WhaleShark.UI;

namespace WhaleShark.Editor
{
    /// <summary>Explicit in-place split migration; subsequent runs validate and refresh prefab artifacts.</summary>
    public sealed class ServiceDeskMigration : OdinEditorWindow
    {
        public const string MainScene = "Assets/SheIsNotHuman/Scenes/PerspectiveCubeViewPrototype.unity";
        public const string Prefabs = "Assets/SheIsNotHuman/Prefabs/";
        private const string Baseline = "tmp/service-desk-refactor-20261002/legacy-references.json";
        [MenuItem("Tools/Service Desk/Migrate saved main scene")]
        [Button("기존 씬 이관 및 검증")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=MainScene) throw new InvalidOperationException("Open the saved main scene first.");
            var existing=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ServiceDesk>(true))
                .FirstOrDefault(x=>x.GetComponent<DeskFocus>()!=null);
            if(existing!=null)
            {
                MobileDeviceMigration.ConfigurePrefab(Prefabs+"MobileDevice.prefab");
                MobileDeviceMigration.ConfigureIdentityPrefab(Prefabs+"IdentityCard.prefab");
                Validate(existing); SaveArtifacts(existing);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); return;
            }
            if(!File.Exists(Baseline)) throw new InvalidOperationException("Migration baseline is missing; capture legacy references first.");
            var data=JObject.Parse(File.ReadAllText(Baseline));
            var old=Ref<ServiceDesk>(data,"viewId");
            var documents=Ref<RectTransform>(data,"documentsRoot");
            var deskRoot=(RectTransform)documents.parent;
            var desk=Ensure<ServiceDesk>(deskRoot.gameObject);
            var context=Ensure<DeskInteractionContext>(deskRoot.gameObject);
            var focus=Ensure<DeskFocus>(deskRoot.gameObject);
            var identity=Ref<IdentityCard>(data,"identityDocument");
            var mobile=Ref<MobileDevice>(data,"orderDocument");
            var focusCanvas=Ref<Canvas>(data,"focusCanvas");
            var writer=Ref<Febucci.TextAnimatorForUnity.TypewriterComponent>(data,"dialogueWriter");
            var dialogueButton=Ref<UnityEngine.UI.Button>(data,"dialogueButton");
            var panel=Ensure<DialoguePanel>(dialogueButton.gameObject);
            var flow=Ref<GameFlowManager>(data,"flowId");
            var player=Ensure<DialoguePlayer>(flow.gameObject);
            var visitorRoot=Ref<RectTransform>(data,"npcRoot");
            var actor=Ensure<VisitorActor>(visitorRoot.gameObject);
            var adapter=Ensure<CubeDeskAdapter>(flow.gameObject);
            var rig=Ref<CubeCameraRig>(data,"navigation");

            MovePrefab("IdentityDocument","IdentityCard"); MovePrefab("OrderDocument","MobileDevice");
            MobileDeviceMigration.ConfigurePrefab(Prefabs+"MobileDevice.prefab");
            MobileDeviceMigration.ConfigureIdentityPrefab(Prefabs+"IdentityCard.prefab");
            // Legacy components are removed from leaf assets; only the desk instance owns gestures.
            var identityItem=Wrap(identity,"IdentityCardItem",context,deskRoot.Find("VisibleItemBounds") as RectTransform);
            var mobileItem=Wrap(mobile,"MobileDeviceItem",context,deskRoot.Find("VisibleItemBounds") as RectTransform);
            var items=deskRoot.GetComponentsInChildren<DeskItem>(true);
            foreach(var item in items) item.Configure(context,deskRoot.Find("VisibleItemBounds") as RectTransform,item.Kind);
            focusCanvas.transform.SetParent(deskRoot,true);
            deskRoot.name="ServiceDesk";
            visitorRoot.parent.name="VisitorReception";
            flow.gameObject.name="GameFlow";
            identity.gameObject.name="IdentityCard"; mobile.gameObject.name="MobileDevice";

            Set(panel,"writer",writer); Set(panel,"button",dialogueButton); Set(panel,"group",Ref<CanvasGroup>(data,"dialogueGroup"));
            Set(panel,"typingSpeed",(float)data["typingSpeed"]); Set(panel,"hideSeconds",(float)data["dialogueHideSeconds"]);
            Set(panel,"hideOffset",new Vector2((float)data["dialogueHideOffset"]["x"],(float)data["dialogueHideOffset"]["y"]));
            Set(player,"panel",panel);
            Set(actor,"root",visitorRoot); Set(actor,"group",Ref<CanvasGroup>(data,"npcGroup"));
            Set(actor,"portrait",Ref<UnityEngine.UI.Image>(data,"portrait")); Set(actor,"nameLabel",Ref<TMPro.TMP_Text>(data,"npcName"));
            Set(actor,"motionSeconds",(float)data["motionSeconds"]);
            Set(focus,"focusCanvas",focusCanvas); Set(focus,"itemsRoot",Ref<RectTransform>(data,"focusItemsRoot"));
            Set(focus,"controls",Ref<RectTransform>(data,"focusControls")); Set(focus,"shield",Ref<GameObject>(data,"modalShield"));
            Set(focus,"group",Ref<CanvasGroup>(data,"modalGroup"));
            Set(focus,"identityCard",identity); Set(focus,"mobileDevice",mobile);
            Set(focus,"identityItem",identityItem); Set(focus,"mobileItem",mobileItem);
            Set(focus,"closeButton",Ref<UnityEngine.UI.Button>(data,"closeButton"));
            Set(focus,"passButton",Ref<UnityEngine.UI.Button>(data,"passButton"));
            Set(focus,"nonPassButton",Ref<UnityEngine.UI.Button>(data,"nonPassButton"));
            Set(focus,"modalSeconds",(float)data["modalSeconds"]);
            Set(desk,"dialoguePanel",panel); Set(desk,"identityCard",identity); Set(desk,"mobileDevice",mobile);
            Set(desk,"documentsRoot",documents); Set(desk,"documentsGroup",Ref<CanvasGroup>(data,"documentsGroup"));
            Set(desk,"focus",focus); Set(desk,"context",context); Set(desk,"items",items);
            Set(desk,"hint",Ref<TMPro.TMP_Text>(data,"hint")); Set(desk,"stateLabel",Ref<TMPro.TMP_Text>(data,"stateLabel"));
            Set(desk,"resultStamp",Ref<TMPro.TMP_Text>(data,"resultStamp")); Set(desk,"restartButton",Ref<UnityEngine.UI.Button>(data,"restartButton"));
            Set(desk,"motionSeconds",(float)data["motionSeconds"]); Set(desk,"animateTransitions",(bool)data["animateTransitions"]);
            Set(adapter,"rig",rig); Set(adapter,"desk",desk); Set(adapter,"focus",focus); Set(adapter,"context",context);
            Set(adapter,"focusDepthRatio",(float)data["focusDepthRatio"]);
            var roster=((JArray)data["roster"]).Select(x=>AssetDatabase.LoadAssetAtPath<VisitorProfile>((string)x)).ToArray();
            flow.Configure(desk,player,actor,roster,adapter);
            Set(flow,"animateTransitions",(bool)data["animateTransitions"]); Set(flow,"visitorLimit",(int)data["visitorLimit"]);
            Set(flow,"reactionHoldSeconds",(float)data["reactionHoldSeconds"]);

            var legacyPanel=Ref<RectTransform>(data,"modalPanel");
            if(legacyPanel!=null) UnityEngine.Object.DestroyImmediate(legacyPanel.gameObject);
            var dummy=Ref<GameObject>(data,"dummyPanel"); if(dummy!=null) UnityEngine.Object.DestroyImmediate(dummy);
            if(old!=null && old!=desk) UnityEngine.Object.DestroyImmediate(old);
            // Focus controls and originals stay on a world canvas only when connected to the cube.
            focusCanvas.gameObject.SetActive(false);
            CubeNavigationMigration.Apply();
            Validate(desk); SaveArtifacts(desk);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            RemoveUnusedExpandedPrefabs();
        }
        private static DeskItem Wrap(Component leaf,string name,DeskInteractionContext context,RectTransform bounds)
        {
            var rect=(RectTransform)leaf.transform;
            var parent=rect.parent;
            if(parent.GetComponent<DeskItem>()!=null) return parent.GetComponent<DeskItem>();
            var wrapper=UIPrefabFactory.Rect(name,parent,rect.anchoredPosition,rect.sizeDelta);
            wrapper.anchorMin=rect.anchorMin; wrapper.anchorMax=rect.anchorMax; wrapper.pivot=rect.pivot;
            wrapper.anchoredPosition3D=rect.anchoredPosition3D; wrapper.localRotation=rect.localRotation;
            wrapper.localScale=rect.localScale; wrapper.SetSiblingIndex(rect.GetSiblingIndex());
            var graphic=Ensure<UnityEngine.UI.Image>(wrapper.gameObject); graphic.color=Color.clear; graphic.raycastTarget=true;
            var oldItem=leaf.GetComponent<DeskItem>(); if(oldItem!=null) UnityEngine.Object.DestroyImmediate(oldItem);
            rect.SetParent(wrapper,false); rect.anchoredPosition3D=Vector3.zero; rect.localRotation=Quaternion.identity; rect.localScale=Vector3.one;
            var item=Ensure<DeskItem>(wrapper.gameObject); item.Configure(context,bounds,DeskItemKind.Document);
            return item;
        }
        public static void Validate(ServiceDesk desk)
        {
            if(desk==null || desk.GetComponent<DeskFocus>()==null || desk.GetComponent<DeskInteractionContext>()==null)
                throw new InvalidOperationException("Incomplete desk composition.");
            foreach(var root in desk.gameObject.scene.GetRootGameObjects())
                foreach(var tr in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)>0)
                        throw new InvalidOperationException("Missing script on "+tr.name);
        }
        public static void SaveArtifacts(ServiceDesk desk)
        {
            var panel=desk.GetComponentInChildren<DialoguePanel>(true);
            if(panel!=null) PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject,Prefabs+"DialoguePanel.prefab",InteractionMode.AutomatedAction);
            // Asset copy contains no cube or game flow; replace only its raycasters and Canvas settings.
            PrefabUtility.SaveAsPrefabAsset(desk.gameObject,Prefabs+"ServiceDesk.prefab");
            var contents=PrefabUtility.LoadPrefabContents(Prefabs+"ServiceDesk.prefab");
            try
            {
                foreach(var corrected in contents.GetComponentsInChildren<DistortionCorrectedGraphicRaycaster>(true))
                {
                    var go=corrected.gameObject; UnityEngine.Object.DestroyImmediate(corrected);
                    Ensure<UnityEngine.UI.GraphicRaycaster>(go);
                }
                foreach(var canvas in contents.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.worldCamera=null; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=200;
                    var canvasRect=(RectTransform)canvas.transform;
                    canvasRect.anchorMin=canvasRect.anchorMax=canvasRect.pivot=Vector2.one*.5f;
                    canvasRect.anchoredPosition3D=Vector3.zero; canvasRect.localRotation=Quaternion.identity;
                    canvasRect.localScale=Vector3.one; canvasRect.sizeDelta=new Vector2(1200,675);
                    var scaler=Ensure<UnityEngine.UI.CanvasScaler>(canvas.gameObject);
                    scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution=new Vector2(1200,675); scaler.matchWidthOrHeight=.5f;
                }
                PrefabUtility.SaveAsPrefabAsset(contents,Prefabs+"ServiceDesk.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        public static void RemoveUnusedExpandedPrefabs()
        {
            var obsolete=new[]{Prefabs+"OrderDocumentExpanded.prefab",Prefabs+"IdentityDocumentExpanded.prefab"};
            var assets=AssetDatabase.FindAssets("",new[]{"Assets/SheIsNotHuman"}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(x=>!AssetDatabase.IsValidFolder(x) && !obsolete.Contains(x)).ToArray();
            foreach(var path in obsolete)
            {
                if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) continue;
                var dependent=assets.Where(x=>AssetDatabase.GetDependencies(x,true).Contains(path)).ToArray();
                if(dependent.Length!=0) throw new InvalidOperationException("Legacy dependency remains: "+string.Join(", ",dependent));
                if(!AssetDatabase.DeleteAsset(path)) throw new IOException("Could not remove "+path);
            }
        }
        private static void MovePrefab(string oldName,string newName)
        {
            var from=Prefabs+oldName+".prefab"; var to=Prefabs+newName+".prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(to)!=null) return;
            var error=AssetDatabase.MoveAsset(from,to); if(!string.IsNullOrEmpty(error)) throw new IOException(error);
        }
        private static T Ref<T>(JObject data,string name) where T:UnityEngine.Object
        {
            var token=data[name]; var value=token is JObject ? (string)token["id"] : (string)token;
            if(!GlobalObjectId.TryParse(value,out var id)) throw new InvalidOperationException("Invalid baseline ref "+name);
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as T;
        }
        internal static T Ensure<T>(GameObject go) where T:Component => go.GetComponent<T>() ?? go.AddComponent<T>();
        internal static void Set(UnityEngine.Object target,string name,object value)
        {
            var serialized=new SerializedObject(target); var property=serialized.FindProperty(name);
            if(property==null) throw new InvalidOperationException(target.GetType().Name+" has no serialized field "+name);
            if(value is UnityEngine.Object obj) property.objectReferenceValue=obj;
            else if(value is UnityEngine.Object[] array)
            { property.arraySize=array.Length; for(int i=0;i<array.Length;i++) property.GetArrayElementAtIndex(i).objectReferenceValue=array[i]; }
            else if(value is bool boolean) property.boolValue=boolean;
            else if(value is float number) property.floatValue=number;
            else if(value is int integer) property.intValue=integer;
            else if(value is Vector2 vector) property.vector2Value=vector;
            else if(value==null) property.objectReferenceValue=null;
            else throw new InvalidOperationException("Unsupported assignment "+name);
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
            if(PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
