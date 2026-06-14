# Meta-Prompts & Reusable Patterns for Agentic TDD IaC

This document extracts reusable patterns, meta-prompts, and agent guidelines from the Event-Driven Sleep Audio Pipeline project. These patterns can be applied to future agentic TDD Infrastructure as Code (IaC) projects.

> **🧪 Experimental Context**: This document was extracted from an agentic TDD IaC experiment. See [`EXPERIMENT.md`](EXPERIMENT.md) for complete experimental design and research methodology.

## 🎯 Project Philosophy

### Pure Issue-Driven Development
**Pattern**: All work is driven by GitHub Issues with clear, self-contained specifications.

**Meta-Prompt**:
```
Each issue must contain:
1. Clear goal statement
2. Specific requirements (numbered list)
3. Acceptance criteria
4. Tasks in strict order
5. Success criteria
6. Reference to next issue (creating a chain)

Example structure:
- Goal: [What we're building]
- Requirements: [Specific, testable requirements]
- Tasks: [Step-by-step execution order]
- Success: [How to verify completion]
- Next: [Link to follow-up issue]
```

### Strict TDD Discipline
**Pattern**: Red-Green-Refactor cycle enforced at every level.

**Meta-Prompt**:
```
Test-Driven Development workflow:
1. RED: Write a failing test that describes desired behavior
   - Test should assert the resource/behavior exists
   - Run test to confirm it fails for the right reason
   
2. GREEN: Write minimum code to make test pass
   - Add only what's needed for this test
   - No premature optimization
   - Verify test passes
   
3. REFACTOR: Clean up while keeping tests green
   - Improve code structure
   - Remove duplication
   - Keep all tests passing
   
Rule: Add CDK resources ONLY when a test requires them.
```

### Architecture as Single Source of Truth
**Pattern**: Maintain a living architecture document that stays in sync with code.

**Meta-Prompt**:
```
Architecture Document Rules:
1. Before starting any issue, read ARCHITECTURE.md
2. Confirm the work fits the documented design
3. If implementation must diverge:
   - Update ARCHITECTURE.md in the same PR
   - Explain why the divergence is necessary
   - Keep prose and diagrams consistent
4. Code and architecture must NEVER drift apart
5. Update status sections as features complete

Document structure:
- Implementation status (what's done, what's next)
- Component descriptions (what each resource does)
- Data flow diagrams (visual representation)
- Security controls (compliance requirements)
- Design decisions (rationale for choices)
```

## 🔨 Development Patterns

### Minimal, Focused Changes
**Pattern**: One logical slice per PR, matching its issue.

**Meta-Prompt**:
```
Pull Request Scope:
- Address ONE issue per PR
- Make minimal changes to achieve the goal
- Don't fix unrelated issues
- Don't add "nice to have" features
- Include only the tests needed for this slice
- Update only the docs that relate to this change

This keeps PRs:
- Easy to review
- Easy to revert
- Easy to understand
- Low risk
```

### Build-Test-Synth Validation
**Pattern**: Run complete validation pipeline before committing.

**Meta-Prompt**:
```
Local Validation Checklist:
Run these commands before opening/updating a PR:

1. Restore dependencies:
   dotnet restore src/[Solution].sln

2. Run all tests:
   dotnet test src/[Solution].sln --no-restore

3. Synthesize CloudFormation:
   npx -y aws-cdk synth

4. Review diff (if deployed):
   npx aws-cdk diff

All must succeed before committing.
Mirrors CI pipeline for fast feedback.
```

## 🧪 Testing Patterns

### CDK Testing with Assertions
**Pattern**: Use CDK Assertions library for infrastructure testing.

**Meta-Prompt**:
```
CDK Testing Approach:

1. Template Capture:
   var template = Template.FromStack(stack);

2. Resource Count Assertions:
   template.ResourceCountIs("AWS::S3::Bucket", 2);

3. Property Assertions:
   template.HasResourceProperties("AWS::S3::Bucket", new Dictionary<string, object> {
       ["BucketEncryption"] = Match.ObjectLike(new Dictionary<string, object> {
           ["ServerSideEncryptionConfiguration"] = Match.AnyValue()
       })
   });

4. Token Handling (for dynamic values):
   var capture = new Capture();
   template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object> {
       ["DefinitionString"] = capture
   });
   var definition = JsonSerializer.Deserialize<JsonElement>(capture.AsObject()["Fn::Join"][1][1].GetString());

5. IAM Permission Assertions:
   template.HasResourceProperties("AWS::IAM::Policy", new Dictionary<string, object> {
       ["PolicyDocument"] = Match.ObjectLike(new Dictionary<string, object> {
           ["Statement"] = Match.ArrayWith(new[] {
               Match.ObjectLike(new Dictionary<string, object> {
                   ["Action"] = "s3:PutObject",
                   ["Effect"] = "Allow"
               })
           })
       })
   });
```

