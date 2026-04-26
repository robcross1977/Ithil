# Ithil Test Plan

Unit tests live in the `tests/` folder and cover isolated logic. This document covers everything above that layer — integration tests that wire real components together and the small set of scenarios that must be verified by hand.

---

## Part 1 — xUnit Integration Tests (`WebApplicationFactory` + TestContainers)

These tests spin up the full gateway in-process with a real Redis instance (via TestContainers) and a mock downstream HTTP service. No UI involved.

### API Key Authentication

| Scenario | Expected |
|---|---|
| Valid `X-Api-Key` header | Request passes through to downstream |
| Unknown API key | 401 Unauthorized |
| No auth header at all | 401 Unauthorized |
| API key for inactive agent | 401 Unauthorized |

### JWT Authentication

| Scenario | Expected |
|---|---|
| Valid Bearer token, agent exists | Request passes through |
| Token signed with wrong key | 401 Unauthorized |
| Expired token | 401 Unauthorized |
| Token with unknown `agentId` claim | 401 Unauthorized |
| No Authorization header | 401 Unauthorized |

### Budget Enforcement

| Scenario | Expected |
|---|---|
| Agent under daily limit | Request passes through |
| Agent at exactly the limit | 429 Too Many Requests |
| Agent over limit | 429 Too Many Requests |
| Redis unavailable (fail-open) | Request passes through |
| Usage resets at start of next day | Request passes through after reset |

### Tool Allowlist Enforcement

| Scenario | Expected |
|---|---|
| Agent calls allowed tool | Request passes through |
| Agent calls tool not in allowlist | 403 Forbidden |
| Agent has empty allowlist (all allowed) | Request passes through for any tool |
| Agent not found in registry | 403 Forbidden |

### MCP Tool Discovery & Routing

| Scenario | Expected |
|---|---|
| Downstream returns valid schema | Gateway exposes tools correctly |
| Same schema fetched twice | Only one downstream request (cached) |
| Downstream unreachable | Gateway returns empty tool list, does not crash |
| Tool call routed to correct downstream URL | Downstream receives request with correct params |
| Unknown tool name in call | Appropriate error returned |

### Semantic Cache

| Scenario | Expected |
|---|---|
| Identical tool call made twice | Second call returns cached response, no downstream hit |
| Similar (paraphrased) call above similarity threshold | Cache hit |
| Dissimilar call below threshold | Cache miss, downstream called |
| Redis unavailable | Cache miss, request continues (fail-open) |
| Embedding service throws | Cache miss, request continues (fail-open) |

### Privacy / PII Redaction

| Scenario | Expected |
|---|---|
| Downstream response contains email address | Email replaced with `[REDACTED]` before agent receives it |
| Response contains SSN (`123-45-6789`) | SSN redacted |
| Response contains credit card (spaces, dashes, none) | Card number redacted |
| Response contains no PII | Response passes through unchanged |
| Custom regex rule configured | Matching content redacted |

### Circuit Breaker

| Scenario | Expected |
|---|---|
| First 4 downstream failures | Requests still forwarded |
| 5th consecutive failure | Circuit opens, subsequent requests fail fast |
| Circuit open, probe request succeeds | Circuit closes, normal routing resumes |
| Circuit open, probe request fails | Circuit stays open |
| Circuit state event fires on open | Event contains correct agentId and toolName |

### Audit Logging

| Scenario | Expected |
|---|---|
| Successful tool call | Audit record written with agentId, tool, timestamp, result |
| Failed request (401, 429, 403) | Audit record written with correct status |
| Multiple sinks configured | All sinks receive the same record |
| Sink throws | Other sinks still receive record, error written to stderr |
| AuditLogger.WriteAsync | Returns to caller in < 100ms (non-blocking) |

---

## Part 2 — Playwright Tests (Blazor Dashboard UI)

These tests drive a real browser against the running dashboard. They verify that the UI reflects backend state correctly and that operator workflows are completable end-to-end.

### Operator Authentication & Dashboard Access

| Scenario | Expected |
|---|---|
| Navigate to dashboard without a session | Redirected to login page |
| Login with correct credentials | Redirected to dashboard overview |
| Login with wrong credentials | Error message shown, no redirect |
| Session expires or is cleared | Subsequent navigation redirects to login |
| Logout | Session cleared, redirected to login |

### Agent Management (Dashboard UI)

| Scenario | Expected |
|---|---|
| Create agent with valid name and budget | Agent appears in agent list |
| Create agent with zero budget | Validation error shown inline |
| Create agent with blank name | Validation error shown inline |
| Edit agent label | Updated label shown in list |
| Edit agent daily token budget | New budget reflected in budget overview |
| Add tool to allowlist | Tool appears in agent's allowlist |
| Remove tool from allowlist | Tool no longer appears |
| Delete agent | Agent removed from list |
| Delete agent that has active budget usage | Agent removed, no crash |

### Budget Overview (Dashboard)

| Scenario | Expected |
|---|---|
| Agent with no usage | Gauge shows 0% |
| Agent at 50% of budget | Gauge shows ~50%, no warning state |
| Agent above 80% of budget | Gauge shows warning color/indicator |
| Agent at 100% | Gauge shows full/danger state |
| Multiple agents shown | Each agent has its own gauge with correct values |

### Circuit Breaker State (Dashboard)

| Scenario | Expected |
|---|---|
| All circuits closed | Closed indicator shown for each downstream |
| Circuit opens (triggered by backend) | UI updates to open state without page refresh |
| Circuit recovers to closed | UI updates to closed state |
| Circuit in half-open state | Half-open indicator shown |

### Real-Time Trace Feed (Dashboard)

| Scenario | Expected |
|---|---|
| Agent makes a tool call | Trace event appears in feed without page refresh |
| Multiple agents active | Events from all agents appear in global feed |
| Filter feed to specific agent | Only that agent's events shown |
| Clear filter | All events visible again |
| Feed under rapid event load | UI remains responsive, events scroll correctly |

---

## Part 3 — Manual Tests

These scenarios are too environment-dependent or one-time in nature to automate reliably.

### Dev Mode / Local Setup

Verify that a developer can get from fresh clone to a running gateway with only config changes.

| Step | Expected |
|---|---|
| Clone repo, fill in `appsettings.Development.json` | No secret committed to source |
| Start Redis locally (Docker or native) | Connection established, no errors at startup |
| `dotnet run` in `Ithil.Gateway` | Gateway starts without exceptions |
| `GET /dev/token` on loopback | Returns a valid JWT for the dev agent |
| Use dev JWT to make a tool call | Request proxied to downstream correctly |
| Open dashboard in browser | Login page loads, login succeeds with dev credentials |

### Email Delivery (License Key)

These require a real inbox and cannot be meaningfully tested in CI.

| Scenario | Expected |
|---|---|
| Non-commercial registration with new email | Email arrives with license key and `appsettings.json` instructions |
| Non-commercial registration with existing email | Same key resent, no duplicate record in Supabase |
| Commercial purchase completes (Paddle — future) | Email arrives with commercial-tier key |
| Email format | Key is readable, instructions are correct, no broken links |
