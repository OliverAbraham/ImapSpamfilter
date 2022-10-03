using Abraham.Mail;
using Abraham.ProgramSettingsManager;
using Abraham.Scheduler;
using NLog.Fluent;
using NLog.Web;

namespace ImapSpamfilter
{
    /// <summary>
    /// Console App Template
    /// 
    /// This is a simple but useful template for console apps, using 
    /// - a configuration file (hjson or json)
    /// - nlog logger, with daily log rotation
    /// - a scheduler that is able to start a method on a regular basis
    /// 
    /// AUTHOR
    /// Written by Oliver Abraham, mail@oliver-abraham.de
    /// 
    /// INSTALLATION
    /// See the README.md
    /// 
    /// SOURCE CODE
    /// https://www.github.com/OliverAbraham/Templates
    /// 
    /// 
    /// </summary>
    public class Program
    {
        public const string VERSION = "2022-10-03";

        #region ------------- Fields --------------------------------------------------------------
        private static ProgramSettingsManager<Configuration> _programSettingsManager;
        private static Configuration _config;
        private static NLog.Logger _logger;
        private static Scheduler _scheduler;
        #endregion



        #region ------------- Init ----------------------------------------------------------------
        public static void Main(string[] args)
        {
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
        private static void ReadConfiguration()
        {
            // ATTENTION: When loading fails, you probably forgot to set the properties of appsettings.hjson to "copy if newer"!
            // ATTENTION: or you have an error in your json file
            _programSettingsManager = new ProgramSettingsManager<Configuration>().Load();
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
            _logger.Debug(@"-----------------------------------------------------------------------------------------");
            _logger.Debug(@"   __  __          _____                      _                             			 ");
            _logger.Debug(@"  |  \/  |        / ____|                    | |          /\                			 ");
            _logger.Debug(@"  | \  / |_   _  | |     ___  _ __  ___  ___ | | ___     /  \   _ __  _ __  			 ");
            _logger.Debug(@"  | |\/| | | | | | |    / _ \| '_ \/ __|/ _ \| |/ _ \   / /\ \ | '_ \| '_ \ 			 ");
            _logger.Debug(@"  | |  | | |_| | | |___| (_) | | | \__ \ (_) | |  __/  / ____ \| |_) | |_) |			 ");
            _logger.Debug(@"  |_|  |_|\__, |  \_____\___/|_| |_|___/\___/|_|\___| /_/    \_\ .__/| .__/ 			 ");
            _logger.Debug(@"           __/ |                                               | |   | |    			 ");
            _logger.Debug(@"          |___/                                                |_|   |_|    			 ");
            _logger.Debug(@"                                                                                       	 ");
            _logger.Info($"MyConsoleApp started, Version {VERSION}                                                  ");
            _logger.Debug(@"-----------------------------------------------------------------------------------------");
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
                .UseIntervalSeconds(10)
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
            ImapClient? _client = null;

            try
            {
		        _logger.Debug("Reading new mails...");

		        _client = new ImapClient()
			        .UseHostname(_config.ImapServer)
			        .UsePort(_config.ImapPort)
			        .UseSecurityProtocol(Security.Ssl)
			        .UseAuthentication(_config.Username, _config.Password)
			        .Open();

		        var folders = _client.GetAllFolders().ToList();
		        var inbox = _client.GetFolderByName(folders, _config.InboxFolderName);
                if (inbox is null) 
                {
                    _logger.Error($"Error getting the inbox from your imap server");
                    return;
                }

		        var unreadEmails = _client.GetUnreadMessagesFromFolder(inbox).ToList();

		        unreadEmails.ForEach(x => Console.WriteLine($"    - {x}"));
            }
            catch (Exception ex) 
            {
                _logger.Error($"Error with the imap server: {ex}");
            }
            finally
            {
                if (_client is not null)
                    _client.Close();
            }
        }
        #endregion
    }
}