### Test Organization
**Pattern**: Organize tests by concern, from infrastructure to end-to-end.

**Meta-Prompt**:
```
Test Suite Structure:

1. Resource Existence Tests
   - Verify resources are created
   - Check resource counts
   - Validate resource types

2. Property Tests
   - Encryption settings
   - Access controls
   - Configuration values

3. Integration Tests
   - IAM permissions
   - Resource connections
   - Event wiring

4. Flow Tests
   - State machine workflows
   - Error paths
   - Happy paths

5. Security Tests
   - Encryption at rest
   - Encryption in transit
   - Least privilege

6. End-to-End Tests
   - Complete pipeline flows
   - Data transformations
   - Error handling
   - Observability

Naming: [Component]_[Behavior]
Example: "S3Bucket_HasEncryptionEnabled"
```

## 🔐 Security Patterns

### Least Privilege IAM
**Pattern**: Grant minimum required permissions to each component.

**Meta-Prompt**:
```
IAM Policy Best Practices:

1. No Wildcards in Resources:
   ❌ "Resource": "*"
   ✅ "Resource": "arn:aws:s3:::specific-bucket-name/*"

2. Specific Actions:
   ❌ "Action": "s3:*"
   ✅ "Action": ["s3:PutObject", "s3:GetObject"]

3. Condition Keys (when applicable):
   "Condition": {
       "StringEquals": {
           "s3:x-amz-server-side-encryption": "AES256"
       }
   }

4. Test Permissions:
   - Write tests that verify IAM policies
   - Assert each permission is present
   - Check resource ARNs are specific
```

### Defense in Depth
**Pattern**: Multiple layers of security controls.

**Meta-Prompt**:
```
Security Layers Checklist:

1. Encryption at Rest:
   - S3: SSE-S3 or SSE-KMS
   - DynamoDB: AWS_MANAGED or CUSTOMER_MANAGED
   - SNS: KMS encryption with key rotation

2. Encryption in Transit:
   - Enforce TLS on S3 buckets
   - Use HTTPS endpoints
   - VPC endpoints for private traffic

3. Access Control:
   - Private S3 buckets (block public access)
   - IAM roles (no long-term credentials)
   - Resource policies (restrict access)

4. Monitoring:
   - CloudWatch Logs (audit trail)
   - X-Ray tracing (request tracking)
   - CloudWatch Alarms (anomaly detection)

5. Key Management:
   - KMS key rotation enabled
   - Separate keys per service
   - Key policies with least privilege
```

## 📊 Observability Patterns

### Structured Logging
**Pattern**: Comprehensive logging for debugging and monitoring.

**Meta-Prompt**:
```
Logging Configuration:

1. Step Functions:
   - Level: ALL (includes execution data)
   - Include execution data: true
   - CloudWatch Log Group with retention

2. Lambda:
   - Structured JSON logging
   - Include request ID, timestamp
   - Log entry/exit of functions
   - Log decision points

3. Log Retention:
   - Development: 7 days
   - Production: 30-90 days
   - Compliance: as required

4. What to Log:
   - Input parameters (sanitize sensitive data)
   - Decision points (why did we branch?)
   - Error conditions (full context)
   - Success confirmations (what completed?)
```

### Distributed Tracing
**Pattern**: Enable X-Ray for request tracking across services.

**Meta-Prompt**:
```
X-Ray Configuration:

1. Enable on State Machine:
   TracingConfiguration = new TracingConfiguration {
       Enabled = true
   }

2. Enable on Lambda:
   Tracing = Tracing.ACTIVE

3. Benefits:
   - See complete request flow
   - Identify bottlenecks
   - Debug distributed failures
   - Measure latency

4. Testing:
   - Assert tracing is enabled
   - Verify in synthesized template
```

### CloudWatch Alarms
**Pattern**: Proactive failure detection.

