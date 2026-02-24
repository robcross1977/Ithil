using Ithil.Gateway;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIthilServices(builder.Configuration);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
