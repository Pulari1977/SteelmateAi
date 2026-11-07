using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TeklaChat;

public static class ChatStore
{
	private static string Dir
	{
		get
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeklaChat", "history");
			Directory.CreateDirectory(text);
			return text;
		}
	}

	public static void Save(ChatRecord r)
	{
		try
		{
			File.WriteAllText(Path.Combine(Dir, r.Id + ".json"), JsonSerializer.Serialize(r, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch
		{
		}
	}

	public static List<ChatRecord> LoadAll()
	{
		List<ChatRecord> list = new List<ChatRecord>();
		try
		{
			string[] files = Directory.GetFiles(Dir, "*.json");
			foreach (string path in files)
			{
				try
				{
					ChatRecord chatRecord = JsonSerializer.Deserialize<ChatRecord>(File.ReadAllText(path));
					if (chatRecord != null && !string.IsNullOrEmpty(chatRecord.Id))
					{
						list.Add(chatRecord);
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return list.OrderByDescending((ChatRecord r) => r.Started).ToList();
	}

	public static void Delete(string id)
	{
		try
		{
			File.Delete(Path.Combine(Dir, id + ".json"));
		}
		catch
		{
		}
	}
}
