# ASP.NET Core Backend Sample

A small ASP.NET Core minimal API with no external service dependencies.

## Requirements

- .NET 8 SDK

## Run locally

```powershell
dotnet run --project .\src\BackendDotnetAspnet
```

Set `PORT` to control the HTTP port. Without `PORT`, ASP.NET Core uses its
normal URL configuration, including the local launch profile during
`dotnet run`.

```powershell
$env:PORT = "8080"
dotnet run --project .\src\BackendDotnetAspnet --no-launch-profile
```

Open `http://localhost:8080`.

## Verify

```powershell
dotnet test
.\scripts\verify.ps1 -BaseUrl http://localhost:8080
```

## Publish

```powershell
dotnet publish .\src\BackendDotnetAspnet -c Release -o .\publish
.\publish\BackendDotnetAspnet.exe
```

## Builder Apps configuration

The root `builder.yaml` declares one public .NET web component. Its
`rootDirectory` points to the ASP.NET Core project so the test project is not
part of the deployed component. The application starts from the compiled
`BackendDotnetAspnet.dll`, listens on port `8080`, and reports readiness through
`/health`.

## Routes

| Route | Purpose |
|---|---|
| `GET /` | Sample identity and deployment markers |
| `GET /products/widget-1` | Direct deep-link test |
| `GET /health` | Runtime health |
| `GET /api/status` | API reachability |
| `GET /api/version` | App, framework, runtime, and deployment identity |
| `GET /api/request` | Safe request metadata |
