using System;
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
    /// <summary>
    /// Fills the Codes and Paths arrays with collider path and pixel code infos.<br />
    /// A path is a list of indices ended with -1.
    /// </summary>
    [BurstCompile]
    public struct PixelWorldColliderJob : IJob
    {
        // In
        [ReadOnly]
        public NativeArray<Pixel> Buffer;
        [ReadOnly]
        public NativeArray<PixelMaterial> Materials;
        public int BufferWidth;
        public int BufferHeight;

        // Out
        public NativeArray<int> Codes;
        public NativeArray<int> Paths; // A path is a list of indices ended with -1
        public NativeArray<int> HandledTmp;

        [BurstCompile]
        public void Execute()
        {
            // 5 4 3
            // 6   2
            // 7 0 1
            NativeArray<int> clockwiseOffsets = new NativeArray<int>(16, Allocator.Temp);
            // Bottom
            clockwiseOffsets[0] = 0; // x
            clockwiseOffsets[1] = -1; // y
            // Bottom right
            clockwiseOffsets[2] = 1; // x
            clockwiseOffsets[3] = -1; // y
            // Right
            clockwiseOffsets[4] = 1;
            clockwiseOffsets[5] = 0;
            // ...
            clockwiseOffsets[6] = 1;
            clockwiseOffsets[7] = 1;

            clockwiseOffsets[8] = 0;
            clockwiseOffsets[9] = 1;

            clockwiseOffsets[10] = -1;
            clockwiseOffsets[11] = 1;

            clockwiseOffsets[12] = -1;
            clockwiseOffsets[13] = 0;

            clockwiseOffsets[14] = -1;
            clockwiseOffsets[15] = -1;

            int count = Buffer.Length;
            int i;
            for (i = 0; i < count; i++)
            {
                PixelWorldChunk.IndexToCoordinates(i, BufferWidth, out int x, out int y);

                int index = PixelWorldChunk.CoordinatesToIndex(x, y, BufferWidth);
                int indexTop = PixelWorldChunk.CoordinatesToIndex(x, y + 1, BufferWidth);
                int indexRight = PixelWorldChunk.CoordinatesToIndex(x + 1, y, BufferWidth);
                int indexBottom = PixelWorldChunk.CoordinatesToIndex(x, y - 1, BufferWidth);
                int indexLeft = PixelWorldChunk.CoordinatesToIndex(x - 1, y, BufferWidth);

                // Determine the code (0-16 incl.) for each pixel based on the neighbours.
                // Neighbours at the very edge are forced to empty. This makes handling edges
                // easier (unnecessary) in the code that deals with each pixel.

                int code = 0;

                if (Buffer[index].HasBehaviour(PixelBehaviour.Solid, ref Materials) 
                    && x >= 1 && x < BufferWidth - 1
                    && y >= 1 && y < BufferHeight - 1)
                {

                    if (indexTop >= 0 && indexTop < count 
                        && x >= 1 && x < BufferWidth - 1
                        && y + 1 >= 1 && y + 1 < BufferHeight - 1
                        && Buffer[indexTop].HasBehaviour(PixelBehaviour.Solid, ref Materials))
                        code += 8;

                    if (indexRight >= 0 && indexRight < count
                        && x + 1 >= 1 && x + 1 < BufferWidth - 1
                        && y >= 1 && y < BufferHeight - 1
                        && Buffer[indexRight].HasBehaviour(PixelBehaviour.Solid, ref Materials))
                        code += 4;

                    if (indexBottom >= 0 && indexBottom < count
                        && x >= 1 && x < BufferWidth - 1
                        && y - 1 >= 1 && y - 1 < BufferHeight - 1
                        && Buffer[indexBottom].HasBehaviour(PixelBehaviour.Solid, ref Materials))
                        code += 2;

                    if (indexLeft >= 0 && indexLeft < count
                        && x - 1 >= 1 && x - 1 < BufferWidth - 1
                        && y >= 1 && y < BufferHeight - 1
                        && Buffer[indexLeft].HasBehaviour(PixelBehaviour.Solid, ref Materials))
                        code += 1;

                    // A filled pixel without any neighbours is code 16.
                    if (code == 0)
                    {
                        code = 16;
                    }
                }

                Codes[i] = code;

                // We also use the loop to clear out our other arrays.
                Paths[i] = -1;
                HandledTmp[i] = 0;
            }

            int pathIndex = 0;
            for (i = 0; i < count; i++)
            {
                int code = Codes[i];

                // Skip pixels that have already been handled
                if (isHandled(ref HandledTmp, code, i))
                {
                    continue;
                }

                // Skip empty or fully surrounded pixels
                if (code == 0 || code == 15)
                {
                    HandledTmp[i] = 1;
                    continue;
                }

                // Fast track for single pixels 
                if (code == 16)
                {
                    HandledTmp[i] = 1;
                    Paths[pathIndex] = i;
                    pathIndex++;

                    Paths[pathIndex] = -1;
                    pathIndex++;

                    continue;
                }

                // JobUtils.DebugLog("-- New path, code: ", code);

                HandledTmp[i] += 1;
                Paths[pathIndex] = i;
                pathIndex++;

                bool pathEnded;
                int startIndex = i;
                int nextIndex = i;
                int nextCode = code;
                int nextStartOffsetX = -1;
                int nextStartOffsetY = -1;
                do
                {
                    pathEnded = true;

                    int handledValue = 99;
                    PixelWorldChunk.IndexToCoordinates(nextIndex, BufferWidth, out int x, out int y);
                    // Only some directions are valid path continuations (depending on the code of the current pixel).
                    // Check the manual "Code" section for more details and some graphics explaining this.
                    // Notice: The ORDER of the neighbour checks is important (counter clock wise, starting at the
                    // last position [initially bottom left]).
                    // 5 4 3
                    // 6   2
                    // 7 0 1
                    int currentCode = nextCode;
                    switch (nextCode)
                    {
                        case 1:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                5, 6, 7, -1, -1, -1
                                );
                            break;

                        case 2:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                7, 0, 1, -1, -1, -1
                                );
                            break;

                        case 3:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                0, 1, 5, 6, -1, -1
                                );
                            break;

                        case 4:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                1, 2, 3, -1, -1, -1
                                );
                            break;

                        case 5:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                1, 2, 3, 5, 6, 7
                                );
                            break;

                        case 6:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                0, 2, 3, 7, -1, -1
                                );
                            break;

                        case 7:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                2, 3, 5, 6, -1, -1
                                );
                            break;

                        case 8:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                3, 4, 5, -1, -1, -1
                                );
                            break;

                        case 9:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                3, 4, 6, 7, -1, -1
                                );
                            break;

                        case 10:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                0, 1, 3, 4, 5, 7
                                );
                            break;

                        case 11:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                0, 1, 3, 4, -1, -1
                                );
                            break;

                        case 12:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                1, 2, 4, 5, -1, -1
                                );
                            break;

                        case 13:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                1, 2, 6, 7, -1, -1
                                );
                            break;

                        case 14:
                            checkNeighboursClockwiseStartingAt(
                                count, startIndex, ref pathEnded, ref nextIndex, ref nextCode, ref handledValue, x, y,
                                ref nextStartOffsetX, ref nextStartOffsetY, ref clockwiseOffsets,
                                0, 4, 5, 7, -1, -1
                                );
                            break;

                        case 0:
                        case 15:
                        case 16:
                        default:
                            break;
                    }

                    // If the current pixel is one that can be visited twice then
                    // we have to do some checks if we are at the end already.
                    if (currentCode == 10 || currentCode == 5)
                    {
                        // Special case if neighbour is the start index then we have looped back to start.
                        int index0 = PixelWorldChunk.CoordinatesToIndex(x - 1, y + 1, BufferWidth);
                        int index1 = (currentCode == 10) ? 
                            PixelWorldChunk.CoordinatesToIndex(x, y + 1, BufferWidth) :
                            PixelWorldChunk.CoordinatesToIndex(x + 1, y, BufferWidth);
                        int index2 = PixelWorldChunk.CoordinatesToIndex(x + 1, y + 1, BufferWidth);
                        int index3 = PixelWorldChunk.CoordinatesToIndex(x - 1, y - 1, BufferWidth);
                        int index4 = (currentCode == 10) ?
                            PixelWorldChunk.CoordinatesToIndex(x, y - 1, BufferWidth) :
                            PixelWorldChunk.CoordinatesToIndex(x - 1, y, BufferWidth);
                        int index5 = PixelWorldChunk.CoordinatesToIndex(x + 1, y - 1, BufferWidth);

                        // Check if start has a pixel surrounding it that has not yet been visited.
                        if (
                               (startIndex == index0 && HandledTmp[index0] <= HandledTmp[nextIndex])
                            || (startIndex == index1 && HandledTmp[index1] <= HandledTmp[nextIndex])
                            || (startIndex == index2 && HandledTmp[index2] <= HandledTmp[nextIndex])
                            || (startIndex == index3 && HandledTmp[index3] <= HandledTmp[nextIndex])
                            || (startIndex == index4 && HandledTmp[index4] <= HandledTmp[nextIndex])
                            || (startIndex == index5 && HandledTmp[index5] <= HandledTmp[nextIndex])
                            )
                        {
                            pathEnded = true;
                        }
                    }

                    if (!pathEnded)
                    {
                        Paths[pathIndex] = nextIndex;
                        pathIndex++;

                        HandledTmp[nextIndex] += 1;
                    }
                    else
                    {
                        Paths[pathIndex] = -1;
                        pathIndex++;
                        // JobUtils.DebugLog("-- Path ended.");
                    }
                }
                while (!pathEnded);
            }

            clockwiseOffsets.Dispose();

            // Reset handled tmp (we use it outside for path checking).
            for (i = 0; i < count; i++)
            {
                HandledTmp[i] = 0;
            }
        }

        private void checkNeighboursClockwiseStartingAt(
            int count, int startIndex, ref bool pathEnded, ref int nextIndex, ref int nextCode, ref int handledValue,
            int x, int y,
            ref int nextStartOffsetX, ref int nextStartOffsetY, ref NativeArray<int> clockwiseOffsets,
            int allowedIndex0, int allowedIndex1, int allowedIndex2, int allowedIndex3, int allowedIndex4, int allowedIndex5)
        {
            int centerCode = nextCode;

            // Count checked neighbours. If 8 is reached then we have completed one turn.
            int visitedPositions = 0;
            bool start = false;
            for (int i = 0; i < 16; i++) // 16 to make sure we check 2 full loops.
            {
                int circleIndex = (i % 8);
                int dx = clockwiseOffsets[circleIndex * 2];
                int dy = clockwiseOffsets[circleIndex * 2 + 1];

                if (start)
                {
                    if (visitedPositions < 8)
                    {
                        visitedPositions++;
                        if (   circleIndex == allowedIndex0
                            || circleIndex == allowedIndex1
                            || circleIndex == allowedIndex2
                            || circleIndex == allowedIndex3
                            || circleIndex == allowedIndex4
                            || circleIndex == allowedIndex5
                            )
                        {
                            checkNeighbourAndUpdateNext(
                                count, centerCode, ref pathEnded, ref nextIndex, ref nextCode,
                                ref nextStartOffsetX, ref nextStartOffsetY,
                                ref handledValue, x, y, dx, dy);
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                // Start checking pixels (the for loop goes around twice to ensure we visit each once).
                if (dx == nextStartOffsetX && dy == nextStartOffsetY)
                {
                    start = true;
                }
            }
        }

        private void checkNeighbourAndUpdateNext(
            int count, int centerCode, ref bool pathEnded, ref int nextIndex, ref int nextCode,
            ref int nextStartOffsetX, ref int nextStartOffsetY,
            ref int handledValue, int x, int y, int deltaX, int deltaY)
        {
            int index = PixelWorldChunk.CoordinatesToIndex(x + deltaX, y + deltaY, BufferWidth);

            // Skip if out of bounds
            if (index < 0 || index >= Codes.Length)
            {
                return;
            }

            int pathCode = Codes[index];

            if (pathCode != 0 && pathCode != 15        // ignore empty or fully surrounded pixels
                && index >= 0 && index < count         // out of bounds pixels are considered empty
                && HandledTmp[index] < handledValue    // Ensure we don't got back to already visited positions.
                                                       // The < comparison ensures we use the least often handled
                                                       // (important for pixels that can be visited multiple times).
                && !isHandled(ref HandledTmp, pathCode, index)
                && shareOneEndPoint(centerCode, pathCode, deltaX, deltaY) // Check if the current center pixel and this neighbour pixel
                                                                          // share at least one start/end point.
                                                                          // If not then ignore the neighbour pixel.
                )
            {
                handledValue = HandledTmp[index];

                // Debugging
                //if (deltaX == 0 && deltaY == -1)
                //    JobUtils.DebugLog("bottom");
                //if (deltaX == 1 && deltaY == -1)
                //    JobUtils.DebugLog("bottom-right");
                //if (deltaX == 1 && deltaY == 0)
                //    JobUtils.DebugLog("right");
                //if (deltaX == 1 && deltaY == 1)
                //    JobUtils.DebugLog("top-right ");
                //if (deltaX == 0 && deltaY == 1)
                //    JobUtils.DebugLog("top");
                //if (deltaX == -1 && deltaY == 1)
                //    JobUtils.DebugLog("top-left", centerCode, pathCode);
                //if (deltaX == -1 && deltaY == 0)
                //    JobUtils.DebugLog("left");
                //if (deltaX == -1 && deltaY == -1)
                //    JobUtils.DebugLog("bottom-left");

                pathEnded = false;
                nextIndex = index;
                nextCode = Codes[nextIndex];

                // Set the next start offset to the current center pixel position (inverse delta).
                nextStartOffsetX = -deltaX;
                nextStartOffsetY = -deltaY;
            }
        }

        private bool shareOneEndPoint(int centerCode, int pathCode, int deltaX, int deltaY)
        {
            // These have no end points, thus always false.
            if (pathCode == 0 || pathCode == 15 || pathCode == 16 || centerCode == 0 || centerCode == 15 || centerCode == 16)
                return false;

            // These have end points at all 4 positions, thus always true.
            if ((pathCode == 5 || pathCode == 10) && (centerCode == 5 || centerCode == 10))
                return true;

            if (centerCode == 5 || centerCode == 10)
            {
                getFourEndPoints(centerCode, 
                    out int centerX0, out int centerY0,
                    out int centerX1, out int centerY1,
                    out int centerX2, out int centerY2,
                    out int centerX3, out int centerY3
                    );

                getTwoEndPoints(pathCode, out int pathX0, out int pathY0, out int pathX1, out int pathY1);

                pathX0 += deltaX;
                pathY0 += deltaY;
                pathX1 += deltaX;
                pathY1 += deltaY;

                if (   (pathX0 == centerX0 && pathY0 == centerY0)
                    || (pathX0 == centerX1 && pathY0 == centerY1)
                    || (pathX0 == centerX2 && pathY0 == centerY2)
                    || (pathX0 == centerX3 && pathY0 == centerY3)
                    || (pathX1 == centerX0 && pathY1 == centerY0)
                    || (pathX1 == centerX1 && pathY1 == centerY1)
                    || (pathX1 == centerX2 && pathY1 == centerY2)
                    || (pathX1 == centerX3 && pathY1 == centerY3)
                    )
                {
                    return true;
                }
            }
            else if (pathCode == 5 || pathCode == 10)
            {
                getFourEndPoints(pathCode,
                    out int pathX0, out int pathY0,
                    out int pathX1, out int pathY1,
                    out int pathX2, out int pathY2,
                    out int pathX3, out int pathY3
                    );

                getTwoEndPoints(centerCode, out int centerX0, out int centerY0, out int centerX1, out int centerY1);

                centerX0 -= deltaX;
                centerY0 -= deltaY;
                centerX1 -= deltaX;
                centerY1 -= deltaY;

                if (   (centerX0 == pathX0 && centerY0 == pathY0)
                    || (centerX0 == pathX1 && centerY0 == pathY1)
                    || (centerX0 == pathX2 && centerY0 == pathY2)
                    || (centerX0 == pathX3 && centerY0 == pathY3)
                    || (centerX1 == pathX0 && centerY1 == pathY0)
                    || (centerX1 == pathX1 && centerY1 == pathY1)
                    || (centerX1 == pathX2 && centerY1 == pathY2)
                    || (centerX1 == pathX3 && centerY1 == pathY3)
                    )
                {
                    return true;
                }
            }
            else
            {
                getTwoEndPoints(centerCode, out int centerX0, out int centerY0, out int centerX1, out int centerY1);
                getTwoEndPoints(pathCode, out int pathX0, out int pathY0, out int pathX1, out int pathY1);

                pathX0 += deltaX;
                pathY0 += deltaY;
                pathX1 += deltaX;
                pathY1 += deltaY;

                if (   (pathX0 == centerX0 && pathY0 == centerY0)
                    || (pathX0 == centerX1 && pathY0 == centerY1)
                    || (pathX1 == centerX0 && pathY1 == centerY0)
                    || (pathX1 == centerX1 && pathY1 == centerY1))
                {
                    return true;
                }
            }

            return false;
        }

        private static void getTwoEndPoints(int code, out int x0, out int y0, out int x1, out int y1)
        {
            switch (code)
            {
                case 1:
                    x0 = 0;
                    y0 = 0;
                    x1 = 0;
                    y1 = 1;
                    break;

                case 2:
                    x0 = 0;
                    y0 = 0;
                    x1 = 1;
                    y1 = 0;
                    break;

                case 3:
                    x0 = 1;
                    y0 = 0;
                    x1 = 0;
                    y1 = 1;
                    break;

                case 4:
                    x0 = 1;
                    y0 = 0;
                    x1 = 1;
                    y1 = 1;
                    break;

                case 6:
                    x0 = 0;
                    y0 = 0;
                    x1 = 1;
                    y1 = 1;
                    break;

                case 7:
                    x0 = 1;
                    y0 = 1;
                    x1 = 0;
                    y1 = 1;
                    break;

                case 8:
                    x0 = 1;
                    y0 = 1;
                    x1 = 0;
                    y1 = 1;
                    break;

                case 9:
                    x0 = 0;
                    y0 = 0;
                    x1 = 1;
                    y1 = 1;
                    break;

                case 11:
                    x0 = 1;
                    y0 = 0;
                    x1 = 1;
                    y1 = 1;
                    break;

                case 12:
                    x0 = 1;
                    y0 = 0;
                    x1 = 0;
                    y1 = 1;
                    break;

                case 13:
                    x0 = 0;
                    y0 = 0;
                    x1 = 1;
                    y1 = 0;
                    break;

                case 14:
                    x0 = 0;
                    y0 = 0;
                    x1 = 0;
                    y1 = 1;
                    break;

                default:
                    x0 = -1;
                    y0 = -1;
                    x1 = -1;
                    y1 = -1;
                    break;
            }
        }

        private static void getFourEndPoints(int code, out int x0, out int y0, out int x1, out int y1, out int x2, out int y2, out int x3, out int y3)
        {
            switch (code)
            {
                case 5:
                case 10:
                    x0 = 0;
                    y0 = 0;
                    x1 = 0;
                    y1 = 1;
                    x2 = 1;
                    y2 = 1;
                    x3 = 1;
                    y3 = 0;
                    break;

                default:
                    x0 = -1;
                    y0 = -1;
                    x1 = -1;
                    y1 = -1;
                    x2 = -1;
                    y2 = -1;
                    x3 = -1;
                    y3 = -1;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool isHandled(ref NativeArray<int> handledTable, int code, int index)
        {
            // 5 and 10 are special. They need to be visited twice (not per path though)
            if (code == 5 || code == 10)
            {
                if (handledTable[index] == 2)
                {
                    return true;
                }
            }
            else if (handledTable[index] == 1)
            {
                return true;
            }

            return false;
        }
    }
}
