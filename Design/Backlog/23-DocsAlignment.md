# Feature 23 — Docs Alignment

The Ithil-Docs site describes several things that either don't exist yet in the codebase or don't match how the code actually works. This feature closes every gap so the docs accurately describe what a developer will experience when they follow them.

---

## Gap inventory

### 1. Schema URL mismatch — easy fix

The docs say the gateway polls `/mcp-schema` and that `MapIthilSchema` registers `GET /mcp-schema`. The code does neither: `ToolRegistryOptions.SchemaPath` defaults to `/ithil/schema`, and `WebApplicationExtensions.MapIthilSchema` registers `GET /ithil/schema`.

Every reference to `/mcp-schema` in the docs is wrong. Pick one URL and make both sides agree.

**Affected docs files:**
- `content/installation/downstream.mdx` (3 occurrences)
- `content/installation/gateway.mdx` (1 occurrence)
- `content/configuration.mdx` (1 occurrence)
- `content/reference/agent-tool-attribute.mdx` (1 occurrence)

**Recommended fix:** Keep `/ithil/schema` (it's already in the code) and update all doc references. No code change required.

---

### 2. `AddIthilHosting()` doesn't exist

The quickstart and downstream installation docs both show:

```csharp
builder.Services.AddIthilHosting();
```

This method does not exist. `Ithil.Hosting` only provides `MapIthilSchema`. The `SampleApi/Program.cs` doesn't call it either.

**Options:**
- Add an empty `AddIthilHosting()` extension method to `Ithil.Hosting` as a future hook (keeps the docs true and gives a place to hang DI registrations later)
- Remove the call from the docs

---

### 3. NuGet packaging not set up

The quickstart shows `dotnet add package Ithil.Hosting` but `Ithil.Hosting.csproj` has no `<PackageId>`, `<Version>`, or other NuGet metadata. There is no CI/CD pipeline to pack or publish. Running `dotnet add package Ithil.Hosting` fails.

**What needs to happen:**
- Add NuGet metadata to `Ithil.Hosting.csproj` (and `Ithil.Attributes.csproj`, since the attribute lives there — `Ithil.Hosting` will need to re-export it or the user will need both packages)
- Decide on versioning strategy (SemVer, pre-release, etc.)
- Add a GitHub Actions workflow that packs and publishes to NuGet.org on release tag

---

### 4. Docker image not published

The docs show `docker pull ithilsoftware/gateway:latest` and `docker run ithilsoftware/gateway:latest`. The image doesn't exist on Docker Hub. The Dockerfile builds correctly locally but has never been pushed.

**What needs to happen:**
- Create `ithilsoftware` organisation (or personal account) on Docker Hub
- Add a GitHub Actions workflow that builds the image and pushes `ithilsoftware/gateway:latest` (and a versioned tag) on release

---

### 5. `dotnet tool install -g Ithil.Gateway` not set up

The gateway installation doc mentions installing as a global dotnet tool:

```bash
dotnet tool install -g Ithil.Gateway
ithil-gateway
```

No global tool packaging exists. `Ithil.Gateway.csproj` has no `<PackAsTool>true</PackAsTool>` or `<ToolCommandName>`.

**What needs to happen:**
- Add `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>ithil-gateway</ToolCommandName>` to `Ithil.Gateway.csproj`
- Include in NuGet publish pipeline
- Or remove this from the docs if the tool install path isn't a priority

---

### 6. `RequiredScopes` is not enforced

The `[AgentTool]` attribute reference says:

> `RequiredScopes` — OAuth scopes the calling agent's JWT must contain. The gateway returns `403` if any required scope is absent.

This is false. The source generator reads `RequiredScopes` and includes it in the schema. The gateway's `ToolRegistryService` deserializes it. But nothing in the gateway pipeline checks JWT scopes against `RequiredScopes`. The 403 never happens.

**Options:**
- Implement scope enforcement in `ToolAllowlistService` or a new middleware step
- Remove `RequiredScopes` from the docs until it's implemented

---

### 7. `MaxResponseTokens` is not enforced

The attribute reference says:

> `MaxResponseTokens` — Token ceiling on the response body. The gateway truncates or rejects responses exceeding this limit before they reach the agent.

The value is carried through the attribute → source generator → schema endpoint → `ToolRegistryService` → `ToolRegistryEntry`. But `ToolCallGovernancePipeline` and `ResponseTransformPipeline` never read it. No truncation or rejection happens.

**Options:**
- Implement enforcement in `ToolCallGovernancePipeline.RunGovernedCallAsync` after counting tokens
- Remove the truncation claim from the docs until implemented

---

### 8. GitHub Actions CI/CD absent

There is no `.github/workflows/` directory. The docs reference a GitHub releases page for the ONNX model and imply automated publishing. Nothing is automated.

**Minimum workflows needed:**
- `ci.yml` — run `dotnet build` and `dotnet test` on every PR
- `release.yml` — on version tag: pack NuGet, push Docker image, create GitHub release

---

### 9. `docker-compose.yml` is incomplete

`docker/docker-compose.yml` only defines the Redis service. The docs show a full compose file that includes the gateway alongside Redis. The existing file can't be run as shown in the docs.

**Fix:** Add the gateway service to `docker/docker-compose.yml` to match what's shown in the installation guide.

---

## Recommended order of work

1. **Schema URL fix** — 15-minute doc edit, no code. Unblocks anyone trying to follow the quickstart today.
2. **`AddIthilHosting()` stub** — small code addition, eliminates a confusing compile error.
3. **`docker-compose.yml`** — complete the gateway service definition.
4. **GitHub Actions CI** — automated build/test on PR.
5. **NuGet packaging** — metadata + publish workflow.
6. **Docker Hub publishing** — build/push workflow.
7. **`RequiredScopes` enforcement** — actual feature work.
8. **`MaxResponseTokens` enforcement** — actual feature work.
9. **Global tool packaging** — lowest priority, can be cut if not needed.

---

## Acceptance criteria

- [ ] Developer can follow the quickstart from top to bottom without hitting a dead end
- [ ] `dotnet add package Ithil.Hosting` installs the package from NuGet.org
- [ ] `docker pull ithilsoftware/gateway:latest` succeeds
- [ ] `docker-compose up` in `/docker` brings up both Redis and the gateway
- [ ] Every `/mcp-schema` reference in the docs has been updated to `/ithil/schema` (or vice versa)
- [ ] `builder.Services.AddIthilHosting()` compiles without error
- [ ] PRs trigger automated build and test
- [ ] Release tags trigger NuGet publish and Docker push
- [ ] `RequiredScopes` enforcement: gateway returns `403` when a required scope is absent from the JWT
- [ ] `MaxResponseTokens` enforcement: responses over the limit are truncated or rejected by the pipeline
