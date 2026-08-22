# Blackhatbadshah Discovery Guide

Blackhatbadshah is an engineering lab, not a content farm.

The fastest path to legitimate visibility is to make difficult technical work easy to understand, reproduce, cite, and share.

## What should become public

Prioritize artifacts that teach something:

1. Reproducible kernel failures
2. Memory-management investigations
3. ELF and user-mode execution findings
4. Linux syscall compatibility work
5. Scheduler and concurrency failures
6. Observability and diagnostics design
7. AI-assisted engineering workflows
8. .NET performance and infrastructure findings

## The publication pattern

For every substantial engineering problem:

```text
Failure
  -> Evidence
  -> Minimal reproduction
  -> Root cause
  -> Fix
  -> Regression test
  -> Short technical explanation
  -> Permanent repository artifact
```

The repository is the source of truth. External posts should point back to the artifact rather than replace it.

## Search-friendly titles

Prefer precise technical titles such as:

- `Why an isolated CR3 caused a page fault in SAIOS`
- `Tracing an ELF entry-point failure from ring 3 to the kernel`
- `Implementing Linux-compatible syscalls without breaking SAIOS contracts`
- `What a scheduler freeze actually looked like at the kernel boundary`

Avoid vague titles such as `Big Update`, `AI Changed Everything`, or `My Coding Journey`.

## Evidence standard

Every claim should identify one or more of:

- commit
- test
- log excerpt
- benchmark
- reproduction command
- architecture decision
- before/after behavior

Do not publish fabricated metrics, endorsements, stars, followers, or community reactions.

## Community behavior

When discussing related work elsewhere:

- contribute useful technical information first
- link to Blackhatbadshah only when it directly answers the discussion
- never mass-post identical promotional messages
- never manufacture engagement
- credit upstream projects and prior work

## The goal

Make **Blackhatbadshah** recognizable because engineers repeatedly encounter useful work carrying the same name.

> Build things worth finding. Document them well enough that other engineers can verify them.
