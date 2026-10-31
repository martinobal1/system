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
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(self)";
        if (env.IsDevelopment())
            headers["Content-Security-Policy"] =
                "default-src 'self' 'unsafe-inline' 'unsafe-eval' data: blob:; " +
                "img-src 'self' data: https: blob:; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com https://cdn.jsdelivr.net; " +
                "font-src 'self' data: https://fonts.gstatic.com; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://unpkg.com https://cdn.jsdelivr.net; " +
                "connect-src 'self' https://gis.lps.sk https://nominatim.openstreetmap.org https://services.arcgisonline.com https://server.arcgisonline.com https://services9.arcgis.com https://unpkg.com; " +
                "worker-src 'self' blob:; frame-src 'self' blob:";
        else
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "img-src 'self' data: https: blob:; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com https://cdn.jsdelivr.net; " +
                "font-src 'self' data: https://fonts.gstatic.com; " +
                "script-src 'self' 'unsafe-inline' https://unpkg.com https://cdn.jsdelivr.net; " +
                "connect-src 'self' https://gis.lps.sk https://nominatim.openstreetmap.org https://services.arcgisonline.com https://server.arcgisonline.com https://services9.arcgis.com https://unpkg.com; " +
                "worker-src 'self' blob:; frame-src 'self' blob:";

        await next(context);
    }
}
