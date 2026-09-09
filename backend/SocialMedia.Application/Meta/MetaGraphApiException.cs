namespace SocialMedia.Application.Meta;

public sealed class MetaGraphApiException : Exception
{
    public MetaGraphApiException(
        string message,
        int? httpStatusCode = null,
        string? metaErrorCode = null,
        string? metaErrorMessage = null,
        string? metaErrorType = null)
        : base(message)
    {
        HttpStatusCode = httpStatusCode;
        MetaErrorCode = metaErrorCode;
        MetaErrorMessage = metaErrorMessage ?? message;
        MetaErrorType = metaErrorType;
    }

    public int? HttpStatusCode { get; }
    public string? MetaErrorCode { get; }
    public string? MetaErrorMessage { get; }
    public string? MetaErrorType { get; }
}
