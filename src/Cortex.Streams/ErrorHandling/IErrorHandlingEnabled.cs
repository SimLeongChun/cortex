using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cortex.Streams.ErrorHandling
{
    /// <summary>
    /// Implemented by operators that want to participate in global error handling.
    /// </summary>
    public interface IErrorHandlingEnabled
    {
        void SetErrorHandling(StreamExecutionOptions options);
    }
}
