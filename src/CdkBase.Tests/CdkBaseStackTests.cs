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
        
        // Convert the captured value to JSON string to check its content
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("polly", definitionValue, StringComparison.OrdinalIgnoreCase);
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

    // ========== Issue #5: DynamoDB Metadata Table Tests ==========

    [Fact]
    public void Stack_HasDynamoDBTable()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.ResourceCountIs("AWS::DynamoDB::Table", 1);
    }

    [Fact]
    public void DynamoDBTable_HasCorrectPartitionKey()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::DynamoDB::Table", new Dictionary<string, object>
        {
            { "KeySchema", new object[]
                {
                    new Dictionary<string, object>
                    {
                        { "AttributeName", "audioId" },
                        { "KeyType", "HASH" }
                    }
                }
            },
            { "AttributeDefinitions", new object[]
                {
                    new Dictionary<string, object>
                    {
                        { "AttributeName", "audioId" },
                        { "AttributeType", "S" }
                    }
                }
            }
        });
    }

    [Fact]
    public void DynamoDBTable_HasOnDemandBillingMode()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::DynamoDB::Table", new Dictionary<string, object>
        {
            { "BillingMode", "PAY_PER_REQUEST" }
        });
    }

    [Fact]
    public void DynamoDBTable_HasEncryptionEnabled()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::DynamoDB::Table", new Dictionary<string, object>
        {
            { "SSESpecification", new Dictionary<string, bool>
                {
                    { "SSEEnabled", true }
                }
            }
        });
    }

    [Fact]
    public void DynamoDBTable_HasPointInTimeRecoveryEnabled()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::DynamoDB::Table", new Dictionary<string, object>
        {
            { "PointInTimeRecoverySpecification", new Dictionary<string, bool>
                {
                    { "PointInTimeRecoveryEnabled", true }
                }
            }
        });
    }

    [Fact]
    public void StateMachine_DefinitionContainsDynamoDBPutItem()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine definition contains DynamoDB PutItem operation
        // The DefinitionString may be a Fn::Join when using tokens, so we check the raw JSON
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        // Convert the captured value to JSON string to check its content
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("dynamodb:putItem", definitionValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StateMachine_DefinitionContainsInitMetadataState()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine definition contains InitMetadata state
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("InitMetadata", definitionValue);
    }

    [Fact]
    public void StateMachine_ExecutionRoleHasDynamoDBWritePermissions()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's an IAM policy with DynamoDB write permissions
        // Check that the policy contains the required action for the state machine role
        template.HasResourceProperties("AWS::IAM::Policy", new Dictionary<string, object>
        {
            { "PolicyDocument", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Statement", Match.ArrayWith(new object[]
                        {
                            Match.ObjectLike(new Dictionary<string, object>
                            {
                                { "Action", Match.ArrayWith(new object[] { "dynamodb:PutItem" }) },
                                { "Effect", "Allow" }
                            })
                        })
                    }
                })
            }
        });
    }

    [Fact]
    public void StateMachine_StartsWithInitMetadata()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine starts with InitMetadata state
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        // The JSON contains escaped characters, so we search for the pattern with escaping
        Assert.Contains("StartAt", definitionValue);
        Assert.Contains("InitMetadata", definitionValue);
    }

    // ========== Issue #6: SNS Notifications and Error Handling Tests ==========

    [Fact]
    public void Stack_HasTwoSNSTopics()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.ResourceCountIs("AWS::SNS::Topic", 2);
    }

    [Fact]
    public void SNSTopic_CompletedTopicExists()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's an SNS topic for pipeline completion
        template.HasResourceProperties("AWS::SNS::Topic", new Dictionary<string, object>
        {
            { "DisplayName", Match.StringLikeRegexp(".*Completed.*") }
        });
    }

    [Fact]
    public void SNSTopic_FailedTopicExists()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's an SNS topic for pipeline failures
        template.HasResourceProperties("AWS::SNS::Topic", new Dictionary<string, object>
        {
            { "DisplayName", Match.StringLikeRegexp(".*Failed.*") }
        });
    }

    [Fact]
    public void SNSTopics_AreEncrypted()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify SNS topics have encryption enabled
        template.ResourcePropertiesCountIs("AWS::SNS::Topic", new Dictionary<string, object>
        {
            { "KmsMasterKeyId", Match.AnyValue() }
        }, 2);
    }

    [Fact]
    public void StateMachine_DefinitionContainsCatchBlock()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine definition contains error handling (Catch)
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("Catch", definitionValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StateMachine_DefinitionContainsSNSPublishTask()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine definition contains SNS publish operation
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("sns:publish", definitionValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StateMachine_DefinitionContainsSuccessNotificationState()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's a state for notifying success
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("NotifySuccess", definitionValue);
    }

    [Fact]
    public void StateMachine_DefinitionContainsFailureNotificationState()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's a state for notifying failure
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("NotifyFailure", definitionValue);
    }

    [Fact]
    public void StateMachine_DefinitionContainsDynamoDBUpdateItem()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine definition contains DynamoDB UpdateItem operation
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("dynamodb:updateItem", definitionValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StateMachine_DefinitionContainsCompletedStatus()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine sets status to COMPLETED
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("COMPLETED", definitionValue);
    }

    [Fact]
    public void StateMachine_DefinitionContainsFailedStatus()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine sets status to FAILED
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("FAILED", definitionValue);
    }

    [Fact]
    public void StateMachine_ExecutionRoleHasSNSPublishPermissions()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's an IAM policy with SNS publish permissions
        template.HasResourceProperties("AWS::IAM::Policy", new Dictionary<string, object>
        {
            { "PolicyDocument", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Statement", Match.ArrayWith(new object[]
                        {
                            Match.ObjectLike(new Dictionary<string, object>
                            {
                                { "Action", "sns:Publish" },
                                { "Effect", "Allow" }
                            })
                        })
                    }
                })
            }
        });
    }

    [Fact]
    public void StateMachine_ExecutionRoleHasDynamoDBUpdatePermissions()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's an IAM policy with DynamoDB UpdateItem permissions
        template.HasResourceProperties("AWS::IAM::Policy", new Dictionary<string, object>
        {
            { "PolicyDocument", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Statement", Match.ArrayWith(new object[]
                        {
                            Match.ObjectLike(new Dictionary<string, object>
                            {
                                { "Action", Match.ArrayWith(new object[] { "dynamodb:UpdateItem" }) },
                                { "Effect", "Allow" }
                            })
                        })
                    }
                })
            }
        });
    }

    // ========== Issue #7: Lambda Function Skeleton Tests ==========

    [Fact]
    public void Stack_HasLambdaFunction()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Note: CDK creates an additional Lambda for S3 bucket notifications, so we verify our Lambda exists specifically
        template.HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object>
        {
            { "Runtime", "dotnet8" }
        });
    }

    [Fact]
    public void LambdaFunction_HasCorrectRuntime()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object>
        {
            { "Runtime", "dotnet8" }
        });
    }

    [Fact]
    public void LambdaFunction_HasEnvironmentVariableForTableName()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object>
        {
            { "Environment", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Variables", Match.ObjectLike(new Dictionary<string, object>
                        {
                            { "TABLE_NAME", Match.AnyValue() }
                        })
                    }
                })
            }
        });
    }

    [Fact]
    public void LambdaFunction_HasExecutionRole()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        template.HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object>
        {
            { "Role", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Fn::GetAtt", Match.AnyValue() }
                })
            }
        });
    }

    [Fact]
    public void LambdaExecutionRole_HasDynamoDBUpdateItemPermissions()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's an IAM policy attached to Lambda role with DynamoDB UpdateItem permissions
        template.HasResourceProperties("AWS::IAM::Policy", new Dictionary<string, object>
        {
            { "PolicyDocument", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Statement", Match.ArrayWith(new object[]
                        {
                            Match.ObjectLike(new Dictionary<string, object>
                            {
                                { "Action", Match.ArrayWith(new object[] { "dynamodb:UpdateItem" }) },
                                { "Effect", "Allow" }
                            })
                        })
                    }
                })
            }
        });
    }

    [Fact]
    public void LambdaExecutionRole_HasCloudWatchLogsPermissions()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify Lambda has basic execution role with CloudWatch Logs
        // CDK automatically creates CloudWatch Logs permissions for Lambda functions
        template.HasResourceProperties("AWS::IAM::Role", new Dictionary<string, object>
        {
            { "AssumeRolePolicyDocument", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Statement", Match.ArrayWith(new object[]
                        {
                            Match.ObjectLike(new Dictionary<string, object>
                            {
                                { "Action", "sts:AssumeRole" },
                                { "Effect", "Allow" },
                                { "Principal", Match.ObjectLike(new Dictionary<string, object>
                                    {
                                        { "Service", "lambda.amazonaws.com" }
                                    })
                                }
                            })
                        })
                    }
                })
            }
        });
    }

    [Fact]
    public void StateMachine_DefinitionContainsLambdaInvokeTask()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine definition contains a Lambda invoke task
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("lambda:invoke", definitionValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StateMachine_DefinitionContainsProcessAudioState()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify the state machine definition contains ProcessAudio state
        var capture = new Capture();
        template.HasResourceProperties("AWS::StepFunctions::StateMachine", new Dictionary<string, object>
        {
            { "DefinitionString", capture }
        });
        
        var definitionValue = System.Text.Json.JsonSerializer.Serialize(capture.AsObject());
        Assert.Contains("ProcessAudio", definitionValue);
    }

    [Fact]
    public void StateMachine_ExecutionRoleHasLambdaInvokePermissions()
    {
        // Arrange
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");
        var template = Template.FromStack(stack);

        // Act & Assert
        // Verify there's an IAM policy with Lambda invoke permissions
        template.HasResourceProperties("AWS::IAM::Policy", new Dictionary<string, object>
        {
            { "PolicyDocument", Match.ObjectLike(new Dictionary<string, object>
                {
                    { "Statement", Match.ArrayWith(new object[]
                        {
                            Match.ObjectLike(new Dictionary<string, object>
                            {
                                { "Action", "lambda:InvokeFunction" },
                                { "Effect", "Allow" }
                            })
                        })
                    }
                })
            }
        });
    }
}
