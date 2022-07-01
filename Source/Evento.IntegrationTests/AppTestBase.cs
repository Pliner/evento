using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Configurations.Databases;
using DotNet.Testcontainers.Containers.Configurations.MessageBrokers;
using DotNet.Testcontainers.Containers.Modules.Abstractions;
using DotNet.Testcontainers.Containers.Modules.Databases;
using DotNet.Testcontainers.Containers.Modules.MessageBrokers;
using Xunit;

namespace Evento.IntegrationTests;

public class AppTestBase : IAsyncLifetime
{
    private readonly TestcontainerMessageBroker rmqContainer = new TestcontainersBuilder<RabbitMqTestcontainer>()
        .WithMessageBroker(
            new RabbitMqTestcontainerConfiguration("rabbitmq:3.10-management")
            {
                Username = "guest",
                Password = "guest",
            }
        )
        .Build();

    private readonly TestcontainerDatabase pgContainer = new TestcontainersBuilder<PostgreSqlTestcontainer>()
        .WithDatabase(
            new PostgreSqlTestcontainerConfiguration("postgres:14")
            {
                Database = "postgres",
                Username = "postgres",
                Password = "some_secret",
            }
        )
        .Build();

    internal AppFactory CreateApp() => new(
        rmqContainer.Hostname,
        rmqContainer.Port,
        pgContainer.Hostname,
        pgContainer.Port
    );

    public async Task InitializeAsync()
    {
        await Task.WhenAll(rmqContainer.StartAsync(), pgContainer.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(rmqContainer.StopAsync(), pgContainer.StopAsync());
    }
}