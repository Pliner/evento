namespace Evento.Internals;

internal sealed class AsyncLazy<T> : IDisposable
{
    private readonly AsyncLock mutex = new();
    private readonly Func<CancellationToken, Task<T>> factory;
    private volatile Task<T>? initializedValueTask;

    public AsyncLazy(Func<CancellationToken, Task<T>> factory) => this.factory = factory;

    public Task<T> GetAsync(CancellationToken cancellationToken = default) => initializedValueTask ?? GetInternalAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => mutex.Dispose();

    private async Task<T> GetInternalAsync(CancellationToken cancellationToken)
    {
        using var _ = await mutex.AcquireAsync(cancellationToken).ConfigureAwait(false);

        var valueTask = initializedValueTask;
        if (valueTask != null) return await valueTask.ConfigureAwait(false);

        var value = await factory(cancellationToken).ConfigureAwait(false);
        initializedValueTask = Task.FromResult(value);
        return value;
    }
}