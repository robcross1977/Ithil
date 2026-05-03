using Ithil.Generated;
using Ithil.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHttpClient("jsonplaceholder", client =>
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/"));

var app = builder.Build();

app.MapControllers();
app.MapIthilSchema(
    SchemaRegistry.Tools.Select(t => new ToolSchemaResponse(
        t.Name, t.Description, t.AllowWrite, t.MaxResponseTokens,
        t.Category, t.RequiredScopes, t.HttpMethod, t.RoutePattern,
        t.ParameterSources,
        ToolSchemaMapper.BuildInputSchema(t.ParameterSources, t.ParameterTypes))));

app.Run();
