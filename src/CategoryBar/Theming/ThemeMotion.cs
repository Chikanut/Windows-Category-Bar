using System.Runtime.CompilerServices;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CategoryBar.Theming;

/// <summary>Owns transition clocks. Re-entry cancels stale completions; unloading releases animations.</summary>
internal static class ThemeMotion
{
    private sealed class Playback
    {
        internal Storyboard? Clock;
        internal int Generation;
        internal DispatcherTimer? Deadline;
        internal Action? Completion;
    }
    private static readonly ConditionalWeakTable<FrameworkElement, Playback> Playing = new();
    internal static bool Enabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    internal static void Stop(FrameworkElement host)
    {
        if (!Playing.TryGetValue(host, out var playback)) return;
        playback.Generation++;
        playback.Deadline?.Stop();
        playback.Deadline = null;
        playback.Clock?.Remove(host);
        playback.Clock = null;
        playback.Completion = null;
    }

    internal static void FinishAll()
    {
        foreach (var (host, playback) in Playing.ToArray())
        {
            var completion = playback.Completion;
            Stop(host);
            completion?.Invoke();
        }
    }

    internal static void Play(FrameworkElement host, string key, Action? completed = null, double offset = 8, int milliseconds = 160, bool exit = false)
    {
        var playback = Playing.GetValue(host, element =>
        {
            element.Unloaded += (_, _) => Stop(element);
            return new Playback();
        });
        Stop(host);
        var generation = playback.Generation;
        if (!Enabled) { completed?.Invoke(); return; }
        // This wrapper belongs to motion; transforms inside the template remain owned by its author.
        host.RenderTransform = new TranslateTransform();
        var authored = host.TryFindResource(key) as Storyboard;
        if (authored is { Children.Count: 0 }) { completed?.Invoke(); return; }
        var storyboard = authored?.Clone() ?? new Storyboard();
        if (authored is null)
        {
            var duration = TimeSpan.FromMilliseconds(milliseconds);
            var fade = new DoubleAnimation(exit ? host.Opacity : 0, exit ? 0 : host.Opacity, duration);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(fade);
            var shift = new DoubleAnimation(exit ? 0 : offset, exit ? offset : 0, duration) { EasingFunction = new CubicEase { EasingMode = exit ? EasingMode.EaseIn : EasingMode.EaseOut } };
            Storyboard.SetTargetProperty(shift, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            storyboard.Children.Add(shift);
        }
        void Finish()
        {
            if (playback.Generation != generation) return;
            // Hide/swap while the last exit frame still applies, then release its clocks.
            completed?.Invoke();
            if (playback.Generation == generation) Stop(host);
        }
        storyboard.Completed += (_, _) => Finish();
        playback.Clock = storyboard;
        playback.Completion = completed;
        // An author cannot hold a menu open forever through nested/repeated timeline durations.
        playback.Deadline = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        playback.Deadline.Tick += (_, _) => Finish();
        playback.Deadline.Start();
        try { storyboard.Begin(host, HandoffBehavior.SnapshotAndReplace, true); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Stop(host);
            System.Diagnostics.Trace.WriteLine("Theme transition failed: " + key + ": " + ex.Message);
            completed?.Invoke();
        }
    }
}
