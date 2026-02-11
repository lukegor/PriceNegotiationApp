using Microsoft.OpenApi;

namespace PriceNegotiationApp.Api.Extensions
{
    public static class OpenApiServiceExtensions
    {
        internal static void ConfigureOpenApi(this IServiceCollection services)
        {
            services.AddOpenApi("v1", options =>
            {
                // 1. INFO & CONTACT
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

                // 2. AUTHENTICATION (JWT Bearer) - Poprawka dla Microsoft.OpenApi v2.0
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();

                    // A. Definicja Schematu w Components (To jest "Co to jest")
                    // Używamy pełnego obiektu OpenApiSecurityScheme
                    document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header,
                        Description = "Wpisz poniżej TYLKO swój token (bez prefiksu 'Bearer')."
                    });

                    // B. Wymaganie Globalne (To jest "Użyj tego")
                    // W wersji 2.0 kluczem słownika musi być OpenApiSecuritySchemeReference
                    document.Security ??= new List<OpenApiSecurityRequirement>();

                    var requirement = new OpenApiSecurityRequirement
                    {
                        {
                            // POPRAWKA TUTAJ: Używamy konstruktora, a nie inicjalizatora właściwości
                            // Argument 1: "Bearer" (ID schematu)
                            // Argument 2: document (Instancja dokumentu do rozwiązania referencji)
                            new OpenApiSecuritySchemeReference("Bearer", document),

                            new List<string>()
                        }
                    };

                    document.Security.Add(requirement);

                    return Task.CompletedTask;
                });

                // 3. CLEAN SCHEMA NAMES - Czyści nazwy klas z namespace'ów
                options.AddSchemaTransformer((schema, context, cancellationToken) =>
                {
                    if (context.JsonTypeInfo.Type.IsClass && !context.JsonTypeInfo.Type.IsAbstract)
                    {
                        schema.Title = context.JsonTypeInfo.Type.Name;
                    }
                    return Task.CompletedTask;
                });
            });
        }
    }
}