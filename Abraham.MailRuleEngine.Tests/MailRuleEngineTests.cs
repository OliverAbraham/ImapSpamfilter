using Abraham.Mail;
using FluentAssertions;
using MimeKit;

namespace Abraham.MailRuleEngine.Tests
{
    [TestClass]
    public class MailRuleEngineTests
    {
        #region ------------- Fields --------------------------------------------------------------
        Configuration _config = new Configuration()
        {
            SpamBlacklists            = new(),
            SpamQueryTimeoutInSeconds = 10,
            MailAccounts              = new(),
            GeneralSpamfilterSettings = new SpamfilterSettings()
            {
                ReCheckEveryUnreadMessage             = false,                   
                SpecialCharacterWhitelist             = "01234567890ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz‰ˆ¸ƒ÷‹ﬂ?!ß$%&/()[]<>'#*@_ ",
		        CharacterWhitelist                    = "abcdefghijklmnopqrstuvwxyz‰ˆ¸ﬂABCDEFGHIJKLMNOPQRSTUVWXYZƒ÷‹ﬂ01234567890<>|,;.:-_#'+*~¥`ﬂ?\\!\"ß$%&/()[]<>=",
                SpecialCharactersSenderEmailThreshold = 0,
                SpecialCharactersSenderNameThreshold  = 0,
                SpecialCharactersSubjectThreshold     = 0,
                NonLatinCharactersSubjectThreshold    = 0,
                SenderWhitelist                       = new string[0],
	            SenderBlacklist                       = new string[0],
                SubjectBlacklist                      = new string[0],
		        GeneralBlacklist                      = new string[] { "bitcoin", "[regular*expression]" }
            },
        };

        Rule _spamCheckRule = new Rule()
        {
            Name                         = "MyRuleName",
            IfMailIsSpam                 = true,
            IfMailWasSentBy              = new(),
            IfMailWasSentTo              = new(),
            IfMailContainsWordsInHeader  = new(),
            IfMailContainsWordsInSubject = new(),
            IfMailContainsWordsInBody    = new(),
            DateRangeFrom                = new DateTimeOffset(1900,1,1,0,0,0, new TimeSpan()),
            DateRangeTo                  = new DateTimeOffset(2099,1,1,0,0,0, new TimeSpan()),
            Actions                      = new List<FilterAction>() { },
            SpamfilterSettings           = new SpamfilterSettings(),
            StopAfterAction              = false,
        };

        Spamfilter.AccountDTO _accountDto = new Spamfilter.AccountDTO(new MailAccount(), new ImapClient());
        #endregion



        #region ------------- Init ----------------------------------------------------------------
        #endregion



        #region ------------- Tests ---------------------------------------------------------------
        [TestMethod]
        public void GeneralRuleTest()
        {
            var sut = CreateSut();
            var message = CreateMessage("Sam Spammer", "spammer@evil.net", "bitcoin wholesale");

            var result = sut.ProcessGeneralRule(_spamCheckRule, message, _accountDto);

            result.Should().BeTrue();
        }
        #endregion



        #region ------------- Implementation ------------------------------------------------------
        private Spamfilter CreateSut()
        {
            var sut = new Spamfilter();
            sut.UseConfiguration(_config);
            return sut;
        }

        private static Message CreateMessage(string fromName, string fromAddr, string subject, string toName = "Me", string toAddr = "mail@example.com")
        {
            var from    = new MailboxAddress(fromName, fromAddr);
            var to      = new MailboxAddress(toName, toAddr);
            var body    = new MailKit.BodyPartBasic();
            var message = new Abraham.Mail.Message(new MailKit.UniqueId(), new MimeMessage(from, to, subject, body));
            return message;
        }
        #endregion
   }
}