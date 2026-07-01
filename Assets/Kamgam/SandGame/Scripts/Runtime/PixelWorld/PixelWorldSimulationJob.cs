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
    /// This is where all the pixel simulation is done.<br />
    /// In the margin areas we only copy the existing pixels. No simulation should be done there.
    /// </summary>
    [BurstCompile]
    public partial struct PixelWorldSimulationJob : IJob
    {
        // Debug
        [ReadOnly] public int DebugLogEnabled;
        [ReadOnly] public int DebugFrameCount;

        // In
        [ReadOnly] public uint RandomSeed;
        [ReadOnly] public NativeArray<PixelMaterial> Materials;

        [ReadOnly] public float FixedDeltaTime;
        [ReadOnly] public int ThreadNr;
        [ReadOnly] public int PixelsPerUnit;
        [ReadOnly] public int HorizontalOrder;
        /// <summary>
        /// // Limits of rect in buffer on which this thread operates on.
        /// </summary>
        [ReadOnly] public int XMinInBuffer;
        [ReadOnly] public int XMaxInBuffer;
        [ReadOnly] public int YMinInBuffer;
        [ReadOnly] public int YMaxInBuffer;
        [ReadOnly] public int BufferWidth;
        [ReadOnly] public int BufferHeight;
        /// <summary>
        /// The simulation buffers starting offet in relation to the world.
        /// </summary>
        [ReadOnly] public int XMinInWorld;
        [ReadOnly] public int YMinInWorld;
        [ReadOnly] public int Margin;

        [ReadOnly] public float WorldTemperature;
        [ReadOnly] public float AirDensity;

        // In / Out
        [NativeDisableContainerSafetyRestriction] // Allow writing to the same array from multiple jobs.
        public NativeArray<Pixel> Buffer;

        public const float Deg2Rad = (float)math.PI / 180f;
        public const float Rad2Deg = 180f / (float)math.PI;

        [BurstCompile]
        public void Execute()
        {
            Random rnd = Random.CreateFromIndex(RandomSeed);

            // Old, none shuffled order (switching each frame).
            // HorizontalOrder = 1; // Debug

            // Create an array of shuffled indices for the x-axis.
            // This is used to randomize the update order within each row.
            // Randomizing gives a nicer look but slows down the horizontal movement since blocking is more likely.
            int numOfIndices = XMaxInBuffer - XMinInBuffer;
            NativeArray<int> shuffledIndices = new NativeArray<int>(numOfIndices, Allocator.Temp);
            for (int i = 0; i < numOfIndices; i++)
            {
                shuffledIndices[i] = XMinInBuffer + i;
            }
            for (int i = numOfIndices - 1; i > 0; i--)
            {
                int randomIndex = rnd.NextInt(0, numOfIndices);
                int temp = shuffledIndices[i];
                shuffledIndices[i] = shuffledIndices[randomIndex];
                shuffledIndices[randomIndex] = temp;
            }

            Pixel pixel;
            int index;

            // Simulation pass
            bool didMove;
            int finalX, finalY;
            for (int y = YMinInBuffer; y < YMaxInBuffer; y++)
            {
                // Alternating ordered indices per line.
                //for (int x = (HorizontalOrder == 1) ? XMinInBuffer : XMaxInBuffer - 1;
                //    (HorizontalOrder == 1) ? x < XMaxInBuffer : x >= XMinInBuffer;
                //    x += HorizontalOrder)
                //{

                // Shuffled indices
                for (int i = 0; i < numOfIndices; i++)
                {
                    int x = shuffledIndices[i];

                    didMove = false;
                    finalX = x;
                    finalY = y;

                    index = PixelWorldChunk.CoordinatesToIndex(x, y, BufferWidth);
                    pixel = Buffer[index];

                    if (!pixel.IsLoaded() || pixel.IsEmpty() || !pixel.RequiresSimulation() || pixel.IsAsleep())
                    {
                        continue;
                    }

                    // Check if inside of margins. If yes, then simulate.
                    if (x > Margin && y > Margin && x < BufferWidth - Margin && y < BufferHeight - Margin)
                    {
                        simulatePixel(x, y, ref rnd, ref didMove, ref finalX, ref finalY, ref Materials);
                    }

                    // Mark as simulated at source position.
                    pixel = Buffer[index];
                    pixel.MarkAsSimulated();

                    Buffer[index] = pixel;
                }
            }

            // Analysis pass (may change properites but does not change pixel positions)
            for (int y = YMinInBuffer; y < YMaxInBuffer; y++)
            {
                for (int x = (HorizontalOrder == 1) ? XMinInBuffer : XMaxInBuffer - 1;
                    (HorizontalOrder == 1) ? x < XMaxInBuffer : x >= XMinInBuffer;
                    x += HorizontalOrder)
                {
                    index = PixelWorldChunk.CoordinatesToIndex(x, y, BufferWidth);
                    pixel = Buffer[index];

                    if (!pixel.IsLoaded() || pixel.IsEmpty())
                        continue;

                    // Check if inside of margins. If yes, then analyze.
                    if (x > Margin && y > Margin && x < BufferWidth - Margin && y < BufferHeight - Margin)
                    {
                        // Stop or start free falling.
                        if (updateFreeFalling(ref pixel, shouldBeFreeFalling(x, y), ref rnd))
                        {
                            Buffer[index] = pixel;
                        }

                        // Drag along if pixel above is free falling
                        // Cheap form of downward pressure.
                        if (pixel.IsFreeFalling())
                        {
                            if (inBounds(x, y + 1))
                            {
                                Pixel top = getPixel(x, y + 1);
                                if (top.IsFreeFalling())
                                {
                                    pixel.velocityY = math.min(top.velocityY, pixel.velocityY);
                                    pixel.deltaY = math.min(top.deltaY, pixel.deltaY);

                                    pixel.velocityX = top.velocityX;
                                    pixel.deltaX = top.deltaX;

                                    Buffer[index] = pixel;
                                }
                            }
                        }
                    }
                }
            }

            shuffledIndices.Dispose();

#if UNITY_EDITOR
            // Debug pass (only enable if you want to debug something)
            debugPass();
#endif
        }

        /// <summary>
        /// Returns true if the free falling state has been changed.
        /// </summary>
        /// <param name="pixel"></param>
        /// <param name="xInBuffer"></param>
        /// <param name="yInBuffer"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool updateFreeFalling(ref Pixel pixel, bool shouldBeFreeFalling, ref Random rnd)
        {
            if (shouldBeFreeFalling)
            {
                if (!pixel.IsFreeFalling())
                {
                    startFreeFalling(ref pixel, ref rnd);
                    return true;
                }
            }
            else
            {
                if (pixel.IsFreeFalling())
                {
                    stopFreeFalling(ref pixel);
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void startFreeFalling(ref Pixel pixel, ref Random rnd)
        {
            pixel.StartFreeFalling();

            if (pixel.IsAffectedByGravity(ref Materials))
            {
                // If a pixel starts free falling we should retain the velocity
                // set by the simulation. However these speeds are often quite high.
                // Thus we reduce them by 50% (looks and feels better).

                float factor = 0.2f + 0.3f * rnd.NextFloat();
                pixel.deltaX = clampToMagnitude(pixel.deltaX * factor, 1f);
                pixel.velocityX = clampToMagnitude(pixel.velocityX * factor, 4f);

                pixel.deltaY = clampToMagnitude(pixel.deltaY * factor, 1f);
                pixel.velocityY = clampToMagnitude(pixel.velocityY * factor, 4f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void stopFreeFalling(ref Pixel pixel)
        {
            pixel.StopFreeFalling();

            if (pixel.IsAffectedByGravity(ref Materials))
            {
                pixel.SetVelocityXToPseudoZero();
                pixel.SetVelocityYToPseudoZero();
                pixel.deltaX = 0;
                pixel.deltaY = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool shouldBeFreeFalling(int xInBuffer, int yInBuffer)
        {
            Pixel pixel = getPixel(xInBuffer, yInBuffer);
            if (!pixel.IsAffectedByGravity(ref Materials))
                return false;

            // Check if pixelBelow is in bounds
            if (yInBuffer - 1 >= 0)
            {
                Pixel pixelBelow = getPixel(xInBuffer, yInBuffer - 1);
                return pixelBelow.IsEmpty() || pixelBelow.IsFreeFalling();
            }
            else
            {
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void simulatePixel(
            int xInBuffer, int yInBuffer,
            ref Unity.Mathematics.Random rnd,
            ref bool didMove,
            ref int finalXInBuffer, ref int finalYInBuffer,
            ref NativeArray<PixelMaterial> materials)
        {
            // NOTICE:
            // * This is only executed on pixels that are not empty, are awake and are scheduled for simulation.
            // * Still, anything you do in here may be executed thousands of times per frame.

            Pixel pixel = getPixel(xInBuffer, yInBuffer);

            // Gravity (only applied to free falling pixels)
            if (pixel.IsAffectedByGravity(ref materials))
            {
                // Apply gravity to velocity if below terminal velocity (max distance of simulation or 90 m/s).
                float maxVelocity = math.min(pixelsPerFrameToUnitsPerSecond(PixelWorld.SimulationMargin-1), 90);

                var material = pixel.GetMaterial(ref materials);
                pixel.SetVelocityY( math.clamp(pixel.velocityY - 9.81f * FixedDeltaTime * material.gravityScale, -maxVelocity, maxVelocity) );
                pixel.SetVelocityX( math.clamp(pixel.velocityX, -maxVelocity, maxVelocity) );
                setPixel(ref pixel, xInBuffer, yInBuffer);
            }

            // Apply velocity to position deltas - NOTICE: Velocities are im m/s, not pixels!
            pixel.ApplyVelocityToPositionDelta(FixedDeltaTime, PixelsPerUnit);

            // Apply friction
            if (!pixel.IsFreeFalling())
            {
                var pixelBelow = getPixel(xInBuffer, yInBuffer - 1);
                if (!pixelBelow.IsEmpty())
                {
                    pixel.ApplyFriction(FixedDeltaTime, ref pixelBelow, ref materials);
                }
            }

            setPixel(ref pixel, xInBuffer, yInBuffer);

            // Movement based on behaviours
            if (pixel.HasBehaviour(PixelBehaviour.MoveLikeLiquid, ref materials))
            {
                moveLiquid(xInBuffer, yInBuffer, ref rnd, ref didMove, ref finalXInBuffer, ref finalYInBuffer, ref materials);
                pixel = getPixel(finalXInBuffer, finalYInBuffer);
            }
            else if (pixel.HasBehaviour(PixelBehaviour.MoveLikeSand, ref materials))
            {
                moveSand(xInBuffer, yInBuffer, ref rnd, ref didMove, ref finalXInBuffer, ref finalYInBuffer, ref materials);
                pixel = getPixel(finalXInBuffer, finalYInBuffer);
            }
            else if (pixel.HasBehaviour(PixelBehaviour.MoveLikeGas, ref materials))
            {
                moveGas(xInBuffer, yInBuffer, ref rnd, ref didMove, ref finalXInBuffer, ref finalYInBuffer);
                pixel = getPixel(finalXInBuffer, finalYInBuffer);
            }
            else
            {
                finalXInBuffer = xInBuffer;
                finalYInBuffer = yInBuffer;
            }

            // Effects FROM the 8 surrounding pixels
            receiveEffectFromNeighbour(ref pixel, finalXInBuffer, finalYInBuffer, -1, -1, ref materials, ref rnd);
            receiveEffectFromNeighbour(ref pixel, finalXInBuffer, finalYInBuffer,  0, -1, ref materials, ref rnd);
            receiveEffectFromNeighbour(ref pixel, finalXInBuffer, finalYInBuffer,  1, -1, ref materials, ref rnd);
                                                                                          
            receiveEffectFromNeighbour(ref pixel, finalXInBuffer, finalYInBuffer, -1, -1, ref materials, ref rnd);
            receiveEffectFromNeighbour(ref pixel, finalXInBuffer, finalYInBuffer,  1,  1, ref materials, ref rnd);
                                                                                          
            receiveEffectFromNeighbour(ref pixel, finalXInBuffer, finalYInBuffer, -1,  1, ref materials, ref rnd);
            receiveEffectFromNeighbour(ref pixel, finalXInBuffer, finalYInBuffer,  0,  1, ref materials, ref rnd);
            receiveEffectFromNeighbour(ref pixel, finalXInBuffer, finalYInBuffer,  1,  1, ref materials, ref rnd);

            // Self damage
            float selfDamage = pixel.GetSelfDamage(FixedDeltaTime, ref materials);
            if (selfDamage > 0.001f)
            {
                pixel.ChangeHealthBy(-selfDamage);
                if (pixel.IsDead())
                {
                    clearPixel(ref pixel, finalXInBuffer, finalYInBuffer);
                    pixel = getPixel(finalXInBuffer, finalYInBuffer);
                }
            }

            // Temperature
            // Make pixel temperature approach the world temperature (cool down or heat up)
            bool isBurning = pixel.IsBurning(ref materials);
            PixelMaterialId aggregateStateMaterialId = PixelMaterialId.Empty;
            if (math.abs(WorldTemperature - pixel.temperature) > 1f && !isBurning)
            {
                wakeUpNeighbours(finalXInBuffer, finalYInBuffer);

                // Change temperature in relation to the world tempreature (at a reduced speed).
                float temperatureDelta = (WorldTemperature - pixel.temperature) * pixel.GetHeatConductivity(FixedDeltaTime, ref materials) * 0.5f;
                aggregateStateMaterialId = pixel.ChangeTemperatureBy(temperatureDelta, ref materials);
                setPixel(ref pixel, finalXInBuffer, finalYInBuffer);
                changeAggregateStateIfNeeded(ref pixel, finalXInBuffer, finalYInBuffer, aggregateStateMaterialId, ref materials, ref rnd);
            }
            
            // Burning
            if (isBurning)
            {
                wakeUpNeighbours(finalXInBuffer, finalYInBuffer);

                // Burn
                aggregateStateMaterialId = pixel.Burn(FixedDeltaTime, ref materials);
                changeAggregateStateIfNeeded(ref pixel, finalXInBuffer, finalYInBuffer, aggregateStateMaterialId, ref materials, ref rnd);

                // Replace with empty pixel if dead (health = 0).
                if (pixel.IsDead())
                {
                    clearPixel(ref pixel, finalXInBuffer, finalYInBuffer);
                }
                else
                {
                    // Burning gfx effect
                    float buringColorRnd = rnd.NextFloat();
                    if(buringColorRnd < 0.33f)
                        pixel.SetColor(255,0,0);
                    else if(buringColorRnd < 0.66f)
                        pixel.SetColor(255, 255, 0);
                    else
                        pixel.SetColor(255, 150, 0);

                    // Update pixel
                    setPixel(ref pixel, finalXInBuffer, finalYInBuffer);
                }
            }

            // Sleep counter
            if (!pixel.IsFreeFalling() && !didMove)
            {
                increaseSleepCounter(xInBuffer, yInBuffer);

                // set velocity to 0 if sleeping
                pixel = getPixel(xInBuffer, yInBuffer);
                if (pixel.IsAsleep())
                {
                    pixel.StopMoving();
                    setPixel(ref pixel, xInBuffer, yInBuffer);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void receiveEffectFromNeighbour(ref Pixel pixel, int xInBuffer, int yInBuffer, int xOffset, int yOffset, ref NativeArray<PixelMaterial> materials, ref Random rnd)
        {
            Pixel neighbourPixel = getPixel(xInBuffer + xOffset, yInBuffer + yOffset);
            if (neighbourPixel.IsBurning(ref materials) || neighbourPixel.HasBehaviour(PixelBehaviour.HeatTransfer, ref materials))
            {
                float temperatureDelta = (pixel.temperature - neighbourPixel.temperature) * pixel.GetHeatConductivity(FixedDeltaTime, ref materials);

                if (math.abs(temperatureDelta) > 1f)
                {
                    wakeUpNeighbours(xInBuffer + xOffset, yInBuffer + yOffset);
                }

                PixelMaterialId newAggregateStateMaterialId = pixel.ChangeTemperatureBy(-temperatureDelta, ref materials);
                setPixel(ref pixel, xInBuffer, yInBuffer);
                changeAggregateStateIfNeeded(ref pixel, xInBuffer, yInBuffer, newAggregateStateMaterialId, ref materials, ref rnd);

                newAggregateStateMaterialId = neighbourPixel.ChangeTemperatureBy(temperatureDelta, ref materials);
                setPixel(ref neighbourPixel, xInBuffer + xOffset, yInBuffer + yOffset);
                changeAggregateStateIfNeeded(ref neighbourPixel, xInBuffer + xOffset, yInBuffer + yOffset, newAggregateStateMaterialId, ref materials, ref rnd);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void changeAggregateStateIfNeeded(
            ref Pixel pixel, int xInBuffer, int yInBuffer, PixelMaterialId newAggregateStateMaterialId,
            ref NativeArray<PixelMaterial> materials, ref Unity.Mathematics.Random rnd)
        {
            if (!pixel.IsEmpty() && newAggregateStateMaterialId != PixelMaterialId.Empty)
            {
                // If aggregate state should change then do it now.
                PixelMaterial oldPixelMaterial = pixel.GetMaterial(ref Materials);
                if (newAggregateStateMaterialId != oldPixelMaterial.id)
                {
                    pixel = PixelFactory.CreatePixel(
                        pixel.x, pixel.y,
                        isEmpty: false, isLoaded: true,
                        ref Materials, newAggregateStateMaterialId,
                        ref rnd);
                    setPixel(ref pixel, xInBuffer, yInBuffer);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void increaseSleepCounter(int xInBuffer, int yInBuffer)
        {
            int index = getPixelIndex(xInBuffer, yInBuffer);
            Pixel pixel = Buffer[index];
            if (pixel.sleepCounter < Pixel.SleepCounterIsSleepingValue)
            {
                bool wasAwake = pixel.IsAwake();
                pixel.sleepCounter++;
                if (wasAwake && !pixel.IsAwake())
                {
                    pixel.Sleep();
                }
                Buffer[index] = pixel;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void moveSand(
            int xInBuffer, int yInBuffer,
            ref Unity.Mathematics.Random rnd,
            ref bool didMove,
            ref int finalX,
            ref int finalY,
            ref NativeArray<PixelMaterial> materials)
        {
            bool didCollide = false;
            int collisionX = 0;
            int collisionY = 0;

            Pixel pixel = getPixel(xInBuffer, yInBuffer);

            if (pixel.IsFreeFalling())
            {
                // Skip movement if velocity is too low to move a whole pixel (aka wait for movement delta to accumulate).
                if (math.abs(pixel.deltaX) >= 1 || math.abs(pixel.deltaY) >= 1)
                {
                    tryTravelBy(
                        xInBuffer, yInBuffer,
                        pixel.GetDeltaXInt(), pixel.GetDeltaYInt(),
                        earlyStop: false,
                        allowSelfIntersection: true,
                        onlyIfTargetIsReached: false,
                        updateVelocity: false,
                        ref rnd,
                        ref finalX, ref finalY,
                        ref didMove, ref didCollide, ref collisionX, ref collisionY);

                    // If the pixel did not move due to free falling but it has non-free falling neighbours
                    // then make it act as if it was not free falling (at least for sideways movement).
                    if (!didMove && hasPressuringNeighbour(xInBuffer, yInBuffer, maxDistance: 2, ref materials))
                    {
                        moveSandSideways(xInBuffer, yInBuffer, yOffset: -1, ref rnd, ref didMove, ref finalX, ref finalY);
                    }
                }
            }
            else
            {
                // Move down by 1
                tryTravelBy(
                    xInBuffer, yInBuffer,
                    xOffset: 0, yOffset: -1,
                    earlyStop: false,
                    allowSelfIntersection: false,
                    onlyIfTargetIsReached: false,
                    updateVelocity: true,
                    ref rnd,
                    ref finalX, ref finalY,
                    ref didMove, ref didCollide, ref collisionX, ref collisionY);

                if (!didMove)
                {
                    moveSandSideways(xInBuffer, yInBuffer, yOffset: -1, ref rnd, ref didMove, ref finalX, ref finalY);
                }
            }

            if (didCollide && !didMove)
            {
                pixel = getPixel(finalX, finalY);
                pixel.deltaX = 0;
                pixel.velocityX = 0;
                setPixel(ref pixel, finalX, finalY);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void moveSandSideways(
            int xInBuffer, int yInBuffer,
            int yOffset,
            ref Unity.Mathematics.Random rnd,
            ref bool didMove,
            ref int finalX,
            ref int finalY)
        {
            bool didCollide = false;
            int collisionX = 0;
            int collisionY = 0;

            Pixel pixel = getPixel(xInBuffer, yInBuffer);

            // Move down "left" by 1
            int firstHorizonalDirection = getFirstHorizontalDirection(ref pixel, ref rnd, updatePixelVelocity: true);

            tryTravelBy(
                xInBuffer, yInBuffer,
                xOffset: firstHorizonalDirection, yOffset,
                earlyStop: false,
                allowSelfIntersection: false,
                onlyIfTargetIsReached: false,
                updateVelocity: true,
                ref rnd,
                ref finalX, ref finalY,
                ref didMove, ref didCollide, ref collisionX, ref collisionY);

            // Move down "right" by 1
            if (!didMove)
            {
                tryTravelBy(
                    xInBuffer, yInBuffer,
                    xOffset: -firstHorizonalDirection, yOffset,
                    earlyStop: false,
                    allowSelfIntersection: false,
                    onlyIfTargetIsReached: false,
                    updateVelocity: true,
                    ref rnd,
                    ref finalX, ref finalY,
                    ref didMove, ref didCollide, ref collisionX, ref collisionY);
            }

            if (!didMove)
            {
                moveSandSidewaysFarAndFast(6, xInBuffer, yInBuffer, yOffset, ref rnd, ref didMove, ref finalX, ref finalY);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void moveSandSidewaysFarAndFast(
            int maxDistance,
            int xInBuffer, int yInBuffer,
            int yOffset,
            ref Unity.Mathematics.Random rnd,
            ref bool didMove,
            ref int finalX,
            ref int finalY)
        {
            bool didCollide = false;
            int collisionX = 0;
            int collisionY = 0;

            Pixel pixel = getPixel(xInBuffer, yInBuffer);

            // Move down "left" by 3
            int firstHorizonalDirection = getFirstHorizontalDirection(ref pixel, ref rnd, updatePixelVelocity: true);

            tryTravelBy(
                xInBuffer, yInBuffer,
                xOffset: firstHorizonalDirection * maxDistance, yOffset: (int)(maxDistance * math.sign(yOffset)),
                earlyStop: true,
                allowSelfIntersection: true,
                onlyIfTargetIsReached: false,
                updateVelocity: true,
                ref rnd,
                ref finalX, ref finalY,
                ref didMove, ref didCollide, ref collisionX, ref collisionY);

            // Move down "right" by 1
            if (!didMove)
            {
                tryTravelBy(
                    xInBuffer, yInBuffer,
                    xOffset: -firstHorizonalDirection * maxDistance, yOffset: (int)(maxDistance * math.sign(yOffset)),
                    earlyStop: true,
                    allowSelfIntersection: true,
                    onlyIfTargetIsReached: false,
                    updateVelocity: true,
                    ref rnd,
                    ref finalX, ref finalY,
                    ref didMove, ref didCollide, ref collisionX, ref collisionY);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void moveLiquid(
            int xInBuffer, int yInBuffer,
            ref Unity.Mathematics.Random rnd,
            ref bool didMove,
            ref int finalX,
            ref int finalY,
            ref NativeArray<PixelMaterial> materials)
        {
            bool didCollide = false;
            int collisionX = 0;
            int collisionY = 0;

            Pixel pixel = getPixel(xInBuffer, yInBuffer);

            if (pixel.IsFreeFalling())
            {
                // Skip movement if velocity is too low to move a whole pixel (aka wait for movement delta to accumulate).
                if (math.abs(pixel.deltaX) >= 1 || math.abs(pixel.deltaY) >= 1)
                {
                    tryTravelBy(
                        xInBuffer, yInBuffer,
                        pixel.GetDeltaXInt(), pixel.GetDeltaYInt(),
                        earlyStop: false,
                        allowSelfIntersection: true,
                        onlyIfTargetIsReached: false,
                        updateVelocity: false,
                        ref rnd,
                        ref finalX, ref finalY,
                        ref didMove, ref didCollide, ref collisionX, ref collisionY);

                    // If the pixel did not move due to free falling but it has non free falling neighbours
                    // then make it act as if it was not free falling. This allows pixels to move sideways
                    // with simulation logic. It kinda emulates pressure (non free falling pixels "pushgin"
                    // the free falling pixels to the side).
                    if (!didMove && hasPressuringNeighbour(xInBuffer, yInBuffer, maxDistance: 4, ref materials))
                    {
                        moveLiquidSideways(xInBuffer, yInBuffer, yOffset: -1, ref rnd, ref didMove, ref finalX, ref finalY);
                    }
                }
            }
            else
            {
                // Move down by 1
                tryTravelBy(
                    xInBuffer, yInBuffer,
                    xOffset: 0, yOffset: -1,
                    earlyStop: false,
                    allowSelfIntersection: false,
                    onlyIfTargetIsReached: false,
                    updateVelocity: true,
                    ref rnd,
                    ref finalX, ref finalY,
                    ref didMove, ref didCollide, ref collisionX, ref collisionY);

                if (!didMove)
                {
                    moveLiquidSideways(xInBuffer, yInBuffer, yOffset: -1, ref rnd, ref didMove, ref finalX, ref finalY);
                }
            }

            if (didCollide && !didMove)
            {
                pixel = getPixel(finalX, finalY);
                pixel.deltaX = 0;
                pixel.velocityX = 0;
                setPixel(ref pixel, finalX, finalY);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void moveLiquidSideways(
            int xInBuffer, int yInBuffer,
            int yOffset,
            ref Unity.Mathematics.Random rnd,
            ref bool didMove,
            ref int finalX, 
            ref int finalY)
        {
            bool didCollide = false;
            int collisionX = 0;
            int collisionY = 0;

            Pixel pixel = getPixel(xInBuffer, yInBuffer);

            // Slide "left"
            int firstHorizonalDirection = getFirstHorizontalDirection(ref pixel, ref rnd, updatePixelVelocity: true);

            // First try moving to the side with earlyStop = true but distance = 2 * flowSpeed.
            // However the distance is limited to the simulation margin.
            int maxSearchDistance = math.min(pixel.GetFlowSpeed(ref Materials) * 2, PixelWorld.SimulationMargin);
            tryTravelBy(
                xInBuffer, yInBuffer,
                xOffset: maxSearchDistance * firstHorizonalDirection, yOffset: 0,
                earlyStop: true, // <- makes it stop at the first empty spot it finds.
                allowSelfIntersection: true, // <- liquids can intersect themselves.
                onlyIfTargetIsReached: false,
                updateVelocity: true,
                ref rnd,
                ref finalX, ref finalY,
                ref didMove, ref didCollide, ref collisionX, ref collisionY);
            // Second: Move as far as possible but at reduced speed.
            if(!didMove)
            {
                tryTravelBy(
                    xInBuffer, yInBuffer,
                    xOffset: pixel.GetFlowSpeed(ref Materials) * firstHorizonalDirection, yOffset: 0,
                    earlyStop: false, // <- moves as far away as it can (can leave gaps)
                    allowSelfIntersection: true,
                    onlyIfTargetIsReached: false,
                    updateVelocity: true,
                    ref rnd,
                    ref finalX, ref finalY,
                    ref didMove, ref didCollide, ref collisionX, ref collisionY);
            }

            // Slide "right"
            if (!didMove)
            {
                tryTravelBy(
                    xInBuffer, yInBuffer,
                    xOffset: maxSearchDistance * -firstHorizonalDirection, yOffset: 0,
                    earlyStop: true, // <- makes it stop at the first empty spot it finds.
                    allowSelfIntersection: true,
                    onlyIfTargetIsReached: false,
                    updateVelocity: true,
                    ref rnd,
                    ref finalX, ref finalY,
                    ref didMove, ref didCollide, ref collisionX, ref collisionY);
            }
            if (!didMove)
            {
                tryTravelBy(
                    xInBuffer, yInBuffer,
                    xOffset: pixel.GetFlowSpeed(ref Materials) * -firstHorizonalDirection, yOffset: 0,
                    earlyStop: false,// <- moves as far away as it can (can leave gaps)
                    allowSelfIntersection: true,
                    onlyIfTargetIsReached: false,
                    updateVelocity: true,
                    ref rnd,
                    ref finalX, ref finalY,
                    ref didMove, ref didCollide, ref collisionX, ref collisionY);
            }

            // If moved to the side then try to densify the liquid afterwards.
            if (didMove)
            {
                bool didMoveDown = false;
                bool didCollideDown = false;
                int collisionXDown = 0;
                int collisionYDown = 0;

                tryTravelBy(
                    finalX, finalY,
                    xOffset: 0, yOffset,
                    earlyStop: false,
                    allowSelfIntersection: true,
                    onlyIfTargetIsReached: false,
                    updateVelocity: true,
                    ref rnd,
                    ref finalX, ref finalY,
                    ref didMoveDown, ref didCollideDown, ref collisionXDown, ref collisionYDown);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void moveGas(
            int xInBuffer, int yInBuffer,
            ref Unity.Mathematics.Random rnd,
            ref bool didMove,
            ref int finalX,
            ref int finalY)
        {
            bool didCollide = false;
            int collisionX = 0;
            int collisionY = 0;

            Pixel pixel = getPixel(xInBuffer, yInBuffer);

            // Move up by 1
            tryTravelBy(
                xInBuffer, yInBuffer,
                xOffset: 0, yOffset: 1,
                earlyStop: false,
                allowSelfIntersection: false,
                onlyIfTargetIsReached: false,
                updateVelocity: false,
                ref rnd,
                ref finalX, ref finalY,
                ref didMove, ref didCollide, ref collisionX, ref collisionY);

            if (!didMove)
            {
                moveLiquidSideways(xInBuffer, yInBuffer, yOffset: 1, ref rnd, ref didMove, ref finalX, ref finalY);
            }

            if (didCollide && !didMove)
            {
                pixel = getPixel(finalX, finalY);
                pixel.deltaX = 0;
                pixel.velocityX = 0;
                setPixel(ref pixel, finalX, finalY);
            }
        }

        /// <summary>
        /// Move performs no in-between checks. It simply checks if the pixel can move according to the rules.
        /// </summary>
        /// <param name="xInBuffer"></param>
        /// <param name="yInBuffer"></param>
        /// <param name="xInBufferTarget"></param>
        /// <param name="yInBufferTarget"></param>
        private bool canMoveTo(
            int xInBuffer, int yInBuffer,
            int xInBufferTarget, int yInBufferTarget)
        {
            if (!inBounds(xInBufferTarget, yInBufferTarget))
            {
                return false;
            }

            Pixel targetPixel = getPixel(xInBufferTarget, yInBufferTarget);

            // Empty?
            if (targetPixel.IsEmpty())
            {
                return true;
            }

            // Static?
            Pixel sourcePixel = getPixel(xInBuffer, yInBuffer);
            if (targetPixel.HasBehaviour(PixelBehaviour.Static, ref Materials))
            {
                return false;
            }

            // Combine?
            else if (sourcePixel.IsCombinableWith(ref targetPixel, ref Materials) || targetPixel.IsCombinableWith(ref sourcePixel, ref Materials))
            {
                return true;
            }

            // Displace?
            else if (sourcePixel.IsDenserThanAndCanBeSwappedWith(ref targetPixel, ref Materials))
            {
                return true;
            }

            // Add more MOVEMENT based pixel to pixel interaction checks here.
            // NOTICE: They are in ORDER.
            /*
            else if (...)
            {
                  // ...
            }
            */

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void tryMoveBy(
            int xInBuffer, int yInBuffer,
            int xOffset, int yOffset,
            bool updateVelocity,
            ref Random rnd, ref bool didMove)
        {
            tryMoveTo(xInBuffer, yInBuffer, xInBuffer + xOffset, yInBuffer + yOffset, updateVelocity, ref rnd, ref didMove);
        }

        /// <summary>
        /// Move performs no in-between checks. It simply moves the pixel according to the rules.
        /// Every moving pixel goes through this method.
        /// </summary>
        /// <param name="xInBuffer"></param>
        /// <param name="yInBuffer"></param>
        /// <param name="xInBufferTarget"></param>
        /// <param name="yInBufferTarget"></param>
        /// <param name="rnd"></param>
        /// <param name="didMove">TRUE if the pixel was moved. False if movement was blocked.</param>
        private void tryMoveTo(
        int xInBuffer, int yInBuffer,
        int xInBufferTarget, int yInBufferTarget,
        bool updateVelocity, ref Random rnd,
        ref bool didMove)
        {
            if (!inBounds(xInBufferTarget, yInBufferTarget))
            {
                return;
            }

            // Don't forget to update these after position changes (pixels are value types).
            Pixel sourcePixel = getPixel(xInBuffer, yInBuffer);
            Pixel targetPixel = getPixel(xInBufferTarget, yInBufferTarget);

            // Empty?
            if (targetPixel.IsEmpty())
            {
                // Pixel moves into empty space
                bool didSwap = false;
                swapPixels(xInBuffer, yInBuffer, xInBufferTarget, yInBufferTarget, clearPixel0: true, clearPixel1: false, ref rnd, ref didSwap);

                // Update source and target references since they are now swapped and pixel are value types!
                if (didSwap)
                {
                    sourcePixel = getPixel(xInBufferTarget, yInBufferTarget);
                    targetPixel = getPixel(xInBuffer, yInBuffer);
                }

                didMove |= didSwap;
            }
            else
            {
                // Static?
                // -> NoOp (static pixels should never arrive in here)

                // Interaction between two non empty pixels.
                bool handled = false;

                // Combine?
                if (!handled)
                {
                    // Corrode
                    // Corrosion applies the damage to both pixels.
                    if (sourcePixel.HasBehaviour(PixelBehaviour.CombineByCorrosion, ref Materials) || targetPixel.HasBehaviour(PixelBehaviour.CombineByCorrosion, ref Materials))
                    {
                        if (sourcePixel.IsCombinableWith(ref targetPixel, ref Materials))
                        {
                            // source > target
                            sourcePixel.ChangeHealthBy(-sourcePixel.GetDamage(FixedDeltaTime, ref Materials));
                            setPixel(ref sourcePixel, xInBuffer, yInBuffer);

                            targetPixel.ChangeHealthBy(-sourcePixel.GetDamage(FixedDeltaTime, ref Materials));
                            setPixel(ref targetPixel, xInBufferTarget, yInBufferTarget);

                            if (targetPixel.IsDead() && sourcePixel.IsDead())
                            {
                                clearPixel(ref sourcePixel, xInBuffer, yInBuffer);
                                clearPixel(ref targetPixel, xInBufferTarget, yInBufferTarget);
                                sourcePixel = getPixel(xInBuffer, yInBuffer);
                                targetPixel = getPixel(xInBufferTarget, yInBufferTarget);
                            }
                            else if (targetPixel.IsDead() && !sourcePixel.IsDead())
                            {
                                bool didSwap = false;
                                swapPixels(xInBuffer, yInBuffer, xInBufferTarget, yInBufferTarget, clearPixel0: true, clearPixel1: false, ref rnd, ref didSwap);
                                if (didSwap)
                                {
                                    sourcePixel = getPixel(xInBufferTarget, yInBufferTarget);
                                    targetPixel = getPixel(xInBuffer, yInBuffer);
                                }
                                didMove |= didSwap;
                            }
                            else if (!targetPixel.IsDead() && sourcePixel.IsDead())
                            {
                                clearPixel(ref sourcePixel, xInBuffer, yInBuffer);
                                sourcePixel = getPixel(xInBuffer, yInBuffer);
                            }
                            handled = true;
                        }
                        else if (targetPixel.IsCombinableWith(ref sourcePixel, ref Materials))
                        {
                            // target > source
                            targetPixel.ChangeHealthBy(-targetPixel.GetDamage(FixedDeltaTime, ref Materials));
                            setPixel(ref targetPixel, xInBufferTarget, yInBufferTarget);

                            sourcePixel.ChangeHealthBy(-targetPixel.GetDamage(FixedDeltaTime, ref Materials));
                            setPixel(ref sourcePixel, xInBuffer, yInBuffer);

                            if (sourcePixel.IsDead() && targetPixel.IsDead())
                            {
                                clearPixel(ref sourcePixel, xInBuffer, yInBuffer);
                                clearPixel(ref targetPixel, xInBufferTarget, yInBufferTarget);
                                sourcePixel = getPixel(xInBuffer, yInBuffer);
                                targetPixel = getPixel(xInBufferTarget, yInBufferTarget);
                            }
                            else if (sourcePixel.IsDead() && !targetPixel.IsDead())
                            {
                                bool didSwap = false;
                                swapPixels(xInBufferTarget, yInBufferTarget, xInBuffer, yInBuffer, clearPixel0: true, clearPixel1: false, ref rnd, ref didSwap);
                                if (didSwap)
                                {
                                    sourcePixel = getPixel(xInBufferTarget, yInBufferTarget);
                                    targetPixel = getPixel(xInBuffer, yInBuffer);
                                }
                                didMove |= didSwap;
                            }
                            else if (!sourcePixel.IsDead() && targetPixel.IsDead())
                            {
                                clearPixel(ref targetPixel, xInBufferTarget, yInBufferTarget);
                                targetPixel = getPixel(xInBuffer, yInBuffer);
                            }
                            handled = true;
                        }
                    }
                }

                // Displace?
                if (!handled && (sourcePixel.IsDenserThanAndCanBeSwappedWith(ref targetPixel, ref Materials)))
                {
                    bool didSwap = false;
                    if (xInBuffer == xInBufferTarget)
                    {
                        swapPixels(xInBuffer, yInBuffer, xInBufferTarget, yInBufferTarget, clearPixel0: false, clearPixel1: false, ref rnd, ref didSwap);
                        // Update source and target references since they are now swapped and pixel are value types!
                        if (didSwap)
                        {
                            sourcePixel = getPixel(xInBufferTarget, yInBufferTarget);
                            targetPixel = getPixel(xInBuffer, yInBuffer);
                        }
                        didMove |= didSwap;
                        handled = true;
                    }
                    // Diagonal density displacement only if it's a direct neighbour.
                    else if (math.abs(xInBufferTarget - xInBuffer) <= 1 && math.abs(yInBufferTarget - yInBuffer) <= 1)
                    {
                        densityDisplacePixelsDiagonally(xInBuffer, yInBuffer, xInBufferTarget, yInBufferTarget, ref rnd, ref didSwap);
                        // Update source and target references since they are now swapped and pixel are value types!
                        if (didSwap)
                        {
                            sourcePixel = getPixel(xInBufferTarget, yInBufferTarget);
                            targetPixel = getPixel(xInBuffer, yInBuffer);
                        }
                        didMove |= didSwap;
                        handled = true;
                    }
                }

                // Add more MOVEMENT based pixel to pixel interactions here.
                // NOTICE: They are in ORDER.
                /*
                if(!handled)
                {
                      // ...
                }
                */
            }

            if (didMove && updateVelocity)
            {
                if (!sourcePixel.IsFreeFalling())
                {
                    sourcePixel.velocityX = pixelsPerFrameToUnitsPerSecond(xInBufferTarget - xInBuffer);
                    sourcePixel.velocityY = pixelsPerFrameToUnitsPerSecond(yInBufferTarget - yInBuffer);
                    setPixel(ref sourcePixel);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void densityDisplacePixelsDiagonally(int x0, int y0, int x1, int y1, ref Random rnd, ref bool hasBeenDisplaced)
        {
            Pixel targetPixel = getPixel(x1, y1);
            Pixel aboveTargetPixel = getPixel(x1, y1 + 1);
            Pixel leftTargetPixel = getPixel(x1 - 1, y1);
            Pixel rightTargetPixel = getPixel(x1 + 1, y1);
            if (
                !targetPixel.RequiresSimulation() && targetPixel.IsMovableOrEmpty(ref Materials) && aboveTargetPixel.IsMovableOrEmpty(ref Materials)
                // Avoid lower density pixels to crawl upward on slopes (e.g. check if they are surrounded by the same type or non movable types)
                && ((leftTargetPixel.materialIndex == targetPixel.materialIndex || !leftTargetPixel.IsMovableOrEmpty(ref Materials)) || (rightTargetPixel.materialIndex == targetPixel.materialIndex || !rightTargetPixel.IsMovableOrEmpty(ref Materials)))
                )
            {
                if (targetPixel.GetDensity(ref Materials) <= aboveTargetPixel.GetDensity(ref Materials) || aboveTargetPixel.IsEmpty())
                {
                    // Move empty space from above down (moves target pixel up).
                    swapPixels(x1, y1, x1, y1 + 1, clearPixel0: false, clearPixel1: false, ref rnd, ref hasBeenDisplaced);
                    // Swap empty space with source pixel
                    swapPixels(x0, y0, x1, y1, clearPixel0: false, clearPixel1: false, ref rnd, ref hasBeenDisplaced);
                }
            }
        }

        /// <summary>
        /// Swaps pixels or displaces them. If clear is true then the original pixel position is cleared.<br />
        /// Every moving pixel goes through this method (one or more times per frame).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void swapPixels(int xInBuffer0, int yInBuffer0, int xInBuffer1, int yInBuffer1, bool clearPixel0, bool clearPixel1, ref Random rnd, ref bool hasBeenSwapped)
        {
            if (!inBounds(xInBuffer1, yInBuffer1))
            {
                return;
            }

            int sourcePixelIndex = PixelWorldChunk.CoordinatesToIndex(xInBuffer0, yInBuffer0, BufferWidth);
            Pixel sourcePixel = Buffer[sourcePixelIndex];

            int targetPixelIndex = PixelWorldChunk.CoordinatesToIndex(xInBuffer1, yInBuffer1, BufferWidth);
            Pixel targetPixel = Buffer[targetPixelIndex];

            if (!sourcePixel.IsLoaded() || !targetPixel.IsLoaded())
                return;

            int dx = xInBuffer1 - xInBuffer0;
            int dy = yInBuffer1 - yInBuffer0;

            // Reduce deltaX but never invert direction.
            if (math.abs(sourcePixel.deltaX) < math.abs(dx))
                sourcePixel.deltaX = 0f;
            else
                sourcePixel.deltaX -= dx;
            // Reduce dY but never invert direction.
            if (math.abs(sourcePixel.deltaY) < math.abs(dy))
                sourcePixel.deltaY = 0f;
            else
                sourcePixel.deltaY -= dy;

            // Mark as simulated & wake up
            sourcePixel.MarkAsSimulated();
            sourcePixel.WakeUp();

            // Temp copy target pixel properties before swap
            int targetPosX = targetPixel.x;
            int targetPosY = targetPixel.y;

            if (clearPixel0)
            {
                // clear source (instead of moving target to source)
                Pixel clearPixel = PixelFactory.CreateEmpty(sourcePixel.x, sourcePixel.y, loaded: true);
                clearPixel.isLoaded = sourcePixel.isLoaded;
                clearPixel.MarkAsSimulated();
                Buffer[sourcePixelIndex] = clearPixel;
            }
            else
            {
                // move target to source
                targetPixel.x = sourcePixel.x;
                targetPixel.y = sourcePixel.y;
                targetPixel.MarkAsSimulated();
                if (targetPixel.IsEmpty())
                {
                    targetPixel.StopFreeFalling();
                }
                else
                {
                    updateFreeFalling(ref targetPixel, sourcePixel.IsFreeFalling(), ref rnd);
                }
                Buffer[sourcePixelIndex] = targetPixel;
            }

            // move source to target
            sourcePixel.x = targetPosX;
            sourcePixel.y = targetPosY;
            Buffer[targetPixelIndex] = sourcePixel;

            // Update free falling state of source for new location.
            updateFreeFalling(ref sourcePixel, shouldBeFreeFalling(xInBuffer1, yInBuffer1), ref rnd);
            setPixel(ref sourcePixel, xInBuffer1, yInBuffer1);

            // wake up neighbours
            wakeUpNeighbours(xInBuffer0, yInBuffer0);
            wakeUpNeighbours(xInBuffer1, yInBuffer1);

            if (clearPixel1)
            {
                // clear source (instead of moving target to source)
                Pixel clearPixel = PixelFactory.CreateEmpty(targetPosX, targetPosY, loaded: true);
                int index = PixelWorldChunk.CoordinatesToIndex(xInBuffer1, yInBuffer1, BufferWidth);
                clearPixel.isLoaded = Buffer[index].isLoaded;
                clearPixel.MarkAsSimulated();
                Buffer[index] = clearPixel;
            }

            hasBeenSwapped = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void clearPixel(ref Pixel pixel)
        {
            getPixelPositionInBuffer(ref pixel, out int xInBuffer, out int yInBuffer);
            clearPixel(ref pixel, xInBuffer, yInBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void clearPixel(ref Pixel pixel, int xInBuffer, int yInBuffer)
        {
            int index = PixelWorldChunk.CoordinatesToIndex(xInBuffer, yInBuffer, BufferWidth);

            pixel = PixelFactory.CreateEmpty(pixel.x, pixel.y, loaded: true);
            pixel.isLoaded = Buffer[index].isLoaded;
            pixel.MarkAsSimulated();

            Buffer[index] = pixel;
        }
    }
}
