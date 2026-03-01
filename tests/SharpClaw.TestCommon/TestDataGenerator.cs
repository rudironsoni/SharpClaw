using Bogus;
using SharpClaw.Gateway;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.TestCommon;

/// <summary>
/// Generates test data using Bogus for consistent, realistic test scenarios.
/// </summary>
public static class TestDataGenerator
{
    private static readonly Faker Faker = new();

    public static RequestFrame CreateRequestFrame(string? method = null, object? payload = null)
    {
        return new RequestFrame(
            Id: Guid.NewGuid().ToString("N"),
            Method: method ?? Faker.Random.Word(),
            Payload: payload,
            IdempotencyKey: Faker.Random.Bool() ? Guid.NewGuid().ToString("N") : null
        );
    }

    public static DeviceContext CreateDeviceContext(
        string? deviceId = null,
        IReadOnlySet<string>? scopes = null,
        bool isAuthenticated = true)
    {
        return new DeviceContext(
            DeviceId: deviceId ?? Faker.Random.Guid().ToString("N"),
            Scopes: scopes ?? new HashSet<string> { "operator:read", "operator:write" },
            IsAuthenticated: isAuthenticated
        );
    }

    public static EventFrame CreateEventFrame(string? eventType = null, object? payload = null)
    {
        return new EventFrame(
            Event: eventType ?? Faker.Hacker.Verb(),
            Payload: payload,
            Seq: Faker.Random.Long(1, long.MaxValue)
        );
    }

    public static string GenerateRunId() => $"run-{Guid.NewGuid():N}";

    public static string GenerateTenantId() => $"tenant-{Faker.Random.Guid():N}";

    public static string GenerateDeviceId() => $"device-{Faker.Random.Guid():N}";

    public static IReadOnlySet<string> GenerateScopes(params string[] additionalScopes)
    {
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "operator:read",
            "operator:write"
        };

        foreach (var scope in additionalScopes)
        {
            scopes.Add(scope);
        }

        return scopes;
    }
}
