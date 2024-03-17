namespace DynamicLayerArchitectureForNetCore.CustomAttributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class ComponentAttribute : Attribute
    {
        public string Name { get; set; }
        public ServiceLifetime ServiceLifetime { get; set; }

        public ComponentAttribute(string name = "", ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            Name = name;
            ServiceLifetime = serviceLifetime;
        }
    }
}