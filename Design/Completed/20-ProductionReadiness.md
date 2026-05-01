# Feature 20 — Production Readiness

Built across PRs #8, #9, and #10. Three related operational concerns bundled under the same feature number: health checks, graceful shutdown, and Redis failure policy. The Dockerfile was also added as part of this work.

---

## What was built

### Health checks (PR #8)

Two health endpoints wired through ASP.NET's built-in health check infrastructure:

| Endpoint | Purpose |
|---|---|
| `GET /health/live` | Always `200 OK` if the process is alive. Kubernetes liveness probe. |
| `GET /health/ready` | `200 OK` only when Redis is reachable and the ONNX model file is loaded. Kubernetes readiness probe. |

The readiness probe blocks traffic until both dependencies are confirmed healthy. If either fails, the response is `503 Service Unavailable` with a JSON body naming the failed component.

**Files:**
- `src/Ithil.Gateway/Health/RedisHealthCheck.cs`
- `src/Ithil.Gateway/Health/EmbeddingModelHealthCheck.cs`
- `src/Ithil.Gateway/ServiceCollectionExtensions.cs` — registers both checks

### Graceful shutdown (PR #9)

The audit background worker drains any in-flight audit records before the process exits, bounded by `Ithil:Shutdown:TimeoutSeconds`. This prevents audit records from being silently lost when a container is stopped or rolled.

`HostOptions.ShutdownTimeout` is set from config at startup. The `AuditBackgroundWorker.ExecuteAsync` catches the `OperationCanceledException` from the stopping token and drains `Channel.Reader` synchronously before returning.

**Files:**
- `src/Ithil.Management/Audit/AuditBackgroundWorker.cs`
- `src/Ithil.Gateway/ServiceCollectionExtensions.cs` — configures `HostOptions`

**Config key:** `Ithil:Shutdown:TimeoutSeconds` (default: `25`)

### Redis failure policy (PR #10)

The budget engine can be configured to either fail-open or fail-closed when Redis is unreachable.

| Policy | Behaviour |
|---|---|
| `FailOpen` (default) | Redis exceptions are caught; budget check passes. Request continues. |
| `FailClosed` | Redis exceptions propagate; `RequestTransformPipeline` surfaces them as `503`. |

**Files:**
- `src/Ithil.Budget/BudgetEngine.cs`
- `src/Ithil.Budget/BudgetEngineOptions.cs`
- `src/Ithil.Core/Enums/RedisFailurePolicy.cs`

**Config key:** `Ithil:Budget:FailurePolicy` — `"FailOpen"` (default) or `"FailClosed"`

### Dockerfile

Multi-stage build: SDK image restores and publishes the gateway, then downloads the ONNX embedding model and vocabulary from HuggingFace (~23 MB) so the final image is self-contained. Runtime stage runs as a non-root user.

**Files:**
- `Dockerfile` (repo root)
- `.dockerignore`

---

## Acceptance criteria

- [x] `GET /health/live` returns `200` when the process is running
- [x] `GET /health/ready` returns `200` when Redis and ONNX model are both healthy
- [x] `GET /health/ready` returns `503` when Redis is down
- [x] `GET /health/ready` returns `503` when the ONNX model file is missing
- [x] Audit records queued before shutdown are drained before the process exits
- [x] `FailOpen` budget policy: Redis failure → request passes through
- [x] `FailClosed` budget policy: Redis failure → 503
- [x] `docker build` succeeds and produces a runnable image
- [x] Container listens on port 8080 by default
- [x] Container runs as non-root user
