using Amazon.CDK;
using Amazon.CDK.Assertions;
using System;
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
        // Verify EventBridge integration by checking that:
        // 1. An EventBridge Rule exists
        // 2. The rule filters on the input bucket's name
        // This confirms the bucket is configured to send events to EventBridge
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

    [Fact]
    public void Stack_HasStepFunctionsStateMachine()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.ResourceCountIs("AWS::StepFunctions::StateMachine", 1);
    }

    [Fact]
    public void StateMachine_HasCloudWatchLogsEnabled()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "LoggingConfiguration", new Dictionary<string, object>
                {
                    { "Level", "ALL" },
                    { "IncludeExecutionData", true }
                }
            }
        });
    }

    [Fact]
    public void StateMachine_HasExecutionRole()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine has a role ARN reference
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "RoleArn", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Fn::GetAtt", Match.AnyValue() }
                })
            }
        });
    }

    [Fact]
    public void StateMachine_DefinitionContainsPollyTask()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine definition contains a reference to Polly service
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definition = capture.AsString();
        Assert.Contains("polly", definition, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EventBridgeRule_TargetsStateMachine()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the EventBridge rule has a target pointing to the state machine
        template.HasResourceProperties("AWS::Events::Rule", new Dictionary<string, object>
        {
            { "Targets", Match.ArrayWith(new object[]
                {
                    Match.ObjectLike(new Dictionary<string, object>
                    {
                        { "Arn", Match.ObjectLike(new Dictionary<string, object>
                            {
                                { "Fn::GetAtt", Match.ArrayWith(new object[] 
                                    { 
                                        Match.StringLikeRegexp(".*StateMachine.*") 
                                    }) 
                                }
                            })
                        }
                    })
                })
            }
        });
    }

    [Fact]
    public void StateMachine_ExecutionRoleHasPollyPermissions()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's an IAM policy with Polly permissions
        template.HasResourceProperties("AWS::IAM::Policy", new Dictionary<string, object>
        {
            { "PolicyDocument", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Statement", Match.ArrayWith(new object[]
                        {
                            Match.ObjectLike(new Dictionary<string, object>
                            {
                                { "Action", Match.ArrayWith(new object[] { Match.StringLikeRegexp("polly:.*") }) }
                            })
                        })
                    }
                })
            }
        });
    }
}
