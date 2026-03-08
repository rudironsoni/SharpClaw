using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using SharpClaw.Abstractions.Identity;
using SharpClaw.Abstractions.Persistence;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Identity;

/// <summary>
/// Tenant-aware identity service with device pairing and authorization.
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private readonly IRepository<DeviceIdentityEntity> _repository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<IdentityService> _logger;
    
    public IdentityService(
        IRepository<DeviceIdentityEntity> repository,
        ITokenService tokenService,
        ILogger<IdentityService> logger)
    {
        _repository = repository;
        _tokenService = tokenService;
        _logger = logger;
    }
    
    public async Task<DeviceIdentity?> GetDeviceAsync(
        string deviceId, 
        string tenantId, 
        CancellationToken ct = default)
    {
        var entity = await _repository.GetAsync(deviceId, tenantId, ct);
        return entity == null ? null : MapToContract(entity);
    }
    
    public async Task<DeviceIdentity> UpsertDeviceAsync(
        string deviceId,
        string tenantId,
        DeviceIdentity identity,
        CancellationToken ct = default)
    {
        var entity = new DeviceIdentityEntity
        {
            DeviceId = deviceId,
            TenantId = tenantId,
            IsPaired = identity.IsPaired,
            PublicKey = identity.PublicKey,
            Scopes = string.Join(",", identity.Scopes),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        
        await _repository.UpsertAsync(deviceId, tenantId, entity, ct);
        
        _logger.LogDebug(
            "Device {DeviceId} upserted in tenant {TenantId}",
            deviceId, tenantId);
        
        return identity;
    }
    
    public async Task<DeviceAuthResult> AuthorizeAsync(
        string deviceId,
        string tenantId,
        IReadOnlySet<string> requiredScopes,
        CancellationToken ct = default)
    {
        var device = await GetDeviceAsync(deviceId, tenantId, ct);
        
        if (device == null)
        {
            _logger.LogWarning(
                "Authorization failed: Device {DeviceId} not found in tenant {TenantId}",
                deviceId, tenantId);
            
            return new DeviceAuthResult
            {
                IsSuccess = false,
                ErrorCode = IdentityErrors.NotLinked,
                ErrorMessage = "Device not registered"
            };
        }
        
        if (!device.IsPaired)
        {
            _logger.LogWarning(
                "Authorization failed: Device {DeviceId} not paired",
                deviceId);
            
            return new DeviceAuthResult
            {
                IsSuccess = false,
                ErrorCode = IdentityErrors.NotPaired,
                ErrorMessage = "Device pairing required"
            };
        }
        
        var missingScopes = requiredScopes
            .Except(device.Scopes, StringComparer.OrdinalIgnoreCase)
            .ToFrozenSet();
        
        if (missingScopes.Count > 0)
        {
            _logger.LogWarning(
                "Authorization failed: Device {DeviceId} missing scopes {Scopes}",
                deviceId, string.Join(", ", missingScopes));
            
            return new DeviceAuthResult
            {
                IsSuccess = false,
                ErrorCode = IdentityErrors.MissingScopes,
                ErrorMessage = $"Missing scopes: {string.Join(", ", missingScopes)}"
            };
        }
        
        var token = _tokenService.GenerateToken(device);
        
        _logger.LogInformation(
            "Device {DeviceId} authorized with scopes {Scopes}",
            deviceId, string.Join(", ", device.Scopes));
        
        return new DeviceAuthResult
        {
            IsSuccess = true,
            Device = device,
            Token = token
        };
    }
    
    public async Task<DeviceAuthResult> PairDeviceAsync(
        string deviceId,
        string tenantId,
        string publicKey,
        CancellationToken ct = default)
    {
        var device = await GetDeviceAsync(deviceId, tenantId, ct);
        
        if (device == null)
        {
            _logger.LogWarning(
                "Pairing failed: Device {DeviceId} not found in tenant {TenantId}",
                deviceId, tenantId);
            
            return new DeviceAuthResult
            {
                IsSuccess = false,
                ErrorCode = IdentityErrors.NotLinked,
                ErrorMessage = "Device not registered"
            };
        }
        
        if (device.IsPaired && device.PublicKey != publicKey)
        {
            _logger.LogWarning(
                "Pairing failed: Device {DeviceId} already paired with different key",
                deviceId);
            
            return new DeviceAuthResult
            {
                IsSuccess = false,
                ErrorCode = IdentityErrors.AlreadyPaired,
                ErrorMessage = "Device already paired"
            };
        }
        
        if (device.IsPaired && device.PublicKey == publicKey)
        {
            var existingToken = _tokenService.GenerateToken(device);
            
            _logger.LogInformation(
                "Device {DeviceId} re-paired successfully",
                deviceId);
            
            return new DeviceAuthResult
            {
                IsSuccess = true,
                Device = device,
                Token = existingToken
            };
        }
        
        // Perform pairing
        var pairedDevice = new DeviceIdentity
        {
            DeviceId = deviceId,
            TenantId = tenantId,
            IsPaired = true,
            PublicKey = publicKey,
            Scopes = device.Scopes,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        
        await UpsertDeviceAsync(deviceId, tenantId, pairedDevice, ct);
        
        var token = _tokenService.GenerateToken(pairedDevice);
        
        _logger.LogInformation(
            "Device {DeviceId} paired successfully with public key {PublicKeyPrefix}...",
            deviceId,
            publicKey[..Math.Min(16, publicKey.Length)]);
        
        return new DeviceAuthResult
        {
            IsSuccess = true,
            Device = pairedDevice,
            Token = token
        };
    }
    
    private static DeviceIdentity MapToContract(DeviceIdentityEntity entity)
    {
        return new DeviceIdentity
        {
            DeviceId = entity.DeviceId,
            TenantId = entity.TenantId,
            IsPaired = entity.IsPaired,
            PublicKey = entity.PublicKey,
            Scopes = entity.Scopes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .ToFrozenSet(),
            UpdatedAt = entity.UpdatedAt
        };
    }
}

/// <summary>
/// Identity error codes.
/// </summary>
public static class IdentityErrors
{
    public const string NotLinked = "NOT_LINKED";
    public const string NotPaired = "NOT_PAIRED";
    public const string MissingScopes = "MISSING_SCOPES";
    public const string AlreadyPaired = "ALREADY_PAIRED";
    public const string AlreadyExists = "ALREADY_EXISTS";
}
