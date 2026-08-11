namespace SocialMedia.Domain.Enums;

public enum SocialAccountStatus
{
    Disconnected = 0,
    Connected = 1,
    Expired = 2,
    Error = 3
}

public enum ProfileType
{
    FacebookPage = 1,
    InstagramBusiness = 2,
    WhatsAppPhone = 3,
    InstagramLogin = 4,
    Other = 99
}

public enum ContentPostType
{
    Text = 1,
    Image = 2,
    Video = 3,
    Carousel = 4,
    Reel = 5,
    Story = 6,
    Other = 99
}

public enum ContentPostStatus
{
    Draft = 0,
    Published = 1,
    Failed = 2,
    Scheduled = 3,
    Deleted = 4
}

public enum MediaType
{
    Image = 1,
    Video = 2,
    Audio = 3,
    Document = 4,
    Other = 99
}

public enum ConversationStatus
{
    Open = 1,
    Closed = 2,
    Archived = 3
}

public enum MessageDirection
{
    Inbound = 1,
    Outbound = 2
}

public enum MessageContentType
{
    Text = 1,
    Image = 2,
    Video = 3,
    Audio = 4,
    Document = 5,
    Template = 6,
    Other = 99
}

public enum MessageDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Delivered = 2,
    Read = 3,
    Failed = 4
}

public enum WebhookEventStatus
{
    Received = 0,
    Queued = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4
}

public enum SyncEntityType
{
    Posts = 1,
    Comments = 2,
    Messages = 3,
    Media = 4,
    Profiles = 5
}

public enum SyncJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}
