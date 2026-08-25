FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props Directory.Packages.props .editorconfig ./
COPY src ./src
RUN dotnet restore src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj
RUN dotnet publish src/PriceNegotiationApp.Api/PriceNegotiationApp.Api.csproj -c Release -f net10.0 -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
USER app
ENTRYPOINT ["dotnet", "PriceNegotiationApp.Api.dll"]

