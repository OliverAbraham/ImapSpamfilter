using Abraham.Mail;
using Abraham.ProgramSettingsManager;
using Abraham.Scheduler;
using NLog.Web;
using CommandLine;
using Abraham.Spamfilter;
using MailKit;
using MimeKit.Text;
using Microsoft.Extensions.Caching.Memory;

namespace ImapSpamfilter
{
    /// <summary>
    /// IMAP SPAMFILTER
    /// 
    /// This is a Spamfilter for IMAP postboxes.
    /// It works independently from yout email client.
    /// It will work with every mail server, as it need no special functionality.
    /// 
    /// 
    /// FUNCTIONING
    /// 
    /// It will connect periodcally to your imap mail server and check every new(unread) email.
    /// If it's classified as spam, it will move it from the inbox to another folder in your postbox.
    /// If you use an imap mail client on your computer, it will automatically update its content.
    /// 
    /// The filter rules are configured in a hjson file. (see my example)
    /// Its able to classify any given email by a set of rules.
    /// Rules are basic now. They are three lists and some fixed rules.
    /// The lists are: 
    ///     - a sender white list
    ///     - a sender black list 
    ///     - a subject black list
    /// 
    /// The rules are processed in the following order.
    /// An email is spam when:
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
    /// 
    /// </summary>
    public class Program
    {
        #region ------------- Fields --------------------------------------------------------------
	    private static CommandLineOptions _commandLineOptions;
        private static ProgramSettingsManager<Configuration> _programSettingsManager;
        private static Configuration _config;
        private static NLog.Logger _logger;
        private static Scheduler _scheduler;
        private static ImapClient? _client;
        private static IMailFolder _inboxFolder;
        private static IMailFolder _spamFolder;
        private static MemoryCache _alreadyCheckedEmails;
        private static MemoryCacheEntryOptions _cacheEntryOptions;

        #endregion



        #region ------------- Command line options ------------------------------------------------
        class CommandLineOptions
	    {
	        [Option('c', "config", Default = "appsettings.hjson", Required = false, HelpText = 
	            """
	            Configuration file (full path and filename).
	            If you don't specify this option, the program will expect your configuration file 
	            named 'appsettings.hjson' in your program folder.
	            You can specify a different location.
	            You can use Variables for special folders, like %APPDATA%.
	            Please refer to the documentation of my nuget package https://github.com/OliverAbraham/Abraham.ProgramSettingsManager
	            """)]
	        public string ConfigurationFile { get; set; }
	
	        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
	        public bool Verbose { get; set; }
	    }
	
	    #endregion



        #region ------------- Init ----------------------------------------------------------------
        public static void Main(string[] args)
        {
            InitCache();
	        ParseCommandLineArguments();
    	    ReadConfiguration();
            ValidateConfiguration();
            InitLogger();
            PrintGreeting();
            LogConfiguration();
            HealthChecks();
            StartScheduler();

            Run();

            StopScheduler();
        }
        #endregion



        #region ------------- Health checks -------------------------------------------------------
        private static void HealthChecks()
        {
        }
        #endregion



        #region ------------- Configuration -------------------------------------------------------
	    private static void ParseCommandLineArguments()
	    {
	        string[] args = Environment.GetCommandLineArgs();
	        CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args)
	            .WithParsed   <CommandLineOptions>(options => { _commandLineOptions = options; })
	            .WithNotParsed<CommandLineOptions>(errors  => { Console.WriteLine(errors.ToString()); });
	    }
	
	    private static void ReadConfiguration()
        {
            // ATTENTION: When loading fails, you probably forgot to set the properties of appsettings.hjson to "copy if newer"!
            // ATTENTION: or you have an error in your json file
	        _programSettingsManager = new ProgramSettingsManager<Configuration>()
            .UseFullPathAndFilename(_commandLineOptions.ConfigurationFile)
            //.UsePathRelativeToSpecialFolder(_commandLineOptions.ConfigurationFile)
            .Load();
            _config = _programSettingsManager.Data;
            Console.WriteLine($"Loaded configuration file '{_programSettingsManager.ConfigFilename}'");
        }

        private static void ValidateConfiguration()
        {
            // ATTENTION: When validating fails, you missed to enter a value for a property in your json file
            _programSettingsManager.Validate();
        }

        private static void SaveConfiguration()
        {
            _programSettingsManager.Save(_programSettingsManager.Data);
        }
        #endregion



        #region ------------- Logging -------------------------------------------------------------
        private static void InitLogger()
        {
            try
            {
                _logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing our logger. {ex.ToString()}");
                throw;  // ATTENTION: When you come here, you probably forgot to set the properties of nlog.config to "copy if newer"!
            }
        }

        /// <summary>
        /// To generate text like this, use https://onlineasciitools.com/convert-text-to-ascii-art
        /// </summary>
        private static void PrintGreeting()
        {
            _logger.Debug("");
            _logger.Debug("");
            _logger.Debug("");
            _logger.Debug(@"---------------------------------------------------------");
            _logger.Debug(@"     _____                        __ _ _ _               ");
            _logger.Debug(@"    / ____|                      / _(_) | |              ");
            _logger.Debug(@"   | (___  _ __   __ _ _ __ ___ | |_ _| | |_ ___ _ __    ");
            _logger.Debug(@"    \___ \| '_ \ / _` | '_ ` _ \|  _| | | __/ _ \ '__|   ");
            _logger.Debug(@"    ____) | |_) | (_| | | | | | | | | | | ||  __/ |      ");
            _logger.Debug(@"   |_____/| .__/ \__,_|_| |_| |_|_| |_|_|\__\___|_|      ");
            _logger.Debug(@"          | |                                            ");
            _logger.Debug(@"          |_|                                            ");
            _logger.Debug(@"                                                         ");
            _logger.Info($"Spamfilter started, Version {AppVersion.Version.VERSION}  ");
            _logger.Debug(@"---------------------------------------------------------");
        }

