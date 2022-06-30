using Evento.Repositories.Subscription;
using Evento.Services;
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

        var transport = Substitute.For<IPublishSubscribeTransport>();
        transport.ActiveSubscriptions.Returns(new HashSet<Guid>());

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, transport);

        await manager.ExecuteAsync();

        await transport.DidNotReceive().SubscribeAsync(Arg.Any<Subscription>());
        await transport.DidNotReceive().UnsubscribeAsync(Arg.Any<Guid>());
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

        var transport = Substitute.For<IPublishSubscribeTransport>();
        transport.ActiveSubscriptions.Returns(new HashSet<Guid>());

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, transport);

        await manager.ExecuteAsync();

        await transport.Received().SubscribeAsync(Arg.Is(subscription));
        await transport.DidNotReceive().UnsubscribeAsync(Arg.Any<Guid>());
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

        var transport = Substitute.For<IPublishSubscribeTransport>();
        transport.ActiveSubscriptions.Returns(new[] { id }.ToHashSet());

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, transport);

        await manager.ExecuteAsync();

        await transport.Received().SubscribeAsync(Arg.Is(subscription));
        await transport.DidNotReceive().UnsubscribeAsync(Arg.Any<Guid>());
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

        var transport = Substitute.For<IPublishSubscribeTransport>();
        transport.ActiveSubscriptions.Returns(new[] { id }.ToHashSet());
        transport.UnsubscribeAsync(Arg.Any<Guid>()).Returns(true);

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, transport);

        await manager.ExecuteAsync();

        await transport.Received().SubscribeAsync(Arg.Is(newSubscription));
        await transport.Received().UnsubscribeAsync(Arg.Is(oldSubscription.Id));
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

        var transport = Substitute.For<IPublishSubscribeTransport>();
        transport.ActiveSubscriptions.Returns(new[] { id }.ToHashSet());
        transport.UnsubscribeAsync(Arg.Any<Guid>()).Returns(false);

        var manager = new ActiveSubscriptionsManager(subscriptionRepository, transport);

        await manager.ExecuteAsync();

        await transport.Received().SubscribeAsync(Arg.Is(newSubscription));
        await transport.Received().UnsubscribeAsync(Arg.Is(oldSubscription.Id));
        await subscriptionRepository.DidNotReceive().DeactivateAsync(Arg.Any<Guid>());
    }
}