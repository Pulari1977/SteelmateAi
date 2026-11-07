using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace TeklaChat;

public class McpHost
{
	private McpClient _client;

	public List<ToolDef> Tools { get; private set; } = new List<ToolDef>();


	public bool Connected => _client != null;

	public async Task ConnectAsync(string exePath)
	{
		await DisconnectAsync();
		StdioClientTransportOptions stdioClientTransportOptions = new StdioClientTransportOptions
		{
			Name = "tekla",
			Command = exePath,
			Arguments = new string[1] { "--mcp" }
		};
		_client = await McpClient.CreateAsync(new StdioClientTransport(stdioClientTransportOptions));
		Tools = (await _client.ListToolsAsync()).Select((McpClientTool t) => new ToolDef
		{
			Name = t.ProtocolTool.Name,
			Description = (t.ProtocolTool.Description ?? ""),
			Schema = JsonNode.Parse(t.ProtocolTool.InputSchema.GetRawText()),
			ReadOnly = (t.ProtocolTool.Annotations?.ReadOnlyHint.GetValueOrDefault() ?? false),
			Destructive = (t.ProtocolTool.Annotations?.DestructiveHint.GetValueOrDefault() ?? false)
		}).ToList();
	}

	public async Task<string> CallAsync(string name, JsonNode args)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		if (args is JsonObject jsonObject)
		{
			foreach (KeyValuePair<string, JsonNode> item in jsonObject)
			{
				dictionary[item.Key] = ((item.Value == null) ? null : ((object)JsonSerializer.Deserialize<JsonElement>(item.Value.ToJsonString())));
			}
		}
		CallToolResult callToolResult = await _client.CallToolAsync(name, dictionary);
		string text = string.Join("\n", from c in callToolResult.Content.OfType<TextContentBlock>()
			select c.Text);
		if (callToolResult.IsError.GetValueOrDefault())
		{
			text = "ERROR: " + text;
		}
		if (text != null && text.Length > 6000)
		{
			text = text.Substring(0, 6000) + "\n…(cut: " + (text.Length - 6000) + " more characters. Narrow the request with filters or a smaller limit.)";
		}
		return string.IsNullOrEmpty(text) ? "(no output)" : text;
	}

	public async Task DisconnectAsync()
	{
		if (_client != null)
		{
			try
			{
				await _client.DisposeAsync();
			}
			catch
			{
			}
			_client = null;
			Tools = new List<ToolDef>();
		}
	}
}