**Meta-Prompt**:
```
Alarm Strategy:

1. What to Alarm On:
   - Execution failures (Step Functions)
   - Error rate thresholds (Lambda)
   - Duration anomalies (Lambda timeout warnings)
   - Resource limits (DynamoDB throttling)

2. Alarm Configuration:
   - Metric: Specific to failure type
   - Threshold: Based on SLA requirements
   - Evaluation periods: Balance sensitivity vs. noise
   - Actions: SNS notifications, auto-remediation

3. Example:
   new Alarm(stack, "ExecutionFailedAlarm", new AlarmProps {
       Metric = stateMachine.MetricFailed(),
       Threshold = 1,
       EvaluationPeriods = 1,
       AlarmDescription = "Alert when state machine execution fails"
   });
```

## 🏗️ Infrastructure Patterns

### Multi-Environment Support
**Pattern**: Use CDK context for environment-specific configuration.

**Meta-Prompt**:
```
Environment Configuration:

1. CDK Context (cdk.json):
   {
     "dev": { "maxFileSize": 10485760 },
     "prod": { "maxFileSize": 104857600 }
   }

2. Stack Parameter:
   public CdkBaseStack(Construct scope, string id, IStackProps props, string environmentName = "dev")
   {
       var context = this.Node.TryGetContext(environmentName) as Dictionary<string, object>;
       // Use context for configuration
   }

3. Deployment:
   npx aws-cdk deploy -c environment=prod

4. Testing:
   Test each environment configuration
   Verify defaults are sensible
```

### Event-Driven Architecture
**Pattern**: Decouple services with events.

**Meta-Prompt**:
```
Event-Driven Design:

1. Use EventBridge over Direct Integration:
   - Decouples producers from consumers
   - Enables multiple subscribers
   - Better observability
   - Easier testing

2. Event Pattern:
   new Rule(stack, "InputRule", new RuleProps {
       EventPattern = new EventPattern {
           Source = new[] { "aws.s3" },
           DetailType = new[] { "Object Created" },
           Detail = new Dictionary<string, object> {
               ["bucket"] = new Dictionary<string, object> {
                   ["name"] = new[] { inputBucket.BucketName }
               }
           }
       }
   });

3. Benefits:
   - Loose coupling
   - Scalability
   - Reliability
   - Extensibility
```

### Orchestration with Step Functions
**Pattern**: Use Step Functions for complex workflows.

**Meta-Prompt**:
```
State Machine Design:

1. When to Use Step Functions:
   - Multiple steps with dependencies
   - Error handling and retries
   - Human approval workflows
   - Long-running processes (up to 1 year)

2. State Types:
   - Task: Do work (Lambda, SDK call)
   - Choice: Conditional branching
   - Parallel: Concurrent execution
   - Wait: Delay
   - Pass: Transform data
   - Succeed/Fail: Terminal states

3. Error Handling:
   - Catch blocks on every task
   - Route errors to cleanup states
   - Update status in database
   - Send notifications

4. Retry Policies:
   - Exponential backoff
   - Max attempts (3-5)
   - Specific error types
   - Test retry behavior
```

## 📝 Documentation Patterns

### README Structure
**Pattern**: Comprehensive, scannable README.

**Meta-Prompt**:
```
README.md Template:

1. Project Title & Description (2-3 sentences)
2. Overview (bullet points or diagram)
3. Architecture (link to ARCHITECTURE.md)
4. Getting Started
   - Prerequisites
   - Installation
   - Development workflow
   - Deployment
5. Documentation Links
6. Testing (how to run, what coverage)
7. Security (key controls)
8. Observability (logs, metrics, alarms)
9. Useful Commands (reference)
10. Multi-Environment Support
11. Contributing (link to guidelines)
12. License
13. Project Status (what's complete, what's next)

Use emoji for visual hierarchy: 📋 🏗️ 🚀 📚 🧪 🔐 📊 🛠️ 🏷️ 🤝 📝 🎯
```

### Agent Guidelines Document
**Pattern**: Short, actionable guidelines for contributors and agents.

**Meta-Prompt**:
```
AGENT_GUIDELINES.md Template:

1. Source of Truth
   - Link to ARCHITECTURE.md
   - How to keep code and docs in sync

2. Development Process (TDD)
   - Red-Green-Refactor cycle
   - When to add resources

3. Build, Test & Validate
   - Commands to run
   - What must pass

4. Pull Request Expectations
   - Scope
   - Testing
   - Security
   - Documentation

Keep it SHORT (1-2 pages max).
Make it ACTIONABLE (specific commands, clear rules).
```

