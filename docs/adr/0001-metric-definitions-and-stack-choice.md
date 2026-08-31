# ADR-0001: Metric Definitions and Stack Choice

**Status:** Accepted
**Date:** 2026-08-27

## Context

We're building a self-hosted dashboard that computes the four DORA metrics (deployment
frequency, lead time for changes, change failure rate, mean time to recovery) from GitHub
activity, for one or more teams, driven entirely by a config file rather than hardcoded to one
org's setup.

Two things needed deciding up front: how to actually *detect* deployments, PR lead time, and
incidents from raw GitHub data (there's no single canonical source for any of these), and what
stack to build on.

## Decision: metric definitions (v1)

- **Deployment frequency** — count of deployments in the window, divided by the number of days in
  the window. What counts as a "deployment" is configurable via `deploymentDetection.strategy`:
  either GitHub Releases (`github-release`) or merge commits to the default branch (`main-merge`).
  A third strategy (tagged workflow runs) is defined in the config schema but not yet implemented —
  selecting it fails fast at sync time instead of silently falling back to a different strategy.
- **Lead time for changes** — for each merged PR, the time until the *next* deployment at or
  after the merge. This is an approximation: it assumes the next deployment after a PR merges is
  the one that shipped it, which holds for repos that deploy frequently but can overstate lead
  time for repos that batch several PRs into one release.
- **Change failure rate** — percentage of deployments followed by an incident (an issue labeled
  per `incidentDetection.labels`) opened within 24 hours. The correlation is time-based, not an
  explicit deployment-incident link, which is the simplification most likely to need revisiting
  if the numbers look wrong for a given repo's release cadence.
- **Mean time to recovery** — average time between an incident's `OpenedAt` and `ResolvedAt`
  (its GitHub issue being closed), for incidents resolved within the window.

These are documented here specifically so they're falsifiable — if a team's numbers look wrong,
this is the first place to check which assumption doesn't hold for their workflow, rather than
treating the dashboard's output as ground truth.

## Decision: stack

- **.NET 8 (ASP.NET Core Minimal API) backend** — plays to existing team strength; EF Core +
  PostgreSQL for storage, matching what a real engineering org would run rather than SQLite.
- **React + TypeScript (Vite) frontend**, calling the API over REST with a typed client
  generated from the OpenAPI spec, so the contract between the two is enforced at compile time.
- **Config-driven** (`dora.config.yaml`) rather than code-driven — teams, repos, and detection
  rules are data, not source changes, which is what makes this a tool another team could pick up
  rather than a personal script.
- **Metric calculators as a strategy interface** (`IMetricCalculator`) registered in DI as a
  collection — adding a fifth metric is a new class, not a change to existing ones.

## Consequences

- The lead-time and change-failure-rate approximations mean the numbers are directionally
  useful, not audit-grade, until deployment-PR and deployment-incident linking becomes explicit
  (tracked as a v2 improvement in the build plan's Phase 4).
- Config-driven detection means onboarding a new team is editing YAML, not touching code — but it
  also means a misconfigured label or repo name fails silently rather than loudly; validating
  `dora.config.yaml` on startup is worth adding before this goes further than a personal project.
