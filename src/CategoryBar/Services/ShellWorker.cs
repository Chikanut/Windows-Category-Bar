using System.Collections.Concurrent;
using System.Threading;

namespace CategoryBar.Services;

/// <summary>
/// One background STA thread for everything that talks to the Windows Shell (icons, shortcut files,
/// application discovery, process inspection). Keeps slow shell calls away from the UI thread.
/// </summary>
internal static class ShellWorker
{
    private static readonly BlockingCollection<Action> Jobs = [];

    static ShellWorker()
    {
        var thread = new Thread(() => { foreach (var job in Jobs.GetConsumingEnumerable()) job(); })
        { IsBackground = true, Name = "Shell worker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    internal static Task<T> Run<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Jobs.Add(() =>
        {
            try { completion.SetResult(work()); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        return completion.Task;
    }
}
