using SocialMedia.Domain.Enums;
using SocialMedia.Domain.Modules.Common.Entities;

namespace SocialMedia.Application.Meta;

/// <summary>
/// Resolves the Instagram/Facebook DM recipient id for outbound replies.
/// Meta requires the customer's app-scoped id (IGSID/PSID) from the inbound webhook — never the business account id.
/// </summary>
public static class InstagramMessagingRecipient
{
    public static string? Resolve(
        MessageEntityBase message,
        ConversationEntityBase conversation,
        SocialProfileEntityBase profile)
    {
        foreach (var candidate in CandidateIds(message, conversation))
        {
            if (IsValidCustomerRecipient(profile, candidate))
                return candidate;
        }

        return null;
    }

    public static bool IsValidCustomerRecipient(SocialProfileEntityBase profile, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        return !ProfileOwnsExternalId(profile, candidate);
    }

    public static string FormatRecipientNotFoundError(string menuType, string? recipientId)
        => string.IsNullOrWhiteSpace(recipientId)
            ? "Recipient is unknown for this conversation. Ask the customer to send a new message, then reply from the same module where the message was received."
            : $"Instagram could not find recipient '{recipientId}'. " +
              "Replies must use the customer's app-scoped id from the inbound webhook, not your business Instagram id. " +
              $"Reconnect Instagram in {menuType} and confirm Meta webhooks use the same app's callback URL for that module.";

    public static string FormatInstagramRecipientApiError(string menuType)
        => "Instagram rejected the recipient id (error 2534014). " +
           "This usually means the DM was received by a different Meta app than the one sending the reply. " +
           $"Use one module end-to-end (connect, webhook URL, and reply) — for Integrations that means the Integration app credentials and /api/integrations/webhooks.";

    private static IEnumerable<string?> CandidateIds(
        MessageEntityBase message,
        ConversationEntityBase conversation)
    {
        yield return conversation.CustomerId;

        if (message.Direction == MessageDirection.Inbound)
        {
            yield return message.SenderId;
            yield return message.ReceiverId;
        }
        else
        {
            yield return message.ReceiverId;
            yield return message.SenderId;
        }
    }

    private static bool ProfileOwnsExternalId(SocialProfileEntityBase profile, string externalId)
    {
        if (string.Equals(profile.ExternalProfileId, externalId, StringComparison.Ordinal))
            return true;

        var pageId = ReadPageId(profile.MetadataJson);
        if (!string.IsNullOrWhiteSpace(pageId) && string.Equals(pageId, externalId, StringComparison.Ordinal))
            return true;

        foreach (var alternateId in ReadAlternateIds(profile.MetadataJson))
        {
            if (string.Equals(alternateId, externalId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string? ReadPageId(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty("pageId", out var pageId) ? pageId.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadAlternateIds(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return Array.Empty<string>();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
            if (!doc.RootElement.TryGetProperty("alternateIds", out var ids) ||
                ids.ValueKind != System.Text.Json.JsonValueKind.Array)
                return Array.Empty<string>();

            return ids.EnumerateArray()
                .Select(id => id.ToString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList()!;
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
