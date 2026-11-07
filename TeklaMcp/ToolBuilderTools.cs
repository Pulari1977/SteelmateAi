using System;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace TeklaMcp;

[McpServerToolType]
public static class ToolBuilderTools
{
	private const string Csproj = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net48</TargetFramework>\n    <LangVersion>latest</LangVersion>\n    <PlatformTarget>x64</PlatformTarget>\n    <AssemblyName>MyTools</AssemblyName>\n    <TeklaVersion>2026.0.0</TeklaVersion>\n    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>\n    <!-- This folder sits inside the build output folder (tools\\src -> tools), and newer SDKs skip\n         everything under the output folder, which silently builds an empty DLL. List sources explicitly. -->\n    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>\n  </PropertyGroup>\n  <ItemGroup>\n    <Compile Include=\"*.cs\" />\n    <Reference Include=\"SteelMateAI\">\n      <HintPath>..\\..\\SteelMateAI.exe</HintPath>\n      <Private>false</Private>\n    </Reference>\n    <PackageReference Include=\"ModelContextProtocol\" Version=\"1.4.0\" ExcludeAssets=\"runtime\" />\n    <PackageReference Include=\"Tekla.Structures.Model\" Version=\"$(TeklaVersion)\" ExcludeAssets=\"runtime\" />\n    <PackageReference Include=\"Tekla.Structures.Drawing\" Version=\"$(TeklaVersion)\" ExcludeAssets=\"runtime\" />\n    <PackageReference Include=\"Microsoft.NETFramework.ReferenceAssemblies\" Version=\"1.0.3\" PrivateAssets=\"all\" />\n  </ItemGroup>\n</Project>\n";

	private const string BuildBat = "@echo off\ncd /d \"%~dp0\"\nset /p TV=Tekla NuGet version [2026.0.0]: \nif \"%TV%\"==\"\" set TV=2026.0.0\ndotnet build MyTools.csproj -c Release -p:TeklaVersion=%TV% -o ..\nif errorlevel 1 (echo BUILD FAILED & pause & exit /b 1)\necho Done. Restart SteelMate AI to load your tools.\npause\n";

	private static string SrcDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "src");

	[McpServerTool(Name = "tekla_add_tool", ReadOnly = false, Destructive = false)]
	[Description("Save the C# source of a NEW MCP tool, or REPLACE an existing one by using the same name (read it first with tekla_read_tool; the old version is kept as .bak) into the tools\\src folder. The file must contain a public static class with [McpServerToolType] and methods with [McpServerTool(Name=\"...\")] and [Description]. Use TeklaMcp.TeklaSession.Run(model => ...) for Tekla access. After saving, tell the user to click Build mine in the Tools tab; no restart is needed.")]
	public static string AddTool([Description("File name without extension: letters, digits, underscore. Example: CountBeams")] string name, [Description("The complete C# source file.")] string code)
	{
		if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]{0,40}$"))
		{
			throw new ArgumentException("name must start with a letter and contain only letters, digits and underscore (max 41 chars).");
		}
		if (string.IsNullOrWhiteSpace(code) || !code.Contains("McpServerToolType") || !code.Contains("McpServerTool("))
		{
			throw new ArgumentException("code must contain [McpServerToolType] and at least one [McpServerTool(...)] method.");
		}
		Directory.CreateDirectory(SrcDir);
		WriteIfMissing("MyTools.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net48</TargetFramework>\n    <LangVersion>latest</LangVersion>\n    <PlatformTarget>x64</PlatformTarget>\n    <AssemblyName>MyTools</AssemblyName>\n    <TeklaVersion>2026.0.0</TeklaVersion>\n    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>\n    <!-- This folder sits inside the build output folder (tools\\src -> tools), and newer SDKs skip\n         everything under the output folder, which silently builds an empty DLL. List sources explicitly. -->\n    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>\n  </PropertyGroup>\n  <ItemGroup>\n    <Compile Include=\"*.cs\" />\n    <Reference Include=\"SteelMateAI\">\n      <HintPath>..\\..\\SteelMateAI.exe</HintPath>\n      <Private>false</Private>\n    </Reference>\n    <PackageReference Include=\"ModelContextProtocol\" Version=\"1.4.0\" ExcludeAssets=\"runtime\" />\n    <PackageReference Include=\"Tekla.Structures.Model\" Version=\"$(TeklaVersion)\" ExcludeAssets=\"runtime\" />\n    <PackageReference Include=\"Tekla.Structures.Drawing\" Version=\"$(TeklaVersion)\" ExcludeAssets=\"runtime\" />\n    <PackageReference Include=\"Microsoft.NETFramework.ReferenceAssemblies\" Version=\"1.0.3\" PrivateAssets=\"all\" />\n  </ItemGroup>\n</Project>\n");
		WriteIfMissing("build_tools.bat", "@echo off\ncd /d \"%~dp0\"\nset /p TV=Tekla NuGet version [2026.0.0]: \nif \"%TV%\"==\"\" set TV=2026.0.0\ndotnet build MyTools.csproj -c Release -p:TeklaVersion=%TV% -o ..\nif errorlevel 1 (echo BUILD FAILED & pause & exit /b 1)\necho Done. Restart SteelMate AI to load your tools.\npause\n");
		string text = Path.Combine(SrcDir, name + ".cs");
		bool flag = File.Exists(text);
		if (flag)
		{
			File.Copy(text, text + ".bak", overwrite: true);
		}
		File.WriteAllText(text, code);
		return TeklaSession.ToJson(new
		{
			saved = text,
			replaced = flag,
			next = "Tell the user to click Build mine in the SteelMate AI Tools tab (needs the .NET SDK). It builds and loads the tool right away; no restart of SteelMate AI or Tekla is needed."
		});
	}

	[McpServerTool(Name = "tekla_read_tool", ReadOnly = true)]
	[Description("Read the C# source of one of the user's own tools (tools\\src\\<name>.cs) so it can be changed. Then save the updated full file with tekla_add_tool using the same name.")]
	public static string ReadTool([Description("File name without extension, e.g. CountParts")] string name)
	{
		if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]{0,40}$"))
		{
			throw new ArgumentException("name must start with a letter and contain only letters, digits and underscore.");
		}
		string path = Path.Combine(SrcDir, name + ".cs");
		if (!File.Exists(path))
		{
			return "Error: no tool source named '" + name + "'. Use tekla_list_my_tools.";
		}
		return File.ReadAllText(path);
	}

	[McpServerTool(Name = "tekla_list_my_tools", ReadOnly = true)]
	[Description("List the user's own tool source files (tools\\src) and built tool DLLs (tools).")]
	public static string ListMyTools()
	{
		string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
		return TeklaSession.ToJson(new
		{
			sources = (Directory.Exists(SrcDir) ? Directory.GetFiles(SrcDir, "*.cs") : new string[0]),
			dlls = (Directory.Exists(path) ? Directory.GetFiles(path, "*.dll") : new string[0])
		});
	}

	private static void WriteIfMissing(string name, string content)
	{
		string path = Path.Combine(SrcDir, name);
		if (!File.Exists(path))
		{
			File.WriteAllText(path, content);
		}
	}
}
