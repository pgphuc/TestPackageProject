using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Kamgam.SandGame.LevelPart;

namespace Kamgam.SandGame
{
    [BurstCompile]
    public struct LevelPartImageToChunkJob : IJob, IJobDisposable
    {
        // In
        [ReadOnly] public uint RandomSeed;

        [ReadOnly]
        public NativeArray<PixelMaterial> Materials;

        [ReadOnly]
        public int TextureWidth;

        /// <summary>
        /// Pixel colros have to be in RGBA32 format.<br />
        /// The array contains the pixels row by row, starting at the bottom left of the texture.<br />
        /// The size of the array is the width × height of the mipmap level.
        /// </summary>
        [ReadOnly]
        public NativeArray<byte> TexturePixelsColors;

        [ReadOnly]
        public int XMinInPixelWorld;

        [ReadOnly]
        public int YMinInPixelWorld;

        // Out
        // Allow accessing the same array from multiple jobs.
        // We need this because the PixelWorldCopyFromChunkToBufferJob
        // needs to be able to access unloaded pixels from the Pixels array
        // of the chunk. This means it may read incompletely loaded pixels
        // (which is okay, we check the isLoaded flag in the simulation anyways).
        [NativeDisableContainerSafetyRestriction]
        [WriteOnly] public NativeArray<Pixel> ChunkPixels;

        public void Execute()
        {
            Unity.Mathematics.Random rnd = Unity.Mathematics.Random.CreateFromIndex(RandomSeed);
            int numOfColors = TexturePixelsColors.Length / 4;
            int materialsLength = Materials.Length;
            for (int i = 0; i < numOfColors; i++)
            {
                // Calc coordinates inside chunk
                int xInTexture = i % TextureWidth;
                int yInTexture = i / TextureWidth;

                byte imageR = TexturePixelsColors[i * 4];
                byte imageG = TexturePixelsColors[i * 4 + 1];
                byte imageB = TexturePixelsColors[i * 4 + 2];
                byte imageA = TexturePixelsColors[i * 4 + 3];
                imageA = (byte)(imageA < 127 ? 0 : 255); // clamp alpha to 0 or 255

                // Find the material matching the color in the image texture.
                int materialIndex = -1;
                PixelMaterial material;
                for (int m = 0; m < materialsLength; m++)
                {
                    material = Materials[m];

                    // Does the pixel color match any pixel type?
                    if (   imageR == material.colorInImage.r
                        && imageG == material.colorInImage.g
                        && imageB == material.colorInImage.b
                        && imageA == 255)
                    {
                        materialIndex = m;
                        break;
                    }
                }

                Pixel pixel = PixelFactory.CreatePixel(
                    x: XMinInPixelWorld + xInTexture,
                    y: YMinInPixelWorld + yInTexture,
                    imageR,
                    imageG,
                    imageB,
                    imageA,
                    isEmpty: materialIndex <= 0,
                    isLoaded: true,
                    ref Materials,
                    materialIndex,
                    ref rnd);

                ChunkPixels[i] = pixel;
            }
        }

        public void Dispose()
        { }
    }
}
