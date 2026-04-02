using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SalesLead.Api.Swagger;

/// <summary>Exposes auth headers in OpenAPI so Swagger UI shows input fields (middleware still enforces them).</summary>
public sealed class SwaggerHeaderOperationFilter : IOperationFilter
{
    /// <summary>
    /// Adds expected auth header parameters to OpenAPI operations for ingest and tenant routes.
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? "";
        if (path.StartsWith("api/v1/ingest", StringComparison.OrdinalIgnoreCase))
        {
            AddHeader(operation, "X-Api-Key", required: true,
                "Tenant API key (seed: sk_demo_tenant_a_essential / sk_demo_tenant_b_professional).");
            return;
        }

        if (path.StartsWith("api/v1/tenants/", StringComparison.OrdinalIgnoreCase))
        {
            AddHeader(operation, "X-User-Id", required: true,
                "Sales user GUID that belongs to the route tenant (seed User A / User B).");
        }
    }

    /// <summary>
    /// Appends a header parameter to the operation when not already defined.
    /// </summary>
    private static void AddHeader(OpenApiOperation operation, string name, bool required, string? description)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        if (operation.Parameters.Any(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) &&
                p.In == ParameterLocation.Header))
            return;

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = required,
            Description = description,
            Schema = new OpenApiSchema { Type = "string" },
        });
    }
}
