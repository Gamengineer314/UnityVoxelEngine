using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;

namespace Voxels {
    
    internal abstract class ParallelGenerator<TCommand, TJob> where TJob : unmanaged, IJob, IDisposable {
        private readonly Dictionary<TCommand, Generation> generations = new();

        public int JobCount => generations.Sum(kv => kv.Value.jobs.Count);
        public int CompletedCount => generations.Sum(kv => kv.Value.jobs.Count(j => j.handle.IsCompleted));


        /// <summary>
        /// Process the result of a job
        /// </summary>
        /// <param name="command">Command that was passed to Schedule</param>
        /// <param name="job">The job</param>
        protected abstract void ProcessResult(TCommand command, TJob job);

        /// <summary>
        /// Create the jobs for a generation
        /// </summary>
        /// <param name="command">Generation command</param>
        /// <param name="jobHorizontalSize">Max horizontal size a generator job can process</param>
        /// <returns>The jobs, or null if nothings needs to be done</returns>
        protected abstract IEnumerable<TJob> CreateJobs(TCommand command, int jobHorizontalSize);


        public void Dispose() {
            foreach (Generation generation in generations.Values) {
                foreach ((TJob job, JobHandle handle) in generation.jobs) {
                    handle.Complete();
                    job.Dispose();
                }
            }
            generations.Clear();
        }


        /// <summary>
        /// Complete the generation jobs for a command and add their results
        /// </summary>
        /// <param name="command">Command that was passed to Schedule</param>
        public void Complete(TCommand command) {
            if (!generations.TryGetValue(command, out var generation)) return;
            foreach ((TJob job, JobHandle handle) in generation.jobs) {
                handle.Complete();
                ProcessResult(command, job);
                job.Dispose();
            }
            generations.Remove(command);
            foreach (Action<TCommand> action in generation.onComplete) {
                action.Invoke(command);
            }
        }


        /// <summary>
        /// Complete the generation jobs that are completed and add their results
        /// </summary>
        public void Update() {
            List<TCommand> completed = new();
            foreach (KeyValuePair<TCommand, Generation> kv in generations) {
                TCommand command = kv.Key;
                List<(TJob job, JobHandle handle)> jobs = kv.Value.jobs;
                bool asynchronous = kv.Value.asynchronous;
                for (int i = jobs.Count - 1; i >= 0; i--) {
                    (TJob job, JobHandle handle) = jobs[i];
                    if (!asynchronous || handle.IsCompleted) {
                        handle.Complete();
                        ProcessResult(command, job);
                        job.Dispose();
                        jobs.RemoveAtSwapBack(i);
                    }
                }
                if (jobs.Count == 0) completed.Add(command);
            }
            foreach (TCommand command in completed) {
                foreach (Action<TCommand> action in generations[command].onComplete) {
                    action.Invoke(command);
                }
                generations.Remove(command);
            }
        }


        /// <summary>
        /// Schedule a generation if needed
        /// </summary>
        /// <param name="command">Generation command</param>
        /// <param name="jobHorizontalSize">Max horizontal size a generator job can process</param>
        /// <param name="asynchronousGeneration">Whether the generation can be performed asynchronously over multiple frames</param>
        /// <param name="onComplete">Callback called when the generation completes</param>
        public void Schedule(TCommand command, int jobHorizontalSize, bool asynchronousGeneration, Action<TCommand> onComplete = null) {
            if (generations.TryGetValue(command, out var generation)) {
                if (onComplete != null) generation.onComplete.Add(onComplete);
                return;
            }
            IEnumerable<TJob> jobs = CreateJobs(command, jobHorizontalSize);
            if (jobs == null) {
                onComplete?.Invoke(command);
                return;
            }
            generations[command] = new Generation(jobs, asynchronousGeneration, onComplete);
        }



        private readonly struct Generation {
            public readonly List<(TJob job, JobHandle handle)> jobs;
            public readonly List<Action<TCommand>> onComplete;
            public readonly bool asynchronous;

            public Generation(IEnumerable<TJob> jobs, bool asynchronous, Action<TCommand> onComplete) {
                this.jobs = jobs.Select(j => (j, j.Schedule())).ToList();
                this.onComplete = new();
                if (onComplete != null) this.onComplete.Add(onComplete);
                this.asynchronous = asynchronous;
            }
        }
    }

}