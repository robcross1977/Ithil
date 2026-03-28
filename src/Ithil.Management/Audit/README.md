# Audit Log

Structured, append-only records of every agent request that flows through the gateway. Written as JSON Lines (one JSON object per line).

## Why stdout is on by default

`StdoutAuditSink` is registered automatically when `AddIthilAudit()` is called. Any log aggregator (Datadog, Splunk, ELK, Loki) that collects container stdout will pick up the audit stream without any configuration. Visibility should require opt-out, not opt-in — this is a governance tool.

## Disabling the stdout sink

Set the following in your configuration:

```json
{
  "Ithil": {
    "Audit": {
      "DisableStdoutSink": true
    }
  }
}
```

## Adding a custom sink

Implement `IAuditSink` and register it in DI:

```csharp
services.AddSingleton<IAuditSink, MyCustomSink>();
```

Multiple sinks can be registered — all receive every record. The built-in options are:

| Sink | Notes |
|---|---|
| `StdoutAuditSink` | On by default. Writes JSON Lines to stdout. |
| `FileAuditSink` | Appends JSON Lines to a rolling file. Configure path via `FileAuditSinkOptions`. |
| `ApplicationInsightsSink` | Writes as a custom event to Azure Application Insights. |

## Record format

Each line is a JSON object with these fields:

| Field | Type | Description |
|---|---|---|
| `timestamp` | string | ISO 8601 UTC |
| `traceId` | string | Correlates with the SignalR trace feed |
| `agentId` | string | The authenticated agent |
| `toolName` | string | The MCP tool that was called |
| `parameters` | string | Tool arguments — PII already scrubbed |
| `outcome` | string | `success`, `error`, `blocked`, or `cache-hit` |
| `tokensUsed` | int? | Token count, if known |
| `latencyMs` | int? | Downstream latency — null for cache hits |
| `cacheHit` | bool | True when served from semantic cache |
| `piiScrubbed` | bool | True when the privacy filter ran on parameters |
| `errorMessage` | string? | Set for `blocked` and `error` outcomes |
