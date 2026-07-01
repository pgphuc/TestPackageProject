using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Kamgam.SandGame
{
    public partial class PixelWorld
    {
        protected List<Vector2> _tmpColliderPathPoints = new List<Vector2>();

        protected void updateWorldColliders(PolygonCollider2D collider)
        {
            // Disable so the collider does not do the expensive post-update actions after every change.
            collider.enabled = false;
            collider.pathCount = 0;

            var info = LevelInfo.GetLoadedLevelInfo(Level);
            if (info != null)
            {
                var job = new PixelWorldColliderJob()
                {
                    Buffer = _simulationBuffer,
                    BufferWidth = SimulationWidth,
                    BufferHeight = SimulationHeight,
                    Materials = info.Materials.NativeMaterials,
                    Codes = _simulationBufferColliderPixelCodes,
                    Paths = _simulationBufferColliderPixelPaths,
                    HandledTmp = _simulationBufferColliderPixelHandledTmp
                }.Schedule();
                job.Complete();
            }

            // Convert paths to actual paths in the polygon collider.
            int prevIndex;
            int index;
            int nextIndex;
            int prevCode;
            int code;
            int nextCode;
            Pixel pixel;
            Vector3 posInCollider;
            Vector2? prevVertexPos = null;
            int count = _simulationBufferColliderPixelPaths.Length;
            _tmpColliderPathPoints.Clear();
            for (int i = 0; i < count; i++)
            {
                prevIndex = (i == 0) ? -1 : _simulationBufferColliderPixelPaths[i - 1];
                index = _simulationBufferColliderPixelPaths[i];
                nextIndex = (i == count-1) ? -1 : _simulationBufferColliderPixelPaths[i + 1];

                // Stop after paths (end of the array is usually a bunch of -1s).
                if (index == -1 && nextIndex == -1)
                    break;

                prevCode = prevIndex < 0 ? -1 : _simulationBufferColliderPixelCodes[prevIndex];
                code = index < 0 ? -1 : _simulationBufferColliderPixelCodes[index];
                nextCode = nextIndex < 0 ? -1 : _simulationBufferColliderPixelCodes[nextIndex];

                // Skip "empty" path entry at the end
                if (index == -1)
                    continue;

                pixel = _simulationBuffer[index];
                posInCollider = PixelToTransformPos(pixel.x, pixel.y, Collider.transform);

                if (prevIndex == -1)
                    prevVertexPos = null;

                addPointsToCollider(
                    _tmpColliderPathPoints, _simulationBufferColliderPixelHandledTmp[index],
                    index, posInCollider, ref prevVertexPos, prevCode, code, nextCode, collider,
                    collapsSlopes: true);
                _simulationBufferColliderPixelHandledTmp[index]++;

                // Path end
                if (nextIndex == -1)
                {
                    // Clean up path points that are on a straight line in x or y.
                    if (_tmpColliderPathPoints.Count >= 3)
                    {
                        for (int p = _tmpColliderPathPoints.Count - 3; p >= 0; p--)
                        {
                            if (
                                (_tmpColliderPathPoints[p].x == _tmpColliderPathPoints[p + 1].x
                                && _tmpColliderPathPoints[p + 1].x == _tmpColliderPathPoints[p + 2].x)
                                ||
                                (_tmpColliderPathPoints[p].y == _tmpColliderPathPoints[p + 1].y
                                && _tmpColliderPathPoints[p + 1].y == _tmpColliderPathPoints[p + 2].y)
                                )
                            {
                                _tmpColliderPathPoints.RemoveAt(p + 1);
                            }
                        }
                    }

                    collider.pathCount++;
                    collider.SetPath(collider.pathCount - 1, _tmpColliderPathPoints);
                    _tmpColliderPathPoints.Clear();
                }
            }

            // Enable collider again.
            collider.enabled = true;
        }

        protected void addPointsToCollider(
            List<Vector2> points, int handled, int index, Vector2 pos,
            ref Vector2? prevVertexPos, int prevCode, 
            int code, int nextCode, PolygonCollider2D collider,
            bool collapsSlopes)
        {
            // Noice: The algorithm starts shapes from bottom left. The prevVertexPos.HasValue depends on this.

            switch (code)
            {
                case 1:
                    if (!prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x, pos.y) - prevVertexPos.Value) < 0.001f)
                    {
                        // CCW
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x, pos.y + UnitsPerPixel);
                    }
                    else
                    {
                        // CW
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        prevVertexPos = new Vector2(pos.x, pos.y);
                    }
                    break;

                case 2:
                    if (prevVertexPos.HasValue && Vector2.SqrMagnitude(new Vector2(pos.x + UnitsPerPixel, pos.y) - prevVertexPos.Value) < 0.001f)
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x, pos.y);
                    }
                    else
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y);
                    }
                    break;

                case 3:
                    if (!prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x + UnitsPerPixel, pos.y) - prevVertexPos.Value) < 0.001f)
                    {
                        if (collapsSlopes && prevCode == 3)
                        {
                            _tmpColliderPathPoints[_tmpColliderPathPoints.Count - 1] = new Vector2(pos.x, pos.y + UnitsPerPixel);
                        }
                        else
                        {
                            if (!collapsSlopes)
                                _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                            _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        }
                        prevVertexPos = new Vector2(pos.x, pos.y + UnitsPerPixel);
                    }
                    else
                    {
                        if (collapsSlopes && prevCode == 3)
                        {
                            _tmpColliderPathPoints[_tmpColliderPathPoints.Count - 1] = new Vector2(pos.x + UnitsPerPixel, pos.y);
                        }
                        else
                        {
                            if (!collapsSlopes)
                                _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                            _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        }
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y);
                    }
                    break;

                case 4:
                    if (!prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel) - prevVertexPos.Value) < 0.001f)
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y);
                    }
                    else
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel);
                    }
                    break;

                case 5:
                    bool orderCCW = !prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x, pos.y) - prevVertexPos.Value) < 0.001f;
                    if (orderCCW)
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y);
                    }
                    else
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x, pos.y + UnitsPerPixel);
                    }
                    break;

                case 6:
                    if (prevVertexPos.HasValue && Vector2.SqrMagnitude(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel) - prevVertexPos.Value) < 0.001f)
                    {
                        if (collapsSlopes && prevCode == 6)
                        {
                            _tmpColliderPathPoints[_tmpColliderPathPoints.Count - 1] = new Vector2(pos.x, pos.y);
                        }
                        else
                        {
                            if (!collapsSlopes)
                                _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                            _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        }
                        prevVertexPos = new Vector2(pos.x, pos.y);
                    }
                    else
                    {
                        if (collapsSlopes && prevCode == 6)
                        {
                            _tmpColliderPathPoints[_tmpColliderPathPoints.Count - 1] = new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel);
                        }
                        else
                        {
                            if (!collapsSlopes)
                                _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                            _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        }
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel);
                    }
                    break;

                case 7:
                    if (prevVertexPos.HasValue && Vector2.SqrMagnitude(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel) - prevVertexPos.Value) < 0.001f)
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x, pos.y + UnitsPerPixel);
                    }
                    else
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel);
                    }
                    break;

                case 8:
                    if (!prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x, pos.y + UnitsPerPixel) - prevVertexPos.Value) < 0.001f)
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel);
                    }
                    else
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        prevVertexPos = new Vector2(pos.x, pos.y + UnitsPerPixel);
                    }
                    break;

                case 9:
                    if (!prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x, pos.y) - prevVertexPos.Value) < 0.001f)
                    {
                        if (collapsSlopes && prevCode == 9)
                        {
                            _tmpColliderPathPoints[_tmpColliderPathPoints.Count - 1] = new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel);
                        }
                        else
                        {
                            if (!collapsSlopes)
                                _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                            _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        }
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel);
                    }
                    else
                    {
                        if (collapsSlopes && prevCode == 9)
                        {
                            _tmpColliderPathPoints[_tmpColliderPathPoints.Count - 1] = new Vector2(pos.x, pos.y);
                        }
                        else
                        {
                            if (!collapsSlopes)
                            _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                            _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        }
                        prevVertexPos = new Vector2(pos.x, pos.y);
                    }
                    break;

                case 10:
                    orderCCW = !prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x + UnitsPerPixel, pos.y) - prevVertexPos.Value) < 0.001f;

                    //if (handled > 0)
                    //    orderCCW = !orderCCW;

                    if (orderCCW)
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel);
                    }
                    else
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x, pos.y);
                    }
                    break;

                case 11:
                    if (prevVertexPos.HasValue && Vector2.SqrMagnitude(new Vector2(pos.x + UnitsPerPixel, pos.y) - prevVertexPos.Value) < 0.001f)
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel);
                    }
                    else
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y);
                    }
                    break;

                case 12:
                    if (!prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x, pos.y + UnitsPerPixel) - prevVertexPos.Value) < 0.001f)
                    {
                        if (collapsSlopes && prevCode == 12)
                        {
                            _tmpColliderPathPoints[_tmpColliderPathPoints.Count - 1] = new Vector2(pos.x + UnitsPerPixel, pos.y);
                        }
                        else
                        {
                            if (!collapsSlopes)
                                _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                            _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        }
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y);
                    }
                    else
                    {
                        if (collapsSlopes && prevCode == 12)
                        {
                            _tmpColliderPathPoints[_tmpColliderPathPoints.Count - 1] = new Vector2(pos.x, pos.y + UnitsPerPixel);
                        }
                        else
                        {
                            if (!collapsSlopes)
                                _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                            _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        }
                        prevVertexPos = new Vector2(pos.x, pos.y + UnitsPerPixel);
                    }
                    break;

                case 13:
                    if (!prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x, pos.y) - prevVertexPos.Value) < 0.001f)
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        prevVertexPos = new Vector2(pos.x + UnitsPerPixel, pos.y);
                    }
                    else
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                        prevVertexPos = new Vector2(pos.x, pos.y);
                    }
                    break;

                case 14:
                    if (!prevVertexPos.HasValue || Vector2.SqrMagnitude(new Vector2(pos.x, pos.y + UnitsPerPixel) - prevVertexPos.Value) < 0.001f)
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                        prevVertexPos = new Vector2(pos.x, pos.y);
                    }
                    else
                    {
                        _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                        prevVertexPos = new Vector2(pos.x, pos.y + UnitsPerPixel);
                    }
                    break;

                case 16:
                    _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y));
                    _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y));
                    _tmpColliderPathPoints.Add(new Vector2(pos.x + UnitsPerPixel, pos.y + UnitsPerPixel));
                    _tmpColliderPathPoints.Add(new Vector2(pos.x, pos.y + UnitsPerPixel));
                    break;

                case 0:
                case 15:
                default:
                    break;
            }
        }
    }
}
