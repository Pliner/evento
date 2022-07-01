using System.Net.Http.Json;
using Evento.Controllers;
using FluentAssertions;
using Xunit;

namespace Evento.IntegrationTests;

public class SubscriptionTests : AppTestBase
{
    [Fact]
    public async Task Should_save_subscription()
    {
        await using var app = CreateApp();
        using var client = app.CreateClient();

        var newSubscription = new NewSubscriptionDto("id", new[] { "type" }, "endpoint");
        using var initialSaveResponse = await client.PostAsync("/subscriptions", JsonContent.Create(newSubscription));
        initialSaveResponse.EnsureSuccessStatusCode();

        using var retrySaveResponse = await client.PostAsync("/subscriptions", JsonContent.Create(newSubscription));
        retrySaveResponse.EnsureSuccessStatusCode();

        var subscriptions = await client.GetFromJsonAsync<SubscriptionDto[]>("/subscriptions");

        subscriptions.Should()
            .BeEquivalentTo(
                new[] { new SubscriptionDto("id", DateTime.Today, new[] { "type" }, "endpoint") },
                c => c.ComparingByMembers<SubscriptionDto>().Excluding(x => x.CreatedAt)
            );
    }
}