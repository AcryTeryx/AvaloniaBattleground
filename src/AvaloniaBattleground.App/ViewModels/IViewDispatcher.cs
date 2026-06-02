using Avalonia.Threading;
using System;

namespace AvaloniaBattleground.App.ViewModels;

public interface IViewDispatcher
{
    void Post(Action action);
}

public sealed class AvaloniaViewDispatcher : IViewDispatcher
{
    public void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
