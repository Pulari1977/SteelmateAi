using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TeklaChat;

public class OpenAiProvider : Provider
{
	private JsonArray _input = new JsonArray();

	public override void Reset()
	{
		_input = new JsonArray();
	}

	public override async Task RunTurnAsync(string userText, IList<Attachment> files, IList<ToolDef> tools, ToolCaller callTool, Action<string> onText, CancellationToken ct)
	{
		JsonArray jsonArray = new JsonArray
		{
			new JsonObject
			{
				["type"] = "input_text",
				["text"] = userText
			}
		};
		if (files != null)
		{
			foreach (Attachment file in files)
			{
				string text = "data:" + file.MimeType + ";base64," + file.Base64;
				if (file.IsPdf)
				{
					jsonArray.Add(new JsonObject
					{
						["type"] = "input_file",
						["filename"] = file.Name,
						["file_data"] = text
					});
				}
				else
				{
					jsonArray.Add(new JsonObject
					{
						["type"] = "input_image",
						["image_url"] = text
					});
				}
			}
		}
		_input.Add(new JsonObject
		{
			["role"] = "user",
			["content"] = jsonArray
		});
		for (int step = 0; step < 12; step++)
		{
			JsonArray jsonArray2 = new JsonArray();
			foreach (ToolDef tool in tools)
			{
				jsonArray2.Add(new JsonObject
				{
					["type"] = "function",
					["name"] = tool.Name,
					["description"] = tool.Description,
					["parameters"] = Provider.Clone(tool.Schema),
					["strict"] = false
				});
			}
			JsonObject body = new JsonObject
			{
				["model"] = Model,
				["instructions"] = base.FullSystem,
				["input"] = Provider.Clone(_input),
				["tools"] = jsonArray2
			};
			JsonNode resp = await Provider.PostAsync("https://api.openai.com/v1/responses", body, new Dictionary<string, string> { ["Authorization"] = "Bearer " + ApiKey }, ct);
			JsonArray jsonArray3 = (resp["output"] as JsonArray) ?? new JsonArray();
			List<JsonNode> results = new List<JsonNode>();
			foreach (JsonNode item in jsonArray3)
			{
				_input.Add(Provider.Clone(item));
				string text2 = (string?)item["type"];
				if (text2 == "message")
				{
					foreach (JsonNode item2 in (JsonArray)(item["content"] ?? new JsonArray()))
					{
						if ((string?)item2["type"] == "output_text" && !string.IsNullOrEmpty((string?)item2["text"]))
						{
							onText((string?)item2["text"]);
						}
					}
				}
				else if (text2 == "function_call")
				{
					string text3 = await callTool((string?)item["name"], Provider.ParseArgs((string?)item["arguments"]));
					results.Add(new JsonObject
					{
						["type"] = "function_call_output",
						["call_id"] = (string?)item["call_id"],
						["output"] = text3
					});
				}
			}
			if (results.Count == 0)
			{
				if ((string?)resp["status"] == "incomplete")
				{
					onText("(The reply was cut off. Ask me to continue.)");
				}
				return;
			}
			foreach (JsonNode item3 in results)
			{
				_input.Add(item3);
			}
		}
		onText("(Stopped after " + 12 + " tool steps. Ask me to continue.)");
	}
}
