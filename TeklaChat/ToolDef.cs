using System.Text.Json.Nodes;

namespace TeklaChat;

public class ToolDef
{
	public string Name;

	public string Description;

	public JsonNode Schema;

	public bool ReadOnly;

	public bool Destructive;
}
