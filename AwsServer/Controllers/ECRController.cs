using Amazon.ECR.Model;
using AwsServer.Configuration;
using AwsServer.ECR;
using Microsoft.AspNetCore.Mvc;

// ReSharper disable InconsistentNaming

namespace AwsServer.Controllers;

[ApiController]
[Route("api/ecr")]
public class ECRController(EcrService ecrService) : ControllerBase
{
    /// <summary>
    /// Initialize ECR service with AWS credentials
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] AwsConfiguration config)
    {
        try
        {
            var success = await ecrService.InitializeAsync(config);
            return Ok(new { success, message = success ? "ECR service initialized successfully" : "Failed to initialize ECR service" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List ECR repositories
    /// </summary>
    [HttpGet("repositories")]
    public async Task<IActionResult> ListRepositories()
    {
        try
        {
            var repositories = await ecrService.ListRepositoriesAsync();
            return Ok(new { success = true, repositoryCount = repositories.Repositories.Count, repositories });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe ECR repositories
    /// </summary>
    [HttpPost("repositories/describe")]
    public async Task<IActionResult> DescribeRepositories([FromBody] List<string>? repositoryNames = null)
    {
        try
        {
            var repositories = await ecrService.DescribeRepositoriesAsync(repositoryNames);
            return Ok(new { success = true, repositoryCount = repositories.Repositories.Count, repositories });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List images in a repository
    /// </summary>
    [HttpGet("repositories/{repositoryName}/images")]
    public async Task<IActionResult> ListImages(string repositoryName)
    {
        try
        {
            var images = await ecrService.ListImagesAsync(repositoryName);
            return Ok(new { success = true, imageCount = images.ImageCount, images });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Describe images in a repository
    /// </summary>
    [HttpPost("repositories/{repositoryName}/images/describe")]
    public async Task<IActionResult> DescribeImages(string repositoryName, [FromBody] List<string>? imageTags = null)
    {
        try
        {
            var imageIds = imageTags?.Select(tag => new ImageIdentifier { ImageTag = tag }).ToList();
            var images = await ecrService.DescribeImagesAsync(repositoryName, imageIds);
            return Ok(new { success = true, imageCount = images.ImageDetails.Count, images });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create ECR repository
    /// </summary>
    [HttpPost("repositories")]
    public async Task<IActionResult> CreateRepository([FromBody] CreateRepositoryRequest request)
    {
        try
        {
            var repository = await ecrService.CreateRepositoryAsync(request.RepositoryName);
            return Ok(new { success = true, repository });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete ECR repository
    /// </summary>
    [HttpDelete("repositories/{repositoryName}")]
    public async Task<IActionResult> DeleteRepository(string repositoryName, [FromQuery] bool force = false)
    {
        try
        {
            await ecrService.DeleteRepositoryAsync(repositoryName, force);
            return Ok(new { success = true, message = "Repository deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Batch delete images
    /// </summary>
    [HttpDelete("repositories/{repositoryName}/images")]
    public async Task<IActionResult> BatchDeleteImages(
        string repositoryName,
        [FromBody] List<string> imageTags)
    {
        try
        {
            var imageIds = imageTags.Select(tag => new ImageIdentifier { ImageTag = tag }).ToList();
            var response = await ecrService.BatchDeleteImageAsync(repositoryName, imageIds);
            return Ok(new { success = true, response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get authorization token for ECR
    /// </summary>
    [HttpGet("authorization-token")]
    public async Task<IActionResult> GetAuthorizationToken()
    {
        try
        {
            var token = await ecrService.GetAuthorizationTokenAsync();
            return Ok(new { success = true, token });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get lifecycle policy for a repository
    /// </summary>
    [HttpGet("repositories/{repositoryName}/lifecycle-policy")]
    public async Task<IActionResult> GetLifecyclePolicy(string repositoryName)
    {
        try
        {
            var policy = await ecrService.GetLifecyclePolicyAsync(repositoryName);
            return Ok(new { success = true, policy });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Put lifecycle policy for a repository
    /// </summary>
    [HttpPut("repositories/{repositoryName}/lifecycle-policy")]
    public async Task<IActionResult> PutLifecyclePolicy(
        string repositoryName,
        [FromBody] PutLifecyclePolicyRequest request)
    {
        try
        {
            var response = await ecrService.PutLifecyclePolicyAsync(repositoryName, request.LifecyclePolicyText);
            return Ok(new { success = true, response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
