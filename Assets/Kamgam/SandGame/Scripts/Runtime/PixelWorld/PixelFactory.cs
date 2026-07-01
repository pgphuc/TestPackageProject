using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Kamgam.SandGame
{
    [BurstCompile]
    public static class PixelFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pixel CreateEmpty(int x, int y, bool loaded)
        {
            Pixel pixel = new();

            pixel.hasValue = 0;
            pixel.isLoaded = (byte)(loaded ? 1 : 0);
            pixel.x = x;
            pixel.y = y;
            pixel.r = 0;
            pixel.g = 0;
            pixel.b = 0;
            pixel.a = 0;

            pixel.materialIndex = 0;

            return pixel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pixel CreatePixel(int x, int y, bool isEmpty, bool isLoaded, ref NativeArray<PixelMaterial> materials, PixelMaterialId materialId, ref Unity.Mathematics.Random rnd)
        {
            int materialIndex = -1;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i].id == materialId)
                {
                    materialIndex = i;
                }
            }

            // Fall back on material 0 if the material was not found.
            materialIndex = math.max(0, materialIndex);

            PixelMaterial material = materials[materialIndex];
            return CreatePixel(x, y, material.colorInImage.r, material.colorInImage.g, material.colorInImage.b, material.colorInImage.a, isEmpty, isLoaded, ref materials, materialIndex, ref rnd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pixel CreatePixel(int x, int y, byte r, byte g, byte b, byte a, bool isEmpty, bool isLoaded, ref NativeArray<PixelMaterial> materials, PixelMaterialId materialId, ref Unity.Mathematics.Random rnd)
        {
            int materialIndex = -1;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i].id == materialId)
                {
                    materialIndex = i;
                }
            }

            return CreatePixel(x, y, r, g, b, a, isEmpty, isLoaded, ref materials, materialIndex, ref rnd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pixel CreatePixel(int x, int y, byte r, byte g, byte b, byte a, bool isEmpty, bool isLoaded, ref NativeArray<PixelMaterial> materials, int materialIndex, ref Unity.Mathematics.Random rnd)
        {
            Pixel pixel = new();

            pixel.x = x;
            pixel.y = y;
            pixel.r = r;
            pixel.g = g;
            pixel.b = b;
            pixel.a = a;
            pixel.hasValue = (byte)(isEmpty ? 0 : 1);
            pixel.isLoaded = (byte)(isLoaded ? 1 : 0);

            // Fall back on material 0 if the material was not found.
            pixel.materialIndex = math.max(0, materialIndex);

            PixelMaterial material = materials[pixel.materialIndex];
            material.ApplyPropertiesToPixel(ref pixel);
            material.SetPixelColors(ref pixel, ref rnd);

            if (material.IsAffectedByGravity())
                pixel.isFreeFalling = 1;

            return pixel;
        }
    }
}
