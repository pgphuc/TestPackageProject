using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Kamgam.SandGame
{
    public class SandGame : MonoBehaviour
    {
        [Header("UI")]
        public Canvas LoadingCanvas;
        public Canvas MenuCanvas;
        public InGameMenu GameCanvas;

        [Header("References")]
        public Camera Camera;

        /// <summary>
        /// Notice that SandWorld is THE BehaviourController that pumps all the ControlledBehaviours.
        /// </summary>
        public SandWorld SandWorld;

        [System.NonSerialized]
        public PixelMaterialId DrawingMaterialId = PixelMaterialId.Sand;
        protected Vector3? _lastMousePixelPos;

        public void Start()
        {
            QualitySettings.vSyncCount = 1;

            // You want to keep the two frame rates in sync or else the pixel world
            // simulation might diverge from your Time.deltaTime based animations.
#if UNITY_ANDROID
            Application.targetFrameRate = 30;
            SandWorld.PixelWorld.FrameRate = 30;
#else
            Application.targetFrameRate = 60;
            SandWorld.PixelWorld.FrameRate = 60;
#endif

            // Performance Debugging on PC (don't forget to disable v-sync)
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 4000;
#endif

            LoadingCanvas.gameObject.SetActive(false);
            MenuCanvas.gameObject.SetActive(true);
            GameCanvas.gameObject.SetActive(false);
            GameCanvas.Init(this, DrawingMaterialId);
        }

        public void ShowMenu()
        {
            MenuCanvas.gameObject.SetActive(true);
        }

        public void HideMenu()
        {
            if (GameCanvas.gameObject.activeInHierarchy)
            {
                MenuCanvas.gameObject.SetActive(false);
            }
        }

        public void LoadLevel(int level)
        {
            LoadingCanvas.gameObject.SetActive(true);
            MenuCanvas.gameObject.SetActive(false);
            GameCanvas.gameObject.SetActive(false);

            SandWorld.LoadLevel(level, onLevelLoaded);
        }

        protected void onLevelLoaded(int level)
        {
            LoadingCanvas.gameObject.SetActive(false);
            MenuCanvas.gameObject.SetActive(false);
            GameCanvas.gameObject.SetActive(true);
        }

        public void UnloadLevel()
        {
            SandWorld.Unload();
        }

        public void Update()
        {
            if (SandWorld.PixelWorld.LoadSucceeded)
            {
                //testMoveCameraInCircle();
                testMoveCameraWithKeys(speed: 10f);
            }

            if (SandWorld.PixelWorld.LoadSucceeded)
            {
                // Touch
                if (Input.touchCount > 0)
                {
                    for (int t = 0; t < Input.touchCount; t++)
                    {
                        Touch touch = Input.GetTouch(t);
                        if (touch.position.x < Screen.width * 0.4f)
                        {
                            continue;
                        }

                        var pixelPos = SandWorld.PixelWorld.ScreenToPixelPos(touch.position);
                        drawAt(bigBrush: false, pixelPos);
                        break;
                    }
                }

                // Keyboard
                else if (
                       ((Input.GetMouseButton(0) && !Input.GetKey(KeyCode.LeftControl))
                    ||  (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftControl)))
                    && !EventSystem.current.IsPointerOverGameObject()
                    )
                {
                    bool isShiftPressed = Input.GetKey(KeyCode.LeftShift);
                    var pixelPos = SandWorld.PixelWorld.MouseToPixelPos(Input.mousePosition);
                    drawAt(isShiftPressed, pixelPos);
                }
                else
                {
                    _lastMousePixelPos = null;
                }
            }
        }

        private void drawAt(bool bigBrush, Vector3 pixelPos)
        {
            if (!_lastMousePixelPos.HasValue)
            {
                SandWorld.PixelWorld.DrawBrushAt(PixelWorldBrushShape.Circle, bigBrush ? 20 : 5, pixelPos.x, pixelPos.y, DrawingMaterialId);
            }
            else
            {
                SandWorld.PixelWorld.DrawLine(PixelWorldBrushShape.Circle, bigBrush ? 20 : 5, _lastMousePixelPos.Value.x, _lastMousePixelPos.Value.y, pixelPos.x, pixelPos.y, DrawingMaterialId);
            }

            _lastMousePixelPos = pixelPos;
        }

        private void testMoveCameraWithKeys(float speed)
        {
            float scaledSpeed = speed * Time.deltaTime * 2f;

            var pos = Camera.transform.position;

            if (Input.touchCount > 0)
            {
                for (int t = 0; t < Input.touchCount; t++)
                {
                    Touch touch = Input.GetTouch(t);

                    // Only move in left screen.
                    if (touch.position.x < Screen.width * 0.4f)
                    {
                        var direction = touch.position - new Vector2(
                            (Screen.width * 0.2f),
                            (Screen.height * 0.5f)
                        );
                        direction.Normalize();

                        pos.x += direction.x * scaledSpeed;
                        pos.y += direction.y * scaledSpeed;
                    }
                }
            }
            else
            {
                float faster = Input.GetKey(KeyCode.LeftShift) ? 4f : 1f;
                pos.x -= (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) ? scaledSpeed * faster : 0f;
                pos.x += (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) ? scaledSpeed * faster : 0f;

                pos.y -= (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) ? scaledSpeed * faster : 0f;
                pos.y += (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) ? scaledSpeed * faster : 0f;
            }

            SandWorld.PixelWorld.MoveCameraToWorldPos(Camera, pos.x, pos.y);
        }

        private void testMoveCameraInCircle()
        {
            var x = Mathf.Sin(Time.realtimeSinceStartup) * 70f;
            var y = Mathf.Cos(Time.realtimeSinceStartup) * 70f;

            SandWorld.PixelWorld.MoveCameraToPixelPos(Camera, x, y);
        }

        public void Quit()
        {
            Application.Quit();
        }
    }
}
