using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Configurations.Databases;
using DotNet.Testcontainers.Containers.Configurations.MessageBrokers;
using DotNet.Testcontainers.Containers.Modules.Abstractions;
using DotNet.Testcontainers.Containers.Modules.Databases;
using DotNet.Testcontainers.Containers.Modules.MessageBrokers;
using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;
using Xunit;

namespace Evento.IntegrationTests;

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
        await rmqContainer.StartAsync();
        await pgContainer.StartAsync();

        using var rmqManagementClient = new ManagementClient(rmqContainer.Hostname, "guest", "guest", rmqContainer.Port);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await WaitForRabbitMqReadyAsync(rmqManagementClient, cts.Token);
    }
    
    private static async Task WaitForRabbitMqReadyAsync(ManagementClient managementClient, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsRabbitMqReadyAsync(managementClient, cancellationToken))
                return;

            await Task.Delay(500, cancellationToken);
        }
    }

    private static async Task<bool> IsRabbitMqReadyAsync(ManagementClient managementClient, CancellationToken cancellationToken)
    {
        try
        {
            return await managementClient.IsAliveAsync(new Vhost{Name = "/"}, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task DisposeAsync()
    {
        await rmqContainer.StopAsync();
        await pgContainer.StopAsync();
    }
}