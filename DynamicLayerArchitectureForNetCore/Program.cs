using DynamicLayerArchitectureForNetCore.Config;

var builder = WebApplication.CreateBuilder(args);
var app = builder.BuildApplication(builder.Configuration);

app.MapControllers();
app.Run();