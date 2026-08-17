# AuditVitals

> Find the SEO problems costing your website traffic.
>
> AuditVitals scans your website, prioritizes technical and content issues, and shows you exactly what to fix first.

An affordable, AI-assisted technical SEO auditing and reporting platform for small businesses, SEO consultants,
freelancers, agencies, and developers. This repository is a custom ASP.NET Core + Angular + PostgreSQL application —
not a WordPress plugin, and not a clone of any existing SEO tool's branding, UI, datasets, or scoring formulas.

**Status:** Milestone 1 — secure project foundation (URL submission, SSRF-safe crawling of a single homepage,
background job processing). The technical SEO rules engine, health scoring, multi-page crawling, authentication,
billing, and most of the planned pages are **not yet implemented** — see [Roadmap](#roadmap) below.

## Table of contents

- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Environment setup](#environment-setup)
- [Start PostgreSQL](#start-postgresql)
- [Apply database migrations](#apply-database-migrations)
- [Run the backend API](#run-the-backend-api)
- [Run the background worker](#run-the-background-worker)
- [Run the Angular frontend](#run-the-angular-frontend)
- [Run the tests](#run-the-tests)
- [Project structure](#project-structure)
- [Security model](#security-model)
- [Roadmap](#roadmap)

## Architecture

```
Angular UI  →  ASP.NET Core API  →  PostgreSQL  →  DB-backed job queue  →  Background worker  →  SSRF-safe crawler
```

This is a **modular monolith**, not microservices. `AuditVitals.Worker` is a separate process today only so it can
later be extracted behind a message queue (e.g. Azure Service Bus) without redesigning the domain or persistence
layers — the API and worker already only communicate through the database, never directly.

Solution layout:

```
src/
  AuditVitals.Domain/          Entities, enums — no dependencies on anything else
  AuditVitals.Application/     Use cases, SSRF validator, safe HTTP fetcher abstraction, HTML parsing
  AuditVitals.Infrastructure/  EF Core, Npgsql, the concrete safe HTTP fetcher, DI wiring
  AuditVitals.Api/             ASP.NET Core minimal API, Swagger, health checks, rate limiting
  AuditVitals.Worker/          BackgroundService that claims and processes queued audit jobs
  auditvitals-web/             Angular 22 standalone-components frontend
tests/
  AuditVitals.Domain.Tests/
  AuditVitals.Application.Tests/
  AuditVitals.Infrastructure.Tests/
  AuditVitals.Api.IntegrationTests/
```

Dependency direction: `Api`/`Worker` → `Infrastructure` → `Application` → `Domain`. `Application` never references
`Infrastructure` — it defines interfaces (`IAuditSubmissionUnitOfWork`, `IAuditProcessingUnitOfWork`,
`ISafeHttpFetcher`, `IDnsResolver`) that `Infrastructure` implements.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22.22.3+](https://nodejs.org/) (or 24.15+ / 26+) and npm
- [Docker](https://www.docker.com/) and Docker Compose, **or** a local PostgreSQL 16 server
- The [`dotnet-ef`](https://learn.microsoft.com/ef/core/cli/dotnet) global tool: `dotnet tool install --global dotnet-ef`

## Environment setup

Copy the example environment file:

```bash
cp .env.example .env
```

The defaults in `.env.example` already match the connection string committed in
`src/AuditVitals.Api/appsettings.Development.json` and `src/AuditVitals.Worker/appsettings.Development.json`. That
password is a throwaway local-only value — it only ever protects a Postgres container bound to `localhost`. It is
**not** used anywhere outside local development. Production connection strings must be supplied via environment
variables (e.g. `ConnectionStrings__AuditVitals`) or a secrets manager, never committed to source control.

## Start PostgreSQL

```bash
docker compose up -d
```

This starts PostgreSQL 16 on `localhost:5432` with a named volume for persistence. If you'd rather run PostgreSQL
natively instead of via Docker, create a database and role matching your `.env` values:

```sql
CREATE ROLE auditvitals WITH LOGIN PASSWORD 'auditvitals_dev_password';
CREATE DATABASE auditvitals OWNER auditvitals;
```

## Apply database migrations

```bash
dotnet ef database update \
  --project src/AuditVitals.Infrastructure \
  --startup-project src/AuditVitals.Api
```

To add a new migration after changing entities or configurations:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/AuditVitals.Infrastructure \
  --startup-project src/AuditVitals.Api \
  --output-dir Persistence/Migrations
```

## Run the backend API

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project src/AuditVitals.Api
```

- API: `http://localhost:5080` (or whatever `--urls` / launch profile you configure)
- Swagger UI: `http://localhost:5080/swagger`
- Liveness: `GET /health/live` · Readiness (checks PostgreSQL): `GET /health/ready`

Try it:

```bash
curl -X POST http://localhost:5080/api/audits \
  -H "Content-Type: application/json" \
  -d '{"url":"https://example.com"}'
# → 202 Accepted, { "auditId": "...", "statusUrl": "/api/audits/..." }

curl http://localhost:5080/api/audits/<auditId>
```

## Run the background worker

In a second terminal:

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project src/AuditVitals.Worker
```

The worker polls the database-backed job queue, atomically claims one queued audit at a time (see
[Security model](#security-model)), fetches the homepage through the SSRF-safe HTTP client, records basic page
data (title, meta description, word count, indexability), and marks the audit `Completed` or `Failed`.

## Run the Angular frontend

```bash
cd src/auditvitals-web
npm install
npm start   # ng serve, proxies /api/* to http://localhost:5080 (see proxy.conf.json)
```

Open `http://localhost:4200`. The free-audit page submits a URL, then polls `GET /api/audits/{id}` every 2 seconds
until the audit reaches a terminal status.

## Run the tests

Backend (from the repo root):

```bash
dotnet test
```

`AuditVitals.Api.IntegrationTests` and part of `AuditVitals.Infrastructure.Tests` run against a real local
PostgreSQL database named `auditvitals_test` (create it once: `CREATE DATABASE auditvitals_test OWNER auditvitals;`)
— they exercise the actual SQL used for atomic job-claiming (`FOR UPDATE SKIP LOCKED`), which can't be meaningfully
verified against a mock. Everything else (the SSRF validator, the safe HTTP fetcher, domain entities, the audit
processing service) runs with no network or database dependency, using injected fakes.

Frontend:

```bash
cd src/auditvitals-web
npm run build   # production build
npm test        # Vitest, headless — no browser install required
```

## Project structure

See [Architecture](#architecture) above. Two design decisions worth calling out:

- **No repository-per-entity pattern.** `IAuditSubmissionUnitOfWork` and `IAuditProcessingUnitOfWork` expose exactly
  the operations each use case needs, not a generic CRUD abstraction over every entity. A single `SaveChangesAsync`
  call per use case is what makes "create a project, audit, and job together" transactional — EF Core's `DbContext`
  already behaves as a unit of work.
- **Domain entities are private-setter classes with factory methods and behavior methods**
  (`Audit.CreateQueued(...)`, `audit.MarkCompleted(...)`), not anemic DTOs — invalid state transitions
  (e.g. completing an audit that was never started) throw, and that's covered by unit tests.

## Security model

The API fetches user-supplied URLs, so SSRF defense is load-bearing, not incidental:

- **`SsrfSafeUrlValidator`** (`AuditVitals.Application.Security`) rejects non-HTTP(S) schemes, embedded credentials,
  missing hosts, and a fixed deny-list of hostnames (`localhost`, `metadata.google.internal`, …) before ever
  touching the network. It then resolves the hostname via an injected `IDnsResolver` and rejects the request if
  **any** resolved address falls in a prohibited range: loopback, RFC 1918 private ranges, link-local/cloud-metadata
  (`169.254.0.0/16`, which covers `169.254.169.254`), carrier-grade NAT (`100.64.0.0/10`), multicast, reserved, the
  IPv6 equivalents, and IPv4-mapped IPv6 addresses used to smuggle a blocked IPv4 target (`::ffff:127.0.0.1`).
- **`SafeHttpFetcher`** (`AuditVitals.Infrastructure.Crawling`) never uses `HttpClient`'s built-in redirect
  following. It follows redirects manually, re-running the full validator (including a fresh DNS resolution) on
  every hop, up to a configurable maximum. It enforces a request timeout, a maximum response size (read via a
  bounded stream, not `ReadAsStringAsync`), and an allowed-content-type list, and it only ever sends a fixed
  User-Agent and `Accept` header — no header from one host is ever carried to another.
- **DNS-rebinding defense in depth:** when no forward proxy is configured for the deployment, the underlying
  `SocketsHttpHandler.ConnectCallback` re-resolves and re-validates the target host at the moment of the actual TCP
  connect — closing the gap between "we validated this URL" and "we connected to it" (a host could otherwise change
  its DNS answer in between). When an egress proxy *is* configured (`HTTPS_PROXY`), that pinning is skipped because
  the handler's first hop is the proxy itself, not the origin — in that topology, the proxy is the trusted network
  boundary, and the pre-flight and per-redirect validation described above still apply on every request.
- **Job claiming** uses a single atomic `UPDATE ... WHERE id = (SELECT ... FOR UPDATE SKIP LOCKED) RETURNING id`
  against PostgreSQL, so two worker processes can never claim the same `AuditJob` row — verified under real
  concurrent load in `EfAuditProcessingUnitOfWorkTests`, not just asserted.
- **Rate limiting:** `POST /api/audits` is limited per client IP (`RateLimiting:AuditSubmission` in configuration;
  5 requests/minute by default) since it is unauthenticated and triggers a real outbound fetch.

## Roadmap

Milestone 1 deliberately stops short of a lot of what's described in the product vision. Not yet built:

- Multi-page crawling, `robots.txt`/sitemap discovery, page-limit enforcement beyond the homepage
- The technical SEO rules engine and the transparent 0–100 health score
- Authentication, user accounts, and per-user project ownership (`Project.UserId` is nullable today — every
  submission is anonymous)
- PDF reports (QuestPDF), billing/Stripe, and the pricing-tier limits described in the product spec
- Most of the planned public and authenticated pages (only the free-audit page exists)

Do not start on any of this without explicit sign-off on the next milestone's scope.
