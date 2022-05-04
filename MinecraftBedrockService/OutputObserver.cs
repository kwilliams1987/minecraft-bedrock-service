namespace MinecraftBedrockService;

internal class OutputObserver : IObserver<string>
{
    private readonly Func<string, Task> _onNextAsync;
    private readonly Func<Exception, Task> _onExceptionAsync;

    public OutputObserver(Func<string, Task> onNext, Func<Exception, Task> onException = null)
    {
        _onNextAsync = onNext;
        _onExceptionAsync = onException;
    }

    public OutputObserver(Action<string> onNext, Action<Exception> onException = null)
    {
        _onNextAsync = value =>
        {
            onNext(value);
            return Task.CompletedTask;
        };
        
        if (onException != null)
        {
            _onExceptionAsync = exception =>
            {
                onException(exception);
                return Task.CompletedTask;
            };
        }
    }

    public void OnCompleted() { }

    public async void OnError(Exception error) => await (_onExceptionAsync?.Invoke(error) ?? Task.CompletedTask).ConfigureAwait(false);

    public async void OnNext(string value) => await _onNextAsync(value).ConfigureAwait(false);
}
