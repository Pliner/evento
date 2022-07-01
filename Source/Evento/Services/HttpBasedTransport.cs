using Evento.Repositories.Subscription;
using Microsoft.AspNetCore.WebUtilities;

namespace Evento.Services;

public sealed class HttpBasedTransport : IDirectTransport
{
    private readonly HttpClient httpClient;

    public HttpBasedTransport(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task SendAsync(Subscription subscription, Event @event, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            { "type", @event.Type },
        };
        using var payload = new ReadOnlyMemoryContent(@event.Payload);
        using var response = await httpClient.PostAsync(QueryHelpers.AddQueryString(subscription.Endpoint, parameters), payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}