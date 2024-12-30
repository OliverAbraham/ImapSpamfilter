using Abraham.HomenetFramework;
using Abraham.ProgramSettingsManager;
using CommandLine;
using NLog;
using Abraham.MailRuleEngine;

namespace ImapSpamfilter;

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
    private static Framework<CommandLineArguments,Configuration,StateFile> F = new();
    private static DateTime    _spamfilterConfigFileLastWriteTime = default(DateTime);
    private static Spamfilter? _spamfilter;
    private static bool        _thisIsTheFirstTime = true;
    #endregion



    #region ------------- Command line options ------------------------------------------------
    class CommandLineArguments
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

	    [Option('n', "statefile", Default = "state.json", Required = false, HelpText = 
	        """
	        File that contains the current program stare (full path and filename).
	        """)]
	    public string StateFile { get; set; } = "";

        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }
    }

    #endregion



    #region ------------- State file --------------------------------------------------------------
    /// <summary>
    /// Stores a set of dynamic data. Contents is read a application start and saved when ending.
    /// Add your properties here.
    /// </summary>
    public class StateFile
    {
        public List<Spamfilter.Blocking> BlockedSenders { get; set; }

        public StateFile()
        {
            BlockedSenders = new List<Spamfilter.Blocking>();
        }
    }
	#endregion



    #region ------------- Init ----------------------------------------------------------------
    public static void Main(string[] args)
    {
        Init();
        Run();
        Cleanup();
    }

    private static void Init()
    {
        F.ParseCommandLineArguments();
        F.ReadConfiguration(F.CommandLineArguments.ConfigurationFile);
        F.ValidateConfiguration();
        F.InitLogger(F.CommandLineArguments.NlogConfigurationFile);
        PrintGreeting();
        HealthChecks();
        F.ReadStateFile(F.CommandLineArguments.StateFile);
        F.StartBackgroundWorker(ProcessRulesPeriodically, F.Config.CheckIntervalMinutes * 60);
    }

    private static void Run()
    {
        F.Logger.Debug($"Background worker was started. Press any key to end the application.");
        Console.ReadKey();
    }

    private static void Cleanup()
    {
        F.StopBackgroundJob();
        F.State.BlockedSenders = _spamfilter.BlockedSenders;
        F.SaveStateFile(F.CommandLineArguments.StateFile);
    }
    #endregion



    #region ------------- Health checks -------------------------------------------------------
    private static void HealthChecks()
    {
    }
    #endregion



    #region ------------- Logging -------------------------------------------------------------
    /// <summary>
    /// To generate text like this, use https://onlineasciitools.com/convert-text-to-ascii-art
    /// </summary>
    private static void PrintGreeting()
    {
        F.Logger.Debug("");
        F.Logger.Debug("");
        F.Logger.Debug("");
        F.Logger.Debug(@"---------------------------------------------------------");
        F.Logger.Debug(@"     _____                        __ _ _ _               ");
        F.Logger.Debug(@"    / ____|                      / _(_) | |              ");
        F.Logger.Debug(@"   | (___  _ __   __ _ _ __ ___ | |_ _| | |_ ___ _ __    ");
        F.Logger.Debug(@"    \___ \| '_ \ / _` | '_ ` _ \|  _| | | __/ _ \ '__|   ");
        F.Logger.Debug(@"    ____) | |_) | (_| | | | | | | | | | | ||  __/ |      ");
        F.Logger.Debug(@"   |_____/| .__/ \__,_|_| |_| |_|_| |_|_|\__\___|_|      ");
        F.Logger.Debug(@"          | |                                            ");
        F.Logger.Debug(@"          |_|                                            ");
        F.Logger.Debug(@"                                                         ");
        F.Logger.Info($"Spamfilter started, Version {AppVersion.Version.VERSION}  ");
        F.Logger.Debug(@"---------------------------------------------------------");
    }

    private static void LogConfiguration()
    {
        F.Logger.Debug($"");
        F.Logger.Debug($"");
        F.Logger.Debug($"");
        F.Logger.Debug($"------------ Configuration -------------------------------------------");
        F.Logger.Debug($"Loaded from file                  : {F.ProgramSettingsManager.ConfigFilename}");
        F.ProgramSettingsManager.Data.LogOptions(F.Logger);
    }
    #endregion



    #region ------------- Domain logic --------------------------------------------------------
    private static void ProcessRulesPeriodically()
    {
        try
        {
            LoadConfigFileAndProcessRules();
        }
        catch (Exception ex) 
        {
            F.Logger.Error($"Error with the imap server: {ex}");
        }
    }

    private static void LoadConfigFileAndProcessRules()
    {
        LoadOrReloadRules();
        _spamfilter?.CheckAllRules();
    }

    private static void LoadOrReloadRules()
    {
        if (_thisIsTheFirstTime)
        {
            _spamfilter = new Spamfilter()
                .UseLogger(F.Logger.Error, F.Logger.Warn, F.Logger.Info, F.Logger.Debug);
            _spamfilter.BlockedSenders = F.State.BlockedSenders;
        }


        // If the config file was changed, we re-read it and process all unread mails in the inbox
        // Already processed unread emails will be re-processed when you changes the rules.
        // That's handy because you don't have to restart the application after you changed a rule.
        // You can simply edit the configuration file and save it.

        var currentWriteTime = File.GetLastWriteTime(F.Config.SpamfilterRules);
        var configFileHasChanged = currentWriteTime != _spamfilterConfigFileLastWriteTime;

        if (_thisIsTheFirstTime || configFileHasChanged)
        {
            if (!_thisIsTheFirstTime)
                F.Logger.Debug("Re-loading the spamfilter rules from file '_config.SpamfilterRules' because the file was changed");

            _spamfilter?.LoadConfigurationFromFile(F.Config.SpamfilterRules);
            _spamfilterConfigFileLastWriteTime = currentWriteTime;

            _spamfilter?.InitSpamhausResolver();
            _spamfilter?.ForgetAlreadyProcessedEmails();

            if (_thisIsTheFirstTime)
                _spamfilter?.Configuration.LogOptions(F.Logger.Debug);

            _thisIsTheFirstTime = false;
        }
    }
    #endregion
}