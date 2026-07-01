using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kamgam.SandGame
{
    /// <summary>
    /// Basically a list of pixels that represent a rectangular region in the pixel world.
    /// </summary>
    public class PixelWorldChunk
    {
        /// <summary>
        /// Image (0/0 = bottom/left)
        /// The array contains the pixels row by row, starting at the bottom left of the texture.
        /// The size of the array is the width × height of the mipmap level.
        /// 
        /// Pixel array layout:
        /// ------>---end
        /// ----->-------
        /// start---->---
        /// </summary>
        public NativeArray<Pixel> Pixels;

        public bool IsLoading { get; private set; }
        public bool LoadingFinished = false;
        public bool LoadSucceeded = false;
        public bool LoadFailed = false;

        protected bool _pixelsHaveValue;

        /// <summary>
        /// Logical x position (this is NOT in pixels, use XMin if you need pixels).
        /// </summary>
        public int X { get; private set; }
        /// <summary>
        /// Logical y position (this is NOT in pixels, use YMin if you need pixels).
        /// </summary>
        public int Y { get; private set; }

        /// <summary>
        /// X min position in pixels in the world.
        /// </summary>
        public int XMin => X * Width;
        /// <summary>
        /// y min position in pixels in the world.
        /// </summary>
        public int YMin => Y * Height;
        public int XMax => (X + 1) * Width;
        public int YMax => (Y + 1) * Height;

        /// <summary>
        /// Width in pixels.
        /// </summary>
        public int Width { get; protected set; }
        /// <summary>
        /// Height in pixels.
        /// </summary>
        public int Height { get; protected set; }

        public void Initialize(int chunkX, int chunkY, int width, int height)
        {
            if (_pixelsHaveValue)
            {
                Pixels.Dispose();
                _pixelsHaveValue = false;
            }

            this.X = chunkX;
            this.Y = chunkY;
            this.Width = width;
            this.Height = height;

            Pixels = new NativeArray<Pixel>(new Pixel[width * height], Allocator.Persistent);
            ClearPixels();
            _pixelsHaveValue = true;
        }

        public void Clear()
        {
            Clear(new Color32(0, 0, 0, 0));
        }

        /// <summary>
        /// Sets all pixels to the clear color.<br />
        /// Beware: this is not multithreaded and thus blocks the main thread!
        /// </summary>
        /// <param name="clearColor"></param>
        public void Clear(Color32 clearColor)
        {
            int count = Pixels.Length;
            for (int i = 0; i < count; i++)
            {
                var p = Pixels[i];
                p.r = clearColor.r;
                p.g = clearColor.g;
                p.b = clearColor.b;
                p.a = clearColor.a;
                Pixels[i] = p;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixelAtChunkPos(int xInChunk, int yInChunk, ref Pixel pixel)
        {
            Pixels[CoordinatesToIndex(xInChunk, yInChunk, Width)] = pixel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(ref Pixel pixel)
        {
            WorldToChunkPos(pixel.x, pixel.y, out int xInChunk, out int yInChunk);
            Pixels[CoordinatesToIndex(xInChunk, yInChunk, Width)] = pixel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetPixel(ref Pixel pixel)
        {
            if (Contains(pixel.x, pixel.y))
            {
                SetPixel(ref pixel);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int xInPixelWorld, int yInPixelWorld)
        {
            return (xInPixelWorld >= XMin && yInPixelWorld >= YMin && xInPixelWorld < XMax && yInPixelWorld < YMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WorldToChunkPos(int xInPixelWorld, int yInPixelWorld, out int xInChunk, out int yInChunk)
        {
            xInChunk = xInPixelWorld - XMin;
            yInChunk = yInPixelWorld - YMin;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ChunkToWorldPos(int xInChunk, int yInChunk, out int xInPixelWorld, out int yInPixelWorld)
        {
            xInPixelWorld = XMin + xInChunk;
            yInPixelWorld = YMin + yInChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPixel(int xInWorld, int yInWorld, out Pixel pixel)
        {
            if (Contains(xInWorld, yInWorld))
            {
                WorldToChunkPos(xInWorld, yInWorld, out int xInChunk, out int yInChunk);
                pixel = GetPixelAtChunkPos(xInChunk, yInChunk);
                return true;
            }

            pixel = new();
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Pixel GetPixel(int xInWorld, int yInWorld)
        {
            WorldToChunkPos(xInWorld, yInWorld, out int xInChunk, out int yInChunk);
            return GetPixelAtChunkPos(xInChunk, yInChunk);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Pixel GetPixelAtChunkPos(int xInChunk, int yInChunk)
        {
            return Pixels[CoordinatesToIndex(xInChunk, yInChunk, Width)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Pixel GetPixelAtChunkIndex(int indexInChunk)
        {
            return Pixels[indexInChunk];
        }

        public void Dispose()
        {
            Pixels.Dispose();
            _pixelsHaveValue = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CoordinatesToIndex(int x, int y, int width)
        {
            return y * width + x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndexToCoordinates(int index, int width, out int x, out int y)
        {
            y = Mathf.FloorToInt(index / width);
            x = index - (y * width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pixel GetPixelAtChunkPos(
            NativeArray<Pixel> pixels, int width, int xInChunk, int yInChunk)
        {
            int index = CoordinatesToIndex(xInChunk, yInChunk, width);
            return pixels[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetPixelAtChunkPos(
            NativeArray<Pixel> pixels, int width, int xInChunk, int yInChunk, Pixel pixel)
        {
            int index = CoordinatesToIndex(xInChunk, yInChunk, width);
            pixels[index]  = pixel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetPixelColorAtChunkPos(
            NativeArray<Pixel> pixels, int width, int xInChunk, int yInChunk,
            out byte r, out byte g, out byte b, out byte a)
        {
            int index = CoordinatesToIndex(xInChunk, yInChunk, width);
            var pixel = pixels[index];
            r = pixel.r;
            g = pixel.g;
            b = pixel.b;
            a = pixel.a;
        }

        public void ClearPixels()
        {
            var job = new InitEmptyChunkJob()
            {
                // In
                Width = Width,
                Height = Height,
                XMinInPixelWorld = XMin,
                YMinInPixelWorld = YMin,
                // Out
                ChunkPixels = Pixels
            };
            var handle = job.Schedule();
            handle.Complete();
        }

        public void LoadFromImage(int level, int chunkX, int chunkY)
        {
            // Load from LevelChunk.
            IsLoading = true;
            LoadingFinished = false;
            LoadSucceeded = false;
            LoadFailed = false;
            LevelInfo.LoadImageIntoChunk(level, chunkX, chunkY, this, onComplete);
        }

        protected void onComplete(bool success)
        {
            finishLoading(success);
        }

        private void finishLoading(bool success)
        {
            IsLoading = false;
            LoadingFinished = true;
            LoadSucceeded = success;
            LoadFailed = !success;
        }

        public void Save()
        { }
    }
}
