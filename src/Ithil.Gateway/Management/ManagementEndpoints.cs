using Ithil.Core.Interfaces;
using Ithil.Management.Models;
using Ithil.Management.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ithil.Gateway.Management;

/// <summary>
/// Maps all /management/* endpoints. Every route requires the ManagementPolicy
/// (authenticated user with scope: admin).
/// </summary>
public static class ManagementEndpoints
{
    public static IEndpointRouteBuilder MapManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/management")
            .RequireAuthorization(ManagementAuthPolicy.PolicyName);

        group.MapGet("/agents", async (IAgentManagementService svc) =>
            (await svc.GetAllAsync()).Match(
                Right: agents => Results.Ok(agents),
                Left: ToHttpError));

        group.MapGet("/agents/{id}", async (string id, IAgentManagementService svc) =>
            (await svc.GetAsync(id)).Match(
                Right: agent => Results.Ok(agent),
                Left: ToHttpError));

        group.MapPost("/agents", async (CreateAgentRequest request, IAgentManagementService svc) =>
            (await svc.CreateAsync(request)).Match(
                Right: created => Results.Created($"/management/agents/{created.AgentId}", created),
                Left: ToHttpError));

        group.MapPut("/agents/{id}", async (string id, UpdateAgentRequest request, IAgentManagementService svc) =>
            (await svc.UpdateAsync(id, request)).Match(
                Right: agent => Results.Ok(agent),
                Left: ToHttpError));

        group.MapDelete("/agents/{id}", async (string id, IAgentManagementService svc) =>
            (await svc.DeleteAsync(id)).Match(
                Right: _ => Results.NoContent(),
                Left: ToHttpError));

        group.MapGet("/agents/{id}/budget", async (string id, IBudgetQueryService svc) =>
            (await svc.GetStatusAsync(id)).Match(
                Right: status => Results.Ok(status),
                Left: ToHttpError));

        group.MapDelete("/agents/{id}/budget", async (
            string id, IBudgetQueryService svc, HttpContext ctx,
            [FromServices] ILogger logger) =>
        {
            var operatorId = ctx.User.FindFirst("sub")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (operatorId is null)
                logger.LogWarning("Budget reset for agent {AgentId} has no operator identity — sub claim absent from token.", id);
            return (await svc.ResetAsync(id, operatorId)).Match(
                Right: _ => Results.NoContent(),
                Left: ToHttpError);
        });

        group.MapGet("/tools", async (IToolRegistry registry) =>
            Results.Ok(await registry.GetToolsAsync()));

        return app;
    }

    private static IResult ToHttpError(ManagementError error) => error switch
    {
        ManagementError.NotFound e => Results.NotFound(new { message = $"Agent '{e.AgentId}' not found." }),
        ManagementError.Invalid e  => Results.BadRequest(new { message = e.Reason }),
        _                          => Results.StatusCode(500),
    };
}
