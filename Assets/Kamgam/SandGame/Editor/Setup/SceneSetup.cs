#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Kamgam.SandGame
{
    public static class SceneSetup
    {
        #region delayed
        static double startAt;

        public static void SetupBuildScenesDelayed()
        {
            EditorApplication.update -= onEditorUpdate;
            EditorApplication.update += onEditorUpdate;
            startAt = EditorApplication.timeSinceStartup + 3; // wait N seconds
        }

        static void onEditorUpdate()
        {
            // wait for the time to reach startAt
            if (startAt - EditorApplication.timeSinceStartup < 0)
            {
                EditorApplication.update -= onEditorUpdate;
                SetupBuildScenes();
                return;
            }
        }
        #endregion

        [MenuItem("Tools/Sand Game/Debug/Setup Build Scenes")]
        public static void SetupBuildScenes()
        {
            Debug.Log("Adding scenes to build");

            string[] scenePaths = new string[]
            {
                "Assets/Kamgam/SandGame/Scenes/SandGame.unity"
            };

            List<EditorBuildSettingsScene> editorBuildSettingsScenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in scenePaths)
            {
                // Skip if already in the list.
                var existingScene = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.path == path);
                if (existingScene != null)
                {
                    Debug.Log("Skipping scene '" + path + "' because it already is part of the build.");
                    continue;
                }

                // Add (make sure SandGame.unity is the first scene).
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (asset != null)
                {
                    if (path.EndsWith("SandGame.unity"))
                        editorBuildSettingsScenes.Insert(0, new EditorBuildSettingsScene(path, enabled: true));
                    else
                        editorBuildSettingsScenes.Add(new EditorBuildSettingsScene(path, enabled: true));
                    Debug.Log("Added scene '" + path + "'.");
                }
                else
                {
                    Debug.LogWarning("Could not add scene '" + path + "'.");
                }
            }
            EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
        }
    }
}
#endif