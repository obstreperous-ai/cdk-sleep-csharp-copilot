# Event-Driven Sleep Audio Pipeline

A fully serverless, event-driven AWS CDK application that processes audio files into soothing sleep audio using AWS managed services.

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
* [`SUMMARY.md`](SUMMARY.md) — Project summary and key decisions

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

**Complete** — All core functionality implemented and tested through Issues #2-#12:
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

Ready for deployment and production use.
