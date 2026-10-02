using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace WhaleShark.UI
{
    public enum PhoneScreen { Home, Witchform, X }

    [DisallowMultipleComponent, MovedFrom(true,"SheIsNotHuman.InspectionMvp",null,"OrderDocumentView")]
    public sealed class MobileDevice : MonoBehaviour
    {
        [Serializable]
        public sealed class AppEntry
        {
            [SerializeField, LabelText("앱 ID")] private string appId;
            [SerializeField, Required, LabelText("화면")] private Transform screenRoot;
            [SerializeField, Required, LabelText("실행 버튼")] private UnityEngine.UI.Button launcherButton;
            public string Id => appId;
            public Transform Screen => screenRoot;
            public UnityEngine.UI.Button Launcher => launcherButton;
            public AppEntry(string id,Transform screen,UnityEngine.UI.Button launcher)
            { appId=id; screenRoot=screen; launcherButton=launcher; }
        }
        [TabGroup("InspectorTabs","연결"), FoldoutGroup("InspectorTabs/연결/참조",Expanded=false),Required,SerializeField]
        private Transform phoneRoot,homeScreen;
        [FoldoutGroup("InspectorTabs/연결/참조"),Required,SerializeField] private CanvasGroup phoneInput;
        [FoldoutGroup("InspectorTabs/연결/참조"),Required,SerializeField] private UnityEngine.UI.Button homeButton,backButton;
        [FoldoutGroup("InspectorTabs/연결/참조"),Required,SerializeField] private WitchformApp witchformApp;
        [FoldoutGroup("InspectorTabs/연결/참조"),Required,SerializeField] private XFeedApp xFeedApp;
        [TabGroup("InspectorTabs","설정"),SerializeField,LabelText("등록 앱")]
        private AppEntry[] apps=Array.Empty<AppEntry>();
        [SerializeField,HideInInspector,FormerlySerializedAs("expanded")] private bool focused;
        private bool inputEnabled;
        private readonly Dictionary<string,AppEntry> registered=new(StringComparer.Ordinal);
        private readonly List<(UnityEngine.UI.Button button,UnityAction callback)> listeners=new();
        [ShowInInspector,ReadOnly,PropertyOrder(-20),LabelText("현재 앱 ID")]
        public string CurrentAppId { get; private set; } = "home";
        public PhoneScreen CurrentApp => CurrentAppId=="witchform"?PhoneScreen.Witchform:CurrentAppId=="x"?PhoneScreen.X:PhoneScreen.Home;
        [ShowInInspector,ReadOnly,PropertyOrder(-19),LabelText("등록 상태")]
        public string RegistryIssue { get; private set; } = string.Empty;
        [ShowInInspector,ReadOnly,PropertyOrder(-18),LabelText("확대 표시")] public bool IsFocused => focused;
        [ShowInInspector,ReadOnly,PropertyOrder(-17),LabelText("입력 허용")]
        public bool CanUsePhone => isActiveAndEnabled && focused && inputEnabled && phoneRoot!=null && phoneRoot.gameObject.activeInHierarchy;
        private void Awake() { BuildRegistry(); ShowScreen("home"); }
        private void OnEnable() { BuildRegistry(); Wire(); RefreshInput(); }
        private void OnDisable() { Unwire(); inputEnabled=false; RefreshInput(); }
        public void ConfigureApps(AppEntry[] entries)
        {
            Unwire(); apps=entries==null?Array.Empty<AppEntry>():(AppEntry[])entries.Clone();
            BuildRegistry(); ShowScreen("home"); if(isActiveAndEnabled) Wire();
        }
        private void BuildRegistry()
        {
            registered.Clear(); RegistryIssue=string.Empty;
            var invalid=new HashSet<string>(StringComparer.Ordinal);
            var launchers=new HashSet<UnityEngine.UI.Button>();
            var screens=new HashSet<Transform>();
            foreach(var app in apps)
            {
                if(app==null || string.IsNullOrEmpty(app.Id) || app.Id=="home" || app.Screen==null || app.Screen==homeScreen || app.Launcher==null)
                { RegistryIssue="누락되거나 예약된 앱 연결"; continue; }
                if(invalid.Contains(app.Id)) continue;
                if(registered.ContainsKey(app.Id))
                { registered.Remove(app.Id); invalid.Add(app.Id); RegistryIssue="중복 앱 ID"; continue; }
                if(!launchers.Add(app.Launcher)) { RegistryIssue="중복 실행 버튼"; continue; }
                if(!screens.Add(app.Screen)) { RegistryIssue="중복 앱 화면"; continue; }
                registered.Add(app.Id,app);
            }
        }
        private void Wire()
        {
            Unwire();
            foreach(var pair in registered)
            {
                string id=pair.Key; UnityAction action=()=>OpenApp(id);
                pair.Value.Launcher.onClick.AddListener(action); listeners.Add((pair.Value.Launcher,action));
            }
            if(homeButton!=null) homeButton.onClick.AddListener(ShowHome);
            if(backButton!=null) backButton.onClick.AddListener(GoBack);
        }
        private void Unwire()
        {
            foreach(var entry in listeners) if(entry.button!=null) entry.button.onClick.RemoveListener(entry.callback);
            listeners.Clear();
            if(homeButton!=null) homeButton.onClick.RemoveListener(ShowHome);
            if(backButton!=null) backButton.onClick.RemoveListener(GoBack);
        }
        public void Bind(OrderDisplayData value)
        { witchformApp?.Bind(value); xFeedApp?.ResetFeed(); ShowScreen("home"); }
        [TabGroup("InspectorTabs","개발 도구"),Button("표시 지우기")]
        public void Clear()
        { witchformApp?.Clear(); xFeedApp?.ResetFeed(); ShowScreen("home"); SetInputEnabled(false); }
        public void SetFocused(bool value)
        {
            if(focused==value) { RefreshInput(); return; }
            focused=value; witchformApp?.SetFocused(value); ShowScreen("home");
            if(!value) inputEnabled=false; RefreshInput();
        }
        public void SetInputEnabled(bool value) { inputEnabled=value; RefreshInput(); }
        public bool OpenApp(string appId)
        {
            if(!CanUsePhone || string.IsNullOrEmpty(appId) || !registered.ContainsKey(appId)) return false;
            ShowScreen(appId); return true;
        }
        public void OpenWitchform() => OpenApp("witchform");
        public void OpenX() => OpenApp("x");
        public void GoBack() { if(CanUsePhone) ShowScreen("home"); }
        public void ShowHome() { if(CanUsePhone) ShowScreen("home"); }
        private void ShowScreen(string appId)
        {
            CurrentAppId=appId;
            if(homeScreen!=null) homeScreen.gameObject.SetActive(appId=="home");
            registered.TryGetValue(appId,out var current);
            var visited=new HashSet<Transform>();
            foreach(var app in apps) if(app!=null && app.Screen!=null && app.Screen!=homeScreen)
                if(visited.Add(app.Screen)) app.Screen.gameObject.SetActive(current!=null && current.Screen==app.Screen);
            RefreshInput();
        }
        private void RefreshInput()
        {
            bool allowed=CanUsePhone;
            if(phoneInput!=null) phoneInput.interactable=phoneInput.blocksRaycasts=allowed;
            foreach(var app in apps) if(app!=null && app.Launcher!=null) app.Launcher.interactable=allowed && registered.ContainsKey(app.Id??string.Empty);
            if(homeButton!=null) homeButton.interactable=allowed && CurrentAppId!="home";
            if(backButton!=null) backButton.interactable=allowed && CurrentAppId!="home";
            xFeedApp?.SetInputEnabled(allowed && CurrentAppId=="x");
        }
    }
}
