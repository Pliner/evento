using System.Net.Http.Json;
using Evento.Controllers;
using Evento.Db;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Evento.IntegrationTests;

public class SubscriptionTests : AppTestBase
{
    [Fact]
    public async Task Should_save_subscription()
    {
        await using var app = CreateApp();
        using var client = app.CreateClient();

        var dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<EventoDbContext>>();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync(CancellationToken.None))
            await dbContext.Database.MigrateAsync();


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