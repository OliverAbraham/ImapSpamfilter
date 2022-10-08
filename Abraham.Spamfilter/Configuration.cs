using System;
using System.Collections.Generic;

namespace Abraham.Spamfilter
{
	public class Configuration
    {
        public string         AllowedCharactersForEmailSendersAndSubjects {  get; set; }
        public int            SpecialCharactersSenderEmailThreshold       {  get; set; }
        public int            SpecialCharactersSenderNameThreshold        {  get; set; }
        public int            SpecialCharactersSubjectThreshold           {  get; set; }
        public string[]       SenderWhitelist                             {  get; set; }
        public string[]       SenderBlacklist                             {  get; set; }
        public string[]       SubjectBlacklist                            {  get; set; }
        public List<Rule>     Rules                                       {  get; set; }
    }                         
                              
    public class Rule         
    {                         
        public List<string>   IfMailComesFromOneOfTheseSenders            {  get; set; }
        public List<string>   IfMailWasSentToOneOfTheseReceivers          {  get; set; }
        public List<string>   IfMailWasSentToOneOfTheseReceiversOnlyCc    {  get; set; }
        public List<string>   IfMailContainsOneOfTheseWordsInHeader       {  get; set; }
        public List<string>   IfMailContainsOneOfTheseWordsInSubject      {  get; set; }
        public List<string>   IfMailContainsOneOfTheseWordsInBody         {  get; set; }
        public DateTimeOffset DateRangeFrom                               {  get; set; }
        public DateTimeOffset DateRangeTo                                 {  get; set; }
    }
}
