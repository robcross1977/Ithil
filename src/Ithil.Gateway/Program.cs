using Ithil.Gateway;
using Ithil.Gateway.Transforms;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIthilServices(builder.Configuration);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        context.AddRequestTransform(async transformContext =>
        {
            var pipeline = transformContext.HttpContext.RequestServices
                .GetRequiredService<RequestTransformPipeline>();

            await pipeline.TransformAsync(transformContext.HttpContext);
        });
    });
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpsRedirection();
app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
