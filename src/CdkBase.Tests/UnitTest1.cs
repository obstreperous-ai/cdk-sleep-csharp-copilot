using Amazon.CDK;
using Amazon.CDK.Assertions;

namespace CdkBase.Tests;

public class UnitTest1
{
    [Fact]
    public void StackSynthesis_StartsWithoutS3Buckets()
    {
        var app = new App();
        var stack = new CdkBaseStack(app, "TestStack");

        var template = Template.FromStack(stack);

        template.ResourceCountIs("AWS::S3::Bucket", 0);
    }
}
