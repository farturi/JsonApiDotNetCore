using System.Net;
using JsonApiDotNetCore.Serialization.Objects;

namespace JsonApiDotNetCore.Errors
{
    /// <summary>
    /// The error that is thrown when a POST request is received that contains a client-generated ID.
    /// </summary>
    public sealed class ResourceIdInPostRequestNotAllowedException : JsonApiException
    {
        public ResourceIdInPostRequestNotAllowedException(int? atomicOperationIndex = null)
            : base(new Error(HttpStatusCode.Forbidden)
            {
                Title = "Specifying the resource ID in POST requests is not allowed.",
                Source =
                {
                    Pointer = atomicOperationIndex != null
                        ? $"/atomic:operations[{atomicOperationIndex}]/data/id"
                        : "/data/id"
                }
            })
        {
        }
    }
}
