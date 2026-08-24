FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src ./src
RUN dotnet restore src/PriceNegotiationApp.AppHost/PriceNegotiationApp.AppHost.csproj
RUN dotnet publish src/PriceNegotiationApp.AppHost/PriceNegotiationApp.AppHost.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
USER app
HEALTHCHECK --interval=30s --timeout=5s CMD ["/usr/bin/wget", "-qO-", "http://localhost:8080/health/live"]
ENTRYPOINT ["dotnet", "PriceNegotiationApp.AppHost.dll"]

