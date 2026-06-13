# Event-Driven Sleep Audio Pipeline

[![CI](https://github.com/obstreperous-ai/cdk-sleep-csharp-copilot/actions/workflows/ci.yml/badge.svg)](https://github.com/obstreperous-ai/cdk-sleep-csharp-copilot/actions/workflows/ci.yml)
[![AWS CDK](https://img.shields.io/badge/AWS%20CDK-2.x-orange.svg)](https://aws.amazon.com/cdk/)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub Copilot](https://img.shields.io/badge/Built%20with-GitHub%20Copilot-blue.svg)](https://github.com/features/copilot)

A fully serverless, event-driven AWS CDK application that processes audio files into soothing sleep audio using AWS managed services.

**🧪 Experiment Notice**: This project is a **TDD IaC experiment** built entirely through pure issue-driven development with GitHub Copilot. Every feature was delivered through strict Test-Driven Development, demonstrating agentic infrastructure development patterns.

## 📑 Table of Contents

- [Overview](#-overview)
- [Architecture](#️-architecture)
- [Experiment Methodology](#-experiment-methodology)
- [Getting Started](#-getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
  - [Development Workflow](#development-workflow)
  - [Deployment](#deployment)
  - [Testing the Pipeline](#testing-the-pipeline)
- [Documentation](#-documentation)
- [Testing](#-testing)
- [Security](#-security)
- [Observability](#-observability)
- [Useful Commands](#️-useful-commands)
- [Multi-Environment Support](#️-multi-environment-support)
- [Contributing](#-contributing)
- [License](#-license)
- [Project Status](#-project-status)

## 📋 Overview

This project implements a complete event-driven pipeline for processing audio files:

1. **Upload** — User uploads audio file to S3 input bucket
2. **Detect** — EventBridge detects the upload and triggers processing
3. **Process** — Step Functions orchestrates the workflow:
   - Validates input
   - Processes audio with Amazon Polly (neural TTS)
   - Stores metadata in DynamoDB
   - Saves output to S3
4. **Notify** — SNS publishes success/failure notifications
5. **Monitor** — CloudWatch provides logs, metrics, and alarms

The system is designed for zero-idle cost, automatic scaling, and production-grade security and observability.

## 🏗️ Architecture

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for complete architecture documentation, including:
- Detailed component descriptions
- Data flow diagrams (Mermaid)
- Security controls
- Implementation status

**Key Components:**
- **S3 Buckets**: Input/output storage with encryption and versioning
- **EventBridge**: Event-driven trigger for S3 uploads
- **Step Functions**: Orchestrates the complete workflow (10+ states)
- **Lambda**: .NET 8 audio processor with Polly integration
- **DynamoDB**: Metadata storage with on-demand capacity
- **SNS**: Success/failure notifications
- **CloudWatch**: Logs, metrics, and alarms
- **X-Ray**: Distributed tracing

## 🧪 Experiment Methodology

This project serves as a **proof-of-concept for agentic TDD Infrastructure as Code** development. Key experimental aspects:

### Pure Issue-Driven Development
- **Every feature** implemented through GitHub Issues (#2-#13)
- Each issue self-contained with clear goals, requirements, tasks, and success criteria
- Issues form a chain, with each linking to the next
- Zero ad-hoc development outside the issue workflow

### Strict Test-Driven Development
- **Red-Green-Refactor** cycle enforced for every change
- Resources added **only when tests require them**
- 98 comprehensive tests covering all infrastructure
- Tests written **before** implementation code
- Zero untested code paths

### Architecture as Single Source of Truth
- [`ARCHITECTURE.md`](ARCHITECTURE.md) maintained as living document
- Updated with **every PR** that changes structure or behavior
- Code and documentation **never drift apart**
- Diagrams stay in sync with prose and implementation

### Continuous Validation
- Local validation before every commit:
  - `dotnet test` - All tests pass
  - `npx aws-cdk synth` - Template synthesizes
  - `npx aws-cdk diff` - Review changes
- CI runs same checks on every push
- Zero tolerance for broken builds

### Reusable Patterns
All patterns, meta-prompts, and agent guidelines extracted into:
- [`META-PROMPTS.md`](META-PROMPTS.md) — Reusable patterns for future agentic TDD IaC projects
- [`AGENT_GUIDELINES.md`](AGENT_GUIDELINES.md) — Contributor and agent guidelines

### Outcomes
- ✅ **100% test coverage** of infrastructure resources
- ✅ **Zero untested deployments** (all changes validated before deploy)
- ✅ **Production-ready** security, error handling, and monitoring
- ✅ **Complete documentation** (architecture, guidelines, patterns)
- ✅ **Reusable templates** for future agentic infrastructure projects

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) (for AWS CDK)
- [AWS CLI](https://aws.amazon.com/cli/) configured with credentials
- AWS Account with appropriate permissions

### Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd cdk-sleep-csharp-copilot
```

2. Restore .NET dependencies:
```bash
dotnet restore src/CdkBase.sln
```

3. Install CDK CLI (if not already installed):
```bash
npm install -g aws-cdk
```

### Development Workflow

#### 1. Build the Solution
```bash
dotnet build src/CdkBase.sln
```

#### 2. Run Tests
```bash
dotnet test src/CdkBase.sln --no-restore
```

All 98 tests should pass, including:
- Infrastructure as Code (IaC) tests
- State machine flow tests
- Security and compliance tests
- End-to-end integration tests

#### 3. Publish Lambda Function
Before synthesizing or deploying, publish the Lambda function:
```bash
dotnet publish src/SleepAudioProcessor/SleepAudioProcessor.csproj \
  -c Release -r linux-x64 \
  -o /tmp/workspace/obstreperous-ai/cdk-sleep-csharp-copilot/src/SleepAudioProcessor/bin/Release/net8.0/publish
```

#### 4. Synthesize CloudFormation Template
```bash
npx aws-cdk synth
```

This generates CloudFormation templates in the `cdk.out/` directory.

#### 5. Compare Changes
```bash
npx aws-cdk diff
```

Review the changes before deploying.

### Deployment

#### Bootstrap CDK (first time only)
```bash
npx aws-cdk bootstrap
```

#### Deploy to AWS
```bash
npx aws-cdk deploy
```

The deployment creates:
- 2 S3 buckets (input/output, encrypted, versioned)
- DynamoDB table for metadata
- Lambda function for audio processing
- Step Functions state machine
- EventBridge rule
- SNS topics (success/failure)
- CloudWatch Alarms
- All necessary IAM roles and policies

### Testing the Pipeline

After deployment:

1. Upload an audio file to the input bucket:
```bash
aws s3 cp test-audio.mp3 s3://<input-bucket-name>/
```

2. Monitor execution in Step Functions Console:
```bash
aws stepfunctions list-executions \
  --state-machine-arn <state-machine-arn> \
  --max-results 10
```

3. Check DynamoDB for metadata:
```bash
aws dynamodb scan --table-name <table-name>
```

4. Verify output in output bucket:
```bash
aws s3 ls s3://<output-bucket-name>/processed/
```

## 📚 Documentation

* [`ARCHITECTURE.md`](ARCHITECTURE.md) — Complete system architecture and design decisions
* [`AGENT_GUIDELINES.md`](AGENT_GUIDELINES.md) — TDD guidelines and contribution workflow
* [`META-PROMPTS.md`](META-PROMPTS.md) — Reusable patterns and meta-prompts for agentic TDD IaC
* [`SUMMARY.md`](SUMMARY.md) — Project summary and key decisions

### For Contributors
Start with [`AGENT_GUIDELINES.md`](AGENT_GUIDELINES.md) to understand the TDD workflow and PR expectations.

### For AI Agents
Read [`META-PROMPTS.md`](META-PROMPTS.md) for reusable patterns, testing strategies, and meta-prompts applicable to similar projects.

### For Learning
This project demonstrates:
- Test-Driven Development for Infrastructure as Code
- Event-driven serverless architecture patterns
- Security best practices (encryption, least privilege, monitoring)
- GitHub Copilot-driven development workflow

## 🧪 Testing

The project follows strict Test-Driven Development (TDD):

- **98 tests** covering all aspects of the infrastructure
- CDK Assertions for infrastructure validation
- State machine flow testing
- Security and compliance testing
- End-to-end integration testing

Run tests with:
```bash
dotnet test src/CdkBase.sln --verbosity normal
```

## 🔐 Security

The pipeline implements multiple security layers:

- **Encryption at rest**: S3 (SSE-S3), DynamoDB (AWS_MANAGED), SNS (KMS)
- **Encryption in transit**: TLS enforced on all S3 operations
- **Access control**: Private buckets, least-privilege IAM roles
- **Monitoring**: CloudWatch Logs, X-Ray tracing, CloudWatch Alarms
- **Key rotation**: KMS keys have automatic rotation enabled

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for detailed security controls.

## 📊 Observability

- **Logs**: CloudWatch Logs for Step Functions and Lambda (structured JSON)
- **Tracing**: X-Ray for distributed tracing
- **Metrics**: CloudWatch metrics for all services
- **Alarms**: CloudWatch Alarms for execution failures

## 🛠️ Useful Commands

* `dotnet build src` — Compile the C# application
* `dotnet test src/CdkBase.sln` — Run all unit tests
* `npx aws-cdk synth` — Synthesize CloudFormation template
* `npx aws-cdk diff` — Compare deployed stack with current state
* `npx aws-cdk deploy` — Deploy stack to AWS account
* `npx aws-cdk destroy` — Remove all deployed resources

## 🏷️ Multi-Environment Support

The stack supports multiple environments via CDK context:

```bash
npx aws-cdk deploy -c environment=dev
npx aws-cdk deploy -c environment=stage
npx aws-cdk deploy -c environment=prod
```

Default environment is `dev` if not specified.

## 🤝 Contributing

See [`AGENT_GUIDELINES.md`](AGENT_GUIDELINES.md) for:
- TDD workflow (Red-Green-Refactor)
- Pull request expectations
- Architecture consistency requirements

## 📝 License

See [LICENSE](LICENSE) file for details.

## 🎯 Project Status

**✅ Complete** — All core functionality implemented and tested through Issues #2-#13:
- ✅ S3 buckets with EventBridge integration
- ✅ Step Functions state machine with full workflow
- ✅ DynamoDB metadata storage
- ✅ Lambda function for audio processing
- ✅ Amazon Polly integration (neural TTS)
- ✅ SNS notifications (success/failure)
- ✅ Input validation and error handling
- ✅ CloudWatch monitoring and alarms
- ✅ Comprehensive test coverage (98 tests)
- ✅ End-to-end validation
- ✅ Production-ready documentation
- ✅ Extracted reusable patterns (META-PROMPTS.md)

**Development Metrics:**
- 🧪 **98 tests** (100% passing)
- 📦 **40+ CloudFormation resources**
- 🔒 **100% encryption** (at rest and in transit)
- 📊 **Zero idle cost** (all services scale to zero)
- 📝 **13 issues** delivered (strict TDD)

Ready for deployment and production use. Patterns and meta-prompts ready for reuse in future projects.
