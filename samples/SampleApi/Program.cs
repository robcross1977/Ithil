var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddHttpClient("jsonplaceholder", client =>
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/"));

var app = builder.Build();

app.MapControllers();

app.Run();
