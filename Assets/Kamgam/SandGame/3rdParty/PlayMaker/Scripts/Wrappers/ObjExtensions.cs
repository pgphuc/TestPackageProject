#if PLAYMAKER
using HutongGames.PlayMaker;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kamgam.SandGame.PlayMaker
{
    public static class ObjExtensions
    {
        public static bool HasValue(this FsmObject obj)
        {
            return obj != null && obj.Value != null;
        }

        public static bool HasValue<T>(this FsmObject obj) where T : class
        {
            if (obj == null || obj.Value == null)
                return false;

            var typedValue = obj as T;
            if (typedValue != null)
                return true;

            return false;
        }

        public static bool HasValue(this PixelWorldObject obj)
        {
            return obj != null && obj.PixelWorld != null;
        }

        public static bool HasValue(this SandWorldObject obj)
        {
            return obj != null && obj.SandWorld != null;
        }

        public static T GetWrapper<T>(this FsmObject obj) where T : class
        {
            if (obj == null || obj.Value == null)
                return null;

            var typedValue = obj as T;
            if (typedValue != null)
                return typedValue;

            return null;
        }

        public static bool TryGetWrapper<T>(this FsmObject obj, out T value) where T : class
        {
            if (obj == null || obj.Value == null)
            {
                value = null;
                return false;
            }

            var typedValue = obj as T;
            if (typedValue != null)
            {
                value = typedValue;
                return true;
            }

            value = null;
            return false;
        }

        public static bool TryGetPixelWorld(this FsmObject fsmObj, out PixelWorld element)
        {
            if (!fsmObj.HasValue())
            {
                element = null;
                return false;
            }

            var wrapper = fsmObj.Value as PixelWorldObject;
            if (!wrapper.HasValue())
            {
                element = null;
                return false;
            }

            element = wrapper.PixelWorld;
            return true;
        }
		
		public static bool TryGetSandWorld(this FsmObject fsmObj, out SandWorld element)
        {
            if (!fsmObj.HasValue())
            {
                element = null;
                return false;
            }

            var wrapper = fsmObj.Value as SandWorldObject;
            if (!wrapper.HasValue())
            {
                element = null;
                return false;
            }

            element = wrapper.SandWorld;
            return true;
        }
    }
}
#endif
