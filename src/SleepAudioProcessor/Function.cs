using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SleepAudioProcessor;

public class Function
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly string _tableName;

    /// <summary>
    /// Default constructor for Lambda function - uses environment variables
    /// </summary>
    public Function()
    {
        _dynamoDbClient = new AmazonDynamoDBClient();
        _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") 
            ?? throw new InvalidOperationException("TABLE_NAME environment variable is not set");
    }

    /// <summary>
    /// Constructor for testing with injected dependencies
    /// </summary>
    public Function(IAmazonDynamoDB dynamoDbClient, string tableName)
    {
        _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
    }

    /// <summary>
    /// Helper method to log structured JSON messages
    /// </summary>
    private static void LogStructured(ILambdaContext context, string level, string message, object? data = null)
    {
        var logEntry = new Dictionary<string, object>
        {
            { "timestamp", DateTime.UtcNow.ToString("o") },
            { "level", level },
            { "message", message },
            { "requestId", context.AwsRequestId },
            { "functionName", context.FunctionName },
            { "functionVersion", context.FunctionVersion }
        };

        if (data != null)
        {
            logEntry["data"] = data;
        }

        var logJson = JsonSerializer.Serialize(logEntry);
        context.Logger.LogInformation(logJson);
    }

    /// <summary>
    /// Lambda function handler that processes audio metadata from the state machine.
    /// This is a placeholder for future audio processing, metadata enrichment, or validation logic.
    /// </summary>
    /// <param name="input">Input from Step Functions state machine</param>
    /// <param name="context">Lambda execution context</param>
    /// <returns>Response with success status and metadata</returns>
    public async Task<Dictionary<string, object>> FunctionHandler(Dictionary<string, object> input, ILambdaContext context)
    {
        try
        {
            // Log the input for debugging with structured logging
            LogStructured(context, "INFO", "Processing audio pipeline request", new { inputKeys = input.Keys });
            
            // Extract audioId from metadata (set by InitMetadata state)
            string? audioId = null;
            if (input.TryGetValue("metadata", out var metadataObj))
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    JsonSerializer.Serialize(metadataObj));
                
                if (metadata?.TryGetValue("Item", out var itemObj) == true)
                {
                    var item = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        JsonSerializer.Serialize(itemObj));
                    
                    if (item?.TryGetValue("audioId", out var audioIdObj) == true)
                    {
                        var audioIdValue = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            JsonSerializer.Serialize(audioIdObj));
                        
                        if (audioIdValue?.TryGetValue("S", out var audioIdString) == true)
                        {
                            audioId = audioIdString?.ToString();
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(audioId))
            {
                LogStructured(context, "ERROR", "audioId not found in input", new { inputKeys = input.Keys });
                throw new InvalidOperationException("audioId not found in input metadata");
            }

            LogStructured(context, "INFO", "Processing audio", new { audioId });

            // Future: Perform audio processing, metadata enrichment, or validation
            // For now, just update the DynamoDB record with processing timestamp
            await UpdateMetadataAsync(audioId, context);

            // Return success response
            var response = new Dictionary<string, object>
            {
                { "status", "success" },
                { "audioId", audioId },
                { "message", "Audio processing placeholder executed successfully" },
                { "processedAt", DateTime.UtcNow.ToString("o") }
            };

            LogStructured(context, "INFO", "Processing complete", new { audioId, status = "success" });
            return response;
        }
        catch (Exception ex)
        {
            LogStructured(context, "ERROR", "Error processing audio", new 
            { 
                error = ex.Message, 
                errorType = ex.GetType().Name,
                stackTrace = ex.StackTrace
            });
            
            // Return error response
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "error", ex.Message },
                { "errorType", ex.GetType().Name }
            };
        }
    }

    /// <summary>
    /// Updates the DynamoDB metadata record with processing timestamp
    /// </summary>
    private async Task UpdateMetadataAsync(string audioId, ILambdaContext context)
    {
        try
        {
            var updateRequest = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "audioId", new AttributeValue { S = audioId } }
                },
                UpdateExpression = "SET processedAt = :processedAt",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":processedAt", new AttributeValue { S = DateTime.UtcNow.ToString("o") } }
                }
            };

            await _dynamoDbClient.UpdateItemAsync(updateRequest);
            LogStructured(context, "INFO", "Updated metadata", new { audioId, tableName = _tableName });
        }
        catch (Exception ex)
        {
            LogStructured(context, "ERROR", "Failed to update metadata", new 
            { 
                audioId, 
                tableName = _tableName,
                error = ex.Message 
            });
            throw;
        }
    }
}
