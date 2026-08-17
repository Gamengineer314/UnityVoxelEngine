using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// Benchmark runner
/// </summary>
public class Benchmark {
    private readonly List<(Action action, string name, double approxRunTime)> actions = new();
    private readonly List<Action> startActions = new();
    private readonly List<Action> endActions = new();


    /// <summary>
    /// Add an action to run in the benchmark
    /// </summary>
    /// <param name="action">The action to run</param>
    /// <param name="name">Name of the action</param>
    /// <param name="approxRunTime">Approximate time (in seconds) allowed to run this action in benchmark</param>
    public void Add(Action action, string name, double approxRunTime)
        => actions.Add((action, name, approxRunTime));


    /// <summary>
    /// Add an action to run before the benchmark
    /// </summary>
    /// <param name="action">The action to run</param>
    public void AddStart(Action action)
        => startActions.Add(action);


    /// <summary>
    /// Add an action to run after the benchmark
    /// </summary>
    /// <param name="action">The action to run</param>
    public void AddEnd(Action action)
        => endActions.Add(action);


    /// <summary>
    /// Run all added actions several times while measuring time.
    /// Actions will be run in the order they were added.
    /// </summary>
    /// <returns>Text containing benchmark results</returns>
    public string Run() {
        string result = "";
        foreach (Action action in startActions) {
            action?.Invoke();
        }
        foreach ((Action action, string name, double approxRunTime) in actions) {
            result += Run(action, name, approxRunTime) + "\n";
        }
        foreach (Action action in endActions) {
            action?.Invoke();
        }
        return result[0..^1];
    }


    // Run single action and return result line
    private string Run(Action action, string name, double approxRunTime) {
        Stopwatch watch = Stopwatch.StartNew();

        // Find best batch size
        int batch = 1;
        double batchTime = 0;
        while (batchTime < 0.1) {
            watch.Restart();
            for (int i = 0; i < batch; i++) {
                action.Invoke();
            }
            batchTime = (double)watch.ElapsedTicks / Stopwatch.Frequency;
            batch *= 5;
        }
        batch /= 5;

        // Run batches
        int nBatches = (int)Math.Ceiling(approxRunTime / batchTime);
        List<double> times = new() { batchTime / batch };
        for (int i = 0; i < nBatches; i++) {
            watch.Restart();
            for (int j = 0; j < batch; j++) {
                action.Invoke();
            }
            times.Add((double)watch.ElapsedTicks / Stopwatch.Frequency / batch);
        }

        // Compute results
        double mean = times.Sum() / times.Count;
        double stdDev = 0;
        foreach (double time in times) {
            stdDev += (time - mean) * (time - mean);
        }
        stdDev = Math.Sqrt(stdDev / (times.Count - 1));

        return $"{name} | Mean: {mean:G4} s | StdDev: {stdDev:G4} s";
    }
}