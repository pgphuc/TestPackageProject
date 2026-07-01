#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Kamgam.SandGame;

namespace Kamgam.SandGame.LevelEditing
{
    /// <summary>
    /// Removes the LevelEditor objects from all scene during build.
    /// </summary>
    public class LevelEditorBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => int.MinValue + 100;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Debug.Log("Sand Game: Removing Level Editor components from build scene '" + scene.name + "'.");

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var comps = root.GetComponentsInChildren<LevelEditor>(includeInactive: true);
                foreach (var comp in comps)
                {
                    GameObject.DestroyImmediate(comp.gameObject);
                }
            }
        }
    }
}
#endif