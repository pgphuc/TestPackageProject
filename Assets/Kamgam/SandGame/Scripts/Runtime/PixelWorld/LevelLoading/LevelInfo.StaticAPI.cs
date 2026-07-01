using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kamgam.SandGame
{
    public partial class LevelInfo
    {
        /// <summary>
        /// Loaded level infos.
        /// </summary>
        static List<LevelInfo> _levelInfos = new List<LevelInfo>();

        static int _isLoadingLevelInfosFor = -1;

        class QueuedLevelPartLoadOperation
        {
            public int levelNr;
            public int x;
            public int y;
            public PixelWorldChunk pixelChunk;
            public System.Action<bool> onComplete;

            public QueuedLevelPartLoadOperation(int levelNr, int x, int y, PixelWorldChunk pixelChunk, Action<bool> onComplete)
            {
                this.levelNr = levelNr;
                this.x = x;
                this.y = y;
                this.pixelChunk = pixelChunk;
                this.onComplete = onComplete;
            }
        }

        // Queue if multiple level parts are waiting to be loaded.
        // This is necessary if multiple level parts are requested
        // for load while the level info data is still being loaded.
        // Level parts can only be loaded once the level info exists.
        static List<QueuedLevelPartLoadOperation> _queuedLevelPartLoadOperations = new List<QueuedLevelPartLoadOperation>();

        public static JobAwaiter JobAwaiter = new JobAwaiter();

        public static string GetAddressablePath(int level)
        {
            return $"SandGame/Level{level}.asset";
        }

        /// <summary>
        /// Returns null if the level info has not yet been loaded.
        /// </summary>
        /// <param name="levelNr"></param>
        /// <returns></returns>
        public static LevelInfo GetLoadedLevelInfo(int levelNr)
        {
            var info = _levelInfos.FirstOrDefault(l => l.Level == levelNr);

            // Copy material into native array if necessary.
            if (info != null)
                info.CreateNativeMaterialsIfNeeded();

            return info;
        }

        public static void LoadImageIntoChunk(int levelNr, int x, int y, PixelWorldChunk pixelChunk, System.Action<bool> onComplete)
        {
            // Before we can load the image we first need to load the level metadata.
            var level = GetLoadedLevelInfo(levelNr);
            
            if (level == null)
            {
                if (_isLoadingLevelInfosFor < 0)
                {
                    // Load level data (chunks meta data) before we can load the chunk.
                    _isLoadingLevelInfosFor = levelNr;
                    string path = GetAddressablePath(levelNr);
                    Addressables.LoadResourceLocationsAsync(path).Completed += (loc) =>
                    {
                        if (loc.Result.Count > 0)
                        {
                            var handle = Addressables.LoadAssetAsync<LevelInfo>(loc.Result[0]);
                            handle.Completed += (h) =>
                            {
                                // Once the meta info has finished loading load the current level part.
                                onLevelMetadataLoaded(h, x, y, pixelChunk, onComplete);

                                // And also start loading all queue level parts.
                                if (h.Status == AsyncOperationStatus.Succeeded)
                                {
                                    foreach (var loadOp in _queuedLevelPartLoadOperations)
                                    {
                                        loadLevelChunk(h.Result, loadOp.x, loadOp.y, loadOp.pixelChunk, loadOp.onComplete);
                                    }
                                }
                                _queuedLevelPartLoadOperations.Clear();
                            };
                        }
                        else
                        {
                            onComplete(false);
                            Debug.LogErrorFormat("LevelInfo {0} not found in Addressable System!", path);
                        }
                        _isLoadingLevelInfosFor = -1;
                    };
                }
                else
                {
                    // We are waiting for level infos to be loaded.
                    // Queue the level chunk load.
                    _queuedLevelPartLoadOperations.Add(new QueuedLevelPartLoadOperation(levelNr, x, y, pixelChunk, onComplete));
                }
            }
            else
            {
                loadLevelChunk(level, x, y, pixelChunk, onComplete);
            }
        }

        public static void Unload(int levelNr)
        {
            var levelInfo = GetLoadedLevelInfo(levelNr);
            if (levelInfo != null)
            {
                levelInfo.Unload();
            }

            _isLoadingLevelInfosFor = -1;
            _queuedLevelPartLoadOperations.Clear();
        }

        static void onLevelMetadataLoaded(AsyncOperationHandle<LevelInfo> handle, int x, int y, PixelWorldChunk pixelChunk, System.Action<bool> onComplete)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var levelInfo = handle.Result;
                levelInfo.CreateNativeMaterialsIfNeeded();
                _levelInfos.Add(levelInfo);

                loadLevelChunk(levelInfo, x, y, pixelChunk, onComplete);
            }
            else
            {
                onComplete(false);
            }
        }

        static void loadLevelChunk(LevelInfo levelInfo, int x, int y, PixelWorldChunk pixelChunk, System.Action<bool> onComplete)
        {
            var part = levelInfo.GetPart(x, y);
            if (part == null)
            {
                // No image for these chunk coordinates.
                onComplete(false);
            }
            else
            {
                if (part.LoadFailed)
                {
                    // Load already failed, don't repeat.
                    onComplete(false);
                }
                else
                {
                    // Startloading only if not yet loaded (or currently loading).
                    if (!part.LoadComplete && !part.IsLoading)
                    {
                        part.LoadImageIntoChunk(JobAwaiter, pixelChunk, levelInfo, onComplete);
                    }
                }
            }
        }
    }
}
