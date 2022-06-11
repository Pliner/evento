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
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddLogging(c => c.AddConsole());
builder.Services.AddSingleton<ISubscriptionRepository, InMemorySubscriptionRepository>();
builder.Services.AddSingleton<IEventTransport, EventTransport>();
builder.Services.AddSingleton<ISubscriptionRegistry, SubscriptionRegistry>();
builder.Services.AddSingleton<IEventPubSub, RmqBasedPubSub>();
builder.Services.AddHttpClient<EventTransport>();
builder.Services.AddSingleton(new RmqSettings("rmq", 5672));
builder.Services.AddSingleton<ActiveSubscriptionsManager>();
builder.Services.RegisterEasyNetQ(
    c =>
    {
        var settings = c.Resolve<RmqSettings>();
        var connectionString = $"host={settings.Host};port={settings.Port};publisherConfirms=True";
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

public record RmqSettings(string Host, int Port);