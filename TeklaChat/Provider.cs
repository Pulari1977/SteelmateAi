using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TeklaChat;

public abstract class Provider
{
	protected static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromMinutes(5.0)
	};

	protected const int MaxSteps = 12;

	public string ApiKey;

	public string Model;

	public string ExtraSystem;

	public const string SystemPrompt = "You operate Tekla Structures through tools. Units are millimetres. At the start of a new task call tekla_workflow_guide if you are unsure which tool to use. Never invent GUIDs: get them from tekla_find_parts or tekla_get_selected first. NC files, reports and drawings need up-to-date numbering: call tekla_check_numbering, and if it is out of date tell the user to number the model in Tekla. Preview before bulk changes. Be brief: no preamble, no repeating tool output, no recap. Use filters and small limits so tool results stay small. If the user attaches a drawing, PDF, sketch or photo: first read it and list what you understood (grids, levels, members with profile, length and position, plates, bolts) and mark every value you are unsure of. Ask the user to confirm or correct that list BEFORE creating anything. After they confirm, build in small batches with the create tools, then check the result with tekla_find_parts or tekla_weight_summary. Never guess a dimension that is not readable; ask instead.";

	protected string FullSystem
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(ExtraSystem))
			{
				return "You operate Tekla Structures through tools. Units are millimetres. At the start of a new task call tekla_workflow_guide if you are unsure which tool to use. Never invent GUIDs: get them from tekla_find_parts or tekla_get_selected first. NC files, reports and drawings need up-to-date numbering: call tekla_check_numbering, and if it is out of date tell the user to number the model in Tekla. Preview before bulk changes. Be brief: no preamble, no repeating tool output, no recap. Use filters and small limits so tool results stay small. If the user attaches a drawing, PDF, sketch or photo: first read it and list what you understood (grids, levels, members with profile, length and position, plates, bolts) and mark every value you are unsure of. Ask the user to confirm or correct that list BEFORE creating anything. After they confirm, build in small batches with the create tools, then check the result with tekla_find_parts or tekla_weight_summary. Never guess a dimension that is not readable; ask instead.\n\nYOU ARE NOW ACTING AS THIS AGENT. Follow its instructions:\n" + ExtraSystem;
			}
			return "You operate Tekla Structures through tools. Units are millimetres. At the start of a new task call tekla_workflow_guide if you are unsure which tool to use. Never invent GUIDs: get them from tekla_find_parts or tekla_get_selected first. NC files, reports and drawings need up-to-date numbering: call tekla_check_numbering, and if it is out of date tell the user to number the model in Tekla. Preview before bulk changes. Be brief: no preamble, no repeating tool output, no recap. Use filters and small limits so tool results stay small. If the user attaches a drawing, PDF, sketch or photo: first read it and list what you understood (grids, levels, members with profile, length and position, plates, bolts) and mark every value you are unsure of. Ask the user to confirm or correct that list BEFORE creating anything. After they confirm, build in small batches with the create tools, then check the result with tekla_find_parts or tekla_weight_summary. Never guess a dimension that is not readable; ask instead.";
		}
	}

	public abstract void Reset();

	public abstract Task RunTurnAsync(string userText, IList<Attachment> files, IList<ToolDef> tools, ToolCaller callTool, Action<string> onText, CancellationToken ct);

	protected static JsonNode Clone(JsonNode n)
	{
		if (n != null)
		{
			return JsonNode.Parse(n.ToJsonString());
		}
		return null;
	}

	protected static async Task<JsonNode> PostAsync(string url, JsonNode body, Dictionary<string, string> headers, CancellationToken ct)
	{
		HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, url);
		try
		{
			foreach (KeyValuePair<string, string> header in headers)
			{
				((HttpHeaders)req.Headers).TryAddWithoutValidation(header.Key, header.Value);
			}
			req.Content = (HttpContent)new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
			HttpResponseMessage resp = await ((HttpMessageInvoker)Http).SendAsync(req, ct);
			try
			{
				string text = await resp.Content.ReadAsStringAsync();
				if (!resp.IsSuccessStatusCode)
				{
					throw new Exception("API error " + (int)resp.StatusCode + ": " + text);
				}
				return JsonNode.Parse(text);
			}
			finally
			{
				((IDisposable)resp)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)req)?.Dispose();
		}
	}

	/// <summary>Sends one tiny request to check that the key and model work.</summary>
	public static async Task PingAsync(string provider, string key, string model)
	{
		using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30.0));
		if (provider == "Gemini")
		{
			await PostAsync("https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent", new JsonObject { ["contents"] = new JsonArray { new JsonObject { ["role"] = "user", ["parts"] = new JsonArray { new JsonObject { ["text"] = "ping" } } } } }, new Dictionary<string, string> { ["x-goog-api-key"] = key }, cts.Token);
		}
		else if (provider == "OpenAI")
		{
			await PostAsync("https://api.openai.com/v1/responses", new JsonObject { ["model"] = model, ["input"] = "ping", ["max_output_tokens"] = 16 }, new Dictionary<string, string> { ["Authorization"] = "Bearer " + key }, cts.Token);
		}
		else
		{
			await PostAsync("https://api.anthropic.com/v1/messages", new JsonObject
			{
				["model"] = model,
				["max_tokens"] = 8,
				["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = "ping" } }
			}, new Dictionary<string, string>
			{
				["x-api-key"] = key,
				["anthropic-version"] = "2023-06-01"
			}, cts.Token);
		}
	}

	/// <summary>Turns a failed AI call into a plain message that says what to do.</summary>
	public static string Explain(string provider, Exception ex)
	{
		string m = ex.Message ?? "";
		string low = m.ToLowerInvariant();
		string raw = (m.Length > 400) ? m.Substring(0, 400) + "…" : m;
		if (ex is TaskCanceledException || ex is OperationCanceledException)
			return provider + " did not answer in time. Check your internet connection and try again.";
		if (ex is HttpRequestException || low.Contains("could not be resolved") || low.Contains("unable to connect") || low.Contains("ssl") || low.Contains("tls") || low.Contains("proxy"))
			return "Cannot reach " + provider + " over the internet. A firewall, VPN, proxy or antivirus may be blocking it.\nDetails: " + Inner(ex);
		if (low.Contains("api error 401") || low.Contains("invalid x-api-key") || low.Contains("authentication") || low.Contains("api key not valid") || low.Contains("incorrect api key"))
			return provider + " rejected the API key. Paste the key again in Settings (no spaces or quotes), then press Save and reconnect.\nDetails: " + raw;
		if (low.Contains("credit balance") || low.Contains("billing") || low.Contains("quota") || low.Contains("insufficient"))
			return "The " + provider + " account has no credit or hit its limit. Add credit in your " + provider + " account, then try again.\nDetails: " + raw;
		if (low.Contains("api error 404") || (low.Contains("model") && (low.Contains("not found") || low.Contains("not_found") || low.Contains("does not exist"))))
			return "The model name is not accepted by " + provider + ". Pick another model in the list at the bottom of the chat.\nDetails: " + raw;
		if (low.Contains("api error 403"))
			return provider + " refused the request (403). The key may not have access to this model, or your region is blocked.\nDetails: " + raw;
		if (low.Contains("api error 429") || low.Contains("api error 529") || low.Contains("overloaded") || low.Contains("rate"))
			return provider + " is busy or you sent too many requests. Wait a minute and try again.\nDetails: " + raw;
		return "Error: " + raw;
	}

	private static string Inner(Exception ex)
	{
		while (ex.InnerException != null) ex = ex.InnerException;
		return ex.Message;
	}

	protected static JsonNode ParseArgs(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return new JsonObject();
		}
		try
		{
			return JsonNode.Parse(json) ?? new JsonObject();
		}
		catch
		{
			return new JsonObject();
		}
	}
}
