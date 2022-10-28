using Abraham.Mail;
using Abraham.ProgramSettingsManager;
using MailKit;
using Microsoft.Extensions.Caching.Memory;
using MimeKit.Text;
using System.Data;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Abraham.MailRuleEngine;

/// <summary>
/// MAIL FILTERING AND SPAM DETECTION ENGINE
/// 
/// This is the business logic for Spamfilter implementation.
/// 
/// 
/// FUNCTIONING
/// 
/// Its able to classify any given email by a set of rules.
/// Rules are basic now. They are three lists and some fixed rules.
/// The lists are: 
///     - a sender white list
///     - a sender black list 
///     - a subject black list
/// 
/// The rules are processed in the following order.
/// An email is spam, when:
///     - if the sender contains one of the sender blacklist words (you can whitelist all senders of a domain, e.g. "@mydomain.com")
///     - if more than half of the subject characters are non-latin (hard coded)
///     - if the sender email address contains more than a given number of special characters (configurable)
///     - if the sender address without punctuation contains more than a given number of special characters (configurable)
///     - if the subject contains one of the subject blacklist words
///
/// 
/// AUTHOR
/// Written by Oliver Abraham, mail@oliver-abraham.de
/// 
/// 
/// INSTALLATION AND CONFIGURATION
/// See README.md in the project root folder.
/// 
/// 
/// LICENSE
/// This project is licensed under Apache license.
/// 
/// 
/// SOURCE CODE
/// https://www.github.com/OliverAbraham/Spamfilter
/// </summary>
/// 
public class Spamfilter
{
    #region ------------- Properties ----------------------------------------------------------
    public Configuration Configuration { get; set; } = new Configuration();
    #endregion



    #region ------------- Fields --------------------------------------------------------------
    private class AccountDTO
    {
        public MailAccount        Account     { get; init; }
        public ImapClient         Client      { get; init; }
        public List<IMailFolder>? Folders     { get; set; } = null;
        public IMailFolder?       InboxFolder { get; set; } = null;

        public AccountDTO(MailAccount account, ImapClient client)
        {
            Account = account;
            Client = client;
        }
    }

