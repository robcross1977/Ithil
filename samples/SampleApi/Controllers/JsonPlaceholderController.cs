using Ithil.Attributes;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace SampleApi.Controllers;

[ApiController]
[Route("api/placeholder")]
public class JsonPlaceholderController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private HttpClient Client => httpClientFactory.CreateClient("jsonplaceholder");

    [HttpGet("posts")]
    [AgentTool("Retrieves a list of posts. Optionally filter by userId using the query parameter.")]
    public async Task<IActionResult> GetPosts() =>
        Ok(await Client.GetFromJsonAsync<object[]>("posts"));

    [HttpGet("posts/{id:int}")]
    [AgentTool("Retrieves a single post by its ID.")]
    public async Task<IActionResult> GetPost(int id) =>
        Ok(await Client.GetFromJsonAsync<object>($"posts/{id}"));

    [HttpGet("comments")]
    [AgentTool("Retrieves comments for a specific post by postId.")]
    public async Task<IActionResult> GetComments([FromQuery] int postId) =>
        Ok(await Client.GetFromJsonAsync<object[]>($"comments?postId={postId}"));

    [HttpPost("posts")]
    [AgentTool("Creates a new post with the given userId, title, and body.")]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
    {
        var response = await Client.PostAsJsonAsync("posts", request);
        return Ok(await response.Content.ReadFromJsonAsync<object>());
    }

    [HttpGet("users")]
    [AgentTool("Retrieves a list of users.")]
    public async Task<IActionResult> GetUsers() =>
        Ok(await Client.GetFromJsonAsync<object[]>("users"));

    [HttpGet("users/{id:int}")]
    [AgentTool("Retrieves a single user by their ID.")]
    public async Task<IActionResult> GetUser(int id) =>
        Ok(await Client.GetFromJsonAsync<object>($"users/{id}"));
}

public record CreatePostRequest(int UserId, string Title, string Body);
