namespace Abraham.Spamfilter
{
	public class Configuration
    {
        public string       AllowedCharactersForEmailSendersAndSubjects {  get; set; }
        public string[]     SenderWhitelist                             {  get; set; }
        public string[]     SenderBlacklist                             {  get; set; }
        public string[]     SubjectBlacklist                            {  get; set; }
        public int          SpecialCharactersSenderEmailThreshold       {  get; set; }
        public int          SpecialCharactersSenderNameThreshold        {  get; set; }
        public int          SpecialCharactersSubjectThreshold           {  get; set; }
    }
}
