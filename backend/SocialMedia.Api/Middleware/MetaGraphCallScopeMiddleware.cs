using SocialMedia.Application.Meta;

namespace SocialMedia.Api.Middleware;

public sealed class MetaGraphCallScopeMiddleware
{
    private readonly RequestDelegate _next;

    public MetaGraphCallScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var scope = MetaGraphCallScope.BeginRequest();
        await _next(context);
    }
}
