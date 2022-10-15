using NLog;

namespace ImapSpamfilter;

public class Configuration
{
    public int    CheckIntervalMinutes      { get; set; }
    public string SpamfilterRules           { get; set; }

    public void LogOptions(ILogger logger)
    {
        logger.Debug($"CheckIntervalMinutes              : {CheckIntervalMinutes     }");
        logger.Debug($"SpamfilterRules file              : {SpamfilterRules          }");
    }
}