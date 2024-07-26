using System.Runtime.InteropServices;
using System.Text;
using log4net.Appender;
using log4net.Core;

namespace DynamicLayerArchitectureForNetCore.Config.LoggerConfig;

public class CustomAppender : AppenderSkeleton
{
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleOutputCP(uint wCodePageId);
        
    protected override void Append(LoggingEvent loggingEvent)
    {
        // Accessing the log event level
        var level = loggingEvent.Level;
        var loggerName = loggingEvent.LoggerName;
        var message = loggingEvent.RenderedMessage;
        var timeStamp = loggingEvent.TimeStamp;
        var threadName = loggingEvent.ThreadName;

        SetConsoleOutputCP(65001);
        Console.OutputEncoding = Encoding.UTF8;
        // Now you can use the level as needed
        var result = new StringBuilder($"{timeStamp:yyyy-MM-dd HH:mm:ss.fff} ")
            .Append($"{GetColorLeveLogging(level),-6} ")
            .Append($"\u001b[95m{threadName,-3}\u001b[0m --- ")
            .Append($"\u001b[96m[{loggerName,30}]\u001b[0m :")
            .Append($"\u001b[97m{message}\u001b[0m");
        Console.WriteLine(result);
    }

    private static string GetColorLeveLogging(Level level)
    {

        var result = new StringBuilder();
        var levelName = level.Name.PadRight(5);
        if (level == Level.Info)
        {
            return result.Append("\u001b[92m").Append(levelName).Append("\u001b[0m").ToString();
        }

        if (level == Level.Error)
        {
            return result.Append("\u001b[91m").Append(levelName).Append("\u001b[0m").ToString();
        }

        return level == Level.Warn
            ? result.Append("\u001b[93m").Append(levelName).Append("\u001b[0m").ToString()
            : level.ToString();
    }
}