using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace MyWorkItem.Api.Infrastructure;

public sealed class SecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var protectedEndpoint = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
        if (protectedEndpoint)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("CookieAuthentication", context.Document)] = []
            });
        }

        if (context.ApiDescription.HttpMethod is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-CSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = false,
                Description = "由 XSRF-TOKEN Cookie 取得；Swagger UI 會自動填入。",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });
        }

        AddResponse(operation, "400", "請求或 CSRF 驗證失敗（ProblemDetails）");
        AddResponse(operation, "429", "請求頻率超過限制（ProblemDetails）");
        AddResponse(operation, "500", "伺服器未預期錯誤（ProblemDetails，不包含 Stack Trace 或 SQL）");
        if (protectedEndpoint)
        {
            AddResponse(operation, "401", "尚未登入或 Access Token 已失效");
            AddResponse(operation, "403", "目前使用者缺少所需 Function");
        }
        if (context.ApiDescription.ParameterDescriptions.Any(description => description.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)))
        {
            AddResponse(operation, "404", "找不到指定資源（ProblemDetails）");
        }
        if (context.ApiDescription.HttpMethod is "POST" or "PUT" or "PATCH")
        {
            AddResponse(operation, "409", "唯一鍵、RowVersion 或其他並行資料衝突（ProblemDetails）");
        }

        var example = context.ApiDescription.RelativePath switch
        {
            "api/v1/auth/login" => """{"loginName":"Admin","password":"DemoPassword123!"}""",
            "api/v1/work-items" when context.ApiDescription.HttpMethod == "POST" =>
                """{"title":"範例 Work Item","description":"範例說明","assignedUserId":null}""",
            var path when path?.StartsWith("api/v1/work-items/{workItemId}", StringComparison.Ordinal) == true &&
                          context.ApiDescription.HttpMethod == "PUT" =>
                """{"title":"更新後標題","description":"更新後說明","assignedUserId":null,"rowVersion":"AAAAAAAAAAE="}""",
            "api/v1/work-items/confirmations/batch" =>
                """{"workItemIds":["11111111-1111-1111-1111-111111111111"]}""",
            "api/v1/users" when context.ApiDescription.HttpMethod == "POST" =>
                """{"loginName":"demo.worker","password":"DemoPassword123!","name":"示範使用者","email":"demo@example.com","remark":null,"roleIds":["11111111-1111-1111-1111-111111111113"]}""",
            _ => null
        };
        if (example is not null && operation.RequestBody?.Content is not null)
        {
            foreach (var mediaType in operation.RequestBody.Content.Values)
            {
                mediaType.Example = JsonNode.Parse(example);
            }
        }
    }

    private static void AddResponse(OpenApiOperation operation, string statusCode, string description)
    {
        operation.Responses ??= new OpenApiResponses();
        if (!operation.Responses.ContainsKey(statusCode))
        {
            operation.Responses.Add(statusCode, new OpenApiResponse { Description = description });
        }
    }
}
