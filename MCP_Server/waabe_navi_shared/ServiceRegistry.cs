using System;
using System.Collections.Concurrent;

namespace waabe_navi_shared
{
    /// <summary>
    /// Central registry for WAABE services.
    /// - Allows registering and retrieving services by name.
    /// - Ensures thread-safe access using a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// - Can list and clear registered services.
    /// </summary>
    public static class ServiceRegistry
    {
        private static readonly ConcurrentDictionary<string, IWaabeService> _services = new ConcurrentDictionary<string, IWaabeService>();

        /// <summary>
        /// Registers a service into the registry, keyed by its <see cref="IWaabeService.ServiceName"/>.
        /// If a service with the same name already exists, it will be overwritten.
        /// </summary>
        /// <param name="service">The service to register.</param>
        public static void Register(IWaabeService service)
        {
            _services[service.ServiceName] = service;
        }

        /// <summary>
        /// Retrieves a service from the registry by name.
        /// </summary>
        /// <param name="name">The unique service name.</param>
        /// <returns>
        /// The service instance if found, or <c>null</c> if no service is registered under that name.
        /// </returns>
        public static IWaabeService GetService(string name)
        {
            return _services.TryGetValue(name, out var svc) ? svc : null;
        }

        /// <summary>
        /// Checks whether a service with the given name is registered.
        /// </summary>
        /// <param name="name">The service name to check.</param>
        /// <returns><c>true</c> if a service with that name exists; otherwise <c>false</c>.</returns>
        public static bool ServiceExists(string name)
        {
            return _services.ContainsKey(name);
        }

        /// <summary>
        /// Removes all registered services from the registry.
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
        }

        /// <summary>
        /// Logs all currently registered services to the central <see cref="LogHelper"/>.
        /// </summary>
        /// <remarks>
        /// - If the registry is empty, a log entry will indicate this.  
        /// - Each service is logged with its name, type, and availability state.  
        /// - Exceptions during logging are caught and logged as errors.  
        /// </remarks>
        public static void LogRegisteredServices()
        {
            try
            {
                if (_services.Count == 0)
                {
                    LogHelper.LogEvent("ServiceRegistry ist LEER!");
                }
                else
                {
                    LogHelper.LogEvent($"Registrierte Services ({_services.Count}):");
                    foreach (var kvp in _services)
                    {
                        LogHelper.LogEvent($"  - {kvp.Key}: {kvp.Value?.GetType().Name} (Available: {kvp.Value?.IsAvailable})");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Auflisten der Services: {ex.Message}");
            }
        }
    }
}
