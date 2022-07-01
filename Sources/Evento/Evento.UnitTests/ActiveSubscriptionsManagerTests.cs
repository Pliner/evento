using Evento.Repositories.Subscription;
using Evento.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Prometheus;
using Xunit;

namespace Evento.UnitTests;

public class ActiveSubscriptionsManagerTests
{
    private readonly ActiveSubscriptionsManager manager;
    private readonly ISubscriptionRepository subscriptionRepository;
    private readonly IPublishSubcribeTransport publishSubcribe;

    public ActiveSubscriptionsManagerTests()
    {
        subscriptionRepository = Substitute.For<ISubscriptionRepository>();
        publishSubcribe = Substitute.For<IPublishSubcribeTransport>();
        manager = new ActiveSubscriptionsManager(
            Substitute.For<ILogger<ActiveSubscriptionsManager>>(),
            subscriptionRepository,
            Substitute.For<IDirectTransport>(),
            publishSubcribe,
            Substitute.For<IMetricFactory>()
        );
    }

    [Fact]
    public async Task Should_do_nothing_When_no_subscription()
    {
        subscriptionRepository.SelectActiveAsync().Returns(Array.Empty<Subscription>());

        publishSubcribe.ActiveSubscriptions.Returns(new HashSet<Guid>());

        await manager.ExecuteAsync();

        await publishSubcribe.DidNotReceive().SubscribeAsync(Arg.Any<Subscription>(), Arg.Any<Func<Subscription, Event, CancellationToken, Task>>());
        await publishSubcribe.DidNotReceive().UnsubscribeAsync(Arg.Any<Guid>());
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
        subscriptionRepository.SelectActiveAsync().Returns(new[] { subscription });

        publishSubcribe.ActiveSubscriptions.Returns(new HashSet<Guid>());

        await manager.ExecuteAsync();

        await publishSubcribe.Received().SubscribeAsync(Arg.Is(subscription), Arg.Any<Func<Subscription, Event, CancellationToken, Task>>());
        await publishSubcribe.DidNotReceive().UnsubscribeAsync(Arg.Any<Guid>());
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

        subscriptionRepository.SelectActiveAsync().Returns(new[] { subscription });
        publishSubcribe.ActiveSubscriptions.Returns(new[] { id }.ToHashSet());

        await manager.ExecuteAsync();

        await publishSubcribe.Received().SubscribeAsync(Arg.Is(subscription), Arg.Any<Func<Subscription, Event, CancellationToken, Task>>());
        await publishSubcribe.DidNotReceive().UnsubscribeAsync(Arg.Any<Guid>());
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

        subscriptionRepository.SelectActiveAsync().Returns(new[] { oldSubscription, newSubscription });

        publishSubcribe.ActiveSubscriptions.Returns(new[] { id }.ToHashSet());
        publishSubcribe.UnsubscribeAsync(Arg.Any<Guid>()).Returns(true);

        await manager.ExecuteAsync();

        await publishSubcribe.Received().SubscribeAsync(Arg.Is(newSubscription), Arg.Any<Func<Subscription, Event, CancellationToken, Task>>());
        await publishSubcribe.Received().UnsubscribeAsync(Arg.Is(oldSubscription.Id));
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

        subscriptionRepository.SelectActiveAsync().Returns(new[] { oldSubscription, newSubscription });

        publishSubcribe.ActiveSubscriptions.Returns(new[] { id }.ToHashSet());
        publishSubcribe.UnsubscribeAsync(Arg.Any<Guid>()).Returns(false);

        await manager.ExecuteAsync();

        await publishSubcribe.Received().SubscribeAsync(Arg.Is(newSubscription), Arg.Any<Func<Subscription, Event, CancellationToken, Task>>());
        await publishSubcribe.Received().UnsubscribeAsync(Arg.Is(oldSubscription.Id));
        await subscriptionRepository.DidNotReceive().DeactivateAsync(Arg.Any<Guid>());
    }
}