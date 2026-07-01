using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Kamgam.SandGame
{
    /// <summary>
    /// A cache of already created (and/or loaded) PixelWorldChunks.<br />
    /// If you have very big levels then this may run out of memory. In that case you
    /// would have to save your level chunks into disk and reload (stream) them in
    /// on demand.
    /// <br />
    /// TODO (n2h): Add level serialization / streaming.
    /// <br /><br />
    /// NOTICE: The pixel cache is only for world chunks that have been created in memory.
    /// The actual loading of pixel data from an image is done in the LevelChunk and is
    /// triggered by LevelChunks.LoadImageIntoChunk().
    /// </summary>
    public class PixelWorldChunkCache
    {
        /// <summary>
        /// The chunks that are currently loaded in memory.
        /// </summary>
        public Dictionary<Vector2Int, PixelWorldChunk> Chunks = new Dictionary<Vector2Int, PixelWorldChunk>();

        public void Add(PixelWorldChunk chunk)
        {
            var key = new Vector2Int(chunk.X, chunk.Y);
            if (!Chunks.ContainsKey(key))
            {
                Chunks.Add(key, chunk);
            }
        }

        /// <summary>
        /// Counts all loaded chunks. Failed and completed ones.
        /// </summary>
        /// <returns></returns>
        public int CountLoadedChunks()
        {
            int n = 0;
            foreach (var kv in Chunks)
            {
                if (kv.Value != null)
                {
                    if (kv.Value.LoadFailed || kv.Value.LoadSucceeded)
                        n++;
                }
            }
            return n;
        }

        public PixelWorldChunk GetOrCreate(int x, int y, int width, int height)
        {
            PixelWorldChunk chunk;

            var key = new Vector2Int(x, y);
            Chunks.TryGetValue(key, out chunk);

            if (chunk == null)
            {
                if(Chunks.ContainsKey(key))
                    Chunks.Remove(key);

                chunk = new PixelWorldChunk();
                chunk.Initialize(x, y, width, height);
                Chunks.Add(key, chunk);
            }

            return chunk;
        }

        /// <summary>
        /// Returns or creates a chunk that contains the given pixel position.
        /// </summary>
        /// <param name="xInPixels"></param>
        /// <param name="yInPixels"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public PixelWorldChunk GetOrCreateByPixelPos(int xInPixels, int yInPixels, int width, int height)
        {
            PixelWorldChunk chunk;

            int x = Mathf.FloorToInt(xInPixels / (float)width);
            int y = Mathf.FloorToInt(yInPixels / (float)height);

            var key = new Vector2Int(x, y);
            Chunks.TryGetValue(key, out chunk);

            if (chunk == null)
            {
                if (Chunks.ContainsKey(key))
                    Chunks.Remove(key);

                chunk = new PixelWorldChunk();
                chunk.Initialize(x, y, width, height);
                Chunks.Add(key, chunk);
            }

            return chunk;
        }


        public void Clear()
        {
            foreach (var kv in Chunks)
            {
                kv.Value?.Dispose();
            }

            Chunks.Clear();
        }
    }
}
