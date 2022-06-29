using Evento.Repositories.Subscription;
using Evento.Services;
using Evento.Services.SubscriptionRegistry;
using NSubstitute;
using Xunit;

namespace Evento.UnitTests;

public class ActiveSubscriptionsManagerTests
{
    [Fact]
    public async Task Should_do_nothing_When_no_subscription()
    {
        var subscriptionRepository = Substitute.For<ISubscriptionRepository>();
        subscriptionRepository.SelectActiveAsync().Returns(Array.Empty<Subscription>());

        var subscriptionRegistry = Substitute.For<ISubscriptionRegistry>();
        subscriptionRegistry.Registered.Returns(new HashSet<Guid>());

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, subscriptionRegistry);

        await manager.ExecuteAsync();

        await subscriptionRegistry.DidNotReceive().RegisterAsync(Arg.Any<Subscription>());
        await subscriptionRegistry.DidNotReceive().UnregisterAsync(Arg.Any<Guid>());
        await subscriptionRepository.DidNotReceive().DeactivateAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Should_register_new_subscription_When_it_has_not_been_registered()
    {
        var subscription = new Subscription
        (
            Id: Guid.NewGuid(),
            Name: "name",
            Version: 1,
            CreatedAt: DateTime.Today,
            Types: new[] { "types" },
            Endpoint: "endpoint"
        );
        var subscriptionRepository = Substitute.For<ISubscriptionRepository>();
        subscriptionRepository.SelectActiveAsync().Returns(new[] { subscription });

        var subscriptionRegistry = Substitute.For<ISubscriptionRegistry>();
        subscriptionRegistry.Registered.Returns(new HashSet<Guid>());

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, subscriptionRegistry);

        await manager.ExecuteAsync();

        await subscriptionRegistry.Received().RegisterAsync(Arg.Is(subscription));
        await subscriptionRegistry.DidNotReceive().UnregisterAsync(Arg.Any<Guid>());
        await subscriptionRepository.DidNotReceive().DeactivateAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Should_try_to_register_new_subscription_When_it_has_already_been_registered()
    {
        var id = Guid.NewGuid();
        var subscription = new Subscription
        (
            Id: id,
            Name: "name",
            Version: 1,
            CreatedAt: DateTime.Today,
            Types: new[] { "types" },
            Endpoint: "endpoint"
        );

        var subscriptionRepository = Substitute.For<ISubscriptionRepository>();
        subscriptionRepository.SelectActiveAsync().Returns(new[] { subscription });

        var subscriptionRegistry = Substitute.For<ISubscriptionRegistry>();
        subscriptionRegistry.Registered.Returns(new[] { id }.ToHashSet());

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, subscriptionRegistry);

        await manager.ExecuteAsync();

        await subscriptionRegistry.Received().RegisterAsync(Arg.Is(subscription));
        await subscriptionRegistry.DidNotReceive().UnregisterAsync(Arg.Any<Guid>());
        await subscriptionRepository.DidNotReceive().DeactivateAsync(Arg.Any<Guid>());
    }


    [Fact]
    public async Task Should_unregister_old_subscription_When_new_is_added()
    {
        var id = Guid.NewGuid();
        var oldSubscription = new Subscription
        (
            Id: id,
            Name: "name",
            Version: 1,
            CreatedAt: DateTime.Today,
            Types: new[] { "types" },
            Endpoint: "endpoint"
        );
        var newSubscription = new Subscription
        (
            Id: Guid.NewGuid(),
            Name: "name",
            Version: 2,
            CreatedAt: DateTime.Today,
            Types: new[] { "types" },
            Endpoint: "endpoint"
        );

        var subscriptionRepository = Substitute.For<ISubscriptionRepository>();
        subscriptionRepository.SelectActiveAsync().Returns(new[] { oldSubscription, newSubscription });

        var subscriptionRegistry = Substitute.For<ISubscriptionRegistry>();
        subscriptionRegistry.Registered.Returns(new[] { id }.ToHashSet());
        subscriptionRegistry.UnregisterAsync(Arg.Any<Guid>()).Returns(true);

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, subscriptionRegistry);

        await manager.ExecuteAsync();

        await subscriptionRegistry.Received().RegisterAsync(Arg.Is(newSubscription));
        await subscriptionRegistry.Received().UnregisterAsync(Arg.Is(oldSubscription.Id));
        await subscriptionRepository.Received().DeactivateAsync(Arg.Is(oldSubscription.Id));
    }

    [Fact]
    public async Task Should_try_unregister_old_subscription_multiple_times_When_new_is_added()
    {
        var id = Guid.NewGuid();
        var oldSubscription = new Subscription
        (
            Id: id,
            Name: "name",
            Version: 1,
            CreatedAt: DateTime.Today,
            Types: new[] { "types" },
            Endpoint: "endpoint"
        );

        var newSubscription = new Subscription
        (
            Id: Guid.NewGuid(),
            Name: "name",
            Version: 2,
            CreatedAt: DateTime.Today,
            Types: new[] { "types" },
            Endpoint: "endpoint"
        );

        var subscriptionRepository = Substitute.For<ISubscriptionRepository>();
        subscriptionRepository.SelectActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { oldSubscription, newSubscription });

        var subscriptionRegistry = Substitute.For<ISubscriptionRegistry>();
        subscriptionRegistry.Registered.Returns(new[] { id }.ToHashSet());
        subscriptionRegistry.UnregisterAsync(Arg.Any<Guid>()).Returns(false);

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, subscriptionRegistry);

        await manager.ExecuteAsync();

        await subscriptionRegistry.Received().RegisterAsync(Arg.Is(newSubscription));
        await subscriptionRegistry.Received().UnregisterAsync(Arg.Is(oldSubscription.Id));
        await subscriptionRepository.DidNotReceive().DeactivateAsync(Arg.Any<Guid>());
    }
}