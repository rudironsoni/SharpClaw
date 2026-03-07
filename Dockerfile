FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copy solution files first for better layer caching
COPY ["Directory.Build.props", "Directory.Packages.props", "nuget.config", "SharpClaw.slnx", "global.json", "./"]

# Copy all project files using wildcard for efficient caching
COPY src/*/*.csproj ./src/
RUN find . -name "*.csproj" -exec dotnet restore {} \;
RUN dotnet restore "src/SharpClaw.Host/SharpClaw.Host.csproj"
COPY . .
RUN dotnet publish "src/SharpClaw.Host/SharpClaw.Host.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SharpClaw.Host.dll"]
