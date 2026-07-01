using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Kamgam.SandGame
{
    /// <summary>
    /// TODO: This class is meant as a basis for object in Unity scene that are to be converted to
    /// pixel representations before simulation. This is not yet implemented. However, each pixel already
    /// has a dynamicObjectId and a U and V property to support this in the future.
    /// </summary>
    public class DynamicObject : MonoBehaviour
    {
        public const byte UndefinedDynamicId = 0;

        public int DynamicId = UndefinedDynamicId;
    }
}
