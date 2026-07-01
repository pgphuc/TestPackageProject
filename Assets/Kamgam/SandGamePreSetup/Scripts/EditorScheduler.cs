#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kamgam.SandGame.PreSetup
{
    public static class EditorScheduler
    {
        public static bool _registeredToEditorUpdate;

        public static List<(double, Action, string, bool)> functionTable = new List<(double, Action, string, bool)>();

        public static bool HasId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            foreach (var tup in functionTable)
            {
                if (tup.Item3 == id)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Schedules the given function to be executed after delay in seconds.
        /// </summary>
        /// <param name="delayInSec"></param>
        /// <param name="func"></param>
        /// <param name="id">If specified then existing entries with the same id are replaced by the new one.</param>
        public static void Schedule(float delayInSec, Action func, string id = null)
        {
            registerToEditorUpdate();

            // Id an id was set then check if there already is a function with that id and if yes replace it.
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < functionTable.Count; i++)
                {
                    if (functionTable[i].Item3 == id)
                    {
                        functionTable[i] = (EditorApplication.timeSinceStartup + delayInSec, func, id, func.Target is MonoBehaviour);
                        return;
                    }
                }
            }

            functionTable.Add((EditorApplication.timeSinceStartup + delayInSec, func, id, func.Target is MonoBehaviour));
        }

        public static void Cancel(string id)
        {
            for (int i = functionTable.Count - 1; i >= 0; i--)
            {
                if (functionTable[i].Item3 == id)
                {
                    functionTable.RemoveAt(i);
                }
            }
        }

        static void registerToEditorUpdate()
        {
            if (_registeredToEditorUpdate)
                return;

            EditorApplication.update += update;
            _registeredToEditorUpdate = true;
        }

        static void unregisterFromEditorUpdate()
        {
            if (!_registeredToEditorUpdate)
                return;

            EditorApplication.update -= update;
            _registeredToEditorUpdate = false;
        }

        static void update()
        {
            double time = EditorApplication.timeSinceStartup;
            for (int i = functionTable.Count - 1; i >= 0; i--)
            {
                if (functionTable[i].Item1 <= time)
                {
                    var func = functionTable[i].Item2;
                    bool isMonoBehaviour = functionTable[i].Item4;
                    functionTable.RemoveAt(i);

                    // Some shenanegans to make sure the object we are calling func on is not destroyed.
                    var behaviour = func.Target as MonoBehaviour;
                    if (func != null && func.Target != null &&
                        (!isMonoBehaviour || (behaviour != null && behaviour.gameObject != null))
                        )
                    {
                        func?.Invoke();
                    }
                }
            }

            if (functionTable.Count == 0)
                unregisterFromEditorUpdate();
        }
    }
}
#endif

