using Evento.Repositories.Subscription;
using Evento.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddLogging(c => c.AddConsole());
builder.Services.AddSingleton<ISubscriptionRepository, InMemorySubscriptionRepository>();
builder.Services.AddSingleton<IEventTransport, HttpEventTransport>();
builder.Services.AddHttpClient<HttpEventTransport>("EventTransport");

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
