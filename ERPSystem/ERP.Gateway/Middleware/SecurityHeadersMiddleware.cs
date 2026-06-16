namespace ERP.Gateway.Middleware;

public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["X-XSS-Protection"] = "1; mode=block";
            headers["Referrer-Policy"] = "no-referrer";

            // =========================
            // DEV CSP (Angular + Swagger)
            // =========================
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +

                // scripts (Angular dev + Swagger)
                "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +

                // styles
                "style-src 'self' 'unsafe-inline'; " +

                // images
                "img-src 'self' data: blob:; " +

                // fonts
                "font-src 'self' data:; " +

                // API + Angular dev server
                "connect-src 'self' http://localhost:4200 http://*.erp.local;" +

                // block embedding
                "frame-ancestors 'none'; " +

                // extra protections
                "object-src 'none'; " +
                "base-uri 'self';";

            // =========================
            // ✅ PRODUCTION CSP (STRICT)
            // =========================
            /*
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +

                // scripts (NO inline/eval in production)
                "script-src 'self'; " +

                // styles (remove unsafe-inline if possible)
                "style-src 'self'; " +

                // images (no data/blob unless required)
                "img-src 'self'; " +

                // fonts
                "font-src 'self'; " +

                // API only (no localhost)
                "connect-src 'self'; " +

                // security protections
                "frame-ancestors 'none'; " +
                "object-src 'none'; " +
                "base-uri 'self';";
            */

            // Enforce HTTPS (only works over HTTPS)
            if (context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] =
                    "max-age=31536000; includeSubDomains; preload";
            }

            await next();
        });
    }
}