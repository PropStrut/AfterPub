using System.Diagnostics;
using AfterPub.Core.Engines;

namespace AfterPub.Tests.Engines;

public sealed class ProcessTrackerTests
{
    [Fact]
    public void AbortAll_StopsATrackedProcess_AndSetsAbortRequested()
    {
        ProcessTracker tracker = new ProcessTracker();
        using Process process = StartLongRunningProcess();

        try
        {
            tracker.Track(process);

            tracker.AbortAll();

            Assert.True(process.WaitForExit(5000));
            Assert.True(tracker.AbortRequested);
        }
        finally
        {
            StopIfRunning(process);
        }
    }

    [Fact]
    public void Track_AfterAbortRequested_StopsTheProcessImmediately()
    {
        ProcessTracker tracker = new ProcessTracker();
        tracker.AbortAll();
        using Process process = StartLongRunningProcess();

        try
        {
            tracker.Track(process);

            Assert.True(process.WaitForExit(5000));
        }
        finally
        {
            StopIfRunning(process);
        }
    }

    [Fact]
    public void Reset_ClearsAbortRequested()
    {
        ProcessTracker tracker = new ProcessTracker();
        tracker.AbortAll();

        tracker.Reset();

        Assert.False(tracker.AbortRequested);
    }

    [Fact]
    public void AbortAll_AfterUntrack_LeavesTheProcessAlone()
    {
        ProcessTracker tracker = new ProcessTracker();
        using Process process = StartLongRunningProcess();

        try
        {
            tracker.Track(process);
            tracker.Untrack(process);

            tracker.AbortAll();

            Assert.False(process.WaitForExit(500));
        }
        finally
        {
            StopIfRunning(process);
        }
    }

    // A harmless process that runs for about 30 seconds unless it is stopped.
    private static Process StartLongRunningProcess()
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "ping",
            Arguments = "-n 30 127.0.0.1",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };

        Process process = new Process { StartInfo = startInfo };
        process.Start();
        return process;
    }

    private static void StopIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Test cleanup only.
        }
    }
}
