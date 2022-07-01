using System.Collections.Concurrent;
using Evento.Db;
using Evento.Services;
using JustEat.HttpClientInterception;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Evento.IntegrationTests;

internal class AppFactory : WebApplicationFactory<Program>
{
    private readonly string rmqHost;
    private readonly int rmqPort;
    private readonly string pgHost;
    private readonly int pgPort;
    private readonly HttpClientInterceptorOptions httpClientInterceptorOptions = new() { ThrowOnMissingRegistration = true };
    private readonly ConcurrentQueue<Event> events = new();
    private long failedEventsCount;

    public IReadOnlyList<Event> ReceivedEvents => events.ToList();
    public long FailedAttemptsCount => Interlocked.Read(ref failedEventsCount);

    public AppFactory(string rmqHost, int rmqPort, string pgHost, int pgPort)
    {
        this.rmqHost = rmqHost;
        this.rmqPort = rmqPort;
        this.pgHost = pgHost;
        this.pgPort = pgPort;
        httpClientInterceptorOptions.Register(
            new HttpRequestInterceptionBuilder()
                .For(x => x.Method == HttpMethod.Post && (x.RequestUri?.ToString().StartsWith("http://hooks/200") ?? false))
                .IgnoringQuery()
                .WithInterceptionCallback(
                    async (m, c) =>
                    {
                        var query = QueryHelpers.ParseQuery(m.RequestUri?.Query);
                        var type = query["type"].Single();
                        var payload = await (m.Content ?? new ByteArrayContent(Array.Empty<byte>())).ReadAsByteArrayAsync(c);
                        var @event = new Event(type, payload.AsMemory());
                        events.Enqueue(@event);
                    }
                ),
            new HttpRequestInterceptionBuilder()
                .For(x => x.Method == HttpMethod.Post && (x.RequestUri?.ToString().StartsWith("http://hooks/500") ?? false))
                .IgnoringQuery()
                .WithInterceptionCallback(x => Interlocked.Increment(ref failedEventsCount))
                .WithStatus(500)
        );
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(
            builder.ConfigureServices(
                    s =>
                    {
                        s.AddSingleton<IHttpMessageHandlerBuilderFilter, InterceptionFilter>(
                                _ => new InterceptionFilter(httpClientInterceptorOptions)
                            )
                        .Insert(0, ServiceDescriptor.Singleton<IHostedService, MigrationService>());
                    })
                .ConfigureAppConfiguration(
                    x => x.AddInMemoryCollection(
                        new Dictionary<string, string>
                        {
                            { "RMQ_HOSTS", $"{rmqHost}:{rmqPort}" },
                            { "POSTGRES_HOST", pgHost },
                            { "POSTGRES_PORT", pgPort.ToString() },
                        }
                    )
                )
        );
    }

    private sealed class InterceptionFilter : IHttpMessageHandlerBuilderFilter
    {
        private readonly HttpClientInterceptorOptions options;

        internal InterceptionFilter(HttpClientInterceptorOptions options)
            => this.options = options;

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
            => builder =>
            {
                next(builder);
                builder.AdditionalHandlers.Add(options.CreateHttpMessageHandler());
            };
    }

    public class MigrationService : IHostedService
    {
        private readonly IDbContextFactory<EventoDbContext> dbContextFactory;

        public MigrationService(IDbContextFactory<EventoDbContext> dbContextFactory) => this.dbContextFactory = dbContextFactory;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}