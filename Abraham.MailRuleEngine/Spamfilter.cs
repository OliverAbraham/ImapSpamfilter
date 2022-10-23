using Abraham.Mail;
using Abraham.ProgramSettingsManager;
using MailKit;
using Microsoft.Extensions.Caching.Memory;
using MimeKit.Text;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Abraham.MailRuleEngine;

/// <summary>
/// SPAMFILTER ENGINE
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
    public Configuration Configuration { get; set; }
    #endregion



    #region ------------- Fields --------------------------------------------------------------
    private IMailFolder _inboxFolder;
    private IMailFolder _spamFolder;
    private MemoryCache _alreadyCheckedEmails;
    private MemoryCacheEntryOptions _cacheEntryOptions;
    private Action<string> _errorLogger;
    private Action<string> _warningLogger;
    private Action<string> _infoLogger;
    private Action<string> _debugLogger;
    private SpamhausResolver _spamhausResolver;
    private Regex _ipAddressRegex = new Regex(@"\[\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b\]");
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
        foreach(var mailAccount in Configuration.MailAccounts)
            Check(mailAccount, Configuration.GeneralSpamfilterSettings);
    }

    private void Check(MailAccount account, SpamfilterSettings generalSpamfilterSettings)
    {
        _debugLogger($"Checking account '{account.Name}'");
        foreach(var rule in account.Rules)
            Check(rule, account, generalSpamfilterSettings);
    }

    private void Check(Rule rule, MailAccount account, SpamfilterSettings generalSpamfilterSettings)
    {
        _debugLogger($"Checking rule    '{rule.Name}'");
        if (rule.IfMailIsSpam)
            CheckForSpam(rule, account, generalSpamfilterSettings);
    }

    private void CheckForSpam(Rule rule, MailAccount account, SpamfilterSettings generalSpamfilterSettings)
    {
        var specificSettingsAreSetForThisAccount = 
            rule.SpamfilterSettings is not null && 
            !string.IsNullOrWhiteSpace(rule.SpamfilterSettings.SpecialCharacterWhitelist);

        var spamfilterSettings = specificSettingsAreSetForThisAccount
            ? rule.SpamfilterSettings 
            : generalSpamfilterSettings;

        ImapClient? client = null;

        try
        {
            _debugLogger($"Spamcheck for    '{account.InboxFolderName}'");
            client = OpenPostbox(account);
            
            var emails = client.GetUnreadMessagesFromFolder(_inboxFolder).ToList();
            if (!emails.Any())
            {
                _debugLogger("no new emails");
                return;
            }

            foreach (var email in emails)
            {
                CheckOneEmailAndMoveItToSpamfolder(client, rule, email, spamfilterSettings);
            }
        }
        catch (Exception ex)
        {
            _errorLogger($"Spamcheck could NOT be done for postbox {account.Name}");
        }
        finally
        {
            ClosePostbox(client);
        }
    }

    private void CheckOneEmailAndMoveItToSpamfolder(ImapClient client, Rule rule, Message email, SpamfilterSettings spamfilterSettings)
    {
        if (WeAlreadyCheckedThis(email, spamfilterSettings))
            return;

        var ipAddresses   = ExtractReceivedFromIPAddresses(email);
        var senderName    = email.Msg.From.ToString();
        var senderAddress = email.Msg.From.ToString();
        var subject       = email.Msg.Subject ?? "";
        var body          = email.Msg.GetTextBody(TextFormat.Html)
                            ?? email.Msg.GetTextBody(TextFormat.Text)
                            ?? email.Msg.GetTextBody(TextFormat.Plain)
                            ?? "";

        var classification = ClassifyEmail(ipAddresses, subject, body, senderName, senderAddress, spamfilterSettings);
        if (classification.EmailIsSpam)
        {
            client.MoveEmailToFolder(email, _inboxFolder, _spamFolder);
            _infoLogger($"    - SPAM: {Format(email)}   Reason: {classification.Reason}");
        }
        else
        {
            _debugLogger($"    - OK  : {Format(email)}   Reason: {classification.Reason}");
        }
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

    private IPAddress ExtractIpAddressFromHeader(string header)
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

    private string Format(Message email)
    {
        var date    = email.Msg.Date   .ToLocalTime().ToString("dd.MM.yyyy  HH:mm:ss");
        var from    = email.Msg.From   .ToString().PadRight(40).Substring(0,40);
        var subject = email.Msg.Subject.ToString().PadRight(60).Substring(0,60);
        return $"{date,-22}    {from}     {subject}";
    }

    private void InitCache()
    {
        _alreadyCheckedEmails = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 10_000 });
        _cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetSlidingExpiration(TimeSpan.FromDays(1));
    }

    private bool WeAlreadyCheckedThis(Message email, SpamfilterSettings settings)
    {
        // Firstly we check if we've already processed this message
        if (!settings.ReCheckEveryUnreadMessage)
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



    #region ------------- IMAP postbox --------------------------------------------------------
    private ImapClient OpenPostbox(MailAccount account)
    {
        ImapClient client = null;
        try
        {
            client = new ImapClient()
                .UseHostname(account.ImapServer)
                .UsePort(account.ImapPort)
                .UseSecurityProtocol(Security.Ssl)
                .UseAuthentication(account.Username, account.Password)
                .Open();
        }
        catch (Exception ex) 
        {
            _errorLogger($"Error opening the connection to postbox {account.ImapServer}. More Info: {ex}");
        }

        List<IMailFolder> folders = null;
        try
        {
            folders = client?.GetAllFolders()?.ToList();
        }
        catch (Exception ex) 
        {
            _errorLogger($"Error opening the connection to postbox {account.ImapServer}. More Info: {ex}");
            throw;
        }

        try
        {
            _inboxFolder = client?.GetFolderByName(folders, account.InboxFolderName);
            if (_inboxFolder is null)
                throw new Exception(FormatErrorMessage(account.InboxFolderName, folders));
        }
        catch (Exception ex) 
        {
            _errorLogger(FormatErrorMessage(account.InboxFolderName, folders));
            throw;
        }

        try
        {
            _spamFolder = client.GetFolderByName(folders, account.SpamFolderName);
            if (_spamFolder is null)
                throw new Exception(FormatErrorMessage(account.SpamFolderName, folders));
        }
        catch (Exception ex) 
        {
            _errorLogger(FormatErrorMessage(account.SpamFolderName, folders));
            throw;
        }

        return client;
    }

    private static string FormatErrorMessage(string name, List<IMailFolder> folders)
    {
        return $"Error getting the folder named '{name}' from your imap server. Existing folders are: {string.Join(',', folders.Select(x => x.Name))}";
    }

    private void ClosePostbox(ImapClient? client)
    {
        client?.Close();
    }
    #endregion



    #region ------------- Spam filter ---------------------------------------------------------
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
                    return new Classification(true, $"Sender is blacklisted: {ip} on list {list}");
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
        if (name == null)
            return name;

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



    #region ------------- Spamhaus Resolver ---------------------------------------------------
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
        var result = _spamhausResolver.IsBlockedAsync(ip).GetAwaiter().GetResult();
        return (result != null, result ?? "");
    }
    #endregion
}
