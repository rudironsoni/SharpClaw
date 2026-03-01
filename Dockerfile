FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "nuget.config", "SharpClaw.slnx", "global.json", "./"]
COPY ["src/SharpClaw.Execution.SandboxManager/SharpClaw.Execution.SandboxManager.csproj", "src/SharpClaw.Execution.SandboxManager/"]
COPY ["src/SharpClaw.OpenResponses.HttpApi/SharpClaw.OpenResponses.HttpApi.csproj", "src/SharpClaw.OpenResponses.HttpApi/"]
COPY ["src/SharpClaw.Persistence.Contracts/SharpClaw.Persistence.Contracts.csproj", "src/SharpClaw.Persistence.Contracts/"]
COPY ["src/SharpClaw.Tenancy/SharpClaw.Tenancy.csproj", "src/SharpClaw.Tenancy/"]
COPY ["src/SharpClaw.Abstractions/SharpClaw.Abstractions.csproj", "src/SharpClaw.Abstractions/"]
COPY ["src/SharpClaw.Persistence.Sqlite/SharpClaw.Persistence.Sqlite.csproj", "src/SharpClaw.Persistence.Sqlite/"]
COPY ["src/SharpClaw.Identity/SharpClaw.Identity.csproj", "src/SharpClaw.Identity/"]
COPY ["src/SharpClaw.Conversations/SharpClaw.Conversations.csproj", "src/SharpClaw.Conversations/"]
COPY ["src/SharpClaw.Persistence.Core/SharpClaw.Persistence.Core.csproj", "src/SharpClaw.Persistence.Core/"]
COPY ["src/SharpClaw.Extensions.Hosting/SharpClaw.Extensions.Hosting.csproj", "src/SharpClaw.Extensions.Hosting/"]
COPY ["src/SharpClaw.Configuration/SharpClaw.Configuration.csproj", "src/SharpClaw.Configuration/"]
COPY ["src/SharpClaw.RateLimiting/SharpClaw.RateLimiting.csproj", "src/SharpClaw.RateLimiting/"]
COPY ["src/SharpClaw.Operations/SharpClaw.Operations.csproj", "src/SharpClaw.Operations/"]
COPY ["src/SharpClaw.Persistence.Abstractions/SharpClaw.Persistence.Abstractions.csproj", "src/SharpClaw.Persistence.Abstractions/"]
COPY ["src/SharpClaw.Gateway/SharpClaw.Gateway.csproj", "src/SharpClaw.Gateway/"]
COPY ["src/SharpClaw.Protocol.Contracts/SharpClaw.Protocol.Contracts.csproj", "src/SharpClaw.Protocol.Contracts/"]
COPY ["src/SharpClaw.Execution.Kubernetes/SharpClaw.Execution.Kubernetes.csproj", "src/SharpClaw.Execution.Kubernetes/"]
COPY ["src/SharpClaw.HttpApi/SharpClaw.HttpApi.csproj", "src/SharpClaw.HttpApi/"]
COPY ["src/SharpClaw.Protocol.Abstractions/SharpClaw.Protocol.Abstractions.csproj", "src/SharpClaw.Protocol.Abstractions/"]
COPY ["src/SharpClaw.Cloud.Azure/SharpClaw.Cloud.Azure.csproj", "src/SharpClaw.Cloud.Azure/"]
COPY ["src/SharpClaw.Approvals/SharpClaw.Approvals.csproj", "src/SharpClaw.Approvals/"]
COPY ["src/SharpClaw.Execution.Daytona/SharpClaw.Execution.Daytona.csproj", "src/SharpClaw.Execution.Daytona/"]
COPY ["src/SharpClaw.Persistence.Postgres/SharpClaw.Persistence.Postgres.csproj", "src/SharpClaw.Persistence.Postgres/"]
COPY ["src/SharpClaw.Persistence.PostgreSQL/SharpClaw.Persistence.PostgreSQL.csproj", "src/SharpClaw.Persistence.PostgreSQL/"]
COPY ["src/SharpClaw.Execution.Docker/SharpClaw.Execution.Docker.csproj", "src/SharpClaw.Execution.Docker/"]
COPY ["src/SharpClaw.Extensions.DependencyInjection/SharpClaw.Extensions.DependencyInjection.csproj", "src/SharpClaw.Extensions.DependencyInjection/"]
COPY ["src/SharpClaw.Web/SharpClaw.Web.csproj", "src/SharpClaw.Web/"]
COPY ["src/SharpClaw.Observability/SharpClaw.Observability.csproj", "src/SharpClaw.Observability/"]
COPY ["src/SharpClaw.Execution.Abstractions/SharpClaw.Execution.Abstractions.csproj", "src/SharpClaw.Execution.Abstractions/"]
COPY ["src/SharpClaw.Host/SharpClaw.Host.csproj", "src/SharpClaw.Host/"]
COPY ["src/SharpClaw.Runs/SharpClaw.Runs.csproj", "src/SharpClaw.Runs/"]
COPY ["src/SharpClaw.Execution.Podman/SharpClaw.Execution.Podman.csproj", "src/SharpClaw.Execution.Podman/"]
RUN dotnet restore "src/SharpClaw.Host/SharpClaw.Host.csproj"
COPY . .
RUN dotnet publish "src/SharpClaw.Host/SharpClaw.Host.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SharpClaw.Host.dll"]
