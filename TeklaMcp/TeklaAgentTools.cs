using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace TeklaMcp;

[McpServerToolType]
public static class TeklaAgentTools
{
	[McpServerTool(Name = "tekla_add_agent")]
	[Description("Create or replace an agent (a saved helper with its own instructions and allowed tools). Saved as agents\\<name>.md next to the program. tools = 'readonly' (all read-only tools), 'all', or a comma list of tool names, optionally mixed, e.g. 'readonly, tekla_bulk_modify'. The user picks it in the Agent drop-down after pressing Reload in the Tools tab.")]
	public static string AddAgent([Description("File name without extension: letters, digits, - or _")] string id, [Description("Display name")] string name, [Description("One-line description")] string description, [Description("Allowed tools, e.g. 'readonly, tekla_fix_columns'")] string tools, [Description("Instructions for the agent: its job, steps, and what it must never do")] string instructions)
	{
		if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, "^[A-Za-z][A-Za-z0-9_-]{0,40}$"))
		{
			throw new ArgumentException("id must start with a letter and use only letters, digits, - or _.");
		}
		if (string.IsNullOrWhiteSpace(instructions))
		{
			throw new ArgumentException("instructions are empty.");
		}
		string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agents");
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, id + ".md");
		bool flag = File.Exists(text2);
		if (flag)
		{
			File.Copy(text2, text2 + ".bak", overwrite: true);
		}
		File.WriteAllText(text2, "name: " + (name ?? id).Replace("\n", " ") + "\ndescription: " + (description ?? "").Replace("\n", " ") + "\ntools: " + (string.IsNullOrWhiteSpace(tools) ? "readonly" : tools.Replace("\n", " ")) + "\n---\n" + instructions.Trim() + "\n");
		return TeklaSession.ToJson(new
		{
			saved = text2,
			replaced = flag,
			next = "Tell the user to press Reload in the Tools tab, then choose the agent in the Agent drop-down."
		});
	}

	[McpServerTool(Name = "tekla_save_report")]
	[Description("Save a text/markdown report (for example a QA report) as a file in Documents\\SteelMate AI\\reports and return the path. Does not change the model.")]
	public static string SaveReport([Description("File name without extension")] string name, [Description("The full report text")] string content)
	{
		string text = new string((name ?? "report").Where((char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
		if (text.Length == 0)
		{
			text = "report";
		}
		string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SteelMate AI", "reports");
		Directory.CreateDirectory(text2);
		string text3 = Path.Combine(text2, text + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".md");
		File.WriteAllText(text3, content ?? "");
		return TeklaSession.ToJson(new
		{
			saved = text3
		});
	}
}
