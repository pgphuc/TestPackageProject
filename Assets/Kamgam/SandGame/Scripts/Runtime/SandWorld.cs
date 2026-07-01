using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Kamgam.SandGame
{
    /// <summary>
    /// Notice that the SandWorld is of type "BehaviourController". This means this is
    /// the MonoBehaviour that drives all the "ControlledBehaviours". If you want your
    /// own classes to be synced with the SandWorld Update flow then use a ControlledBehaviour
    /// as the base class instead of a MonoBehaviour.<br />
    /// <br />
    /// The SandWorld contains a PixelWorld and a PixelCanvas but it also loads the UnityScene
    /// for each level.
    /// </summary>
    public class SandWorld : MonoBehaviour
    {
        [Header("References")]
        public PixelCanvas PixelCanvas;
        public PixelWorld PixelWorld;

        [Header("Resolution")]
        [Tooltip("The width and height of your level images covering roughly one screen.")]
        public int Width = 320;
        public int Height = 280;
        public int PixelsPerUnit = 8;

        [Header("Simulation")]
        /// <summary>
        /// The max frame rate at which the simulation will run.<br />
        /// It works similar to fixed physics updates. Meaning it may run 0, 1 or multiple times per frame.<br />
        /// If you increase the frame rate then then simulated pixels will be sped up in comparison to free falling pixels.<br />
        /// The simulation code is tweaked to give nice results at 60 fps. Do NOT change this unless you absolutely need to.
        /// </summary>
        [Tooltip("The max frame rate at which the simulation will run.\n" +
            "It works similar to fixed physics updates. Meaning it may run 0, 1 or multiple times per frame.\n" +
            "If you increase the frame rate then then simulated pixels will be sped up in comparison to not " +
            "simulated content (free-falling pixels or Unity scene objects)\n" +
            "The simulation code is tweaked to give nice results at 60 fps. Do NOT change this unless you " +
            "absolutely need to.")]
        [Range(0, 240)]
        [SerializeField]
        protected int _simulationFrameRate = 60;
        protected int SimulationFrameRate
        {
            get => _simulationFrameRate;
            set
            {
                _simulationFrameRate = value;

                if (PixelWorld != null)
                    PixelWorld.FrameRate = _simulationFrameRate;
            }
        }

        /// <summary>
        /// If enabled then the simulations actual simulation frame rate is capped to the
        /// real application frame rate (i.e. the simulation is executed at most once per frame).
        /// </summary>
        [SerializeField]
        [Tooltip("If enabled then the simulations actual simulation frame rate is capped to the " +
            "real application frame rate (i.e. the simulation is executed at most once per frame).\n" +
            "If the frame rate drops too much then this may lead to diverging speeds between simulated " +
            "and not simulated content (free-falling pixels or Unity scene objects).")]
        protected bool _limitToApplicationFrameRate = true;
        protected bool LimitToApplicationFrameRate
        {
            get => _limitToApplicationFrameRate;
            set
            {
                _limitToApplicationFrameRate = value;

                if (PixelWorld != null)
                    PixelWorld.LimitToApplicationFrameRate = _limitToApplicationFrameRate;
            }
        }

        public float WorldTemperature = 20f;

        [Header("Physics")]
        public PolygonCollider2D Collider;

        [Header("Camera")]
        public Camera Camera;

        protected AsyncOperationHandle<SceneInstance> _levelSceneHandle;

        protected Scene _levelScene;
        public Scene LevelScene => _levelScene;

        public void Start()
        {
            PixelWorld = new PixelWorld();
            PixelWorld.FrameRate = SimulationFrameRate;
            PixelWorld.LimitToApplicationFrameRate = LimitToApplicationFrameRate;
            PixelWorld.WorldTemperature = WorldTemperature;
            PixelWorld.Collider = Collider;
            PixelWorld.Initialize(PixelsPerUnit, Width, Height, Camera, PixelCanvas);
        }

        public void LoadLevel(int level, System.Action<int> onLevelLoaded)
        {
            ResetCameraPosition();

            if (PixelWorld.IsLoading)
                return;

            unloadLevelScene();

            PixelWorld.LoadLevelAsync(level, (lvl) => onPixelWorldLoadComplete(lvl, onLevelLoaded));
        }

        protected void onPixelWorldLoadComplete(int level, System.Action<int> onLevelLoaded)
        {
            PixelWorld.ViewPortMinX = 0;
            PixelWorld.ViewPortMinY = 0;
            PixelWorld.MatchCameraToViewportPos(Camera);

            // Proceed with loading the Unity scene for this level.
            StartCoroutine(loadLevelScene(level, onLevelLoaded));
        }

        protected IEnumerator loadLevelScene(int level, System.Action<int> onLevelLoaded)
        {
            var levelInfo = LevelInfo.GetLoadedLevelInfo(level);
            if (levelInfo != null && levelInfo.HasScene())
            {
                _levelSceneHandle = Addressables.LoadSceneAsync(levelInfo.Scene.RuntimeKey, LoadSceneMode.Additive, activateOnLoad: true);
                
                yield return _levelSceneHandle;

                if(_levelSceneHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    _levelScene = _levelSceneHandle.Result.Scene;
                }
                onLevelLoaded(level);
            }
            else
            {
                onLevelLoaded(level);
            }
        }

        protected void unloadLevelScene()
        {
            if (_levelSceneHandle.IsValid())
            {
                Addressables.UnloadSceneAsync(_levelSceneHandle, autoReleaseHandle: true);
                _levelScene = default;
            }
        }

        public void Unload()
        {
            if (PixelWorld.IsLoading)
                return;

            ResetCameraPosition();
            unloadLevelScene();
            PixelWorld.Unload();
        }

        public void ResetCameraPosition()
        {
            var pos = Camera.transform.position;
            pos.x = 0;
            pos.y = 0;
            Camera.transform.position = pos;
        }

        public void OnDestroy()
        {
            PixelWorld.Dispose();
        }

        public void LateUpdate()
        {
            // Schedule threads to do the simulation and copy pixel work AFTER all the update code has run.
            // This is where all the simulation work is dones.
            PixelWorld.LateUpdateAfterCode();
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // Update the camera orthographic size to match the height in pixels.
                if (Camera != null && PixelsPerUnit > 0)
                {
                    PixelWorld.MatchCameraSizeToHeight(Camera, Height, PixelsPerUnit);
                }
            }

            SimulationFrameRate = _simulationFrameRate;
            LimitToApplicationFrameRate = _limitToApplicationFrameRate;
        }
#endif
    }
}
