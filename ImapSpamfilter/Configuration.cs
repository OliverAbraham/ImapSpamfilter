using NLog;

namespace ImapSpamfilter;

public class Configuration
{
    public string ImapServer                { get; set; }
    public int    ImapPort                  { get; set; }
    public string Username                  { get; set; }
    public string Password                  { get; set; }
    public string InboxFolderName           { get; set; }
    public string SpamFolderName            { get; set; }
    public int    CheckIntervalMinutes      { get; set; }
    public bool   ReCheckEveryUnreadMessage { get; set; }
    public string SpamfilterRules           { get; set; }

    public void LogOptions(ILogger logger)
    {
        //Note: To align the output in columns, set visual studio to use spaces instead of tabs!
        logger.Debug($"ImapServer                        : {ImapServer               }");
        logger.Debug($"ImapPort                          : {ImapPort                 }");
        logger.Debug($"Username                          : {Username                 }");
        logger.Debug($"Password                          : ***************************");
        logger.Debug($"InboxFolderName (source folder)   : {InboxFolderName          }");
        logger.Debug($"SpamFolderName (destination)      : {SpamFolderName           }");
        logger.Debug($"CheckIntervalMinutes              : {CheckIntervalMinutes     }");
        logger.Debug($"ReCheckEveryUnreadMessage         : {ReCheckEveryUnreadMessage}");
        logger.Debug($"SpamfilterRules file              : {SpamfilterRules          }");
    }
}