using System.Collections.Concurrent;
using System.Globalization;
using Evento.Services;
using JustEat.HttpClientInterception;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    public IReadOnlyList<Event> ReceivedEvents => events.ToList();

    public AppFactory(string rmqHost, int rmqPort, string pgHost, int pgPort)
    {
        this.rmqHost = rmqHost;
        this.rmqPort = rmqPort;
        this.pgHost = pgHost;
        this.pgPort = pgPort;
        httpClientInterceptorOptions.Register(
            new[]
            {
                new HttpRequestInterceptionBuilder()
                    .For(x => x.Method == HttpMethod.Post && (x.RequestUri?.ToString().StartsWith("http://hooks/success") ?? false))
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
                    )
            });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(
                s => s.AddSingleton<IHttpMessageHandlerBuilderFilter, InterceptionFilter>(
                    _ => new InterceptionFilter(httpClientInterceptorOptions)
                )
            )
            .ConfigureAppConfiguration(
                x => x.AddInMemoryCollection(
                    new Dictionary<string, string>
                    {
                        { "RMQ_HOSTS", $"{rmqHost}:{rmqPort}" },
                        { "POSTGRES_HOST", pgHost },
                        { "POSTGRES_PORT", pgPort.ToString() },
                    }
                )
            );
        base.ConfigureWebHost(builder);


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
}