# Ithil.Hosting

Add this package to your existing ASP.NET Web API to expose your `[AgentTool]`-decorated controller methods to the [Ithil Gateway](https://ithil.software).

## Installation

```bash
dotnet add package Ithil.Hosting
```

## Quick setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddIthilHosting();

var app = builder.Build();

app.MapControllers();
app.MapIthilSchema(SchemaRegistry.Tools);

app.Run();
```

`SchemaRegistry` is generated at compile time by the Ithil source generator. It contains one entry for every method decorated with `[AgentTool]`.

## Documentation

Full documentation at [ithil.software/installation/downstream](https://ithil.software/installation/downstream).
