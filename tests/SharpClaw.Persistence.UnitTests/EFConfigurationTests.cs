using Microsoft.EntityFrameworkCore;
using SharpClaw.Persistence.Contracts.Entities;
using Xunit;

namespace SharpClaw.Persistence.UnitTests;

public class EntityTests
{
    [Fact]
    public void RunRecordEntity_Properties_CanBeSetAndRetrieved()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new RunRecordEntity
        {
            RunId = "run-1",
            TenantId = "tenant-1",
            Status = "running",
            IdempotencyKey = "idem-1",
            Provider = "docker",
            SandboxId = "sandbox-1",
            DeviceId = "device-1",
            CreatedAt = now,
            StartedAt = now,
            CompletedAt = null,
            InputData = "input",
            OutputData = "output",
            ErrorData = null,
            InputTokens = 100,
            OutputTokens = 50,
            RetryCount = 0,
            Metadata = "{}"
        };

        Assert.Equal("run-1", entity.RunId);
        Assert.Equal("tenant-1", entity.TenantId);
        Assert.Equal("running", entity.Status);
        Assert.Equal("idem-1", entity.IdempotencyKey);
        Assert.Equal("docker", entity.Provider);
        Assert.Equal("sandbox-1", entity.SandboxId);
        Assert.Equal("device-1", entity.DeviceId);
        Assert.Equal("input", entity.InputData);
        Assert.Equal("output", entity.OutputData);
        Assert.Null(entity.ErrorData);
        Assert.Equal(100, entity.InputTokens);
        Assert.Equal(50, entity.OutputTokens);
        Assert.Equal(0, entity.RetryCount);
        Assert.Equal("{}", entity.Metadata);
        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal(now, entity.StartedAt);
        Assert.Null(entity.CompletedAt);
    }

    [Fact]
    public void RunRecordEntity_DefaultStatus_IsPending()
    {
        var entity = new RunRecordEntity
        {
            RunId = "run-1",
            TenantId = "tenant-1"
        };

        Assert.Equal("pending", entity.Status);
    }

    [Fact]
    public void RunRecordEntity_DefaultCreatedAt_IsUtcNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var entity = new RunRecordEntity
        {
            RunId = "run-1",
            TenantId = "tenant-1"
        };
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.True(entity.CreatedAt >= before);
        Assert.True(entity.CreatedAt <= after);
    }

    [Fact]
    public void DeviceIdentityEntity_Properties_CanBeSetAndRetrieved()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            IsPaired = true,
            PublicKey = "public-key-123",
            Scopes = "operator:read,operator:write",
            DeviceName = "Test Device",
            LastIpAddress = "192.168.1.1",
            UpdatedAt = now
        };

        Assert.Equal("device-1", entity.DeviceId);
        Assert.Equal("tenant-1", entity.TenantId);
        Assert.True(entity.IsPaired);
        Assert.Equal("public-key-123", entity.PublicKey);
        Assert.Equal("operator:read,operator:write", entity.Scopes);
        Assert.Equal("Test Device", entity.DeviceName);
        Assert.Equal("192.168.1.1", entity.LastIpAddress);
        Assert.Equal(now, entity.UpdatedAt);
    }

    [Fact]
    public void DeviceIdentityEntity_DefaultIsPaired_IsFalse()
    {
        var entity = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1"
        };

        Assert.False(entity.IsPaired);
    }

    [Fact]
    public void AuditEventEntity_Properties_CanBeSetAndRetrieved()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new AuditEventEntity
        {
            EventId = "event-1",
            TenantId = "tenant-1",
            Timestamp = now,
            EntityType = "RunRecord",
            EntityId = "run-1",
            Action = "Created",
            UserId = "user-1",
            IpAddress = "192.168.1.1",
            OldValues = null,
            NewValues = "{\"status\":\"running\"}",
            Metadata = null,
            IsSensitive = false
        };

        Assert.Equal("event-1", entity.EventId);
        Assert.Equal("tenant-1", entity.TenantId);
        Assert.Equal(now, entity.Timestamp);
        Assert.Equal("RunRecord", entity.EntityType);
        Assert.Equal("run-1", entity.EntityId);
        Assert.Equal("Created", entity.Action);
        Assert.Equal("user-1", entity.UserId);
        Assert.Equal("192.168.1.1", entity.IpAddress);
        Assert.Null(entity.OldValues);
        Assert.Equal("{\"status\":\"running\"}", entity.NewValues);
        Assert.Null(entity.Metadata);
        Assert.False(entity.IsSensitive);
    }

    [Fact]
    public void AuditEventEntity_DefaultTimestamp_IsUtcNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var entity = new AuditEventEntity
        {
            EventId = "event-1",
            TenantId = "tenant-1",
            EntityType = "RunRecord",
            EntityId = "run-1",
            Action = "Created"
        };
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.True(entity.Timestamp >= before);
        Assert.True(entity.Timestamp <= after);
    }

    [Fact]
    public void IdempotencyKeyEntity_Properties_CanBeSetAndRetrieved()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new IdempotencyKeyEntity
        {
            Key = "idem-1",
            TenantId = "tenant-1",
            RunId = "run-1",
            CreatedAt = now,
            ExpiresAt = now.AddDays(1)
        };

        Assert.Equal("idem-1", entity.Key);
        Assert.Equal("tenant-1", entity.TenantId);
        Assert.Equal("run-1", entity.RunId);
        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal(now.AddDays(1), entity.ExpiresAt);
    }

    [Fact]
    public void ConfigRevisionEntity_Properties_CanBeSetAndRetrieved()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new ConfigRevisionEntity
        {
            RevisionId = "rev-1",
            TenantId = "tenant-1",
            Hash = "hash-123",
            Configuration = "{\"key\":\"value\"}",
            CreatedAt = now,
            IsActive = true,
            CreatedBy = "user-1",
            ChangeDescription = "Initial config"
        };

        Assert.Equal("rev-1", entity.RevisionId);
        Assert.Equal("tenant-1", entity.TenantId);
        Assert.Equal("hash-123", entity.Hash);
        Assert.Equal("{\"key\":\"value\"}", entity.Configuration);
        Assert.Equal(now, entity.CreatedAt);
        Assert.True(entity.IsActive);
        Assert.Equal("user-1", entity.CreatedBy);
        Assert.Equal("Initial config", entity.ChangeDescription);
    }

    [Fact]
    public void SessionRecordEntity_Properties_CanBeSetAndRetrieved()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new SessionRecordEntity
        {
            SessionId = "session-1",
            TenantId = "tenant-1",
            DeviceId = "device-1",
            CreatedAt = now,
            ExpiresAt = now.AddHours(1),
            LastActivityAt = now,
            IpAddress = "192.168.1.1",
            UserAgent = "TestAgent",
            IsActive = true
        };

        Assert.Equal("session-1", entity.SessionId);
        Assert.Equal("tenant-1", entity.TenantId);
        Assert.Equal("device-1", entity.DeviceId);
        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal(now.AddHours(1), entity.ExpiresAt);
        Assert.Equal(now, entity.LastActivityAt);
        Assert.Equal("192.168.1.1", entity.IpAddress);
        Assert.Equal("TestAgent", entity.UserAgent);
        Assert.True(entity.IsActive);
    }

    [Fact]
    public void RunRecordEntity_NullableProperties_CanBeNull()
    {
        var entity = new RunRecordEntity
        {
            RunId = "run-1",
            TenantId = "tenant-1",
            IdempotencyKey = null,
            Provider = null,
            SandboxId = null,
            DeviceId = null,
            StartedAt = null,
            CompletedAt = null,
            InputData = null,
            OutputData = null,
            ErrorData = null,
            InputTokens = null,
            OutputTokens = null,
            Metadata = null
        };

        Assert.Null(entity.IdempotencyKey);
        Assert.Null(entity.Provider);
        Assert.Null(entity.SandboxId);
        Assert.Null(entity.DeviceId);
        Assert.Null(entity.StartedAt);
        Assert.Null(entity.CompletedAt);
        Assert.Null(entity.InputData);
        Assert.Null(entity.OutputData);
        Assert.Null(entity.ErrorData);
        Assert.Null(entity.InputTokens);
        Assert.Null(entity.OutputTokens);
        Assert.Null(entity.Metadata);
    }

    [Fact]
    public void DeviceIdentityEntity_NullableStringProperties_CanBeNull()
    {
        var entity = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            PublicKey = null,
            DeviceName = null,
            LastIpAddress = null
        };

        Assert.Null(entity.PublicKey);
        Assert.Null(entity.DeviceName);
        Assert.Null(entity.LastIpAddress);
    }

    [Fact]
    public void DeviceIdentityEntity_Scopes_CanBeEmpty()
    {
        var entity = new DeviceIdentityEntity
        {
            DeviceId = "device-1",
            TenantId = "tenant-1",
            Scopes = ""
        };

        Assert.Equal("", entity.Scopes);
    }

    [Fact]
    public void AuditEventEntity_NullableProperties_CanBeNull()
    {
        var entity = new AuditEventEntity
        {
            EventId = "event-1",
            TenantId = "tenant-1",
            EntityType = "RunRecord",
            EntityId = "run-1",
            Action = "Created",
            UserId = null,
            IpAddress = null,
            OldValues = null,
            NewValues = null,
            Metadata = null
        };

        Assert.Null(entity.UserId);
        Assert.Null(entity.IpAddress);
        Assert.Null(entity.OldValues);
        Assert.Null(entity.NewValues);
        Assert.Null(entity.Metadata);
    }

    [Fact]
    public void RunRecordEntity_RetryCount_CanBeSet()
    {
        var entity = new RunRecordEntity
        {
            RunId = "run-1",
            TenantId = "tenant-1",
            RetryCount = 5
        };

        Assert.Equal(5, entity.RetryCount);
    }

    [Fact]
    public void AuditEventEntity_IsSensitive_CanBeSet()
    {
        var entity = new AuditEventEntity
        {
            EventId = "event-1",
            TenantId = "tenant-1",
            EntityType = "RunRecord",
            EntityId = "run-1",
            Action = "Created",
            IsSensitive = true
        };

        Assert.True(entity.IsSensitive);
    }

    [Fact]
    public void ConfigRevisionEntity_IsActive_CanBeSet()
    {
        var entity = new ConfigRevisionEntity
        {
            RevisionId = "rev-1",
            TenantId = "tenant-1",
            IsActive = false
        };

        Assert.False(entity.IsActive);
    }

    [Fact]
    public void SessionRecordEntity_DefaultIsActive_IsTrue()
    {
        var entity = new SessionRecordEntity
        {
            SessionId = "session-1",
            TenantId = "tenant-1",
            DeviceId = "device-1",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        Assert.True(entity.IsActive);
    }

    [Fact]
    public void SessionRecordEntity_DefaultCreatedAt_IsUtcNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var entity = new SessionRecordEntity
        {
            SessionId = "session-1",
            TenantId = "tenant-1",
            DeviceId = "device-1",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.True(entity.CreatedAt >= before);
        Assert.True(entity.CreatedAt <= after);
    }
}
