using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PriceNegotiationApp.Api.Extensions
{
    public static class OpenApiServiceExtensions
    {
        internal static void ConfigureOpenApi(this IServiceCollection services)
        {
            services.AddOpenApi("v1", options =>
            {
                options.AddDocumentInfo();

                options.AddJwtBearerAuthenticationRequirement();

                options.AddGlobalResponsesFromExceptionHandler();
            });
        }

        extension(OpenApiOptions options)
        {
            /// <summary>
            /// Adds general project info, specifies license and adds author's personal contact info
            /// </summary>
            private OpenApiOptions AddDocumentInfo()
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info = new OpenApiInfo
                    {
                        Title = "Price Negotiation App",
                        Version = "v1",
                        Description = "API do negocjacji cen produktów.",
                        Contact = new OpenApiContact
                        {
                            Name = "Łukasz Górski",
                            Url = new Uri("https://www.linkedin.com/in/lukasz-gorski-lukegor/")
                        },
                        License = new OpenApiLicense
                        {
                            Name = "Apache License 2.0",
                            Url = new Uri("https://opensource.org/license/apache-2-0/")
                        }
                    };
                    return Task.CompletedTask;
                });

                return options;
            }

            /// <summary>
            /// Adds Bearer Authentication using JWT format.
            /// </summary>
            private OpenApiOptions AddJwtBearerAuthenticationRequirement()
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();

                    var scheme = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description = "Wpisz poniżej TYLKO swój token (bez prefiksu 'Bearer')."
                    };

                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes.Add("Bearer", scheme);

                    return Task.CompletedTask;
                });

                return options;
            }

            private OpenApiOptions AddGlobalResponsesFromExceptionHandler()
            {
                options.AddOperationTransformer((operation, context, cancellationToken) =>
                {
                    var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                    var authorizeAttributes = metadata.OfType<AuthorizeAttribute>().ToList();
                    var allowAnonymous = metadata.OfType<AllowAnonymousAttribute>().Any();
                    bool isSecured = !allowAnonymous && authorizeAttributes.Any();

                    var hasRouteParameters = ContainsRouteParameters(context.Description);

                    var inlineProblemDetailsSchema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Title = "ProblemDetails",
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["type"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                            ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    };

                    var errorResponses = new Dictionary<string, string>();

                    errorResponses.Add("500", "Internal Server Error");
                    errorResponses.Add("400", "Bad Request");

                    if (isSecured)
                    {
                        errorResponses.TryAdd("401", "Unauthorized");

                        var hasSpecificRequirements = authorizeAttributes.Any(a =>
                            !string.IsNullOrEmpty(a.Roles) ||
                            !string.IsNullOrEmpty(a.Policy));

                        // 'true' due to resource-based auth
                        // in this project, if authentication is required, it must be fully secure
                        if (hasSpecificRequirements || true)
                        {
                            errorResponses.TryAdd("403", "Forbidden");
                        }

                        operation.Security = new List<OpenApiSecurityRequirement>
                        {
                            new OpenApiSecurityRequirement
                            {
                                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = new List<string>()
                            }
                        };
                    }

                    if (hasRouteParameters)
                    {
                        errorResponses.Add("404", "Not Found");
                    }

                    foreach (var (code, description) in errorResponses)
                    {
                        if (!operation.Responses.ContainsKey(code))
                        {
                            operation.Responses.Add(code, new OpenApiResponse
                            {
                                Description = description,
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/problem+json"] = new OpenApiMediaType
                                    {
                                        Schema = inlineProblemDetailsSchema
                                    }
                                }
                            });
                        }
                    }

                    return Task.CompletedTask;
                });

                return options;
            }
        }

        /// <summary>
        /// Determines if endpoint has route parameters (e.g. "api/negotiations/{id}").
        /// </summary>
        private static bool ContainsRouteParameters(ApiDescription endpointMetadata)
        {
            var routeTemplate = endpointMetadata.RelativePath;
            return routeTemplate?.Contains('{') ?? false;
        }
    }
}