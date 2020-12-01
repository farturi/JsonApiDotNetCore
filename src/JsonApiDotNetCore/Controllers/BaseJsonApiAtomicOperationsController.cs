using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JsonApiDotNetCore.AtomicOperations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Controllers
{
    /// <summary>
    /// Implements the foundational ASP.NET Core controller layer in the JsonApiDotNetCore architecture for handling atomic:operations requests.
    /// See https://jsonapi.org/ext/atomic/ for details. Delegates work to <see cref="IAtomicOperationsProcessor"/>.
    /// </summary>
    public abstract class BaseJsonApiAtomicOperationsController : CoreJsonApiController
    {
        private readonly IJsonApiOptions _options;
        private readonly IAtomicOperationsProcessor _processor;
        private readonly TraceLogWriter<BaseJsonApiAtomicOperationsController> _traceWriter;

        protected BaseJsonApiAtomicOperationsController(IJsonApiOptions options, ILoggerFactory loggerFactory,
            IAtomicOperationsProcessor processor)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _options = options ?? throw new ArgumentNullException(nameof(options));
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _traceWriter = new TraceLogWriter<BaseJsonApiAtomicOperationsController>(loggerFactory);
        }

        /// <summary>
        /// Processes a document with atomic operations and returns their results.
        /// </summary>
        /// <example><code><![CDATA[
        /// POST /api/v1/operations HTTP/1.1
        /// Content-Type: application/vnd.api+json
        /// 
        /// {
        ///   "operations": [{
        ///     "op": "add",
        ///     "ref": {
        ///       "type": "authors"
        ///     },
        ///     "data": {
        ///       "type": "authors",
        ///       "attributes": {
        ///         "name": "John Doe"
        ///       }
        ///     }
        ///   }]
        /// }
        /// ]]></code></example>
        public virtual async Task<IActionResult> PostOperationsAsync([FromBody] AtomicOperationsDocument document,
            CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new {document});

            if (document == null)
            {
                // TODO: @OPS: Should throw NullReferenceException here, but catch this error higher up the call stack (JsonApiReader).
                return new StatusCodeResult(422);
            }

            if (_options.ValidateModelState)
            {
                // TODO: @OPS: Add ModelState validation.
            }

            var results = await _processor.ProcessAsync(document.Operations, cancellationToken);

            if (results.Any(result => result.Data != null))
            {
                return Ok(new AtomicOperationsDocument
                {
                    Results = results
                });
            }

            return NoContent();
        }
    }
}
