using DynamicLayerArchitectureForNetCore.CustomAttributes;

namespace DynamicLayerArchitectureForNetCore;

[Repository]
public interface IRepository
{

    [Query("SELECT * FROM sys.sys_config")]
    List<object> TestMethod();
}