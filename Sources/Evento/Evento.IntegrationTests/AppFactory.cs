using System.Collections.Concurrent;
using System.Globalization;
using Evento.Services;
using JustEat.HttpClientInterception;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Evento.Tests;

internal class AppFactory : WebApplicationFactory<Program>
{
    private readonly RmqSettings rmqSettings;
    private readonly HttpClientInterceptorOptions httpClientInterceptorOptions = new() { ThrowOnMissingRegistration = true };
    private readonly ConcurrentQueue<Event> events = new();

    public IReadOnlyList<Event> ReceivedEvents => events.ToList();

    public AppFactory(RmqSettings rmqSettings)
    {
        this.rmqSettings = rmqSettings;
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
                            var id = query["id"].Single();
                            var type = query["type"].Single();
                            var timestamp = DateTime.Parse(query["timestamp"].Single(), DateTimeFormatInfo.InvariantInfo, DateTimeStyles.RoundtripKind);
                            var payload = await (m.Content ?? new ByteArrayContent(Array.Empty<byte>())).ReadAsByteArrayAsync(c);
                            var @event = new Event(id, type, timestamp, payload.AsMemory());
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
            ).AddSingleton(rmqSettings)
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