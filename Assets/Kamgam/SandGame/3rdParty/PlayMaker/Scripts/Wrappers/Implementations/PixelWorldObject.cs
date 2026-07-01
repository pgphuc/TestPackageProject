#if PLAYMAKER
using HutongGames.PlayMaker;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kamgam.SandGame.PlayMaker
{
    /// <summary>
    /// A wrapper for a Pixel World<br />
    /// <br />
    /// Since Playmaker variables can not store arbitrary types we have to wrap PixelWorlds
    /// in a UnityEngie.Object, see: https://forum.unity.com/threads/playmaker-visual-scripting-for-unity.72349/page-70#post-9271821
    /// </summary>
    public class PixelWorldObject : ScriptableObject, IEquatable<PixelWorldObject>
    {
        protected PixelWorld _pixelWorld;
        public PixelWorld PixelWorld
        {
            get => _pixelWorld;

            set
            {
                if (_pixelWorld != value)
                {
                    _pixelWorld = value;
                    refreshName();
                }
            }
        }

        public static PixelWorldObject CreateInstance(PixelWorld PixelWorld)
        {
            var obj = ScriptableObject.CreateInstance<PixelWorldObject>();
            obj.PixelWorld = PixelWorld;
            return obj;
        }

        protected void refreshName()
        {
            name = PixelWorld.GetType().Name;
        }

        public override bool Equals(object obj) => Equals(obj as PixelWorldObject);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PixelWorld.GetHashCode();
                return hashCode;
            }
        }

        public bool Equals(PixelWorldObject other)
        {
            return PixelWorld.Equals(other.PixelWorld);
        }
    }
}
#endif
