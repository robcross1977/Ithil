# Additional Architectural Considerations

## Dashboard Framework Recommendation: Blazor Server

### Recommendation

Use **Blazor Server**, embedded as middleware in the Gateway (or a thin `Ithil.Dashboard` project),
served via `app.UseIthilDashboard()`. Do not use Next.js or any JS framework.

The core reason is ecosystem alignment. Every company using Ithil has .NET developers. They are the
ones who will evaluate the library, file issues, fork the dashboard, and extend it internally.
Requiring them to context-switch into a Node.js ecosystem to contribute to or maintain the dashboard
is friction that compounds over time and reduces adoption of the OSS path.

SignalR is the secondary reason. Blazor Server runs over SignalR natively — the `TraceHub` connection
is not a client library concern, it is part of the framework. Reconnection logic, group subscriptions,
and real-time state updates are handled at the infrastructure level rather than written by hand.

For charts (latency sparklines, token burn graphs), use JS interop with `Chart.js` or `ApexCharts`
only. This is a well-established pattern and does not require abandoning Blazor for the rest of the UI.
Use `MudBlazor` for the component system — it covers tables, badges, tabs, and layout well.

### How It Should Be Done

**Project structure:**

```
src/
└── Ithil.Dashboard/
    ├── Ithil.Dashboard.csproj         -- Razor class library, targets net10.0
    ├── DashboardMiddlewareExtensions  -- app.UseIthilDashboard() entry point
    ├── Hubs/
    │   └── DashboardHubConnection     -- wraps TraceHub subscription logic
    ├── Pages/
    │   ├── TraceFeed                  -- live event stream, per-agent filter
    │   ├── CircuitBreakers            -- state machine display per cluster
    │   ├── BudgetOverview             -- per-agent token burn and daily limits
    │   └── Agents                     -- CRUD for agent configuration
    ├── Components/
    │   ├── TraceEventRow
    │   ├── CircuitStateIndicator
    │   └── LatencyChart               -- JS interop component
    └── wwwroot/                       -- embedded static assets
```

**Embedding approach:**

The `.csproj` sets `<EmbeddedResource>` on `wwwroot` contents and the project references
`Microsoft.AspNetCore.Components.WebAssembly.Server` for static file serving. Consumers
reference `Ithil.Dashboard` and call the middleware extension — no separate container,
no Node.js, no build step on their end.

**Auth:**

Plug into the existing ASP.NET auth middleware. The dashboard should require the same JWT
or API key that the Gateway already validates. Do not introduce a separate auth system.

### The Functional Programming Tension (Insight from CLAUDE.MD)

This is the honest conflict: Blazor components are stateful and imperative by nature.
They do not compose well with LanguageExt monads and will resist the functional style
that the rest of the codebase follows.

The resolution is a strict boundary: **Blazor components are view-only shells.**
All logic — filtering, aggregation, state transitions, validation — lives in pure C# service
classes that return `Either<DashboardError, T>` or `Option<T>`. The component calls the
service, pattern-matches the result, and renders. The component itself holds no business
logic and no mutable state beyond what the UI framework requires.

This is not a compromise on the functional principles — it is the same separation of
concerns that applies everywhere else. The component is equivalent to a controller action:
a thin boundary that delegates to pure logic.

The 75-line file limit applies cleanly here: split each component into a `.razor` template
and a `.razor.cs` code-behind. The code-behind should be short. If it is not, the logic
belongs in a service.

---

## Things You Are Not Thinking About Yet

### 1. Token Counting Is Wrong and Will Embarrass You

`ResponseTransformPipeline` approximates tokens by splitting on whitespace. This will be
meaningfully inaccurate for JSON payloads, code, and any non-English content — which is
exactly what enterprise API responses look like. The budget numbers the dashboard displays
will be wrong, and customers will notice when they compare to their LLM provider's billing.

`Microsoft.ML.Tokenizers` is already in the dependency tree. Wire it up before the
dashboard makes budget figures look authoritative.

### 2. Circuit Breaker Has Three States, Not Two

`AgentTraceEvent.CircuitState` carries `"open"` or `"closed"`. Polly has three states:
`Closed → Open → HalfOpen → Closed`. The half-open state — where Polly sends a probe
request to test if the downstream has recovered — is the most operationally interesting
moment. It is currently invisible to the dashboard.

Add `"half-open"` to the circuit state model and fire a trace event on `OnHalfOpened`
before the dashboard is built, or you will retrofit it later under pressure.

### 3. Unknown Agent Traffic Is a Security Surface, Not a Noise Problem

When `AgentIdentity` resolution fails, the trace event fires with a null or missing
`AgentId`. On the dashboard this will look like noise. It is not — it is potentially
a misconfigured client, a leaked key being probed, or a scanning attempt.

The dashboard needs a dedicated view for unidentified traffic, not just a blank cell
in the agent column. Treat it as a first-class event type from the start.

### 4. Audit Log and Trace Feed Are Different Products in the Same UI

The trace feed is real-time, ephemeral, and high-volume. It answers "what is happening
right now." The audit log is persistent, queryable, and compliance-relevant. It answers
"what happened last Tuesday and who authorized it."

These have completely different UX patterns — live tail versus paginated search with
filters — and different data retention requirements. Designing them as the same feature
will produce something that does neither well.

`11-AuditLog.md` is already in-progress. Before `12-Dashboard.md` moves out of Backlog,
decide explicitly which of those two things the first version of the dashboard is, and
leave the other as a clearly scoped phase two.

### 5. Load-Balanced Deployments Will Break SignalR on Day One

Companies running more than one instance of the Gateway behind a load balancer will see
clients randomly disconnecting from the `TraceHub` because their HTTP requests land on
different nodes. This is the SignalR sticky session / backplane problem.

Document the requirement explicitly and provide configuration hooks for the Redis backplane
(`StackExchange.Redis` is already a dependency). If you do not, the first enterprise
customer who runs two instances will file a critical bug and lose trust in the library.

### 6. The License Will Be Rejected by Enterprise Legal Before It Is Read

AGPL is the default assumption for "open source or pay" models. Many enterprise legal
teams have a blanket policy against AGPL due to concerns about copyleft propagation to
proprietary code. They will reject it without evaluating the actual risk.

**Business Source License (BUSL-1.1)** avoids this. It is well understood by enterprise
legal, used by HashiCorp and CockroachDB, and converts to Apache 2.0 after a defined
period. The commercial terms are explicit and narrow. Deals will get further.

### 7. Logging Philosophy Applies to the Tracing Pipeline Itself

CLAUDE.MD is clear: only log when something that should work is not working. This is
worth stating explicitly for the tracing layer because there is a temptation to log
every trace event to the application log as well as broadcasting it over SignalR.

The trace feed is observability, not logging. `ITraceNotifier.NotifyAsync()` should
fire and forget to SignalR. It should not write to `ILogger` on success. If the
SignalR broadcast itself fails, that is worth logging. Normal trace events are not.

### 8. There Is No Data Retention Strategy

The trace feed is currently in-memory. There is no defined answer to: how long are
events kept, where do they go when the process restarts, and what happens when event
volume is high. Customers will ask this on day one.

Decide the scope deliberately: ring buffer in memory only (simple, document the limit),
optional persistence to a pluggable store, or required Redis stream. Each has different
complexity and operational burden. The dashboard design depends on which answer you pick.
