using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TeklaChat;

public class Settings
{
	public string Provider { get; set; } = "Claude";


	public string McpPath { get; set; } = "";


	public bool AskBeforeChanges { get; set; } = true;


	public bool EnableRecorder { get; set; }

	public bool ShowAgents { get; set; }

	public bool DockLeft { get; set; } = true;


	public bool DockRight { get; set; } = true;


	public bool FitTekla { get; set; }

	public Dictionary<string, string> Models { get; set; } = new Dictionary<string, string>();


	public Dictionary<string, string> Keys { get; set; } = new Dictionary<string, string>();


	private static string FilePath
	{
		get
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeklaChat");
			Directory.CreateDirectory(text);
			return Path.Combine(text, "settings.json");
		}
	}

	/// <summary>Bumped when defaults change, so old settings are moved to the new defaults once.</summary>
	public int SettingsVersion { get; set; }

	public const int CurrentVersion = 2;

	public static Settings Load()
	{
		Settings s = null;
		try
		{
			if (File.Exists(FilePath))
			{
				s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
			}
		}
		catch
		{
		}
		if (s == null)
		{
			s = new Settings();
		}
		if (s.SettingsVersion < CurrentVersion)
		{
			// One-time move to Claude as the default, with a known-good model name.
			s.Provider = "Claude";
			s.Models["Claude"] = "claude-sonnet-5-5";
			s.SettingsVersion = CurrentVersion;
			s.Save();
		}
		return s;
	}

	public void Save()
	{
		try
		{
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch
		{
		}
	}

	public string GetKey(string provider)
	{
		try
		{
			if (!Keys.TryGetValue(provider, out var value) || string.IsNullOrEmpty(value))
			{
				return "";
			}
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(value), (byte[])null, (DataProtectionScope)0);
			return Encoding.UTF8.GetString(bytes);
		}
		catch
		{
			return "";
		}
	}

	public void SetKey(string provider, string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			Keys.Remove(provider);
			return;
		}
		key = key.Trim().Trim('"', '\'', ' ').Replace("\r", "").Replace("\n", "").Replace(" ", "");
		if (key.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
		{
			key = key.Substring(6);
		}
		byte[] inArray = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), (byte[])null, (DataProtectionScope)0);
		Keys[provider] = Convert.ToBase64String(inArray);
	}
}
