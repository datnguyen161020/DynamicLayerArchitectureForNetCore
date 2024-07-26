using log4net;
using log4net.Config;

[assembly: XmlConfigurator(ConfigFile = "log4net.config")]
namespace DynamicLayerArchitectureForNetCore.Config.LoggerConfig;

public static class LoggerFactory
{
    private static CustomLogger? _logger;
    private static readonly object LogLock = new();
    public static CustomLogger CreateLogger(Type type)
    {
        lock (LogLock)
        {
            if (_logger != null) return _logger;
            var log = LogManager.GetLogger(type);
            BasicConfigurator.Configure(new CustomAppender());
            _logger = new CustomLogger(log.Logger);
            return _logger;

        }
    }
}