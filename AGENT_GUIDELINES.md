# Agent & Contributor Guidelines

These guidelines apply to all contributors and AI agents working in this
repository. They are intentionally short; the goal is consistent, test-first,
incremental delivery of the system described in [`ARCHITECTURE.md`](./ARCHITECTURE.md).

**For detailed patterns and meta-prompts**, see [`META-PROMPTS.md`](./META-PROMPTS.md) — a comprehensive guide to reusable patterns extracted from this project.

## Source of Truth

[`ARCHITECTURE.md`](./ARCHITECTURE.md) is the **single source of truth** for the
design of the Event-Driven Sleep Audio Pipeline. Before starting any issue:

1. Read `ARCHITECTURE.md` and confirm the work fits the documented design.
2. If the implementation must diverge from the design, **update
   `ARCHITECTURE.md` in the same pull request** and explain why. Code and the
   architecture document must never drift apart.
3. Keep the prose description and the Mermaid diagram consistent with each other.

## Test-Driven Development (TDD)

Implementation is strictly test-first and delivered one thin slice per issue:

1. **Red** — write a failing test that describes the desired behavior
   (for example, asserting that a resource exists in the synthesized template).
2. **Green** — write the minimum CDK/code to make the test pass.
3. **Refactor** — clean up while keeping tests green.

Add CDK resources only when a test requires them. The starter test in
`src/CdkBase.Tests/CdkBaseStackTests.cs` asserts the stack begins with **zero**
S3 buckets; update such tests deliberately as the design is implemented.

## Build, Test & Validate Locally

Run the same checks CI runs before opening or updating a pull request:

```bash
dotnet restore src/CdkBase.sln
dotnet test src/CdkBase.sln --no-restore
npx -y aws-cdk synth
```

## Pull Request Expectations

- **Minimal, focused changes** — one logical slice per PR, matching its issue.
- **Tests included** — every behavior change is covered by a test.
- **Security & least privilege** — follow the security section of
  `ARCHITECTURE.md` (private buckets, encryption at rest, narrowly scoped IAM).
- **Multi-environment aware** — respect CDK context (`dev`/`stage`/`prod`).
- **Docs stay current** — update `ARCHITECTURE.md` and other docs when behavior
  or design changes.

## Additional Resources

- [`META-PROMPTS.md`](./META-PROMPTS.md) — Detailed patterns, meta-prompts, and reusable templates for TDD IaC
- [`ARCHITECTURE.md`](./ARCHITECTURE.md) — System design and implementation status
- [`SUMMARY.md`](./SUMMARY.md) — Project timeline, decisions, and lessons learned

## Quick Reference

**Build & Test Commands:**
```bash
dotnet restore src/CdkBase.sln
dotnet test src/CdkBase.sln --no-restore
npx -y aws-cdk synth
```

**TDD Cycle:**
1. 🔴 Red — Write failing test
2. 🟢 Green — Minimum code to pass
3. 🔵 Refactor — Clean up

**Documentation Updates:**
- Code change → Update `ARCHITECTURE.md`
- New pattern → Consider adding to `META-PROMPTS.md`
- Design decision → Document in `SUMMARY.md`
