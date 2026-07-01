using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Kamgam.SandGame
{
    [BurstCompile]
    public struct ChunkToCanvasJob : IJob
    {
        // In
        [ReadOnly] public NativeArray<Pixel> PixelsInChunk;
        [ReadOnly] public int ChunkWidth;
        [ReadOnly] public int XMinInChunk;
        [ReadOnly] public int XMaxInChunk;
        [ReadOnly] public int YMinInChunk;
        [ReadOnly] public int YMaxInChunk;

        [ReadOnly] public int ViewportWidth;
        [ReadOnly] public int XMinInCanvas;
        [ReadOnly] public int YMinInCanvas;

        // Out
        [NativeDisableContainerSafetyRestriction] // Allow writing to the same array from multiple jobs.
        [WriteOnly]
        public NativeArray<byte> Colors;

        [BurstCompile]
        public void Execute()
        {
            byte r, g, b, a;
            int width = ViewportWidth;

            for (int y = YMinInChunk; y < YMaxInChunk; y++)
            {
                for (int x = XMinInChunk; x < XMaxInChunk; x++)
                {
                    // Get color
                    PixelWorldChunk.GetPixelColorAtChunkPos(
                        PixelsInChunk, ChunkWidth, x, y,
                        out r, out g, out b, out a
                        );

                    // Testing:
                    /*
                    r = 127;
                    g = 0;
                    b = 0;
                    a = 255;
                    //*/

                    // Set color
                    int xInCanvas = XMinInCanvas + x - XMinInChunk;
                    int yInCanvas = YMinInCanvas + y - YMinInChunk;
                    PixelCanvas.SetPixel(Colors, ViewportWidth, xInCanvas, yInCanvas, r, g, b, a);
                }
            }
        }
    }
}
