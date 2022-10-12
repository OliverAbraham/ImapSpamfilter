namespace Abraham.MailRuleEngine
{
	public class Classification
	{
		public bool EmailIsSpam { get; set; }
		public string Reason { get; set; }

		public Classification(bool emailIsSpam, string reason)
		{
			EmailIsSpam = emailIsSpam;
			Reason = reason;
		}
	}
}
