using Microsoft.Extensions.DependencyInjection;

namespace SmsRelay;

/// <summary>Allows Android components created by the OS to reach the MAUI service container.</summary>
public static class ServiceRegistry
{
    public static IServiceProvider? Provider { get; set; }
    public static T Get<T>() where T : notnull
    {
        var provider = Provider ?? throw new InvalidOperationException("SMS Relay services are not initialized.");
        return provider.GetRequiredService<T>();
    }
}
