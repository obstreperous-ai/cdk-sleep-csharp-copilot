# Architecture — Event-Driven Sleep Audio Pipeline

> **Status:** Partial Implementation (TDD-driven). The foundational infrastructure
> is now implemented: S3 input/output buckets, EventBridge rule for object
> creation events, **Step Functions state machine with Polly integration**, and
> **DynamoDB table for audio pipeline metadata with basic I/O handling**.
> The next slice is *"[6] TDD: SNS Notifications + Basic Error Handling & Status
> Updates"*.
>
> This document is the **single source of truth** for the system design. Every
> future issue and pull request must keep the code and this document consistent.
> If an implementation needs to diverge from this design, update this document in
> the same pull request and explain why.

## 0. Implementation Status

### Completed (Issue #3)
- ✅ **Input S3 Bucket** (`SleepAudioInputBucket`)
  - Private, encrypted with S3-managed keys (SSE-S3)
  - Versioning enabled
  - EventBridge notifications enabled for Object Created events
  - Public access blocked
  - TLS enforced
  - Retention policy: RETAIN

- ✅ **Output S3 Bucket** (`SleepAudioOutputBucket`)
  - Private, encrypted with S3-managed keys (SSE-S3)
  - Versioning enabled
  - Public access blocked
  - TLS enforced
  - Retention policy: RETAIN

- ✅ **EventBridge Rule** (`SleepAudioInputRule`)
  - Triggers on `Object Created` events from the Input Bucket
  - Filters events specifically for the input bucket by name
  - Rule is enabled
  - Targets the Step Functions state machine

### Completed (Issue #4)
- ✅ **Step Functions State Machine** (`SleepAudioPipelineStateMachine`)
  - Skeleton workflow with Polly integration
  - CloudWatch Logs enabled (ALL level, execution data included)
  - Least-privilege IAM execution role
  - Polly Task: Uses `startSpeechSynthesisTask` API
  - Placeholder parameters (text, voice, output format)
  - Triggered by EventBridge rule on S3 Object Created events

- ✅ **Polly Integration**
  - State machine includes Polly task state
  - Uses AWS SDK integration (`arn:aws:states:::aws-sdk:polly:startSpeechSynthesisTask`)
  - Placeholder text: "This is a placeholder sleep audio text."
  - Voice: Joanna
  - Output format: MP3
  - IAM permissions: `polly:StartSpeechSynthesisTask`, `polly:GetSpeechSynthesisTask`

- ✅ **Orchestration Layer**
  - EventBridge → Step Functions wiring complete
  - S3 event data passed to state machine as input
  - CloudWatch Logs log group created for state machine execution
  - 7-day log retention

### Completed (Issue #5)
- ✅ **DynamoDB Metadata Table** (`SleepAudioMetadataTable`)
  - Partition key: `audioId` (string, UUID)
  - Billing mode: PAY_PER_REQUEST (on-demand)
  - Encryption: AWS_MANAGED (SSE)
  - Point-in-time recovery: Enabled
  - Removal policy: RETAIN
  - Stores: `audioId`, `inputBucket`, `inputKey`, `status`, `createdAt`

- ✅ **State Machine I/O Handling**
  - InitMetadata state: First state in workflow
  - Uses `dynamodb:putItem` AWS SDK integration
  - Generates UUID for `audioId`
  - Captures S3 event data: bucket name, object key
  - Sets initial status: "PROCESSING"
  - Records creation timestamp
  - Workflow: InitMetadata → PollyTask
  - IAM permissions: State machine role granted `dynamodb:PutItem`

### Pending
- Lambda functions for validation, metadata extraction, persistence
- Full audio processing workflow (multi-step state machine)
- Amazon Bedrock integration (optional, feature-flagged)
- SNS topic for notifications
- CloudWatch alarms and observability
- Status updates (update DynamoDB status on completion/failure)

## 1. High-Level Overview

The Sleep Audio Pipeline is a serverless, **event-driven** system on AWS that
turns raw user-supplied audio into soothing, sleep-oriented audio assets.

