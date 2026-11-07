using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TeklaChat;

public class GeminiProvider : Provider
{
	private JsonArray _contents = new JsonArray();

	public override void Reset()
	{
		_contents = new JsonArray();
	}

	private static JsonNode CleanSchema(JsonNode n)
	{
		if (n is JsonObject jsonObject)
		{
			JsonObject jsonObject2 = new JsonObject();
			{
				foreach (KeyValuePair<string, JsonNode> item in jsonObject)
				{
					if (item.Key == "additionalProperties" || item.Key == "$schema" || item.Key == "title" || item.Key == "default" || item.Key == "$id")
					{
						continue;
					}
					if (item.Key == "type" && item.Value is JsonArray source)
					{
						string text = source.Select((JsonNode x) => (string?)x).FirstOrDefault((string x) => x != "null") ?? "string";
						jsonObject2["type"] = text;
					}
					else
					{
						jsonObject2[item.Key] = CleanSchema(item.Value);
					}
				}
				return jsonObject2;
			}
		}
		if (n is JsonArray jsonArray)
		{
			JsonArray jsonArray2 = new JsonArray();
			{
				foreach (JsonNode item2 in jsonArray)
				{
					jsonArray2.Add(CleanSchema(item2));
				}
				return jsonArray2;
			}
		}
		return Provider.Clone(n);
	}

	public override async Task RunTurnAsync(string userText, IList<Attachment> files, IList<ToolDef> tools, ToolCaller callTool, Action<string> onText, CancellationToken ct)
	{
		JsonArray jsonArray = new JsonArray();
		if (files != null)
		{
			foreach (Attachment file in files)
			{
				jsonArray.Add(new JsonObject { ["inlineData"] = new JsonObject
				{
					["mimeType"] = file.MimeType,
					["data"] = file.Base64
				} });
			}
		}
		jsonArray.Add(new JsonObject { ["text"] = userText });
		_contents.Add(new JsonObject
		{
			["role"] = "user",
			["parts"] = jsonArray
		});
		for (int step = 0; step < 12; step++)
		{
			JsonArray jsonArray2 = new JsonArray();
			foreach (ToolDef tool in tools)
			{
				jsonArray2.Add(new JsonObject
				{
					["name"] = tool.Name,
					["description"] = tool.Description,
					["parameters"] = CleanSchema(tool.Schema)
				});
			}
			JsonObject body = new JsonObject
			{
				["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray
				{
					new JsonObject { ["text"] = base.FullSystem }
				} },
				["contents"] = Provider.Clone(_contents),
				["tools"] = new JsonArray
				{
					new JsonObject { ["functionDeclarations"] = jsonArray2 }
				}
			};
			JsonNode jsonNode = await Provider.PostAsync("https://generativelanguage.googleapis.com/v1beta/models/" + Model + ":generateContent", body, new Dictionary<string, string> { ["x-goog-api-key"] = ApiKey }, ct);
			JsonNode jsonNode2 = jsonNode["candidates"]?[0]?["content"];
			if (jsonNode2 == null)
			{
				onText("(The model returned no content: " + jsonNode.ToJsonString() + ")");
				return;
			}
			_contents.Add(Provider.Clone(jsonNode2));
			JsonArray responses = new JsonArray();
			foreach (JsonNode item in (JsonArray)(jsonNode2["parts"] ?? new JsonArray()))
			{
				if (item["text"] != null)
				{
					onText((string?)item["text"]);
				}
				JsonNode fc = item["functionCall"];
				if (fc != null)
				{
					string name = (string?)fc["name"];
					string text = await callTool(name, Provider.Clone(fc["args"]) ?? new JsonObject());
					JsonObject jsonObject = new JsonObject
					{
						["name"] = name,
						["response"] = new JsonObject { ["result"] = text }
					};
					if (fc["id"] != null)
					{
						jsonObject["id"] = (string?)fc["id"];
					}
					responses.Add(new JsonObject { ["functionResponse"] = jsonObject });
				}
			}
			if (responses.Count == 0)
			{
				return;
			}
			_contents.Add(new JsonObject
			{
				["role"] = "user",
				["parts"] = responses
			});
		}
		onText("(Stopped after " + 12 + " tool steps. Ask me to continue.)");
	}
}
