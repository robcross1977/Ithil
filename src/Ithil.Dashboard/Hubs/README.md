# SignalR Backplane — Multi-Instance Deployments

## The Problem

Blazor Server holds an open SignalR circuit per connected browser tab. The dashboard
subscribes to live trace events via `DashboardHubConnection`, which registers a callback
with `ITraceSubscriptionManager`. `ITraceSubscriptionManager` is a singleton that lives
inside the Gateway process.

In a single-instance deployment this works fine. In a multi-instance deployment (e.g. two
Gateway pods behind a load balancer), each instance has its own `ITraceSubscriptionManager`.
A trace event fired on instance A only reaches clients connected to instance A. Clients on
instance B see a partial feed with no errors — the silence looks like low traffic.

## Solution 1 — Redis Backplane (Recommended for Production)

Wire `Microsoft.AspNetCore.SignalR.StackExchangeRedis` into the SignalR configuration.
Every broadcast is published to a Redis channel; all instances subscribe and fan out to
their local clients. StackExchange.Redis is already a Gateway dependency.

```csharp
builder.Services
    .AddSignalR()
    .AddStackExchangeRedis(connectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("ithil");
    });
```

`connectionString` is the same Redis instance used for budget and cache — pull it from
`ConnectionStrings:Redis` in configuration.

This is fully transparent to the dashboard components. No code changes required beyond
the one-line registration.

## Solution 2 — Sticky Sessions (Simpler, No Extra Infrastructure)

Configure the load balancer to pin each client to one Gateway instance for the duration
of their session (affinity by cookie or IP hash). Each client sees a complete feed from
their pinned instance.

Tradeoff: if the pinned instance restarts, the client loses their circuit and must
reconnect. The Redis backplane is resilient to instance failure; sticky sessions are not.

## Which to Choose

| Scenario | Recommendation |
|---|---|
| Single instance (dev, small teams) | Neither needed |
| Multi-instance, Redis already in use | Redis backplane |
| Multi-instance, no Redis budget | Sticky sessions |
| Kubernetes with rolling deploys | Redis backplane |
