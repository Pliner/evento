using System.Net.Http.Json;
using Evento.Controllers;
using Evento.Db;
using Evento.Services;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Evento.IntegrationTests;

public class EventTests : AppTestBase
{
    [Fact]
    public async Task Should_receive_events()
    {
        await using var app = CreateApp();
        using var client = app.CreateClient();

        var dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<EventoDbContext>>();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync(CancellationToken.None))
            await dbContext.Database.MigrateAsync();

        var newSubscription = new NewSubscriptionDto("id", new[] { "type" }, "http://hooks/success");
        using var saveResponse = await client.PostAsync("/subscriptions", JsonContent.Create(newSubscription));
        saveResponse.EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromSeconds(5));

        var @event = new Event("type", new byte[] { 42 }.AsMemory());
        var parameters = new Dictionary<string, string>
        {
            { "type", @event.Type }
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