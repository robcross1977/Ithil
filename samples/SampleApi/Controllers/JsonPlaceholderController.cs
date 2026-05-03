using Ithil.Attributes;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace SampleApi.Controllers;

/// <summary>
/// Demo endpoints that proxy JSONPlaceholder — exposes posts, users, and comments
/// as agent-callable tools. Ithil handles authentication at the gateway layer;
/// this API trusts all traffic that reaches it.
/// </summary>
[ApiController]
[Route("api/placeholder")]
public class JsonPlaceholderController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private HttpClient Client => httpClientFactory.CreateClient("jsonplaceholder");

    /// <summary>Returns all posts.</summary>
    [AgentTool("Returns all posts from JSONPlaceholder", Category = "Placeholder")]
    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts() =>
        Ok(await Client.GetFromJsonAsync<object[]>("posts"));

    /// <summary>Returns a single post by ID.</summary>
    [AgentTool("Returns a single post by ID from JSONPlaceholder", Category = "Placeholder")]
    [HttpGet("posts/{id:int}")]
    public async Task<IActionResult> GetPost(int id) =>
        Ok(await Client.GetFromJsonAsync<object>($"posts/{id}"));

    /// <summary>Returns comments for a specific post.</summary>
    [AgentTool(
        "Returns all comments for a given post from JSONPlaceholder",
        Category = "Placeholder"
    )]
    [HttpGet("comments")]
    public async Task<IActionResult> GetComments([FromQuery] int postId) =>
        Ok(await Client.GetFromJsonAsync<object[]>($"comments?postId={postId}"));

    /// <summary>Creates a new post (simulated — JSONPlaceholder does not persist).</summary>
    [AgentTool(
        "Creates a new post in JSONPlaceholder",
        AllowWrite = true,
        Category = "Placeholder"
    )]
    [HttpPost("posts")]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
    {
        var response = await Client.PostAsJsonAsync("posts", request);
        return Ok(await response.Content.ReadFromJsonAsync<object>());
    }

    /// <summary>Returns all users.</summary>
    [AgentTool("Returns all users from JSONPlaceholder", Category = "Placeholder")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() =>
        Ok(await Client.GetFromJsonAsync<object[]>("users"));

    /// <summary>Returns a single user by ID.</summary>
    [AgentTool("Returns a single user by ID from JSONPlaceholder", Category = "Placeholder")]
    [HttpGet("users/{id:int}")]
    public async Task<IActionResult> GetUser(int id) =>
        Ok(await Client.GetFromJsonAsync<object>($"users/{id}"));
}

/// <summary>Request body for creating a post.</summary>
public record CreatePostRequest(int UserId, string Title, string Body);
