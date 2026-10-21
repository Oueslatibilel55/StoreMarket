# The API image for Render (build context: backend/). Same layout as RichLife's.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first so `restore` is cached while only code changes.
COPY ["Directory.Build.props", "global.json", ".editorconfig", "./"]
COPY ["src/WebApp.Api/WebApp.Api.csproj",                       "src/WebApp.Api/"]
COPY ["src/WebApp.Application/WebApp.Application.csproj",       "src/WebApp.Application/"]
COPY ["src/WebApp.Domain/WebApp.Domain.csproj",                 "src/WebApp.Domain/"]
COPY ["src/WebApp.Infrastructure/WebApp.Infrastructure.csproj", "src/WebApp.Infrastructure/"]
COPY ["src/WebApp.ServiceDefaults/WebApp.ServiceDefaults.csproj", "src/WebApp.ServiceDefaults/"]
RUN dotnet restore "src/WebApp.Api/WebApp.Api.csproj"

COPY src/ src/
WORKDIR /src/src/WebApp.Api
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Render routes traffic to 8080 (the .NET image's default); images go to the database (Storage__Provider=Database).
ENTRYPOINT ["dotnet", "WebApp.Api.dll"]
