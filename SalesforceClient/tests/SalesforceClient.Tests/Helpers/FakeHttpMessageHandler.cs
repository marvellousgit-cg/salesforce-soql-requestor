using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SalesforceClient.Tests.Helpers;

/// <summary>
/// Test-only HttpMessageHandler that returns pre-configured responses in order,
/// cycling back to the last response once the queue is exhausted.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    internal FakeHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        foreach (var r in responses)
            _responses.Enqueue(r);
    }

    internal static HttpResponseMessage JsonOk(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    internal static HttpResponseMessage Error(HttpStatusCode status, string body = "")
        => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.InternalServerError)
              { Content = new StringContent("No more queued responses.") };

        return Task.FromResult(response);
    }
}
