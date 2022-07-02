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

    public async Task SendEventAsync(EventDto @event, CancellationToken cancellationToken = default)
    {
        using var content = new ReadOnlyMemoryContent(@event.Payload);
        using var response = await httpClient.PostAsync($"api/events?type={@event.Type}", content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddSubscriptionAsync(NewSubscriptionDto newSubscription, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/subscriptions", newSubscription, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SubscriptionDto[]?> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<SubscriptionDto[]>("api/subscriptions", cancellationToken);
    }
}