namespace Evento.Internals;

internal static class AsyncDisposableActions
{
    public static AsyncDisposableAction Create(Func<Task> asyncFunc) => new(asyncFunc);

    internal readonly struct AsyncDisposableAction : IAsyncDisposable
    {
        private readonly Func<Task> asyncFunc;
        public AsyncDisposableAction(Func<Task> asyncFunc) => this.asyncFunc = asyncFunc;

        public async ValueTask DisposeAsync() => await asyncFunc();
    }
}