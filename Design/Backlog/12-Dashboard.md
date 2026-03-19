# Feature: Developer Dashboard (Blazor Server)

## What It Is

An operator dashboard embedded directly in the Gateway as a Razor Class Library (RCL).
Consumers opt in by calling `app.UseIthilDashboard()` — no separate container, no Node.js,
no build step. The dashboard is a devops tool for the engineers and operators running the
Gateway, not a public-facing application.

Dashboard v1 scope is the **live operational view**: what is happening right now, which
agents are active, circuit breaker states, and budget burn. Compliance query (paginated
audit log search with filters) is explicitly phase two and out of scope here.

---

## Technology Decisions

| Decision | Choice | Reason |
|---|---|---|
| UI framework | Blazor Server | .NET teams evaluating Ithil should not need Node.js tooling to extend or contribute to the dashboard |
| Component system | MudBlazor | Covers tables, badges, tabs, layout — no custom component primitives needed |
| Charts | Chart.js via JS interop | Well-established pattern; does not require abandoning Blazor for the rest of the UI |
| Delivery | Razor Class Library with embedded static assets | Installs as a NuGet package; consumers call one middleware extension method |
| Auth | Reuse existing Gateway JWT / API key middleware | No new auth system; dashboard routes are additional paths in the same pipeline |

---

## Functional Programming Boundary

Blazor components are stateful and imperative by nature. The resolution is a strict
boundary: **components are view-only shells.** All filtering, aggregation, state
transitions, and validation live in pure C# service classes returning `Either<DashboardError, T>`
or `Option<T>`. The component calls the service, pattern-matches the result, and renders.

The 75-line file limit applies: split each component into a `.razor` template and a
`.razor.cs` code-behind. If the code-behind is long, the logic belongs in a service.

---

## Architecture

```mermaid
flowchart TD
    A[Browser] -->|Blazor Server / SignalR circuit| B[Ithil.Dashboard RCL]
    B -->|Injected services| C[Ithil.Management\nAgent config, budget reads]
    B -->|Injected services| D[Ithil.Core\nITraceNotifier subscription]
    B -->|Static assets| E[wwwroot — embedded in RCL]
    F[Consumer app.UseIthilDashboard] --> B
```

---

## Page Structure

```mermaid
flowchart TD
    A[Dashboard] --> B[/ Overview\nActive agents · request rate · budget burn today]
    A --> C[/trace Live Trace Feed\nReal-time SignalR event stream]
    A --> D[/circuit Circuit Breakers\nState per cluster — closed / open / half-open]
    A --> E[/budget Budget Overview\nPer-agent token burn and daily limits]
    A --> F[/agents Agent Management\nCRUD for agent configuration]
    C --> G[Unidentified Traffic View\nDedicated section for requests with no resolved AgentId]
```

---

## Scaling and Multi-Instance Deployments

Blazor Server holds an open SignalR circuit per connected client. For an internal operator
tool with 5–20 concurrent users this is not a scaling concern. The concern that affects
general web apps (thousands of concurrent users) does not apply here.

The real multi-instance risk is the TraceHub: if two Gateway instances are running behind
a load balancer, a trace event fired on instance A is only broadcast to clients connected
to instance A. Clients on instance B see a partial feed.

Two supported solutions, in order of preference:

**Option 1 — Redis backplane (recommended for production)**
Wire `Microsoft.AspNetCore.SignalR.StackExchangeRedis` into the SignalR configuration.
Every broadcast is published to Redis; all instances subscribe and fan out to their local
clients. StackExchange.Redis is already a Gateway dependency.

**Option 2 — Sticky sessions (simpler, no extra infrastructure)**
Configure the load balancer to pin each client to one Gateway instance for the duration
of their session. Less robust under instance failure but requires no additional config.

> **Required:** A `README.md` must exist in `Ithil.Dashboard/Hubs/` explaining the
> backplane problem, both solutions, and how to configure each. This is a hard requirement
> — any developer who touches the hub code must be able to understand the multi-instance
> behavior without researching it externally.

Under high agent traffic volume, the hub should support **event sampling / rate limiting**:
batching or sampling trace events rather than broadcasting every one. This keeps the
dashboard usable under load and is a configuration option, not a default restriction.

---

## Unidentified Traffic

Requests where `AgentIdentity` resolution fails arrive with a null or missing `AgentId`.
This must be treated as a first-class event type, not noise. The trace feed must have a
dedicated section or filter for unidentified traffic. A blank cell in the agent column is
not acceptable — operators need to see these events prominently because they may indicate
a misconfigured client, a leaked key being probed, or a scanning attempt.

---

## Project Structure

