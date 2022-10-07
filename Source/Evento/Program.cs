using EasyNetQ;
using EasyNetQ.ConnectionString;
using Evento.Db;
using Evento.Infrastructure;
using Evento.Repositories.Subscription;
using Evento.Services;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddLogging(c => c.AddConsole());
builder.Services.AddSingleton(s =>
{
    var configuration = s.GetRequiredService<IConfiguration>();
    return new RmqBasedTransportOptions
    {
        ExchangeName = configuration["EXCHANGE_NAME"] ?? "events"
    };
});
builder.Services.AddSingleton<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddSingleton<IDirectTransport, HttpBasedTransport>();
builder.Services.AddSingleton<IPublishSubscribe, RmqBasedPublishSubscribe>();
builder.Services.AddHttpClient<IDirectTransport, HttpBasedTransport>(c => c.Timeout = TimeSpan.FromSeconds(60))
    .AddPolicyHandler(
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                Backoff.DecorrelatedJitterBackoffV2(
                    medianFirstRetryDelay: builder.Environment.IsProduction()
                        ? TimeSpan.FromSeconds(1)
                        : TimeSpan.FromSeconds(0.1),
                    retryCount: 3,
                    fastFirst: true
                )
            )
    )
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(builder.Environment.IsProduction() ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1)))
    .UseHttpClientMetrics();
builder.Services.AddSingleton<IMetricFactory>(Metrics.WithCustomRegistry(Metrics.DefaultRegistry));
builder.Services.RegisterEasyNetQ(
    c =>
    {
        var configuration = c.Resolve<IConfiguration>();
        var parameters = new Dictionary<string, string>
        {
            { "host", configuration["RABBITMQ_HOST"] ?? "rmq" },
            { "username", configuration["RABBITMQ_USER"] ?? "guest" },
            { "password", configuration["RABBITMQ_PASSWORD"] ?? "guest" },
            { "virtualHost", configuration["RABBITMQ_VHOST"] ?? "/" },
            { "publisherConfirms", "True" },
            { "consumerDispatcherConcurrency", "1" },
            { "prefetchCount", "50" }
        };
        var connectionString = string.Join(";", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return c.Resolve<IConnectionStringParser>().Parse(connectionString);
    },
    c => c.EnableMicrosoftLogging()
        .EnableNewtonsoftJson()
        .EnableAlwaysNackWithRequeueConsumerErrorStrategy()
);
builder.Services.AddPeriodicJob<SubscriptionsManager>();
builder.Services.AddDbContextFactory<EventoDbContext>(
    (s, o) => o.SetupPostgresql(s.GetRequiredService<IConfiguration>())
);

var app = builder.Build();
app.UseSwagger();
app.UseHttpMetrics();
app.MapMetrics();
app.MapHealthChecks("/healthcheck");
app.MapControllers();
app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    }
);
await app.RunAsync();