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
    public partial struct PixelWorldSimulationJob : IJob
    {
        /// <summary>
        /// Randomize whether the pixel slides left or right.
        /// If the pixel already has a velocity then use
        /// that as the preferred direction to avoid oscillations.
        /// </summary>
        /// <param name="pixel"></param>
        /// <param name="rnd"></param>
        /// <param name="updatePixelVelocity"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getFirstHorizontalDirection(ref Pixel pixel, ref Random rnd, bool updatePixelVelocity)
        {
            int direction;
            if (pixel.velocityX == 0f)
            {
                direction = rnd.NextBool() ? -1 : 1;
            }
            else
            {
                direction = pixel.velocityX < 0f ? -1 : 1;
            }

            // Update velocity to make the pixel remember the preferred direction.
            if (updatePixelVelocity && pixel.velocityX == 0f)
            {
                pixel.SetVelocityDirectionX(direction);
                setPixel(ref pixel);
            }

            return direction;
        }

        private void tryTravelByAngleSweep(
            int xInBuffer, int yInBuffer,
            int xOffset, int yOffset,
            float flowMaxAngle,
            int firstDirection,
            bool earlyStop, bool allowSelfIntersection, bool onlyIfTargetIsReached,
            bool updateVelocity,
            ref Random rnd,
            ref int finalX,
            ref int finalY,
            ref bool didMove,
            ref bool didCollide, ref int collisionX, ref int collisionY)
        {
            Pixel pixel = getPixel(xInBuffer, yInBuffer);

            // First try moving directly along the offset direction.
            tryTravelBy(
                xInBuffer, yInBuffer, xOffset, yOffset,
                earlyStop, allowSelfIntersection, onlyIfTargetIsReached,
                updateVelocity, ref rnd,
                ref finalX, ref finalY, ref didMove, ref didCollide, ref collisionX, ref collisionY);
            if (didMove)
                return;

            // Sweep search if direct move did not work.

            float distance = math.sqrt(xOffset * xOffset + yOffset * yOffset);
            if (distance <= 0f)
                return;

            // Calc what angle would be needed to change the y position by 1 (angle gets lower the bigger the distance is).
            float anglePerPx = math.atan(0.9f / distance) * Rad2Deg;
            int numOfSteps = (int)math.floor(flowMaxAngle / anglePerPx);
            float startAngle = getAngle360(xOffset, yOffset);

            if (numOfSteps > 0)
            {
                float angleStep = flowMaxAngle / (float)numOfSteps;
                float angle;

                // sweep search
                for (int i = 1; i < numOfSteps; ++i) // start with 1 because be have already checked angle delta 0 above.
                {
                    // first direction
                    angle = startAngle + angleStep * i * firstDirection;
                    tryTravelByAngleAndDistance(xInBuffer, yInBuffer, angle, distance, earlyStop, allowSelfIntersection, onlyIfTargetIsReached, updateVelocity, ref rnd, ref finalX, ref finalY, ref didMove, ref didCollide, ref collisionX, ref collisionY);
                    if (didMove)
                    {
                        break;
                    }

                    // second direction
                    if (!didMove)
                    {
                        angle = startAngle + angleStep * i * -firstDirection;
                        tryTravelByAngleAndDistance(xInBuffer, yInBuffer, angle, distance, earlyStop, allowSelfIntersection, onlyIfTargetIsReached, updateVelocity, ref rnd, ref finalX, ref finalY, ref didMove, ref didCollide, ref collisionX, ref collisionY);
                        if (didMove)
                        {
                            break;
                        }
                    }
                }
            }

            if (!didMove)
            {
                tryTravelByAngleAndDistance(xInBuffer, yInBuffer, startAngle + flowMaxAngle * firstDirection, distance, earlyStop, allowSelfIntersection, onlyIfTargetIsReached, updateVelocity, ref rnd, ref finalX, ref finalY, ref didMove, ref didCollide, ref collisionX, ref collisionY);
                if (!didMove)
                {
                    tryTravelByAngleAndDistance(xInBuffer, yInBuffer, startAngle + flowMaxAngle * -firstDirection, distance, earlyStop, allowSelfIntersection, onlyIfTargetIsReached, updateVelocity, ref rnd, ref finalX, ref finalY, ref didMove, ref didCollide, ref collisionX, ref collisionY);
                }
            }
        }

        private void tryTravelByAngleAndDistance(
            int xInBuffer, int yInBuffer,
            float angle, float distance,
            bool earlyStop, bool allowSelfIntersection, bool onlyIfTargetIsReached,
            bool updateVelocity,
            ref Random rnd,
            ref int finalX,
            ref int finalY,
            ref bool didMove,
            ref bool didCollide, ref int collisionX, ref int collisionY)
        {
            int xOffset = (int) math.round(math.cos(angle * Deg2Rad) * distance);
            int yOffset = (int) math.round(math.sin(angle * Deg2Rad) * distance);

            tryTravelTo(
                xInBuffer, yInBuffer,
                xInBuffer + xOffset, yInBuffer + yOffset,
                earlyStop,
                allowSelfIntersection,
                onlyIfTargetIsReached,
                updateVelocity,
                ref rnd,
                ref finalX, ref finalY,
                ref didMove, ref didCollide, ref collisionX, ref collisionY);
        }
        
        private bool canTravelByAngleAndDistance(
            int xInBuffer, int yInBuffer,
            float angle, float distance,
            bool earlyStop, bool allowSelfIntersection, bool onlyIfTargetIsReached,
            ref int finalX, ref int finalY,
            ref bool didCollide, ref int collisionX, ref int collisionY)
        {
            int xOffset = (int) math.round(math.cos(angle * Deg2Rad) * distance);
            int yOffset = (int) math.round(math.sin(angle * Deg2Rad) * distance);

            return canTravelTo(
                xInBuffer,
                yInBuffer,
                xInBuffer + xOffset,
                yInBuffer + yOffset,
                earlyStop,
                allowSelfIntersection,
                onlyIfTargetIsReached,
                ref finalX,
                ref finalY,
                ref didCollide,
                ref collisionX,
                ref collisionY);
        }

        private void tryTravelBy(
            int xInBuffer, int yInBuffer, int xOffset, int yOffset,
            bool earlyStop, bool allowSelfIntersection, bool onlyIfTargetIsReached,
            bool updateVelocity,
            ref Random rnd,
            ref int finalX, ref int finalY,
            ref bool didMove, ref bool didCollide, ref int collisionX, ref int collisionY)
        {
            tryTravelTo(
                xInBuffer, yInBuffer,
                xInBuffer + xOffset, yInBuffer + yOffset,
                earlyStop,
                allowSelfIntersection,
                onlyIfTargetIsReached,
                updateVelocity,
                ref rnd,
                ref finalX,
                ref finalY,
                ref didMove,
                ref didCollide,
                ref collisionX,
                ref collisionY);

            var p = getPixel(finalX, finalY);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xInBuffer"></param>
        /// <param name="yInBuffer"></param>
        /// <param name="targetXInBuffer"></param>
        /// <param name="targetYInBuffer"></param>
        /// <param name="earlyStop"></param>
        /// <param name="allowSelfIntersection"></param>
        /// <param name="onlyIfTargetIsReached"></param>
        /// <param name="finalX"></param>
        /// <param name="finalY"></param>
        /// <param name="didCollide">If allowSelfIntersection and the pixel did only collide with itself then this will be false.</param>
        /// <param name="collisionX"></param>
        /// <param name="collisionY"></param>
        /// <returns></returns>
        private bool canTravelTo(
            int xInBuffer, int yInBuffer, int targetXInBuffer, int targetYInBuffer,
            bool earlyStop, bool allowSelfIntersection, bool onlyIfTargetIsReached,
            ref int finalX, ref int finalY, ref bool didCollide, ref int collisionX, ref int collisionY)
        {
            finalX = xInBuffer;
            finalY = yInBuffer;

            if (!inBounds(targetXInBuffer, targetYInBuffer))
            {
                return false;
            }

            // If distance is <= 1 then call canMoveTo() directly.
            else if (math.abs(targetXInBuffer - xInBuffer) <= 1 && math.abs(targetYInBuffer - yInBuffer) <= 1)
            {
                if (canMoveTo(xInBuffer, yInBuffer, targetXInBuffer, targetYInBuffer))
                {
                    finalX = targetXInBuffer;
                    finalY = targetYInBuffer;
                    return true;
                }
                else
                {
                    didCollide = true;
                    collisionX = targetXInBuffer;
                    collisionY = targetYInBuffer;
                    return false;
                }
            }
            // If ONLY horizontal or vertical then use a simple for loop to move.
            // If earlyStop == true then move to the first valid position.
            // If earlyStop == false then move to the furthest position possible.
            else if (targetXInBuffer == xInBuffer || targetYInBuffer == yInBuffer)
            {
                Pixel src, target;
                bool canMove, selfIntersecting, ignoreCollision;

                if (targetXInBuffer == xInBuffer)
                {
                    int direction = targetYInBuffer > yInBuffer ? 1 : -1;
                    int steps = math.abs(yInBuffer - targetYInBuffer);
                    int distance = 0;
                    bool previousWasValid = true;
                    for (int step = 1; step < steps; step++)
                    {
                        src = getPixel(xInBuffer, yInBuffer);
                        target = getPixel(xInBuffer, yInBuffer + step * direction);
                        selfIntersecting = src.materialIndex == target.materialIndex;
                        canMove = canMoveTo(xInBuffer, yInBuffer, xInBuffer, yInBuffer + step * direction);
                        ignoreCollision = selfIntersecting && allowSelfIntersection;

                        if (!canMove && previousWasValid)
                        {
                            didCollide = true;
                            collisionX = xInBuffer;
                            collisionY = yInBuffer + step * direction;
                        }

                        if (canMove)
                        {
                            previousWasValid = true;
                            didCollide = false;

                            distance = step * direction;

                            if (earlyStop)
                            {
                                break;
                            }
                        }

                        if (!canMove && !ignoreCollision)
                        {
                            break;
                        }
                    }
                    if (distance != 0)
                    {
                        if (!onlyIfTargetIsReached || (yInBuffer + distance == targetYInBuffer))
                        {
                            finalX = xInBuffer;
                            finalY = yInBuffer + distance;
                            return true;
                        }
                    }
                }
                else
                {
                    int direction = targetXInBuffer > xInBuffer ? 1 : -1;
                    int steps = math.abs(xInBuffer - targetXInBuffer);
                    int distance = 0;
                    bool previousWasValid = true;
                    for (int step = 1; step < steps; step++)
                    {
                        src = getPixel(xInBuffer, yInBuffer);
                        target = getPixel(xInBuffer + step * direction, yInBuffer);
                        selfIntersecting = src.materialIndex == target.materialIndex;
                        canMove = canMoveTo(xInBuffer, yInBuffer, xInBuffer + step * direction, yInBuffer);
                        ignoreCollision = selfIntersecting && allowSelfIntersection;

                        if (!canMove && previousWasValid)
                        {
                            didCollide = true;
                            collisionX = xInBuffer + step * direction;
                            collisionY = yInBuffer;
                        }

                        if (canMove)
                        {
                            previousWasValid = true;
                            didCollide = false;

                            distance = step * direction;

                            if (earlyStop)
                            {
                                break;
                            }
                        }

                        if (!canMove && !ignoreCollision)
                        {
                            break;
                        }
                    }
                    if (distance != 0)
                    {
                        if (!onlyIfTargetIsReached || (xInBuffer + distance == targetXInBuffer))
                        {
                            finalX = xInBuffer + distance;
                            finalY = yInBuffer;
                            return true;
                        }
                    }
                }
            }
            // Move diagonally over multiple pixels.
            else
            {
                // Use Bresenham algorithm (ensure order from start to finish).
                Pixel src, target;
                bool canMove, selfIntersecting, ignoreCollision;

                int tmpFinalX = 0;
                int tmpFinalY = 0;
                bool move = false;

                // convert to primary quadrant to ensure order.
                int deltaX = math.abs(targetXInBuffer - xInBuffer);
                int directionX = targetXInBuffer == xInBuffer ? 0 : (targetXInBuffer > xInBuffer ? 1 : -1);

                int deltaY = math.abs(targetYInBuffer - yInBuffer);
                int directionY = targetYInBuffer == yInBuffer ? 0 : (targetYInBuffer > yInBuffer ? 1 : -1);

                int x0 = 0;
                int y0 = 0;
                int x1 = deltaX;
                int y1 = deltaY;
                // Calc is too steep to use x.
                bool steep = math.abs(y1 - y0) > math.abs(x1 - x0);
                if (steep)
                {
                    // swap x0 and y0
                    int t;
                    t = x0;
                    x0 = y0;
                    y0 = t;
                    // swap x1 and y1
                    t = x1;
                    x1 = y1;
                    y1 = t;
                }

                // The actual algorithm
                int dx = x1 - x0;
                int dy = math.abs(y1 - y0);
                int error = dx / 2;
                int yStep = (y0 < y1) ? 1 : -1;
                int y = y0;
                bool previousWasValid = true;
                for (int x = x0; x <= x1; x++)
                {
                    // Convert back to real coordinates & quadrant.
                    int targetX = xInBuffer + (steep ? y : x) * directionX;
                    int targetY = yInBuffer + (steep ? x : y) * directionY;

                    // Ignore start position since that's the pixel itself.
                    if (targetX != xInBuffer || targetY != yInBuffer)
                    {
                        // Check if pixel can be moved.
                        src = getPixel(xInBuffer, yInBuffer);
                        target = getPixel(targetX, targetY);
                        selfIntersecting = src.materialIndex == target.materialIndex;
                        canMove = canMoveTo(xInBuffer, yInBuffer, targetX, targetY);
                        ignoreCollision = selfIntersecting && allowSelfIntersection;

                        // Update collision.
                        if (!canMove && previousWasValid)
                        {
                            didCollide = true;
                            collisionX = targetX;
                            collisionY = targetY;
                        }

                        if (canMove)
                        {
                            previousWasValid = true;
                            move = true;

                            tmpFinalX = targetX;
                            tmpFinalY = targetY;

                            if (earlyStop)
                                break;
                        }

                        if (!canMove && !ignoreCollision)
                        {
                            break;
                        }
                    }

                    // Error accumulation
                    error = error - dy;
                    if (error < 0)
                    {
                        y += yStep;
                        error += dx;
                    }
                }

                // Perform move
                if (move)
                {
                    if (!onlyIfTargetIsReached || (tmpFinalX == targetXInBuffer && tmpFinalY == targetYInBuffer))
                    {
                        finalX = tmpFinalX;
                        finalY = tmpFinalY;
                        return true;
                    }
                }
            }

            return false;
        }

        private void tryTravelTo(
            int xInBuffer, int yInBuffer, int xInBufferTarget, int yInBufferTarget,
            bool earlyStop, bool allowSelfIntersection, bool onlyIfTargetIsReached,
            bool updateVelocity, ref Random rnd,
            ref int finalX, ref int finalY,
            ref bool didMove, ref bool didCollide, ref int collisionX, ref int collisionY)
        {
            if (canTravelTo(
                xInBuffer, yInBuffer, xInBufferTarget, yInBufferTarget,
                 earlyStop, allowSelfIntersection, onlyIfTargetIsReached, ref finalX, ref finalY,
                ref didCollide, ref collisionX, ref collisionY))
            {
                tryMoveTo(xInBuffer, yInBuffer, finalX, finalY, updateVelocity, ref rnd, ref didMove);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool hasPressuringNeighbour(int xInBuffer, int yInBuffer, int maxDistance, ref NativeArray<PixelMaterial> materials)
        {
            for (int i = -maxDistance; i <= maxDistance; i++)
            {
                if (i == 0) continue; // Skip middle since that's the pixel itself
                Pixel neighbour = getPixel(xInBuffer + i, yInBuffer);
                if (!neighbour.IsEmpty() && !neighbour.IsFreeFalling() && neighbour.IsAffectedByGravity(ref materials))
                {
                    return true;
                }
            }

            return false;
        }

        // Not used but I left it in here for reference.
        /*
        private void reflectPixel(ref Pixel pixel, int finalX, int finalY, ref Pixel collisionPixel, int collisionX, int collisionY, ref NativeArray<PixelMaterial> materials, ref Random rnd)
        {
            // TODO: Investigate why > ~0.94 leads to increase in velocity upon reflection.
            float bouncyness = 0.94f * pixel.GetMaterial(ref materials).bouncyness;

            // Reflection pixel
            Pixel horizontal = PixelFactory.CreateEmpty(collisionX, collisionY, true);
            Pixel vertical = PixelFactory.CreateEmpty(collisionX, collisionY, true);

            // Coming from ..
            // .. top left
            if (finalX < collisionX && finalY > collisionY)
            {
                // left of collision pixel (used below to check if empty)
                horizontal = getPixel(collisionX - 1, collisionY);
                // top of collision pixel (used below to check if empty)
                vertical = getPixel(collisionX, collisionY + 1);
            }
            // .. top right
            else if (finalX > collisionX && finalY > collisionY)
            {
                // right of collision pixel
                horizontal = getPixel(collisionX + 1, collisionY);
                // top of collision pixel
                vertical = getPixel(collisionX, collisionY + 1);
            }
            // .. bottom right
            else if (finalX > collisionX && finalY < collisionY)
            {
                // right of collision pixel
                horizontal = getPixel(collisionX + 1, collisionY);
                // bottom of collision pixel
                vertical = getPixel(collisionX, collisionY - 1);
            }
            // .. bottom left
            else if (finalX < collisionX && finalY < collisionY)
            {
                // left of collision pixel
                horizontal = getPixel(collisionX - 1, collisionY);
                // bottom of collision pixel
                vertical = getPixel(collisionX, collisionY - 1);
            }
            // .. top
            else if (finalX == collisionX && finalY > collisionY)
            {
                horizontal = collisionPixel;
            }
            // .. right
            else if (finalX > collisionX && finalY == collisionY)
            {
                vertical = collisionPixel;
            }
            // .. bottom
            else if (finalX == collisionX && finalY < collisionY)
            {
                horizontal = collisionPixel;
            }
            // .. left
            else if (finalX < collisionX && finalY == collisionY)
            {
                vertical = collisionPixel;
            }

            if (!horizontal.IsEmpty() && !vertical.IsEmpty())
            {
                // Both axis are blocked.
                // The only way to reflect is back out again (from collision to pixel).
                // ----------------------------
                // Vertical      | Pixel      |
                //-----------------------------
                // CollisonPixel | Horizontal |
                //-----------------------------
                pixel.velocityX *= -1f * bouncyness;
                pixel.deltaX *= -1f * bouncyness;
                pixel.velocityY *= -1f * bouncyness;
                pixel.deltaY *= -1f * bouncyness;
            }
            else if (!horizontal.IsEmpty())
            {
                pixel.velocityY *= -1f * bouncyness;
                pixel.deltaY *= -1f * bouncyness;
            }
            else if (!vertical.IsEmpty())
            {
                pixel.velocityX *= -1f * bouncyness;
                pixel.deltaX *= -1f * bouncyness;
            }
            else
            {
                // Both directions are empty -> randomize reflection axis.
                if (rnd.NextBool())
                {
                    pixel.velocityX *= -1f * bouncyness;
                    pixel.deltaX *= -1f * bouncyness;
                }
                else
                {
                    pixel.velocityY *= -1f * bouncyness;
                    pixel.deltaY *= -1f * bouncyness;
                }
            }
        }
        */
    }
}
