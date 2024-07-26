using System.Runtime.CompilerServices;
using log4net.Core;
using Newtonsoft.Json;
using ILogger = log4net.Core.ILogger;

namespace DynamicLayerArchitectureForNetCore.Config.LoggerConfig;

public class CustomLogger : LogImpl
{
    public CustomLogger(ILogger logger) : base(logger)
    {
    }

    public new void Info(object message)
    {
        Log(Level.Info, message);
    }
        
    public void Info(object message, params object[] args)
    {
        Log(Level.Info, message, args);
    }
        
    public new void Warn(object message)
    {
        Log(Level.Warn, message);
    }
        
    public void Warn(object message, params object[] args)
    {
        Log(Level.Warn, message, args);
    }
        
    public new void Error(object message)
    {
        Log(Level.Error, message);
    }
        
    public void Error(object message, params object[] args)
    {
        Log(Level.Error, message, args);
    }

    public void Log(Level level, object message, params object[] args)
    {
        Log(level, message, null, args);
    }
        
    public void Log(Level level, object message, Exception exception, params object[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            args[index] = ConvertMessage(args[index]);
        }

        Logger.Log(typeof(CustomLogger), level, string.Format((string)message, args), exception);
    }

    public void Log(Level level, object message, Exception exception = null)
    {
        message = ConvertMessage(message);
        Logger.Log(typeof(CustomLogger), level, message, exception);
    }

    private static object ConvertMessage(object message)
    {
        if (message.GetType().IsPrimitive || message is string)
        {
            return message;
        }

        if (!IsAnonymousType(message.GetType()) 
            && (message.GetType().IsArray || message.GetType().IsGenericType) 
            && (message.GetType().GenericTypeArguments[0].IsPrimitive 
                || message.GetType().GenericTypeArguments[0] == typeof(string)))
        {
            return JsonConvert.SerializeObject(message);
        }

        return JsonConvert.SerializeObject(message, Formatting.Indented);
    }
        
    private static bool IsAnonymousType(Type type) 
    {
        var hasCompilerGeneratedAttribute = type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any();
        var nameContainsAnonymousType = type.FullName != null && type.FullName.Contains("AnonymousType");
        var isAnonymousType = hasCompilerGeneratedAttribute && nameContainsAnonymousType;

        return isAnonymousType;
    }
}