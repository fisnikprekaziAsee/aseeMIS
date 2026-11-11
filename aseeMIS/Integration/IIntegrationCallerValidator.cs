using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace aseeMIS.Integration
{
    /// <summary>
    /// Validates integration caller identity from HTTP headers or gRPC metadata.
    /// </summary>
    public interface IIntegrationCallerValidator
    {
        /// <summary>
        /// Validates caller from ASP.NET Core HTTP request headers.
        /// </summary>
        IntegrationCallerContext Validate(IHeaderDictionary headers);

        /// <summary>
        /// Validates caller from gRPC <see cref="ServerCallContext"/> metadata.
        /// </summary>
        IntegrationCallerContext Validate(ServerCallContext context);
    }
}
