using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Polly;
using Amazon.Polly.Model;
using Moq;
using System.Text.Json;

namespace SleepAudioProcessor.Tests;

public class FunctionTests
{
    // ========== Constructor Tests ==========

    [Fact]
    public void Constructor_WithDependencyInjection_SetsPropertiesCorrectly()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        var tableName = "test-table";
        var inputBucket = "input-bucket";
        var outputBucket = "output-bucket";

        // Act
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            tableName,
            inputBucket,
            outputBucket
        );

        // Assert
        Assert.NotNull(function);
    }

    [Fact]
    public void Constructor_WithNullDynamoDb_ThrowsArgumentNullException()
    {
        // Arrange
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new Function(null!, mockS3.Object, mockPolly.Object, "table", "input", "output"));
    }

    [Fact]
    public void Constructor_WithNullS3_ThrowsArgumentNullException()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockPolly = new Mock<IAmazonPolly>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new Function(mockDynamoDb.Object, null!, mockPolly.Object, "table", "input", "output"));
    }

    [Fact]
    public void Constructor_WithNullPolly_ThrowsArgumentNullException()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new Function(mockDynamoDb.Object, mockS3.Object, null!, "table", "input", "output"));
    }

    [Fact]
    public void Constructor_WithNullTableName_ThrowsArgumentNullException()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new Function(mockDynamoDb.Object, mockS3.Object, mockPolly.Object, null!, "input", "output"));
    }

    // ========== FunctionHandler Success Path Tests ==========

    [Fact]
    public async Task FunctionHandler_WithValidInput_ReturnsSuccessResponse()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "test-audio.mp3");
        var context = new TestLambdaContext();

        // Mock S3 GetObjectMetadata
        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 1024 });

        // Mock Polly SynthesizeSpeech
        var audioStream = new MemoryStream(new byte[] { 0x01, 0x02, 0x03 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ReturnsAsync(new SynthesizeSpeechResponse { AudioStream = audioStream });

        // Mock S3 PutObject
        mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());

        // Mock DynamoDB UpdateItem
        mockDynamoDb.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("success", response["status"].ToString());
        Assert.True(response.ContainsKey("audioId"));
        Assert.Equal("test-audio-123", response["audioId"].ToString());
        Assert.True(response.ContainsKey("outputKey"));
        Assert.True(response.ContainsKey("message"));
    }

    [Fact]
    public async Task FunctionHandler_WithValidInput_CallsS3GetObjectMetadata()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "test-audio.mp3");
        var context = new TestLambdaContext();

        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 1024 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ReturnsAsync(new SynthesizeSpeechResponse { AudioStream = new MemoryStream(new byte[] { 0x01 }) });
        mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());
        mockDynamoDb.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await function.FunctionHandler(input, context);

        // Assert
        mockS3.Verify(s => s.GetObjectMetadataAsync(
            It.Is<GetObjectMetadataRequest>(r => 
                r.BucketName == "input-bucket" && 
                r.Key == "test-audio.mp3"), 
            default), 
            Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_WithValidInput_CallsPollySynthesizeSpeech()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "test-audio.mp3");
        var context = new TestLambdaContext();

        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 1024 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ReturnsAsync(new SynthesizeSpeechResponse { AudioStream = new MemoryStream(new byte[] { 0x01 }) });
        mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());
        mockDynamoDb.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await function.FunctionHandler(input, context);

        // Assert
        mockPolly.Verify(p => p.SynthesizeSpeechAsync(
            It.Is<SynthesizeSpeechRequest>(r => 
                r.VoiceId == VoiceId.Joanna && 
                r.OutputFormat == OutputFormat.Mp3 &&
                r.Engine == Engine.Neural), 
            default), 
            Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_WithValidInput_CallsS3PutObject()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "test-audio.mp3");
        var context = new TestLambdaContext();

        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 1024 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ReturnsAsync(new SynthesizeSpeechResponse { AudioStream = new MemoryStream(new byte[] { 0x01 }) });
        mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());
        mockDynamoDb.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await function.FunctionHandler(input, context);

        // Assert
        mockS3.Verify(s => s.PutObjectAsync(
            It.Is<PutObjectRequest>(r => 
                r.BucketName == "output-bucket" && 
                r.ContentType == "audio/mpeg" &&
                r.Key.Contains("test-audio-123")), 
            default), 
            Times.Once);
    }

    [Fact]
    public async Task FunctionHandler_WithValidInput_CallsDynamoDBUpdateItem()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "test-audio.mp3");
        var context = new TestLambdaContext();

        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 1024 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ReturnsAsync(new SynthesizeSpeechResponse { AudioStream = new MemoryStream(new byte[] { 0x01 }) });
        mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());
        mockDynamoDb.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        await function.FunctionHandler(input, context);

        // Assert
        mockDynamoDb.Verify(d => d.UpdateItemAsync(
            It.Is<UpdateItemRequest>(r => 
                r.TableName == "test-table" && 
                r.Key["audioId"].S == "test-audio-123" &&
                r.ExpressionAttributeValues[":status"].S == "COMPLETED"), 
            default), 
            Times.Once);
    }

    // ========== Error Handling Tests ==========

    [Fact]
    public async Task FunctionHandler_WithMissingAudioId_ReturnsErrorResponse()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = new Dictionary<string, object>
        {
            { "metadata", new { Item = new { } } }
        };
        var context = new TestLambdaContext();

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("error", response["status"].ToString());
        Assert.True(response.ContainsKey("error"));
        Assert.Contains("audioId not found", response["error"].ToString());
    }

    [Fact]
    public async Task FunctionHandler_WithMissingInputBucket_ReturnsErrorResponse()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "", "");
        var context = new TestLambdaContext();

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("error", response["status"].ToString());
        Assert.True(response.ContainsKey("error"));
        Assert.Contains("Input bucket or key not found", response["error"].ToString());
    }

    [Fact]
    public async Task FunctionHandler_WhenS3GetMetadataFails_ReturnsErrorResponse()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "test-audio.mp3");
        var context = new TestLambdaContext();

        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ThrowsAsync(new AmazonS3Exception("Object not found"));

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("error", response["status"].ToString());
        Assert.True(response.ContainsKey("error"));
    }

    [Fact]
    public async Task FunctionHandler_WhenPollySynthesizeFails_ReturnsErrorResponse()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "test-audio.mp3");
        var context = new TestLambdaContext();

        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 1024 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ThrowsAsync(new AmazonPollyException("Polly service error"));

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("error", response["status"].ToString());
        Assert.True(response.ContainsKey("errorType"));
    }

    [Fact]
    public async Task FunctionHandler_WhenS3PutObjectFails_ReturnsErrorResponse()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "test-audio.mp3");
        var context = new TestLambdaContext();

        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 1024 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ReturnsAsync(new SynthesizeSpeechResponse { AudioStream = new MemoryStream(new byte[] { 0x01 }) });
        mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ThrowsAsync(new AmazonS3Exception("Upload failed"));

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("error", response["status"].ToString());
    }

    [Fact]
    public async Task FunctionHandler_WhenDynamoDBUpdateFails_ReturnsErrorResponse()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "test-audio.mp3");
        var context = new TestLambdaContext();

        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 1024 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ReturnsAsync(new SynthesizeSpeechResponse { AudioStream = new MemoryStream(new byte[] { 0x01 }) });
        mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());
        mockDynamoDb.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ThrowsAsync(new AmazonDynamoDBException("Update failed"));

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("error", response["status"].ToString());
    }

    // ========== Edge Case Tests ==========

    [Fact]
    public async Task FunctionHandler_WithEmptyMetadata_ReturnsErrorResponse()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = new Dictionary<string, object>();
        var context = new TestLambdaContext();

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("status"));
        Assert.Equal("error", response["status"].ToString());
    }

    [Fact]
    public async Task FunctionHandler_WithLargeInputFile_HandlesCorrectly()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("test-audio-123", "input-bucket", "large-audio.mp3");
        var context = new TestLambdaContext();

        // Mock large file (100MB)
        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 100 * 1024 * 1024 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ReturnsAsync(new SynthesizeSpeechResponse { AudioStream = new MemoryStream(new byte[1024]) });
        mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());
        mockDynamoDb.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("success", response["status"].ToString());
        Assert.True(response.ContainsKey("inputFileSize"));
        Assert.Equal((long)(100 * 1024 * 1024), response["inputFileSize"]);
    }

    [Fact]
    public async Task FunctionHandler_OutputKeyFormat_IsCorrect()
    {
        // Arrange
        var mockDynamoDb = new Mock<IAmazonDynamoDB>();
        var mockS3 = new Mock<IAmazonS3>();
        var mockPolly = new Mock<IAmazonPolly>();
        
        var function = new Function(
            mockDynamoDb.Object,
            mockS3.Object,
            mockPolly.Object,
            "test-table",
            "input-bucket",
            "output-bucket"
        );

        var input = CreateValidInput("audio-456", "input-bucket", "my-file.mp3");
        var context = new TestLambdaContext();

        mockS3.Setup(s => s.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
            .ReturnsAsync(new GetObjectMetadataResponse { ContentLength = 1024 });
        mockPolly.Setup(p => p.SynthesizeSpeechAsync(It.IsAny<SynthesizeSpeechRequest>(), default))
            .ReturnsAsync(new SynthesizeSpeechResponse { AudioStream = new MemoryStream(new byte[] { 0x01 }) });
        mockS3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(new PutObjectResponse());
        mockDynamoDb.Setup(d => d.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), default))
            .ReturnsAsync(new UpdateItemResponse());

        // Act
        var response = await function.FunctionHandler(input, context);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.ContainsKey("outputKey"));
        var outputKey = response["outputKey"].ToString();
        Assert.Contains("processed/audio-456/my-file-sleep-audio.mp3", outputKey);
    }

    // ========== Helper Methods ==========

    private static Dictionary<string, object> CreateValidInput(string audioId, string bucket, string key)
    {
        return new Dictionary<string, object>
        {
            {
                "metadata", new Dictionary<string, object>
                {
                    {
                        "Item", new Dictionary<string, object>
                        {
                            {
                                "audioId", new Dictionary<string, object>
                                {
                                    { "S", audioId }
                                }
                            },
                            {
                                "inputBucket", new Dictionary<string, object>
                                {
                                    { "S", bucket }
                                }
                            },
                            {
                                "inputKey", new Dictionary<string, object>
                                {
                                    { "S", key }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
