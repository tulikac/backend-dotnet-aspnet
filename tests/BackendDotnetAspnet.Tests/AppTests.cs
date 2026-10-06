using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;

namespace BackendDotnetAspnet.Tests;

public sealed class AppTests : IClassFixture<RunningApp>
{
    private readonly HttpClient _client;

    public AppTests(RunningApp app)
    {
        _client = app.Client;
    }

    [Fact]
    public async Task ServesLandingPageAndDeepRoute()
    {
        var home = await _client.GetAsync("/");
        var deepRoute = await _client.GetAsync("/products/widget-1");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("ASP.NET Core backend is running", await home.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, deepRoute.StatusCode);
        Assert.Contains("Widget 1", await deepRoute.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReportsHealthAndDeploymentIdentity()
    {
        var health = await _client.GetFromJsonAsync<Dictionary<string, object>>("/health");
        var version = await _client.GetFromJsonAsync<Dictionary<string, object>>("/api/version");

        Assert.Equal("ok", health!["status"].ToString());
        Assert.Equal("backend-dotnet-aspnet", health["app"].ToString());
        Assert.Equal("backend-dotnet-aspnet", version!["app"].ToString());
        Assert.Equal("ASP.NET Core", version["framework"].ToString());
        Assert.NotEmpty(version["runtime"].ToString()!);
    }

    [Fact]
    public async Task ReturnsSafeRequestMetadata()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/request");
        request.Headers.UserAgent.ParseAdd("builder-app-shape-test");
        request.Headers.Authorization = new("Bearer", "must-not-be-returned");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<RequestMetadata>();

        Assert.Equal("GET", body!.Method);
        Assert.Equal("/api/request", body.Path);
        Assert.Equal("builder-app-shape-test", body.Headers.UserAgent);
    }

    [Fact]
    public async Task DistinguishesPageAndApiMisses()
    {
        var page = await _client.GetAsync("/missing-page");
        var api = await _client.GetAsync("/api/missing");
        var apiBody = await api.Content.ReadFromJsonAsync<ApiError>();

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Contains("Page not found", await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
        Assert.Equal("not_found", apiBody!.Error);
        Assert.Equal("The requested API route does not exist.", apiBody.Message);
    }

    private sealed record RequestMetadata(
        string Method,
        string Path,
        RequestHeaders Headers);

    private sealed record RequestHeaders(
        string? Host,
        string? UserAgent,
        string? ForwardedProto);

    private sealed record ApiError(string Error, string Message);
}

public sealed class RunningApp : IAsyncLifetime
{
    private readonly StringBuilder _output = new();
    private Process? _process;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var port = FindAvailablePort();
        var appAssembly = typeof(Program).Assembly.Location;

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(appAssembly)!,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(appAssembly);
        startInfo.Environment["PORT"] = port.ToString();
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";

        _process = new Process { StartInfo = startInfo };
        _process.OutputDataReceived += CaptureOutput;
        _process.ErrorDataReceived += CaptureOutput;

        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start the ASP.NET Core test process.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        Client = new HttpClient { BaseAddress = new($"http://127.0.0.1:{port}") };

        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The ASP.NET Core test process exited unexpectedly.{Environment.NewLine}{_output}");
            }

            try
            {
                var response = await Client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"The ASP.NET Core test process did not become healthy.{Environment.NewLine}{_output}");
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (_process is null)
        {
            return;
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }

    private void CaptureOutput(object sender, DataReceivedEventArgs args)
    {
        if (args.Data is not null)
        {
            _output.AppendLine(args.Data);
        }
    }

    private static int FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
