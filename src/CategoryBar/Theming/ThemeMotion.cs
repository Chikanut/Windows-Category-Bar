using System.Runtime.CompilerServices;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CategoryBar.Theming;

/// <summary>
/// Plays the theme's lifecycle transitions ("Theme.Menu.Enter", "Theme.Tab.Enter", …) on a host element.
/// Owns their clocks: re-entry cancels stale completions and unloading releases animations.
/// </summary>
internal static class ThemeMotion
{
    private sealed class Playback
    {
        internal Storyboard? Clock;
        internal int Generation;
        internal DispatcherTimer? Deadline, Delay;
        internal Action? Completion;
    }
    private static readonly ConditionalWeakTable<FrameworkElement, Playback> Playing = new();
    internal static bool Enabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    internal static void Stop(FrameworkElement host)
    {
        if (!Playing.TryGetValue(host, out var playback)) return;
        playback.Generation++;
        playback.Deadline?.Stop(); playback.Deadline = null;
        playback.Delay?.Stop(); playback.Delay = null;
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

    /// <summary>The pause between neighbours of a staggered group, from the stylesheet's "stagger" of that selector.</summary>
    internal static double Stagger(FrameworkElement host, string selector) => host.TryFindResource(selector + ".Stagger") is double value ? value : 0;

    /// <summary>
    /// Plays the transition stored under <paramref name="key"/>. Without one, a short fade and shift is used when
    /// <paramref name="fallback"/> allows it. <paramref name="delay"/> holds the first frame, which is how a group staggers.
    /// </summary>
    internal static void Play(FrameworkElement host, string key, Action? completed = null, double offset = 8, int milliseconds = 160, bool exit = false, bool fallback = true, double delay = 0)
    {
        var playback = Playing.GetValue(host, element =>
        {
            element.Unloaded += (_, _) => Stop(element);
            return new Playback();
        });
        Stop(host);
        var generation = playback.Generation;
        // Three parameter IDs are understood by the engine itself when a theme declares them:
        // "motion" (Toggle) switches transitions off, "motion-speed" (Number) scales their pace and
        // "motion-style" (Choice) prefers the variant stored as "<key>.<choice>", e.g. "Theme.Menu.Enter.pop".
        if (!Enabled || host.TryFindResource("Theme.Parameter.motion") is false) { completed?.Invoke(); return; }
        var authored = (host.TryFindResource("Theme.Parameter.motion-style") is string style ? host.TryFindResource(key + "." + style) as Storyboard : null)
            ?? host.TryFindResource(key) as Storyboard;
        if (authored is { Children.Count: 0 } || authored is null && !fallback) { completed?.Invoke(); return; }
        var storyboard = authored?.Clone() ?? new Storyboard();
        if (authored is null)
        {
            var duration = TimeSpan.FromMilliseconds(milliseconds);
            var fade = new DoubleAnimation(exit ? host.Opacity : 0, exit ? 0 : host.Opacity, duration);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(fade);
            var shift = new DoubleAnimation(exit ? 0 : offset, exit ? offset : 0, duration) { EasingFunction = new CubicEase { EasingMode = exit ? EasingMode.EaseIn : EasingMode.EaseOut } };
            Storyboard.SetTargetProperty(shift, new PropertyPath(ThemeTransform.YProperty));
            storyboard.Children.Add(shift);
        }
        if (host.TryFindResource("Theme.Parameter.motion-speed") is double speed && speed > 0) storyboard.SpeedRatio *= Math.Clamp(speed, .25, 4);
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
        playback.Deadline = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3 + delay / 1000) };
        playback.Deadline.Tick += (_, _) => Finish();
        playback.Deadline.Start();
        try
        {
            storyboard.Begin(host, HandoffBehavior.SnapshotAndReplace, true);
            if (delay > 0)
            {
                // Held on its first frame, so a waiting element already looks the way its transition starts.
                storyboard.Pause(host);
                playback.Delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Min(delay, 2000)) };
                playback.Delay.Tick += (_, _) => { playback.Delay?.Stop(); playback.Delay = null; if (playback.Generation == generation) storyboard.Resume(host); };
                playback.Delay.Start();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Stop(host);
            System.Diagnostics.Trace.WriteLine("Theme transition failed: " + key + ": " + ex.Message);
            completed?.Invoke();
        }
    }
}
