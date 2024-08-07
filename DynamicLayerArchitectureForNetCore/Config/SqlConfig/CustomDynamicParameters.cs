﻿using System.Data;
using System.Text;
using Dapper;
using DynamicLayerArchitectureForNetCore.Config.LoggerConfig;
using DynamicLayerArchitectureForNetCore.Exceptions;
using Newtonsoft.Json;
using LoggerFactory = DynamicLayerArchitectureForNetCore.Config.LoggerConfig.LoggerFactory;

namespace DynamicLayerArchitectureForNetCore.Config.SqlConfig;

public class CustomDynamicParameters : SqlMapper.IDynamicParameters
{
    private static readonly CustomLogger Log = LoggerFactory.CreateLogger(typeof(CustomDynamicParameters));

    private readonly Dictionary<string, object> _parameters = new();
        
    public void Add(string name, object? value = null, DbType? dbType = null, ParameterDirection? direction = null, int? size = null)
    {
        _parameters.Add(name, value ?? "null");
    }

    public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
    {
        var sqlWithValue = ReplaceParametersInSql(command.CommandText);
        Log.Info($"Query string \n{sqlWithValue}");
        command.CommandText = sqlWithValue;
    }
        
    private string ReplaceParametersInSql(string originalSql)
    {
        var query = new StringBuilder(originalSql);
        var queryParams = Array.FindAll(query.ToString()
            .Split(' ', ',', '(', ')'), param => param.StartsWith(':'));

        foreach (var queryParam in queryParams)
        {
            var queryParamTmp = queryParam;
            if (query.ToString().StartsWith("Insert", StringComparison.InvariantCultureIgnoreCase))
            {
                queryParamTmp = queryParamTmp.Replace(",", "");
            }
            var param = queryParamTmp.Split('.');

            if (param.Length > 1)
            {
                var parameterObject =
                    _parameters[param[0].Split(':').LastOrDefault() ?? throw new InvalidOperationException()];
                var paramValue = parameterObject.GetType()
                    .GetProperty(param.LastOrDefault() ?? string.Empty)?.GetValue(parameterObject, null);
                if (parameterObject.GetType().GetProperty(param.LastOrDefault() ?? string.Empty)?.PropertyType == typeof(string))
                {
                    query.Replace(queryParamTmp,
                        new StringBuilder(string.Empty).Append('\'')
                            .Append(JsonConvert.SerializeObject(paramValue)).Append('\'')
                            .Replace("\"", string.Empty).ToString());
                    continue;
                }
                query.Replace(queryParamTmp, JsonConvert.SerializeObject(paramValue));
                    
                continue;
            }
            var parameter = _parameters[queryParamTmp.Split(':').LastOrDefault() ?? throw new InvalidOperationException()];
            if (parameter.GetType().IsPrimitive)
            {
                query.Replace(queryParamTmp, JsonConvert.SerializeObject(parameter));
                continue;
            }
            
            if (parameter is string)
            {
                query.Replace(queryParam, new StringBuilder(string.Empty).Append('\'')
                    .Append(JsonConvert.SerializeObject(parameter)).Append('\'')
                    .Replace("\"", string.Empty).ToString());
                continue;
            }

            if (parameter.GetType().IsGenericType || parameter.GetType().IsArray)
            {
                var listString = new StringBuilder(string.Empty).Append(JsonConvert.SerializeObject(parameter));
                listString.Replace(listString[0], '(');
                listString.Replace(listString[^1], ')');
                query.Replace(queryParam, listString.ToString());
                continue;
            }

            ReplaceInsertData(query, parameter, queryParamTmp);
        }
            
        return query.ToString();
    }

    private static void ReplaceInsertData(StringBuilder query, object parameter, string queryParam)
    {
        if (!query.ToString().StartsWith("Insert", StringComparison.InvariantCultureIgnoreCase))
        {
            throw new SqlParameterException("Param not valid");
        }

        var insertData = new StringBuilder(string.Empty);
        foreach (var propertyInfo in parameter.GetType().GetProperties())
        {
            if (insertData.Length != 0) insertData.Append(',');
            if (propertyInfo.PropertyType.IsArray || propertyInfo.PropertyType.IsGenericType)
            {
                throw new SqlParameterException($"Cannot convert data type is {propertyInfo.PropertyType}");
            }

            var propertyValue = propertyInfo.GetValue(parameter, null);
            if (propertyValue is null)
            {
                insertData.Append("null");
                continue;
            }
            if (propertyInfo.PropertyType == typeof(string))
            {
                insertData.Append('\'')
                    .Append(propertyInfo.GetValue(parameter, null))
                    .Append('\'');
                continue;
            }
            insertData.Append(propertyInfo.GetValue(parameter, null));
        }
        query.Replace(queryParam, insertData.ToString());
    }
}