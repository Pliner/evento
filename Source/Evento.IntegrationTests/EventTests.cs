using Evento.Client;
using Evento.Services;
using FluentAssertions;
using Xunit;

namespace Evento.IntegrationTests;

public class EventTests : AppTestBase
{
    [Fact]
    public async Task Should_process_event()
    {
        await using var app = CreateApp();
        using var httpClient = app.CreateClient();
        var eventoClient = new EventoClient(httpClient);

        var newSubscription = new NewSubscriptionDto("id", new[] { "type" }, "http://hooks/200");
        await eventoClient.AddSubscriptionAsync(newSubscription);

        await Task.Delay(TimeSpan.FromSeconds(10));

        var @event = new EventDto("type", new byte[] { 42 }.AsMemory());
        await eventoClient.SendEventAsync(@event);

        await Task.Delay(TimeSpan.FromSeconds(10));

        app.ReceivedEvents.Should().BeEquivalentTo(
            new[] { new Event(@event.Type, @event.Payload) },
            c => c.ComparingByMembers<Event>().Using(new ReadOnlyMemoryComparer<byte>())
        );
    }

    [Fact]
    public async Task Should_failed_to_process_event()
    {
        await using var app = CreateApp();
        using var httpClient = app.CreateClient();
        var eventoClient = new EventoClient(httpClient);

        var newSubscription = new NewSubscriptionDto("id", new[] { "type" }, "http://hooks/500");
        await eventoClient.AddSubscriptionAsync(newSubscription);

        await Task.Delay(TimeSpan.FromSeconds(10));

        var @event = new EventDto("type", new byte[] { 42 }.AsMemory());
        await eventoClient.SendEventAsync(@event);

        await Task.Delay(TimeSpan.FromSeconds(10));

        app.FailedAttemptsCount.Should().Be(6);
    }

    private class ReadOnlyMemoryComparer<T> : IEqualityComparer<ReadOnlyMemory<T>>
    {
        public bool Equals(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y) => x.Span.SequenceEqual(y.Span);

        public int GetHashCode(ReadOnlyMemory<T> obj) => 42;
    }
}