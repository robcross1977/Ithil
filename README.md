# Ithil

**Agentic Governance Layer for Enterprise .NET Backends**

Ithil sits in front of your existing C# APIs and translates them into [Model Context Protocol (MCP)](https://modelcontextprotocol.io/)-compliant Tool Servers — enabling AI agents (Claude, GPT, AutoGen, etc.) to interact with private enterprise data safely, observably, and cost-controlled.

---

## The Problem

| Problem | Ithil Solution |
|---|---|
| Agents hallucinate with messy REST APIs | Auto-generates MCP-compliant schemas from C# controllers at compile time |
| No visibility into agent activities | Real-time SignalR tracing dashboard |
| Agents loop and call destructive endpoints | Circuit breaker + per-agent daily token budgets |
| Redundant LLM calls cost thousands/month | Semantic caching in Redis keyed on intent, not raw URLs |
| PII leaking to LLMs | Privacy filter scrubs data before leaving the gateway |

---

## How It Works

```
Agent → [Ithil Gateway] → Your C# Microservice
              ↓
    1. Verify agent identity (JWT / API Key)
    2. Check daily token budget
    3. Verify tool is in agent's allowlist
    4. Forward request with trace ID injected
    5. Scrub PII from response
    6. Record token usage
    7. Fire real-time trace event to dashboard
```

Developers mark their controller methods with a single attribute:

```csharp
[AgentTool("Returns stock levels for a SKU",
    Category = "Inventory",
    RequiredScopes = ["inventory.read"],
    AllowWrite = false,
    MaxResponseTokens = 500)]
public async Task<IActionResult> GetInventory(int productId, string warehouseId)
```

A Roslyn Source Generator picks these up at compile time and produces an MCP-compliant schema registry — zero runtime reflection cost.

---

## Core Components

| Component | Description |
|---|---|
| `AgentTool` Attribute | Marks C# controller methods as MCP-callable tools |
| YARP Gateway | Request/response transform pipeline (auth, budget, PII, tracing) |
| Roslyn Source Generator | Compiles `[AgentTool]` methods into a schema registry at build time |
| MCP Protocol Layer | JSON-RPC 2.0 endpoints (`/.well-known/mcp`, `/mcp`, `/mcp/sse`) |
| Budget Engine | Redis-backed per-agent daily token ledger with 429 enforcement |
| Agent Identity | JWT (enterprise) and API Key (self-serve) verification |
| Privacy Filter | Regex-based PII scrubber (emails, SSNs, credit cards, custom rules) |
| Semantic Cache | Redis vector search deduplicates intent-equivalent requests |
| Circuit Breaker | Polly circuit breaker for downstream failure protection |
| SignalR Tracing | Real-time hub broadcasting tool call events to the dashboard |
| Audit Log | Structured JSON audit trail (agent, tool, params, outcome, latency) |
| Dashboard | Next.js 15 management UI (tool library, agent registry, live feed) |

---

## Tech Stack

- **Gateway:** C# / .NET 9+, YARP, Polly, LanguageExt
- **Real-time:** SignalR
- **Cache / Budget:** Redis (Redis Stack with vector search)
- **Auth:** OIDC / JWT, API Keys
- **Source Gen:** Roslyn Incremental Source Generators
- **Dashboard:** TypeScript / Next.js 15, Tailwind CSS v4, Shadcn/UI
- **Infrastructure:** Docker, Azure

---

## Project Status

This project is currently in the **design and planning phase**. No source code has been written yet.

| Sprint | Feature | Status |
|---|---|---|
| 1 | `[AgentTool]` attribute + YARP scaffolding | Planned |
| 2 | Roslyn Source Generator + MCP endpoints | Planned |
| 3 | Budget Engine + JWT auth + Management API | Planned |
| 4 | Dashboard v1: Tool Library, Agent Registry, Tool Tester | Planned |
| 5 | SignalR hub + Live Trace Feed + Budget gauge | Planned |
| 6 | Semantic Cache + Privacy Filter | Planned |
| 7 | Circuit Breaker + Audit Log + Docker Compose | Planned |
| 8 | Stripe billing + SaaS onboarding + Landing page | Planned |

---

## Repository Structure

```
/Ithil/
├── Design/
│   ├── Overview.md               # Architecture overview
│   ├── Backlog/                  # One design doc per feature (acceptance criteria + test plans)
│   ├── InProgress/               # Features currently being implemented
│   └── Completed/                # Finished feature designs
├── plan.md                       # Full build and go-to-market plan
├── CLAUDE.md                     # Coding standards and collaboration guidelines
└── README.md
```

---

## Pricing

| Tier | Price | Requests/Month |
|---|---|---|
| Developer | Free (OSS, self-host) | 10k |
| Startup | $199/mo | 500k |
| Professional | $999/mo | 5M |
| Enterprise Self-Hosted | $500/mo | Unlimited |
| Cloud Consumption | $0.01/request | Pay-as-you-go |

---

## Design Docs

Each planned component has a dedicated design document in [`/Design/Backlog/`](./Design/Backlog/) covering:

- What the component does and why
- Acceptance criteria
- Files and functions to be built
- Unit testing plan (written before implementation)
- Mermaid flow diagrams

See [`/Design/Overview.md`](./Design/Overview.md) for the full architecture and [`/plan.md`](./plan.md) for the complete build and go-to-market strategy.
