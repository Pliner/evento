using Evento.Client;
using FluentAssertions;
using Xunit;

namespace Evento.IntegrationTests;

public class SubscriptionTests : AppTestBase
{
    [Fact]
    public async Task Should_save_subscription()
    {
        await using var app = CreateApp();
        using var httpClient = app.CreateClient();
        var eventoClient = new EventoClient(httpClient);

        var newSubscription = new NewSubscriptionDto("id", new[] { "type" }, "endpoint");
        await eventoClient.AddSubscriptionAsync(newSubscription);
        await eventoClient.AddSubscriptionAsync(newSubscription);

        var subscriptions = await eventoClient.GetSubscriptionsAsync();

        subscriptions.Should()
            .BeEquivalentTo(
                new[] { new SubscriptionDto("id", DateTime.Today, new[] { "type" }, "endpoint") },
                c => c.ComparingByMembers<SubscriptionDto>().Excluding(x => x.CreatedAt)
            );
    }
}