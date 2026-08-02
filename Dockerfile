FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore MyWorkItem.Backend.sln
RUN dotnet publish src/MyWorkItem.Api/MyWorkItem.Api.csproj -c Release --no-restore -o /out/api
RUN dotnet publish src/MyWorkItem.DatabaseMigrator/MyWorkItem.DatabaseMigrator.csproj -c Release --no-restore -o /out/migrator

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/api ./api
COPY --from=build /out/migrator ./migrator
WORKDIR /app/api
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyWorkItem.Api.dll"]
