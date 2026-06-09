using Amazon.CDK;
using Amazon.CDK.Pipelines;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace CdkBase
{
    /// <summary>
    /// CDK Pipeline stack for continuous deployment of the Sleep Audio Pipeline application.
    /// This is a skeleton implementation that sets up the basic CI/CD infrastructure.
    /// 
    /// NOTE: This is a placeholder implementation with the following required configurations:
    /// 1. Replace "OWNER/REPO" with your actual GitHub repository
    /// 2. Configure GitHub authentication token in AWS Secrets Manager
    /// 3. Add deployment stages for your target environments (dev, stage, prod)
    /// 
    /// Usage:
    ///   var pipelineStack = new PipelineStack(app, "PipelineStack", new StackProps());
    /// </summary>
    public class PipelineStack : Stack
    {
        public CodePipeline Pipeline { get; }

        public PipelineStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // Create a basic CodePipeline using CDK Pipelines
            // This is a skeleton - in a real implementation, you would:
            // 1. Configure a source (GitHub, CodeCommit, etc.)
            // 2. Add synth step (build and synthesize CDK app)
            // 3. Add deployment stages for different environments
            
            // For now, we create a minimal pipeline structure that can be expanded later
            // Source: Placeholder for GitHub source
            // TODO: Replace "OWNER/REPO" with actual GitHub owner and repository name
            // TODO: Configure GitHub token authentication via AWS Secrets Manager
            var source = CodePipelineSource.GitHub(
                "OWNER/REPO",  // MUST BE REPLACED: Update with actual GitHub repository (e.g., "myorg/myrepo")
                "main",
                new GitHubSourceOptions
                {
                    // TODO: In production, store GitHub token in Secrets Manager and reference here:
                    // Authentication = GitHub.Secret(SecretValue.SecretsManager("github-token"))
                }
            );

            // Synth step: Build and synthesize the CDK application
            var synthStep = new ShellStep("Synth", new ShellStepProps
            {
                Input = source,
                Commands = new[]
                {
                    // Build Lambda function
                    "cd src/SleepAudioProcessor",
                    "dotnet publish -c Release -r linux-x64 --self-contained false",
                    // Note: This copy command is intentionally structured to ensure the Lambda asset
                    // is in the expected location for CDK. The CDK expects the publish folder at
                    // bin/Release/net8.0/publish, but dotnet publish with -r puts it at
                    // bin/Release/net8.0/linux-x64/publish, so we copy it to the expected location.
                    "mkdir -p bin/Release/net8.0/publish",
                    "cp -r bin/Release/net8.0/linux-x64/publish/* bin/Release/net8.0/publish/",
                    "cd ../..",
                    // Restore and test CDK project
                    "dotnet restore src/CdkBase.sln",
                    "dotnet test src/CdkBase.sln",
                    // Synthesize CDK app
                    "npx cdk synth"
                },
                PrimaryOutputDirectory = "cdk.out"
            });

            // Create the pipeline
            Pipeline = new CodePipeline(this, "Pipeline", new CodePipelineProps
            {
                PipelineName = "SleepAudioPipeline",
                Synth = synthStep,
                // Self-mutation: Pipeline can update itself
                SelfMutation = true,
                // Cross-account keys for multi-account deployments (optional)
                CrossAccountKeys = false
            });

            // Future: Add deployment stages
            // var devStage = new PipelineStage(this, "Dev", new StageProps { ... });
            // Pipeline.AddStage(devStage);
            // 
            // var prodStage = new PipelineStage(this, "Prod", new StageProps { ... });
            // Pipeline.AddStage(prodStage, new AddStageOpts {
            //     Pre = new[] { new ManualApprovalStep("PromoteToProd") }
            // });
        }
    }
}
