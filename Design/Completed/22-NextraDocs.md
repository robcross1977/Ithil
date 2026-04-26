# 22 — Nextra Documentation Site

## Summary

Build a Nextra-powered documentation site hosted at `docs.ithil.software` as a separate Vercel project. This is the destination for all "Docs" links on the marketing site.

## Motivation

The marketing site links to `https://docs.ithil.software` throughout. Until this exists, those links 404. A README is not a substitute for documentation on a paid product — developers will not trust or buy something they cannot understand how to use.

## Stack

- **Framework:** [Nextra](https://nextra.site) (Next.js-based, Vercel-native)
- **Hosting:** Vercel (separate project from `ithil-web`)
- **Domain:** `docs.ithil.software` (subdomain of existing Vercel domain)
- **Repo:** New repo — `Ithil-Docs` under the same GitHub account

## Minimum Viable Docs (launch target)

These pages must exist before the marketing site goes fully live:

1. **Getting Started** — what Ithil is, what you need, 5-minute setup
2. **Installation** — NuGet package, Docker, appsettings.json
3. **Configuration** — all `Ithil:*` config keys explained
4. **`[AgentTool]` Reference** — attribute parameters, examples, scope values
5. **Licensing** — how keys work, where to put them, non-commercial vs commercial

## Nice to Have (post-launch)

- Agent token budgeting guide
- Privacy filter configuration
- Semantic cache setup
- Multi-model routing examples
- Changelog

## Notes

- Nextra chosen over Mintlify ($150/mo) and GitHub Wiki (looks like an afterthought)
- Vercel hosting makes deployment free and consistent with the marketing site
- All docs links on `ithil.software` currently point to `https://docs.ithil.software` — no temporary redirect needed, just ship the docs before or shortly after launch
