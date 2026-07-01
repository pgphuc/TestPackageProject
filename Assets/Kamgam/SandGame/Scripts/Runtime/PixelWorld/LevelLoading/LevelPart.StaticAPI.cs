using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kamgam.SandGame
{
    public partial class LevelPart
    {
        public static string GetLevelInfoAddress(int level, int chunkX, int chunkY)
        {
            return $"SandGame/Level{level}_{chunkX}_{chunkY}.asset";
        }

        public static string GetLevelPartImageFilename(int level, int chunkX, int chunkY)
        {
            return $"{chunkX}_{chunkY}.png";
        }
    }
}
