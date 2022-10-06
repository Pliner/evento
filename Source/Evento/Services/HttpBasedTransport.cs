using System.Net.Http.Headers;
using Evento.Repositories.Subscription;
using Microsoft.AspNetCore.WebUtilities;

namespace Evento.Services;

public sealed class HttpBasedTransport : IDirectTransport
{
    private readonly HttpClient httpClient;

    public HttpBasedTransport(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task SendAsync(
        Subscription subscription,
        EventProperties properties,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default
    )
    {
        using var content = new ReadOnlyMemoryContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue(properties.ContentType);

        var uri = QueryHelpers.AddQueryString(subscription.Endpoint, new Dictionary<string, string?> { { "type", properties.Type } });
        using var response = await httpClient.PostAsync(uri, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}