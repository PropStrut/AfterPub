using System.Diagnostics;

namespace AfterPub.Core.Engines;

/// <summary>
/// Keeps track of the engine processes a batch has started (LibreOffice and Scribus run as
/// separate processes) so "Abort now" can stop them immediately (CLAUDE.md section 3.4).
/// One instance is shared by the engines for the lifetime of the window. Safe to call from
/// the UI thread while an engine is running on a worker thread.
///
/// Publisher is automated through COM rather than a child process the app owns, so it is
/// not tracked here; an abort takes effect for it after the current file finishes.
/// </summary>
public sealed class ProcessTracker
{
    /// <summary>The failure message an engine returns when its process was stopped by an abort.</summary>
    public const string AbortedMessage = "Stopped by the user.";

    private readonly object _lock = new object();
    private readonly List<Process> _running = new List<Process>();
    private bool _abortRequested;

    /// <summary>True from <see cref="AbortAll"/> until <see cref="Reset"/>.</summary>
    public bool AbortRequested
    {
        get
        {
            lock (this._lock)
            {
                return this._abortRequested;
            }
        }
    }

    /// <summary>Clears a previous abort. Call at the start of each batch.</summary>
    public void Reset()
    {
        lock (this._lock)
        {
            this._abortRequested = false;
        }
    }

    /// <summary>
    /// Registers a started process. If an abort has already been requested (the click landed
    /// just before this process started), it is stopped immediately.
    /// </summary>
    public void Track(Process process)
    {
        bool killNow;

        lock (this._lock)
        {
            this._running.Add(process);
            killNow = this._abortRequested;
        }

        if (killNow)
        {
            TryKill(process);
        }
    }

    /// <summary>Removes a process once it is finished, before it is disposed.</summary>
    public void Untrack(Process process)
    {
        lock (this._lock)
        {
            this._running.Remove(process);
        }
    }

    /// <summary>Stops every tracked process (and its children) and remembers that an abort happened.</summary>
    public void AbortAll()
    {
        List<Process> toKill;

        lock (this._lock)
        {
            this._abortRequested = true;
            toKill = new List<Process>(this._running);
        }

        foreach (Process process in toKill)
        {
            TryKill(process);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already exited or already disposed; either way there is nothing left to stop.
        }
    }
}
