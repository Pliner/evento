using Microsoft.AspNetCore.WebUtilities;

namespace Evento.Services.Transport;

public sealed class EventTransport : IEventTransport
{
    private readonly HttpClient httpClient;

    public EventTransport(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task TransmitAsync(string destination, Event @event, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            { "type", @event.Type }
        };
        using var payload = new ReadOnlyMemoryContent(@event.Payload);
        using var response = await httpClient.PostAsync(QueryHelpers.AddQueryString(destination, parameters), payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}