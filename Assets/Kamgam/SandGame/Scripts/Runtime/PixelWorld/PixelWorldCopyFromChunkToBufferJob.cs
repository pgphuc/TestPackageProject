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
    /// Copies the world chunk pixels to the buffer.
    /// </summary>
    [BurstCompile]
    public struct PixelWorldCopyFromChunkToBufferJob : IJob
    {
        // In
        // Allow accessing the same array from multiple jobs.
        // We need this because the LevelPartImageToChunkJob
        // needs to be able to write to unloaded pixels.
        // This means here we may read incompletely loaded pixels
        // (which is okay, we check the isLoaded flag in the simulation anyways).
        [NativeDisableContainerSafetyRestriction]
        [ReadOnly] public NativeArray<Pixel> PixelsInChunk;

        [ReadOnly] public int ChunkWidth;
        [ReadOnly] public int XMinInChunk;
        [ReadOnly] public int XMaxInChunk;
        [ReadOnly] public int YMinInChunk;
        [ReadOnly] public int YMaxInChunk;

        [ReadOnly] public int BufferWidth;
        [ReadOnly] public int XMinInBuffer;
        [ReadOnly] public int YMinInBuffer;

        [ReadOnly] public int XMinInWorld;
        [ReadOnly] public int YMinInWorld;


        // Out
        [NativeDisableContainerSafetyRestriction] // Allow writing to the same array from multiple jobs.
        [WriteOnly] public NativeArray<Pixel> Buffer;
        
        [BurstCompile]
        public void Execute()
        {
            int indexInSimulation;
            Pixel pixelInChunk;
            for (int y = YMinInChunk; y < YMaxInChunk; y++)
            {
                for (int x = XMinInChunk; x < XMaxInChunk; x++)
                {
                    // Get from chunk
                    pixelInChunk = PixelWorldChunk.GetPixelAtChunkPos(PixelsInChunk, ChunkWidth, x, y);
                    if (pixelInChunk.IsLoaded())
                        pixelInChunk.ScheduleSimulation();

                    // Copy into buffer
                    int xInBuffer = XMinInBuffer + (x - XMinInChunk);
                    int yInBuffer = YMinInBuffer + (y - YMinInChunk);
                    indexInSimulation = PixelWorldChunk.CoordinatesToIndex(xInBuffer, yInBuffer, BufferWidth);

                    // Debug
                    //if (pixelInChunk.x != XMinInWorld + (x - XMinInChunk) || pixelInChunk.y != YMinInWorld + (y - YMinInChunk))
                    //    JobUtils.DebugLog("Diverging in job ", pixelInChunk.x - (XMinInWorld + xInBuffer), pixelInChunk.y -(YMinInWorld + yInBuffer));

                    Buffer[indexInSimulation] = pixelInChunk;
                }
            }
        }
    }
}
