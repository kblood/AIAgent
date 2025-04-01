using System;
using System.Collections.Generic;

namespace AIAgentTest.Services
{
    public static class ServiceProvider
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        
        public static void RegisterService<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }
        
        public static void RegisterService<TInterface, TImplementation>(TImplementation service)
            where TImplementation : class, TInterface
            where TInterface : class
        {
            _services[typeof(TInterface)] = service;
        }
        
        public static T GetService<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
        }
        
        public static object GetServiceByType(Type type)
        {
            if (_services.TryGetValue(type, out var service))
            {
                return service;
            }
            
            throw new InvalidOperationException($"Service of type {type.Name} is not registered.");
        }
        
        public static void ClearServices()
        {
            foreach (var service in _services.Values)
            {
                if (service is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            _services.Clear();
        }
    }
}