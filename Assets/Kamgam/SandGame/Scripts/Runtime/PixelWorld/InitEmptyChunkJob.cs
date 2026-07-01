using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Kamgam.SandGame.LevelPart;

namespace Kamgam.SandGame
{
    [BurstCompile]
    public struct InitEmptyChunkJob : IJob, IJobDisposable
    {
        // In
        [ReadOnly]
        public int Width;

        [ReadOnly]
        public int Height;

        [ReadOnly]
        public int XMinInPixelWorld;

        [ReadOnly]
        public int YMinInPixelWorld;

        // Out
        [WriteOnly]
        public NativeArray<Pixel> ChunkPixels;

        public void Execute()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    int index = PixelWorldChunk.CoordinatesToIndex(x, y, Width);
                    int xInWorld = XMinInPixelWorld + x;
                    int yInWorld = YMinInPixelWorld + y;

                    // Init empty pixel with value flag, position and color.
                    Pixel pixel = PixelFactory.CreateEmpty(xInWorld, yInWorld, loaded: false);
                    ChunkPixels[index] = pixel;
                }
            }
        }

        public void Dispose()
        { }
    }
}
