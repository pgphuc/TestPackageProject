using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kamgam.SandGame
{
    public class DebugTexture : MonoBehaviour
    {
        public static DebugTexture Instance;

        public Color clearColor = Color.clear;
        public RenderTexture texture;

        private void Awake()
        {
            Instance = this;
        }

        public static void Clear()
        {
            if (Instance == null)
            {
                logMissing();
                return;
            }

            var previousActive = RenderTexture.active;
            RenderTexture.active = Instance.texture;
            GL.Clear(true, true, Instance.clearColor);
            RenderTexture.active = previousActive;
        }
        public static void DrawRect(int xMin, int yMin, int xMax, int yMax, Color color)
        {
            if (Instance == null)
            {
                logMissing();
                return;
            }

            Instance.DrawRectangleOutline(xMin, yMin, xMax, yMax, color);
        }

        private static void logMissing()
        {
            Debug.LogError("No DebugTexture in scene. Add one and link a render texture.");
        }

        public void DrawRectangleOutline(int xMin, int yMin, int xMax, int yMax, Color color)
        {
            var previousActive = RenderTexture.active;

            RenderTexture.active = texture;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, texture.width, texture.height, 0);

            Material material = new Material(Shader.Find("Hidden/Internal-Colored"));
            material.SetPass(0);

            GL.Begin(GL.LINES);
            GL.Color(color);

            xMin += texture.width / 2;
            xMax += texture.width / 2;
            yMin += texture.height / 2;
            yMax += texture.height / 2;

            // Top
            DrawLine(new Vector2(xMin, yMin), new Vector2(xMax, yMin));

            // Bottom
            DrawLine(new Vector2(xMin, yMax), new Vector2(xMax, yMax));

            // Left
            DrawLine(new Vector2(xMin, yMin), new Vector2(xMin, yMax));

            // Right
            DrawLine(new Vector2(xMax, yMin), new Vector2(xMax, yMax));

            GL.End();
            GL.PopMatrix();

#if UNITY_EDITOR
            EditorUtility.SetDirty(texture);
#endif

            RenderTexture.active = previousActive;
        }

        void DrawLine(Vector2 start, Vector2 end)
        {
            GL.Vertex(start);
            GL.Vertex(end);
        }
    }
}