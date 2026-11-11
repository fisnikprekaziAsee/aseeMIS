namespace aseeMIS.Integration
{
    /// <summary>
    /// Represents the validated caller context extracted from integration headers / gRPC metadata.
    /// </summary>
    public sealed class IntegrationCallerContext
    {
        public bool IsAllowed { get; init; }
        public string? RequestUser { get; init; }
        public string? CorrelationId { get; init; }
        public string? ChannelCode { get; init; }
    }
}
