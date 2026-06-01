# Agent & Contributor Guidelines

These guidelines apply to all contributors and AI agents working in this
repository. They are intentionally short; the goal is consistent, test-first,
incremental delivery of the system described in [`ARCHITECTURE.md`](./ARCHITECTURE.md).

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
npx -y aws-cdk diff --template cdk.out/CdkBaseStack.template.json
```

## Pull Request Expectations

- **Minimal, focused changes** — one logical slice per PR, matching its issue.
- **Tests included** — every behavior change is covered by a test.
- **Security & least privilege** — follow the security section of
  `ARCHITECTURE.md` (private buckets, encryption at rest, narrowly scoped IAM).
- **Multi-environment aware** — respect CDK context (`dev`/`stage`/`prod`).
- **Docs stay current** — update `ARCHITECTURE.md` and other docs when behavior
  or design changes.
