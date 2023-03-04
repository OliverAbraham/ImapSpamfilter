namespace Abraham.MailRuleEngine;

public class Configuration
{
    public List<string>?      SpamBlacklists                              { get; set; } = null;
    public int                SpamQueryTimeoutInSeconds                   { get; set; }
    public bool               ReCheckEveryUnreadMessage                   { get; set; } = false;
    public string             AiEngineTrainingFileName                    { get; set; } = "";
    public List<MailAccount>? MailAccounts                                { get; set; } = null;
    public SpamfilterSettings? GeneralSpamfilterSettings                  { get; set; } = null;

    public void LogOptions(Action<string> logger)
    {
        var urls = (SpamBlacklists is not null) ? string.Join(',', SpamBlacklists) : "";
        var accounts = (MailAccounts is not null) ? string.Join(',', MailAccounts.Select(x => x.Name)) : "";

        logger($"Spam blacklist URLs               : {urls}");
        logger($"Accounts                          : {accounts}");
        logger("");
    }
}                             
                              
public class MailAccount
{
    public string             Name                                        { get; set; } = "";
    public string             ImapServer                                  { get; set; } = "";
    public int                ImapPort                                    { get; set; } = 993;
    public string             Username                                    { get; set; } = "";
    public string             Password                                    { get; set; } = "";
	public string             SmtpServer                                  { get; set; } = "";
	public int                SmtpPort                                    { get; set; } = 465;
	public string             SmtpUsername                                { get; set; } = "";
	public string             SmtpPassword                                { get; set; } = "";
    public string             InboxFolderName                             { get; set; } = "";
    public List<Rule>         Rules                                       { get; set; } = new List<Rule>();
}

public class Rule             
{                             
    public string             Name                                        { get; set; } = "";
    public bool               IfMailIsSpam                                { get; set; }
    public List<string>       IfMailWasSentBy                             { get; set; } = new List<string>();
    public List<string>       IfMailWasSentTo                             { get; set; } = new List<string>();
    public List<string>       IfMailContainsWordsInHeader                 { get; set; } = new List<string>();
    public List<string>       IfMailContainsWordsInSubject                { get; set; } = new List<string>();
    public List<string>       IfMailContainsWordsInBody                   { get; set; } = new List<string>();
    public DateTimeOffset?    DateRangeFrom                               { get; set; } = null;
    public DateTimeOffset?    DateRangeTo                                 { get; set; } = null;
    public List<FilterAction>? Actions                                    { get; set; } = null;
    public SpamfilterSettings? SpamfilterSettings                         { get; set; } = null;
    public bool               StopAfterAction                             { get; set; } = false;
}

public class FilterAction
{
    public string             Type                                        { get; set; } = "";
    public string             Folder                                      { get; set; } = "";
    public string             Receiver                                    { get; set; } = "";
    public string             ResponseSubject                             { get; set; } = "";
    public string             ResponseBody                                { get; set; } = "";
    public string             ResponseCC                                  { get; set; } = "";
    public string             ResponseBCC                                 { get; set; } = "";
}

public class FilterActionType
{
    public const string MoveToFolder = "MoveToFolder";
    public const string CopyToFolder = "CopyToFolder";
    public const string Delete       = "Delete";
    public const string SendTo       = "SendTo";
    public const string RespondWith  = "RespondWith";
    public const string Forward      = "Forward";
    public const string MarkAsRead   = "MarkAsRead";
}

public class SpamfilterSettings
{
    public bool               ReCheckEveryUnreadMessage                   { get; set; } = false;
    public string             SpecialCharacterWhitelist                   { get; set; } = "";
    public string             CharacterWhitelist                          { get; set; } = "";
    public int                SpecialCharactersSenderEmailThreshold       { get; set; }
    public int                SpecialCharactersSenderNameThreshold        { get; set; }
    public int                SpecialCharactersSubjectThreshold           { get; set; }
    public string[]           SenderWhitelist                             { get; set; } = new string[0];
    public string[]           SenderBlacklist                             { get; set; } = new string[0];
    public string[]           SubjectBlacklist                            { get; set; } = new string[0];
}                             
