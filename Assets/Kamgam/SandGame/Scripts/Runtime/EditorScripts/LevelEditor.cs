#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Kamgam.SandGame;

namespace Kamgam.SandGame.LevelEditing
{
    [ExecuteAlways]
    public class LevelEditor : MonoBehaviour
    {
        public bool RemoveInPlayMode = true;

        private void Awake()
        {
            // Simulate built behaviour in editor by destroying it in awake.
            if (RemoveInPlayMode && EditorApplication.isPlayingOrWillChangePlaymode)
                GameObject.DestroyImmediate(this.gameObject);
        }

        void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

        }

        void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

        }
    }
}
#endif