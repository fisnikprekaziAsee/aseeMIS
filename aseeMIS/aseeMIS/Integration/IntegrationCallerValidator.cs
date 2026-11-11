using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace aseeMIS.Integration
{
    /// <summary>
    /// Default implementation of <see cref="IIntegrationCallerValidator"/>.
    /// Reads API key and caller metadata from HTTP headers / gRPC metadata and validates
    /// against the configured allowed key.
    /// </summary> 
    public sealed class IntegrationCallerValidator(IConfiguration configuration) : IIntegrationCallerValidator
    {
        private readonly string? _configuredApiKey = configuration["Integration:ApiKey"];

        /// <inheritdoc />
        public IntegrationCallerContext Validate(IHeaderDictionary headers)
        {
            var apiKey = headers[IntegrationHeaderNames.ApiKey].FirstOrDefault();
            var requestUser = headers[IntegrationHeaderNames.RequestUser].FirstOrDefault();
            var correlationId = headers[IntegrationHeaderNames.CorrelationId].FirstOrDefault();
            var channelCode = headers[IntegrationHeaderNames.ChannelCode].FirstOrDefault();

            return BuildContext(apiKey, requestUser, correlationId, channelCode);
        }

        /// <inheritdoc />
        public IntegrationCallerContext Validate(ServerCallContext context)
        {
            var entries = context.RequestHeaders;

            var apiKey = entries.GetValue(IntegrationHeaderNames.ApiKey);
            var requestUser = entries.GetValue(IntegrationHeaderNames.RequestUser);
            var correlationId = entries.GetValue(IntegrationHeaderNames.CorrelationId);
            var channelCode = entries.GetValue(IntegrationHeaderNames.ChannelCode);

            return BuildContext(apiKey, requestUser, correlationId, channelCode);
        }

        private IntegrationCallerContext BuildContext( string? apiKey,  string? requestUser, string? correlationId, string? channelCode)
        {
            // If no API key is configured, reject all callers (fail closed).
            if (string.IsNullOrWhiteSpace(_configuredApiKey))
            {
                return new IntegrationCallerContext
                {
                    IsAllowed = false,
                    RequestUser = requestUser,
                    CorrelationId = correlationId,
                    ChannelCode = channelCode
                };
            }

            var isAllowed = !string.IsNullOrWhiteSpace(apiKey) && apiKey == _configuredApiKey;

            return new IntegrationCallerContext
            {
                IsAllowed = isAllowed,
                RequestUser = requestUser,
                CorrelationId = correlationId,
                ChannelCode = channelCode
            };
        }
    }
}
