# Nice To Have — Future Enhancements

These are features that would meaningfully improve Ithil but are not required for the
initial enterprise release. They are tracked here so they are not forgotten and can be
prioritised in later versions.

---

## Auth: JWKS / Asymmetric JWT Support (RS256 / ES256)

**What:** Support for validating JWTs signed with asymmetric keys (RS256 or ES256) via a
configurable JWKS URI. This is how enterprise identity providers work — Okta, Azure AD,
Auth0, and any OIDC-compliant provider publish their public keys at a well-known URL.
Ithil would fetch and cache those keys, then verify token signatures locally without any
per-request network call.

**Why it matters:** The current HS256 shared-key approach requires operators to generate
tokens manually with a shared secret. Enterprise customers will not accept this — they
have existing identity infrastructure and will expect Ithil to integrate with it. JWKS
support removes the shared secret entirely; the operator points Ithil at their JWKS URI
and their existing provider issues tokens.

**How to configure (target API):**
```csharp
builder.Services.AddIthilGateway(options =>
{
    options.Jwt.JwksUri = "https://your-org.okta.com/oauth2/v1/keys";
    options.Jwt.ValidIssuer = "https://your-org.okta.com";
    options.Jwt.ValidAudience = "ithil-gateway";
    // SigningKey no longer required when JwksUri is set
});
```

**What changes:** `ServiceCollectionExtensions` JWT configuration switches from
`IssuerSigningKey` (symmetric) to `IssuerSigningKeyResolver` (async JWKS fetch with
caching). Microsoft ships `Microsoft.IdentityModel.Protocols.OpenIdConnect` which handles
JWKS fetching and key rotation automatically.

**Priority:** High — likely requested by the first enterprise customer.

---

## Auth: Separate Management JWT Issuer (Option 3)

**What:** A completely separate token issuer for management API operations, independent
of the agent JWT issuer. Operators authenticate to the management API with tokens from a
dedicated admin identity provider, entirely separate from the provider that issues agent
tokens.

**Why it matters:** For large enterprises with strict separation of concerns — the team
that manages Ithil configuration may be a different team (platform/infra) from the one
whose systems run as agents. Using the same issuer for both conflates two distinct
identity domains.

**Current approach:** Management endpoints use the same JWT issuer as agents but require
an `admin` scope claim (Option 1, implemented in v1). This is sufficient for most
deployments.

**When to implement:** When a customer explicitly requires it, or when moving to a
multi-tenant model where different tenants have different identity providers.

---

## Multi-Tenancy

**What:** A single Ithil Gateway instance serving multiple independent teams or business
units, each with isolated agent registries, budgets, and tool allowlists.

**Why it matters:** Large enterprises often have central platform teams that want to run
one gateway for multiple internal consumers rather than one per team.

**Complexity:** Significant. Every keyed resource (agent config, budgets, cache entries,
circuit breaker state) would need a tenant prefix. The management API and dashboard would
need tenant context on every operation.

---

## Stripe Billing Integration

**What:** Built-in usage metering and billing for the hosted / SaaS path — agent token
usage reported to Stripe Billing, automatic subscription enforcement, customer portal.

**Why it matters:** Required if Ithil ever moves from self-hosted license to a cloud
consumption model.

**Dependency:** Requires a decision on whether Ithil will ever be offered as a managed
cloud service vs purely self-hosted.

---

## Kubernetes Operator / Helm Chart

**What:** A Kubernetes Operator that manages Ithil Gateway deployments declaratively via
CRDs, or a production-ready Helm chart covering all deployment scenarios.

**Why it matters:** Enterprise Kubernetes teams expect Helm charts. Providing one removes
a significant evaluation barrier.
