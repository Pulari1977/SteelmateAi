using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeklaChat;

namespace SteelMateAI;

internal static class Program
{
	[DllImport("user32.dll")]
	private static extern bool SetProcessDPIAware();

	[STAThread]
	private static int Main(string[] args)
	{
		AppDomain.CurrentDomain.AssemblyResolve += ResolveFromTekla;
		if (args.Any((string a) => string.Equals(a, "--mcp", StringComparison.OrdinalIgnoreCase)))
		{
			Task.Run((Func<Task?>)RunMcpServerAsync).GetAwaiter().GetResult();
			return 0;
		}
		ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		try
		{
			SetProcessDPIAware();
		}
		catch
		{
		}
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		Application.Run((Form)(object)new MainForm());
		return 0;
	}

	/// <summary>
	/// The Tekla API files need helper files (Trimble.Remoting, Tekla.Common.Geometry, ...).
	/// If one is not next to SteelMateAI.exe, load it from the installed Tekla Structures bin folder.
	/// </summary>
	private static Assembly ResolveFromTekla(object sender, ResolveEventArgs e)
	{
		try
		{
			string file = new AssemblyName(e.Name).Name + ".dll";
			string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
			if (File.Exists(local))
			{
				return Assembly.LoadFrom(local);
			}
			foreach (string bin in TeklaBinFolders())
			{
				string path = Path.Combine(bin, file);
				if (File.Exists(path))
				{
					return Assembly.LoadFrom(path);
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static string[] _teklaBins;

	private static string[] TeklaBinFolders()
	{
		if (_teklaBins != null)
		{
			return _teklaBins;
		}
		var list = new System.Collections.Generic.List<string>();
		string wanted = typeof(Program).Assembly.GetReferencedAssemblies()
			.Where(a => a.Name == "Tekla.Structures.Model").Select(a => a.Version.Major.ToString()).FirstOrDefault() ?? "";
		foreach (string root in new[]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
			Environment.GetEnvironmentVariable("ProgramW6432") ?? "",
			@"C:\Program Files",
			@"D:\Program Files"
		}.Where(r => !string.IsNullOrEmpty(r)).Distinct(StringComparer.OrdinalIgnoreCase))
		{
			string tekla = Path.Combine(root, "Tekla Structures");
			if (!Directory.Exists(tekla))
			{
				continue;
			}
			// Prefer the Tekla version this program was built for, then newest first.
			foreach (string ver in Directory.GetDirectories(tekla)
				.OrderByDescending(d => Path.GetFileName(d).StartsWith(wanted) ? 1 : 0)
				.ThenByDescending(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
			{
				string bin = Path.Combine(ver, "bin");
				if (Directory.Exists(bin))
				{
					list.Add(bin);
				}
			}
		}
		_teklaBins = list.ToArray();
		return _teklaBins;
	}

	private static async Task RunMcpServerAsync()
	{
		HostApplicationBuilder hostApplicationBuilder = Host.CreateApplicationBuilder(Array.Empty<string>());
		hostApplicationBuilder.Logging.ClearProviders();
		IMcpServerBuilder builder = hostApplicationBuilder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
		string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
		if (Directory.Exists(path))
		{
			string[] files = Directory.GetFiles(path, "*.dll");
			foreach (string text in files)
			{
				try
				{
					builder.WithToolsFromAssembly(Assembly.LoadFrom(text));
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Skipped tool DLL " + text + ": " + ex.Message);
				}
			}
		}
		await hostApplicationBuilder.Build().RunAsync();
	}
}
