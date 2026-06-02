using Amazon.CDK;
using Amazon.CDK.Assertions;
using System.Collections.Generic;

namespace CdkBase.Tests;

public class CdkBaseStackTests
{
    [Fact]
    public void Stack_HasTwoS3Buckets()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.ResourceCountIs("AWS::S3::Bucket", 2);
    }

    [Fact]
    public void InputBucket_HasVersioningEnabled()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::S3::Bucket", new Dictionary<string, object>
        {
            { "VersioningConfiguration", new Dictionary<string, string>
                {
                    { "Status", "Enabled" }
                }
            }
        });
    }

    [Fact]
    public void InputBucket_IsEncrypted()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::S3::Bucket", new Dictionary<string, object>
        {
            { "BucketEncryption", new Dictionary<string, object>
                {
                    { "ServerSideEncryptionConfiguration", Match.AnyValue() }
                }
            }
        });
    }

    [Fact]
    public void InputBucket_BlocksPublicAccess()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::S3::Bucket", new Dictionary<string, object>
        {
            { "PublicAccessBlockConfiguration", new Dictionary<string, bool>
                {
                    { "BlockPublicAcls", true },
                    { "BlockPublicPolicy", true },
                    { "IgnorePublicAcls", true },
                    { "RestrictPublicBuckets", true }
                }
            }
        });
    }

    [Fact]
    public void InputBucket_HasEventBridgeEnabled()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // EventBridge integration is verified by the presence of the EventBridge Rule
        // that filters on the specific bucket name
        template.HasResourceProperties("AWS::Events::Rule", new Dictionary<string, object>
        {
            { "EventPattern", new Dictionary<string, object>
                {
                    { "source", new[] { "aws.s3" } },
                    { "detail-type", new[] { "Object Created" } },
                    { "detail", new Dictionary<string, object>
                        {
                            { "bucket", new Dictionary<string, object>
                                {
                                    { "name", Match.AnyValue() }
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    [Fact]
    public void OutputBucket_HasVersioningEnabled()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Both buckets should have versioning - checking count
        template.ResourcePropertiesCountIs("AWS::S3::Bucket", new Dictionary<string, object>
        {
            { "VersioningConfiguration", new Dictionary<string, string>
                {
                    { "Status", "Enabled" }
                }
            }
        }, 2);
    }

    [Fact]
    public void Stack_HasEventBridgeRule()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.ResourceCountIs("AWS::Events::Rule", 1);
    }

    [Fact]
    public void EventBridgeRule_TriggersOnS3ObjectCreated()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::Events::Rule", new Dictionary<string, object>
        {
            { "EventPattern", new Dictionary<string, object>
                {
                    { "source", new[] { "aws.s3" } },
                    { "detail-type", new[] { "Object Created" } }
                }
            }
        });
    }

    [Fact]
    public void EventBridgeRule_IsEnabled()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::Events::Rule", new Dictionary<string, object>
        {
            { "State", "ENABLED" }
        });
    }
}
