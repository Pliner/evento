using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Configurations.MessageBrokers;
using DotNet.Testcontainers.Containers.Modules.Abstractions;
using DotNet.Testcontainers.Containers.Modules.MessageBrokers;
using Xunit;

namespace Evento.Tests;

public class AppTestBase : IAsyncLifetime
{
    private readonly TestcontainerMessageBroker rmqContainer = new TestcontainersBuilder<RabbitMqTestcontainer>()
        .WithMessageBroker(
            new RabbitMqTestcontainerConfiguration("rabbitmq:3.10-management")
            {
                Username = "guest",
                Password = "guest"
            }
        )
        .Build();

    internal AppFactory CreateApp() => new(new RmqSettings(rmqContainer.Hostname, rmqContainer.Port));

    public Task InitializeAsync()
    {
        return rmqContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return rmqContainer.StopAsync();
    }
}