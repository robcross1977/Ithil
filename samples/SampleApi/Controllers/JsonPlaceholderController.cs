using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace SampleApi.Controllers;

[ApiController]
[Route("api/placeholder")]
public class JsonPlaceholderController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private HttpClient Client => httpClientFactory.CreateClient("jsonplaceholder");

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts() =>
        Ok(await Client.GetFromJsonAsync<object[]>("posts"));

    [HttpGet("posts/{id:int}")]
    public async Task<IActionResult> GetPost(int id) =>
        Ok(await Client.GetFromJsonAsync<object>($"posts/{id}"));

    [HttpGet("comments")]
    public async Task<IActionResult> GetComments([FromQuery] int postId) =>
        Ok(await Client.GetFromJsonAsync<object[]>($"comments?postId={postId}"));

    [HttpPost("posts")]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
    {
        var response = await Client.PostAsJsonAsync("posts", request);
        return Ok(await response.Content.ReadFromJsonAsync<object>());
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() =>
        Ok(await Client.GetFromJsonAsync<object[]>("users"));

    [HttpGet("users/{id:int}")]
    public async Task<IActionResult> GetUser(int id) =>
        Ok(await Client.GetFromJsonAsync<object>($"users/{id}"));
}

public record CreatePostRequest(int UserId, string Title, string Body);
