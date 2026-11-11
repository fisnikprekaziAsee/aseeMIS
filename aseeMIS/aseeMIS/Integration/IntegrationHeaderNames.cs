namespace aseeMIS.Integration
{
    /// <summary>
    /// Standard HTTP header names for server-to-server integration calls.
    /// </summary>
    public static class IntegrationHeaderNames
    {
        public const string ApiKey = "x-api-key";
        public const string RequestUser = "x-request-user";
        public const string CorrelationId = "x-correlation-id";
        public const string ChannelCode = "x-channel-code";
    }
}
