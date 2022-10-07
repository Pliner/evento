namespace Evento.Internals;

internal static class TimeSpanExtensions
{
    public static TimeSpan Randomize(this TimeSpan source)
    {
        return source + source.Multiply(Random.Shared.NextDouble());
    }
}