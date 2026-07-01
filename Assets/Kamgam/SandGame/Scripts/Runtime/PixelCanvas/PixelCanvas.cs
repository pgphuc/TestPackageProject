using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Kamgam.SandGame
{
    public class PixelCanvas : MonoBehaviour
    {
        public Transform Stabilizer;

        public int Width { get; protected set; }
        public float PreciseWidth { get; protected set; }
        public int Height { get; protected set; }

        public Material Material;

        [Tooltip("The render target will be reiszed to fill the whole screen.")]
        public MeshRenderer RenderTarget;

        // canvas clear color
        public Color32 ClearColor = new Color32(0, 0, 0, 0);

        // Byte array for texture painting, these are just colors.
        // Buffer array layout:
        // ------>---end
        // -----<-------
        // rgba,rgba,->-
        private NativeArray<byte> colorBuffer;

        // Texture that we paint into (it gets updated from pixels[] array)
        private Texture2D canvasTexture;

        // Copy pixel job handles
        protected List<JobHandle> _jobHandles = new List<JobHandle>();

        protected PixelWorld.ScheduleChunkJobDelegate _scheduleChunkJobDelegate;

        public void Reinitialize(int width, int height)
        {
            RenderTarget.gameObject.SetActive(true);

            Width = width + 2;
            Height = height + 2;

            initTexture();

            // Test
            // SetFilledRect(100, 100, 100, 100, 255, 255, 0, 255);
            // CopyPixelsToTexture();

            _scheduleChunkJobDelegate = scheduleChunkJob;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CoordinatesToIndex(int x, int y, int width)
        {
            return (y * width + x) * 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPixel(NativeArray<byte> pixelColors, int width, int x, int y, byte r, byte g, byte b, byte a)
        {
            int index = CoordinatesToIndex(x, y, width);
            pixelColors[index] = r;
            pixelColors[index + 1] = g;
            pixelColors[index + 2] = b;
            pixelColors[index + 3] = a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddToPixel(NativeArray<byte> pixelColors, int width, int x, int y, byte r, byte g, byte b, byte a)
        {
            int index = CoordinatesToIndex(x, y, width);
            pixelColors[index] = (byte)(pixelColors[index] + r);
            pixelColors[index + 1] = (byte)(pixelColors[index + 1] + g);
            pixelColors[index + 2] = (byte)(pixelColors[index + 2] + b);
            pixelColors[index + 3] = (byte)(pixelColors[index + 3] + a);
        }

        public void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
        {
            int index = CoordinatesToIndex(x, y, Width);
            colorBuffer[index] = r;
            colorBuffer[index + 1] = g;
            colorBuffer[index + 2] = b;
            colorBuffer[index + 3] = a;
        }

        public void SetPixel(int x, int y, Color32 color)
        {
            int index = CoordinatesToIndex(x, y, Width);
            colorBuffer[index] = color.r;
            colorBuffer[index + 1] = color.g;
            colorBuffer[index + 2] = color.b;
            colorBuffer[index + 3] = color.a;
        }

        public void SetPixel(int x, int y, Color color)
        {
            int index = CoordinatesToIndex(x, y, Width);
            colorBuffer[index] = (byte)Mathf.RoundToInt(color.r * 255);
            colorBuffer[index + 1] = (byte)Mathf.RoundToInt(color.g * 255);
            colorBuffer[index + 2] = (byte)Mathf.RoundToInt(color.b * 255);
            colorBuffer[index + 3] = (byte)Mathf.RoundToInt(color.a * 255);
        }

        public void SetFilledRect(
            int xMin, int yMin, int width, int height,
            byte r, byte g, byte b, byte a)
        {
            if (xMin < 0 || xMin > Width || yMin < 0 || yMin > Height)
                return;

            int xMax = xMin + width;
            int yMax = yMin + height;
            for (int x = xMin; x < xMax; x++)
            {
                for (int y = yMin; y < yMax; y++)
                {
                    SetPixel(x, y, r, g, b, a);
                }
            }
        }

        public void SetRect(
            int xMin, int yMin, int width, int height,
            byte r, byte g, byte b, byte a)
        {
            if (xMin < 0 || xMin > Width || yMin < 0 || yMin > Height)
                return;

            int xMax = xMin + width;
            int yMax = yMin + height;
            for (int x = xMin; x < xMax; x++)
            {
                for (int y = yMin; y < yMax; y++)
                {
                    if (x == xMin || x == xMax - 1 || y == yMin || y == yMax - 1)
                        SetPixel(x, y, r, g, b, a);
                }
            }
        }

        protected void initTexture()
        {
            if (canvasTexture == null)
            {
                canvasTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, mipChain: false);
            }
            else
            {
                canvasTexture.Reinitialize(Width, Height, TextureFormat.RGBA32, hasMipMap: false);
            }
            // set texture modes
            canvasTexture.filterMode = FilterMode.Point;
            canvasTexture.wrapMode = TextureWrapMode.Clamp;

            Material.mainTexture = canvasTexture;

            // init pixels array
            colorBuffer = canvasTexture.GetRawTextureData<byte>();
            clearColorBuffer();
        }

        public void Clear()
        {
            clearColorBuffer();
        }

        protected void clearColorBuffer()
        {
            int numOfPixelColorChannels = Height * Width * 4;
            for (int p = 0; p < numOfPixelColorChannels; p += 4)
            {
                colorBuffer[p] = ClearColor.r;
                colorBuffer[p + 1] = ClearColor.g;
                colorBuffer[p + 2] = ClearColor.b;
                colorBuffer[p + 3] = ClearColor.a;
            }

            SendColorBufferToGraphicsCard();
        }

        public void CopyPixelsToColorBufferMultithreaded(
            PixelWorld pixelWorld,
            int chunkWidth, int chunkHeight,
            int areaMinX, int areaMinY
            )
        {
            // Move -1 px in both directions due to the 1 px margin (see +2 on width and height in Reinitialize()).
            areaMinX -= 1;
            areaMinY -= 1;

            _jobHandles.Clear();

            pixelWorld.ScheduleChunkJobs(
                pixelWorld.ActiveChunks, chunkWidth, chunkHeight,
                areaMinX, areaMinY, Width, Height,
                minPixelsPerBatch: 1000,
                allowAccessToLoadingChunks: false,
                _scheduleChunkJobDelegate,
                _jobHandles);

            // Debug.Log("ChunkToCanvasJob jobs: " + _jobHandles.Count);

            // Start executing jobs now.
            // Notice: we do not wait for completion here (see WaitForCompletion).
            JobHandle.ScheduleBatchedJobs();

            JobUtils.WaitForJobs(_jobHandles);
        }

        protected void scheduleChunkJob(
            List<JobHandle> jobHandles,
            NativeArray<Pixel> chunkPixels,
            int chunkWidth, int chunkHeight, int xMinInChunk, int xMaxInChunk, int yMinInChunk, int yMaxInChunk,
            int areaWidth, int areaHeight, int xMinInArea, int xMaxInArea, int yMinInArea, int yMaxInArea,
            int xMinInWorld, int yMinInWorld)
        {
            var handle = new ChunkToCanvasJob()
            {
                // In
                PixelsInChunk = chunkPixels,
                ChunkWidth = chunkWidth,
                XMinInChunk = xMinInChunk,
                XMaxInChunk = xMaxInChunk,
                YMinInChunk = yMinInChunk,
                YMaxInChunk = yMaxInChunk,
                ViewportWidth = areaWidth,
                XMinInCanvas = xMinInArea,
                YMinInCanvas = yMinInArea,
                // Out
                Colors = colorBuffer
            }.Schedule();
            jobHandles.Add(handle);
        }

        public void SendColorBufferToGraphicsCard()
        {
            if (canvasTexture != null)
                canvasTexture.Apply(updateMipmaps: false);
        }

        /// <summary>
        /// Centers the canvas on the camera and scales the canvas according to the camera aspect ratio.
        /// </summary>
        /// <param name="cam"></param>
        public void MatchCamera(Camera cam)
        {
            // Aspect
            var scale = RenderTarget.transform.localScale;
            scale.x = cam.orthographicSize * cam.aspect * 2f;
            scale.y = cam.orthographicSize * 2f;
            RenderTarget.transform.localScale = scale;

            // scale up if a margin is set
            {
                float scalePerPixelX = scale.x / (Width - 2);
                float scalePerPixelY = scale.y / (Height - 2);
                
                scale.x += scalePerPixelX * 2;
                scale.y += scalePerPixelY * 2;
                RenderTarget.transform.localScale = scale;
            }

            // Position
            var pos = transform.position;
            pos.x = cam.transform.position.x;
            pos.y = cam.transform.position.y;
            transform.position = pos;
        }
    }
}
