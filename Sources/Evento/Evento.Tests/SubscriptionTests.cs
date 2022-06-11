using System.Net.Http.Json;
using Evento.Controllers;
using FluentAssertions;
using Xunit;

namespace Evento.Tests;

public class SubscriptionTests
{
    [Fact]
    public async Task Should_save_subscription()
    {
        await using var app = new ApplicationFactory();
        using var client = app.CreateClient();

        var newSubscription = new NewSubscriptionDto("id", new[] { "type" }, "endpoint");
        using var saveResponse = await client.PostAsync("/subscriptions", JsonContent.Create(newSubscription));
        saveResponse.EnsureSuccessStatusCode();

        var subscriptions = await client.GetFromJsonAsync<SubscriptionDto[]>("/subscriptions");

        subscriptions.Should()
            .BeEquivalentTo(
                new[] { new SubscriptionDto("id", DateTime.Today, new[] { "type" }, "endpoint") },
                c => c.ComparingByMembers<SubscriptionDto>().Excluding(x => x.CreatedAt)
            );
    }
}