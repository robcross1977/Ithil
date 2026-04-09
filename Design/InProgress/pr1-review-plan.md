# PR #1 Review Comment Resolution Plan

PR: `ft_14_McpSdk` — MCP SDK Migration
Copilot ran two review passes. Below is every substantive comment, current status, and the proposed fix.

---

## Comment 1 — Governance pipeline bypass (OUTDATED, SKIP)

**File:** `ToolProxyAIFunction.cs` lines +24–28
**Status:** Marked **Outdated** by GitHub — fixed in commit `afcf491` by adding `ToolCallGovernancePipeline` and routing all tool calls through it.
**Action:** None — already resolved.

---

## Comment 2 — Allowlist filtering is O(n×m) instead of O(n)

**File:** `McpSessionConfiguration.cs` lines +29–31
**Comment:** `names.Contains(t.Name)` iterates the allowlist `Seq<string>` for every tool, making filtering O(|tools|×|allowed|). Should be O(n).

**Fix:** Before the `Filter` call, convert `names` to a `HashSet<string>` so each lookup is O(1).

```csharp
// change this:
Some: names => allTools.Filter(t => names.Contains(t.Name)),

// to this:
Some: names =>
{
    var nameSet = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
    return allTools.Filter(t => nameSet.Contains(t.Name));
},
```

**Who:** User writes (source .cs file).

---

## Comment 3 — Generator test assertions only check field names exist, not their values

**File:** `tests/Ithil.SourceGenerator.Tests/AgentToolGeneratorTests.cs`
**Comment:** `SingleTool_EmitsHttpMethodAndRoutePatternFields` only asserts `"HttpMethod"` and `"RoutePattern"` appear in the output — not that they have correct values. HTTP verb defaulting and parameter source inference could silently regress.

**Fix:** Add two new tests:
- Assert that a `[HttpGet]`-decorated method emits `HttpMethod = "GET"` (not just the string `"HttpMethod"`).
- Assert `AllowWrite` defaults to `false`.

**Who:** Claude writes (test file).

---

## Comment 4 — Route-pattern test doesn't assert HttpMethod or parameter source classification

**File:** `tests/Ithil.SourceGenerator.Tests/AgentToolGeneratorTests.cs`
**Comment:** `SingleTool_EmitsRoutePatternInEntry` verifies the combined route template but doesn't assert the `HttpMethod` value or that the `sku` parameter is classified as a route source in `ParameterSources`.

**Fix:** Extend or add a test that also asserts:
- `HttpMethod = "GET"` for an `[HttpGet]`-decorated method.
- `ParameterSources` contains `"sku"` mapped to `"route"`.

**Who:** Claude writes (test file).

---

## Comment 5 — Gateway MCP tests deleted without replacement

**File:** `tests/Ithil.Gateway.Tests/Mcp/` (McpDispatcherTests.cs, ToolsListHandlerTests.cs deleted)
**Comment:** The old dispatcher/handler tests were removed but no replacement tests cover the new SDK wiring: auth enforcement, per-session tool filtering, or the MCP endpoint itself.

**Fix:** Add tests in `Ithil.Gateway.Tests/Mcp/` (Claude writes the test file):
1. `McpPost_Returns401_WithNoToken` — verify `POST /mcp` with no JWT returns 401 before SDK processes the request.
2. `McpSessionConfiguration_FiltersTools_ByAllowlist` — given an agent with a restricted allowlist, assert `ConfigureSessionAsync` only registers allowed tools.
3. `McpSessionConfiguration_ExposesAllTools_WhenNoAllowlist` — agent with no allowlist gets all tools.

**Who:** Claude writes (test file). Note: test #1 is an integration test that requires `WebApplicationFactory`; tests #2–3 are unit tests that mock `IToolAllowlistService` and `IToolRegistry`.

---

## Comment 6 — `throw ex` resets the stack trace

**File:** `ToolCallGovernancePipeline.cs`
**Comment:** In the `result.Match(Fail: ex => throw ex)` branch, rethrowing with `throw ex` discards the original call site and stack frames. Downstream failures become very hard to diagnose.

**Fix:**
```csharp
// change:
Fail: ex => throw ex);

// to:
Fail: ex => { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw(); return string.Empty; });
```
Or restructure to use a local variable and `throw;` inside a real catch block.

**Who:** User writes (source .cs file).

---

## Comment 7 — `cancellationToken` accepted but not propagated in governance pipeline

**File:** `ToolCallGovernancePipeline.cs`
**Comment:** `ExecuteAsync` accepts `CancellationToken` but the async calls to `budgetEngine`, `traceNotifier`, and `auditLogger` inside the method don't receive it. MCP session cancellation won't stop in-flight work.

**Fix:** Pass `cancellationToken` to each async call:
```csharp
var isWithinBudget = await budgetEngine.IsWithinBudgetAsync(agentId, cancellationToken);
await traceNotifier.NotifyAsync(..., cancellationToken);
await auditLogger.WriteAsync(..., cancellationToken);
await budgetEngine.RecordUsageAsync(agentId, tokensUsed, cancellationToken);
```
Check the interface signatures first — if they don't accept a token yet, add the parameter.

