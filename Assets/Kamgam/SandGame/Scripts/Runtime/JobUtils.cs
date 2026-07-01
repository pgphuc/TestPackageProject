using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Kamgam.SandGame
{
    public static class JobUtils
    {
        /// <summary>
        /// Returns the number of threads (at least 8) based on the number of CPUs - 1.
        /// </summary>
        /// <returns></returns>
        public static int GetNumberOfThreadProcessors()
        {
            return Mathf.Max(8, SystemInfo.processorCount - 1);
        }

        public static uint GetRandomSeed()
        {
            // Seed must not be uint.MaxValue for BURST
            return (uint)(UnityEngine.Random.value * (uint.MaxValue - 1));
        }

        /// <summary>
        /// Waits for all the given jobs to complete.<br />
        /// Uses JobHandle.CompleteAll(), see: https://forum.unity.com/threads/whats-the-correct-way-of-waiting-for-a-list-of-jobs-to-finish.841915/
        /// </summary>
        /// <param name="handles"></param>
        /// <param name="clear">Clear the handles after waiting?</param>
        public static void WaitForJobs(List<JobHandle> handles, bool clear = true)
        {
            var jobs = new NativeArray<JobHandle>(handles.Count, Allocator.Temp);
            for (int i = 0; i < handles.Count; i++)
            {
                jobs[i] = handles[i];
            }

            JobHandle.CompleteAll(jobs);
            jobs.Dispose();

            if (clear)
                handles.Clear();
        }

        public static JobHandle CombineJobHandles(List<JobHandle> handles)
        {
            var jobs = new NativeArray<JobHandle>(handles.Count, Allocator.Temp);
            for (int i = 0; i < handles.Count; i++)
            {
                jobs[i] = handles[i];
            }

            var combinedHandle = JobHandle.CombineDependencies(jobs);
            jobs.Dispose();

            return combinedHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugLog(FixedString32Bytes text)
        {
            UnityEngine.Debug.Log(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugLog(FixedString32Bytes text, int number)
        {
            UnityEngine.Debug.Log(string.Format("{0}: {1}", text, number));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugLog(FixedString32Bytes text, int number0, int number1)
        {
            UnityEngine.Debug.Log(string.Format("{0}: {1}, {2}", text, number0, number1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugLog(FixedString32Bytes text, int number0, int number1, int number2)
        {
            UnityEngine.Debug.Log(string.Format("{0}: {1}, {2}, {3}", text, number0, number1, number2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugLog(FixedString32Bytes text, int number0, int number1, int number2, int number3)
        {
            UnityEngine.Debug.Log(string.Format("{0}: {1}, {2}, {3}, {4}", text, number0, number1, number2, number3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugLog(FixedString32Bytes text, float number)
        {
            UnityEngine.Debug.Log(string.Format("{0}: {1}", text, number));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugLog(FixedString32Bytes text, float number0, float number1)
        {
            UnityEngine.Debug.Log(string.Format("{0}: {1}, {2}", text, number0, number1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugLog(FixedString32Bytes text, float number0, float number1, float number2)
        {
            UnityEngine.Debug.Log(string.Format("{0}: {1}, {2}, {3}", text, number0, number1, number2));
        }
    }
}
