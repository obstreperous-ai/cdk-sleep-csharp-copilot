using Amazon.CDK;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.S3.Notifications;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace CdkBase
{
    public class CdkBaseStack : Stack
    {
        public IBucket InputBucket { get; }
        public IBucket OutputBucket { get; }
        public Rule EventBridgeRule { get; }
        public IStateMachine StateMachine { get; }

        public CdkBaseStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
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

            // CloudWatch Logs for State Machine
            var stateMachineLogGroup = new Amazon.CDK.AWS.Logs.LogGroup(this, "SleepAudioPipelineStateMachineLogGroup", new Amazon.CDK.AWS.Logs.LogGroupProps
            {
                Retention = RetentionDays.ONE_WEEK,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Step Functions State Machine with Polly Integration
            // Define the state machine using JSON definition for a skeleton workflow
            var stateMachineDefinition = new System.Collections.Generic.Dictionary<string, object>
            {
                { "Comment", "Sleep Audio Pipeline State Machine - skeleton with Polly task" },
                { "StartAt", "PollyTask" },
                { "States", new System.Collections.Generic.Dictionary<string, object>
                    {
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

            // Create the State Machine using CfnStateMachine for full control
            var cfnStateMachine = new Amazon.CDK.AWS.StepFunctions.CfnStateMachine(this, "SleepAudioPipelineStateMachine", new Amazon.CDK.AWS.StepFunctions.CfnStateMachineProps
            {
                RoleArn = stateMachineRole.RoleArn,
                DefinitionString = definitionJson,
                StateMachineName = "SleepAudioPipelineStateMachine",
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
        }
    }
}