**Who:** User writes (source .cs files for interfaces and implementations).

---

## Comment 8 — Semantic cache not wired into tool call path

**File:** `ToolProxyAIFunction.cs` (and `ToolCallGovernancePipeline.cs`)
**Comment:** The design doc explicitly requires injecting `ISemanticCacheService` into the tool proxy to check for cache hits before calling downstream and write back on success. Currently the cache is completely bypassed.

**Fix:** Inject `ISemanticCacheService` into `ToolProxyAIFunction` (or into `ToolCallGovernancePipeline`) and wrap the downstream call:

```csharp
// In ToolProxyAIFunction or ToolCallGovernancePipeline:
var cacheKey = BuildCacheKey(toolName, argsJson);
var cached = await semanticCache.TryGetAsync(cacheKey);
if (cached.IsSome)
    return cached.Value;

var result = await governance.ExecuteAsync(...);
await semanticCache.SetAsync(cacheKey, result);
return result;
```

This is the largest change. The design doc already specifies the flow in its Mermaid diagram.

**Who:** User writes (source .cs files). We will need to decide: inject in `ToolProxyAIFunction` or in `GovernancePipeline`. The design doc says "inside the tool proxy" — so `ToolProxyAIFunction` is the right place.

---

## Comment 9 — Design doc still references obsolete generated proxy approach

**File:** `Design/InProgress/14-McpSdkMigration.md`
**Comment:** The "Open Questions / Blockers" section still references `McpToolProxies.g.cs`, `[McpServerToolType]`, the `McpToolProxyEmitter.cs` generator, and typed `ExecuteAsync` parameters — all of which were abandoned in favour of `SchemaRegistry.g.cs`. This section will confuse future readers.

**Fix:** Update the doc to:
1. Remove the `McpToolProxyEmitter.cs` entry from "Files & Functions / New / Modified".
2. Remove the "Generated Proxy Parameters" blocker (now irrelevant).
3. Update the "SDK API — Per-Session Tool Filtering" blocker status to ✅ DONE.
4. Move the whole document to `Design/Completed/` (or keep in InProgress if semantic cache is still outstanding).

**Who:** Claude writes (doc file).

---

## Comment 10 — No unit tests for `ToolCallGovernancePipeline`

**File:** `src/Ithil.Gateway/Transforms/ToolCallGovernancePipeline.cs`
**Comment:** The governance pipeline has budget pre-check, trace events, PII scrubbing, token counting, and audit logging — but no tests. Any regression is silent.

**Fix:** Add `tests/Ithil.Gateway.Tests/Transforms/ToolCallGovernancePipelineTests.cs` with:
1. `ExecuteAsync_ThrowsInvalidOperationException_WhenBudgetExceeded` — budget returns false → exception.
2. `ExecuteAsync_ScrubsResponseAndRecordsUsage_OnSuccess` — happy path, verifies privacy filter called and usage recorded.
3. `ExecuteAsync_EmitsErrorTraceAndAudit_OnDownstreamFailure` — downstream throws → error trace + audit written, exception rethrown.
4. `ExecuteAsync_EmitsSuccessTraceAndAudit_OnSuccess` — happy path, verifies success trace + audit written.

**Who:** Claude writes (test file — show user first for approval before writing).

---

## Comment 11 — No tests for `McpSessionConfiguration` tool filtering

**File:** `src/Ithil.Gateway/Mcp/McpSessionConfiguration.cs`
**Comment:** Per-session tool filtering is core security behavior — restricted agents must only see allowed tools. No tests exist to prevent regressions.

**Fix:** Add tests in `tests/Ithil.Gateway.Tests/Mcp/McpSessionConfigurationTests.cs`:
1. `ConfigureSessionAsync_OnlyRegistersAllowedTools_WhenAllowlistPresent` — agent has allowlist → only those tools appear in options.ToolCollection.
2. `ConfigureSessionAsync_RegistersAllTools_WhenNoAllowlistConfigured` — no allowlist → all tools registered.
3. `ConfigureSessionAsync_RegistersNoTools_WhenAllowlistIsEmpty` — empty allowlist → zero tools.

**Who:** Claude writes (test file — show user first for approval before writing).

---

## Execution Order

| # | Comment | Who | Order |
|---|---------|-----|-------|
| 1 | SKIP — outdated | — | — |
| 9 | Update design doc | Claude | First (no deps) |
| 2 | HashSet allowlist | User | Early (simple) |
| 6 | `throw ex` → ExceptionDispatchInfo | User | Early (simple) |
| 7 | Propagate cancellationToken | User | After #6 |
| 3+4 | Generator test improvements | Claude | Parallel with code fixes |
| 10 | GovernancePipeline tests | Claude | After #6, #7 |
| 11 | McpSessionConfiguration tests | Claude | After #2 |
| 5 | Gateway MCP auth/allowlist tests | Claude | After all above |
| 8 | Semantic cache injection | User (large) | Last — most complex |
