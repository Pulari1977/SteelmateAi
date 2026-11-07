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
using ModelContextProtocol.Server;
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
		TeklaMcp.TeklaSession.Log("Tool server started (SteelMate AI " + Application.ProductVersion + ")");
		AppDomain.CurrentDomain.UnhandledException += (s, e) => TeklaMcp.TeklaSession.Log("CRASH " + e.ExceptionObject);
		try
		{
			HostApplicationBuilder hostApplicationBuilder = Host.CreateApplicationBuilder(Array.Empty<string>());
			hostApplicationBuilder.Logging.ClearProviders();
			IMcpServerBuilder builder = hostApplicationBuilder.Services.AddMcpServer().WithStdioServerTransport();
			int count = AddTools(builder, typeof(Program).Assembly);
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
			if (Directory.Exists(path))
			{
				foreach (string text in Directory.GetFiles(path, "*.dll"))
				{
					try
					{
						count += AddTools(builder, Assembly.LoadFrom(text));
					}
					catch (Exception ex)
					{
						TeklaMcp.TeklaSession.Log("Skipped tool DLL " + text + ": " + ex);
						Console.Error.WriteLine("Skipped tool DLL " + text + ": " + ex.Message);
					}
				}
			}
			TeklaMcp.TeklaSession.Log("Tool server ready: " + count + " tool groups");
			await hostApplicationBuilder.Build().RunAsync();
			TeklaMcp.TeklaSession.Log("Tool server stopped");
		}
		catch (Exception ex)
		{
			TeklaMcp.TeklaSession.Log("Tool server FAILED: " + ex);
			Console.Error.WriteLine("Tool server failed: " + ex.Message);
			throw;
		}
	}

	/// <summary>
	/// Registers the tool classes of one assembly. Skips classes that cannot load
	/// (instead of failing the whole tool server like WithToolsFromAssembly does).
	/// </summary>
	private static int AddTools(IMcpServerBuilder builder, Assembly asm)
	{
		Type[] types;
		try
		{
			types = asm.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			foreach (Exception le in ex.LoaderExceptions.Where(x => x != null).Take(5))
			{
				TeklaMcp.TeklaSession.Log("Type load problem in " + asm.GetName().Name + ": " + le.Message);
			}
			types = ex.Types.Where(t => t != null).ToArray();
		}
		var toolTypes = new System.Collections.Generic.List<Type>();
		foreach (Type t in types)
		{
			try
			{
				if (t.GetCustomAttributes(typeof(McpServerToolTypeAttribute), false).Length > 0)
				{
					toolTypes.Add(t);
				}
			}
			catch (Exception ex)
			{
				TeklaMcp.TeklaSession.Log("Skipped " + t.FullName + ": " + ex.Message);
			}
		}
		if (toolTypes.Count > 0)
		{
			builder.WithTools(toolTypes);
		}
		return toolTypes.Count;
	}
}
