﻿using Abraham.ProgramSettingsManager;
using Abraham.Scheduler;
using NLog.Web;
using CommandLine;
using Abraham.MailRuleEngine;

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
    /// An email is spam:
    ///     - if the sender is blacklisted by spamhaus
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
	    private static CommandLineOptions                    _commandLineOptions                = new CommandLineOptions();
        private static ProgramSettingsManager<Configuration> _programSettingsManager            = new ProgramSettingsManager<Configuration>();
        private static Configuration                         _config                            = new();
        private static NLog.Logger                           _logger                            = NLogBuilder.ConfigureNLog("").GetCurrentClassLogger();
        private static Scheduler?                            _scheduler;
        private static DateTime                              _spamfilterConfigFileLastWriteTime = default(DateTime);
        private static Spamfilter?                           _spamfilter;
        private static bool                                  _thisIsTheFirstTime                = true;
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
	        public string ConfigurationFile { get; set; } = "";

	        [Option('n', "nlogconfig", Default = "nlog.config", Required = false, HelpText = 
	            """
	            NLOG Configuration file (full path and filename).
	            If you don't specify this option, the program will expect your configuration file 
	            named 'nlog.config' in your program folder.
	            You can specify a different location.
	            """)]
            public string NlogConfigurationFile { get; set; } = "";
	
	        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
	        public bool Verbose { get; set; }
	    }
	
	    #endregion



        #region ------------- Init ----------------------------------------------------------------
        public static void Main(string[] args)
        {
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
            
            if (_commandLineOptions is null)
                throw new Exception();
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
                _logger = NLogBuilder.ConfigureNLog(_commandLineOptions.NlogConfigurationFile).GetCurrentClassLogger();
                if (_logger is null)
                    throw new Exception();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing our logger with the configuration file {_commandLineOptions.NlogConfigurationFile}. More info: {ex}");
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
            _logger.Debug($"------------ Configuration -------------------------------------------");
            _logger.Debug($"Loaded from file                  : {_programSettingsManager.ConfigFilename}");
            _programSettingsManager.Data.LogOptions(_logger);
        }
        #endregion



        #region ------------- Periodic actions ----------------------------------------------------
        private static void StartScheduler()
        {
            _spamfilter = new Spamfilter()
                .UseLogger(_logger.Error, _logger.Warn, _logger.Info, _logger.Debug);

            _scheduler = new Scheduler()
                .UseAction(() => PeriodicJob())
                .UseFirstStartRightNow()
                .UseIntervalMinutes(_config.CheckIntervalMinutes)
                .Start();
        }

        private static void StopScheduler()
        {
            _scheduler?.Stop();
        }

        private static void PeriodicJob()
        {
            ProcessRules();
        }
        #endregion



        #region ------------- Domain logic --------------------------------------------------------
        private static void Run()
        {
            Console.ReadKey();
        }

        private static void ProcessRules()
        {
            try
            {
                LoadConfigFileAndProcessRules();
            }
            catch (Exception ex) 
            {
                _logger.Error($"Error with the imap server: {ex}");
            }
        }

        private static void LoadConfigFileAndProcessRules()
        {
            LoadOrReloadRules();
            _spamfilter?.CheckAllRules();
        }

        private static void LoadOrReloadRules()
        {
            // If the config file was changed, we re-read it and process all unread mails in the inbox
            // Already processed unread emails will be re-processed when you changes the rules.
            // Thats handy because you don't have to restart the application after you changed a rule.
            var currentWriteTime = File.GetLastWriteTime(_config.SpamfilterRules);
            var configFileHasChanged = currentWriteTime != _spamfilterConfigFileLastWriteTime;

            if (_thisIsTheFirstTime || configFileHasChanged)
            {
                if (!_thisIsTheFirstTime)
                    _logger.Debug("Re-loading the spamfilter rules from file '_config.SpamfilterRules' because the file was changed");

                _spamfilter?.LoadConfigurationFromFile(_config.SpamfilterRules);
                _spamfilterConfigFileLastWriteTime = currentWriteTime;

                _spamfilter?.InitSpamhausResolver();
                _spamfilter?.ForgetAlreadyProcessedEmails();

                if (_thisIsTheFirstTime)
                    _spamfilter?.Configuration.LogOptions(_logger.Debug);

                _thisIsTheFirstTime = false;
            }
        }
        #endregion
    }
}