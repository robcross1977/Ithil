# Ithil Demo Guide
## How to add Ithil governance to an existing ASP.NET API — step by step

This guide walks through everything a developer does to connect their API to Ithil.
The SampleApi in this folder is the finished result.

---

## What you need before you start

| Requirement | How to check |
|---|---|
| .NET 10 SDK | Run `dotnet --version` in a terminal. Should print `10.x.x`. |
| Docker Desktop running | The Docker icon in your system tray should be green. |
| An Anthropic API key | You need this to test an AI agent calling your tools. |

---

## Part 1 — Start Redis

Ithil uses Redis to track token budgets. Run this once to start it in Docker:

```bash
docker run -d -p 6379:6379 redis
```

If you see a long string of letters and numbers printed, Redis is running. If Docker says the container already exists, it's already running.

---

## Part 2 — The API (what the customer already has)

This is what a plain ASP.NET API looks like **before** Ithil is added.

**`Program.cs`** — the startup file:
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpClient("jsonplaceholder", client =>
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/"));

var app = builder.Build();
app.MapControllers();
app.Run();
```

**A controller** — a normal endpoint:
```csharp
[ApiController]
[Route("api/placeholder")]
public class JsonPlaceholderController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private HttpClient Client => httpClientFactory.CreateClient("jsonplaceholder");

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts() =>
        Ok(await Client.GetFromJsonAsync<object[]>("posts"));
}
```

Nothing special here. This is standard .NET.

---

## Part 3 — Add Ithil (3 steps)

### Step 3a — Install the package

Open a terminal in the project folder and run:

```bash
dotnet add package Ithil.Hosting
```

That's the only package needed. It installs everything — the hosting library and the code generator.

### Step 3b — Update `Program.cs`

Add two lines (marked with `// ADD THIS`):

```csharp
using Ithil.Generated;   // ADD THIS
using Ithil.Hosting;     // ADD THIS

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddIthilHosting();   // ADD THIS
builder.Services.AddHttpClient("jsonplaceholder", client =>
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/"));

var app = builder.Build();
app.MapControllers();
app.MapIthilSchema(SchemaRegistry.Tools);   // ADD THIS
app.Run();
```

`AddIthilHosting()` registers Ithil's services.
`MapIthilSchema(SchemaRegistry.Tools)` adds a `/ithil/schema` endpoint that the gateway uses to discover your tools.

### Step 3c — Mark which endpoints the AI can call

Add `[AgentTool("description")]` to any controller method you want to expose:

```csharp
using Ithil.Attributes;   // ADD THIS

[AgentTool("Returns all posts")]   // ADD THIS
[HttpGet("posts")]
public async Task<IActionResult> GetPosts() =>
    Ok(await Client.GetFromJsonAsync<object[]>("posts"));
```

The description is what the AI sees when deciding which tool to call. Write it like you're explaining to a person.

For write operations (POST, PUT, DELETE), add `AllowWrite = true`:

```csharp
[AgentTool("Creates a new post", AllowWrite = true)]
[HttpPost("posts")]
public async Task<IActionResult> CreatePost(...)
```

**Build the project** after adding the attributes:

```bash
dotnet build
```

The build generates `SchemaRegistry.Tools` automatically from your `[AgentTool]` attributes.

---

## Part 4 — Start the gateway

The gateway sits in front of your API and handles authentication, token budgets, and MCP.

```bash
docker run -d \
  -p 5100:8080 \
  -e Ithil__LicenseKey="your-license-key" \
  -e Ithil__Jwt__SigningKey="your-32-character-or-longer-key-here" \
  -e Ithil__ToolRegistry__DownstreamBaseUrl="http://host.docker.internal:5200" \
  -e ConnectionStrings__Redis="host.docker.internal:6379" \
  ithilsoftware/gateway:latest
```

**What each setting means:**
- `Ithil__LicenseKey` — your license key from [ithil.software/register](https://ithil.software/register). Free non-commercial keys are valid for 30 days.
- `Ithil__Jwt__SigningKey` — a secret string (at least 32 characters) used to sign agent tokens. Use anything long and random for a demo.
- `Ithil__ToolRegistry__DownstreamBaseUrl` — where your API is running. `host.docker.internal` lets Docker reach your local machine.
- `ConnectionStrings__Redis` — where Redis is running.

**Start your API** (in a separate terminal, in the SampleApi folder):

```bash
dotnet run --urls "http://localhost:5200"
```

---

## Part 5 — Verify it works

### Check the gateway discovered your tools

```bash
curl http://localhost:5100/ithil/schema
```

You should see a JSON list of all your `[AgentTool]`-decorated methods.

### Check the MCP endpoint is live

```bash
curl http://localhost:5100/.well-known/mcp
```

This is the URL you give to Claude or any MCP-compatible AI agent.

---

## Part 6 — Connect an AI agent

Point Claude Desktop (or any MCP client) at:

```
http://localhost:5100/.well-known/mcp
```

The agent will see your tools by name and description, and can call them through the gateway — with full token budget enforcement, PII filtering, and audit logging applied automatically.

---

## What just happened (the 30-second explanation for customers)

Your API didn't change. You added one package, four lines of code, and some annotations. Everything else — authentication, budgets, privacy filtering, audit logs — is handled by the gateway. When you're ready for production, swap the Docker run command for a Kubernetes deployment and point `DownstreamBaseUrl` at your real service.
