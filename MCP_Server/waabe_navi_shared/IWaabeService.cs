using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace waabe_navi_shared
{
    /// <summary>
    /// Common interface for services registered in the WAABE service registry.
    /// Provides a consistent contract for service identification and availability.
    /// </summary>
    public interface IWaabeService
    {
        /// <summary>
        /// Gets the unique name of the service.
        /// Used for identification and lookup in the service registry.
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Gets a value indicating whether the service is currently available.
        /// </summary>
        bool IsAvailable { get; }
    }
}
