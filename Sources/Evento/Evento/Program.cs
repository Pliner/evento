using EasyNetQ;
using EasyNetQ.ConnectionString;
using Evento.Db;
using Evento.Infrastructure;
using Evento.Repositories.Subscription;
using Evento.Services;
using Evento.Services.PubSub;
using Evento.Services.SubscriptionRegistry;
using Evento.Services.Transport;
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
builder.Services.AddSingleton<ISubscriptionRepository, DbSubscriptionRepository>();
builder.Services.AddSingleton<IEventTransport, EventTransport>();
builder.Services.AddSingleton<ISubscriptionRegistry, SubscriptionRegistry>();
builder.Services.AddSingleton<IEventPubSub, RmqBasedPubSub>();
builder.Services.AddHttpClient<EventTransport>(c => c.Timeout = TimeSpan.FromSeconds(20))
    .AddPolicyHandler(
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                Backoff.DecorrelatedJitterBackoffV2(
                    medianFirstRetryDelay: TimeSpan.FromSeconds(1),
                    retryCount: 5,
                    fastFirst: true
                )
            )
    );
builder.Services.RegisterEasyNetQ(
    c =>
    {
        var configuration = c.Resolve<IConfiguration>();
        var parameters = new Dictionary<string, string>
        {
            { "host", configuration["RMQ_HOSTS"] ?? "rmq" },
            { "username", configuration["RMQ_USER"] ?? "guest" },
            { "password", configuration["RMQ_PASSWORD"] ?? "guest" },
            { "virtualHost", configuration["RMQ_VHOST"] ?? "/" },
            { "publisherConfirms", "True" }
        };
        var connectionString = string.Join(";", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return c.Resolve<IConnectionStringParser>().Parse(connectionString);
    },
    c => c.EnableMicrosoftLogging()
        .EnableNewtonsoftJson()
        .EnableAlwaysNackWithRequeueConsumerErrorStrategy()
);
builder.Services.AddPeriodicJob<ActiveSubscriptionsManager>();
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