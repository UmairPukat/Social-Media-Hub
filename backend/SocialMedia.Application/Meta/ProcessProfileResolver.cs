using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

public static class ProcessProfileResolver
{
    /// <summary>
    /// Prefer the profile matching the connected account's external id, then profile type, then most recently updated.
    /// </summary>
    public static SocialProfileEntityBase? PickConnectedProfile(
        IReadOnlyList<SocialProfileEntityBase> profiles,
        string? externalAccountId,
        ProfileType? preferredType = null)
    {
        if (profiles.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(externalAccountId))
        {
            var match = profiles.FirstOrDefault(p =>
                string.Equals(p.ExternalProfileId, externalAccountId, StringComparison.Ordinal));
            if (match is not null)
                return match;
        }

        if (preferredType.HasValue)
        {
            var typed = profiles.FirstOrDefault(p => p.ProfileType == preferredType.Value);
            if (typed is not null)
                return typed;
        }

        return profiles
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefault();
    }
}
