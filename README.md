# Ithil

**The Agentic Governance Layer for Enterprise .NET Backends**

[![License: BUSL-1.1](https://img.shields.io/badge/License-BUSL--1.1-blue.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10%2B-purple)](https://dotnet.microsoft.com/)

Ithil is a gateway that sits in front of your existing C# APIs and makes them safe for AI agents to call. It handles identity, budgets, privacy, caching, and observability — so your team doesn't have to build any of that.

| Problem | Ithil Solution |
|---|---|
| Agents hallucinate with messy REST APIs | Compile-time MCP schema generation from `[AgentTool]`-decorated C# controllers |
| No visibility into what agents are doing | SignalR real-time trace feed — every tool call, latency, outcome |
| Agents loop and call destructive endpoints | Circuit breaker + per-agent daily token budgets with hard 429 enforcement |
| Redundant LLM calls cost thousands per month | Semantic cache in Redis — keyed on intent, not raw request parameters |
| PII leaking to LLMs | Privacy filter scrubs every response before it leaves the gateway |

---

## How It Works

Ithil is two things working together: a **standalone gateway** you deploy, and a **small library** you add to your downstream services.

```
┌─────────────────────────────────────────────────────────────┐
│  AI Agent  (Claude / GPT / AutoGen / any MCP client)        │
└─────────────────────────┬───────────────────────────────────┘
                          │  JSON-RPC 2.0 over HTTP/SSE
                          ▼
┌─────────────────────────────────────────────────────────────┐
│  Ithil Gateway                                              │
│                                                             │
│  1. Verify agent identity  (JWT or API Key)                 │
│  2. Check daily token budget                                │
│  3. Verify tool is in agent's allowlist                     │
│  4. Check semantic cache  (Redis vector search)             │
│  5. Stamp X-Ithil-TraceId and forward request               │
│  6. Scrub PII from response                                 │
│  7. Record token usage                                      │
│  8. Fire real-time trace event                              │
└─────────────────────────┬───────────────────────────────────┘
                          │  Internal HTTP
                          ▼
┌─────────────────────────────────────────────────────────────┐
│  Your C# Services  (unchanged, annotated with [AgentTool])  │
└─────────────────────────────────────────────────────────────┘
```

The gateway reads `/ithil/schema` from each downstream service at startup — an endpoint your services expose automatically once they reference `Ithil.Attributes` and `Ithil.Hosting`.

---

## Quick Start

### Step 1 — Annotate your controllers

In your existing C# service, add the `Ithil.Attributes` and `Ithil.Hosting` packages:

```bash
dotnet add package Ithil.Attributes
dotnet add package Ithil.Hosting
```

Decorate the methods you want agents to be able to call:

```csharp
[AgentTool("Returns current stock levels for a product",
    Category = "Inventory",
    AllowWrite = false,
    MaxResponseTokens = 500)]
public async Task<IActionResult> GetInventory(int productId, string warehouseId)
{
    // your existing implementation — unchanged
}
```

Expose the schema endpoint so the gateway can discover your tools:

```csharp
// Program.cs in your downstream service
app.MapIthilSchema(SchemaRegistry.Tools);
```

`SchemaRegistry` is generated at compile time by the Ithil Roslyn Source Generator — no reflection, no runtime cost.

### Step 2 — Configure the gateway

Clone the gateway and create your `appsettings.json`:

```bash
git clone https://github.com/crossland-creative/ithil
cd src/Ithil.Gateway
```

```json
{
  "ConnectionStrings": {
    "Redis": "your-redis-stack-host:6379"
  },
  "Ithil": {
    "Jwt": {
      "SigningKey": "your-32-plus-byte-signing-key",
      "Issuer": "your-issuer",
      "Audience": "ithil-gateway"
    },
    "Budget": {
      "DefaultDailyTokenLimit": 100000
    },
    "ToolRegistry": {
      "DownstreamBaseUrl": "http://your-service:5200"
    }
  },
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": { "Path": "/api/{**catch-all}" }
      }
    },
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "destination1": { "Address": "http://your-service:5200" }
        }
      }
    }
  }
}
```

### Step 3 — Run

```bash
dotnet run
# or
docker-compose up
```

### Step 4 — Point your agent at the gateway

```
POST https://your-gateway/mcp
Authorization: Bearer <agent-jwt>
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": { "name": "GetInventory", "arguments": { "productId": 42, "warehouseId": "UK-01" } },
  "id": 1
}
```

The gateway verifies identity, checks budget, checks the allowlist, checks the semantic cache, then forwards to your service. The agent never touches your service directly.

---

## Features

### `[AgentTool]` Attribute

The only change you make to your existing C# code. Every parameter becomes part of the auto-generated MCP input schema.

```csharp
[AgentTool(
    "Submits a purchase order for a product",
    Category       = "Orders",
    RequiredScopes = ["orders.write"],
    AllowWrite     = true,
    MaxResponseTokens = 1000)]
public async Task<IActionResult> CreateOrder(string sku, int quantity, string buyerId)
```

- `AllowWrite` defaults to `false` — destructive methods require explicit opt-in
- `MaxResponseTokens` defaults to `2000`
- `RequiredScopes` are checked against the agent's JWT claims
- Applying the attribute to a class decorates all public methods on that controller

---

### Agent Identity

Every request must carry a verifiable agent identity. Two mechanisms are supported:

**JWT (enterprise / OIDC-backed):**
```
Authorization: Bearer eyJhbGci...
```
The JWT must contain an `agent_id` claim. Signature, expiry, issuer, and audience are all validated.

**API Key (self-serve):**
```
X-Api-Key: ithil_live_a3f9...
```
Keys are stored hashed (SHA-256) — the plaintext is shown once at creation and never stored.

Configure JWT validation in `appsettings.json`:
```json
"Ithil": {
  "Jwt": {
    "SigningKey": "...",
    "Issuer": "your-oidc-provider",
    "Audience": "ithil-gateway"
  }
}
```

---

### Budget Enforcement

Each agent has a daily token budget. Once exhausted, every call returns `429` until UTC midnight resets the counter. Redis `INCR` ensures atomic accounting with no race conditions.

```json
"Ithil": {
  "Budget": {
    "DefaultDailyTokenLimit": 100000
  }
}
```

Per-agent limits are set on the agent config record and override the default. Token counts are stored in Redis with a 48-hour TTL — yesterday's usage remains available for reporting.

If Redis is unreachable, the budget check **fails open** by default (requests are allowed through). This is configurable.

---

### Tool Allowlist

Each agent config carries a list of tools it is allowed to call. A request to any tool not on that list returns `403` — the downstream service is never contacted.

Allowlists are managed via the agent config record. An empty allowlist means the agent can call all tools.

---

### Privacy Filter

Every response body passes through the privacy filter before it reaches the agent. Built-in patterns cover the most common PII types:

| Pattern | Replacement |
|---|---|
| Email addresses | `[EMAIL REDACTED]` |
| Social Security Numbers (`NNN-NN-NNNN`) | `[SSN REDACTED]` |
| Credit card numbers (13–16 digits) | `[CARD REDACTED]` |

Custom rules are added via config — no code changes required:

```json
"Ithil": {
  "Privacy": {
    "CustomRules": [
      { "Pattern": "ACME-\\d{6}", "Replacement": "[ACCOUNT REDACTED]" },
      { "Pattern": "EMP-[A-Z]{2}\\d{4}", "Replacement": "[EMPLOYEE REDACTED]" }
    ]
  }
}
```

All regex patterns are compiled at startup — zero per-request compilation cost.

---

### Semantic Cache

Cache entries are keyed on **semantic intent**, not raw parameters. Two agents asking for the same data with slightly different phrasing hit the same cache entry.

Under the hood: request parameters are serialized to a normalized intent string, embedded via a local ONNX model (`all-MiniLM-L6-v2` — no data leaves your network), and matched against cached vectors in Redis using cosine similarity.

```json
"Ithil": {
  "SemanticCache": {
    "ModelPath": "models/all-MiniLM-L6-v2.onnx",
    "VocabPath": "models/vocab.txt",
    "SimilarityThreshold": 0.95,
    "DefaultTtlMinutes": 15
  }
}
```

> **Requires Redis Stack** (not plain Redis) for vector search support. See [Deployment](#deployment).

Cache misses are fail-open — an unavailable Redis or embedding service never blocks a request.

---

### Circuit Breaker

A Polly circuit breaker protects your downstream services from repeated failures. When the failure rate exceeds the threshold, the circuit opens and requests fail fast with `503` — no downstream calls are made.

```json
"Ithil": {
  "CircuitBreaker": {
    "FailureRatioThreshold": 0.5,
    "SamplingDurationSeconds": 30,
    "MinimumThroughput": 5,
    "BreakDurationSeconds": 15
  }
}
```

Circuit state changes (Closed → Open → HalfOpen → Closed) are emitted as trace events, visible in the real-time trace feed.

---

### Audit Log

Every tool call produces a structured audit record written to stdout as JSON Lines:

```json
{
  "timestamp": "2026-03-24T14:32:01Z",
  "traceId": "a1b2c3d4",
  "agentId": "claude-prod-01",
  "toolName": "GetInventory",
  "outcome": "success",
  "tokensUsed": 312,
  "latencyMs": 84,
  "cacheHit": false,
  "piiScrubbed": false
}
```

Possible `outcome` values: `success`, `error`, `blocked`, `cache-hit`, `budget-reset`.

The default `StdoutAuditSink` is always active. Ship the stdout stream to your existing log aggregator (Datadog, Loki, Splunk, etc.) — no additional config required.

---

### Real-Time Tracing

Every tool call emits an event to a SignalR hub at `/hubs/trace`. Connect any SignalR client to stream live agent activity:

```javascript
const connection = new HubConnectionBuilder()
    .withUrl("https://your-gateway/hubs/trace")
    .build();

connection.on("TraceEvent", event => console.log(event));
await connection.start();
```

Each event carries: `traceId`, `agentId`, `toolName`, `outcome`, `latencyMs`, `circuitState`, `timestamp`.

---

## Configuration Reference

Complete `appsettings.json` with all available options:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "Ithil": {
    "Jwt": {
      "SigningKey": "your-32-plus-byte-signing-key",
      "Issuer": "your-issuer",
      "Audience": "ithil-gateway"
    },
    "Budget": {
      "DefaultDailyTokenLimit": 100000,
      "FailOpenOnRedisError": true
    },
    "SemanticCache": {
      "ModelPath": "models/all-MiniLM-L6-v2.onnx",
      "VocabPath": "models/vocab.txt",
      "SimilarityThreshold": 0.95,
      "DefaultTtlMinutes": 15
    },
    "CircuitBreaker": {
      "FailureRatioThreshold": 0.5,
      "SamplingDurationSeconds": 30,
      "MinimumThroughput": 5,
      "BreakDurationSeconds": 15
    },
    "Privacy": {
      "CustomRules": []
    },
    "ToolRegistry": {
      "DownstreamBaseUrl": "http://localhost:5200"
    }
  },
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": { "Path": "/api/{**catch-all}" }
      }
    },
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "destination1": { "Address": "http://your-service:5200" }
        }
      }
    }
  }
}
```

---

## Requirements

| Requirement | Notes |
|---|---|
| .NET 10+ | Gateway and downstream services |
| Redis Stack | Required for semantic cache (vector search) and budget engine. Plain Redis is **not** sufficient for the cache — use `redis/redis-stack` |
| ONNX model files | `all-MiniLM-L6-v2.onnx` + `vocab.txt` — place in `models/` relative to the gateway. No internet access required at runtime |

**Without Redis:** Set `UseInMemory: true` in dev environments. Budget and cache use in-memory fallbacks. Not suitable for production or multi-instance deployments.

---

## Deployment

A minimal `docker-compose.yml` to get the gateway and Redis Stack running:

```yaml
version: "3.9"
services:
  redis:
    image: redis/redis-stack:latest
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  gateway:
    build:
      context: .
      dockerfile: src/Ithil.Gateway/Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ConnectionStrings__Redis=redis:6379
      - Ithil__Jwt__SigningKey=${ITHIL_SIGNING_KEY}
      - Ithil__Jwt__Issuer=${ITHIL_ISSUER}
      - Ithil__Jwt__Audience=ithil-gateway
      - Ithil__ToolRegistry__DownstreamBaseUrl=http://your-service:5200
    depends_on:
      - redis

volumes:
  redis-data:
```

**Health check:** `GET /health` — returns `200` with no auth required. Use this for Kubernetes `readinessProbe` and `livenessProbe`.

> **Note:** For multi-instance gateway deployments, a Redis backplane is required for the SignalR trace hub. Configure via `AddStackExchangeRedisHubProtocol()`.

---

## License

Ithil is licensed under the [Business Source License 1.1](./LICENSE) (BUSL-1.1).

- **Non-commercial use** — free. Personal projects, open-source evaluation, internal development.
- **Commercial production use** — requires a commercial license from Crossland Creative LLC.
- **2033-04-17** — license converts to Apache 2.0, permanently and irrevocably.

| Tier | Price | What You Get |
|---|---|---|
| Non-Commercial | Free | Full source under BUSL. Self-host. No commercial use. |
| Commercial Small | $149/mo or $1,490/yr | Commercial license, self-host, best-effort email support |
| Commercial Business | $499/mo or $4,990/yr | Commercial license, self-host, 48h SLA email support |
| Enterprise | Contact us | Custom contract and SLA |

Commercial licensing: contact Crossland Creative LLC.
