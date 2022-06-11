using System.Net.Http.Json;
using Evento.Controllers;
using Evento.Services;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Evento.Tests;

public class EventTests : AppTestBase
{

    [Fact]
    public async Task Should_receive_events()
    {
        await using var app = CreateApp();
        using var client = app.CreateClient();

        var newSubscription = new NewSubscriptionDto("id", new[] { "type" }, "http://hooks/success");
        using var saveResponse = await client.PostAsync("/subscriptions", JsonContent.Create(newSubscription));
        saveResponse.EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromSeconds(5));

        var @event = new Event("id", "type", DateTime.UtcNow, new byte[] { 42 }.AsMemory());
        var parameters = new Dictionary<string, string>
        {
            { "id", @event.Id },
            { "type", @event.Type },
            { "timestamp", @event.Timestamp.ToString("O") },
        };
        var submitEventResponse = await client.PostAsync(
            QueryHelpers.AddQueryString("/events", parameters), new ReadOnlyMemoryContent(@event.Payload)
        );
        submitEventResponse.EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromSeconds(5));

        app.ReceivedEvents.Should().BeEquivalentTo(
            new[] { @event },
            c => c.ComparingByMembers<Event>().Using(new ReadOnlyMemoryComparer<byte>())
        );
    }

    private class ReadOnlyMemoryComparer<T> : IEqualityComparer<ReadOnlyMemory<T>>
    {
        public bool Equals(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y) => x.Span.SequenceEqual(y.Span);

        public int GetHashCode(ReadOnlyMemory<T> obj) => 42;
    }
}