    private MemoryCache              _alreadyCheckedEmails = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 1 });
    private MemoryCacheEntryOptions  _cacheEntryOptions    = new MemoryCacheEntryOptions();
    private Action<string>           _errorLogger          = delegate(string message){};
    private Action<string>           _warningLogger        = delegate(string message){};
    private Action<string>           _infoLogger           = delegate(string message){};
    private Action<string>           _debugLogger          = delegate(string message){};
    private SpamhausResolver?        _spamhausResolver     = null;
    private Regex                    _ipAddressRegex       = new Regex(@"\[\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b\]");
    #endregion



    #region ------------- Init ----------------------------------------------------------------
    public Spamfilter()
    {
        InitCache();
    }
    #endregion



    #region ------------- Methods -------------------------------------------------------------
    public Spamfilter UseLogger(Action<string> errorLogger, Action<string> warningLogger, Action<string> infoLogger, Action<string> debugLogger)
    {
        _errorLogger   = errorLogger;
        _warningLogger = warningLogger;
        _infoLogger    = infoLogger;
        _debugLogger   = debugLogger;
        return this;
    }

    public Spamfilter UseConfiguration(Configuration configuration)
    {
        Configuration = configuration;
        return this;
    }

    public Spamfilter LoadConfigurationFromFile(string filename)
    {
        Configuration = new ProgramSettingsManager<Configuration>()
            .UseFullPathAndFilename(filename)
            .Load()
            .Data;
        return this;
    }

    public Spamfilter InitSpamhausResolver()
    {
        InitResolver();
        return this;
    }

    public Spamfilter CheckAllRules()
    {
        try
        {
            CheckAllRules_internal();
        }
        catch (Exception ex) 
        {
            _errorLogger($"Error with the imap server: {ex}");
        }
        return this;
    }

    public void ForgetAlreadyProcessedEmails()
    {
        InitCache();
    }
    #endregion



    #region ------------- Implementation ------------------------------------------------------
    private void CheckAllRules_internal()
    {
        if (Configuration.MailAccounts is null)
            return;

        foreach(var mailAccount in Configuration.MailAccounts)
            Check(mailAccount);
    }

    private void Check(MailAccount account)
    {
        _debugLogger($"Checking account '{account.Name}'");
        AccountDTO? dto = null;

        try
        {
            dto = OpenPostbox(account);
            if (dto is null)
                throw new Exception("cannot open postbox");
            if (dto.Client is null)
                throw new Exception("cannot open postbox");

            if (dto.InboxFolder is null)
                return;
            var emails = dto.Client.GetUnreadMessagesFromFolder(dto.InboxFolder).ToList();
            if (!emails.Any())
            {
                _debugLogger("no new emails");
                return;
            }

            CheckAllEmails(emails, dto);
        }
        catch (Exception ex)
        {
            _errorLogger($"Spamcheck could NOT be done for postbox {account.Name}. More info: {ex}");
        }
        finally
        {
            if (dto is not null && dto.Client is not null)
                ClosePostbox(dto.Client);
        }
    }

    private void CheckAllEmails(List<Message> emails, AccountDTO dto)
    {
        foreach (var email in emails)
            CheckEmail(email, dto);
    }

    private void CheckEmail(Message email, AccountDTO dto)
    {
        if (WeAlreadyCheckedThis(email, Configuration.ReCheckEveryUnreadMessage))
            return;

        foreach (var rule in dto.Account.Rules)
        {
            var oneActionWasDone = CheckRule(rule, email, dto);
            
            if (rule.StopAfterAction && oneActionWasDone)
                break;
        }
    }

    private bool CheckRule(Rule rule, Message email, AccountDTO dto)
    {
        var oneActionWasDone = ProcessGeneralRule(rule, email, dto);

        if (!oneActionWasDone)
            _debugLogger(FormatResult("OK", rule, email, "", ""));

        return oneActionWasDone;
    }

    private string FormatResult(string actionCode, Rule rule, Message email, string action, string reasons)
    {
        return $"    - {FormatActionCode(actionCode)}: {FormatEmail(email)}   {FormatRule(rule)}   {FormatComment(action, reasons)}";
    }

    private string FormatActionCode(string actionCode)
    {
        return actionCode.PadRight(9).Substring(0,9);
    }

    private string FormatEmail(Message email)
    {
        var date    = email.Msg.Date   .ToLocalTime().ToString("dd.MM.yyyy  HH:mm:ss");
        var from    = email.Msg.From   .ToString().PadRight(40).Substring(0,40);
        var subject = email.Msg.Subject.ToString().PadRight(60).Substring(0,60);
        return $"{date,-22}    {from,-40}     {subject,-40}";
    }

    private string FormatRule(Rule rule)
    {
        return "Rule: " + rule.Name.PadRight(30).Substring(0,30);
    }

    private string FormatComment(string action, string reasons)
    {
        var comments = action;
        if (!string.IsNullOrWhiteSpace(reasons))
            comments += $", because {reasons}";
        return comments;
    }
    #endregion



    #region ------------- General rule ------------------------------------------------------------
    private bool ProcessGeneralRule(Rule rule, Message email, AccountDTO dto)
    {
        var ipAddresses   = ExtractReceivedFromIPAddresses(email);
        var senderName    = email.Msg.From.ToString();
        var senderAddress = email.Msg.From.ToString();
        var receiver      = email.Msg.To.ToString();
        var subject       = email.Msg.Subject ?? "";
        var body          = email.Msg.GetTextBody(TextFormat.Html)
                         ?? email.Msg.GetTextBody(TextFormat.Text)
                         ?? email.Msg.GetTextBody(TextFormat.Plain)
                         ?? "";
        var headers       = email.Msg.Headers.Select(x => x.Value.ToLower()).ToList();
        var allHeaders    = string.Join(' ', headers);
        var reasons       = "";

        if (rule.IfMailIsSpam)
            if (!MailContainsSpam(rule, ipAddresses, senderName, senderAddress, subject, body, ref reasons))
                return false;

        if (!MailContainsOneOfTheWordInList(rule.IfMailWasSentBy, senderAddress, "sender", ref reasons))
            return false;

        if (!MailContainsOneOfTheWordInList(rule.IfMailWasSentTo, receiver, "receiver", ref reasons))
            return false;

        if (!MailContainsOneOfTheWordInList(rule.IfMailContainsWordsInHeader, allHeaders, "header", ref reasons))
            return false;

        if (!MailContainsOneOfTheWordInList(rule.IfMailContainsWordsInSubject, subject, "subject", ref reasons))
            return false;

        if (!MailContainsOneOfTheWordInList(rule.IfMailContainsWordsInBody, body, "body", ref reasons))
            return false;

        if (reasons == "")
            reasons = ", the rule has no condition";
        reasons = reasons.TrimStart(new char[] { ',', ' ' });

        var oneActionWasDone = ExecuteAllRuleActions(rule, email, dto, reasons);
        return oneActionWasDone;
    }

    private bool MailContainsOneOfTheWordInList(List<string> wordList, string part, string partName, ref string reasons)
    {
        if (wordList is null || wordList.Count == 0)
            return true;
        
        var found = "";
        foreach(var word in wordList)
        {
            if (part.ToLower().Contains(word.ToLower()))
            {
                found = $", contains '{word.ToLower()}' in {partName}";
                reasons += found;
                return true;
            }
        }
        return false;
    }

    private bool MailContainsSpam(Rule rule, List<IPAddress> ipAddresses, string senderName,
                                  string senderAddress, string subject, string body, ref string reasons)
    {
        var specificSettingsAreSetForThisAccount =
            rule.SpamfilterSettings is not null &&
            !string.IsNullOrWhiteSpace(rule.SpamfilterSettings.SpecialCharacterWhitelist);

        var spamfilterSettings = specificSettingsAreSetForThisAccount
            ? rule.SpamfilterSettings
            : Configuration.GeneralSpamfilterSettings;

        if (spamfilterSettings is null)
        {
            _errorLogger($"Cannot do spam check. No spamfiler rules are set!");
            return false;
        }

        if (spamfilterSettings is null)
        {
            _errorLogger($"Cannot do spam check. No general spamfiler rules are set!");
            return false;
        }

        var classification = ClassifyEmail(ipAddresses, subject, body, senderName, senderAddress, spamfilterSettings);
        
        if (classification.EmailIsSpam)
        {
            reasons += $"mail is SPAM ({classification.Reason})";
            return true;
        }
        return false;
    }

    private List<IPAddress> ExtractReceivedFromIPAddresses(Message email)
    {
        var results = new List<IPAddress>();

        var receivedHeaders = email.Msg.Headers.Where(x => x.Id == MimeKit.HeaderId.Received);
        var senderReceivedHeaders = receivedHeaders.Select(x => x.GetValue(Encoding.UTF8)).ToList();

        foreach(var header in senderReceivedHeaders)
        {
            var ip = ExtractIpAddressFromHeader(header);
            if (ip is not null)
                results.Add(ip);
        }

        return results;
    }

    private IPAddress? ExtractIpAddressFromHeader(string header)
    {
        var matches = _ipAddressRegex.Matches(header);
        if (matches is null || !matches.Any())
            return null;

        var firstMatch = matches.First().Value;
        var ipString = firstMatch.TrimStart('[').TrimEnd(']');
        if (IPAddress.TryParse(ipString, out var ipAddress))
            return ipAddress;
        else
            return null; 
    }

    private void InitCache()
    {
        _alreadyCheckedEmails = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 10_000 });
        _cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetSlidingExpiration(TimeSpan.FromDays(1));
    }

    private bool WeAlreadyCheckedThis(Message email, bool recheckEveryUnreadMessage)
    {
        // Firstly we check if we've already processed this message
        if (!recheckEveryUnreadMessage)
        {
            // We build a unique ID to recognize previously checked emails.
            // In case the imap server doesn't give us an id, we take the date.
            var id = email.Msg.MessageId ?? email.Msg.Date.ToLocalTime().ToString(); 

            if (_alreadyCheckedEmails.TryGetValue(id, out object element))
                return true;
            _alreadyCheckedEmails.Set(id, "", _cacheEntryOptions);
        }
        return false;
    }
    #endregion



    #region ------------- Rule actions ------------------------------------------------------------
    private bool ExecuteAllRuleActions(Rule rule, Message email, AccountDTO dto, string reasons)
    {
        if (rule.Actions is null)
            return false;

        bool oneActionWasDone = false;

        foreach(var action in rule.Actions)
            oneActionWasDone |= ExecuteRuleAction(action, rule, email, dto, reasons);

        return oneActionWasDone;
    }

    private bool ExecuteRuleAction(FilterAction action, Rule rule, Message email, AccountDTO dto, string reasons)
    {
        if (action.Type == FilterActionType.MoveToFolder) 
            return MoveEmailToFolder(action, rule, email, dto, reasons);

        if (action.Type == FilterActionType.Forward) 
            return ForwardEmail(action, rule, email, dto, reasons);

        if (action.Type == FilterActionType.MarkAsRead) 
            return MarkAsRead(action, rule, email, dto, reasons);

        return false;
    }

    private bool MoveEmailToFolder(FilterAction action, Rule rule, Message email, AccountDTO dto, string reasons)
    {
        if (action?.Folder is null)
        {
            _errorLogger($"Cannot move the email, because no folder was given. Please check your rule action.");
            return false;
        }
        
        if (dto.Folders is null)
        {
            _errorLogger($"Cannot move the email, Mailbox doesn't seem to contain any folder. Please check your mailbox.");
            return false;
        }
        
        if (dto.InboxFolder is null)
        {
            _errorLogger($"Cannot move the email, the inbox folder name wasn't set. Please check your account settings.");
            return false;
        }        
        
        try
        {
            var folder = dto.Client.GetFolderByName(dto.Folders, action.Folder);
            if (folder is null)
                throw new Exception();

            dto.Client.MoveEmailToFolder(email, dto.InboxFolder, folder);
            _infoLogger(FormatResult("MOVED", rule, email, $"moved to folder '{folder}'", reasons));
            return true;
        }
        catch (Exception ex) 
        {
            _errorLogger($"Cannot move the email, because the folder named '{action.Folder}' does not exist. Please check your rule action. More info: {ex}");
            throw;
        }
    }

    private bool ForwardEmail(FilterAction action, Rule rule, Message email, AccountDTO dto, string reason)
    {
        if (string.IsNullOrWhiteSpace(action.Receiver))
        {
            _errorLogger($"Cannot forward the email, because no receiver was set. Please check your rule action.");
            return false;
        }
        
        try
        {
		    var _client = new Abraham.Mail.SmtpClient()
			    .UseHostname(dto.Account.SmtpServer)
                .UsePort(dto.Account.SmtpPort)
			    .UseSecurityProtocol(Security.Ssl)
			    .UseAuthentication(dto.Account.SmtpUsername, dto.Account.SmtpPassword)
			    .Open();

            var subject       = email.Msg.Subject ?? "";
            var body          = email.Msg.GetTextBody(TextFormat.Html)
                             ?? email.Msg.GetTextBody(TextFormat.Text)
                             ?? email.Msg.GetTextBody(TextFormat.Plain)
                             ?? "";
            var attachments   = email.Msg.Attachments.ToList();

    		_client.SendEmail(dto.Account.Name, action.Receiver, "FW: " + subject, body, attachments);

            _infoLogger(FormatResult("FORWARDED", rule, email, $"forwarded to '{action.Receiver}'", reason));
            return true;
        }
        catch (Exception ex) 
        {
            _errorLogger($"Cannot move the email, because the folder named '{action.Folder}' does not exist. Please check your rule action. More info: {ex}");
            throw;
        }
    }

    private bool MarkAsRead(FilterAction action, Rule rule, Message email, AccountDTO dto, string reasons)
    {
        try
        {
            dto.Client.MarkAsRead(email, dto.InboxFolder);
            _infoLogger(FormatResult("MARKEDREAD", rule, email, $"marked as read", reasons));
            return true;
        }
        catch (Exception ex) 
        {
            _errorLogger($"Cannot mark the email as read. More info: {ex}");
            throw;
        }
    }
    #endregion



    #region ------------- IMAP postbox ------------------------------------------------------------
    private AccountDTO OpenPostbox(MailAccount account)
    {
        AccountDTO? dto = null;
        try
        {
            var client = new ImapClient()
                .UseHostname(account.ImapServer)
                .UsePort(account.ImapPort)
                .UseSecurityProtocol(Security.Ssl)
                .UseAuthentication(account.Username, account.Password)
                .Open();
            if (client is null)
            {
                _errorLogger($"Error opening the connection to postbox {account.ImapServer}.");
                throw new Exception();
            }
            dto = new AccountDTO(account, client);
        }
        catch (Exception ex) 
        {
            _errorLogger($"Error opening the connection to postbox {account.ImapServer}. More Info: {ex}");
            throw;
        }

        try
        {
            dto.Folders = dto.Client?.GetAllFolders()?.ToList();
            if (dto.Folders is null)
                throw new Exception();
        }
        catch (Exception ex) 
        {
            _errorLogger($"Error opening the connection to postbox {account.ImapServer}. More Info: {ex}");
            throw;
        }

        try
        {
            dto.InboxFolder = dto.Client?.GetFolderByName(dto.Folders, account.InboxFolderName);
            if (dto.InboxFolder is null)
                throw new Exception(FormatErrorMessage(account.InboxFolderName, dto.Folders));
        }
        catch (Exception) 
        {
            _errorLogger(FormatErrorMessage(account.InboxFolderName, dto.Folders));
            throw;
        }

        return dto;
    }

    private static string FormatErrorMessage(string name, List<IMailFolder> folders)
    {
        var allImapFolders = string.Join(',', folders.Select(x => x.Name));
        return $"Error getting the folder named '{name}' from your imap server. Existing folders are: {allImapFolders}";
    }

    private void ClosePostbox(ImapClient? client)
    {
        client?.Close();
    }
    #endregion



    #region ------------- Spam filter -------------------------------------------------------------
    private Classification ClassifyEmail(List<IPAddress> senderIpAddresses, string subject, string body, string senderName, string senderEMail, SpamfilterSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        if (subject is null)
            subject = "";
        if (body is null)
            body = "";
        if (senderName is null)
            senderName = "";
        if (senderEMail is null)
            senderEMail = "";

        var senderNameWithoutPunctuation = RemoveAllPunctuationCharactersFrom(senderName);
        var subjectWithoutPunctuation    = RemoveAllPunctuationCharactersFrom(subject);

        var senderEmailAllLower = senderEMail.ToLower();
        foreach(var sender in settings.SenderWhitelist)
        {
            if (senderEmailAllLower.Contains(sender.ToLower()))
                return new Classification(false, $"Sender on whitelist: {senderEMail}");
        }

        if (senderIpAddresses.Any())
        {
            foreach(var ip in senderIpAddresses) 
            {
                (var blacklisted, var list) = IpIsBlacklistedbySpamhaus(ip);
                if (blacklisted)
                    return new Classification(true, $"Sender IP is blacklisted: {ip} on list {list}");
            }
        }

        // If more than half of the characters are non-latin, this is spam
        var countTotal              = subject.Length;
        var countNonLatinCharacters = CalculateNonWhiteListCharacterCount(subject, settings);
        if (countNonLatinCharacters >= countTotal/2)
        {
            return new Classification(true, $"Too many unallowed special characters in subject ({countNonLatinCharacters} of {countTotal})");
        }

        var specialCharactersSenderEmail = CalculateSpecialCharacterCount(senderEMail, settings);
        if (specialCharactersSenderEmail.Count > settings.SpecialCharactersSenderEmailThreshold)
        {
            return new Classification(true, $"Too many unallowed special characters in sender email ({specialCharactersSenderEmail.Details})");
        }

        var specialCharactersSenderName1 = CalculateSpecialCharacterCount(senderName, settings);
        if (specialCharactersSenderName1.Count > settings.SpecialCharactersSenderNameThreshold)
        {
            return new Classification(true, $"Too many unallowed special characters in sender name ({specialCharactersSenderName1.Details})");
        }

        var specialCharactersSenderName2 = CalculateSpecialCharacterCount(senderNameWithoutPunctuation, settings);
        if (specialCharactersSenderName2.Count > settings.SpecialCharactersSenderNameThreshold)
        {
            return new Classification(true, $"Too many unallowed special characters in sender name ({specialCharactersSenderName2.Details})");
        }

        var specialCharactersSubject1 = CalculateSpecialCharacterCount(subject, settings);
        if (specialCharactersSubject1.Count > settings.SpecialCharactersSubjectThreshold)
        {
            return new Classification(true, $"Too many unallowed special characters in subject ({specialCharactersSubject1.Details})");
        }

        var specialCharactersSubject2 = CalculateSpecialCharacterCount(subjectWithoutPunctuation, settings);
        if (specialCharactersSubject2.Count > settings.SpecialCharactersSubjectThreshold)
        {
            return new Classification(true, $"Too many unallowed special characters in subject ({specialCharactersSubject2.Details})");
        }

        var senderNameAllLower = senderName.ToLower();
        foreach (var blacklistWord in settings.SenderBlacklist)
        {
            if (senderNameAllLower.Contains(blacklistWord.ToLower()))
            {
                return new Classification(true, $"Sender contains a blacklisted word ('{blacklistWord}' in '{senderNameAllLower}')");
            }
        }

        if (subject != null)
            foreach (var blacklistWord in settings.SubjectBlacklist)
            {
                if (subject.ToLower().Contains(blacklistWord.ToLower()))
                {
                    return new Classification(true, $"Subject contains a blacklisted word ('{blacklistWord}' in '{subject}')");
                }
            }

        return new Classification(false, "");
    }

    private string RemoveAllPunctuationCharactersFrom(string name)
    {
        if (name is null)
            return "";

        var result = "";
        foreach (var character in name)
        {
            if (!char.IsPunctuation(character) && character != ' ' && character != '\t')
                result += character;
        }
        return result;
    }

    private AnalysisResult CalculateSpecialCharacterCount(string text, SpamfilterSettings settings)
    {
        var countPerCharacter = new Dictionary<char, int>();

        if (text != null)
            foreach (var character in text)
            {
                if (ThisCharacterIsSpecialCharacter(character, settings))
                    AddTheCountForThisCharacter(countPerCharacter, character);
            }

        var result = ConvertDictionaryToAnalysisResult(countPerCharacter);
        return result;
    }

    private AnalysisResult ConvertDictionaryToAnalysisResult(Dictionary<char, int> countPerCharacter)
    {
        var result = new AnalysisResult();
        result.Count = countPerCharacter.Values.Any() ? countPerCharacter.Values.Max() : 0;

        result.Details = "";
        foreach (var character in countPerCharacter.Keys)
            result.Details += $"{countPerCharacter[character]}x {character}, ";

        result.Details = result.Details.TrimEnd(',');
        return result;
    }

    private void AddTheCountForThisCharacter(Dictionary<char, int> countPerCharacter, char character)
    {
        if (countPerCharacter.ContainsKey(character))
            countPerCharacter[character] = countPerCharacter[character] + 1;
        else
            countPerCharacter.Add(character, 1);
    }

    private bool ThisCharacterIsSpecialCharacter(char character, SpamfilterSettings settings)
    {
        return !settings.SpecialCharacterWhitelist.Contains(character);
    }

    private int CalculateNonWhiteListCharacterCount(string text, SpamfilterSettings settings)
    {
        int count = 0;
        foreach(var character in text)
        {
            if (!settings.CharacterWhitelist.Contains(character))
            count++;
        }
        return count;
    }
    #endregion



    #region ------------- Spamhaus Resolver -------------------------------------------------------
    private void InitResolver()
    {
        _debugLogger("Initializing Spamhaus resolver");

        _spamhausResolver = new SpamhausResolver()
            .UseQueryTimeout(TimeSpan.FromSeconds(Configuration.SpamQueryTimeoutInSeconds))
            .Initialize();
        
        _debugLogger("Initialized.");
    }

    private (bool,string) IpIsBlacklistedbySpamhaus(IPAddress ip)
    {
        if (_spamhausResolver is null)
            return (false, "");
        var result = _spamhausResolver.IsBlockedAsync(ip).GetAwaiter().GetResult();
        return (result != null, result ?? "");
    }
    #endregion
}
