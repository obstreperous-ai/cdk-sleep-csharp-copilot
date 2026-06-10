using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Polly;
using Amazon.Polly.Model;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SleepAudioProcessor;

public class Function
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IAmazonS3 _s3Client;
    private readonly IAmazonPolly _pollyClient;
    private readonly string _tableName;
    private readonly string _inputBucketName;
    private readonly string _outputBucketName;

    /// <summary>
    /// Default constructor for Lambda function - uses environment variables
    /// </summary>
    public Function()
    {
        _dynamoDbClient = new AmazonDynamoDBClient();
        _s3Client = new AmazonS3Client();
        _pollyClient = new AmazonPollyClient();
        _tableName = Environment.GetEnvironmentVariable("TABLE_NAME") 
            ?? throw new InvalidOperationException("TABLE_NAME environment variable is not set");
        _inputBucketName = Environment.GetEnvironmentVariable("INPUT_BUCKET_NAME")
            ?? throw new InvalidOperationException("INPUT_BUCKET_NAME environment variable is not set");
        _outputBucketName = Environment.GetEnvironmentVariable("OUTPUT_BUCKET_NAME")
            ?? throw new InvalidOperationException("OUTPUT_BUCKET_NAME environment variable is not set");
    }

    /// <summary>
    /// Constructor for testing with injected dependencies
    /// </summary>
    public Function(IAmazonDynamoDB dynamoDbClient, IAmazonS3 s3Client, IAmazonPolly pollyClient, 
        string tableName, string inputBucketName, string outputBucketName)
    {
        _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _pollyClient = pollyClient ?? throw new ArgumentNullException(nameof(pollyClient));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _inputBucketName = inputBucketName ?? throw new ArgumentNullException(nameof(inputBucketName));
        _outputBucketName = outputBucketName ?? throw new ArgumentNullException(nameof(outputBucketName));
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
    /// Lambda function handler that processes audio files from input bucket, generates/enhances sleep audio,
    /// and stores results in output bucket.
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
            string? inputBucket = null;
            string? inputKey = null;
            
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
                    
                    // Extract input bucket and key from metadata
                    if (item?.TryGetValue("inputBucket", out var bucketObj) == true)
                    {
                        var bucketValue = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            JsonSerializer.Serialize(bucketObj));
                        if (bucketValue?.TryGetValue("S", out var bucketString) == true)
                        {
                            inputBucket = bucketString?.ToString();
                        }
                    }
                    
                    if (item?.TryGetValue("inputKey", out var keyObj) == true)
                    {
                        var keyValue = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            JsonSerializer.Serialize(keyObj));
                        if (keyValue?.TryGetValue("S", out var keyString) == true)
                        {
                            inputKey = keyString?.ToString();
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(audioId))
            {
                LogStructured(context, "ERROR", "audioId not found in input", new { inputKeys = input.Keys });
                throw new InvalidOperationException("audioId not found in input metadata");
            }
            
            if (string.IsNullOrEmpty(inputBucket) || string.IsNullOrEmpty(inputKey))
            {
                LogStructured(context, "ERROR", "Input bucket or key not found", new { audioId, inputBucket, inputKey });
                throw new InvalidOperationException("Input bucket or key not found in metadata");
            }

            LogStructured(context, "INFO", "Processing audio", new { audioId, inputBucket, inputKey });

            // Step 1: Download input file from S3
            var inputFileSize = await GetInputFileSizeAsync(inputBucket, inputKey, context);
            LogStructured(context, "INFO", "Input file metadata retrieved", new { audioId, inputBucket, inputKey, inputFileSize });

            // Step 2: Generate sleep audio using Polly
            var outputKey = $"processed/{audioId}/{Path.GetFileNameWithoutExtension(inputKey)}-sleep-audio.mp3";
            var sleepText = "Welcome to your sleep relaxation session. Close your eyes and take a deep breath. " +
                          "Let all tension melt away as you drift into peaceful, restful sleep. " +
                          "Feel your body becoming heavier with each breath. You are safe, comfortable, and deeply relaxed.";
            
            var audioStream = await GenerateSleepAudioAsync(sleepText, context);
            LogStructured(context, "INFO", "Sleep audio generated with Polly", new { audioId, textLength = sleepText.Length });

            // Step 3: Upload processed audio to Output S3 bucket
            var outputFileSize = await UploadToS3Async(outputKey, audioStream, context);
            LogStructured(context, "INFO", "Processed audio uploaded to output bucket", 
                new { audioId, outputBucket = _outputBucketName, outputKey, outputFileSize });

            // Step 4: Update DynamoDB with output location and metadata
            await UpdateMetadataWithOutputAsync(audioId, outputKey, outputFileSize, context);

            // Return success response with output metadata
            var response = new Dictionary<string, object>
            {
                { "status", "success" },
                { "audioId", audioId },
                { "inputBucket", inputBucket },
                { "inputKey", inputKey },
                { "outputBucket", _outputBucketName },
                { "outputKey", outputKey },
                { "inputFileSize", inputFileSize },
                { "outputFileSize", outputFileSize },
                { "message", "Audio processing completed successfully" },
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
    /// Gets the size of the input file from S3
    /// </summary>
    private async Task<long> GetInputFileSizeAsync(string bucket, string key, ILambdaContext context)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = key
            };
            
            var response = await _s3Client.GetObjectMetadataAsync(request);
            return response.ContentLength;
        }
        catch (Exception ex)
        {
            LogStructured(context, "ERROR", "Failed to get input file size", new { bucket, key, error = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// Generates sleep audio using Amazon Polly
    /// </summary>
    private async Task<Stream> GenerateSleepAudioAsync(string text, ILambdaContext context)
    {
        try
        {
            var request = new SynthesizeSpeechRequest
            {
                Text = text,
                VoiceId = VoiceId.Joanna,
                OutputFormat = OutputFormat.Mp3,
                Engine = Engine.Neural // Use neural engine for more natural sounding voice
            };
            
            var response = await _pollyClient.SynthesizeSpeechAsync(request);
            
            // Copy the stream to a MemoryStream so we can reuse it
            var memoryStream = new MemoryStream();
            await response.AudioStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            
            return memoryStream;
        }
        catch (Exception ex)
        {
            LogStructured(context, "ERROR", "Failed to generate sleep audio with Polly", new { error = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// Uploads processed audio to Output S3 bucket
    /// </summary>
    private async Task<long> UploadToS3Async(string key, Stream audioStream, ILambdaContext context)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _outputBucketName,
                Key = key,
                InputStream = audioStream,
                ContentType = "audio/mpeg",
                Metadata =
                {
                    ["x-amz-meta-generated-by"] = "SleepAudioProcessor",
                    ["x-amz-meta-generated-at"] = DateTime.UtcNow.ToString("o")
                }
            };
            
            var response = await _s3Client.PutObjectAsync(request);
            
            // Get the size of the uploaded file
            audioStream.Position = 0;
            return audioStream.Length;
        }
        catch (Exception ex)
        {
            LogStructured(context, "ERROR", "Failed to upload to S3", new { bucket = _outputBucketName, key, error = ex.Message });
            throw;
        }
    }

    /// <summary>
    /// Updates the DynamoDB metadata record with output location and processing completion
    /// </summary>
    private async Task UpdateMetadataWithOutputAsync(string audioId, string outputKey, long outputFileSize, ILambdaContext context)
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
                UpdateExpression = "SET processedAt = :processedAt, outputKey = :outputKey, outputBucket = :outputBucket, outputFileSize = :outputFileSize, #status = :status",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#status", "status" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":processedAt", new AttributeValue { S = DateTime.UtcNow.ToString("o") } },
                    { ":outputKey", new AttributeValue { S = outputKey } },
                    { ":outputBucket", new AttributeValue { S = _outputBucketName } },
                    { ":outputFileSize", new AttributeValue { N = outputFileSize.ToString() } },
                    { ":status", new AttributeValue { S = "COMPLETED" } }
                }
            };

            await _dynamoDbClient.UpdateItemAsync(updateRequest);
            LogStructured(context, "INFO", "Updated metadata with output information", 
                new { audioId, outputKey, outputBucket = _outputBucketName, outputFileSize });
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
