using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace DynamicLayerArchitectureForNetCore;

[ApiController]
public class Controller : ControllerBase
{
    private readonly IRepository _repository;
    public Controller(IRepository repository)
    {
        _repository = repository;
    }
    
    [HttpGet("/")]
    public Task<IActionResult> SignIn()
    {
        var response = _repository.TestMethod();
        Console.WriteLine(JsonConvert.SerializeObject(response));
        return Task.FromResult<IActionResult>(Ok(JsonConvert.SerializeObject(response)));
    }
}