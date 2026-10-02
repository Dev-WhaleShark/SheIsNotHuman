using System;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using WhaleShark.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace WhaleShark.Editor
{
    /// <summary>Builds independent UI artifacts from saved authored visuals, without resetting main gameplay.</summary>
    public sealed class ServiceDeskBuilder : OdinEditorWindow
    {
        public const string SandboxScene="Assets/SheIsNotHuman/Scenes/DeskSandbox.unity";
        [MenuItem("Tools/Service Desk/Build standalone desk sandbox")]
        [Button("독립 책상 샌드박스 생성")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            var original=EditorSceneManager.GetActiveScene();
            if(original.isDirty) throw new InvalidOperationException("Save the active scene before building sandbox.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ServiceDeskMigration.Prefabs+"ServiceDesk.prefab");
            if(prefab==null) throw new InvalidOperationException("Migrate and save ServiceDesk.prefab first.");
            var alreadyOpen=SceneManager.GetSceneByPath(SandboxScene);
            if(alreadyOpen.IsValid() && alreadyOpen.isLoaded) throw new InvalidOperationException("Close the existing sandbox before rebuilding it.");
            var sandbox=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(sandbox);
                var cameraGo=new GameObject("SandboxCamera",typeof(Camera)); cameraGo.tag="MainCamera";
                var camera=cameraGo.GetComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=5;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.88f,.89f,.88f);
                cameraGo.transform.position=new Vector3(0,0,-10);
                var canvasGo=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
                var canvas=canvasGo.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1200,675); scaler.matchWidthOrHeight=.5f;
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,sandbox); instance.transform.SetParent(canvasGo.transform,false);
                var rect=(RectTransform)instance.transform; rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
                rect.anchoredPosition3D=Vector3.zero; rect.localRotation=Quaternion.identity; rect.localScale=Vector3.one;
                var desk=instance.GetComponent<ServiceDesk>(); var panel=instance.GetComponentInChildren<DialoguePanel>(true);
                var dialogue=ServiceDeskMigration.Ensure<DialoguePlayer>(instance);
                ServiceDeskMigration.Set(dialogue,"panel",panel);
                var harness=ServiceDeskMigration.Ensure<DeskSandbox>(instance); harness.Configure(desk,dialogue);
                EditorUtility.SetDirty(harness);
                var eventGo=new GameObject("EventSystem",typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                eventGo.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                eventGo.AddComponent<StandaloneInputModule>();
#endif
                if(!EditorSceneManager.SaveScene(sandbox,SandboxScene)) throw new InvalidOperationException("Could not save sandbox.");
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(sandbox,true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Standalone desk sandbox saved with ordinary Canvas, camera, EventSystem and sample UI harness.");
        }
    }
}
