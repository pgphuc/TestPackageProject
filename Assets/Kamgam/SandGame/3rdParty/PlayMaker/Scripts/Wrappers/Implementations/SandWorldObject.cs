#if PLAYMAKER
using HutongGames.PlayMaker;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kamgam.SandGame.PlayMaker
{
    /// <summary>
    /// A wrapper for a Sand World<br />
    /// <br />
    /// Since PlayMaker variables can not store arbitrary types we have to wrap SandWorlds
    /// in a UnityEngie.Object, see: https://forum.unity.com/threads/playmaker-visual-scripting-for-unity.72349/page-70#post-9271821
    /// </summary>
    public class SandWorldObject : ScriptableObject, IEquatable<SandWorldObject>
    {
        protected SandWorld _sandWorld;
        public SandWorld SandWorld
        {
            get => _sandWorld;

            set
            {
                if (_sandWorld != value)
                {
                    _sandWorld = value;
                    refreshName();
                }
            }
        }

        public static SandWorldObject CreateInstance(SandWorld SandWorld)
        {
            var obj = ScriptableObject.CreateInstance<SandWorldObject>();
            obj.SandWorld = SandWorld;
            return obj;
        }

        protected void refreshName()
        {
            name = SandWorld.GetType().Name;
        }

        public override bool Equals(object obj) => Equals(obj as SandWorldObject);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = SandWorld.GetHashCode();
                return hashCode;
            }
        }

        public bool Equals(SandWorldObject other)
        {
            return SandWorld.Equals(other.SandWorld);
        }
    }
}
#endif
