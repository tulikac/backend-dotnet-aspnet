using System.Net;
using System.Reflection;

const string appName = "backend-dotnet-aspnet";

var builder = WebApplication.CreateBuilder(args);
var configuredPort = ResolvePort(Environment.GetEnvironmentVariable("PORT"));

if (configuredPort is not null)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{configuredPort}");
}

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
});

app.MapGet("/", () => Results.Content(
    Page(
        "ASP.NET Core backend is running",
        """<p>The application started successfully and is serving HTTP traffic.</p><p><a href="/products/widget-1">Open the deep route</a></p>"""),
    "text/html"));

app.MapGet("/products/widget-1", () => Results.Content(
    Page(
        "Widget 1",
        """<p>This page proves that direct navigation to a server-rendered deep route works.</p><p><a href="/">Return home</a></p>"""),
    "text/html"));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    app = appName,
    deploymentMarker = Environment.GetEnvironmentVariable("DEPLOYMENT_MARKER") ?? "local"
}));

app.MapGet("/api/status", () => Results.Ok(new
{
    status = "available",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/api/version", () => Results.Ok(DeploymentInfo()));

app.MapGet("/api/request", (HttpRequest request) => Results.Ok(new
{
    method = request.Method,
    path = request.Path.Value,
    headers = new
    {
        host = request.Headers.Host.FirstOrDefault(),
        userAgent = request.Headers.UserAgent.FirstOrDefault(),
        forwardedProto = request.Headers["X-Forwarded-Proto"].FirstOrDefault()
    }
}));

app.MapFallback((HttpRequest request) =>
{
    if (request.Path.StartsWithSegments("/api"))
    {
        return Results.Json(
            new
            {
                error = "not_found",
                message = "The requested API route does not exist."
            },
            statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Content(
        Page(
            "Page not found",
            """<p>The requested page does not exist.</p><p><a href="/">Return home</a></p>"""),
        "text/html",
        statusCode: StatusCodes.Status404NotFound);
});

app.Run();

static int? ResolvePort(string? rawPort)
{
    if (string.IsNullOrWhiteSpace(rawPort))
    {
        return null;
    }

    if (!int.TryParse(rawPort, out var port) || port is < 1 or > 65535)
    {
        throw new InvalidOperationException(
            $"PORT must be an integer between 1 and 65535; received \"{rawPort}\".");
    }

    return port;
}

static object DeploymentInfo()
{
    var assemblyVersion = Assembly.GetExecutingAssembly()
        .GetName()
        .Version?
        .ToString(3) ?? "1.0.0";

    return new
    {
        app = appName,
        version = assemblyVersion,
        framework = "ASP.NET Core",
        runtime = Environment.Version.ToString(),
        deploymentMarker = Environment.GetEnvironmentVariable("DEPLOYMENT_MARKER") ?? "local",
        buildMarker = Environment.GetEnvironmentVariable("BUILD_MARKER") ?? "local",
        runtimeMarker = Environment.GetEnvironmentVariable("RUNTIME_MARKER") ?? "local"
    };
}

static string Page(string title, string content)
{
    var info = new Dictionary<string, string>
    {
        ["Application"] = appName,
        ["Framework"] = "ASP.NET Core",
        ["Runtime"] = $".NET {Environment.Version}",
        ["Deployment"] = Environment.GetEnvironmentVariable("DEPLOYMENT_MARKER") ?? "local",
        ["Build setting"] = Environment.GetEnvironmentVariable("BUILD_MARKER") ?? "local",
        ["Runtime setting"] = Environment.GetEnvironmentVariable("RUNTIME_MARKER") ?? "local"
    };

    var details = string.Join(
        Environment.NewLine,
        info.Select(pair =>
            $"<div><dt>{WebUtility.HtmlEncode(pair.Key)}</dt><dd>{WebUtility.HtmlEncode(pair.Value)}</dd></div>"));

    return $$"""
        <!doctype html>
        <html lang="en">
          <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{WebUtility.HtmlEncode(title)}}</title>
            <link rel="stylesheet" href="/styles.v1.css">
          </head>
          <body>
            <main>
              <p class="eyebrow">Builder Apps shape test</p>
              <h1>{{WebUtility.HtmlEncode(title)}}</h1>
              {{content}}
              <dl>
                {{details}}
              </dl>
            </main>
          </body>
        </html>
        """;
}

public partial class Program;
