namespace DynamicLayerArchitectureForNetCore.Config.ComponentConfig;

public class ServiceCollectionAccessor
{
    public IServiceCollection Services { get; }

    public ServiceCollectionAccessor(IServiceCollection services)
    {
        Services = services;
    }
}