FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props NuGet.Config MyWorkItem.Backend.sln ./
COPY src/MyWorkItem.Domain/MyWorkItem.Domain.csproj src/MyWorkItem.Domain/
COPY src/MyWorkItem.Application/MyWorkItem.Application.csproj src/MyWorkItem.Application/
COPY src/MyWorkItem.Infrastructure/MyWorkItem.Infrastructure.csproj src/MyWorkItem.Infrastructure/
COPY src/MyWorkItem.DatabaseMigrator/MyWorkItem.DatabaseMigrator.csproj src/MyWorkItem.DatabaseMigrator/
COPY src/MyWorkItem.Api/MyWorkItem.Api.csproj src/MyWorkItem.Api/
COPY tests/MyWorkItem.UnitTests/MyWorkItem.UnitTests.csproj tests/MyWorkItem.UnitTests/
COPY tests/MyWorkItem.IntegrationTests/MyWorkItem.IntegrationTests.csproj tests/MyWorkItem.IntegrationTests/
RUN dotnet restore MyWorkItem.Backend.sln
COPY . .
RUN dotnet publish src/MyWorkItem.Api/MyWorkItem.Api.csproj -c Release --no-restore -o /app/api
RUN dotnet publish src/MyWorkItem.DatabaseMigrator/MyWorkItem.DatabaseMigrator.csproj -c Release --no-restore -o /app/migrator

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /app/api .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyWorkItem.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrator
WORKDIR /app
COPY --from=build /app/migrator .
USER $APP_UID
ENTRYPOINT ["dotnet", "MyWorkItem.DatabaseMigrator.dll"]
