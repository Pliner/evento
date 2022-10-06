using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Evento.Client;

public class EventoClient : IEventoClient
{
    private readonly HttpClient httpClient;

    public EventoClient(HttpClient httpClient)
    {
        if (httpClient.BaseAddress is null)
            throw new ArgumentOutOfRangeException(nameof(httpClient.BaseAddress), null, "BaseAddress should be filled in");

        this.httpClient = httpClient;
    }

    public async Task SendEventAsync(EventPropertiesDto properties, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        using var content = new ReadOnlyMemoryContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue(properties.ContentType);
        using var response = await httpClient.PostAsync($"api/events?type={properties.Type}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddSubscriptionAsync(NewSubscriptionDto newSubscription, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/subscriptions", newSubscription, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string[]?> GetSubscriptionsNamesAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<string[]>("api/subscriptions", cancellationToken);
    }


    public async Task<SubscriptionDto?> GetSubscriptionByNameAsync(string subscriptionName, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<SubscriptionDto>($"api/subscriptions/{subscriptionName}", cancellationToken);
    }
}