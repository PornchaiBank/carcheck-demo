using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5190");
builder.Services.AddHttpClient("line-webhook", client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:5188");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();
app.MapPost("/api/line/webhook", async (HttpRequest request, HttpResponse clientResponse, IHttpClientFactory clients, CancellationToken token) =>
{
    if (request.ContentLength is > 1_048_576)
    {
        clientResponse.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return;
    }

    using var upstream = new HttpRequestMessage(HttpMethod.Post, "/api/line/webhook");
    upstream.Content = new StreamContent(request.Body);
    if (!string.IsNullOrWhiteSpace(request.ContentType))
        upstream.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
    if (request.Headers.TryGetValue("x-line-signature", out var signature))
        upstream.Headers.TryAddWithoutValidation("x-line-signature", signature.ToArray());

    using var response = await clients.CreateClient("line-webhook").SendAsync(upstream, HttpCompletionOption.ResponseHeadersRead, token);
    clientResponse.StatusCode = (int)response.StatusCode;
    clientResponse.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
    await response.Content.CopyToAsync(clientResponse.Body, token);
});
app.MapFallback(() => Results.NotFound());
app.Run();
