using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Kamgam.SandGame.LevelPart;

namespace Kamgam.SandGame
{
    public interface IJobDisposable
    {
        public void Dispose();
    }

    /// <summary>
    /// Helper class that allows to register onComplete callbacks for jobs.<br />
    /// If work by polling the jobs for IsComplete. The polling has to be triggered
    /// manually by calling "Update()" and/or "LaterUpdate()".
    /// </summary>
    public class JobAwaiter
    {
        public class JobEntry
        {
            public IJobDisposable Job;
            public JobHandle JobHandle;
            public System.Action<JobHandle> OnComplete;

            public JobEntry(IJobDisposable job, JobHandle jobHandle, Action<JobHandle> onComplete)
            {
                Job = job;
                JobHandle = jobHandle;
                OnComplete = onComplete;
            }
        }

        public List<JobEntry> UpdateJobs = new List<JobEntry>();
        public List<JobEntry> LateUpdateJobs = new List<JobEntry>();

        public void AddToUpdate(IJobDisposable job, JobHandle jobHandle, System.Action<JobHandle> onComplete)
        {
            UpdateJobs.Add(new JobEntry(job, jobHandle, onComplete));
        }

        public void AddToLateUpdate(IJobDisposable job, JobHandle jobHandle, System.Action<JobHandle> onComplete)
        {
            LateUpdateJobs.Add(new JobEntry(job, jobHandle, onComplete));
        }

        public void Update()
        {
            waitForJobs(UpdateJobs);
        }

        public void LateUpdate()
        {
            waitForJobs(LateUpdateJobs);
        }

        protected void waitForJobs(List<JobEntry> jobs)
        {
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                var entry = jobs[i];

                if (!entry.JobHandle.IsCompleted)
                    continue;
                
                jobs.RemoveAt(i);

                entry.JobHandle.Complete();
                entry.Job.Dispose();
                entry?.OnComplete(entry.JobHandle);
            }
        }

        public void Clear()
        {
            UpdateJobs.Clear();
            LateUpdateJobs.Clear();
        }
    }
}