### Architecture Documentation
**Pattern**: Living document with status, design, and diagrams.

**Meta-Prompt**:
```
ARCHITECTURE.md Template:

1. Status Section (top)
   - What's complete
   - What's in progress
   - What's next

2. Implementation Status (detailed)
   - Completed features (by issue)
   - Resource details
   - Configuration notes

3. System Overview
   - High-level description
   - Key components
   - Data flow

4. Component Details
   - Each major service/resource
   - Purpose, configuration, connections

5. Data Flow Diagram (Mermaid)
   - Visual representation
   - Keep in sync with prose

6. Security Controls
   - Encryption at rest/in transit
   - Access controls
   - Monitoring

7. Design Decisions
   - Key choices
   - Rationale
   - Alternatives considered

Update this document in EVERY PR that changes behavior or structure.
```

## 🚀 Deployment Patterns

### CDK Deployment Workflow
**Pattern**: Safe, repeatable deployment process.

**Meta-Prompt**:
```
Deployment Checklist:

1. Local Validation:
   - dotnet test (all tests pass)
   - npx aws-cdk synth (synthesizes successfully)
   - npx aws-cdk diff (review changes)

2. First Time Setup:
   - npx aws-cdk bootstrap (bootstrap CDK in account/region)

3. Deployment:
   - npx aws-cdk deploy (deploys stack)
   - Review changeset
   - Confirm deployment

4. Verification:
   - Check AWS Console (resources created)
   - Run smoke tests (upload file, check processing)
   - Monitor CloudWatch (logs, metrics)

5. Rollback (if needed):
   - npx aws-cdk deploy --rollback (rollback changes)
   - Or redeploy previous version

6. Cleanup:
   - npx aws-cdk destroy (remove all resources)
   - Verify in console (some resources may be retained)
```

## 🎓 Lessons Learned

### What Works Well

**Meta-Prompt for Future Projects**:
```
Practices to Adopt:

1. TDD from Day 1
   - Write test before code
   - Build confidence early
   - Enable fearless refactoring
   - Document expected behavior

2. Incremental Development
   - Small, focused issues
   - One concern per PR
   - Frequent commits
   - Fast feedback loops

3. Architecture Document
   - Single source of truth
   - Updated with every PR
   - Prevents drift
   - Onboards new contributors

4. Strong Typing (C# with CDK)
   - IntelliSense/autocomplete
   - Compile-time errors
   - Better refactoring
   - Clearer intent

5. Security by Default
   - Encryption at rest/transit
   - Least privilege IAM
   - Private by default
   - Test security controls
```

### Common Challenges & Solutions

**Meta-Prompt for Troubleshooting**:
```
Known Issues and Solutions:

1. Lambda Path Handling:
   Problem: Asset path not found in tests
   Solution: Use absolute paths consistent with CI environment
   Example: /tmp/workspace/[org]/[repo]/src/Lambda/bin/Release/net8.0/publish

2. State Machine Token Handling:
   Problem: CDK tokens in DefinitionString cause string assertion failures
   Solution: Use Capture.AsObject() and deserialize JSON
   Example:
     var capture = new Capture();
     template.HasResourceProperties("AWS::StepFunctions::StateMachine", 
       new Dictionary<string, object> { ["DefinitionString"] = capture });
     var definition = JsonSerializer.Deserialize<JsonElement>(
       capture.AsObject()["Fn::Join"][1][1].GetString());

3. IAM Permission Scoping:
   Problem: Over-permissive policies (wildcards)
   Solution: Use specific resource ARNs
   Test: Assert resource ARNs in policy

4. Multi-Service Integration:
   Problem: Components work individually but not together
   Solution: End-to-end integration tests
   Test: Full pipeline flow from S3 upload to SNS notification
```

## 📋 Issue Template

