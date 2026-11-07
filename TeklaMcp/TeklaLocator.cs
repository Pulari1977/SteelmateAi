using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TeklaMcp;

/// <summary>
/// Finds the Tekla Structures "bin" folder to load the Tekla API files from, so one
/// SteelMate AI build works with Tekla 2025 and 2026 (and later versions with the same API).
/// Order: the Tekla that is running now, then the newest installed version.
/// The Tekla API files are NOT shipped next to SteelMateAI.exe, so .NET always asks us.
/// </summary>
public static class TeklaLocator
{
	private static readonly object Gate = new object();
	private static string _bin;
	private static bool _searched;

	/// <summary>Oldest Tekla major version this build supports.</summary>
	public const int MinVersion = 2025;

	public static string BinFolder
	{
		get
		{
			lock (Gate)
			{
				if (!_searched)
				{
					_searched = true;
					_bin = FromRunningTekla() ?? NewestInstalled();
					TeklaSession.Log("Tekla files from: " + (_bin ?? "(no Tekla 2025 or newer found)"));
				}
				return _bin;
			}
		}
	}

	/// <summary>"2026.0" style name of the Tekla version in use, or null.</summary>
	public static string VersionName
	{
		get
		{
			string bin = BinFolder;
			return bin == null ? null : Path.GetFileName(Path.GetDirectoryName(bin));
		}
	}

	public static string Describe()
	{
		return BinFolder == null
			? "No Tekla Structures " + MinVersion + " or newer was found on this PC."
			: "Using Tekla " + VersionName + " (" + BinFolder + ").";
	}

	private static readonly string[] TeklaPrefixes = { "Tekla.", "Trimble.", "DotNetKit", "Polly", "SemanticDb" };

	public static Assembly Resolve(string fullName)
	{
		try
		{
			string name = new AssemblyName(fullName).Name;
			if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			// Already loaded (any version)? Reuse it so only one copy exists.
			Assembly loaded = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
			if (loaded != null)
			{
				return loaded;
			}
			bool isTekla = TeklaPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
			string file = name + ".dll";
			if (isTekla && BinFolder != null)
			{
				string path = Path.Combine(BinFolder, file);
				if (File.Exists(path))
				{
					return Assembly.LoadFrom(path);
				}
			}
			string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
			if (File.Exists(local))
			{
				return Assembly.LoadFrom(local);
			}
		}
		catch (Exception ex)
		{
			TeklaSession.Log("Could not load " + fullName + ": " + ex.Message);
		}
		return null;
	}

	private static string FromRunningTekla()
	{
		try
		{
			foreach (Process p in Process.GetProcessesByName("TeklaStructures"))
			{
				try
				{
					string bin = Path.GetDirectoryName(p.MainModule.FileName);
					if (IsUsable(bin))
					{
						return bin;
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
		return null;
	}

	private static string NewestInstalled()
	{
		var roots = new List<string>();
		foreach (string r in new[]
		{
			Environment.GetEnvironmentVariable("ProgramW6432"),
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
			@"C:\Program Files",
			@"D:\Program Files"
		})
		{
			if (!string.IsNullOrEmpty(r) && !roots.Contains(r, StringComparer.OrdinalIgnoreCase))
			{
				roots.Add(r);
			}
		}
		// Any fixed drive: X:\Program Files\Tekla Structures and X:\Tekla Structures.
		try
		{
			foreach (DriveInfo d in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
			{
				foreach (string r in new[] { Path.Combine(d.RootDirectory.FullName, "Program Files"), d.RootDirectory.FullName })
				{
					if (!roots.Contains(r, StringComparer.OrdinalIgnoreCase))
					{
						roots.Add(r);
					}
				}
			}
		}
		catch
		{
		}
		var found = new List<Tuple<Version, string>>();
		// Installed programs list in Windows (catches custom install folders).
		foreach (string bin in FromUninstallRegistry())
		{
			if (Version.TryParse(VersionFromPath(bin), out Version rv) && rv.Major >= MinVersion && IsUsable(bin))
			{
				found.Add(Tuple.Create(rv, bin));
			}
		}
		foreach (string root in roots)
		{
			string tekla = Path.Combine(root, "Tekla Structures");
			if (!Directory.Exists(tekla))
			{
				continue;
			}
			foreach (string dir in Directory.GetDirectories(tekla))
			{
				string bin = Path.Combine(dir, "bin");
				if (Version.TryParse(VersionFromPath(bin), out Version v) && v.Major >= MinVersion && IsUsable(bin))
				{
					found.Add(Tuple.Create(v, bin));
				}
			}
		}
		return found.OrderByDescending(t => t.Item1).Select(t => t.Item2).FirstOrDefault();
	}

	private static IEnumerable<string> FromUninstallRegistry()
	{
		var result = new List<string>();
		foreach (Microsoft.Win32.RegistryHive hive in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
		{
			try
			{
				using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Registry64);
				using var un = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
				if (un == null)
				{
					continue;
				}
				foreach (string sub in un.GetSubKeyNames())
				{
					using var k = un.OpenSubKey(sub);
					string name = k?.GetValue("DisplayName") as string ?? "";
					string loc = k?.GetValue("InstallLocation") as string ?? "";
					if (name.IndexOf("Tekla Structures", StringComparison.OrdinalIgnoreCase) < 0 || string.IsNullOrWhiteSpace(loc))
					{
						continue;
					}
					foreach (string bin in new[] { Path.Combine(loc, "bin"), loc })
					{
						if (IsUsable(bin))
						{
							result.Add(bin);
						}
					}
				}
			}
			catch
			{
			}
		}
		return result;
	}

	/// <summary>Finds "2025.0" in a path like D:\Apps\Tekla Structures\2025.0\bin.</summary>
	private static string VersionFromPath(string bin)
	{
		foreach (string part in bin.Split('\\', '/').Reverse())
		{
			if (part.Length >= 4 && part.StartsWith("20") && Version.TryParse(part.Contains(".") ? part : part + ".0", out _))
			{
				return part.Contains(".") ? part : part + ".0";
			}
		}
		return "";
	}

	private static bool IsUsable(string bin)
	{
		return !string.IsNullOrEmpty(bin) && File.Exists(Path.Combine(bin, "Tekla.Structures.Model.dll"));
	}
}
