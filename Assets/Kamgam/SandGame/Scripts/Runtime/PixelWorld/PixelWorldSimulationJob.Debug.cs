using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Kamgam.SandGame
{
    public partial struct PixelWorldSimulationJob
    {
#if UNITY_EDITOR

        private void debugPass()
        {
            int index;
            Pixel pixel;
            for (int y = YMinInBuffer; y < YMaxInBuffer; y++)
            {
                for (int x = XMinInBuffer; x < XMaxInBuffer; x++)
                {
                    index = PixelWorldChunk.CoordinatesToIndex(x, y, BufferWidth);
                    pixel = Buffer[index];

                    // Loading
                    //if (!pixel.IsEmpty()) debugDrawIsLoading(index);

                    // Simulated
                    //if (!pixel.IsEmpty()) debugDrawSimulatedPixels(ref pixel, index);

                    // Positions
                    //debugDrawPos(ref pixel, index);

                    // Sleeping
                    //debugDrawSleepCounter(ref pixel, index);

                    // FreeFalling
                    //debugDrawIsFreeFalling(index, ref Materials);

                    // Velocity Y
                    //debugDrawVelocityY(ref pixel, index);

                    // Velocity (x/y separated)
                    //if (DebugFrameCount % 2 == 0)
                    //    debugDrawVelocityX(ref pixel, index);
                    //else
                    //    debugDrawVelocityY(ref pixel, index);

                    // Delta
                    //debugDrawDeltaX(ref pixel, index);
                    //debugDrawDeltaY(ref pixel, index);

                    //debugDrawHealth(ref pixel, index);

                    //debugColorizeByThreadNr(ref pixel, index);
                    // debugDrawThreadBorders(ref pixel, x, y, index);

                    // Temperature
                    //debugDrawTemperature(ref pixel, index, ref Materials); 
                }
            }
        }

        int debugCheckPixelPosX(int index, string text)
        {
            var pixel = Buffer[index];
            PixelWorldChunk.IndexToCoordinates(index, BufferWidth, out int x, out int y);
            if (pixel.x != XMinInWorld + x)
            {
                JobUtils.DebugLog(text, pixel.x - (XMinInWorld + x));
            }

            return y;
        }

        int debugCheckPixelPosX(ref Pixel pixel, string text)
        {
            int index = getPixelIndex(ref pixel);
            var p = Buffer[index];
            PixelWorldChunk.IndexToCoordinates(index, BufferWidth, out int x, out int y);
            if (p.x != XMinInWorld + x)
            {
                JobUtils.DebugLog(text, p.x);
                JobUtils.DebugLog(text, XMinInWorld + x);
            }

            return y;
        }

        private void debugDrawSleepCounter(ref Pixel pixel, int index)
        {
            if (!pixel.IsEmpty())
            {
                if (pixel.sleepCounter >= Pixel.SleepCounterIsSleepingValue)
                {
                    pixel.r = 0;
                    pixel.g = 255;
                    pixel.b = 255;
                    Buffer[index] = pixel;
                }
                else
                {
                    pixel.r = (byte)((int)pixel.sleepCounter * 10);
                    pixel.g = 0;
                    pixel.b = 0;
                    Buffer[index] = pixel;
                }
            }
        }

        private void debugDrawSimulatedPixels(ref Pixel pixel, int index)
        {
            if (pixel.IsEmpty() || !pixel.RequiresSimulation() || pixel.IsAsleep())
            {
                pixel.r = 255;
                pixel.g = 0;
                pixel.b = 0;
                Buffer[index] = pixel;
            }
            else
            {
                pixel.r = 0;
                pixel.g = 255;
                pixel.b = 0;
                Buffer[index] = pixel;
            }
        }

        private void debugDrawHealth(ref Pixel pixel, int index)
        {
            if (!pixel.IsEmpty())
            {
                if (pixel.IsDead())
                {
                    pixel.r = 255;
                    pixel.g = 0;
                    pixel.b = 0;
                }
                else
                {
                    pixel.r = 0;
                    pixel.g = (byte)math.round(pixel.health * 2);
                    pixel.b = 0;
                    Buffer[index] = pixel;
                }
            }
            else
            {
                pixel.r = 0;
                pixel.g = 0;
                pixel.b = 0;
                pixel.a = 0;
                Buffer[index] = pixel;
            }
        }

        private void debugDrawIsFreeFalling(int index, ref NativeArray<PixelMaterial> materials)
        {
            Pixel pixel = Buffer[index];

            if (pixel.IsEmpty())
            {
                pixel.r = 0;
                pixel.g = 0;
                pixel.b = 0;
                pixel.a = 0;
                Buffer[index] = pixel;
                return;
            }

            if (pixel.IsFreeFalling())
            {
                pixel.r = 0;
                pixel.g = 255;
                pixel.b = 0;
                Buffer[index] = pixel;
            }
            else
            {
                pixel.r = (byte)(pixel.IsAffectedByGravity(ref materials) ? 255 : 127);
                pixel.g = 0;
                pixel.b = 0;
                Buffer[index] = pixel;
            }

            if (pixel.IsAsleep())
            {
                pixel.r = (byte)(pixel.IsAffectedByGravity(ref materials) ? 255 : 127);
                pixel.g = (byte)(pixel.IsAffectedByGravity(ref materials) ? 255 : 127);
                pixel.b = 0;
                Buffer[index] = pixel;
            }
        }

        private void debugDrawIsLoading(int index)
        {
            Pixel pixel = Buffer[index];
            pixel.r = (byte) (pixel.IsLoaded() ? 0 : 255);
            pixel.g = (byte) (pixel.IsLoaded() ? 255 : 0);
            pixel.b = 0;
            pixel.a = 255;
            Buffer[index] = pixel;
        }

        private void debugColorizeByThreadNr(ref Pixel pixel, int index)
        {
            pixel.r = (byte)(ThreadNr == 0 ? 255 : 0);
            pixel.g = (byte)(ThreadNr == 1 ? 255 : 0);
            pixel.b = (byte)(ThreadNr == 2 ? 255 : 0);
            Buffer[index] = pixel;
        }

        private void debugDrawPos(ref Pixel pixel, int index)
        {
            if (!pixel.IsEmpty())
            {
                pixel.r = (byte)(pixel.x % 255);
                pixel.g = (byte)(pixel.y % 255);
                pixel.b = 0;
                Buffer[index] = pixel;
            }
        }

        private void debugDrawThreadBorders(ref Pixel pixel, int x, int y, int index)
        {
            pixel.r = (byte)(x == XMinInBuffer ? math.min(pixel.r + 5, 255) : pixel.r);
            pixel.g = 0;
            pixel.b = 0;
            Buffer[index] = pixel;
        }

        private void debugDrawVelocityX(ref Pixel pixel, int index)
        {
            if (!pixel.IsEmpty())
            {
                if (math.abs(pixel.velocityX) < 0.01f)
                    pixel.b = 0;
                else
                    pixel.r = (byte)(50 + math.min((int)(math.abs(pixel.velocityX) * 10), 200));
                pixel.g = 0;
                pixel.b = 0;
                Buffer[index] = pixel;
            }
        }

        private void debugDrawVelocityY(ref Pixel pixel, int index)
        {
            if (!pixel.IsEmpty())
            {
                pixel.r = 0;
                if (math.abs(pixel.velocityY) < 0.01f)
                    pixel.g = 0;
                else
                    pixel.g = (byte)(50 + math.min((int)(math.abs(pixel.velocityY) * 10) , 200));
                pixel.b = 0;
                Buffer[index] = pixel;
            }
        }

        private void debugDrawVelocity(ref Pixel pixel, int index)
        {
            if (!pixel.IsEmpty())
            {
                if (math.abs(pixel.velocityX) < 0.01f)
                    pixel.r = 0;
                else
                    pixel.r = (byte)(50 + math.min((int)(math.abs(pixel.velocityX) * 10), 200));

                if (math.abs(pixel.deltaY) == 0)
                    pixel.g = 0;
                else
                    pixel.g = (byte)math.min((int)(50 + 20 * math.abs(pixel.deltaY)), 255);

                pixel.b = 0;
                Buffer[index] = pixel;
            }
        }

        private void debugDrawDeltaX(ref Pixel pixel, int index)
        {
            if (!pixel.IsEmpty())
            {
                if (math.abs(pixel.deltaX) == 0)
                    pixel.r = 0;
                else
                    pixel.r = (byte)math.min((int)(50 + 20 * math.abs(pixel.deltaX)), 255);
                pixel.g = 0;
                pixel.b = 0;

                Buffer[index] = pixel;
            }
        }

        private void debugDrawDeltaY(ref Pixel pixel, int index)
        {
            if (!pixel.IsEmpty())
            {
                pixel.r = 0;
                if (math.abs(pixel.deltaY) == 0)
                    pixel.g = 0;
                else
                    pixel.g = (byte)math.min((int)(50 + 20 * math.abs(pixel.deltaY)), 255);
                pixel.b = 0;

                Buffer[index] = pixel;
            }
        }

        private void debugDrawTemperature(ref Pixel pixel, int index, ref NativeArray<PixelMaterial> materials)
        {
            if (!pixel.IsEmpty())
            {
                if (pixel.temperature < 0)
                {
                    pixel.r = 255;
                    pixel.g = (byte)(-pixel.temperature * 0.6f);
                    pixel.b = (byte)(-pixel.temperature * 0.6f);
                }
                else if(pixel.temperature < 100)
                {
                    pixel.r = 255;
                    pixel.g = 255;
                    pixel.b = (byte)(255 - (pixel.temperature * 0.39f));
                }
                else
                {
                    pixel.r = 255;
                    pixel.g = (byte)math.max(0, (255 - (pixel.temperature - 100) / 5f));
                    pixel.b = 0;
                }

                if (pixel.IsBurning(ref materials))
                {
                    pixel.r = 255;
                    pixel.g = 0;
                    pixel.b = 255;
                }

                Buffer[index] = pixel;
            }
        }
#endif
    }
}
