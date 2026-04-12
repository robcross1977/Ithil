using System.Text;
using Ithil.Generated;
using Ithil.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Ithil:Jwt");
var signingKey = jwt["SigningKey"]!;
var issuer = jwt["Issuer"]!;
var audience = jwt["Audience"]!;

builder.Services.AddControllers();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidIssuer = issuer,
            ValidAudience = audience
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient("jsonplaceholder", client =>
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/"));

builder.WebHost.UseUrls("http://localhost:5200");

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapIthilSchema(
    SchemaRegistry.Tools.Select(t => new ToolSchemaResponse(
        t.Name, t.Description, t.AllowWrite, t.MaxResponseTokens,
        t.Category, t.HttpMethod, t.RoutePattern, t.ParameterSources,
        ToolSchemaMapper.BuildInputSchema(t.ParameterSources))));
app.Run();
