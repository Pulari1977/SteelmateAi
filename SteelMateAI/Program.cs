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

	/// <summary>Loads the Tekla API files from the Tekla version that is running (2025, 2026, ...).</summary>
	private static Assembly ResolveFromTekla(object sender, ResolveEventArgs e)
	{
		return TeklaMcp.TeklaLocator.Resolve(e.Name);
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
