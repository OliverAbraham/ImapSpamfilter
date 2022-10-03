using System;
using System.Collections.Generic;
using System.Linq;

namespace Abraham.Spamfilter
{
	public class Spamfilter
	{
		#region ------------- Properties ----------------------------------------------------------
		public Configuration Configuration { get; set; }
		#endregion



		#region ------------- Fields --------------------------------------------------------------
		private const string _latinCharacters = "abcdefghijklmnopqrstuvwxyzäöüßABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÜß01234567890<>|,;.:-_#'+*~´`ß?\\!\"§$%&/()=";
		#endregion



		#region ------------- Init ----------------------------------------------------------------
		#endregion



		#region ------------- Methods -------------------------------------------------------------
		public Classification ClassifyEmail(string subject, string body, string senderName, string senderEMail)
		{
            if (Configuration == null)
                throw new ArgumentNullException(nameof(Configuration));
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

            if (Configuration.SenderWhitelist.Contains(senderEMail))
            {
                return new Classification(false, $"Sender white list contains this sender ({senderEMail})");
            }

            // If more than half of the characters are non-latin, this is spam
            var countTotal              = subject.Length;
            var countNonLatinCharacters = CalculateNonLatinCharacterCount(subject);
            if (countNonLatinCharacters >= countTotal/2)
            {
                return new Classification(true, $"{countNonLatinCharacters} of {countTotal} characters are non-latin in subject");
            }

            var specialCharactersSenderEmail = CalculateSpecialCharacterCount(senderEMail);
            if (specialCharactersSenderEmail.Count > Configuration.SpecialCharactersSenderEmailThreshold)
            {
                return new Classification(true, $"{specialCharactersSenderEmail.Details} in sender email");
            }

            var specialCharactersSenderName1 = CalculateSpecialCharacterCount(senderName);
            if (specialCharactersSenderName1.Count > Configuration.SpecialCharactersSenderNameThreshold)
            {
                return new Classification(true, $"{specialCharactersSenderName1.Details} in sender name");
            }

            var specialCharactersSenderName2 = CalculateSpecialCharacterCount(senderNameWithoutPunctuation);
            if (specialCharactersSenderName2.Count > Configuration.SpecialCharactersSenderNameThreshold)
            {
                return new Classification(true, $"{specialCharactersSenderName2.Details} in sender name");
            }

            var specialCharactersSubject1 = CalculateSpecialCharacterCount(subject);
            if (specialCharactersSubject1.Count > Configuration.SpecialCharactersSubjectThreshold)
            {
                return new Classification(true, $"{specialCharactersSubject1.Details}");
            }

            var specialCharactersSubject2 = CalculateSpecialCharacterCount(subjectWithoutPunctuation);
            if (specialCharactersSubject2.Count > Configuration.SpecialCharactersSubjectThreshold)
            {
                return new Classification(true, $"{specialCharactersSubject2.Details}");
            }

            if (Configuration.SenderBlacklist.Contains(senderEMail.ToLower()))
            {
                return new Classification(true, $"the sender email is on the blacklist");
            }

            foreach (var blacklistWord in Configuration.SenderBlacklist)
            {
                if (senderName.ToLower().Contains(blacklistWord.ToLower()))
                {
                    return new Classification(true, $"the sender name contains the blacklisted word '{blacklistWord}'");
                }
            }

            if (subject != null)
                foreach (var blacklistWord in Configuration.SubjectBlacklist)
                {
                    if (subject.ToLower().Contains(blacklistWord.ToLower()))
                    {
                        return new Classification(true, $"the subject contains the blacklisted word '{blacklistWord}'");
                    }
                }

            return new Classification(false, "");
		}
		#endregion



		#region ------------- Implementation ------------------------------------------------------
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

        private AnalysisResult CalculateSpecialCharacterCount(string text)
        {
            var countPerCharacter = new Dictionary<char, int>();

            if (text != null)
                foreach (var character in text)
                {
                    if (ThisCharacterIsSpecialCharacter(character))
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

        private bool ThisCharacterIsSpecialCharacter(char character)
        {
            return !Configuration.AllowedCharactersForEmailSendersAndSubjects.Contains(character);
        }

		private int CalculateNonLatinCharacterCount(string text)
		{
			int count = 0;
            foreach(var character in text)
			{
				if (!_latinCharacters.Contains(character))
                    count++;
			}
            return count;
		}
		#endregion
	}
}
