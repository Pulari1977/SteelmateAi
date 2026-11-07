namespace TeklaChat;

public class Attachment
{
	public string Name;

	public string MimeType;

	public string Base64;

	public bool IsPdf => MimeType == "application/pdf";
}