**Meta-Prompt for Creating New Issues**:
```markdown
Title: [Issue Number] [Category]: [Brief Description]

**Goal**
[One sentence describing what we're building/fixing]

**Context**
[Why this issue exists, what problem it solves]

**Strict Discipline (must follow):**
- [Any non-negotiable rules specific to this issue]
- [Example: "Must maintain backwards compatibility"]
- [Example: "Update ARCHITECTURE.md if design changes"]
- [Example: "Use conventional commits"]

**Specific Requirements:**

1. **[Category 1]**
   - [Specific, testable requirement]
   - [Another requirement]

2. **[Category 2]**
   - [Specific, testable requirement]

**Tasks (in strict order):**

1. [First task - usually research/read]
2. [Write failing tests]
3. [Implement minimum code]
4. [Refactor]
5. [Update documentation]
6. [Validate (build, test, synth)]

**Success Criteria**
- [Specific, measurable criteria]
- [Example: "All tests pass"]
- [Example: "CDK synth succeeds"]
- [Example: "Documentation updated"]

**Next Issue** (when complete):
"[Next Issue Number] [Next Category]: [Next Description]"
```

## 🔄 Continuous Integration

**Meta-Prompt for CI Pipeline**:
```yaml
# .github/workflows/ci.yml
name: ci

on:
  push:
    branches: [main]
  pull_request:

permissions:
  contents: read

jobs:
  build-test-synth:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 20

      - name: Restore
        run: dotnet restore src/[Solution].sln

      - name: Test
        run: dotnet test src/[Solution].sln --no-restore

      - name: CDK Synth
        run: npx -y aws-cdk synth

# Keep it simple: restore, test, synth
# Same commands developers run locally
# Fast feedback on every push/PR
```

## 🎯 Success Metrics

**Meta-Prompt for Measuring Success**:
```
Project Health Indicators:

1. Test Coverage:
   - Target: 100% of infrastructure resources tested
   - Measure: Resource count in template vs. test count
   - Trend: Should increase with each issue

2. Build Success Rate:
   - Target: 100% of PRs pass CI
   - Measure: GitHub Actions success rate
   - Trend: Should stay at 100%

3. Documentation Freshness:
   - Target: ARCHITECTURE.md updated with every structural change
   - Measure: PR description mentions doc updates
   - Trend: Every PR with code includes doc update

4. Issue Velocity:
   - Target: 1 issue per session (small, focused)
   - Measure: Time from issue creation to closure
   - Trend: Should decrease as patterns emerge

5. Deployment Readiness:
   - Target: Every commit is deployable
   - Measure: CDK synth success rate
   - Trend: Should stay at 100%

6. Security Compliance:
   - Target: Zero security violations
   - Measure: Security test failures
   - Trend: Should stay at zero
```

## 📚 Further Reading

### CDK Best Practices
- [AWS CDK Best Practices](https://docs.aws.amazon.com/cdk/v2/guide/best-practices.html)
- [CDK Patterns](https://cdkpatterns.com/)
- [AWS Well-Architected Framework](https://aws.amazon.com/architecture/well-architected/)

### Testing Infrastructure
- [Testing AWS CDK Applications](https://docs.aws.amazon.com/cdk/v2/guide/testing.html)
- [TDD for Infrastructure](https://www.hashicorp.com/resources/test-driven-development-tdd-for-infrastructure)

### Security
- [AWS Security Best Practices](https://aws.amazon.com/security/best-practices/)
- [Least Privilege IAM](https://docs.aws.amazon.com/IAM/latest/UserGuide/best-practices.html#grant-least-privilege)

### Agent-Driven Development
- [GitHub Copilot Documentation](https://docs.github.com/en/copilot)
- [AI-Assisted Development Patterns](https://github.blog/developer-skills/github/how-to-use-github-copilot-in-your-ide-tips-tricks-and-best-practices/)

---

## 💡 Using These Patterns

### For Human Contributors
1. Read AGENT_GUIDELINES.md first
2. Review relevant patterns from this document
3. Follow the TDD cycle strictly
4. Update documentation with code changes
5. Run validation before committing

### For AI Agents
1. Parse issue for goal, requirements, tasks, success criteria
2. Apply relevant meta-prompts from this document
3. Execute TDD cycle (Red-Green-Refactor)
4. Test at each step
5. Update architecture documentation
6. Validate before completing

### For Future Projects
1. Copy this document as a template
2. Customize patterns for your tech stack
3. Add project-specific patterns as you discover them
4. Keep this document in sync with actual practices
5. Treat as a living, evolving guide

---

*This document was extracted from the Event-Driven Sleep Audio Pipeline project, built entirely with TDD and GitHub Copilot.*
