using EasyNetQ;
using EasyNetQ.ConnectionString;
using Evento.Infrastructure;
using Evento.Repositories.Subscription;
using Evento.Services;
using Evento.Services.PubSub;
using Evento.Services.SubscriptionRegistry;
using Evento.Services.Transport;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddLogging(c => c.AddConsole());
builder.Services.AddSingleton<ISubscriptionRepository, InMemorySubscriptionRepository>();
builder.Services.AddSingleton<IEventTransport, EventTransport>();
builder.Services.AddSingleton<ISubscriptionRegistry, SubscriptionRegistry>();
builder.Services.AddSingleton<IEventPubSub, RmqBasedPubSub>();
builder.Services.AddHttpClient<EventTransport>();
builder.Services.RegisterEasyNetQ(
    c =>
    {
        var configuration = c.Resolve<IConfiguration>();
        var parameters = new Dictionary<string, string>
        {
            { "host", configuration["RMQ_HOSTS"] ?? "localhost" },
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

app.Run();