```
src/
└── Ithil.Dashboard/
    ├── Ithil.Dashboard.csproj          -- Razor Class Library, targets net10.0
    ├── DashboardMiddlewareExtensions   -- app.UseIthilDashboard() entry point
    ├── Hubs/
    │   ├── DashboardHubConnection      -- wraps TraceHub subscription logic
    │   └── README.md                   -- REQUIRED: backplane explanation for future devs
    ├── Pages/
    │   ├── Overview.razor / .razor.cs
    │   ├── TraceFeed.razor / .razor.cs
    │   ├── CircuitBreakers.razor / .razor.cs
    │   ├── BudgetOverview.razor / .razor.cs
    │   └── Agents.razor / .razor.cs
    ├── Components/
    │   ├── TraceEventRow.razor / .razor.cs
    │   ├── UnidentifiedTrafficRow.razor / .razor.cs
    │   ├── CircuitStateIndicator.razor / .razor.cs
    │   ├── BudgetGauge.razor / .razor.cs
    │   └── LatencyChart.razor / .razor.cs  -- JS interop
    ├── Services/
    │   ├── TraceFeedService.cs         -- pure; filters, aggregates, returns Option/Either
    │   ├── BudgetDashboardService.cs   -- pure; reads budget state, returns Either
    │   ├── CircuitDashboardService.cs  -- pure; maps Polly state to view model
    │   └── AgentManagementService.cs   -- pure; CRUD against IAgentRepository
    └── wwwroot/                        -- embedded static assets
```

---

## Acceptance Criteria

### General
- [ ] Mounted via `app.UseIthilDashboard()` — nothing appears if the call is absent
- [ ] All dashboard routes require the same JWT / API key auth the Gateway already validates
- [ ] No separate container, Node.js toolchain, or build step required by the consumer
- [ ] `README.md` exists in `Hubs/` explaining the SignalR backplane problem and both configuration options

### Overview (`/`)
- [ ] Shows count of active agents, request rate (last 60s), and total token spend today
- [ ] Data refreshes on a configurable interval (default 10s)

### Live Trace Feed (`/trace`)
- [ ] Connects to TraceHub on page load, disconnects on navigation away
- [ ] Events appear newest-first, capped at a configurable ring buffer size (default 200)
- [ ] Blocked and error events are visually distinct from successful events
- [ ] Each event shows: timestamp, agentId (or "Unidentified"), toolName, status, tokensUsed, latencyMs, circuitState
- [ ] Unidentified traffic (null agentId) appears in a dedicated, visually distinct section — not mixed silently into the main feed
- [ ] A filter control allows narrowing the feed by agentId or status

### Circuit Breakers (`/circuit`)
- [ ] Shows one row per monitored cluster
- [ ] Each row displays the current Polly state: Closed, Open, or HalfOpen
- [ ] HalfOpen state is visually distinct and labelled — it is not treated as Open
- [ ] Last state-change timestamp is shown per cluster

### Budget Overview (`/budget`)
- [ ] Lists all agents with: daily budget, tokens used today, percentage consumed
- [ ] Budget gauge shows a warning color when consumption exceeds a configurable threshold (default 80%)
- [ ] Values update live via SignalR when a new trace event arrives with token data

### Agent Management (`/agents`)
- [ ] Lists all registered agents with: ID, label, budget, active status
- [ ] New Agent form: label, daily token budget, allowed tools (multi-select)
- [ ] Agent API key is shown once after creation and cannot be retrieved again
- [ ] Revoke Agent button calls delete with a confirmation dialog before acting

---

## Out of Scope (Phase Two)

- Paginated audit log search with date/agent/outcome filters
- Tool library and tool tester (may move here from a separate feature or remain separate)
- Gateway settings management via the dashboard UI

---

## Unit Testing Plan

Tests live in `Ithil.Dashboard.Tests/`.

### TraceFeedService
- `TraceFeedService_FiltersEventsByAgentId` — given 5 events for two agents, filtering by one agentId returns only that agent's events
- `TraceFeedService_IdentifiesUnidentifiedTraffic` — events with null agentId are returned separately from identified traffic
- `TraceFeedService_CapsBufferAtConfiguredSize` — adding events beyond the cap drops the oldest

### CircuitDashboardService
- `CircuitDashboardService_MapsClosedState` — Polly Closed maps to correct view model value
- `CircuitDashboardService_MapsHalfOpenState` — Polly HalfOpen maps correctly and is not treated as Open

### BudgetDashboardService
- `BudgetDashboardService_ReturnsWarning_WhenThresholdExceeded` — at 81% consumption with 80% threshold, returns warning state
- `BudgetDashboardService_ReturnsSome_ForKnownAgent` — returns `Some` for a registered agent
- `BudgetDashboardService_ReturnsNone_ForUnknownAgent` — returns `None` for an unrecognised agentId

### AgentManagementService
- `AgentManagementService_ReturnsRight_OnSuccessfulCreate`
- `AgentManagementService_ReturnsLeft_WhenAgentIdAlreadyExists`
- `AgentManagementService_ReturnsNone_WhenDeletingUnknownAgent`
