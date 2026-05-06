# Ithil Demo Guide
## How to add Ithil governance to an existing ASP.NET API — step by step

This guide walks through everything a developer does to connect their API to Ithil.
The SampleApi in this folder is the **starting point** — a plain .NET API with no Ithil integration. Follow the steps below to add Ithil yourself.

---

## What you need before you start

| Requirement | How to check |
|---|---|
| .NET 10 SDK | Run `dotnet --version` in a terminal. Should print `10.x.x`. |
| Docker Desktop running | The Docker icon in your system tray should be green. |
| An Ithil license key | Register for free at [ithil.software/register](https://ithil.software/register). Non-commercial keys are valid for 30 days. |

---

## Part 1 — Start Redis

Ithil uses Redis to track token budgets. Run this once to start it in Docker:

```bash
docker run -d -p 6379:6379 redis
```

If you see a long string of letters and numbers printed, Redis is running. If Docker says the container already exists, it's already running.

To confirm Redis is accepting connections, find the container name from `docker ps` and run:

```bash
docker exec <redis-container-name> redis-cli ping
```

You should see `PONG`.

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

Add four lines (marked with `// ADD THIS`):

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

[AgentTool("Returns a single post by ID")]   // ADD THIS
[HttpGet("posts/{id:int}")]
public async Task<IActionResult> GetPost(int id) =>
    Ok(await Client.GetFromJsonAsync<object>($"posts/{id}"));
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

### Confirm the schema endpoint works

Start the API:

```bash
dotnet run --urls "http://localhost:5200"
```

Then in a second terminal:

```bash
curl http://localhost:5200/ithil/schema
```

You should see a JSON array of your `[AgentTool]`-decorated methods. This is the endpoint the gateway polls to discover your tools.

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
- `Ithil__LicenseKey` — your license key from [ithil.software/register](https://ithil.software/register).
- `Ithil__Jwt__SigningKey` — a secret string (at least 32 characters) used to sign agent tokens. Use anything long and random for a demo.
- `Ithil__ToolRegistry__DownstreamBaseUrl` — where your API is running. `host.docker.internal` lets Docker reach your local machine.
- `ConnectionStrings__Redis` — where Redis is running.

Check the gateway is healthy:

```bash
curl http://localhost:5100/health/ready
```

You should see `Healthy`.

---

## Part 5 — Verify the MCP endpoint works

The gateway exposes an MCP endpoint at `/mcp`. AI agents connect to this URL.

### Step 5a — Get a short-lived agent token

In development, use the built-in token endpoint (only works from localhost):

```bash
curl http://localhost:5100/dev/token?agentId=my-agent
```

Copy the `token` value from the response.

### Step 5b — Initialize an MCP session

```bash
curl -si http://localhost:5100/mcp \
  -H "Authorization: Bearer <token-from-above>" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"0.1"}}}'
```

Copy the `Mcp-Session-Id` value from the response headers.

### Step 5c — List your tools

```bash
curl -s http://localhost:5100/mcp \
  -H "Authorization: Bearer <token-from-above>" \
  -H "Mcp-Session-Id: <session-id-from-step-b>" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
```

You should see your `[AgentTool]`-decorated methods listed by name and description.

### Step 5d — Make a tool call

```bash
curl -s http://localhost:5100/mcp \
  -H "Authorization: Bearer <token-from-above>" \
  -H "Mcp-Session-Id: <session-id-from-step-b>" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
      "name": "GetPost",
      "arguments": { "id": 1 }
    }
  }'
```

You will see the post data in the response. If the user record includes an email address, notice it will appear as `[EMAIL REDACTED]` — the gateway's PII filter applied automatically, with no changes to your API code.

---

## Part 6 — Connect Claude Desktop

Claude Desktop supports MCP servers over HTTP. Point it at the gateway's `/mcp` endpoint.

### Step 6a — Get a token

```bash
curl http://localhost:5100/dev/token?agentId=claude-desktop
```

Copy the `token` value. Tokens are valid for 8 hours; repeat this step if Claude stops seeing your tools.

### Step 6b — Edit the Claude Desktop config file

Open the config file in a text editor:

| Platform | Path |
|----------|------|
| Mac | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |

Add the `mcpServers` block (create the file if it doesn't exist):

```json
{
  "mcpServers": {
    "ithil": {
      "url": "http://localhost:5100/mcp",
      "headers": {
        "Authorization": "Bearer YOUR_TOKEN_HERE"
      }
    }
  }
}
```

Replace `YOUR_TOKEN_HERE` with the token from Step 6a.

### Step 6c — Restart Claude Desktop

Quit and reopen Claude Desktop. You should see a tools icon in the message bar showing your API's tools are connected.

Ask Claude something like:

> "Fetch post number 5 for me"

Claude will call `GetPost` through the Ithil gateway — with budget enforcement, PII filtering, and audit logging applied transparently.

---

## What just happened (the 30-second explanation for customers)

Your API didn't change. You added one package, four lines of code, and some annotations. Everything else — authentication, budgets, privacy filtering, audit logs — is handled by the gateway. When you're ready for production, swap the Docker run command for a Kubernetes deployment and point `DownstreamBaseUrl` at your real service.
