using Amazon.CDK;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.S3.Notifications;
using Constructs;

namespace CdkBase
{
    public class CdkBaseStack : Stack
    {
        public IBucket InputBucket { get; }
        public IBucket OutputBucket { get; }
        public Rule EventBridgeRule { get; }

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

            // Note: Target will be added in a future issue when Step Functions state machine is implemented
            // For now, the rule is configured but has no target (placeholder)
        }
    }
}
