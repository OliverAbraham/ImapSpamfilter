using Abraham.Mail;
using Abraham.ProgramSettingsManager;
using MailKit;
using Microsoft.Extensions.Caching.Memory;
using MimeKit.Text;
using NLog;

namespace Abraham.MailRuleEngine
{
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
	public class Spamfilter
	{
		#region ------------- Properties ----------------------------------------------------------
		public Configuration Configuration { get; set; }
		#endregion



		#region ------------- Fields --------------------------------------------------------------
        private static Configuration _spamfilterConfiguration;
        private static IMailFolder _inboxFolder;
        private static IMailFolder _spamFolder;
        private static MemoryCache _alreadyCheckedEmails;
        private static MemoryCacheEntryOptions _cacheEntryOptions;
        private static ILogger _logger;
        #endregion



        #region ------------- Init ----------------------------------------------------------------
        public Spamfilter()
        {
            InitCache();
        }
		#endregion



		#region ------------- Methods -------------------------------------------------------------
        public Spamfilter UseLogger(ILogger logger)
        {
            _logger = logger;
            return this;
        }

        public Spamfilter UseConfiguration(Configuration configuration)
        {
            Configuration = configuration;
            return this;
        }

        public Spamfilter LoadConfigurationFromFile(string filename)
        {
            _spamfilterConfiguration = new ProgramSettingsManager<Configuration>()
                .UseFullPathAndFilename(filename)
                .Load()
                .Data;
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
                _logger.Error($"Error with the imap server: {ex}");
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
            foreach(var mailAccount in _spamfilterConfiguration.MailAccounts)
                Check(mailAccount);
        }

        private void Check(MailAccount account)
        {
            _logger.Debug($"{account.Username}");
            foreach(var rule in account.Rules)
                Check(rule, account);
        }

        private void Check(Rule rule, MailAccount account)
        {
            if (rule.IfMailIsSpam)
                CheckForSpam(rule, account);
        }

        private void CheckForSpam(Rule rule, MailAccount account)
        {
            ImapClient? client = null;

            try
            {
                _logger.Debug($"Check for Spam mails in folder '{account.InboxFolderName}'...");
                client = OpenPostbox(account);
                
                var emails = client.GetUnreadMessagesFromFolder(_inboxFolder).ToList();
                if (!emails.Any())
                {
                    _logger.Debug("no new emails");
                    return;
                }

                foreach (var email in emails)
                {
                    CheckOneEmailAndMoveItToSpamfolder(client, rule, email);
                }
            }
            finally
            {
                ClosePostbox(client);
            }
        }

        private void CheckOneEmailAndMoveItToSpamfolder(ImapClient client, Rule rule, Message email)
        {
            if (WeAlreadyCheckedThis(email, rule.SpamfilterSettings))
                return;

            var senderName    = email.Msg.From.ToString();
            var senderAddress = email.Msg.From.ToString();
            var subject       = email.Msg.Subject ?? "";
            var body          = email.Msg.GetTextBody(TextFormat.Html)
                             ?? email.Msg.GetTextBody(TextFormat.Text)
                             ?? email.Msg.GetTextBody(TextFormat.Plain) 
                             ?? "";
            
            var classification = ClassifyEmail(subject, body, senderName, senderAddress, rule.SpamfilterSettings);
            if (classification.EmailIsSpam)
            { 
                client.MoveEmailToFolder(email, _inboxFolder, _spamFolder);
                _logger.Info ($"    - SPAM: {Format(email)}   Reason: {classification.Reason}");
            }
            else
            {
                _logger.Debug($"    - OK  : {Format(email)}   Reason: {classification.Reason}");
            }
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
            var client = new ImapClient()
                .UseHostname(account.ImapServer)
                .UsePort(account.ImapPort)
                .UseSecurityProtocol(Security.Ssl)
                .UseAuthentication(account.Username, account.Password)
                .Open();

            var folders = client.GetAllFolders().ToList();

            _inboxFolder = client.GetFolderByName(folders, account.InboxFolderName);
            if (_inboxFolder is null)
                throw new Exception($"Error getting the folder named '{account.InboxFolderName}' from your imap server");

            _spamFolder = client.GetFolderByName(folders, account.SpamFolderName);
            if (_spamFolder is null)
                throw new Exception($"Error getting the folder named '{account.SpamFolderName}' from your imap server");

            return client;
        }

        private void ClosePostbox(ImapClient? client)
        {
            client?.Close();
        }
        #endregion



		#region ------------- Spam filter ---------------------------------------------------------
		private Classification ClassifyEmail(string subject, string body, string senderName, string senderEMail, SpamfilterSettings settings)
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

            if (settings.SenderWhitelist.Contains(senderEMail.ToLower()))
            {
                return new Classification(false, $"Sender white list contains the exact sender name ({senderEMail})");
            }

            var senderEmailAllLower = senderEMail.ToLower();
            foreach(var sender in settings.SenderWhitelist)
            {
                if (senderEmailAllLower.Contains(sender.ToLower()))
                    return new Classification(false, $"Sender white list contains a part of this sender ({senderEMail})");
            }

            // If more than half of the characters are non-latin, this is spam
            var countTotal              = subject.Length;
            var countNonLatinCharacters = CalculateNonWhiteListCharacterCount(subject, settings);
            if (countNonLatinCharacters >= countTotal/2)
            {
                return new Classification(true, $"{countNonLatinCharacters} of {countTotal} characters are non-latin in subject");
            }

            var specialCharactersSenderEmail = CalculateSpecialCharacterCount(senderEMail, settings);
            if (specialCharactersSenderEmail.Count > settings.SpecialCharactersSenderEmailThreshold)
            {
                return new Classification(true, $"{specialCharactersSenderEmail.Details} in sender email");
            }

            var specialCharactersSenderName1 = CalculateSpecialCharacterCount(senderName, settings);
            if (specialCharactersSenderName1.Count > settings.SpecialCharactersSenderNameThreshold)
            {
                return new Classification(true, $"{specialCharactersSenderName1.Details} in sender name");
            }

            var specialCharactersSenderName2 = CalculateSpecialCharacterCount(senderNameWithoutPunctuation, settings);
            if (specialCharactersSenderName2.Count > settings.SpecialCharactersSenderNameThreshold)
            {
                return new Classification(true, $"{specialCharactersSenderName2.Details} in sender name");
            }

            var specialCharactersSubject1 = CalculateSpecialCharacterCount(subject, settings);
            if (specialCharactersSubject1.Count > settings.SpecialCharactersSubjectThreshold)
            {
                return new Classification(true, $"{specialCharactersSubject1.Details}");
            }

            var specialCharactersSubject2 = CalculateSpecialCharacterCount(subjectWithoutPunctuation, settings);
            if (specialCharactersSubject2.Count > settings.SpecialCharactersSubjectThreshold)
            {
                return new Classification(true, $"{specialCharactersSubject2.Details}");
            }

            var senderNameAllLower = senderName.ToLower();
            foreach (var blacklistWord in settings.SenderBlacklist)
            {
                if (senderNameAllLower.Contains(blacklistWord.ToLower()))
                {
                    return new Classification(true, $"the sender name contains the blacklisted word '{blacklistWord}'");
                }
            }

            if (subject != null)
                foreach (var blacklistWord in settings.SubjectBlacklist)
                {
                    if (subject.ToLower().Contains(blacklistWord.ToLower()))
                    {
                        return new Classification(true, $"the subject contains the blacklisted word '{blacklistWord}'");
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
    }
}
