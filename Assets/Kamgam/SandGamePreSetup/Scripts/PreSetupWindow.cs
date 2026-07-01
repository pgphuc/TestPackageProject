#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kamgam.SandGame.PreSetup
{
    public class PreSetupWindow : EditorWindow
    {
        public const string Version = "1.1.1";

        public const string AssetName = "Sand Game";
        public static string AssetRootPath = "Assets/Kamgam/SandGamePreSetup/";
        public static string AssetRootPathOfSandGame = "Assets/Kamgam/SandGame/";
        public static string PackagePath = "Assets/Kamgam/SandGamePreSetup/SandGame.unitypackage";
        public const string ManualUrl = "https://kamgam.com/unity/SandGameManual.pdf";

        public static Version GetVersion() => new Version(Version);

        [UnityEditor.Callbacks.DidReloadScripts(998001)]
        public static void ShowIfNeeded()
        {
            bool versionChanged = PreSetupVersionHelper.UpgradeVersion(GetVersion, out Version oldVersion, out Version newVersion);
            if (versionChanged)
            {
                Debug.Log(AssetName + " version changed from " + oldVersion + " to " + newVersion);

                show();
            }
        }

        [MenuItem("Tools/" + AssetName + "/Pre Install Check", priority = 999)]
        public static void Setup()
        {
            show();
        }

        private static void show()
        {
            PreSetupWindow wnd = GetWindow<PreSetupWindow>();
            wnd.titleContent = new GUIContent("Sand Game Pre Install Check");

            const int width = 450;
            const int height = 500;
            var x = Screen.currentResolution.width / 2 - width;
            var y = Screen.currentResolution.height / 2 - height;
            wnd.position = new Rect(x, y, width, height);
            wnd.Show(immediateDisplay: true);
        }


        // 0 = not yet checked, 1 = checking, 2 = success, 3 = fail.
        protected int _checkingPackages = 0;
        protected bool _installedPackageBURST = false;
        protected bool _installedPackageAddressables = false;
        protected bool _installedPackageCollections = false;
        protected bool _installedPackageMathematics = false;
        protected bool _checkingPackageResourceManager = false;

        public void OnGUI()
        {
            GUILayout.BeginHorizontal();
            drawLabel("Sand", color: new Color(1f, 1f, 0.1f), bold: true, fontSize: 32);
            drawLabel(" Game", color: new Color(1f, 0.8f, 0.1f), bold: true, fontSize: 32);
            drawLabel(" Template", color: new Color(1f, 0.6f, 0.1f), bold: true, fontSize: 32);
            if (drawButton(" ", icon: "_Help"))
            {
                Application.OpenURL(ManualUrl);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            bool allInstalled = _installedPackageBURST && _installedPackageAddressables && _installedPackageCollections && _installedPackageMathematics;
            GUILayout.Label("Required packages", EditorStyles.boldLabel);
            if (allInstalled)
            {
                drawLabel("All packages are installed. You are ready to go!");
                EditorScheduler.Cancel("CheckPackagesLoop");
            }
            else
            {
                drawLabel("Before you can install the Sand Game Template please make sure these packages are installed:");
                CheckPackagesLoop();
            }
            if (_checkingPackages == 1)
            {
                GUILayout.Label("  [Checking..] BURST");
                GUILayout.Label("  [Checking..] Addressables");
                GUILayout.Label("  [Checking..] Collections");
                GUILayout.Label("  [Checking..] Mathematics");
            }
            else if (_checkingPackages == 2)
            {
                var col = GUI.color;
                
                GUI.color = _installedPackageBURST ? Color.green : Color.red;
                GUILayout.Label("  [" + (_installedPackageBURST ? "OK" : "MISSING") + "] BURST");
                if (!_installedPackageBURST)
                {
                    if (GUILayout.Button("Install BURST package"))
                    {
                        UnityEditor.PackageManager.UI.Window.Open("com.unity.burst");
                    }
                }

                GUI.color = _installedPackageMathematics ? Color.green : Color.red;
                GUILayout.Label("  [" + (_installedPackageMathematics ? "OK" : "MISSING") + "] Mathematics");
                if (!_installedPackageMathematics)
                {
                    if (GUILayout.Button("Install Mathematics package"))
                    {
                        UnityEditor.PackageManager.UI.Window.Open("com.unity.mathematics");
                    }
                }

                bool addressablesInitialized = areAddressableSettingsAvailable();
                GUI.color = _installedPackageAddressables ? Color.green : Color.red;
                if (_installedPackageAddressables && !addressablesInitialized)
                {
                    GUI.color = Color.yellow;
                }
                GUILayout.Label("  [" + (_installedPackageAddressables ? (addressablesInitialized ? "OK" : "NOT INITIALIZED") : "MISSING") + "] Addressables");
                if (!_installedPackageAddressables)
                {
                    if (GUILayout.Button("Install Addressables package"))
                    {
                        UnityEditor.PackageManager.UI.Window.Open("com.unity.addressables");
                    }
                }
                else if (!areAddressableSettingsAvailable())
                {
                    if (GUILayout.Button("Initialize Addressables")) 
                    {
                        EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
                    }
                }

                GUI.color = _installedPackageCollections ? Color.green : Color.red;
                GUILayout.Label("  [" + (_installedPackageCollections ? "OK" : "MISSING") + "] Collections");
                if (!_installedPackageCollections)
                {
                    if (GUILayout.Button("Install Collections package"))
                    {
                        UnityEditor.PackageManager.UI.Window.Open("com.unity.collections");
                    }
                }

                GUI.color = col;
            }
            else if (_checkingPackages == 3)
            {
                drawLabel("  Checking packages FAILED! Please contact support.");
            }

            if (_checkingPackages == 0)
            {
                EditorApplication.delayCall += CheckPackages;
            }
            GUI.enabled = _checkingPackages != 1 && !allInstalled;
            drawLabel("Hit this button to refresh the installed packages:");
            if (GUILayout.Button("Refresh installed packages"))
            {
                EditorApplication.delayCall += CheckPackages;
            }
            GUI.enabled = true;

            GUILayout.Space(20);
            drawLabel("Install Sand Game Template", bold: true);
            if (!allInstalled)
            {
                drawLabel("You can install the sand game template once all required packages are installed.");
            }
            else
            {
                drawLabel("All packages are installed. You can now install the Sand Game.");
            }
            GUI.enabled = allInstalled;
            if (GUILayout.Button("Install Sand Game Template"))
            {
                EditorApplication.delayCall += InstallFromPackage;
            }
            GUI.enabled = true;

            GUILayout.Space(20);
            bool installed = System.IO.Directory.Exists(AssetRootPathOfSandGame);
            if(installed)
            {
                drawLabel("Sand Game is installed, YAY!", color: Color.green, bold: true);
                drawLabel("You can close this window now.");

                drawLabel("NOTICE", color: Color.yellow, bold: true);
                drawLabel("Depending on your Unity version you may have to assign a 'Content Packing Loading' schema to the 'Sand Game Assets' group. If the demo levels do not load in a build then that's probably why.", color: Color.yellow, bold: false);

                if (GUILayout.Button("Open 'Sand Game Assets' group"))
                {
                    var group = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Kamgam/SandGame/Addressables/Sand Game Assets.asset");
                    EditorGUIUtility.PingObject(group);
                    Selection.objects = new UnityEngine.Object[] { group };
                }

                if (GUILayout.Button("Open Manual"))
                {
                    Application.OpenURL(ManualUrl);
                }

                GUILayout.Space(7);

                if (GUILayout.Button("Open Demo Scene"))
                {
                    Close();

                    EditorApplication.delayCall += () =>
                    {
                        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Kamgam/SandGame/Scenes/SandGame.unity");
                        EditorGUIUtility.PingObject(scene);
                        EditorSceneManager.OpenScene("Assets/Kamgam/SandGame/Scenes/SandGame.unity");
                    };
                }
            }
        }

        protected bool areAddressableSettingsAvailable()
        {
#if KAMGAM_SANDGAME_ADDRESSABLES
            return UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings != null;
#else
            return false;
#endif
        }

        public void OnDisable()
        {
            EditorScheduler.Cancel("CheckPackagesLoop");
        }

        public void CheckPackagesLoop()
        {
            if (!EditorScheduler.HasId("CheckPackagesLoop"))
            {
                EditorScheduler.Schedule(2f, CheckPackagesLoop, "CheckPackagesLoop");
                EditorApplication.delayCall += CheckPackages;
            }
        }

        public void CheckPackages()
        {
            PackageChecker.LoadPackagesInfos(onPackagesChecked);
            _checkingPackages = 1;
        }

        private void onPackagesChecked(ListRequest request)
        {
            _checkingPackages = request.Status == UnityEditor.PackageManager.StatusCode.Success ? 2 : 3;
            if (_checkingPackages != 2)
                return;

            _installedPackageBURST = PackageChecker.ContainsPackage(request, "com.unity.burst");
            _installedPackageMathematics = PackageChecker.ContainsPackage(request, "com.unity.mathematics");
            _installedPackageAddressables = PackageChecker.ContainsPackage(request, "com.unity.addressables");
            _installedPackageCollections = PackageChecker.ContainsPackage(request, "com.unity.collections");

            Repaint();
        }

        public void InstallFromPackage()
        {
            PackageImporter.Import(null);
        }

        private static void drawLabel(string text, Color color, bool bold = false, int fontSize = -1)
        {
            var skin = new GUIStyle(GUI.skin.label);
            skin.wordWrap = true;
            skin.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            skin.normal.textColor = color;
            if (fontSize >= 0)
                skin.fontSize = fontSize;
            GUILayout.Label(text, skin);
        }

        private static void drawLabel(string text, bool bold = false)
        {
            var skin = GUI.skin.label;
            skin.wordWrap = true;
            skin.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            GUILayout.Label(text, skin);
        }

        private static bool drawButton(string text, string tooltip = null, string icon = null, params GUILayoutOption[] options)
        {
            GUIContent content;

            // icon
            if (!string.IsNullOrEmpty(icon))
                content = EditorGUIUtility.IconContent(icon);
            else
                content = new GUIContent();

            // text
            content.text = text;

            // tooltip
            if (!string.IsNullOrEmpty(tooltip))
                content.tooltip = tooltip;

            return GUILayout.Button(content, options);
        }
    }
}
#endif