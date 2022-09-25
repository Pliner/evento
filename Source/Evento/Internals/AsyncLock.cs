namespace Evento.Internals;

internal sealed class AsyncLock : IDisposable
{
    private readonly SemaphoreSlim semaphore;
    private readonly IDisposable releaser;
    private readonly Task<IDisposable> releaserTask;

    public AsyncLock()
    {
        semaphore = new SemaphoreSlim(1);
        releaser = new Releaser(semaphore);
        releaserTask = Task.FromResult(releaser);
    }

    public Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        var acquireAsync = semaphore.WaitAsync(cancellationToken);
        return acquireAsync.IsCompletedSuccessfully ? releaserTask : WaitForAcquireAsync(acquireAsync);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim semaphore;

        public Releaser(SemaphoreSlim semaphore) => this.semaphore = semaphore;

        public void Dispose() => semaphore.Release();
    }

    /// <inheritdoc />
    public void Dispose() => semaphore.Dispose();

    private async Task<IDisposable> WaitForAcquireAsync(Task acquireAsync)
    {
        await acquireAsync.ConfigureAwait(false);
        return releaser;
    }
}