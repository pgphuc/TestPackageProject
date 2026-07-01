using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Kamgam.SandGame
{
    public partial class PixelWorld
    {
        public List<Pixel> _pixelsToDraw = new List<Pixel>(100);
        public List<Vector2> _pixelsToWakeUp = new List<Vector2>(200);

        protected bool tryGetPixelFromChunks(float x, float y, out Pixel pixel)
        {
            int xInt = Mathf.FloorToInt(x);
            int yInt = Mathf.FloorToInt(y);

            foreach (var chunk in ActiveChunks)
            {
                if (!chunk.LoadSucceeded || !chunk.Contains(xInt, yInt))
                    continue;

                pixel = chunk.GetPixel(xInt, yInt);
                return true;
            }

            pixel = new();
            pixel.x = (int)x;
            pixel.y = (int)y;
            return false;
        }

        protected bool trySetPixelInChunks(ref Pixel pixel)
        {
            foreach (var chunk in ActiveChunks)
            {
                if (!chunk.LoadSucceeded || !chunk.Contains(pixel.x, pixel.y))
                    continue;

                chunk.SetPixel(ref pixel);
                return true;
            }

            return false;
        }

        protected bool copyPixelScheduledForDrawing()
        {
            bool didChange = _pixelsToDraw.Count > 0;

            for (int i = _pixelsToDraw.Count-1; i >= 0; i--)
            {
                var pixel = _pixelsToDraw[i];
                if (trySetPixelInChunks(ref pixel))
                {
                    _pixelsToDraw.RemoveAt(i);
                }
            }

            for (int i = _pixelsToWakeUp.Count - 1; i >= 0; i--)
            {
                if (TryGetPixelAt(_pixelsToWakeUp[i].x, _pixelsToWakeUp[i].y, out Pixel pixel))
                {
                    pixel.WakeUp();
                    trySetPixelInChunks(ref pixel);
                    _pixelsToWakeUp.RemoveAt(i);
                }
            }

            return didChange;
        }

        public bool TryGetPixelAt(Vector3 pos, out Pixel pixel)
        {
            return TryGetPixelAt(pos.x, pos.y, out pixel);
        }

        public bool TryGetPixelAt(float x, float y, out Pixel pixel)
        {
            return tryGetPixelFromChunks(x, y, out pixel);
        }

        public void DrawPixelAt(Vector3 pos, PixelMaterialId materialId)
        {
            DrawPixelAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), materialId);
        }

        public void DrawPixelAt(float x, float y, PixelMaterialId materialId)
        {
            if (!LoadSucceeded)
                return;

            var levelInfo = LevelInfo.GetLoadedLevelInfo(Level);
            if (levelInfo == null || !levelInfo.HasValidMaterials())
                return;

            // Skip if the pixel at that position is already set to the desired material.
            if (TryGetPixelAt(x, y, out Pixel existingPixel) && existingPixel.materialIndex == levelInfo.Materials.GetIndex(materialId))
            {
                return;
            }

            var material = levelInfo.Materials.GetById(materialId);
            var pixel = PixelFactory.CreatePixel(Mathf.FloorToInt(x), Mathf.FloorToInt(y), isEmpty: materialId == PixelMaterialId.Empty, isLoaded: true, ref levelInfo.Materials.NativeMaterials, materialId, ref RandomNumberGenerator);
            _pixelsToDraw.Add(pixel);

            WakeUpNeighbours(x, y);
        }

        public void WakeUpNeighbours(float x, float y)
        {
            for (int nx = -1; nx <= 1 ; nx++)
            {
                for (int ny = -1; ny <= 1; ny++)
                {
                    if (nx == 0 && ny == 0)
                        continue;

                    _pixelsToWakeUp.Add(new Vector2(x + nx, y + ny));
                }
            }
        }

        public void DrawLine(PixelWorldBrushShape brushShape, int brushSize, float x0, float y0, float x1, float y1, PixelMaterialId materialId)
        {
            DrawLine(
                brushShape, brushSize,
                Mathf.FloorToInt(x0),
                Mathf.FloorToInt(y0),
                Mathf.FloorToInt(x1),
                Mathf.FloorToInt(y1),
                materialId
            );
        }

        public void DrawLine(PixelWorldBrushShape brushShape, int brushSize, int x0, int y0, int x1, int y1, PixelMaterialId materialId)
        {
            // If distance is <= 1 then draw the two pixels directly.
            if (Mathf.Abs(x0 - x1) <= 1 && Mathf.Abs(y0 - y1) <= 1)
            {
                DrawBrushAt(brushShape, brushSize, x0, y0, materialId);
                DrawBrushAt(brushShape, brushSize, x1, y1, materialId);
            }
            // If ONLY horizontal or vertical then use a simple for loop to move.
            else if (x0 == x1)
            {
                int direction = y1 > y0 ? 1 : -1;
                int steps = Mathf.Abs(y0 - y1);
                for (int step = 0; step < steps; step++)
                {
                    DrawBrushAt(brushShape, brushSize, x0, y0 + step * direction, materialId);
                }
            }
            else if(y0 == y1)
            {
                int direction = x1 > x0 ? 1 : -1;
                int steps = Mathf.Abs(x0 - x1);
                for (int step = 0; step < steps; step++)
                {
                    DrawBrushAt(brushShape, brushSize, x0 + step * direction, y0, materialId);
                }
            }
            // Move diagonally over multiple pixels.
            else
            {
                // Use Bresenham algorithm (ensure order from start to finish).

                // Needed because we will reuse x0 and y0 vars below.
                int startX = x0;
                int startY = y0;

                // Convert to primary quadrant to ensure order.
                int deltaX = Math.Abs(x1 - x0);
                int directionX = x1 == x0 ? 0 : (x1 > x0 ? 1 : -1);

                int deltaY = Math.Abs(y1 - y0);
                int directionY = y1 == y0 ? 0 : (y1 > y0 ? 1 : -1);

                x0 = 0;
                y0 = 0;
                x1 = deltaX;
                y1 = deltaY;
                // Calc is too steep to use x.
                bool steep = Mathf.Abs(y1 - y0) > Mathf.Abs(x1 - x0);
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
                int dy = Mathf.Abs(y1 - y0);
                int error = dx / 2;
                int yStep = (y0 < y1) ? 1 : -1;
                int y = y0;
                for (int x = x0; x <= x1; x++)
                {
                    // Convert back to real coordinates & quadrant.
                    int targetX = startX + (steep ? y : x) * directionX;
                    int targetY = startY + (steep ? x : y) * directionY;

                    DrawBrushAt(brushShape, brushSize, targetX, targetY, materialId);

                    // Error accumulation
                    error = error - dy;
                    if (error < 0)
                    {
                        y += yStep;
                        error += dx;
                    }
                }
            }
        }

        public void DrawBrushAt(PixelWorldBrushShape shape, int brushSize, float x, float y, PixelMaterialId materialId)
        {
            if(shape == PixelWorldBrushShape.SinglePixel)
            {
                DrawPixelAt(x, y, materialId);
                return;
            }

            var offsets = PixelWorldBrushShapeUtils.GetBrushPixelOffsets(shape, brushSize, 0, 0);
            foreach (var offset in offsets)
            {
                DrawPixelAt(x + offset.x, y + offset.y, materialId);
            }
        }
    }
}
