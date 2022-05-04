using System.Linq;

namespace System.Collections.Generic;

internal static class IObserverCollectionExtensions
{
    public static void OnNext<T>(this IEnumerable<IObserver<T>> observers, T value)
    {
        foreach (var observer in observers.ToArray())
        {
            observer.OnNext(value);
        }
    }
}
