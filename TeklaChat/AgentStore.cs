using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TeklaChat;

public static class AgentStore
{
	public static string Dir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agents");

	public static List<AgentDef> Load()
	{
		List<AgentDef> list = new List<AgentDef>();
		if (!Directory.Exists(Dir))
		{
			return list;
		}
		foreach (string item in from x in Directory.GetFiles(Dir, "*.md")
			orderby x
			select x)
		{
			try
			{
				AgentDef agentDef = new AgentDef
				{
					Id = Path.GetFileNameWithoutExtension(item)
				};
				string[] array = File.ReadAllLines(item);
				int i;
				for (i = 0; i < array.Length && array[i].Trim() != "---"; i++)
				{
					int num = array[i].IndexOf(':');
					if (num < 0)
					{
						continue;
					}
					string text = array[i].Substring(0, num).Trim().ToLowerInvariant();
					string text2 = array[i].Substring(num + 1).Trim();
					switch (text)
					{
					case "name":
						agentDef.Name = text2;
						break;
					case "description":
						agentDef.Description = text2;
						break;
					case "tools":
						foreach (string item2 in from x in text2.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
							select x.Trim())
						{
							if (item2.Equals("all", StringComparison.OrdinalIgnoreCase))
							{
								agentDef.All = true;
							}
							else if (item2.Equals("readonly", StringComparison.OrdinalIgnoreCase))
							{
								agentDef.ReadOnly = true;
							}
							else
							{
								agentDef.Tools.Add(item2);
							}
						}
						break;
					}
				}
				agentDef.Instructions = string.Join("\n", array.Skip(i + 1)).Trim();
				if (string.IsNullOrWhiteSpace(agentDef.Name))
				{
					agentDef.Name = agentDef.Id;
				}
				if (!agentDef.All && !agentDef.ReadOnly && agentDef.Tools.Count == 0)
				{
					agentDef.ReadOnly = true;
				}
				list.Add(agentDef);
			}
			catch
			{
			}
		}
		return list;
	}

	public static IList<ToolDef> Filter(AgentDef a, IList<ToolDef> all)
	{
		if (a == null || a.All)
		{
			return all;
		}
		return all.Where((ToolDef t) => a.Tools.Contains(t.Name) || (a.ReadOnly && t.ReadOnly)).ToList();
	}
}