        private static void LogConfiguration()
        {
            _logger.Debug($"");
            _logger.Debug($"");
            _logger.Debug($"");
            _logger.Debug($"------------ 0 Configuration -------------------------------------------");
            _logger.Debug($"Loaded from file               : {_programSettingsManager.ConfigFilename}");
            _programSettingsManager.Data.LogOptions(_logger);
            _logger.Debug("");
        }
        #endregion



        #region ------------- Periodic actions ----------------------------------------------------
        private static void StartScheduler()
        {
            // set the interval to 2 seconds
            _scheduler = new Scheduler()
                .UseAction(() => PeriodicJob())
                .UseFirstStartRightNow()
                .UseIntervalMinutes(_config.CheckIntervalMinutes)
                .Start();
        }

        private static void StopScheduler()
        {
            _scheduler.Stop();
        }

        private static void PeriodicJob()
        {
            ProcessAllNewEmails();
        }
        #endregion



        #region ------------- Domain logic --------------------------------------------------------
        private static void Run()
        {
            Console.ReadKey();
        }

        private static void ProcessAllNewEmails()
        {
            _logger.Debug($"Reading and classifying unread mails from '{_config.InboxFolderName}'...");

            try
            {
                OpenPostbox();
                LoadNecessaryFolders();
                var unreadEmails = _client.GetUnreadMessagesFromFolder(_inboxFolder).ToList();
                CheckEmailsForSpam(unreadEmails);
            }
            catch (Exception ex) 
            {
                _logger.Error($"Error with the imap server: {ex}");
            }
            finally
            {
                ClosePostbox();
            }
        }

        private static void OpenPostbox()
        {
            _client = new ImapClient()
                .UseHostname(_config.ImapServer)
                .UsePort(_config.ImapPort)
                .UseSecurityProtocol(Security.Ssl)
                .UseAuthentication(_config.Username, _config.Password)
                .Open();
        }

        private static void ClosePostbox()
        {
            _client?.Close();
        }

        private static void LoadNecessaryFolders()
        {
            var folders = _client.GetAllFolders().ToList();

            _inboxFolder = _client.GetFolderByName(folders, _config.InboxFolderName);
            if (_inboxFolder is null)
                throw new Exception($"Error getting the folder named '{_config.InboxFolderName}' from your imap server");

            _spamFolder = _client.GetFolderByName(folders, _config.SpamFolderName);
            if (_spamFolder is null)
                throw new Exception($"Error getting the folder named '{_config.SpamFolderName}' from your imap server");
        }

        private static void CheckEmailsForSpam(List<Message> emails)
        {
            if (!emails.Any())
            {
                _logger.Debug("no emails");
                return;
            }

            var spamfilterConfiguration = new ProgramSettingsManager<Abraham.Spamfilter.Configuration>()
                .UseFullPathAndFilename(_config.SpamfilterRules)
                .Load()
                .Data;

            var spamfilter = new Spamfilter()
                .UseConfiguration(spamfilterConfiguration);

            foreach(var email in emails)
            {
                CheckOneEmailAndMoveItToSpamfolder(spamfilter, email);
            }
        }

        private static void CheckOneEmailAndMoveItToSpamfolder(Spamfilter spamfilter, Message email)
        {
            if (WeAlreadyCheckedThis(email))
                return;

            var senderName    = email.Msg.From.ToString();
            var senderAddress = email.Msg.From.ToString();
            var subject       = email.Msg.Subject ?? "";
            var body          = email.Msg.GetTextBody(TextFormat.Html)
                             ?? email.Msg.GetTextBody(TextFormat.Text)
                             ?? email.Msg.GetTextBody(TextFormat.Plain) 
                             ?? "";
            
            var classification = spamfilter.ClassifyEmail(subject, body, senderName, senderAddress);
            if (classification.EmailIsSpam)
            { 
                _client.MoveEmailToFolder(email, _inboxFolder, _spamFolder);
                _logger.Info ($"    - SPAM: {Format(email)}   Reason: {classification.Reason}");
            }
            else
            {
                _logger.Debug($"    - OK  : {Format(email)}");
            }
        }

        private static string Format(Message email)
        {
            var date    = email.Msg.Date   .ToLocalTime().ToString("dd.MM.yyyy  HH:mm:ss");
            var from    = email.Msg.From   .ToString().PadRight(40).Substring(0,40);
            var subject = email.Msg.Subject.ToString().PadRight(60).Substring(0,60);
            return $"{date,-22}    {from}     {subject}";
        }
 
        private static void InitCache()
        {
            _alreadyCheckedEmails = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 10_000 });
            _cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(1)
                .SetSlidingExpiration(TimeSpan.FromDays(1));
        }

        private static bool WeAlreadyCheckedThis(Message email)
        {
            // Firstly we check if we've already processed this message
            if (!_config.ReCheckEveryUnreadMessage)
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
    }
}