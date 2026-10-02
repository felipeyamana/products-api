using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ProductsApi.Common;
using Xunit;

namespace ProductsApi.UnitTests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsProblemDetailsWithTraceIdentifier()
    {
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id"
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("Sensitive internal detail"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        var root = response.RootElement;

        Assert.Equal("An unexpected error occurred.", root.GetProperty("title").GetString());
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("test-trace-id", root.GetProperty("traceId").GetString());
        Assert.DoesNotContain("Sensitive internal detail", root.GetRawText(), StringComparison.Ordinal);
    }
}
