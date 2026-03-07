# SharpClaw

AI Gateway for OpenClaw-compatible protocol implementation.

## Overview

SharpClaw is a sophisticated AI gateway project built on .NET 10 (preview) that implements an OpenClaw-compatible protocol for AI agent orchestration. The project features a modular design, comprehensive testing strategy, and security-conscious implementation.

## Project Structure

- **37 source projects** - Core functionality, protocols, execution providers
- **29 test projects** - Unit, integration, and E2E tests
- **Target Framework**: .NET 10.0 (preview)
- **Solution**: Modern `.slnx` format with Central Package Management

## Quick Start

### Prerequisites

- .NET 10 SDK (preview)
- Docker (for containerized execution)
- PostgreSQL (optional, SQLite available)

### Configuration

Copy `appsettings.json` and configure:

```json
{
  "Jwt": {
    "SecretKey": "${JWT_SECRET_KEY}",  // Min 32 characters
    "Issuer": "SharpClaw",
    "Audience": "SharpClawClients"
  },
  "RateLimiting": {
    "TokenLimit": 1000,
    "FeatureFlags": {
      "UseNewRateLimiting": false  // Toggle for migration
    }
  }
}
```

### Running

```bash
dotnet build
dotnet run --project src/SharpClaw.Host
```

### Docker

```bash
docker-compose up -d  # Requires POSTGRES_PASSWORD env var
```

## Security Features

- **JWT Authentication** - HMAC-SHA256 signed tokens with key rotation
- **Rate Limiting** - Token bucket with backward compatibility
- **Docker Security** - Seccomp, AppArmor, capability dropping
- **Security Headers** - HSTS, CSP, X-Frame-Options

## API Endpoints

- `POST /api/runs` - Execute code
- `GET /api/runs/{id}` - Get run status
- `POST /api/runs/{id}/events` - Stream events
- `GET /health` - Health check

## Observability

- **Metrics** - OpenTelemetry with custom business metrics
- **Health Checks** - Configurable endpoints
- **Logging** - Structured logging with correlation IDs

## Development

### Testing

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage threshold: **80%**

### Load Testing

See `tests/SharpClaw.LoadTests/` for load testing scenarios.

## Migration Notes

### Rate Limiting (Phase 4)

Migrated from deprecated `AspNetCoreRateLimit` to `System.Threading.RateLimiting`:

- Feature flags control migration (`UseNewRateLimiting`)
- Backward compatible HTTP 429 responses
- Toggle via configuration

### JWT Implementation (Phase 1)

Replaced placeholder implementation with proper JWT:

- HMAC-SHA256 signing
- Key rotation support
- Proper claims structure

## Contributing

1. Fork the repository
2. Create a feature branch
3. Run tests: `dotnet test`
4. Ensure coverage > 80%
5. Submit PR

## License

MIT License - see LICENSE file
