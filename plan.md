# Ithil — Detailed Build & Go-To-Market Plan

**Tagline:** The Agentic Governance Layer for Enterprise .NET Backends
**Target Year:** 2026
**Stack:** C# / .NET 9+, TypeScript / Next.js 15, Redis, YARP, SignalR, MCP

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Problem & Market Context](#2-problem--market-context)
3. [Product Architecture](#3-product-architecture)
4. [Core Concepts & Primitives](#4-core-concepts--primitives)
5. [Phase 1 — Core Engine (C# / .NET)](#5-phase-1--core-engine-c--net)
6. [Phase 2 — Developer Dashboard (TypeScript / Next.js)](#6-phase-2--developer-dashboard-typescript--nextjs)
7. [Phase 3 — Semantic Caching & Budget Engine](#7-phase-3--semantic-caching--budget-engine)
8. [Phase 4 — Enterprise Features](#8-phase-4--enterprise-features)
9. [Phase 5 — Real-Time Agent Tracing (SignalR)](#9-phase-5--real-time-agent-tracing-signalr)
10. [MCP Tool Schema Generation (Source Generators)](#10-mcp-tool-schema-generation-source-generators)
11. [Revenue Model & Packaging](#11-revenue-model--packaging)
12. [Azure Marketplace vs. Standalone SaaS](#12-azure-marketplace-vs-standalone-saas)
13. [Go-To-Market Strategy](#13-go-to-market-strategy)
14. [Milestones & Build Sequence](#14-milestones--build-sequence)
15. [Risk Register](#15-risk-register)

---

## 1. Executive Summary

Ithil is a .NET middleware gateway that sits in front of existing C# enterprise APIs and translates them into Model Context Protocol (MCP)-compliant Tool Servers. It makes AI agents (Claude, GPT-5, Gemini, AutoGen, OpenAI Operators) safe, observable, and cost-controlled when interacting with private enterprise data.

**Core Value Propositions:**

| Problem | Ithil Solution |
|---|---|
| AI agents hallucinate when calling messy REST APIs | Auto-generates MCP-compliant schemas from C# controllers at compile time |
| No visibility into what agents are doing inside private networks | Real-time SignalR tracing dashboard |
| Agents loop and call destructive endpoints repeatedly | Circuit breaker + per-agent daily token budgets |
| Redundant LLM calls cost thousands per month | Semantic caching in Redis keyed on intent, not raw URL |
| Enterprise teams fear PII leaking to LLMs | Privacy filter scrubs PII before data leaves the gateway |

**Revenue:** B2B SaaS. Sold per Agentic Request at $0.01 (cloud) or $500/month flat (self-hosted enterprise).

---

## 2. Problem & Market Context

### 2.1 Why Now (2026)

- More API traffic originates from AI agents than human clicks.
- AutoGen, OpenAI Operators, Anthropic Claude agents, and open-source frameworks (CrewAI, LangGraph) all consume REST APIs.
- Enterprise C#/.NET backends represent 60%+ of Fortune 500 backend infrastructure.
- MCP (Model Context Protocol) has become the gold standard for agent-to-tool communication, but there is no turnkey bridge for .NET.

### 2.2 The Gap

Existing solutions (Kong, Apigee, AWS API Gateway) handle human web traffic. None of them understand:
- Agent identity and budgets
- Token cost per request
- MCP schema negotiation
- Semantic deduplication of intent

### 2.3 Buyer Personas

| Persona | Pain | What They Buy |
|---|---|---|
| Enterprise Architect | Agents touching prod databases with zero guardrails | Self-hosted Docker container, OIDC integration |
| Platform Engineering Lead | Building internal AI tooling on top of .NET microservices | CLI tool + NuGet package + dashboard |
| AI Product Manager | Can't see what agents are doing, can't justify the LLM bill | The TypeScript Dashboard (Agent Command Center) |
| ISV / SaaS Builder | Building multi-tenant AI products on .NET backends | Cloud SaaS, consumption billing |

---

## 3. Product Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        AI Agents                            │
│         (Claude / GPT-5 / AutoGen / Custom)                 │
└────────────────────────┬────────────────────────────────────┘
                         │  JSON-RPC 2.0 over HTTP/SSE (MCP)
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   Ithil Gateway                        │
│  ┌──────────────┐  ┌────────────────┐  ┌────────────────┐  │
│  │  MCP Router  │  │ Budget Engine  │  │ Privacy Filter │  │
│  │  (YARP Core) │  │ (Token Ledger) │  │  (PII Scrub)   │  │
│  └──────────────┘  └────────────────┘  └────────────────┘  │
│  ┌──────────────┐  ┌────────────────┐  ┌────────────────┐  │
│  │  Semantic    │  │Circuit Breaker │  │ Schema Registry│  │
│  │  Cache(Redis)│  │  (Polly)       │  │ (Source Gen)   │  │
│  └──────────────┘  └────────────────┘  └────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐   │
│  │           SignalR Trace Bus (Real-Time Feed)          │   │
│  └──────────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────────┘
                         │  Internal REST / gRPC
                         ▼
┌─────────────────────────────────────────────────────────────┐
│             Existing Enterprise C# Microservices             │
│       (Inventory, Users, Orders, Finance, etc.)              │
└─────────────────────────────────────────────────────────────┘

                         ▲ Management API (JSON)
                         │
┌─────────────────────────────────────────────────────────────┐
│           TypeScript / Next.js 15 Dashboard                  │
│     (Tool Library · Agent Playground · Live Trace Feed)      │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. Core Concepts & Primitives

Before reading the implementation sections, this section explains the foundational ideas that every other part of Ithil is built on. If a piece of code in sections 5–9 uses a term without explanation, the answer is here.

---

### 4.1 What is a Reverse Proxy?

Start with the simplest mental model.

**A regular proxy** (what you might use at work or school) sits in front of *you* — it intercepts your outbound requests and forwards them on your behalf. The destination server never sees your real IP.

**A reverse proxy** flips that. It sits in front of *your servers*, intercepting *inbound* requests before they ever reach your actual application. The client never talks directly to your real server.

```
WITHOUT a reverse proxy:

  AI Agent ──────────────────────────────► C# InventoryService :5001
  AI Agent ──────────────────────────────► C# OrderService     :5002
  AI Agent ──────────────────────────────► C# UserService      :5003


WITH a reverse proxy (Ithil):

  AI Agent ──► Ithil Gateway :443 ──► C# InventoryService :5001
                                       ──► C# OrderService     :5002
                                       ──► C# UserService      :5003
```

The agent only ever knows about one address. Ithil decides where the request actually goes.

**Why not let agents call your services directly?** Because then you have none of the following, and each is a real production problem:

| Problem | Consequence |
|---|---|
| No single entry point | Agents must know all 12 service URLs. When one moves, every agent breaks. |
| No enforcement point | A looping agent calls `DELETE /users` 800 times. Nothing stops it. Database gone. |
| No translation layer | Your C# service returns `qty_oh: 142`. The agent has no idea what that means. It hallucinates. |
| No observability | Agents call services directly — you have no centralized place to see what they're doing. |

The gateway is the one chokepoint where all of these are solved simultaneously.

---

### 4.2 What is YARP?

YARP stands for **Yet Another Reverse Proxy**. It is a NuGet library built by Microsoft — not a standalone application like Nginx, but a toolkit you embed directly into a .NET application.

The critical distinction: **YARP is code, not configuration.**

Nginx is a server you configure with text files. YARP is a C# library you wire into your ASP.NET Core pipeline. This means your proxy logic is just C# — you can inject services, query Redis, call databases, use dependency injection — all inside the code that handles the request.

That is why YARP is the right foundation for Ithil. Ithil isn't just forwarding requests. It is making intelligent decisions *during* the forwarding process — checking budgets, scrubbing PII, firing trace events. All of that is pure C# running inside YARP transforms.

Without YARP, you would be building the raw HTTP forwarding plumbing yourself — connection pooling, header forwarding, streaming, load balancing — thousands of lines of low-level networking code before you write a single line of your actual product.

---

### 4.3 HTTP Headers — What They Are

Every HTTP request has two parts: **headers** and a **body**.

The body is the actual data — the JSON payload, the file upload, etc.

Headers are metadata that travel alongside the request — invisible to most users but read by every server. They are key-value pairs sent at the top of every HTTP request.

You already know some headers without realizing it:

```
Content-Type: application/json        ← "my body is JSON, not a form or a file"
Authorization: Bearer eyJhbGci...     ← "here is my login token"
Accept-Language: en-US                ← "I prefer English responses"
```

Your browser sends these automatically on every request. The server reads them to understand the context of the request before it even touches the body.

**A custom header** is one you invent yourself. There is no restriction. By convention, custom headers are prefixed with `X-` to signal that they are non-standard:

```
X-Agent-Id: claude-prod-01
X-Ithil-TraceId: abc-123-def-456
X-Agent-Mode: test-manual
```

That is all they are — labels you attach to a request so something downstream can read them.

---

### 4.4 What is a Tracing Header?

Imagine a request comes into your gateway. That single request might trigger a chain:

1. Ithil receives it
2. Ithil calls InventoryService
3. InventoryService queries the database
4. InventoryService calls PricingService
5. Everything returns back up the chain

If step 4 fails, which log entry do you look at? You have logs scattered across five different places. How do you know which log lines belong to the *same original request*?

A **trace ID** solves this. It is a random unique string — like `abc-123-def-456` — that is created at the very start of the request and then passed forward to every subsequent call. Every service logs it alongside their own entries.

Now when something breaks, you search all your logs for `abc-123-def-456` and see the complete journey of that one request, in order, across every service:

```
[abc-123] Ithil received GetInventory from agent claude-prod-01
[abc-123] Ithil forwarding to InventoryService:5001
[abc-123] InventoryService queried database — 12ms
[abc-123] InventoryService called PricingService
[abc-123] PricingService — ERROR: connection timeout
[abc-123] Ithil returned 500 to agent
```

Without the trace ID, those six lines are scattered across three log files with nothing connecting them.

The way you pass the trace ID from service to service is via a **tracing header** — a custom HTTP header that carries the ID forward on every hop:

```
X-Ithil-TraceId: abc-123-def-456
```

Ithil generates it at the moment it receives a request, stamps it on the forwarded request before sending it downstream, and every downstream service receives it, logs it, and passes it along to anything it calls in turn.

---

### 4.5 Where the Agent ID Comes From

An AI agent is just code making HTTP requests. When that code calls your gateway, the gateway needs to answer: **"Who is this, and should I trust them?"**

The agent ID is how you identify *which agent* is making the request. But the gateway cannot trust whatever the caller claims — anyone could write `X-Agent-Id: god-mode` in a header. The identity has to be *verified*.

There are two common approaches:

---

#### Option A: API Keys (Simpler)

You issue a secret key to each agent when it is registered in your system — long, random, and unguessable, like a password for machines.

```
X-Api-Key: sk_live_a9f3k2m8p1q7r4s6t0u5v
```

When the gateway receives this header, it looks the key up in its database, finds the associated agent record, and knows who the caller is.

```
API Key: sk_live_a9f3k2m8p1q7r4s6t0u5v
        └──► Database lookup ──► { agentId: "claude-prod-01", budget: 50000 }
```

The downside: if someone steals the key, they can impersonate that agent with no way for you to tell the difference.

---

#### Option B: JWT Tokens (Industry Standard)

JWT stands for **JSON Web Token**. It is the modern enterprise standard.

A JWT is a self-contained, cryptographically signed blob of text. It has three parts separated by dots:

```
eyJhbGciOiJSUzI1NiJ9.eyJhZ2VudF9pZCI6ImNsYXVkZS1wcm9kLTAxIn0.SflKxw...
└──── Header ───────┘ └──────────── Payload ─────────────────────┘ └─ Signature ┘
```

The middle section (the payload) decodes to readable JSON:

```json
{
  "agent_id": "claude-prod-01",
  "allowed_tools": ["GetInventory", "GetOrders"],
  "daily_budget": 50000,
  "issued_at": "2026-02-21T09:00:00Z",
  "expires_at": "2026-02-22T09:00:00Z"
}
```

The signature at the end is created using a private key that only your authorization server holds. The gateway verifies the signature using the corresponding public key. If the signature checks out, the data in the payload is guaranteed not to have been tampered with.

This means the gateway does not need to hit a database to verify the agent on every request. It reads the payload directly, verifies the signature in memory, and trusts the contents immediately. The agent ID, its permissions, and its budget are all baked into the token itself.

The JWT travels on the standard `Authorization` header:

```
Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.eyJhZ2VudF9pZCI6...
```

**Ithil uses JWTs for enterprise deployments and API keys for the self-serve developer tier.**

---

### 4.6 The Full Request Lifecycle (End to End)

Here is the complete picture of what happens from the moment a developer registers an agent to the moment that agent's request completes:

```
REGISTRATION (one time, done in the dashboard):

  1. Developer logs into Ithil dashboard
  2. Creates agent: "claude-prod-01"
     Sets daily token budget: 50,000
     Sets allowed tools: GetInventory, GetOrders
  3. Ithil issues a JWT (or API key) for that agent
  4. Developer pastes the token into their agent's config


RUNTIME (every request):

  5. Agent code sends a request with the token attached:

     POST /mcp/tools/GetInventory
     Authorization: Bearer eyJhbGci...
     Content-Type: application/json

     { "productId": 42, "warehouseId": "UK-01" }

  6. Request hits Ithil gateway

  7. REQUEST TRANSFORM runs:
     a. Verify the JWT signature        → identifies "claude-prod-01"
     b. Check budget in Redis           → 12,400 of 50,000 tokens used today
     c. 12,400 < 50,000 → proceed
     d. Check tool allowlist            → GetInventory is permitted
     e. Generate trace ID               → "abc-123-def-456"
     f. Stamp trace header on the forwarded request

  8. YARP forwards the modified request to InventoryService:5001
     with headers:
       Authorization: Bearer eyJhbGci...
       X-Ithil-TraceId: abc-123-def-456

  9. InventoryService responds with inventory data

  10. RESPONSE TRANSFORM runs:
      a. Read response body
      b. Run PII scrubber (strip emails, SSNs, card numbers)
      c. Record token usage in Redis     → increment to 12,712
      d. Fire SignalR trace event        → dashboard updates live

  11. Cleaned response returned to the agent
```

---

### 4.7 Why the Agent ID Unlocks Everything

The agent ID is not just a label. It is the key that makes every governance feature in Ithil possible:

```
Agent ID
   │
   ├──► Budget check      "has claude-prod-01 spent its 50k tokens today?"
   │
   ├──► Tool allowlist    "is claude-prod-01 allowed to call DeleteUser?"
   │
   ├──► Live trace feed   "show all calls made by claude-prod-01 in the last hour"
   │
   ├──► Audit log         "which agent accessed patient records on Feb 21st?"
   │
   └──► Circuit breaker   "claude-prod-01 called the same endpoint 200 times
                           in 60 seconds — kill its connection"
```

Without an agent ID, every request is anonymous. You cannot do per-agent budgeting, you cannot enforce tool allowlists, you cannot trace individual agents, and you cannot answer a compliance question like "which agent accessed this data." The agent ID is the thread that ties every governance feature together.

The **trace ID** is different and separate — it does not identify *who* is making the request, it identifies *one specific request* as it travels through your entire system. One identifies the caller. The other identifies the call.

---

## 5. Phase 1 — Core Engine (C# / .NET)

### 5.1 Project Structure

```
Ithil/
├── src/
│   ├── Ithil.Gateway/          # YARP-based gateway host
│   ├── Ithil.Core/             # Shared models, interfaces
│   ├── Ithil.Attributes/       # [AgentTool] attribute + metadata
│   ├── Ithil.SourceGenerator/  # Roslyn Source Generator
│   ├── Ithil.Cache/            # Redis semantic cache
│   ├── Ithil.Budget/           # Token ledger + per-agent limits
│   ├── Ithil.Privacy/          # PII scrubbing pipeline
│   └── Ithil.Management/       # REST management API for dashboard
├── sdk/
│   └── ithil-ts/               # TypeScript SDK (npm package)
├── dashboard/
│   └── ithil-dashboard/        # Next.js 15 dashboard
└── docker/
    └── docker-compose.yml
```

### 5.2 The `[AgentTool]` Attribute

```csharp
// Ithil.Attributes/AgentToolAttribute.cs
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AgentToolAttribute : Attribute
{
    public string Description { get; }
    public string[]? RequiredScopes { get; init; }
    public bool AllowWrite { get; init; } = false;      // Read-only by default
    public int MaxResponseTokens { get; init; } = 2000;
    public string? Category { get; init; }

    public AgentToolAttribute(string description) => Description = description;
}
```

#### What `Category` Is For

`Category` is optional grouping metadata. On its own it does nothing — it becomes useful in three specific places:

**1. Dashboard grouping.** If you have 40 tools across 8 microservices, the dashboard groups them into collapsible sections rather than one flat alphabetical list:

```
Inventory (6 tools)
  └── GetInventory, GetStockLevel, GetWarehouseLocations...
Orders (8 tools)
  └── GetOrder, GetOrderHistory, CreateOrder...
Finance (4 tools)
  └── GetInvoice, GetRevenueReport...
Admin (2 tools)
  └── DeleteUser, ResetPassword...
```

**2. Agent access control by category.** Instead of listing 12 individual tool names in an agent's allowlist, you assign it a whole category: "this agent is allowed the `Inventory` category." Easier to manage, easier to audit.

**3. Token budget allocation per category.** Write-heavy categories (`Orders`, `Finance`) can carry a higher token cost than read-only categories (`Inventory`). Category gives the budget engine a grouping handle to apply those rules.

```csharp
// Read-only, low cost, broadly accessible
[AgentTool("Returns stock levels for a SKU",
    Category = "Inventory",
    RequiredScopes = ["inventory.read"],
    MaxResponseTokens = 500)]
public async Task<IActionResult> GetInventory(int productId, string warehouseId)

// Write operation, higher stakes
[AgentTool("Creates a new purchase order",
    Category = "Orders",
    AllowWrite = true,
    RequiredScopes = ["orders.write"],
    MaxResponseTokens = 1000)]
public async Task<IActionResult> CreateOrder(OrderRequest request)

// Finance data, restricted to specific agents
[AgentTool("Returns the revenue report for a date range",
    Category = "Finance",
    RequiredScopes = ["finance.read"],
    MaxResponseTokens = 2000)]
public async Task<IActionResult> GetRevenueReport(DateOnly from, DateOnly to)

// Destructive, requires admin scope
[AgentTool("Permanently deletes a user account",
    Category = "Admin",
    AllowWrite = true,
    RequiredScopes = ["admin.write"],
    MaxResponseTokens = 200)]
public async Task<IActionResult> DeleteUser(Guid userId)
```

### 5.3 YARP Gateway Setup

```csharp
// Ithil.Gateway/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx =>
    {
        ctx.AddRequestTransform(async transform =>
        {
            // 1. Validate agent identity (JWT or API key)
            var agentId = transform.HttpContext.Request.Headers["X-Agent-Id"].ToString();

            // 2. Check token budget
            var budget = ctx.Services.GetRequiredService<IBudgetEngine>();
            if (!await budget.IsWithinBudgetAsync(agentId))
            {
                transform.HttpContext.Response.StatusCode = 429;
                return;
            }

            // 3. Inject tracing context
            transform.ProxyRequest.Headers.Add("X-Ithil-TraceId",
                Activity.Current?.Id ?? Guid.NewGuid().ToString());
        });

        ctx.AddResponseTransform(async transform =>
        {
            // 4. Run privacy filter on response body
            var filter = ctx.Services.GetRequiredService<IPrivacyFilter>();
            transform.SuppressResponseBody = true;
            var scrubbed = await filter.ScrubAsync(transform.ResponseBodyStream);
            await transform.HttpContext.Response.WriteAsync(scrubbed);
        });
    });

builder.Services.AddIthilServices(builder.Configuration);

var app = builder.Build();
app.MapReverseProxy();
app.MapMcpEndpoints();  // Exposes /.well-known/mcp and /mcp/tools
app.MapManagementApi(); // Exposes /management/* for dashboard
app.Run();
```

### 5.4 MCP Endpoint Mapping

#### What is `/.well-known/`?

`/.well-known/` is a standardized URL prefix defined in RFC 5785. It is a reserved location on any web server for machine-readable metadata *about* the service — not the service itself. You have already encountered it without realising:

```
/.well-known/openid-configuration   ← OAuth2 providers advertise their auth endpoints here
/.well-known/acme-challenge/        ← Let's Encrypt validates your domain here
/.well-known/security.txt           ← security contact information
/.well-known/mcp                    ← Ithil advertises its full tool manifest here
```

When an AI agent connects to Ithil for the first time, it hits `/.well-known/mcp` before anything else. The response is the full tool manifest — every tool the agent is allowed to call, with schemas. This is **discovery**: the agent does not need to be told what tools exist, it finds out by asking.

#### Why No PATCH, DELETE, PUT on MCP Endpoints?

Because MCP does not use HTTP verbs to express intent. It uses **JSON-RPC 2.0** — a protocol where every request is a `POST`, and the *action* is encoded inside the request body as a `method` field:

```json
{
  "jsonrpc": "2.0",
  "id": "abc-123",
  "method": "tools/call",
  "params": {
    "name": "GetInventory",
    "arguments": { "productId": 42 }
  }
}
```

HTTP verbs (GET, POST, PATCH, DELETE) are a REST convention. MCP deliberately avoids REST so that the same protocol works identically over HTTP, WebSockets, and stdio (local processes). The `method` field in the JSON body determines the action — not the HTTP verb.

PATCH, DELETE, and PUT *do* appear in Ithil — but on the **management API** (`/management/*`), which is a standard REST API used by the TypeScript dashboard to manage agents, budgets, and settings. That is a separate layer from the MCP protocol.

#### MCP Has Two Primitives: Tools and Resources

The plan previously only described **Tools** — functions the agent *calls*. MCP also defines **Resources** — data the agent *reads* directly into its context window. Think of them this way:

| Primitive | Analogy | Example |
|---|---|---|
| **Tool** | A verb — do something | `GetInventory(productId: 42)` — runs a query, returns a result |
| **Resource** | A noun — read something | `inventory://product/42` — a document the agent can pull and browse |

Tools are for actions and computations. Resources are for reference material, documents, and data the agent needs to hold in context. A pricing rules document, a product catalogue, an API schema — these are resources, not tools.

#### The Initialization Handshake

Before any tool calls, MCP requires a capability negotiation handshake. The agent announces what it supports; the server announces what it offers. Neither side sends tool calls until this is complete.

```
1. Agent  →  POST /mcp   { "method": "initialize", "params": { "protocolVersion": "2024-11-05", "capabilities": { "tools": {}, "resources": {} } } }
2. Server →              { "result": { "protocolVersion": "2024-11-05", "capabilities": { "tools": { "listChanged": true }, "resources": { "subscribe": true } }, "serverInfo": { "name": "Ithil", "version": "1.0.0" } } }
3. Agent  →  POST /mcp   { "method": "notifications/initialized" }
   (no response expected — this is a one-way notification)
4. Agent is now free to call tools/list, tools/call, resources/list, etc.
```

#### Complete Endpoint Map

```
# MCP Protocol Layer (agent-facing)
GET  /.well-known/mcp          ← tool + resource manifest, no auth required
                                  agents hit this first to discover capabilities
POST /mcp                      ← all JSON-RPC 2.0 calls, auth required:
                                    initialize          (handshake)
                                    notifications/initialized
                                    tools/list          (list available tools)
                                    tools/call          (execute a tool)
                                    resources/list      (list available resources)
                                    resources/read      (read a resource)
GET  /mcp/sse                  ← SSE stream for long-running tool responses
                                  agent opens once, server pushes events down

# Management API Layer (dashboard-facing, standard REST)
GET    /management/agents        ← list all registered agents
POST   /management/agents        ← register a new agent, receive API key / JWT
PATCH  /management/agents/{id}   ← update budget, allowlist, or active status
DELETE /management/agents/{id}   ← revoke an agent
GET    /management/tools         ← list all discovered tools with live telemetry
GET    /management/resources     ← list all registered resources
GET    /management/trace         ← recent trace events (paginated)
GET    /management/budget/{id}   ← current token usage for an agent today
```

#### Updated C# Implementation

```csharp
// Ithil.Gateway/McpEndpoints.cs
public static class McpEndpointExtensions
{
    public static IEndpointRouteBuilder MapMcpEndpoints(this IEndpointRouteBuilder app)
    {
        // Discovery — no auth, agents hit this first
        app.MapGet("/.well-known/mcp", (ISchemaRegistry registry) =>
            Results.Json(registry.GetManifest()));

        // All JSON-RPC 2.0 traffic flows through one POST endpoint
        app.MapPost("/mcp", async (
            JsonRpcRequest request,
            IMcpDispatcher dispatcher,
            HttpContext ctx) =>
        {
            var result = await dispatcher.DispatchAsync(request, ctx);
            return Results.Json(result);
        }).RequireAuthorization();

        // SSE stream for long-running tool responses
        app.MapGet("/mcp/sse", async (
            HttpContext ctx,
            ISseEmitter emitter) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            await emitter.StreamAsync(ctx);
        }).RequireAuthorization();

        return app;
    }
}

// Ithil.Gateway/McpDispatcher.cs
// Routes each JSON-RPC method to the correct handler
public class McpDispatcher : IMcpDispatcher
{
    public async Task<JsonRpcResponse> DispatchAsync(JsonRpcRequest request, HttpContext ctx)
    {
        return request.Method switch
        {
            "initialize"               => await HandleInitializeAsync(request, ctx),
            "notifications/initialized"=> HandleInitializedNotification(),
            "tools/list"               => await HandleToolsListAsync(request, ctx),
            "tools/call"               => await HandleToolsCallAsync(request, ctx),
            "resources/list"           => await HandleResourcesListAsync(request, ctx),
            "resources/read"           => await HandleResourcesReadAsync(request, ctx),
            _                          => JsonRpcResponse.MethodNotFound(request.Id)
        };
    }

    private async Task<JsonRpcResponse> HandleInitializeAsync(JsonRpcRequest request, HttpContext ctx)
    {
        // Negotiate protocol version and return server capabilities
        return new JsonRpcResponse(request.Id, new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { listChanged = true },
                resources = new { subscribe = true }
            },
            serverInfo = new { name = "Ithil", version = "1.0.0" }
        });
    }
}
```

---

## 6. Phase 2 — Developer Dashboard (TypeScript / Next.js)

### 5.1 Tech Stack

- **Framework:** Next.js 15 (App Router)
- **UI:** Shadcn/UI + Tailwind CSS v4
- **State:** Zustand
- **Real-Time:** WebSocket client (connects to C# SignalR hub)
- **Data Fetching:** TanStack Query v5
- **Charts:** Recharts
- **Package Manager:** Bun

### 5.2 TypeScript Interfaces

```typescript
// dashboard/src/types/mcp.ts

export interface McpTool {
  name: string;
  description: string;
  category?: string;
  allowWrite: boolean;
  maxResponseTokens: number;
  requiredScopes: string[];
  inputSchema: {
    type: "object";
    properties: Record<string, JsonSchemaProperty>;
    required: string[];
  };
  // Live telemetry (populated from management API)
  status: "healthy" | "degraded" | "offline";
  lastUsedAt?: string;
  usageCount: number;
  avgLatencyMs: number;
  estimatedTokensPerCall: number;
  errorRate: number; // 0.0 – 1.0
}

export interface AgentIdentity {
  agentId: string;
  label: string;
  tokenBudgetDaily: number;
  tokenBudgetUsedToday: number;
  allowedTools: string[];  // tool name allowlist
  isActive: boolean;
  createdAt: string;
}

export interface AgentTraceEvent {
  traceId: string;
  agentId: string;
  toolName: string;
  status: "pending" | "success" | "error" | "blocked";
  tokensUsed?: number;
  latencyMs?: number;
  timestamp: string;
  errorMessage?: string;
}
```

### 5.3 Dashboard Pages

| Route | Description |
|---|---|
| `/` | Overview: active agents, request rate, cost today |
| `/tools` | Tool Library: all discovered C# endpoints as MCP tools |
| `/tools/[name]` | Tool detail: schema, telemetry, test playground |
| `/agents` | Agent registry: create/manage agent identities & budgets |
| `/agents/[id]` | Agent detail: budget gauge, allowed tools, recent traces |
| `/trace` | Live trace feed (SignalR real-time stream) |
| `/settings` | Gateway config, privacy filter rules, circuit breaker thresholds |

### 5.4 Tool Tester Component (The Killer Feature)

```typescript
// dashboard/src/components/ToolTester.tsx
"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import type { McpTool } from "@/types/mcp";

interface ToolTesterProps {
  tool: McpTool;
}

export function ToolTester({ tool }: ToolTesterProps) {
  const [params, setParams] = useState<Record<string, string>>({});
  const [result, setResult] = useState<unknown>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [latency, setLatency] = useState<number | null>(null);

  const handleTest = async () => {
    setIsLoading(true);
    const start = Date.now();
    try {
      const response = await fetch(`/api/gateway/mcp/tools/${tool.name}`, {
        method: "POST",
        body: JSON.stringify({
          jsonrpc: "2.0",
          id: crypto.randomUUID(),
          method: tool.name,
          params,
        }),
        headers: {
          "Content-Type": "application/json",
          "X-Agent-Mode": "test-manual",
          "X-Agent-Id": "dashboard-playground",
        },
      });
      setResult(await response.json());
      setLatency(Date.now() - start);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="p-6 border rounded-xl space-y-4">
      <div className="flex items-center gap-2">
        <h3 className="font-mono text-lg font-semibold">{tool.name}</h3>
        {tool.allowWrite && <Badge variant="destructive">WRITE</Badge>}
        <Badge variant="outline">~{tool.estimatedTokensPerCall} tokens</Badge>
      </div>

      <p className="text-sm text-muted-foreground">{tool.description}</p>

      <div className="space-y-2">
        {Object.entries(tool.inputSchema.properties).map(([prop, schema]) => (
          <div key={prop} className="space-y-1">
            <label className="text-xs font-medium font-mono">
              {prop}
              {tool.inputSchema.required.includes(prop) && (
                <span className="text-destructive ml-1">*</span>
              )}
              <span className="text-muted-foreground ml-2">({schema.type})</span>
            </label>
            <Input
              placeholder={schema.description ?? prop}
              onChange={(e) => setParams({ ...params, [prop]: e.target.value })}
            />
          </div>
        ))}
      </div>

      <Button onClick={handleTest} disabled={isLoading} className="w-full">
        {isLoading ? "Executing..." : "Execute Against C# Gateway"}
      </Button>

      {result && (
        <div className="space-y-1">
          {latency && (
            <p className="text-xs text-muted-foreground">{latency}ms</p>
          )}
          <pre className="bg-muted p-4 rounded-lg text-xs overflow-auto max-h-64">
            {JSON.stringify(result, null, 2)}
          </pre>
        </div>
      )}
    </div>
  );
}
```

---

## 7. Phase 3 — Semantic Caching & Budget Engine

### 6.1 Semantic Caching (Redis)

Standard caches key on the exact URL. Ithil keys on **semantic intent** using vector embeddings, so two different agents asking for the same data (phrased differently) hit the cache.

```csharp
// Ithil.Cache/SemanticCacheService.cs
public class SemanticCacheService : ISemanticCache
{
    private readonly IDatabase _redis;
    private readonly IEmbeddingService _embedder; // calls a local embedding model

    public async Task<CacheResult?> TryGetAsync(string toolName, object parameters)
    {
        // 1. Serialize the tool call intent
        var intent = $"{toolName}:{JsonSerializer.Serialize(parameters)}";

        // 2. Generate embedding vector
        var vector = await _embedder.EmbedAsync(intent);

        // 3. Vector similarity search in Redis (requires Redis Stack / RediSearch)
        var matches = await _redis.SearchSimilarAsync(
            indexName: "agent-cache",
            vector: vector,
            threshold: 0.95f,  // 95% similarity = cache hit
            limit: 1);

        return matches.FirstOrDefault();
    }

    public async Task SetAsync(string toolName, object parameters, object response, TimeSpan ttl)
    {
        var intent = $"{toolName}:{JsonSerializer.Serialize(parameters)}";
        var vector = await _embedder.EmbedAsync(intent);
        await _redis.StoreVectorAsync("agent-cache", vector,
            JsonSerializer.Serialize(response), ttl);
    }
}
```

**Embedding Model:** Use a lightweight local model (e.g., `all-MiniLM-L6-v2` via ONNX) so no data leaves the enterprise's VPC.

### 6.2 Budget Engine (Token Ledger)

```csharp
// Ithil.Budget/BudgetEngine.cs
public class BudgetEngine : IBudgetEngine
{
    private readonly IDatabase _redis;

    public async Task<bool> IsWithinBudgetAsync(string agentId)
    {
        var config = await GetAgentConfigAsync(agentId);
        var usedToday = await _redis.StringGetAsync($"budget:{agentId}:{DateTime.UtcNow:yyyyMMdd}");
        return (long)(usedToday ?? 0) < config.DailyTokenBudget;
    }

    public async Task RecordUsageAsync(string agentId, int tokensUsed)
    {
        var key = $"budget:{agentId}:{DateTime.UtcNow:yyyyMMdd}";
        await _redis.StringIncrementAsync(key, tokensUsed);
        await _redis.KeyExpireAsync(key, TimeSpan.FromDays(2)); // auto-cleanup
    }
}
```

### 6.3 Circuit Breaker

Uses Polly (already a .NET standard):

```csharp
services.AddHttpClient("downstream")
    .AddPolicyHandler(Policy<HttpResponseMessage>
        .Handle<Exception>()
        .CircuitBreakerAsync(
            exceptionsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (ex, ts) => traceHub.NotifyCircuitOpen(agentId, toolName),
            onReset: () => traceHub.NotifyCircuitClosed(agentId, toolName)));
```

---

## 8. Phase 4 — Enterprise Features

### 7.1 Privacy Filter (PII Scrubbing)

Runs on every response body before it is returned to the agent.

```csharp
// Ithil.Privacy/PrivacyFilterService.cs
public class PrivacyFilterService : IPrivacyFilter
{
    private static readonly Regex EmailRegex =
        new(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled);

    private static readonly Regex SsnRegex =
        new(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled);

    private static readonly Regex CreditCardRegex =
        new(@"\b(?:\d[ -]?){13,16}\b", RegexOptions.Compiled);

    // Additional patterns loaded from configuration (allows custom enterprise rules)
    private readonly IEnumerable<PiiRule> _customRules;

    public async Task<string> ScrubAsync(Stream responseBody)
    {
        using var reader = new StreamReader(responseBody);
        var content = await reader.ReadToEndAsync();

        content = EmailRegex.Replace(content, "[EMAIL REDACTED]");
        content = SsnRegex.Replace(content, "[SSN REDACTED]");
        content = CreditCardRegex.Replace(content, "[CARD REDACTED]");

        foreach (var rule in _customRules)
            content = rule.Apply(content);

        return content;
    }
}
```

### 7.2 Agent Identity & OIDC Integration

Enterprise customers authenticate agents using their existing Azure AD or Okta.

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Supports Azure AD, Okta, Auth0
        options.Authority = config["Ithil:Authority"];
        options.Audience = "ithil-api";
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            NameClaimType = "agent_id",  // Custom claim for agent identity
        };
    });
```

### 7.3 Audit Log

Every agent request is written to a structured audit log (JSON lines format, shipped to Azure Monitor / Datadog / Splunk):

```json
{
  "timestamp": "2026-03-15T14:22:01Z",
  "traceId": "abc-123",
  "agentId": "claude-prod-01",
  "toolName": "GetInventory",
  "parameters": { "productId": 42, "warehouseId": "UK-01" },
  "outcome": "success",
  "tokensUsed": 312,
  "latencyMs": 87,
  "cacheHit": true,
  "piiScrubbed": false,
  "userId": null
}
```

---

## 9. Phase 5 — Real-Time Agent Tracing (SignalR)

### 8.1 C# SignalR Hub

```csharp
// Ithil.Gateway/Hubs/TraceHub.cs
public class TraceHub : Hub
{
    public async Task SubscribeToAgent(string agentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
    }
}

// Injected into the YARP transform pipeline:
public class TraceNotifier
{
    private readonly IHubContext<TraceHub> _hub;

    public async Task NotifyAsync(AgentTraceEvent traceEvent)
    {
        await _hub.Clients
            .Group($"agent:{traceEvent.AgentId}")
            .SendAsync("TraceEvent", traceEvent);

        // Also broadcast to the global dashboard feed
        await _hub.Clients
            .Group("dashboard-all")
            .SendAsync("TraceEvent", traceEvent);
    }
}
```

### 8.2 TypeScript Live Feed Component

```typescript
// dashboard/src/components/LiveTraceFeed.tsx
"use client";

import { useEffect, useState } from "react";
import * as signalR from "@microsoft/signalr";
import type { AgentTraceEvent } from "@/types/mcp";

export function LiveTraceFeed() {
  const [events, setEvents] = useState<AgentTraceEvent[]>([]);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/trace")
      .withAutomaticReconnect()
      .build();

    connection.on("TraceEvent", (event: AgentTraceEvent) => {
      setEvents((prev) => [event, ...prev].slice(0, 200)); // keep last 200
    });

    connection.start();
    return () => { connection.stop(); };
  }, []);

  return (
    <div className="font-mono text-xs space-y-1 max-h-96 overflow-y-auto">
      {events.map((event) => (
        <div
          key={event.traceId}
          className={`flex gap-3 p-2 rounded ${
            event.status === "blocked" ? "bg-destructive/10" :
            event.status === "error"   ? "bg-orange-500/10" :
                                         "bg-muted"
          }`}
        >
          <span className="text-muted-foreground">
            {new Date(event.timestamp).toLocaleTimeString()}
          </span>
          <span className="text-primary font-semibold">{event.agentId}</span>
          <span>→</span>
          <span>{event.toolName}</span>
          <span className={
            event.status === "success" ? "text-green-500" :
            event.status === "blocked" ? "text-destructive" : "text-orange-500"
          }>
            [{event.status.toUpperCase()}]
          </span>
          {event.tokensUsed && (
            <span className="text-muted-foreground">{event.tokensUsed}t</span>
          )}
          {event.latencyMs && (
            <span className="text-muted-foreground">{event.latencyMs}ms</span>
          )}
        </div>
      ))}
    </div>
  );
}
```

---

## 10. MCP Tool Schema Generation (Source Generators)

This is the architectural centerpiece. A Roslyn Source Generator runs at **compile time**, scanning every method tagged with `[AgentTool]` and emitting a `SchemaRegistry.g.cs` file with the full MCP manifest baked in.

### 9.1 Why Source Generators (Not Reflection)

| | Reflection (Runtime) | Source Generator (Compile Time) |
|---|---|---|
| Speed | Slow on startup | Zero runtime cost |
| AOT compatible | No | Yes |
| Errors surfaced | Runtime | Build time |
| Docker image size | +Roslyn | No change |

### 9.2 Source Generator Implementation

```csharp
// Ithil.SourceGenerator/AgentToolGenerator.cs
[Generator]
public class AgentToolGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Ithil.Attributes.AgentToolAttribute",
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => GetToolMetadata(ctx));

        context.RegisterSourceOutput(methods.Collect(), GenerateSchemaRegistry);
    }

    private static void GenerateSchemaRegistry(
        SourceProductionContext ctx,
        ImmutableArray<ToolMetadata> tools)
    {
        var schemas = tools.Select(t => $$"""
            new McpToolDefinition {
                Name = "{{t.MethodName}}",
                Description = "{{t.Description}}",
                AllowWrite = {{t.AllowWrite.ToString().ToLower()}},
                MaxResponseTokens = {{t.MaxResponseTokens}},
                InputSchema = new() {
                    Properties = new Dictionary<string, JsonSchemaProperty> {
                        {{string.Join(",\n                        ", t.Parameters.Select(p =>
                            $"""["{p.Name}"] = new() {{ Type = "{p.JsonType}", Description = "{p.Description}" }}"""))}}
                    },
                    Required = [{{string.Join(", ", t.RequiredParams.Select(p => $"\"{p}\""))}}]
                }
            }
        """);

        var source = $$"""
            // <auto-generated by Ithil.SourceGenerator />
            namespace Ithil.Generated;

            public static class SchemaRegistry
            {
                public static readonly IReadOnlyList<McpToolDefinition> Tools = [
                    {{string.Join(",\n                    ", schemas)}}
                ];
            }
        """;

        ctx.AddSource("SchemaRegistry.g.cs", source);
    }
}
```

### 9.3 Generated Output Example

Given this controller method:

```csharp
[AgentTool("Returns inventory for a SKU", RequiredScopes = ["inventory.read"])]
public async Task<IActionResult> GetInventory(
    [FromQuery] int productId,
    [FromQuery] string warehouseId)
```

The generator emits:

```json
{
  "name": "GetInventory",
  "description": "Returns inventory for a SKU",
  "allowWrite": false,
  "inputSchema": {
    "type": "object",
    "properties": {
      "productId": { "type": "integer" },
      "warehouseId": { "type": "string" }
    },
    "required": ["productId", "warehouseId"]
  }
}
```

---

## 11. Revenue Model & Packaging

### 10.1 Pricing Tiers

| Tier | Price | Who | What's Included |
|---|---|---|---|
| **Developer** | Free | Solo devs, OSS | 10k requests/month, no SLA, community support |
| **Startup** | $199/month | Small SaaS teams | 500k requests/month, 3 agent identities, email support |
| **Professional** | $999/month | Mid-market | 5M requests/month, unlimited agents, SSO/OIDC, Slack support |
| **Enterprise Self-Hosted** | $500/month flat | Fortune 500 | Docker container, runs in their VPC, no usage caps, phone support |
| **Cloud Consumption** | $0.01/request | ISVs / burst users | Pay-as-you-go, billed monthly, min $50/month |

### 10.2 What Drives Expansion Revenue

- Every new AI agent identity added = more tool calls routed through Ithil.
- Every new C# microservice "registered" = more schema generation, more gateway traffic.
- "Privacy Filter Rules" add-on (custom enterprise PII patterns) — $200/month.
- "Compliance Export" add-on (SOC2/HIPAA audit log exports) — $300/month.

---

## 12. Azure Marketplace vs. Standalone SaaS

### 11.1 Azure Marketplace (Priority Channel)

**Why:** Microsoft's buyer community is overwhelmingly enterprise .NET shops. An Azure Marketplace listing puts Ithil directly in front of the buyer at the moment they are provisioning infrastructure.

**Listing Type:** Azure Managed Application (the customer pays through their Azure subscription, which counts against their Microsoft Azure Commit — a major purchasing incentive for enterprise buyers).

**Steps to List:**

1. Create a Partner Center account.
2. Package the gateway as an ARM template or Bicep file (deploys to the customer's subscription).
3. Offer plans: "Self-Hosted Managed App" at $500/month and "Consumption Metered" at $0.01/request using Azure Marketplace metered billing API.
4. Certify against Azure Marketplace technical requirements (security scan, etc.).
5. Co-sell with Microsoft (once listed, you become eligible for Microsoft seller referrals).

**Azure-Specific Integrations to Build First:**

- Azure AD integration (OIDC) for agent authentication.
- Azure Monitor / Application Insights for trace event export.
- Azure Cache for Redis (semantic cache backend).
- Key Vault integration for secrets management.

### 11.2 Standalone SaaS (Self-Serve Motion)

**Why:** Lower friction for startups and ISVs. Faster iteration on the product. Serves buyers who aren't Azure-first.

**Infrastructure:**

- Hosted on Azure (align with marketplace for co-sell eligibility).
- Stripe for billing (consumption metering via Stripe Meter Events API).
- Auth0 or Azure AD B2C for customer authentication to the dashboard.
- Fly.io or Railway for the Next.js dashboard (can be moved to Azure later).

**Self-Serve Onboarding Flow:**

1. Dev signs up at `ithil.dev`.
2. Installs the NuGet package: `dotnet add package Ithil.Gateway`.
3. Adds 3 lines to `Program.cs` and decorates their controllers with `[AgentTool]`.
4. The gateway auto-registers with the SaaS control plane.
5. Dashboard shows their tools immediately. First 10,000 requests free.

### 11.3 Channel Comparison

| | Azure Marketplace | Standalone SaaS |
|---|---|---|
| Time to first customer | 3–6 months (certification) | 1–4 weeks |
| Deal size | $50k+ ACV (enterprise) | $2.4k–$12k ACV |
| Sales motion | Co-sell with Microsoft | Self-serve + inside sales |
| Billing complexity | Low (Microsoft handles invoicing) | Medium (Stripe metering) |
| Best for | Phase 2+ (after product-market fit) | Phase 1 (finding PMF) |

**Recommendation:** Launch standalone SaaS first. Get 10 paying customers. Then list on Azure Marketplace.

---

## 13. Go-To-Market Strategy

### 12.1 Phase 1 GTM — Developer Awareness (Months 1–3)

**Goal:** 500 GitHub stars, 50 Discord members, 3 paying design partners.

- **Open-source the core:** Release `Ithil.Attributes` and `Ithil.SourceGenerator` as MIT-licensed NuGet packages on GitHub. The gateway and dashboard are the paid product.
- **Content:** Write "How to turn your ASP.NET Core API into an MCP Tool Server in 10 minutes" — target Hacker News, r/dotnet, r/MachineLearning, Dev.to.
- **YouTube demo:** 5-minute video showing an AI agent navigating a C# inventory API via Ithil. No setup required to watch. Links to GitHub.
- **Design Partners:** Reach out to 20 companies with .NET backends who are actively building AI agents. Offer 3 months free in exchange for weekly feedback calls.

### 12.2 Phase 2 GTM — Inbound Self-Serve (Months 4–6)

**Goal:** $5k MRR.

- SEO content: "MCP server .NET", "AI agent API gateway C#", "ASP.NET Core MCP".
- Product Hunt launch (time with a significant dashboard release).
- Integration guide for Claude, GPT-5, AutoGen, and LangGraph.
- Stripe billing live. Startup tier at $199/month.

### 12.3 Phase 3 GTM — Enterprise & Marketplace (Months 7–12)

**Goal:** $30k MRR, first Azure Marketplace listing.

- Inside sales: 2–3 outbound emails/day to Platform Engineering leads at companies with >500 engineers.
- Azure Marketplace listing (certification takes 4–8 weeks, start the process in Month 5).
- SOC2 Type II audit (starts Month 7, required for most enterprise deals).
- Partner with SI firms (Accenture, Avanade) who do Azure/AI modernization work.

---

## 14. Milestones & Build Sequence

### Sprint 1 (Weeks 1–2): Foundation
- [ ] Create solution structure (`Ithil.sln`)
- [ ] Implement `[AgentTool]` attribute
- [ ] Basic YARP gateway host with health check endpoint
- [ ] `GET /.well-known/mcp` returns hardcoded manifest (no generator yet)

### Sprint 2 (Weeks 3–4): Source Generator
- [ ] Roslyn Source Generator scans controllers and emits `SchemaRegistry.g.cs`
- [ ] `GET /.well-known/mcp` reads from generated registry
- [ ] Unit tests for generator with a sample controller

### Sprint 3 (Weeks 5–6): Budget & Auth
- [ ] Redis integration (local Docker for dev)
- [ ] Budget Engine: per-agent daily token limit enforced in YARP transform
- [ ] JWT authentication for agent requests
- [ ] Management API: `GET /management/agents`, `POST /management/agents`

### Sprint 4 (Weeks 7–8): Dashboard v1
- [ ] Next.js 15 project scaffolded (Bun, Shadcn/UI, Tailwind v4)
- [ ] Tool Library page reads from `/.well-known/mcp`
- [ ] Agent Registry page (CRUD via management API)
- [ ] Tool Tester component functional

### Sprint 5 (Weeks 9–10): Real-Time Tracing
- [ ] SignalR hub in C# gateway
- [ ] Trace events fired from YARP transform pipeline
- [ ] Live Trace Feed component in dashboard (WebSocket)
- [ ] Budget gauge in agent detail page (updates live)

### Sprint 6 (Weeks 11–12): Semantic Cache & Privacy Filter
- [ ] Redis vector search integration (Redis Stack)
- [ ] Local ONNX embedding model for intent vectorization
- [ ] Privacy Filter: regex-based PII scrubbing (email, SSN, card)
- [ ] Privacy Filter rules configurable via `appsettings.json`

### Sprint 7 (Weeks 13–14): Circuit Breaker & Hardening
- [ ] Polly circuit breaker on downstream HTTP clients
- [ ] Circuit state visible in dashboard
- [ ] Audit log pipeline (structured JSON, pluggable sinks)
- [ ] Docker Compose file for local full-stack dev

### Sprint 8 (Weeks 15–16): SaaS Billing & Launch Prep
- [ ] Stripe Meter Events integration (consumption billing)
- [ ] Stripe Checkout for Startup and Professional tiers
- [ ] Onboarding flow (sign up → install NuGet → see first tool)
- [ ] Landing page at `ithil.dev`

---

## 15. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| MCP spec changes break schema format | Medium | High | Abstract schema generation behind an interface; versioned manifests |
| Redis vector search too slow for P99 latency | Low | Medium | Add exact-match cache layer before vector search; tune threshold |
| Enterprise security team blocks gateway in their VPC | Medium | High | Offer full on-prem deployment with no outbound calls; SOC2 cert |
| Roslyn Source Generator incompatible with target project's TFM | Low | Medium | Publish generator as a separate Analyzer NuGet; test on net8.0 and net9.0 |
| OpenAI / Anthropic ship their own .NET MCP SDK | Medium | High | Differentiate on governance (budgets, PII, circuit breakers) — not just schema gen |
| Low developer adoption of `[AgentTool]` attribute pattern | Medium | Medium | Provide auto-discovery mode that scans all public endpoints without attributes |
| Azure Marketplace certification rejected | Low | Low | Start certification early (Month 5); Marketplace not required for revenue |
| SignalR WebSocket connections don't scale beyond 10k agents | Low | Medium | Use Azure SignalR Service (managed, handles scale automatically) |

---

*Last updated: 2026-02-21*
*Version: 0.1 — Pre-Build Plan*
