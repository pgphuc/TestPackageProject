using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kamgam.SandGame
{
    [Serializable]
    public partial class LevelPart
    {
        [System.NonSerialized]
        public LevelInfo Level;

        public Vector2Int Coordinates;
        public AssetReference ImageReference;

        [System.NonSerialized]
        public bool IsLoading;

        /// <summary>
        /// True only if loaded successfully.
        /// </summary>
        [System.NonSerialized]
        public bool LoadComplete;

        [System.NonSerialized]
        public bool LoadFailed;

        public void LoadImageIntoChunk(JobAwaiter jobAwaiter, PixelWorldChunk chunk, LevelInfo levelInfo, System.Action<bool> onComplete)
        {
            startLoading();
            AsyncOperationHandle<Texture2D> imageLoadHandle = ImageReference.LoadAssetAsync<Texture2D>();
            imageLoadHandle.Completed += (handle) => imageLoaded(jobAwaiter, handle, chunk, levelInfo, onComplete);
        }

        protected void imageLoaded(JobAwaiter jobAwaiter, AsyncOperationHandle<Texture2D> imageLoadHandle, PixelWorldChunk chunk, LevelInfo levelInfo, System.Action<bool> onComplete)
        {
            if (imageLoadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                copyTextureInto(jobAwaiter, imageLoadHandle.Result, chunk, levelInfo, onComplete);
            }
            else
            {
                Debug.LogError($"AssetReference level part image {chunk.X}/{chunk.Y} {ImageReference.RuntimeKey} failed to load.");
                finishLoading(success: false);
                onComplete(LoadComplete);
            }

            ImageReference.ReleaseAsset();
        }

        protected void startLoading()
        {
            IsLoading = true;
            LoadComplete = false;
            LoadFailed = false;
        }

        protected void finishLoading(bool success)
        {
            IsLoading = false;
            LoadComplete = success;
            LoadFailed = !success;
        }

        protected void copyTextureInto(JobAwaiter jobAwaiter, Texture2D texture, PixelWorldChunk chunk, LevelInfo levelInfo, System.Action<bool> onComplete)
        {
            if (texture.width  != chunk.Width ||
                texture.height != chunk.Height)
            {
                Debug.LogError($"SandGame: Stopped loading level part image {chunk.X}/{chunk.Y} ({ImageReference.RuntimeKey}) as its resolution {texture.width}x{texture.height} does not match the world chunk size {chunk.Width}x{chunk.Height}.");
                finishLoading(success: false);
                onComplete(false);
                return;
            }

            // Debug.Log("Job for: " + chunk.X + " / " + chunk.Y + " frame: " + Time.frameCount);

            // TODO: cancel level part loading if level is unloaded.
            if (levelInfo.Materials.NativeMaterials.IsCreated) // Happens after level was unloaded but an old level part is still being loaded.
            {
                var job = new LevelPartImageToChunkJob()
                {
                    // In
                    RandomSeed = JobUtils.GetRandomSeed(),
                    Materials = levelInfo.Materials.NativeMaterials,
                    TextureWidth = texture.width,
                    TexturePixelsColors = texture.GetRawTextureData<byte>(),
                    XMinInPixelWorld = chunk.XMin,
                    YMinInPixelWorld = chunk.YMin,
                    // Out
                    ChunkPixels = chunk.Pixels
                };
                jobAwaiter.AddToLateUpdate(job, job.Schedule(), (handle) => onCompletedTextureLoad(handle, onComplete));
            }
            else
            {
                finishLoading(success: false);
                onComplete(false);
            }
        }

        private void onCompletedTextureLoad(JobHandle handle, System.Action<bool> onComplete)
        {
            // Debug.Log("completed in frame: " + Time.frameCount);
            finishLoading(success: true);
            onComplete(true);
        }

        public void Unload()
        {
            if (ImageReference.IsValid())
                ImageReference.ReleaseAsset();

            IsLoading = false;
            LoadComplete = false;
            LoadFailed = false;
        }
    }
}
