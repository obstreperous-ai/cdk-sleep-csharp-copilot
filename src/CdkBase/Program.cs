using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CdkBase
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            
            // Get environment from context (defaults to "dev")
            var environmentName = app.Node.TryGetContext("environment")?.ToString() ?? "dev";
            
            // Get account and region from environment variables or leave unset for environment-agnostic
            var account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT");
            var region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION");
            
            // Create environment-specific stack props
            var stackProps = new StackProps();
            
            // Only set Env if both account and region are available
            if (!string.IsNullOrEmpty(account) && !string.IsNullOrEmpty(region))
            {
                stackProps.Env = new Amazon.CDK.Environment
                {
                    Account = account,
                    Region = region
                };
            }
            
            // Create the application stack with environment-specific naming
            new CdkBaseStack(app, $"CdkBaseStack-{environmentName}", stackProps, environmentName);
            
            app.Synth();
        }
    }
}
