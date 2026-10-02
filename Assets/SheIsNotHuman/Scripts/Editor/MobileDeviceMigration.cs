using System;
using UnityEditor;
using UnityEngine;
using TMPro;
using WhaleShark.UI;
using WhaleShark.Interaction;

namespace WhaleShark.Editor
{
    /// <summary>Copies authored phone references explicitly, without recreating visual art or fonts.</summary>
    public static class MobileDeviceMigration
    {
        public static void ConfigurePrefab(string path)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var device=root.GetComponent<MobileDevice>();
                if(device==null) throw new InvalidOperationException("MobileDevice missing on "+path);
                var phone=root.transform.Find("PhoneRoot");
                if(phone==null) throw new InvalidOperationException("Authored phone UI missing.");
                var witch=phone.Find("WitchformScreen"); var x=phone.Find("XScreen");
                var app=ServiceDeskMigration.Ensure<WitchformApp>(witch.gameObject);
                var feed=ServiceDeskMigration.Ensure<XFeedApp>(x.gameObject);
                ServiceDeskMigration.Ensure<PhonePointerGuard>(x.gameObject);
                ServiceDeskMigration.Set(device,"phoneRoot",phone);
                ServiceDeskMigration.Set(device,"homeScreen",phone.Find("HomeScreen"));
                ServiceDeskMigration.Set(device,"phoneInput",ServiceDeskMigration.Ensure<CanvasGroup>(phone.gameObject));
                foreach(var mapping in new[]{("homeButton","NavBar/HomeButton"),("backButton","NavBar/BackButton")})
                    ServiceDeskMigration.Set(device,mapping.Item1,phone.Find(mapping.Item2).GetComponent<UnityEngine.UI.Button>());
                device.ConfigureApps(new[]{
                    new MobileDevice.AppEntry("witchform",witch,phone.Find("HomeScreen/WitchformAppButton").GetComponent<UnityEngine.UI.Button>()),
                    new MobileDevice.AppEntry("x",x,phone.Find("HomeScreen/XAppButton").GetComponent<UnityEngine.UI.Button>())});
                EditorUtility.SetDirty(device);
                ServiceDeskMigration.Set(device,"witchformApp",app); ServiceDeskMigration.Set(device,"xFeedApp",feed);
                foreach(var mapping in new[]{("customerNameText","CustomerNameValue"),("customerCodeText","CustomerCodeValue"),
                    ("orderNumberText","OrderNumberValue"),("productText","ProductValue"),("quantityText","QuantityValue")})
                    ServiceDeskMigration.Set(app,mapping.Item1,witch.Find(mapping.Item2).GetComponent<TMP_Text>());
                ServiceDeskMigration.Set(feed,"feed",x.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true));
                RemoveDeskComponents(root);
                // Order content is entirely in Witchform now; remove verified hidden paper-only siblings.
                for(int i=root.transform.childCount-1;i>=0;i--)
                    if(root.transform.GetChild(i)!=phone && !root.transform.GetChild(i).gameObject.activeSelf)
                        UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void ConfigureIdentityPrefab(string path)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try { RemoveDeskComponents(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void RemoveDeskComponents(GameObject root)
        {
            var item=root.GetComponent<DeskItem>(); if(item!=null) UnityEngine.Object.DestroyImmediate(item);
            var button=root.GetComponent<UnityEngine.UI.Button>(); if(button!=null) UnityEngine.Object.DestroyImmediate(button);
        }
    }
}
