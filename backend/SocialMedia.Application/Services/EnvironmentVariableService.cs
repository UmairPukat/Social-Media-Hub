using SocialMedia.Application.Catalog;
using SocialMedia.Application.DTOs.Common;
using SocialMedia.Application.DTOs.EnvironmentVariables;
using SocialMedia.Application.Interfaces;
using SocialMedia.Domain.Entities;
using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Interfaces;

namespace SocialMedia.Application.Services;

public class EnvironmentVariableService : IEnvironmentVariableService
{
    private const string MaskedValue = "••••••••••••";

    private readonly IUnitOfWork _unitOfWork;

    public EnvironmentVariableService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<IReadOnlyList<EnvironmentVariableDto>>> GetByScopeAsync(
        string scope,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseScope(scope, out var parsedScope))
            return ApiResponse<IReadOnlyList<EnvironmentVariableDto>>.Fail("Scope must be 'frontend' or 'backend'.");

        var items = await _unitOfWork.EnvironmentVariables.GetByScopeAsync(parsedScope, cancellationToken);
        var dtos = items.Select(x => ToDto(x, maskSensitive: true)).ToList();
        return ApiResponse<IReadOnlyList<EnvironmentVariableDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<EnvironmentVariableDto>> GetByIdAsync(
        Guid id,
        bool revealValue = false,
        CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.EnvironmentVariables.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return ApiResponse<EnvironmentVariableDto>.Fail("Environment variable not found.");

        return ApiResponse<EnvironmentVariableDto>.Ok(ToDto(entity, maskSensitive: !revealValue));
    }

    public async Task<ApiResponse<EnvironmentVariableDto>> CreateAsync(
        UpsertEnvironmentVariableRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRequestAsync(request, excludeId: null, cancellationToken);
        if (validation is not null)
            return ApiResponse<EnvironmentVariableDto>.Fail(validation);

        var entity = new EnvironmentVariable
        {
            Name = NormalizeName(request.Name),
            Value = request.Value?.Trim() ?? string.Empty,
            Description = request.Description?.Trim() ?? string.Empty,
            IsRequired = request.IsRequired,
            Scope = ParseScopeOrDefault(request.Scope),
            IsSensitive = DetectSensitive(request.Name)
        };

        await _unitOfWork.EnvironmentVariables.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<EnvironmentVariableDto>.Ok(ToDto(entity, maskSensitive: entity.IsSensitive), "Variable created.");
    }

    public async Task<ApiResponse<EnvironmentVariableDto>> UpdateAsync(
        Guid id,
        UpsertEnvironmentVariableRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.EnvironmentVariables.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return ApiResponse<EnvironmentVariableDto>.Fail("Environment variable not found.");

        var validation = await ValidateRequestAsync(request, excludeId: id, cancellationToken);
        if (validation is not null)
            return ApiResponse<EnvironmentVariableDto>.Fail(validation);

        entity.Name = NormalizeName(request.Name);
        entity.Value = request.Value?.Trim() ?? string.Empty;
        entity.Description = request.Description?.Trim() ?? string.Empty;
        entity.IsRequired = request.IsRequired;
        entity.Scope = ParseScopeOrDefault(request.Scope);
        entity.IsSensitive = DetectSensitive(request.Name);
        entity.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.EnvironmentVariables.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<EnvironmentVariableDto>.Ok(ToDto(entity, maskSensitive: entity.IsSensitive), "Variable updated.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.EnvironmentVariables.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return ApiResponse<object>.Fail("Environment variable not found.");

        _unitOfWork.EnvironmentVariables.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<object>.Ok(new { }, "Variable deleted.");
    }

    public static bool DetectSensitive(string name)
    {
        var upper = name.ToUpperInvariant();
        return upper.Contains("SECRET")
            || upper.Contains("PASSWORD")
            || upper.Contains("TOKEN")
            || upper.Contains("KEY")
            || upper.Contains("CREDENTIAL")
            || upper.Contains("PRIVATE");
    }

    private async Task<string?> ValidateRequestAsync(
        UpsertEnvironmentVariableRequest request,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Variable name is required.";

        if (!TryParseScope(request.Scope, out _))
            return "Scope must be 'frontend' or 'backend'.";

        var normalized = NormalizeName(request.Name);
        if (await _unitOfWork.EnvironmentVariables.NameExistsAsync(normalized, ParseScopeOrDefault(request.Scope), excludeId, cancellationToken))
            return "A variable with this name already exists for this scope.";

        return null;
    }

    private static EnvironmentVariableDto ToDto(EnvironmentVariable entity, bool maskSensitive)
    {
        var masked = maskSensitive && entity.IsSensitive && !string.IsNullOrEmpty(entity.Value);
        return new EnvironmentVariableDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Value = masked ? MaskedValue : entity.Value,
            Description = entity.Description,
            IsRequired = entity.IsRequired,
            Scope = entity.Scope == EnvironmentVariableScope.Frontend ? "frontend" : "backend",
            IsSensitive = entity.IsSensitive,
            IsMasked = masked
        };
    }

    private static string NormalizeName(string name) => name.Trim();

    private static bool TryParseScope(string scope, out EnvironmentVariableScope parsed)
    {
        parsed = EnvironmentVariableScope.Frontend;
        if (string.IsNullOrWhiteSpace(scope))
            return false;

        return scope.Trim().ToLowerInvariant() switch
        {
            "frontend" => Assign(EnvironmentVariableScope.Frontend, out parsed),
            "backend" => Assign(EnvironmentVariableScope.Backend, out parsed),
            _ => false
        };
    }

    private static EnvironmentVariableScope ParseScopeOrDefault(string scope)
    {
        TryParseScope(scope, out var parsed);
        return parsed;
    }

    private static bool Assign(EnvironmentVariableScope value, out EnvironmentVariableScope parsed)
    {
        parsed = value;
        return true;
    }
}