A user uploads a raw audio file (a voice recording, an ambient capture, or a
short text prompt rendered as audio) to an **input S3 bucket**. The upload event
is detected by **Amazon EventBridge**, which starts an **AWS Step Functions**
state machine. The state machine validates the file, extracts metadata,
optionally generates or enhances audio with **Amazon Polly** (text-to-speech /
soothing narration) and **Amazon Bedrock** (AI-generated sleep sounds or audio
enhancement), writes the result to a **versioned output S3 bucket**, records
metadata in **Amazon DynamoDB**, and publishes a success or failure notification
to **Amazon SNS**.

The design favors managed, pay-per-use services so the pipeline scales to zero
when idle, requires no servers to patch, and isolates each processing step for
clear observability and least-privilege security.

### Design Goals

- **Event-driven & decoupled** — components communicate through events and
  durable storage, not synchronous calls, so each stage can fail and retry
  independently.
- **Serverless-first** — no always-on compute; cost scales with usage.
- **Secure by default** — least-privilege IAM, encryption at rest and in
  transit, private (non-public) buckets.
- **Observable** — structured logs, metrics, and alarms for every stage.
- **Multi-environment** — the same stack deploys to `dev`, `stage`, and `prod`
  via CDK context, with environment-specific naming and settings.
- **Extensible** — new processing steps can be added to the state machine
  without reworking the ingestion or storage layers.

## 2. Data Flow

1. **Upload.** A client uploads a raw object to the **input bucket** under a
   key convention such as `uploads/{user_id}/{upload_id}.{ext}`. Object-level
   metadata (for example `user_id`) travels with the object.
2. **Detect.** S3 emits an **`Object Created`** event to the default event bus.
   An **EventBridge rule** matches the input bucket (optionally filtered by key
   prefix/suffix) and triggers the workflow. EventBridge is used instead of a
   direct S3→Lambda notification so multiple consumers and richer routing can be
   added later without touching the bucket.
