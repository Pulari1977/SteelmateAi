using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TeklaChat;

public class ClaudeProvider : Provider
{
	private JsonArray _msgs = new JsonArray();

	private static void MarkLastForCache(JsonArray msgs)
	{
		if (msgs != null && msgs.Count != 0 && msgs[msgs.Count - 1] is JsonObject jsonObject)
		{
			if (jsonObject["content"] is JsonValue)
			{
				jsonObject["content"] = new JsonArray
				{
					new JsonObject
					{
						["type"] = "text",
						["text"] = (string?)jsonObject["content"]
					}
				};
			}
			if (jsonObject["content"] is JsonArray { Count: >0 } jsonArray && jsonArray[jsonArray.Count - 1] is JsonObject jsonObject2)
			{
				jsonObject2["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
			}
		}
	}

	public override void Reset()
	{
		_msgs = new JsonArray();
	}

	public override async Task RunTurnAsync(string userText, IList<Attachment> files, IList<ToolDef> tools, ToolCaller callTool, Action<string> onText, CancellationToken ct)
	{
		if (files == null || files.Count == 0)
		{
			_msgs.Add(new JsonObject
			{
				["role"] = "user",
				["content"] = userText
			});
		}
		else
		{
			JsonArray jsonArray = new JsonArray();
			foreach (Attachment file in files)
			{
				jsonArray.Add(new JsonObject
				{
					["type"] = (file.IsPdf ? "document" : "image"),
					["source"] = new JsonObject
					{
						["type"] = "base64",
						["media_type"] = file.MimeType,
						["data"] = file.Base64
					}
				});
			}
			jsonArray.Add(new JsonObject
			{
				["type"] = "text",
				["text"] = userText
			});
			_msgs.Add(new JsonObject
			{
				["role"] = "user",
				["content"] = jsonArray
			});
		}
		for (int step = 0; step < 12; step++)
		{
			JsonArray jsonArray2 = new JsonArray();
			foreach (ToolDef tool in tools)
			{
				jsonArray2.Add(new JsonObject
				{
					["name"] = tool.Name,
					["description"] = tool.Description,
					["input_schema"] = Provider.Clone(tool.Schema)
				});
			}
			JsonObject jsonObject = new JsonObject
			{
				["model"] = Model,
				["max_tokens"] = 16000,
				["system"] = new JsonArray
				{
					new JsonObject
					{
						["type"] = "text",
						["text"] = base.FullSystem,
						["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
					}
				},
				["tools"] = jsonArray2,
				["messages"] = Provider.Clone(_msgs)
			};
			MarkLastForCache((JsonArray)jsonObject["messages"]);
			JsonNode resp = await Provider.PostAsync("https://api.anthropic.com/v1/messages", jsonObject, new Dictionary<string, string>
			{
				["x-api-key"] = ApiKey,
				["anthropic-version"] = "2023-06-01"
			}, ct);
			JsonArray jsonArray3 = (JsonArray)resp["content"];
			_msgs.Add(new JsonObject
			{
				["role"] = "assistant",
				["content"] = Provider.Clone(jsonArray3)
			});
			JsonArray results = new JsonArray();
			foreach (JsonNode block in jsonArray3)
			{
				string text = (string?)block["type"];
				if (text == "text")
				{
					onText((string?)block["text"]);
				}
				else if (text == "tool_use")
				{
					string text2 = await callTool((string?)block["name"], Provider.Clone(block["input"]));
					results.Add(new JsonObject
					{
						["type"] = "tool_result",
						["tool_use_id"] = (string?)block["id"],
						["content"] = text2
					});
				}
			}
			if (results.Count == 0)
			{
				string text3 = (string?)resp["stop_reason"];
				if (text3 == "refusal")
				{
					onText("(The model declined this request.)");
				}
				else if (text3 == "max_tokens")
				{
					onText("(The reply was cut off. Ask me to continue.)");
				}
				return;
			}
			_msgs.Add(new JsonObject
			{
				["role"] = "user",
				["content"] = results
			});
		}
		onText("(Stopped after " + 12 + " tool steps. Ask me to continue.)");
	}
}
