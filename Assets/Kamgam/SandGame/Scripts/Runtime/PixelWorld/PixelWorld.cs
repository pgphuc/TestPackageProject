using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kamgam.SandGame
{
    /// <summary>
    /// The pixel world starts with about 9 active chunks that form a grid.
    /// ---------------------------------------
    /// | Top-Left    | Top    | Top-Right    |
    /// --------------+--------+---------------
    /// | Middle-Left | Middle | Middle-Right |
    /// --------------+--------+---------------
    /// | Botom-Left  | Bottom | Bottom-Right |
    /// ---------------------------------------
    /// Each chunk is about as big as one screen.
    /// 
    /// At the start the "Middle" chunk is aligned with the camera.
    /// The bottom left corner of the "Middle" chunk is the origin (x = 0, y = 0).
    /// If the camera moves to the left then then "Middle-Left" chunk will come into view.
    /// If the camera keeps moving to the left then at some point new chunks will be loaded.
    /// 
    /// At the end of each frame the parts of the chunks that are visible (viewport) are then
    /// copied into the PixelCanvas texture.
    /// </summary>
    public partial class PixelWorld
    {
        public int Level;
        public PixelCanvas Canvas;
        public PolygonCollider2D Collider;

        public int ChunkWidth { get; protected set; }
        public int ChunkHeight { get; protected set; }
        public int ViewportWidth { get; protected set; }
        public int ViewportHeight { get; protected set; }
        /// <summary>
        /// 1 Unit = # Pixels. For physics calculations it is assumed that 1 Unit = 1 Meter.
        /// </summary>
        public int PixelsPerUnit { get; protected set; }

        /// <summary>
        /// 1 pixel = # Unity units
        /// </summary>
        public float UnitsPerPixel { get => 1f / PixelsPerUnit; }

        /// <summary>
        /// How big the simulation buffer rect should be in relation to the screen size (2 = double the size, 3 = tripple ...).<br />
        /// Increasing this costs A LOT of performance (grows x squared). Don't do it unless absolutely needed.<br />
        /// If you increase it in one direction think about reducing it in the other.
        /// </summary>
        public static readonly Vector2 SimulationBufferScale = new Vector2(2.5f, 2.5f);

        /// <summary>
        /// The number of pixels on the edges of the simulation buffer rect that the simulation can read and write but does not actively simulate.<br />
        /// This also limits how far a pixel can move in one frame.
        /// </summary>
        public const int SimulationMargin = 20;

        /// <summary>
        /// The max frame rate at which the simulation will run.<br />
        /// It works similar to fixed physics updates. Meaning it may run 0, 1 or multiple times per frame.<br />
        /// If you increase the frame rate then then simulated pixels will be sped up in comparison to free falling pixels.<br />
        /// The simulation code is tweaked to give nice results at 60 fps. Do NOT change this unless you absolutely need to.
        /// </summary>
        public int FrameRate = 60;

        /// <summary>
        /// If enabled then the simulations actual simulation frame rate is capped
        /// to the real application frame rate (i.e. the simulation is executed at most once per frame).<br />
        /// This is useful to keep your simulation step rate and the actual frame rate in sync.
        /// </summary>
        public bool LimitToApplicationFrameRate = true;

        /// <summary>
        /// The total number of frames that the simulation has been run in since the last level loaded.<br />
        /// NOTICE: This is NOT the number of simulation steps (multiple steps may be executed per frame).
        /// </summary>
        public int FrameCount { get; protected set; }

        /// <summary>
        /// The total number of simulation steps that have been run since the last level loaded.
        /// </summary>
        public int SimulationStepCount { get; protected set; }

        /// <summary>
        /// Total time in sec since last level load.
        /// </summary>
        public float TimeSinceLevelLoad { get; protected set; }
        protected float _lastFrameTime = 0f;
        public float DeltaTime { get; protected set; }

        /// <summary>
        /// Total FixedTime in sec since last level load.<br />
        /// This is always <= Time.<br />
        /// NOTICE: If "PreventFrameChoking" is enabled then this may lag behind by a lot (lag will grow over time).
        /// </summary>
        public float FixedTime { get; protected set; }
        public float FixedDeltaTime { get => FrameRate <= 0 ? float.MaxValue : 1f / FrameRate; }

        public int SimulationStepsInThisFrame { get; protected set; }

        public delegate void OnPixelWorldFixedUpdateDelegate(PixelWorld pixelWorld);
        /// <summary>
        /// Called whenever a frame begins that might execute simulation steps.
        /// Use this to sync your Unity scene animations with the pixel world.
        /// </summary>
        public OnPixelWorldFixedUpdateDelegate OnPixelWorldFixedUpdate;

        protected float _viewPortMinX = 0f;
        protected float _previousViewPortMinX = 0f;
        /// <summary>
        /// The viewport min x in pixels (as float).
        /// </summary>
        public float ViewPortMinX
        {
            get => _viewPortMinX;
            set
            {
                if (!Mathf.Approximately(value, _viewPortMinX))
                {
                    _previousViewPortMinX = _viewPortMinX;
                    _viewPortMinX = value;
                }
            }
        }
        public int ViewPortMinXInt => Mathf.RoundToInt(ViewPortMinX);
        public int ViewPortMaxXInt => ViewPortMinXInt + ViewportWidth;
        public float ViewPortMinXWorld => ViewPortMinX / PixelsPerUnit;
        public float ViewPortMaxXWorld => (ViewPortMinX + ViewportWidth) / PixelsPerUnit;

        protected float _viewPortMinY = 0f;
        protected float _previousViewPortMinY = 0f;
        /// <summary>
        /// The viewport min y in pixels (as float).
        /// </summary>
        public float ViewPortMinY
        {
            get => _viewPortMinY;
            set
            {
                if (!Mathf.Approximately(value, _viewPortMinY))
                {
                    _previousViewPortMinY = _viewPortMinY;
                    _viewPortMinY = value;
                }
            }
        }
        public int ViewPortMinYInt => Mathf.RoundToInt(ViewPortMinY);
        public int ViewPortMaxYInt => ViewPortMinYInt + ViewportHeight;
        public float ViewPortMinYWorld => ViewPortMinY / PixelsPerUnit;
        public float ViewPortMaxYWorld => (ViewPortMinY + ViewportHeight) / PixelsPerUnit;

        // Simulation buffer with and height can not be bigger than 2X the chunk size.
        public int SimulationWidth { get => Mathf.Min(Mathf.FloorToInt(ViewportWidth * SimulationBufferScale.x), ChunkWidth * 2); }
        public int SimulationHeight { get => Mathf.Min(Mathf.FloorToInt(ViewportHeight * SimulationBufferScale.y), ChunkHeight * 2); }
        public int SimulationMinXInt { get => ViewPortMinXInt - ((SimulationWidth - ViewportWidth) / 2); }
        public int SimulationMinYInt { get => ViewPortMinYInt - ((SimulationHeight - ViewportHeight) / 2); }
        public int SimulationMaxXInt { get => SimulationMinXInt + SimulationWidth; }
        public int SimulationMaxYInt { get => SimulationMinYInt + SimulationHeight; }

        public bool IsLoading { get; protected set; } = false;
        public bool LoadingFinished { get; protected set; } = false;
        public bool LoadSucceeded { get; protected set; } = false;
        public bool LoadFailed { get; protected set; } = false;

        /// <summary>
        /// All chunks that have been loaded.<br />
        /// TODO: Serialize them to disk to support really big levels.
        /// </summary>
        public PixelWorldChunkCache ChunkCache = new PixelWorldChunkCache();

        /// <summary>
        /// All the currently active chunks.
        /// </summary>
        public List<PixelWorldChunk> ActiveChunks = new List<PixelWorldChunk>();

        public Unity.Mathematics.Random RandomNumberGenerator = Unity.Mathematics.Random.CreateFromIndex(JobUtils.GetRandomSeed());

        /// <summary>
        /// World temperature in degrees celcius.
        /// </summary>
        public float WorldTemperature = 20f;

        /// <summary>
        /// Air density in Kg / m³, see: https://en.wikipedia.org/wiki/Density
        /// </summary>
        public float AirDensity = 1.2f;

        public int MaxNumOfSimulationThreads = 8;
        public int MaxNumOfCopyPixelThreads = 8;

        protected Action<int> _onLoadComplete;

        // Copy pixel job handles
        protected List<JobHandle> _copyPixelsJobHandles = new List<JobHandle>();

        protected Camera _camera;
        protected float _cameraAspect;

        protected NativeArray<Pixel> _simulationBuffer;
        protected NativeArray<int> _simulationBufferColliderPixelCodes;
        protected NativeArray<int> _simulationBufferColliderPixelPaths; // pixel indices ordered by paths (outlines)
        protected NativeArray<int> _simulationBufferColliderPixelHandledTmp;
        protected List<JobHandle> _copyChunksToSimulationBufferJobHandles = new List<JobHandle>();
        protected List<JobHandle> _simulationJobHandles = new List<JobHandle>();
        protected List<JobHandle> _copySimulationBufferToChunkJobHandles = new List<JobHandle>();

        /// <summary>
        /// The method that is called to schedule one job that handles a part of a chunk.
        /// </summary>
        /// <param name="jobHandles"></param>
        /// <param name="chunkPixels">The chunk pixels in an array starting bottom left.</param>
        /// <param name="chunkWidth">This is the total chunk width, not the width of the chunk part that is handled by this thread.</param>
        /// <param name="chunkHeight">This is the total chunk height, not the height of the chunk part that is handled by this thread.</param>
        /// <param name="xMinInChunk">The x min position relative to the chunk XMin world pixel position (i.e. between 0 and chunk.Width).</param>
        /// <param name="xMaxInChunk">The x max position relative to the chunk XMax world pixel position (i.e. between 0 and chunk.Width).</param>
        /// <param name="yMinInChunk">The y min position relative to the chunk YMin world pixel position (i.e. between 0 and chunk.Height).</param>
        /// <param name="yMaxInChunk">The y max position relative to the chunk YMax world pixel position (i.e. between 0 and chunk.Height).</param>
        /// <param name="areaWidth">This is the total area width, not the width of the chunk part that is handled by this thread.</param>
        /// <param name="areaHeight">This is the total area height, not the height of the chunk part that is handled by this thread.</param>
        /// <param name="xMinInArea">The min overlap position of the thread relative to area xMin (clamped negative values since those are outside the area)</param>
        /// <param name="xMaxInArea">The max overlap position of the thread relative to area xMin (clamped values bigger than areaWidth since those are outside the area)</param>
        /// <param name="yMinInArea">The min overlap of the thread relative to area yMin (clamped negative values since those are outside the area)</param>
        /// <param name="yMaxInArea">The max overlap position of the thread relative to area yMin (clamped values bigger than areaWidth since those are outside the area)</param>
        /// <param name="xMinInWorld">The world pixel coordinates of the min x position.</param>
        /// <param name="yMinInWorld">The world pixel coordinates of the min y position.</param>
        public delegate void ScheduleChunkJobDelegate(
            List<JobHandle> jobHandles,
            NativeArray<Pixel> chunkPixels,
            int chunkWidth, int chunkHeight, int xMinInChunk, int xMaxInChunk, int yMinInChunk, int yMaxInChunk,
            int areaWidth, int areaHeight, int xMinInArea, int xMaxInArea, int yMinInArea, int yMaxInArea,
            int xMinInWorld, int yMinInWorld);

        protected ScheduleChunkJobDelegate _onScheduleCopyChunkToSimulationBufferJobDelegate;
        protected ScheduleChunkJobDelegate _onScheduleCopySimulationBufferToChunkJobDelegate;

        /// <summary>
        /// Updates the camera orthographic size to match the height in pixels.
        /// </summary>
        /// <param name="cam"></param>
        /// <param name="height"></param>
        /// <param name="pixelsPerUnit"></param>
        public static void MatchCameraSizeToHeight(Camera cam, int height, int pixelsPerUnit)
        {
            cam.orthographicSize = (float)height / pixelsPerUnit * 0.5f;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="level"></param>
        /// <param name="chunkWidth"></param>
        /// <param name="chunkHeight"></param>
        /// <param name="pixelsPerUnit">Pixels per Unity world unit.</param>
        /// <param name="cam">Orthogonal camera.</param>
        /// <param name="canvas">The pixel canvas to render the viewport into.</param>
        public void Initialize(
            int pixelsPerUnit,
            int chunkWidth, int chunkHeight,
            Camera cam, PixelCanvas canvas)
        {
            PixelsPerUnit = pixelsPerUnit;
            ChunkWidth = chunkWidth;
            ChunkHeight = chunkHeight;

            // Update the camera orthographic size to match the height in pixels.
            _camera = cam;
            if (!_camera.orthographic)
                throw new System.Exception("Sand Game: Orthographic camera required.");
            _cameraAspect = cam.aspect;

            Canvas = canvas;

            _onScheduleCopyChunkToSimulationBufferJobDelegate = onScheduleCopyChunkToSimulationBufferJob;
            _onScheduleCopySimulationBufferToChunkJobDelegate = onScheduleCopySimulationBufferToChunkJob;
        }

        public void LoadLevelAsync(int level, System.Action<int> onComplete)
        {
            Unload();

            Level = level;
            _onLoadComplete = onComplete;

            startLoading();
            initViewportCanvasAndCamera(PixelsPerUnit, ChunkHeight, Canvas, _camera);
            loadChunksForViewportPos();
        }

        protected void startLoading()
        {
            IsLoading = true;
            LoadingFinished = false;
            LoadSucceeded = false;
            LoadFailed = false;
        }

        protected void finishLoading(bool success)
        {
            IsLoading = false;
            LoadingFinished = true;
            LoadSucceeded = success;
            LoadFailed = !success;

            resetTime();

            _onLoadComplete?.Invoke(Level);

            Canvas.gameObject.SetActive(success);
        }

        protected void initViewportCanvasAndCamera(int pixelsPerUnit, int chunkHeight, PixelCanvas canvas, Camera cam)
        {
            ViewportWidth = Mathf.CeilToInt(cam.orthographicSize * 2f * pixelsPerUnit * cam.aspect);
            ViewportHeight = Mathf.CeilToInt(cam.orthographicSize * 2f * pixelsPerUnit);

            // Create the simulation buffer with enough room to store the viewport and a margin surrounding it.
            disposeSimulationBuffer();
            _simulationBuffer = new NativeArray<Pixel>(SimulationWidth * SimulationHeight, Allocator.Persistent);
            _simulationBufferColliderPixelCodes = new NativeArray<int>(_simulationBuffer.Length, Allocator.Persistent);
            // Paths in the form of indices to _simulationBufferColliderPixelCodes. Paths are delimited by -1.
            // Example of two paths: 0,3,2,5,-1, 127,130,154,-1,..., -1, -1, -1, ...
            _simulationBufferColliderPixelPaths = new NativeArray<int>(_simulationBuffer.Length, Allocator.Persistent);
            _simulationBufferColliderPixelHandledTmp = new NativeArray<int>(_simulationBuffer.Length, Allocator.Persistent);

            Canvas = canvas;
            Canvas.Reinitialize(ViewportWidth, ViewportHeight);

            MatchCameraSizeToHeight(cam, chunkHeight, pixelsPerUnit);

            Canvas.MatchCamera(Camera.main);
        }

        public void Unload()
        {
            IsLoading = false;
            LoadingFinished = false;
            LoadSucceeded = false;
            LoadFailed = false;

            ActiveChunks.Clear();
            ChunkCache.Clear();
            Canvas.Clear();

            disposeSimulationBuffer();
            LevelInfo.JobAwaiter.Clear();
            LevelInfo.Unload(Level);
            _simulationJobHandles.Clear();

            resetTime();

            _pixelsToDraw.Clear();

            Canvas.gameObject.SetActive(false);
        }

        private void resetTime()
        {
            TimeSinceLevelLoad = 0f;
            _lastFrameTime = 0f;
            DeltaTime = 0f;
            FixedTime = 0f;
            FrameCount = 0;
            SimulationStepsInThisFrame = 0;
        }

        protected void disposeSimulationBuffer()
        {
            if (_simulationBuffer.IsCreated)
                _simulationBuffer.Dispose();

            if (_simulationBufferColliderPixelCodes.IsCreated)
                _simulationBufferColliderPixelCodes.Dispose();

            if (_simulationBufferColliderPixelPaths.IsCreated)
                _simulationBufferColliderPixelPaths.Dispose();

            if (_simulationBufferColliderPixelHandledTmp.IsCreated)
                _simulationBufferColliderPixelHandledTmp.Dispose();
        }

        public void Dispose()
        {
            Unload();
        }

        protected void onAspectChanged()
        {
            initViewportCanvasAndCamera(PixelsPerUnit, ChunkHeight, Canvas, _camera);
        }

        protected void loadChunksForViewportPos()
        {
            var xMargin = Mathf.RoundToInt(ChunkWidth * 0.5f);
            var yMargin = Mathf.RoundToInt(ChunkHeight * 0.5f);

            float xMovement = ViewPortMinX - _previousViewPortMinX;
            float yMovement = ViewPortMinY - _previousViewPortMinY;

            // Unload chunks
            int chunkCount = ActiveChunks.Count;
            for (int i = chunkCount - 1; i >= 0; i--)
            {
                var chunk = ActiveChunks[i];

                // Check against simulatin buffer bounds.
                // If is outside then remove from active chunks.
                if (chunk.XMin > SimulationMaxXInt + xMargin
                    || chunk.XMax < SimulationMinXInt - xMargin
                    || chunk.XMax < SimulationMinXInt - xMargin
                    || chunk.YMin > SimulationMaxYInt + yMargin
                    || chunk.YMax < SimulationMinYInt - yMargin
                    )
                {
                    ActiveChunks.RemoveAt(i);
                }
            }

            // Find center chunk
            int simCenterX = SimulationMinXInt + Mathf.RoundToInt(SimulationWidth * 0.5f);
            int simCenterY = SimulationMinYInt + Mathf.RoundToInt(SimulationHeight * 0.5f);
            var centerChunk = ChunkCache.GetOrCreateByPixelPos(simCenterX, simCenterY, ChunkWidth, ChunkHeight);
            if (!ActiveChunks.Contains(centerChunk))
                ActiveChunks.Add(centerChunk);

            // From the center chunk calculate all needed chunks and load them if necessary.
            int chunksX = Mathf.RoundToInt((SimulationWidth + xMargin * 2) / ChunkWidth) + 1;
            int chunksXHalf = Mathf.FloorToInt(chunksX / 2f);
            int chunksY = Mathf.RoundToInt((SimulationHeight + yMargin * 2) / ChunkHeight) + 1;
            int chunksYHalf = Mathf.FloorToInt(chunksY / 2f);

            int chunksRight = chunksXHalf;
            int chunksLeft = chunksXHalf;
            if (chunksX % 2 != 0)
            {
                chunksRight = chunksXHalf;
                chunksLeft = chunksXHalf;
            }
            else
            {
                if (xMovement >= 0f)
                    chunksRight += 1;
                else
                    chunksLeft += 1;
            }

            int chunksUp = chunksYHalf;
            int chunksDown = chunksYHalf;
            if (chunksY % 2 != 0)
            {
                chunksUp = chunksYHalf;
                chunksDown = chunksYHalf;
            }
            else
            {
                if (yMovement >= 0f)
                    chunksUp += 1;
                else
                    chunksDown += 1;
            }

            for (int x = centerChunk.X - chunksLeft; x <= centerChunk.X + chunksRight; x++)
            {
                for (int y = centerChunk.Y - chunksDown; y <= centerChunk.Y + chunksUp; y++)
                {
                    var chunk = ChunkCache.GetOrCreate(x, y, ChunkWidth, ChunkHeight);
                    if (!ActiveChunks.Contains(chunk))
                        ActiveChunks.Add(chunk);
                }
            }

            // Load chunks
            chunkCount = ActiveChunks.Count;
            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = ActiveChunks[i];

                // Load if not yet loaded.
                if (!chunk.IsLoading && !chunk.LoadingFinished)
                {
                    chunk.LoadFromImage(Level, chunk.X, chunk.Y);
                }
            }
        }

        protected bool isOutOfBounds(PixelWorldChunk chunk, int xMargin, int yMargin)
        {
            return chunk.XMin > ViewPortMaxXInt + xMargin
                || chunk.XMax < ViewPortMinXInt - xMargin
                || chunk.YMin > ViewPortMaxYInt + yMargin
                || chunk.YMax < ViewPortMinYInt - yMargin;
        }

        public void LateUpdateAfterCode()
        {
            LevelInfo.JobAwaiter.LateUpdate();

            if (LoadSucceeded)
            {
                // Update Time
                TimeSinceLevelLoad += UnityEngine.Time.deltaTime;
                DeltaTime = TimeSinceLevelLoad - _lastFrameTime;
                _lastFrameTime = TimeSinceLevelLoad;
                FrameCount++;

                float fixedToCurrentDelta = TimeSinceLevelLoad - FixedTime;
                int fixedTimeSteps = Mathf.Max(0, Mathf.FloorToInt(fixedToCurrentDelta / FixedDeltaTime));
                if (LimitToApplicationFrameRate)
                {
                    // Run at a maximum of once per frame.
                    fixedTimeSteps = Mathf.Min(fixedTimeSteps, 1);
                }

                // Increase fixed time & call simulation fixed updates
                for (int i = 0; i < fixedTimeSteps; i++)
                {
                    FixedTime += FixedDeltaTime;
                    OnPixelWorldFixedUpdate?.Invoke(this);
                }

                SimulationStepsInThisFrame = fixedTimeSteps;
                SimulationStepCount += SimulationStepsInThisFrame;
            }

            if (LoadSucceeded || IsLoading)
            {
                loadChunksForViewportPos();
                LevelInfo.JobAwaiter.Update();
            }

            if (IsLoading)
            {
                // Are all initial chunks loaded?
                if (IsLoading && ChunkCache.CountLoadedChunks() >= ActiveChunks.Count)
                {
                    finishLoading(success: true);
                }
            }

            if (LoadSucceeded)
            {
                // Refresh canvas if aspect ratio changed.
                if (!Mathf.Approximately(_cameraAspect, _camera.aspect))
                {
                    _cameraAspect = _camera.aspect;
                    onAspectChanged();
                }

                // Match the canvas to the camera position.
                Canvas.MatchCamera(Camera.main);
                stabilizeCameraOnSubPixel();

                // Skip simulation if still loading or not yet loaded - OR - if frame delay is needed.
                bool didChange = false;

                if (SimulationStepsInThisFrame > 0)
                {
                    for (int i = 0; i < SimulationStepsInThisFrame; i++)
                    {
                        copyChunksToBuffer();
                        simulateInBuffer();
                        copyBufferToChunks();
                    }

                    // Reset simulation steps only if actually simulated.
                    SimulationStepsInThisFrame = 0;

                    didChange = true;
                }

                // Add pixels that have been added externally.
                didChange |= copyPixelScheduledForDrawing();

                if (didChange)
                {
                    // Update canvas
                    Canvas.CopyPixelsToColorBufferMultithreaded(this, ChunkWidth, ChunkHeight, ViewPortMinXInt, ViewPortMinYInt);
                    Canvas.SendColorBufferToGraphicsCard();

                    // Update colliders
                    updateWorldColliders(Collider);
                }
            }
        }

        /// <summary>
        /// Displaces the canvas render target by the missing fractional pixel
        /// difference between ViewPortMinX and ViewPortMinXInt.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        protected void stabilizeCameraOnSubPixel()
        {
            float dX = ViewPortMinX - ViewPortMinXInt;
            float dY = ViewPortMinY - ViewPortMinYInt;

            var dXUnits = dX / PixelsPerUnit;
            var dYUnits = dY / PixelsPerUnit;

            var pos = Canvas.Stabilizer.localPosition;
            pos.x = -dXUnits;
            pos.y = -dYUnits;
            Canvas.Stabilizer.localPosition = pos;
        }

        /// <summary>
        /// Center the camera on the given x & y world pixel positions.
        /// </summary>
        /// <param name="x">Position in pixels</param>
        /// <param name="y">Position in pixels</param>
        public void MoveCameraToPixelPos(Camera cam, float x, float y)
        {
            // Move the camera
            var pos = cam.transform.position;
            pos.x = x / PixelsPerUnit;
            pos.y = y / PixelsPerUnit;
            cam.transform.position = pos;

            MatchViewportToCameraPos(cam);
        }

        /// <summary>
        /// Center the camera on the given x & y world positions.
        /// </summary>
        /// <param name="x">Position in world units.</param>
        /// <param name="y">Position in world units.</param>
        public void MoveCameraToWorldPos(Camera cam, float x, float y)
        {
            // Move the camera
            var pos = cam.transform.position;
            pos.x = x;
            pos.y = y;
            cam.transform.position = pos;

            MatchViewportToCameraPos(cam);
        }

        /// <summary>
        /// Align the camera with the viewport.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void MatchCameraToViewportPos(Camera cam)
        {
            var pos = cam.transform.position;
            pos.x = (ViewPortMinX + ViewportWidth * 0.5f) / PixelsPerUnit;
            pos.y = (ViewPortMinY + ViewportHeight * 0.5f) / PixelsPerUnit;
            cam.transform.position = pos;
        }

        public Vector2 CalcViewPortDeltaToCameraPos(Camera cam)
        {
            var pos = cam.transform.position;

            float newViewPortMinX = pos.x * PixelsPerUnit - ViewportWidth * 0.5f;
            float newViewPortMinY = pos.y * PixelsPerUnit - ViewportHeight * 0.5f;

            return new Vector2(
                newViewPortMinX - ViewPortMinX,
                newViewPortMinY - ViewPortMinY
                );
        }

        public void MatchViewportToCameraPos(Camera cam)
        {
            var pos = cam.transform.position;
            ViewPortMinX = pos.x * PixelsPerUnit - ViewportWidth * 0.5f;
            ViewPortMinY = pos.y * PixelsPerUnit - ViewportHeight * 0.5f;
        }

        protected void copyChunksToBuffer()
        {
            // Debug
            //DebugTexture.Clear();
            //// Simulation Buffer
            //DebugTexture.DrawRect(-1, -1, SimulationWidth+1, SimulationHeight+1, Color.white);
            //// Viewport
            //DebugTexture.DrawRect(ViewPortMinXInt - SimulationMinXInt, ViewPortMinYInt - SimulationMinYInt,
            //                     (ViewPortMaxXInt - SimulationMinXInt), (ViewPortMaxYInt - SimulationMinYInt),
            //                     new Color(0f, 1f, 1f, 1f));

            ScheduleChunkJobs(
                ActiveChunks, ChunkWidth, ChunkHeight,
                SimulationMinXInt, SimulationMinYInt, SimulationWidth, SimulationHeight,
                minPixelsPerBatch: 1000,
                allowAccessToLoadingChunks: true,
                _onScheduleCopyChunkToSimulationBufferJobDelegate, // -> onScheduleCopyChunkToSourceBufferJob
                _copyChunksToSimulationBufferJobHandles);

            JobUtils.WaitForJobs(_copyChunksToSimulationBufferJobHandles);
        }

        protected void onScheduleCopyChunkToSimulationBufferJob(
            List<JobHandle> jobHandles,
            NativeArray<Pixel> chunkPixels, int chunkWidth, int chunkHeight, int xMinInChunk, int xMaxInChunk, int yMinInChunk, int yMaxInChunk,
            int areaWidth, int areaHeight, int xMinInArea, int xMaxInArea, int yMinInArea, int yMaxInArea,
            int xMinInWorld, int yMinInWorld)
        {
            // Debug
            // Thread area
            //DebugTexture.DrawRect(xMinInWorld- SimulationMinXInt+1, yMinInWorld- SimulationMinYInt+1, xMinInWorld - SimulationMinXInt - 1 + (xMaxInChunk - xMinInChunk), yMinInWorld - SimulationMinYInt - 1 + (yMaxInChunk - yMinInChunk), Color.yellow);

            var handle = new PixelWorldCopyFromChunkToBufferJob()
            {
                // In
                PixelsInChunk = chunkPixels,
                ChunkWidth = chunkWidth,
                XMinInChunk = xMinInChunk,
                XMaxInChunk = xMaxInChunk,
                YMinInChunk = yMinInChunk,
                YMaxInChunk = yMaxInChunk,

                BufferWidth = SimulationWidth,
                XMinInBuffer = xMinInArea,
                YMinInBuffer = yMinInArea,

                XMinInWorld = xMinInWorld,
                YMinInWorld = yMinInWorld,

                // Out
                Buffer = _simulationBuffer
            }.Schedule();
            jobHandles.Add(handle);
        }

        protected void simulateInBuffer()
        {
            int margin = SimulationMargin;
            int minPixelsPerBatch = 1000; // some min. pixels per thread, or else making threads is a waste.

            // Calc batch size for multithreading.
            int simWidth = SimulationWidth;
            int simHeight = SimulationHeight;
            int numOfPixels = simWidth * simHeight;
            int batchSize = numOfPixels / JobUtils.GetNumberOfThreadProcessors();
            batchSize = Mathf.Min(numOfPixels, Mathf.Max(minPixelsPerBatch, batchSize));
            int numOfThreads = Mathf.CeilToInt(numOfPixels / batchSize);
            numOfThreads = Mathf.Min(numOfThreads, MaxNumOfSimulationThreads);

            // numOfThreads = 1; // Debug

            int stepSizeX = Mathf.CeilToInt(simWidth / (float)numOfThreads);
            // Don't allow smaller step sizes than margin to avoid overlap of moving pixels.
            stepSizeX = Mathf.Max(PixelWorld.SimulationMargin, stepSizeX);
            int offset = UnityEngine.Random.Range(0, stepSizeX);
            if (numOfThreads == 1)
            {
                offset = simWidth;
                stepSizeX = simWidth;
            }

            simulationPhase(margin, offset, stepSizeX, 0, 3);
            if (numOfThreads > 1) simulationPhase(margin, offset, stepSizeX, 1, 3);
            if (numOfThreads > 2) simulationPhase(margin, offset, stepSizeX, 2, 3);
        }

        private void simulationPhase(int margin, int offset, int stepSizeX, int phase, int numOfPhases)
        {
            _simulationJobHandles.Clear();

            // Per thread min max values
            int xMin = 0;
            int xMax = 0;
            int yMin = 0; // y axis is not divided by threads, only x is.
            int yMax = SimulationHeight;

            // Split from left to right until the end (SimulationWidth) is reached.
            int threadNr = -1;
            while (xMin < SimulationWidth)
            {
                threadNr++;

                if (threadNr % numOfPhases != phase)
                    continue;

                if (threadNr == 0)
                {
                    xMin = 0;
                    xMax = Mathf.Min(offset, SimulationWidth);
                }
                else
                {
                    xMin = offset + threadNr * stepSizeX;
                    xMax = Mathf.Min(xMin + stepSizeX, SimulationWidth);
                }

                if (xMax - xMin > 0 && yMax - yMin > 0)
                {
                    // Debug.Log("From " + xMin + " -> " + xMax + ", " + yMin + "-> " + yMax + " ---- " + SimulationWidth + ", " + SimulationHeight);
                    var handle = new PixelWorldSimulationJob()
                    {
                        DebugLogEnabled = Input.GetKey(KeyCode.Space) ? 1 : 0,
                        DebugFrameCount = Time.frameCount,

                        // In
                        ThreadNr = phase,
                        RandomSeed = JobUtils.GetRandomSeed(),
                        Materials = LevelInfo.GetLoadedLevelInfo(this.Level).Materials.NativeMaterials,
                        FixedDeltaTime = FixedDeltaTime,
                        PixelsPerUnit = PixelsPerUnit,
                        HorizontalOrder = (FrameCount % 2 == 0 ? -1 : 1),
                        XMinInBuffer = xMin,
                        XMaxInBuffer = xMax,
                        YMinInBuffer = yMin,
                        YMaxInBuffer = yMax,
                        BufferWidth = SimulationWidth,
                        BufferHeight = SimulationHeight,
                        XMinInWorld = SimulationMinXInt,
                        YMinInWorld = SimulationMinYInt,
                        Margin = margin,

                        WorldTemperature = WorldTemperature,
                        AirDensity = AirDensity,

                        // In/Out
                        Buffer = _simulationBuffer
                    }.Schedule();
                    _simulationJobHandles.Add(handle);
                }
            }

            JobUtils.WaitForJobs(_simulationJobHandles);
        }

        protected void copyBufferToChunks()
        {
            ScheduleChunkJobs(
                ActiveChunks, ChunkWidth, ChunkHeight,
                SimulationMinXInt, SimulationMinYInt, SimulationWidth, SimulationHeight,
                minPixelsPerBatch: 1000,
                allowAccessToLoadingChunks: false,
                _onScheduleCopySimulationBufferToChunkJobDelegate, // -> onScheduleCopyTargetBufferToChunkJob (done to avoid delegate GC)
                _copySimulationBufferToChunkJobHandles
                );

            JobUtils.WaitForJobs(_copySimulationBufferToChunkJobHandles);
        }

        protected void onScheduleCopySimulationBufferToChunkJob(
            List<JobHandle> jobHandles,
            NativeArray<Pixel> chunkPixels, int chunkWidth, int chunkHeight, int xMinInChunk, int xMaxInChunk, int yMinInChunk, int yMaxInChunk,
            int areaWidth, int areaHeight, int xMinInArea, int xMaxInArea, int yMinInArea, int yMaxInArea,
            int xMinInWorld, int yMinInWorld)
        {
            var handle = new PixelWorldCopyFromBufferToChunkJob()
            {
                // In
                Buffer = _simulationBuffer,
                BufferWidth = SimulationWidth,
                XMinInBuffer = xMinInArea,
                YMinInBuffer = yMinInArea,
                ChunkWidth = chunkWidth,
                XMinInChunk = xMinInChunk,
                XMaxInChunk = xMaxInChunk,
                YMinInChunk = yMinInChunk,
                YMaxInChunk = yMaxInChunk,
                // Out
                PixelsInChunk = chunkPixels
            }.Schedule();
            jobHandles.Add(handle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="activeChunks"></param>
        /// <param name="chunkWidth"></param>
        /// <param name="chunkHeight"></param>
        /// <param name="areaMinX">The areas x min position in world pixels</param>
        /// <param name="areaMinY">The areas y min position in world pixels</param>
        /// <param name="areaWidth">The areas width in world pixels</param>
        /// <param name="areaHeight">The areas height in world pixels</param>
        /// <param name="minPixelsPerBatch"></param>
        /// <param name="onScheduleJob"></param>
        /// <param name="jobHandles"></param>
        public void ScheduleChunkJobs(
            List<PixelWorldChunk> activeChunks, int chunkWidth, int chunkHeight,
            int areaMinX, int areaMinY, int areaWidth, int areaHeight,
            int minPixelsPerBatch, bool allowAccessToLoadingChunks,
            ScheduleChunkJobDelegate onScheduleJob,
            List<JobHandle> jobHandles)
        {
            int areaMaxX = areaMinX + areaWidth;
            int areaMaxY = areaMinY + areaHeight;

            jobHandles.Clear();

            // Calc batch size for multithreading.
            int numOfPixels = areaWidth * areaHeight;
            int batchSize = numOfPixels / JobUtils.GetNumberOfThreadProcessors();
            batchSize = Mathf.Min(numOfPixels, Mathf.Max(minPixelsPerBatch, batchSize)); // some min. pixels per thread, or else making threads is a waste.

            // Find all chunks that overlap the viewport and schedule jobs for them
            foreach (var chunk in activeChunks)
            {
                // Debug
                //DebugTexture.DrawRect(chunk.XMin - SimulationMinXInt, chunk.YMin - SimulationMinYInt, chunk.XMin - SimulationMinXInt + chunk.Width, chunk.YMin - SimulationMinYInt + chunk.Height, new Color(1f, 0,0, 0.3f));

                // Only copy from chunks that have been loaded (successfully or not).
                // Otherwise we would have two jobs trying to access the same native array
                // because the loading job may still be writing into it.
                if (!chunk.LoadingFinished && !allowAccessToLoadingChunks)
                {
                    continue;
                }

                // Is this chunk overlapping the area?
                if (areaMinX >= chunk.XMax
                    || areaMaxX <= chunk.XMin
                    || areaMinY >= chunk.YMax
                    || areaMaxY <= chunk.YMin)
                {
                    continue;
                }

                // Debug
                //DebugDrawer.DrawRect(chunk.XMin - SimulationMinXInt, chunk.YMin - SimulationMinYInt, chunk.XMin - SimulationMinXInt + chunk.Width, chunk.YMin - SimulationMinYInt + chunk.Height, Color.green);

                // What part of the chunk needs to be copied?

                // Get positions relative inside chunk.
                int xMinInChunk = Mathf.Clamp(areaMinX - chunk.XMin, 0, chunk.Width);
                int yMinInChunk = Mathf.Clamp(areaMinY - chunk.YMin, 0, chunk.Height);
                int xMaxInChunk = Mathf.Clamp(areaMaxX - chunk.XMin, 0, chunk.Width);
                int yMaxInChunk = Mathf.Clamp(areaMaxY - chunk.YMin, 0, chunk.Height);

                // Get positions relative inside area.
                int xMinInArea = Mathf.Clamp(chunk.XMin - areaMinX, 0, areaWidth);
                int yMinInArea = Mathf.Clamp(chunk.YMin - areaMinY, 0, areaHeight);
                int xMaxInArea = Mathf.Clamp(chunk.XMax - areaMinX, 0, areaWidth);
                int yMaxInArea = Mathf.Clamp(chunk.YMax - areaMinY, 0, areaHeight);

                int xMinInWorld = chunk.XMin + xMinInChunk;
                int yMinInWorld = chunk.YMin + yMinInChunk;

                // Calc num of threads
                int threadsForChunk = Mathf.Max(1, (xMaxInChunk - xMinInChunk) * (yMaxInChunk - yMinInChunk) / batchSize);

                // Slice the pixels of each chunk into N parts (N = threadsForChunk) and schedule a job for each slice.
                // Each slices is full width (x direction) but only a part in the y direction.
                int sliceHeight = Mathf.CeilToInt((yMaxInChunk - yMinInChunk) / (float)threadsForChunk);
                for (int i = 0; i < threadsForChunk; i++)
                {
                    // Prepare coordinates for the thread (only part of the chunk is handled by each thread).
                    int yMinInChunkThread = yMinInChunk + i * sliceHeight;
                    int yMaxInChunkThread = Mathf.Min(yMinInChunkThread + sliceHeight, yMaxInChunk);
                    int yMinInAreaThread = yMinInArea + i * sliceHeight;
                    int yMaxInAreaThread = Mathf.Min(yMinInAreaThread + sliceHeight, yMaxInArea);

                    if (onScheduleJob != null)
                    {
                        onScheduleJob.Invoke(
                            jobHandles,
                            chunk.Pixels,
                            chunkWidth: chunkWidth,
                            chunkHeight: chunkHeight,
                            xMinInChunk,
                            xMaxInChunk,
                            yMinInChunk: yMinInChunkThread,
                            yMaxInChunk: yMaxInChunkThread,
                            areaWidth,
                            areaHeight,
                            xMinInArea,
                            xMaxInArea,
                            yMinInArea: yMinInAreaThread,
                            yMaxInArea: yMaxInAreaThread,
                            xMinInWorld,
                            yMinInWorld + (yMinInChunkThread - yMinInChunk)
                            );
                    }
                }
            }

            // Start executing jobs now.
            // Notice: we do not wait for completion here (see waitForJobs()).
            JobHandle.ScheduleBatchedJobs();
        }

        public Vector3 WorldToPixelPos(Vector3 worldPos)
        {
            return new Vector3(
                worldPos.x * PixelsPerUnit,
                worldPos.y * PixelsPerUnit,
                worldPos.z * PixelsPerUnit
            );
        }

        public Vector3 PixelToWorldPos(int x, int y)
        {
            return new Vector3(
                x / PixelsPerUnit,
                y / PixelsPerUnit,
                0
            );
        }

        public Vector3 PixelToWorldPos(float x, float y)
        {
            return new Vector3(
                x / PixelsPerUnit,
                y / PixelsPerUnit,
                0
            );
        }

        public Vector3 PixelToWorldPos(Vector3 pixelPos)
        {
            return new Vector3(
                pixelPos.x / PixelsPerUnit,
                pixelPos.y / PixelsPerUnit,
                pixelPos.z / PixelsPerUnit
            );
        }

        public Vector3 PixelToTransformPos(int x, int y, Transform transform)
        {
            // Debug.Log($"pixel x {x} y {y}");
            var worldPos = PixelToWorldPos(new Vector3(x, y, 0));
            return transform.InverseTransformPoint(worldPos);
        }

        public Vector3 PixelToTransformPos(float x, float y, Transform transform)
        {
            var worldPos = PixelToWorldPos(new Vector3(x, y, 0));
            return transform.InverseTransformPoint(worldPos);
        }

        public Vector3 PixelToTransformPos(Vector3 pixelPos, Transform transform)
        {
            var worldPos = PixelToWorldPos(pixelPos);
            return transform.InverseTransformPoint(worldPos);
        }

        public Vector3 TransformToPixelPos(Vector3 transformPos, Transform transform)
        {
            return WorldToPixelPos(transform.TransformPoint(transformPos));
        }

        public Vector3 MouseToPixelPos(Vector3 mousePos)
        {
            return ScreenToPixelPos(mousePos);
        }

        public Vector3 ScreenToPixelPos(Vector3 screenPos, Camera cam = null)
        {
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            if(cam != null)
            {
                screenWidth = cam.pixelWidth;
                screenHeight = cam.pixelHeight;
            }

            return new Vector3(
                ViewPortMinX + ViewportWidth * (screenPos.x / screenWidth),
                ViewPortMinY + ViewportHeight * (screenPos.y / screenHeight),
                0f
            );
        }

        public Vector3 PixelToMousePos(int x, int y)
        {
            return PixelToScreenPos(new Vector3(x, y, 0));
        }

        public Vector3 PixelToMousePos(float x, float y)
        {
            return PixelToScreenPos(new Vector3(x, y, 0));
        }

        public Vector3 PixelToMousePos(Vector3 pixelPos)
        {
            return PixelToScreenPos(pixelPos);
        }

        public Vector3 PixelToScreenPos(int x, int y)
        {
            return PixelToScreenPos(new Vector3(x, y, 0));
        }

        public Vector3 PixelToScreenPos(float x, float y)
        {
            return PixelToScreenPos(new Vector3(x, y, 0));
        }

        public Vector3 PixelToScreenPos(Vector3 pixelPos, Camera cam = null)
        {
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            if (cam != null)
            {
                screenWidth = cam.pixelWidth;
                screenHeight = cam.pixelHeight;
            }

            return new Vector3(
                ((pixelPos.x - ViewPortMinX) / ViewportWidth) * screenWidth,
                ((pixelPos.y - ViewPortMinY) / ViewportHeight) * screenHeight,
                0f
            );
        }

        public Vector3 ViewportToPixelPos(Vector3 viewportPos, Camera camera)
        {
            return WorldToPixelPos(camera.ViewportToWorldPoint(viewportPos));
        }

        public Vector3 PixelToScreenPos(int x, int y, Camera camera)
        {
            return PixelToViewportPos(new Vector3(x, y, 0), camera);
        }

        public Vector3 PixelToScreenPos(float x, float y, Camera camera)
        {
            return PixelToViewportPos(new Vector3(x, y, 0), camera);
        }

        public Vector3 PixelToViewportPos(Vector3 pixelPos, Camera camera)
        {
            return camera.WorldToViewportPoint(PixelToWorldPos(pixelPos));
        }
    }
}
