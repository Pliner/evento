using Microsoft.AspNetCore.WebUtilities;

namespace Evento.Services;

public sealed class HttpEventTransport : IEventTransport
{
    private readonly HttpClient httpClient;

    public HttpEventTransport(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task TransmitAsync(string destination, Event @event, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "id", @event.Id },
            { "type", @event.Type },
            { "timestamp", @event.Timestamp.ToString("O") },
        };
        using var payload = new ReadOnlyMemoryContent(@event.Payload);
        using var response = await httpClient.PostAsync(QueryHelpers.AddQueryString(destination, parameters), payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}