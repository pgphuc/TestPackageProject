using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Kamgam.SandGame
{
    /// <summary>
    /// Pixel behaviours are shortcuts for how a pixel material will behave (logic).
    /// Pixel properties are stored either on the pixel itself or in PixelMaterials (data).
    /// </summary>
    public enum PixelBehaviour
    {
        // By default all behaviours that are not set have a value of 0. This exists only to formalize it.
        None = 0,

        /// <summary>
        /// Static pixels do not move or interact in any way. They are also not simulated at all.
        /// </summary>
        Static = 1,

        /// <summary>
        /// Solids do not move or fall under gravity (as opposed to movable pixels like liquids or sand).
        /// </summary>
        Solid = 2,

        // Movement types:
        MoveLikeSand = 11,
        MoveLikeLiquid = 12,
        MoveLikeGas = 13,

        /// <summary>
        /// Means this pixel can be combined with another non-empty pixel (example: acid).
        /// </summary>
        CombineByCorrosion = 30,

        /// <summary>
        /// Means this pixel will transmit heat energy to its neighbours.
        /// </summary>
        HeatTransfer = 40
    }
}
