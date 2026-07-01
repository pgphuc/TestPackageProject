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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float clampToMagnitude(float value, float magnitude)
        {
            if (value < 0f) return math.clamp(value, -magnitude, 0f);
            return math.clamp(value, 0f, magnitude);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool inBounds(int xInBufferTarget, int yInBufferTarget)
        {
            return xInBufferTarget >= 0 && xInBufferTarget < BufferWidth && yInBufferTarget > 0 && yInBufferTarget < BufferHeight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float pixelsPerFrameToUnitsPerSecond(int pixelsPerFrame)
        {
            return pixelsPerFrame / (PixelsPerUnit * FixedDeltaTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float pixelsPerFrameToUnitsPerSecond(float pixelsPerFrame)
        {
            return pixelsPerFrame / (PixelsPerUnit * FixedDeltaTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float unitsPerSecondToPixelsPerFrame(float unitsPerSecond)
        {
            return unitsPerSecond * PixelsPerUnit * FixedDeltaTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getPixelIndex(int xInBuffer, int yInBuffer)
        {
            return PixelWorldChunk.CoordinatesToIndex(xInBuffer, yInBuffer, BufferWidth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getPixelIndex(ref Pixel pixel)
        {
            getPixelPositionInBuffer(ref pixel, out int xInBuffer, out int yInBuffer);
            return PixelWorldChunk.CoordinatesToIndex(xInBuffer, yInBuffer, BufferWidth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Pixel getPixel(int xInBuffer, int yInBuffer)
        {
            int index = PixelWorldChunk.CoordinatesToIndex(xInBuffer, yInBuffer, BufferWidth);
            Pixel pixel = Buffer[index];
            return pixel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getPixelPositionInBufferX(ref Pixel pixel)
        {
            return pixel.x - XMinInWorld;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getPixelPositionInBufferY(ref Pixel pixel)
        {
            return pixel.y - YMinInWorld;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void getPixelPositionInBuffer(ref Pixel pixel, out int x, out int y)
        {
            x = pixel.x - XMinInWorld;
            y = pixel.y - YMinInWorld;
        }


        /// <summary>
        /// A more efficient version of setPixel if x and y are already known.
        /// </summary>
        /// <param name="pixel"></param>
        /// <param name="xInBuffer"></param>
        /// <param name="yInBuffer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setPixel(ref Pixel pixel, int xInBuffer, int yInBuffer)
        {
            int index = PixelWorldChunk.CoordinatesToIndex(xInBuffer, yInBuffer, BufferWidth);
            Buffer[index] = pixel;
        }

        /// <summary>
        /// Whenever possible use setPixel(ref Pixel pixel, int xInBuffer, int yInBuffer) instead since that is more efficient.
        /// </summary>
        /// <param name="pixel"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setPixel(ref Pixel pixel)
        {
            if(!inBounds(getPixelPositionInBufferX(ref pixel), getPixelPositionInBufferY(ref pixel)))
            {
#if UNITY_EDITOR
                JobUtils.DebugLog("setPixel out of bounds X/Y index", getPixelPositionInBufferX(ref pixel), getPixelPositionInBufferY(ref pixel), getPixelIndex(ref pixel));
#endif
                return;
            }

            Buffer[getPixelIndex(ref pixel)] = pixel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setPixelColor(int xInBuffer, int yInBuffer, byte r, byte g, byte b, byte a)
        {
            int index = PixelWorldChunk.CoordinatesToIndex(xInBuffer, yInBuffer, BufferWidth);
            setPixelColor(index, r, g, b, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setPixelColor(int index, byte r, byte g, byte b, byte a)
        {
            Pixel pixel = Buffer[index];
            pixel.r = r;
            pixel.g = g;
            pixel.b = b;
            pixel.a = a;
            Buffer[index] = pixel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setPixelColor(ref NativeArray<Pixel> Buffer, ref Pixel pixel, int x, int y, byte r, byte g, byte b, byte a)
        {
            int index = PixelWorldChunk.CoordinatesToIndex(x, y, BufferWidth);
            pixel.r = r;
            pixel.g = g;
            pixel.b = b;
            pixel.a = a;
            Buffer[index] = pixel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void wakeUpNeighbours(int xInBuffer, int yInBuffer)
        {
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    wakeUp(xInBuffer + x, yInBuffer + y);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void wakeUp(int xInBuffer, int yInBuffer)
        {
            int index = PixelWorldChunk.CoordinatesToIndex(xInBuffer, yInBuffer, BufferWidth);
            var pixel = Buffer[index];
            pixel.sleepCounter = Pixel.SleepCounterResetValue;
            Buffer[index] = pixel;
        }

        /// <summary>
        /// 0 degrees = facing right (X = 1 and Y = 0 on a unit circle).
        /// Angle increases in CCW order.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private float getAngle360(float x, float y)
        {
            if (y == 0)
            {
                return x < 0 ? 180f : 0f;
            }
            else if (x == 0)
            {
                return y < 0 ? 270f : 90f;
            }
            else
            {
                float angle = math.atan(y / x) * Rad2Deg;
                // Since atan returns values between -PI/2 and +PI/2
                // we have to correct for quadrants.
                if (x > 0 && y > 0) return angle;
                else if (x < 0) return angle + 180f; // angle is between -90 and +90
                else return angle + 360f; // angle is between 0 and -90
            }
        }
    }
}
