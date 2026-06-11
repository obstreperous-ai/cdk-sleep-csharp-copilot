using Amazon.CDK;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.S3.Notifications;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.KMS;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.CloudWatch;
using Constructs;

namespace CdkBase
{
    public class CdkBaseStack : Stack
    {
        public IBucket InputBucket { get; }
        public IBucket OutputBucket { get; }
        public Rule EventBridgeRule { get; }
        public IStateMachine StateMachine { get; }
        public ITable MetadataTable { get; }
        public ITopic CompletedTopic { get; }
        public ITopic FailedTopic { get; }
        public IFunction AudioProcessorFunction { get; }
        public string EnvironmentName { get; }

        /// <summary>
        /// Creates a new instance of CdkBaseStack with default environment.
        /// This constructor exists for backward compatibility with existing tests and defaults to 'dev' environment.
        /// </summary>
        public CdkBaseStack(Construct scope, string id, IStackProps props = null) 
            : this(scope, id, props, "dev")
        {
        }

        /// <summary>
        /// Creates a new instance of CdkBaseStack with specified environment name.
        /// </summary>
        /// <param name="scope">The parent construct</param>
        /// <param name="id">The stack ID</param>
        /// <param name="props">Stack properties (account, region, etc.)</param>
        /// <param name="environmentName">Environment name (dev, stage, prod) for resource naming and configuration</param>
        public CdkBaseStack(Construct scope, string id, IStackProps props, string environmentName) : base(scope, id, props)
        {
            EnvironmentName = environmentName;
            
            // Input S3 Bucket for raw uploads
            InputBucket = new Bucket(this, "SleepAudioInputBucket", new BucketProps
            {
                Versioned = true,
                Encryption = BucketEncryption.S3_MANAGED,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                EnforceSSL = true,
                EventBridgeEnabled = true,
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            // Output S3 Bucket for processed audio
            // Note: EventBridge is not enabled for the output bucket because this bucket
            // is written to by the pipeline (Step Functions/Lambda), not by external users.
            // We don't need to trigger workflows on output writes.
            OutputBucket = new Bucket(this, "SleepAudioOutputBucket", new BucketProps
            {
                Versioned = true,
                Encryption = BucketEncryption.S3_MANAGED,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                EnforceSSL = true,
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            // EventBridge Rule to detect Object Created events from Input Bucket
            EventBridgeRule = new Rule(this, "SleepAudioInputRule", new RuleProps
            {
                EventPattern = new EventPattern
                {
                    Source = new[] { "aws.s3" },
                    DetailType = new[] { "Object Created" },
                    Detail = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "bucket", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "name", new[] { InputBucket.BucketName } }
                            }
                        }
                    }
                },
                Enabled = true
            });

            // DynamoDB Table for Audio Pipeline Metadata
            MetadataTable = new Table(this, "SleepAudioMetadataTable", new TableProps
            {
                PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
                {
                    Name = "audioId",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                Encryption = TableEncryption.AWS_MANAGED,
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            // Enable Point-in-Time Recovery using the underlying CfnTable
            var cfnTable = (CfnTable)MetadataTable.Node.DefaultChild;
            cfnTable.PointInTimeRecoverySpecification = new CfnTable.PointInTimeRecoverySpecificationProperty
            {
                PointInTimeRecoveryEnabled = true
            };

            // KMS Key for SNS Topic Encryption
            var snsKey = new Key(this, "SleepAudioPipelineSNSKey", new KeyProps
            {
                Description = "KMS key for Sleep Audio Pipeline SNS topic encryption",
                EnableKeyRotation = true,
                RemovalPolicy = RemovalPolicy.RETAIN
            });

            // SNS Topic for Pipeline Completion Notifications
            CompletedTopic = new Topic(this, "SleepAudioPipelineCompletedTopic", new TopicProps
            {
                DisplayName = "Sleep Audio Pipeline Completed",
                MasterKey = snsKey
            });

            // SNS Topic for Pipeline Failure Notifications
            FailedTopic = new Topic(this, "SleepAudioPipelineFailedTopic", new TopicProps
            {
                DisplayName = "Sleep Audio Pipeline Failed",
                MasterKey = snsKey
            });

            // Lambda Function for Audio Processing
            // Use hardcoded absolute path for the Lambda publish directory
            // This works consistently for both synth and test scenarios
            var lambdaPublishPath = "/tmp/workspace/obstreperous-ai/cdk-sleep-csharp-copilot/src/SleepAudioProcessor/bin/Release/net8.0/publish";
            
            AudioProcessorFunction = new Function(this, "SleepAudioProcessorFunction", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "SleepAudioProcessor::SleepAudioProcessor.Function::FunctionHandler",
                Code = Code.FromAsset(lambdaPublishPath),
                Environment = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "TABLE_NAME", MetadataTable.TableName },
                    { "INPUT_BUCKET_NAME", InputBucket.BucketName },
                    { "OUTPUT_BUCKET_NAME", OutputBucket.BucketName }
                },
                Timeout = Duration.Seconds(30),
                MemorySize = 512,
                Description = "Processes audio files from input bucket, generates/enhances sleep audio, and stores results in output bucket",
                Tracing = Tracing.ACTIVE
            });

            // Grant Lambda permissions to update DynamoDB table
            MetadataTable.GrantWriteData(AudioProcessorFunction);

            // Grant Lambda permissions to read from Input S3 bucket
            InputBucket.GrantRead(AudioProcessorFunction);

            // Grant Lambda permissions to write to Output S3 bucket
            OutputBucket.GrantWrite(AudioProcessorFunction);

            // Grant Lambda permissions to use Amazon Polly for speech synthesis
            // Note: Polly's SynthesizeSpeech action does not support resource-level permissions,
            // so we must use a wildcard resource. This is an AWS service limitation.
            AudioProcessorFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] { "polly:SynthesizeSpeech" },
                Resources = new[] { "*" }
            }));

            // CloudWatch Logs for State Machine
            var stateMachineLogGroup = new Amazon.CDK.AWS.Logs.LogGroup(this, "SleepAudioPipelineStateMachineLogGroup", new Amazon.CDK.AWS.Logs.LogGroupProps
            {
                Retention = RetentionDays.ONE_WEEK,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Step Functions State Machine with Polly Integration, Error Handling, and Notifications
            // Define the state machine using JSON definition for a skeleton workflow
            var stateMachineDefinition = new System.Collections.Generic.Dictionary<string, object>
            {
                { "Comment", "Sleep Audio Pipeline State Machine - with error handling, SNS notifications, and status updates" },
                { "StartAt", "InitMetadata" },
                { "States", new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "InitMetadata", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Type", "Task" },
                                { "Resource", "arn:aws:states:::dynamodb:putItem" },
                                { "Parameters", new System.Collections.Generic.Dictionary<string, object>
                                    {
                                        { "TableName", MetadataTable.TableName },
                                        { "Item", new System.Collections.Generic.Dictionary<string, object>
                                            {
                                                { "audioId", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S.$", "States.UUID()" }
                                                    }
                                                },
                                                { "inputBucket", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S.$", "$.detail.bucket.name" }
                                                    }
                                                },
                                                { "inputKey", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S.$", "$.detail.object.key" }
                                                    }
                                                },
                                                { "status", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S", "PROCESSING" }
                                                    }
                                                },
                                                { "createdAt", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S.$", "$$.State.EnteredTime" }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                { "ResultPath", "$.metadata" },
                                { "Next", "ValidateInput" },
                                { "Retry", new object[]
                                    {
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "DynamoDB.ProvisionedThroughputExceededException" } },
                                            { "IntervalSeconds", 2 },
                                            { "MaxAttempts", 3 },
                                            { "BackoffRate", 2.0 }
                                        },
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "States.TaskFailed" } },
                                            { "IntervalSeconds", 1 },
                                            { "MaxAttempts", 2 },
                                            { "BackoffRate", 2.0 }
                                        }
                                    }
                                },
                                { "Catch", new object[]
                                    {
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "DynamoDB.ProvisionedThroughputExceededException" } },
                                            { "ResultPath", "$.error" },
                                            { "Next", "UpdateStatusFailed" }
                                        },
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "States.ALL" } },
                                            { "ResultPath", "$.error" },
                                            { "Next", "UpdateStatusFailed" }
                                        }
                                    }
                                }
                            }
                        },
                        { "ValidateInput", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Type", "Choice" },
                                { "Comment", "Validate input from S3 event: check required fields and file extension" },
                                { "Choices", new object[]
                                    {
                                        // Check if bucket name is missing or empty
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "Not", new System.Collections.Generic.Dictionary<string, object>
                                                {
                                                    { "Variable", "$.detail.bucket.name" },
                                                    { "IsPresent", true }
                                                }
                                            },
                                            { "Next", "ValidationFailed" }
                                        },
                                        // Check if object key is missing or empty
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "Not", new System.Collections.Generic.Dictionary<string, object>
                                                {
                                                    { "Variable", "$.detail.object.key" },
                                                    { "IsPresent", true }
                                                }
                                            },
                                            { "Next", "ValidationFailed" }
                                        },
                                        // Check if file extension is .mp3 (lowercase)
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "Variable", "$.detail.object.key" },
                                            { "StringMatches", "*.mp3" },
                                            { "Next", "ProcessAudio" }
                                        },
                                        // Check if file extension is .MP3 (uppercase)
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "Variable", "$.detail.object.key" },
                                            { "StringMatches", "*.MP3" },
                                            { "Next", "ProcessAudio" }
                                        },
                                        // Check if file extension is .wav (lowercase)
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "Variable", "$.detail.object.key" },
                                            { "StringMatches", "*.wav" },
                                            { "Next", "ProcessAudio" }
                                        },
                                        // Check if file extension is .WAV (uppercase)
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "Variable", "$.detail.object.key" },
                                            { "StringMatches", "*.WAV" },
                                            { "Next", "ProcessAudio" }
                                        },
                                        // Check if file extension is .m4a (lowercase)
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "Variable", "$.detail.object.key" },
                                            { "StringMatches", "*.m4a" },
                                            { "Next", "ProcessAudio" }
                                        },
                                        // Check if file extension is .M4A (uppercase)
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "Variable", "$.detail.object.key" },
                                            { "StringMatches", "*.M4A" },
                                            { "Next", "ProcessAudio" }
                                        }
                                    }
                                },
                                // Default path if no conditions match - unsupported file type
                                { "Default", "ValidationFailed" }
                            }
                        },
                        { "ValidationFailed", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Type", "Pass" },
                                { "Comment", "Mark validation failure and prepare error information" },
                                { "Result", new System.Collections.Generic.Dictionary<string, object>
                                    {
                                        { "Error", "ValidationError" },
                                        { "Cause", "Invalid input: missing required fields or unsupported file type" }
                                    }
                                },
                                { "ResultPath", "$.error" },
                                { "Next", "UpdateStatusFailed" }
                            }
                        },
                        { "ProcessAudio", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Type", "Task" },
                                { "Resource", "arn:aws:states:::lambda:invoke" },
                                { "Parameters", new System.Collections.Generic.Dictionary<string, object>
                                    {
                                        { "FunctionName", AudioProcessorFunction.FunctionArn },
                                        { "Payload.$", "$" }
                                    }
                                },
                                { "ResultPath", "$.processorResult" },
                                { "Next", "PollyTask" },
                                { "Retry", new object[]
                                    {
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "Lambda.ServiceException", "Lambda.TooManyRequestsException" } },
                                            { "IntervalSeconds", 2 },
                                            { "MaxAttempts", 3 },
                                            { "BackoffRate", 2.0 }
                                        },
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "States.TaskFailed" } },
                                            { "IntervalSeconds", 1 },
                                            { "MaxAttempts", 2 },
                                            { "BackoffRate", 2.0 }
                                        }
                                    }
                                },
                                { "Catch", new object[]
                                    {
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "Lambda.ServiceException" } },
                                            { "ResultPath", "$.error" },
                                            { "Next", "UpdateStatusFailed" }
                                        },
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "States.TaskFailed" } },
                                            { "ResultPath", "$.error" },
                                            { "Next", "UpdateStatusFailed" }
                                        },
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "States.ALL" } },
                                            { "ResultPath", "$.error" },
                                            { "Next", "UpdateStatusFailed" }
                                        }
                                    }
                                }
                            }
                        },
                        { "PollyTask", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Type", "Task" },
                                { "Resource", "arn:aws:states:::aws-sdk:polly:startSpeechSynthesisTask" },
                                { "Parameters", new System.Collections.Generic.Dictionary<string, object>
                                    {
                                        { "OutputFormat", "mp3" },
                                        { "Text", "This is a placeholder sleep audio text." },
                                        { "VoiceId", "Joanna" },
                                        { "OutputS3BucketName.$", "$.detail.bucket.name" }
                                    }
                                },
                                { "ResultPath", "$.pollyResult" },
                                { "Next", "UpdateStatusCompleted" },
                                { "Retry", new object[]
                                    {
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "Polly.ServiceException", "Polly.ThrottlingException" } },
                                            { "IntervalSeconds", 3 },
                                            { "MaxAttempts", 3 },
                                            { "BackoffRate", 2.0 }
                                        },
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "States.TaskFailed" } },
                                            { "IntervalSeconds", 2 },
                                            { "MaxAttempts", 2 },
                                            { "BackoffRate", 2.0 }
                                        }
                                    }
                                },
                                { "Catch", new object[]
                                    {
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "Polly.ServiceException" } },
                                            { "ResultPath", "$.error" },
                                            { "Next", "UpdateStatusFailed" }
                                        },
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "States.TaskFailed" } },
                                            { "ResultPath", "$.error" },
                                            { "Next", "UpdateStatusFailed" }
                                        },
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "States.ALL" } },
                                            { "ResultPath", "$.error" },
                                            { "Next", "UpdateStatusFailed" }
                                        }
                                    }
                                }
                            }
                        },
                        { "UpdateStatusCompleted", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Type", "Task" },
                                { "Resource", "arn:aws:states:::dynamodb:updateItem" },
                                { "Parameters", new System.Collections.Generic.Dictionary<string, object>
                                    {
                                        { "TableName", MetadataTable.TableName },
                                        { "Key", new System.Collections.Generic.Dictionary<string, object>
                                            {
                                                { "audioId", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S.$", "$.metadata.Item.audioId.S" }
                                                    }
                                                }
                                            }
                                        },
                                        { "UpdateExpression", "SET #status = :status, #updatedAt = :updatedAt" },
                                        { "ExpressionAttributeNames", new System.Collections.Generic.Dictionary<string, string>
                                            {
                                                { "#status", "status" },
                                                { "#updatedAt", "updatedAt" }
                                            }
                                        },
                                        { "ExpressionAttributeValues", new System.Collections.Generic.Dictionary<string, object>
                                            {
                                                { ":status", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S", "COMPLETED" }
                                                    }
                                                },
                                                { ":updatedAt", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S.$", "$$.State.EnteredTime" }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                { "ResultPath", "$.updateResult" },
                                { "Next", "NotifySuccess" },
                                { "Retry", new object[]
                                    {
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "DynamoDB.ProvisionedThroughputExceededException" } },
                                            { "IntervalSeconds", 2 },
                                            { "MaxAttempts", 3 },
                                            { "BackoffRate", 2.0 }
                                        }
                                    }
                                }
                            }
                        },
                        { "UpdateStatusFailed", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Type", "Task" },
                                { "Resource", "arn:aws:states:::dynamodb:updateItem" },
                                { "Parameters", new System.Collections.Generic.Dictionary<string, object>
                                    {
                                        { "TableName", MetadataTable.TableName },
                                        { "Key", new System.Collections.Generic.Dictionary<string, object>
                                            {
                                                { "audioId", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S.$", "$.metadata.Item.audioId.S" }
                                                    }
                                                }
                                            }
                                        },
                                        { "UpdateExpression", "SET #status = :status, #updatedAt = :updatedAt" },
                                        { "ExpressionAttributeNames", new System.Collections.Generic.Dictionary<string, string>
                                            {
                                                { "#status", "status" },
                                                { "#updatedAt", "updatedAt" }
                                            }
                                        },
                                        { "ExpressionAttributeValues", new System.Collections.Generic.Dictionary<string, object>
                                            {
                                                { ":status", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S", "FAILED" }
                                                    }
                                                },
                                                { ":updatedAt", new System.Collections.Generic.Dictionary<string, object>
                                                    {
                                                        { "S.$", "$$.State.EnteredTime" }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                { "ResultPath", "$.updateResult" },
                                { "Next", "NotifyFailure" },
                                { "Retry", new object[]
                                    {
                                        new System.Collections.Generic.Dictionary<string, object>
                                        {
                                            { "ErrorEquals", new[] { "DynamoDB.ProvisionedThroughputExceededException" } },
                                            { "IntervalSeconds", 2 },
                                            { "MaxAttempts", 3 },
                                            { "BackoffRate", 2.0 }
                                        }
                                    }
                                }
                            }
                        },
                        { "NotifySuccess", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Type", "Task" },
                                { "Resource", "arn:aws:states:::sns:publish" },
                                { "Parameters", new System.Collections.Generic.Dictionary<string, object>
                                    {
                                        { "TopicArn", CompletedTopic.TopicArn },
                                        { "Message.$", "States.Format('Sleep audio pipeline completed successfully for audioId: {}', $.metadata.Item.audioId.S)" },
                                        { "Subject", "Sleep Audio Pipeline - Completed" }
                                    }
                                },
                                { "End", true }
                            }
                        },
                        { "NotifyFailure", new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "Type", "Task" },
                                { "Resource", "arn:aws:states:::sns:publish" },
                                { "Parameters", new System.Collections.Generic.Dictionary<string, object>
                                    {
                                        { "TopicArn", FailedTopic.TopicArn },
                                        { "Message.$", "States.Format('Sleep audio pipeline failed for audioId: {}. Error: {}', $.metadata.Item.audioId.S, States.JsonToString($.error))" },
                                        { "Subject", "Sleep Audio Pipeline - Failed" }
                                    }
                                },
                                { "End", true }
                            }
                        }
                    }
                }
            };

            var definitionJson = System.Text.Json.JsonSerializer.Serialize(stateMachineDefinition);

            // Create IAM Role for State Machine with least privilege
            var stateMachineRole = new Role(this, "SleepAudioPipelineStateMachineRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("states.amazonaws.com"),
                Description = "Execution role for Sleep Audio Pipeline State Machine"
            });

            // Grant Polly permissions (least privilege)
            stateMachineRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] 
                { 
                    "polly:StartSpeechSynthesisTask",
                    "polly:GetSpeechSynthesisTask"
                },
                Resources = new[] { "*" }
            }));

            // Grant DynamoDB write permissions (least privilege)
            stateMachineRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] { "dynamodb:PutItem", "dynamodb:UpdateItem" },
                Resources = new[] { MetadataTable.TableArn }
            }));

            // Grant SNS publish permissions (least privilege)
            stateMachineRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] { "sns:Publish" },
                Resources = new[] { CompletedTopic.TopicArn, FailedTopic.TopicArn }
            }));

            // Grant Lambda invoke permissions (least privilege)
            stateMachineRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] { "lambda:InvokeFunction" },
                Resources = new[] { AudioProcessorFunction.FunctionArn }
            }));

            // Grant S3 permissions for reading input and writing output
            InputBucket.GrantRead(stateMachineRole);
            OutputBucket.GrantWrite(stateMachineRole);

            // Grant CloudWatch Logs permissions
            stateMachineRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
                    "logs:CreateLogDelivery",
                    "logs:GetLogDelivery",
                    "logs:UpdateLogDelivery",
                    "logs:DeleteLogDelivery",
                    "logs:ListLogDeliveries",
                    "logs:PutResourcePolicy",
                    "logs:DescribeResourcePolicies",
                    "logs:DescribeLogGroups"
                },
                Resources = new[] { "*" }
            }));

            // Grant X-Ray permissions for State Machine
            stateMachineRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]
                {
                    "xray:PutTraceSegments",
                    "xray:PutTelemetryRecords"
                },
                Resources = new[] { "*" }
            }));

            // Create the State Machine using CfnStateMachine for full control
            var cfnStateMachine = new Amazon.CDK.AWS.StepFunctions.CfnStateMachine(this, "SleepAudioPipelineStateMachine", new Amazon.CDK.AWS.StepFunctions.CfnStateMachineProps
            {
                RoleArn = stateMachineRole.RoleArn,
                DefinitionString = definitionJson,
                StateMachineName = "SleepAudioPipelineStateMachine",
                TracingConfiguration = new Amazon.CDK.AWS.StepFunctions.CfnStateMachine.TracingConfigurationProperty
                {
                    Enabled = true
                },
                LoggingConfiguration = new Amazon.CDK.AWS.StepFunctions.CfnStateMachine.LoggingConfigurationProperty
                {
                    Level = "ALL",
                    IncludeExecutionData = true,
                    Destinations = new[]
                    {
                        new Amazon.CDK.AWS.StepFunctions.CfnStateMachine.LogDestinationProperty
                        {
                            CloudWatchLogsLogGroup = new Amazon.CDK.AWS.StepFunctions.CfnStateMachine.CloudWatchLogsLogGroupProperty
                            {
                                LogGroupArn = stateMachineLogGroup.LogGroupArn
                            }
                        }
                    }
                }
            });

            // Store reference for testing
            StateMachine = Amazon.CDK.AWS.StepFunctions.StateMachine.FromStateMachineArn(
                this, 
                "StateMachineRef", 
                cfnStateMachine.AttrArn
            );

            // Wire EventBridge rule to trigger the state machine
            EventBridgeRule.AddTarget(new Amazon.CDK.AWS.Events.Targets.SfnStateMachine(StateMachine, new Amazon.CDK.AWS.Events.Targets.SfnStateMachineProps
            {
                Input = Amazon.CDK.AWS.Events.RuleTargetInput.FromEventPath("$")
            }));

            // CloudWatch Alarms for Observability

            // Alarm for State Machine Execution Failures
            var stateMachineFailureAlarm = new Alarm(this, "SleepAudioPipelineStateMachineFailureAlarm", new AlarmProps
            {
                Metric = new Metric(new MetricProps
                {
                    Namespace = "AWS/States",
                    MetricName = "ExecutionsFailed",
                    DimensionsMap = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "StateMachineArn", cfnStateMachine.AttrArn }
                    },
                    Statistic = "Sum",
                    Period = Duration.Minutes(5)
                }),
                Threshold = 1,
                EvaluationPeriods = 1,
                ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
                AlarmDescription = "Alert when Step Functions state machine executions fail",
                TreatMissingData = TreatMissingData.NOT_BREACHING
            });

            // Alarm for Lambda Function Errors
            var lambdaErrorAlarm = new Alarm(this, "SleepAudioProcessorLambdaErrorAlarm", new AlarmProps
            {
                Metric = new Metric(new MetricProps
                {
                    Namespace = "AWS/Lambda",
                    MetricName = "Errors",
                    DimensionsMap = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "FunctionName", AudioProcessorFunction.FunctionName }
                    },
                    Statistic = "Sum",
                    Period = Duration.Minutes(5)
                }),
                Threshold = 1,
                EvaluationPeriods = 1,
                ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
                AlarmDescription = "Alert when Lambda function errors occur",
                TreatMissingData = TreatMissingData.NOT_BREACHING
            });

            // Optional: Add alarm actions to publish to SNS topic
            stateMachineFailureAlarm.AddAlarmAction(new Amazon.CDK.AWS.CloudWatch.Actions.SnsAction(FailedTopic));
            lambdaErrorAlarm.AddAlarmAction(new Amazon.CDK.AWS.CloudWatch.Actions.SnsAction(FailedTopic));
        }
    }
}
