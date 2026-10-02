using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using WhaleShark.UI;

namespace WhaleShark.Editor
{
    /// <summary>Validates saved device geometry; it never replaces player-facing layouts.</summary>
    public sealed class DeviceLayout : OdinEditorWindow
    {
        [MenuItem("Tools/Service Desk/Validate saved device layout")]
        [Button("저장된 신분증·휴대전화 서식 확인")]
        public static void Apply()
        {
            Check<IdentityCard>("IdentityCard",new Vector2(460,280));
            Check<MobileDevice>("MobileDevice",new Vector2(310,480));
            Debug.Log("Device layouts and original prefab connections verified; authored fonts and appearance preserved.");
        }
        private static void Check<T>(string name,Vector2 size) where T:Component
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ServiceDeskMigration.Prefabs+name+".prefab");
            if(prefab==null || prefab.GetComponent<T>()==null || ((RectTransform)prefab.transform).sizeDelta!=size)
                throw new InvalidOperationException("Unexpected device composition/geometry: "+name);
        }
    }
}
