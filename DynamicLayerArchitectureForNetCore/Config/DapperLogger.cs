using System.Data;
using Dapper;
using DynamicLayerArchitectureForNetCore.CustomAttributes;

namespace DynamicLayerArchitectureForNetCore.Config;

[Component]
public class DapperLogger
{
    private readonly IDbConnection _dbConnection;

    public DapperLogger(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public void LogAndExecute(Action<IDbConnection> action)
    {
        _dbConnection.Open();
        action.Invoke(_dbConnection);
    }

    public IEnumerable<T> Query<T>(string sql, object? param = null)
    {
        var customParams = param as CustomDynamicParameters;
        return _dbConnection.Query<T>(sql, customParams);
    }

    public int Execute(string sql, object? param = null)
    {
        var customParams = param as CustomDynamicParameters;
        return _dbConnection.Execute(sql, customParams);
    }
}