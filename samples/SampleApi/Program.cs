using Ithil.Generated;
using Ithil.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.WebHost.UseUrls("http://localhost:5200");

var app = builder.Build();
app.MapControllers();
app.MapIthilSchema(
    SchemaRegistry.Tools.Select(t => new ToolSchemaResponse(
        t.Name, t.Description, t.AllowWrite, t.MaxResponseTokens,
        t.Category, t.HttpMethod, t.RoutePattern, t.ParameterSources,
        ToolSchemaMapper.BuildInputSchema(t.ParameterSources))));
app.Run();
