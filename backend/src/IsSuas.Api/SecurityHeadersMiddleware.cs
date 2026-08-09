namespace IsSuas.Api;

/// <summary>Basic security headers (F-3.5). CSP is relaxed in Development for Swagger.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        if (env.IsDevelopment())
            headers["Content-Security-Policy"] =
                "default-src 'self' 'unsafe-inline' 'unsafe-eval' data: blob:; img-src 'self' data: https:; connect-src 'self'";
        else
            headers["Content-Security-Policy"] =
                "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; connect-src 'self'";

        await next(context);
    }
}
