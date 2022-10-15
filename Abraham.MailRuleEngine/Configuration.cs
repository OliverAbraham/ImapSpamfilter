namespace Abraham.MailRuleEngine;

public class Configuration
{
    public List<string>       SpamBlacklists                              { get; set; }
    public int                SpamQueryTimeoutInSeconds                   { get; set; }
    public List<MailAccount>  MailAccounts                                { get; set; }

    public void LogOptions(Action<string> logger)
    {
        logger($"Spam blacklist URLs               : {string.Join(',', SpamBlacklists)}");
        logger($"Accounts                          : {string.Join(',', MailAccounts.Select(x => x.Name))}");
        logger("");
    }
}                             
                              
public class MailAccount
{
    public string             Name                                        { get; set; }
    public string             ImapServer                                  { get; set; }
    public int                ImapPort                                    { get; set; }
    public string             Username                                    { get; set; }
    public string             Password                                    { get; set; }
    public string             InboxFolderName                             { get; set; }
    public string             SpamFolderName                              { get; set; }
    public List<Rule>         Rules                                       { get; set; }
}

public class Rule             
{                             
    public string             Name                                        { get; set; }
    public bool               IfMailIsSpam                                { get; set; }
    public List<string>       IfMailComesFromOneOfTheseSenders            { get; set; }
    public List<string>       IfMailWasSentToOneOfTheseReceivers          { get; set; }
    public List<string>       IfMailWasSentToOneOfTheseReceiversOnlyCc    { get; set; }
    public List<string>       IfMailContainsOneOfTheseWordsInHeader       { get; set; }
    public List<string>       IfMailContainsOneOfTheseWordsInSubject      { get; set; }
    public List<string>       IfMailContainsOneOfTheseWordsInBody         { get; set; }
    public DateTimeOffset     DateRangeFrom                               { get; set; }
    public DateTimeOffset     DateRangeTo                                 { get; set; }
    public List<FilterAction> Actions                                     { get; set; }
    public SpamfilterSettings SpamfilterSettings                          { get; set; }
}

public class FilterAction
{
    public string             Type                                        { get; set; }
    public string             Receiver                                    { get; set; }
    public string             ResponseSubject                             { get; set; }
    public string             ResponseBody                                { get; set; }
    public string             ResponseCC                                  { get; set; }
    public string             ResponseBCC                                 { get; set; }
}

public class FilterActionType
{
    public const string MoveToFolder = "MoveToFolder";
    public const string CopyToFolder = "CopyToFolder";
    public const string Delete       = "Delete";
    public const string SendTo       = "SendTo";
    public const string RespondWith  = "RespondWith";
}

public class SpamfilterSettings
{
    public bool               ReCheckEveryUnreadMessage                   { get; set; }
    public string             SpecialCharacterWhitelist                   { get; set; }
    public string             CharacterWhitelist                          { get; set; }
    public int                SpecialCharactersSenderEmailThreshold       { get; set; }
    public int                SpecialCharactersSenderNameThreshold        { get; set; }
    public int                SpecialCharactersSubjectThreshold           { get; set; }
    public string[]           SenderWhitelist                             { get; set; }
    public string[]           SenderBlacklist                             { get; set; }
    public string[]           SubjectBlacklist                            { get; set; }
}                             
