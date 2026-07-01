using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Kamgam.SandGame
{
    public enum PixelWorldBrushShape
    {
        SinglePixel,
        Quad,
        Circle
    }

    public static class PixelWorldBrushShapeUtils
    {
        class Brush
        {
            public PixelWorldBrushShape Shape;
            public int Size;
            public int InitialOffsetX;
            public int InitialOffsetY;
            public List<Vector2Int> Offsets;

            public Brush(PixelWorldBrushShape shape, int size, int initialOffsetX, int initialOffsetY, List<Vector2Int> offsets)
            {
                Shape = shape;
                Size = size;
                InitialOffsetX = initialOffsetX;
                InitialOffsetY = initialOffsetY;
                Offsets = offsets;
            }
        }

        private static List<Brush> _cachedBrushes = new List<Brush>();

        static Brush getBrush(PixelWorldBrushShape shape, int size = 1, int initialOffsetX = 0, int initialOffsetY = 0)
        {
            int count = _cachedBrushes.Count;
            for (int i = 0; i < count; i++)
            {
                if (   _cachedBrushes[i].Shape == shape
                    && _cachedBrushes[i].Size == size 
                    && _cachedBrushes[i].InitialOffsetX == initialOffsetX
                    && _cachedBrushes[i].InitialOffsetY == initialOffsetY)
                {
                    return _cachedBrushes[i];
                }
            }

            return null;
        }

        public static IList<Vector2Int> GetBrushPixelOffsets(PixelWorldBrushShape shape, int size = 1, int initialOffsetX = 0, int initialOffsetY = 0)
        {
            var brush = getBrush(shape, size, initialOffsetX, initialOffsetY);

            if (brush == null)
            {
                var offsets = new List<Vector2Int>();

                if (shape == PixelWorldBrushShape.SinglePixel)
                {
                    offsets.Add(new Vector2Int(initialOffsetX, initialOffsetY));
                }
                else if (shape == PixelWorldBrushShape.Quad || (shape == PixelWorldBrushShape.Circle && size < 3))
                {
                    int startValue = size / 2;
                    initialOffsetX -= startValue;
                    initialOffsetY -= startValue;
                    for (int x = 0; x < size; x++)
                    {
                        for (int y = 0; y < size; y++)
                        {
                            offsets.Add(new Vector2Int(initialOffsetX + x, initialOffsetY + y));
                        }
                    }
                }
                else if (shape == PixelWorldBrushShape.Circle)
                { 
                    int centerOffset = size / 2;
                    float sqrRadius = centerOffset * centerOffset;
                    for (int x = 0; x < size; x++)
                    {
                        for (int y = 0; y < size; y++)
                        {
                            int tx = x - centerOffset;
                            int ty = y - centerOffset;
                            if (tx * tx + ty * ty < sqrRadius)
                                offsets.Add(new Vector2Int(initialOffsetX + x - centerOffset, initialOffsetY + y - centerOffset));
                        }
                    }
                }

                brush = new Brush(shape, size, initialOffsetX, initialOffsetY, offsets);
                _cachedBrushes.Add(brush);
            }

            return brush.Offsets;
        }
    }
}
