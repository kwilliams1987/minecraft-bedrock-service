using System.Collections.Generic;

namespace MinecraftBedrockService.Helpers;

internal class Observer<T> : IDisposable
{
    private readonly List<IObserver<T>> _observers;
    private readonly IObserver<T> _observer;

    public Observer(List<IObserver<T>> observers, IObserver<T> observer)
    {
        _observers = observers ?? throw new ArgumentNullException(nameof(observers));
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));

        _observers.Add(observer);
    }

    public void Dispose()
    {
        if (_observer != null && _observers.Contains(_observer))
        {
            _observers.Remove(_observer);
        }
    }
}
