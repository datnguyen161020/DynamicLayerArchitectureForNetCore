using DynamicLayerArchitectureForNetCore.CustomAttributes;

namespace DynamicLayerArchitectureForNetCore.Config;

public static class StartUp
{
    public static WebApplication BuildApplication(this WebApplicationBuilder builder, IConfiguration configuration)
    {
        DatabaseDriverFactory.InstallDriver(configuration, builder).Wait();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                
                if (type.GetCustomAttributes(typeof(ComponentAttribute), true).Length <= 0) continue;
                CreateComponent(builder, type);

            }
        }

        builder.Services.AddControllers();

        return builder.Build();
    }
    
    private static void CreateComponent(WebApplicationBuilder builder, Type type)
    {
        if (type.GetCustomAttributes(typeof(ComponentAttribute), true).Length > 0 
            && type.GetCustomAttributes(typeof(RepositoryAttribute), true).Length <= 0
            && type.GetCustomAttributes(typeof(ServiceAttribute), true).Length <= 0)
        {
            builder.Services.AddTransient(type);
            return;
        }    
        if (type.GetCustomAttributes(typeof(RepositoryAttribute), true).Length <= 0)
        {
            builder.Services.AddTransient(type);
            return;
        }
            
        builder.Services.AddScoped(type, provider => GetRepositoryCreator(type, provider));
    }

    private static object GetRepositoryCreator(Type type, IServiceProvider provider)
    {
        return DynamicRepository.CreateRepository(type, provider) ?? throw new InvalidOperationException();
    }
}