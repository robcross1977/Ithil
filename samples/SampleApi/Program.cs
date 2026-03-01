var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.WebHost.UseUrls("http://localhost:5200");

var app = builder.Build();
app.MapControllers();
app.Run();
