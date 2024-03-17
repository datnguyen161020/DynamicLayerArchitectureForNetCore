using DynamicLayerArchitectureForNetCore.Config;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration["SqlDriver"] = "MySqlConnector";
builder.Configuration["connectionString"] = "Server=localhost; Port = 3308; User=root; Database=sys; password=123456;";
var app = builder.BuildApplication(builder.Configuration);

app.MapControllers();
app.Run();