3. **Orchestrate.** EventBridge starts the **Step Functions** state machine,
   passing the bucket name and object key. The state machine owns the
   end-to-end processing logic.
   
   **Current Implementation (Issues #4-5 - Skeleton with Metadata):**
   - **InitMetadata State** — The first state writes an initial metadata record
     to DynamoDB using `dynamodb:putItem`. It generates a UUID for `audioId`,
     captures the S3 event data (bucket name, object key), sets status to
     "PROCESSING", and records the creation timestamp. The result is stored in
     `$.metadata` while preserving the original input for downstream states.
   - **Polly Task** — The skeleton state machine includes a Polly task that uses
     `startSpeechSynthesisTask` to generate MP3 audio from placeholder text
     ("This is a placeholder sleep audio text.") using the Joanna voice. The
     output is written to the S3 bucket specified in the event data.
   - **CloudWatch Logs** — All state machine execution events are logged to
     CloudWatch Logs with ALL level logging and execution data included.
   
   **Future Steps (Pending):**
   - **Validate** — confirm the object exists, the content type/size is within
     limits, and required metadata is present. Invalid inputs short-circuit to
     the failure path.
   - **Extract metadata** — read duration, format, sample rate, and size.
   - **Generate / enhance** *(conditional)* —
     - **Amazon Polly** synthesizes soothing narration from text input.
     - **Amazon Bedrock** generates ambient sleep sounds or enhances the audio.
       This branch is optional and feature-flagged per environment.
   - **Persist output** — write the processed object to the **output bucket**
     under `processed/{user_id}/{upload_id}.{ext}` with **versioning enabled**.
   - **Update metadata** — update the DynamoDB item with output location,
     processing duration, and final status.
4. **Notify.** On completion the state machine publishes to an **SNS topic**:
   a `SUCCEEDED` message with the output location, or a `FAILED` message with
   the error cause. Subscribers (email, queues, downstream services) react as
   needed.
5. **Observe.** Every Lambda/state transition emits **CloudWatch Logs** and
   metrics; **CloudWatch Alarms** fire on workflow failures and error-rate or
   latency thresholds.

### Status Lifecycle (DynamoDB `status`)

`PROCESSING → SUCCEEDED`, or `PROCESSING → FAILED`.

**Current Implementation:** Status is set to `PROCESSING` when the InitMetadata
state writes the initial record.

**Future:** Status will be updated to `SUCCEEDED` or `FAILED` based on the final
outcome of the workflow.

## 3. Key AWS Services and Rationale

| Service | Role in the pipeline | Why it was chosen |
| --- | --- | --- |
| **Amazon S3 (input)** | Durable landing zone for raw uploads. | Cheap, durable object storage with native event integration; private bucket with SSE. |
| **Amazon S3 (output)** | Stores processed audio with **versioning**. | Versioning preserves history and protects against accidental overwrite/regression of generated assets. |
| **Amazon EventBridge** | Detects uploads and routes them to the workflow. | Decouples producers from consumers, supports content-based filtering, and lets us add consumers without changing the bucket. |
| **AWS Step Functions** | Orchestrates validate → extract → generate → persist → notify. | Preferred over a single Lambda: built-in retries, error handling, branching, and a visual, auditable workflow. Each step stays small and least-privileged. |
| **AWS Lambda** | Implements individual task states (validation, metadata, persistence glue). | Serverless, pay-per-use compute that scales to zero. |
| **Amazon Polly** | Text-to-speech / soothing voice generation. | Managed neural TTS; no model hosting required. |
| **Amazon Bedrock** | Optional AI-generated sleep sounds / audio enhancement. | Access to foundation models without managing infrastructure; feature-flagged to control cost. |
| **Amazon DynamoDB** | Stores per-job metadata and processing status. | Serverless, single-digit-millisecond key-value store that scales with traffic; on-demand capacity avoids idle cost. |
| **Amazon SNS** | Completion and error notifications. | Simple pub/sub fan-out to email, queues, and downstream systems. |
| **Amazon CloudWatch** | Logs, metrics, and alarms. | Native observability for all of the above. |
| **AWS IAM + KMS** | Least-privilege roles and encryption keys. | Enforces security boundaries and encryption at rest. |

## 4. Architecture Diagram

> **Note:** Components with solid lines are implemented. Components with dashed
> lines are pending implementation.

```mermaid
flowchart TD
    user([User / Client]) -->|1 Upload raw audio| inputBucket

    subgraph ingestion[✅ Ingestion - IMPLEMENTED]
        inputBucket[(S3 Input Bucket<br/>private · SSE)]
        eventBridge{{EventBridge Rule<br/>Object Created}}
        inputBucket -->|2 Object Created event| eventBridge
        outputBucket[(S3 Output Bucket<br/>versioned · SSE)]
    end

    eventBridge -->|3 Start execution| sfn

    subgraph processing[✅ Orchestration - SKELETON WITH METADATA]
        sfn[Step Functions<br/>State Machine]
        initMetadata[InitMetadata<br/>dynamodb:putItem]
        pollyTask[Polly Task<br/>startSpeechSynthesisTask]
        
        sfn --> initMetadata
        initMetadata -->|Write initial record| dynamo
        initMetadata --> pollyTask
        pollyTask -->|TTS| polly[[Amazon Polly]]
        
        validate[⏳ Validate input]
        metadata[⏳ Extract metadata]
        persist[⏳ Persist output]
        updateMetadata[⏳ Update metadata]
        
        pollyTask -.->|Future steps| validate
        validate -.-> metadata
        metadata -.-> persist
        persist -.-> updateMetadata
        updateMetadata -.->|Upsert| dynamo
        validate -.->|invalid| failNotify
    end
    
    dynamo[(✅ DynamoDB<br/>Metadata Table<br/>audioId · status)]

    pollyTask -->|4 Write to S3| outputBucket
    updateMetadata -.->|5 Success| successNotify
    failNotify[⏳ Build failure event]

    subgraph notifications[⏳ Notifications - PENDING]
        successNotify[Publish SUCCEEDED]
        failNotify -.-> snsFail[Publish FAILED]
        successNotify -.-> sns([SNS Topic])
        snsFail -.-> sns
    end

    sns -.->|7 Notify subscribers| subscribers([Email / Queues / Downstream])

    subgraph observability[✅ Logging - PARTIAL]
        logs[[CloudWatch Logs<br/>State Machine]]
        alarms[[⏳ CloudWatch Alarms]]
    end

    processing --> logs
    logs -.-> alarms

    style ingestion fill:#d4edda,stroke:#28a745,stroke-width:3px
    style inputBucket fill:#d4edda,stroke:#28a745,stroke-width:2px
    style eventBridge fill:#d4edda,stroke:#28a745,stroke-width:2px
    style outputBucket fill:#d4edda,stroke:#28a745,stroke-width:2px
    style processing fill:#d4edda,stroke:#28a745,stroke-width:3px
    style sfn fill:#d4edda,stroke:#28a745,stroke-width:2px
    style initMetadata fill:#d4edda,stroke:#28a745,stroke-width:2px
    style pollyTask fill:#d4edda,stroke:#28a745,stroke-width:2px
    style dynamo fill:#d4edda,stroke:#28a745,stroke-width:2px
    style logs fill:#d4edda,stroke:#28a745,stroke-width:2px
```

## 5. Security

- **Private buckets.** Both S3 buckets block all public access. Access is via
  IAM principals only; TLS is enforced for data in transit.
- **Encryption at rest.** S3 (SSE, KMS where required), DynamoDB, and SNS are
  encrypted at rest. Output-bucket **versioning** guards against accidental
  loss or overwrite.
- **Least-privilege IAM.** Each Lambda/task state receives a narrowly scoped
  role — for example, the validation step gets `s3:GetObject` on the input
  bucket only; the persistence step gets `s3:PutObject` on the output bucket
  only; the metadata step gets scoped DynamoDB write access. No wildcard
  resource ARNs.
- **Scoped invocation.** EventBridge is granted permission only to start the
  specific state machine; Step Functions assumes purpose-built task roles.
- **Secrets & config.** No credentials in code; configuration flows through CDK
  context and environment variables, with sensitive values in SSM/Secrets
  Manager when needed.

## 6. Observability

- **Logging.** Step Functions execution history plus structured CloudWatch Logs
  from every task state, correlated by `upload_id`.
- **Metrics & alarms.** CloudWatch alarms on Step Functions `ExecutionsFailed`,
  Lambda error rate and duration, and DynamoDB throttling. Alarms notify the
  SNS topic (or a dedicated ops topic) so failures are actionable.
- **Traceability.** `user_id` and `upload_id` are propagated through events,
  logs, DynamoDB items, and notifications for end-to-end tracing.

## 7. Cost Considerations

- **Scale-to-zero.** All compute (Lambda, Step Functions) and DynamoDB
  on-demand capacity cost nothing when idle.
- **Feature-flag expensive paths.** The Bedrock generation/enhancement branch
  is optional and can be disabled per environment (for example off in `dev`) to
  control inference cost.
- **Storage lifecycle.** Lifecycle rules can transition or expire old raw
  uploads and non-current output versions to cheaper storage tiers.
- **Right-sized notifications.** SNS and CloudWatch usage are proportional to
  pipeline volume.

## 8. Multi-Environment Support

The stack is parameterized by a CDK **context** value (for example
`-c env=dev|stage|prod`). The environment drives:

- Resource naming/prefixes to avoid collisions across environments.
- Feature flags (for example enabling Bedrock only in `stage`/`prod`).
- Capacity, alarm thresholds, and retention/lifecycle settings.
- Removal policies (more permissive cleanup in `dev`, retain in `prod`).

## 9. Future Extensibility

- **Additional processing steps** (noise reduction, loudness normalization,
  format transcoding) slot into the Step Functions workflow without changing
  ingestion or storage.
- **New consumers** can subscribe to the same EventBridge events or SNS topic
  (analytics, search indexing, moderation) with no producer changes.
- **API layer** (API Gateway + Lambda) can be added in front of the input
  bucket for pre-signed uploads and job status queries backed by DynamoDB.
- **Catalog / search** over generated assets can be built from the DynamoDB
  metadata table.

## 10. Out of Scope (for the initial design)

- Client/mobile applications and authentication of end users.
- Billing/subscription management.
- A public REST/GraphQL API (noted as a future extension above).
