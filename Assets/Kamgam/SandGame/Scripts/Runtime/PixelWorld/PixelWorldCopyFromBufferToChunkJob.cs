using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Kamgam.SandGame
{
    /// <summary>
    /// Copies the buffer pixels to the world and clears each copied pixel in the buffer.
    /// </summary>
    [BurstCompile]
    public struct PixelWorldCopyFromBufferToChunkJob : IJob
    {
        // In
        [ReadOnly] public NativeArray<Pixel> Buffer;
        [ReadOnly] public int BufferWidth;
        [ReadOnly] public int XMinInBuffer;
        [ReadOnly] public int YMinInBuffer;

        [ReadOnly] public int ChunkWidth;
        [ReadOnly] public int XMinInChunk;
        [ReadOnly] public int XMaxInChunk;
        [ReadOnly] public int YMinInChunk;
        [ReadOnly] public int YMaxInChunk;

        // Out
        [NativeDisableContainerSafetyRestriction] // Allow writing to the same array from multiple jobs.
        [WriteOnly] public NativeArray<Pixel> PixelsInChunk;

        [BurstCompile]
        public void Execute()
        {
            int indexInSimulation;
            for (int y = YMinInChunk; y < YMaxInChunk; y++)
            {
                for (int x = XMinInChunk; x < XMaxInChunk; x++)
                {
                    // calc index in buffer
                    int xInSimulation = XMinInBuffer + x - XMinInChunk;
                    int yInSimulation = YMinInBuffer + y - YMinInChunk;
                    indexInSimulation = PixelWorldChunk.CoordinatesToIndex(xInSimulation, yInSimulation, BufferWidth);

                    // Copy into world
                    PixelWorldChunk.SetPixelAtChunkPos(PixelsInChunk, ChunkWidth, x, y, Buffer[indexInSimulation]);
                }
            }
        }
    }
}
