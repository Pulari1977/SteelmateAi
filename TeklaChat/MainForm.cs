using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ModelContextProtocol.Server;
using TeachRecorder;
using Tekla.Structures.Model;

namespace TeklaChat;

public class MainForm : Form
{
	private static readonly Dictionary<string, string[]> ModelChoices = new Dictionary<string, string[]>
	{
		["Claude"] = new string[4] { "claude-sonnet-5-5", "claude-opus-5-5", "claude-fable-5-1", "claude-haiku-5-5" },
		["Gemini"] = new string[4] { "gemini-3.8-flash", "gemini-4-argon", "gemini-3.6-flash", "gemini-3.5-flash-lite" },
		["OpenAI"] = new string[4] { "gpt-6.1-sol", "gpt-6-astra", "gpt-6.1-terra", "gpt-6-luna" }
	};

	private readonly Settings _settings = Settings.Load();

	private readonly McpHost _mcp = new McpHost();

	private List<AgentDef> _agents = new List<AgentDef>();

	private AgentDef _agent;

	private readonly WebView2 _web;

	private Provider _provider;

	private CancellationTokenSource _cts;

	private bool _busy;

	private bool _warnedNoTekla;

	private readonly List<string> _lines;

	private ChatRecord _current;

	private string _contextPrefix;

	private readonly Dictionary<string, TaskCompletionSource<bool>> _approvals;

	private static readonly HashSet<string> BuiltIn = FindBuiltIn();

	private static string ToolsDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");

	public MainForm()
	{
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Expected O, but got Unknown
		WebView2 webView = new WebView2();
		((Control)webView).Dock = (DockStyle)5;
		_web = webView;
		_lines = new List<string>();
		_approvals = new Dictionary<string, TaskCompletionSource<bool>>();
		((Control)this).Text = "SteelMate AI";
		((Control)this).Width = 460;
		((Control)this).Height = 760;
		((Control)this).MinimumSize = new Size(340, 480);
		((Control)this).BackColor = Color.FromArgb(243, 245, 247);
		((Control)this).Controls.Add((Control)(object)_web);
		((Form)this).Shown += async delegate
		{
			await InitWebAsync();
			ApplyDock();
		};
		((Form)this).FormClosing += (FormClosingEventHandler)async delegate
		{
			SaveCurrent();
			if (_settings.DockLeft && _settings.FitTekla)
			{
				DockHelper.Undock((Form)(object)this);
			}
			_settings.Save();
			await _mcp.DisconnectAsync();
		};
	}

	private async System.Threading.Tasks.Task InitWebAsync()
	{
		_ = 1;
		try
		{
			string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelMateAI", "webview");
			CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
			await _web.EnsureCoreWebView2Async(environment);
			_web.CoreWebView2.Settings.AreDevToolsEnabled = false;
			_web.CoreWebView2.Settings.IsStatusBarEnabled = false;
			_web.CoreWebView2.WebMessageReceived += async delegate(object s, CoreWebView2WebMessageReceivedEventArgs e)
			{
				try
				{
					await HandleAsync(JsonNode.Parse(e.WebMessageAsJson));
				}
				catch (Exception ex2)
				{
					Add("e", "Internal error: " + ex2.Message);
				}
			};
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui", "index.html");
			if (!File.Exists(text))
			{
				throw new FileNotFoundException("ui\\index.html is missing next to SteelMateAI.exe. Keep every file in the dist folder together.");
			}
			_web.CoreWebView2.Navigate(new Uri(text).AbsoluteUri);
		}
		catch (Exception ex)
		{
			MessageBox.Show((IWin32Window)(object)this, "The SteelMate AI window needs the Microsoft WebView2 Runtime (already on Windows 11 and most Windows 10).\n\nDownload it from: https://developer.microsoft.com/microsoft-edge/webview2/\n\nDetails: " + ex.Message, "SteelMate AI", (MessageBoxButtons)0, (MessageBoxIcon)48);
		}
	}

	private void Post(object message)
	{
		if (((Control)this).IsDisposed)
		{
			return;
		}
		string json = JsonSerializer.Serialize(message);
		if (((Control)this).InvokeRequired)
		{
			try
			{
				((Control)this).BeginInvoke((Delegate)(Action)delegate
				{
					PostNow(json);
				});
				return;
			}
			catch
			{
				return;
			}
		}
		PostNow(json);
	}

	private void PostNow(string json)
	{
		try
		{
			_web.CoreWebView2?.PostWebMessageAsJson(json);
		}
		catch
		{
		}
	}

	private void Add(string kind, string text)
	{
		_lines.Add(kind + ":" + text);
		Post(new
		{
			type = "append",
			kind = kind,
			text = text
		});
	}

	private async System.Threading.Tasks.Task HandleAsync(JsonNode m)
	{
		string text = (string?)m["type"];
		if (text == null)
		{
			return;
		}
		if (text == "claudeDesktop")
		{
			ConnectClaudeDesktop();
			return;
		}
		switch (text.Length)
		{
		case 5:
			switch (text[0])
			{
			case 'r':
				if (text == "ready")
				{
					PostInit();
					Post(new
					{
						type = "welcome"
					});
					ConnectAsync();
					AutoConnectAiAsync(fromSettings: false);
				}
				break;
			case 't':
				if (text == "tools")
				{
					PostTools();
				}
				break;
			}
			break;
		case 4:
			switch (text[1])
			{
			case 'e':
			{
				if (!(text == "send"))
				{
					break;
				}
				List<Attachment> list = ReadFiles(m["files"] as JsonArray);
				string text2 = ((string?)m["text"]) ?? "";
				if (m["screen"] != null && (bool)m["screen"])
				{
					Attachment attachment = CaptureScreen();
					if (attachment != null)
					{
						list.Add(attachment);
						text2 += "\n\n[A screenshot of my screen is attached (screen.jpg). Use it to see what I am looking at in Tekla, then do what I asked with the Tekla tools. Confirm the objects with tekla_get_selected or tekla_find_parts before changing anything.]";
					}
				}
				await SendAsync(text2, list);
				break;
			}
			case 't':
				if (text == "stop")
				{
					_cts?.Cancel();
				}
				break;
			}
			break;
		case 8:
			switch (text[3])
			{
			case 'M':
				if (text == "setModel")
				{
					SetModel((string?)m["provider"], (string?)m["model"]);
				}
				break;
			case 'A':
				if (text == "setAgent")
				{
					_agent = _agents.FirstOrDefault((AgentDef a) => a.Id == (string?)m["id"]);
					if (_provider != null)
					{
						_provider.Reset();
					}
					Add("n", (_agent == null) ? "Agent: General assistant." : ("Agent: " + _agent.Name + " (" + (_agent.All ? "all tools" : (_agent.ReadOnly ? "read-only" : "limited tools")) + ")."));
				}
				break;
			case 't':
				if (text == "continue")
				{
					ContinueChat((string?)m["id"]);
				}
				break;
			case 'i':
				if (text == "minimize")
				{
					((Form)this).WindowState = (FormWindowState)1;
				}
				break;
			}
			break;
		case 7:
			switch (text[0])
			{
			case 'h':
				if (text == "history")
				{
					PostHistory();
				}
				break;
			case 'n':
				if (text == "newChat")
				{
					NewChat();
				}
				break;
			case 'a':
			{
				if (text == "approve" && _approvals.TryGetValue((string?)m["id"], out var value))
				{
					value.TrySetResult((bool)m["allow"]);
				}
				break;
			}
			}
			break;
		case 10:
			switch (text[0])
			{
			case 'h':
				if (text == "historyGet")
				{
					PostHistoryOne((string?)m["id"]);
				}
				break;
			case 'b':
				if (text == "buildTools")
				{
					await BuildToolsAsync();
				}
				break;
			}
			break;
		case 6:
			switch (text[0])
			{
			case 'd':
				if (text == "delete")
				{
					ChatStore.Delete((string?)m["id"]);
					PostHistory();
				}
				break;
			case 'r':
				if (text == "record" && _settings.EnableRecorder)
				{
					((Form)new RecorderForm()).Show((IWin32Window)(object)this);
				}
				break;
			}
			break;
		case 3:
			if (text == "ask")
			{
				_settings.AskBeforeChanges = (bool)m["value"];
				_settings.Save();
			}
			break;
		case 12:
			if (text == "saveSettings")
			{
				SaveSettings(m);
				await AutoConnectAiAsync(fromSettings: true);
				await ConnectAsync();
			}
			break;
		case 9:
			if (text == "reconnect")
			{
				await ConnectAsync();
				PostAgents();
			}
			break;
		case 11:
			if (text == "toolsFolder")
			{
				OpenToolsFolder();
			}
			break;
		}
	}

	private static Attachment CaptureScreen()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		try
		{
			Rectangle bounds = Screen.PrimaryScreen.Bounds;
			Bitmap val = new Bitmap(bounds.Width, bounds.Height);
			try
			{
				Graphics val2 = Graphics.FromImage((Image)(object)val);
				try
				{
					val2.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				double num = Math.Min(1.0, 1568.0 / (double)Math.Max(bounds.Width, bounds.Height));
				Bitmap val3 = new Bitmap((Image)(object)val, (int)((double)bounds.Width * num), (int)((double)bounds.Height * num));
				try
				{
					using MemoryStream memoryStream = new MemoryStream();
					((Image)val3).Save((Stream)memoryStream, ImageFormat.Jpeg);
					return new Attachment
					{
						Name = "screen.jpg",
						MimeType = "image/jpeg",
						Base64 = Convert.ToBase64String(memoryStream.ToArray())
					};
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch
		{
			return null;
		}
	}

	private static List<Attachment> ReadFiles(JsonArray arr)
	{
		List<Attachment> list = new List<Attachment>();
		if (arr == null)
		{
			return list;
		}
		foreach (JsonNode item in arr)
		{
			list.Add(new Attachment
			{
				Name = (string?)item["name"],
				MimeType = (string?)item["mime"],
				Base64 = (string?)item["b64"]
			});
		}
		return list;
	}

	private void PostInit()
	{
		string provider = (ModelChoices.ContainsKey(_settings.Provider) ? _settings.Provider : "Claude");
		PostAgents();
		Post(new
		{
			type = "init",
			models = ModelChoices,
			provider = provider,
			chosen = _settings.Models,
			ask = _settings.AskBeforeChanges,
			dock = _settings.DockLeft,
			side = (_settings.DockRight ? "Right" : "Left"),
			fit = _settings.FitTekla,
			rec = _settings.EnableRecorder,
			agents = _settings.ShowAgents,
			ver = Application.ProductVersion,
			hasKey = !string.IsNullOrEmpty(_settings.GetKey(provider))
		});
	}

	private void PostAgents()
	{
		_agents = AgentStore.Load();
		if (_agent != null)
		{
			_agent = _agents.FirstOrDefault((AgentDef a) => a.Id == _agent.Id);
		}
		Post(new
		{
			type = "agents",
			current = (_agent?.Id ?? ""),
			list = _agents.Select((AgentDef a) => new
			{
				id = a.Id,
				name = a.Name,
				desc = a.Description,
				mode = (a.All ? "all tools" : (a.ReadOnly ? "read-only" : "limited tools"))
			}).ToList()
		});
	}

	private void SetModel(string p, string model)
	{
		if (ModelChoices.ContainsKey(p) && !string.IsNullOrWhiteSpace(model))
		{
			if (p != _settings.Provider)
			{
				_provider = null;
			}
			_settings.Provider = p;
			_settings.Models[p] = model.Trim();
			_settings.Save();
			PostInit();
			Add("n", "Model: " + p + " · " + model.Trim() + (string.IsNullOrEmpty(_settings.GetKey(p)) ? ("\nNo API key saved for " + p + " yet. Open Settings (the gear) and paste it.") : ""));
		}
	}

	private void SaveSettings(JsonNode m)
	{
		string text = (string?)m["provider"];
		if (!ModelChoices.ContainsKey(text))
		{
			text = "Claude";
		}
		if (text != _settings.Provider)
		{
			_provider = null;
		}
		_settings.Provider = text;
		string text2 = (((string?)m["model"]) ?? "").Trim();
		if (text2.Length > 0)
		{
			_settings.Models[text] = text2;
		}
		string text3 = (((string?)m["key"]) ?? "").Trim();
		if (text3.Length > 0)
		{
			_settings.SetKey(text, text3);
		}
		_settings.AskBeforeChanges = (bool)m["ask"];
		_settings.DockLeft = (bool)m["dock"];
		_settings.DockRight = (string?)m["side"] != "Left";
		_settings.FitTekla = (bool)m["fit"];
		_settings.EnableRecorder = m["rec"] != null && (bool)m["rec"];
		_settings.ShowAgents = m["agents"] != null && (bool)m["agents"];
		if (!_settings.ShowAgents && _agent != null)
		{
			_agent = null;
			if (_provider != null)
			{
				_provider.Reset();
			}
		}
		_settings.Save();
		if (((Control)this).InvokeRequired)
		{
			((Control)this).BeginInvoke((Delegate)new Action(ApplyDock));
		}
		else
		{
			ApplyDock();
		}
		PostInit();
	}

	private void ApplyDock()
	{
		if (_settings.DockLeft)
		{
			DockHelper.Dock((Form)(object)this, _settings.DockRight, _settings.FitTekla);
		}
		else
		{
			DockHelper.Undock((Form)(object)this);
		}
	}

	private async System.Threading.Tasks.Task ConnectAsync()
	{
		Post(new
		{
			type = "status",
			text = Status(null, "connecting…", null),
			state = StatusState()
		});
		try
		{
			await _mcp.ConnectAsync(Application.ExecutablePath);
			Post(new
			{
				type = "status",
				text = Status(null, _mcp.Tools.Count + " tools", true),
				state = StatusState()
			});
			PostTools();
		}
		catch (Exception ex)
		{
			Post(new
			{
				type = "status",
				text = Status(null, "not connected", false),
				state = StatusState()
			});
			TeklaMcp.TeklaSession.Log("Could not start the tool server: " + ex);
			Add("e", "Could not start the Tekla tool server: " + ex.Message + (ex.InnerException != null ? " (" + ex.InnerException.Message + ")" : "") + "\nDetails are in %LOCALAPPDATA%\\SteelMateAI\\mcp.log. Press Reconnect on the Tools tab to try again.");
		}
	}

	private string _aiText = "…", _teklaText = "…";

	private bool? _aiOk, _teklaOk;

	/// <summary>One status line for both links, e.g. "Claude: connected · Tekla: 58 tools".</summary>
	private string Status(string ai, string tekla, bool? teklaOk, bool? aiOk = null)
	{
		if (ai != null) { _aiText = ai; _aiOk = aiOk; }
		if (tekla != null) { _teklaText = tekla; _teklaOk = teklaOk; }
		return _settings.Provider + ": " + _aiText + " · Tekla: " + _teklaText;
	}

	private string StatusState()
	{
		if (_aiOk == true && _teklaOk == true) return "on";
		if (_aiOk == false) return "err";
		return _aiOk == true ? "on" : "";
	}

	/// <summary>
	/// Registers SteelMateAI.exe --mcp as a tool server in the Claude desktop app, so the user can
	/// drive Tekla from Claude Desktop with a Claude Pro/Max subscription (no API key).
	/// </summary>
	private void ConnectClaudeDesktop()
	{
		try
		{
			var configs = new List<string>();
			string roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude");
			if (Directory.Exists(roaming))
			{
				configs.Add(Path.Combine(roaming, "claude_desktop_config.json"));
			}
			// Microsoft Store install keeps its own copy of the folder.
			string packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
			if (Directory.Exists(packages))
			{
				foreach (string pkg in Directory.GetDirectories(packages, "Claude_*"))
				{
					string dir = Path.Combine(pkg, "LocalCache", "Roaming", "Claude");
					if (Directory.Exists(dir))
					{
						configs.Add(Path.Combine(dir, "claude_desktop_config.json"));
					}
				}
			}
			if (configs.Count == 0)
			{
				Add("e", "The Claude desktop app was not found on this PC. Install it from https://claude.ai/download , sign in once, close it, then press Connect to Claude Desktop again.");
				return;
			}
			foreach (string path in configs)
			{
				JsonObject root = null;
				if (File.Exists(path))
				{
					string old = File.ReadAllText(path);
					File.Copy(path, path + ".steelmate-backup", true);
					try
					{
						root = JsonNode.Parse(string.IsNullOrWhiteSpace(old) ? "{}" : old) as JsonObject;
					}
					catch
					{
						Add("e", "Could not read " + path + " (it is not valid JSON). Nothing was changed.");
						continue;
					}
				}
				root = root ?? new JsonObject();
				if (!(root["mcpServers"] is JsonObject servers))
				{
					servers = new JsonObject();
					root["mcpServers"] = servers;
				}
				servers["steelmate-tekla"] = new JsonObject
				{
					["command"] = Application.ExecutablePath,
					["args"] = new JsonArray { "--mcp" }
				};
				File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
			}
			Add("n", "Done: Claude Desktop now has your Tekla tools (\"steelmate-tekla\").\n1) Fully quit Claude Desktop: right-click its icon near the clock, then Quit.\n2) Open it again and start a new chat.\n3) Open Tekla with a model and ask, e.g. \"Give me a weight take-off of the model\".\nClaude Desktop asks before using each tool the first time.");
		}
		catch (Exception ex)
		{
			Add("e", "Could not set up Claude Desktop: " + ex.Message);
		}
	}

	private static readonly string[] VendorOrder = { "Claude", "OpenAI", "Gemini" };

	private static readonly Dictionary<string, string[]> EnvKeyNames = new Dictionary<string, string[]>
	{
		["Claude"] = new[] { "ANTHROPIC_API_KEY", "CLAUDE_API_KEY" },
		["OpenAI"] = new[] { "OPENAI_API_KEY" },
		["Gemini"] = new[] { "GEMINI_API_KEY", "GOOGLE_API_KEY" }
	};

	/// <summary>
	/// Connects to the AI with no clicks: picks up keys from Windows environment variables,
	/// chooses a vendor that has a key (Claude first), checks the key, and refreshes every
	/// vendor's model list from the vendor itself so new models appear automatically.
	/// </summary>
	private async System.Threading.Tasks.Task AutoConnectAiAsync(bool fromSettings)
	{
		try
		{
			// Saved model lists from last time (so dropdowns are current even before the check finishes).
			foreach (var kv in _settings.ModelLists)
			{
				if (ModelChoices.ContainsKey(kv.Key) && kv.Value != null && kv.Value.Count > 0)
				{
					ModelChoices[kv.Key] = kv.Value.ToArray();
				}
			}
			bool changed = false;
			foreach (string v in VendorOrder)
			{
				if (!string.IsNullOrEmpty(_settings.GetKey(v)))
				{
					continue;
				}
				foreach (string env in EnvKeyNames[v])
				{
					string k = Environment.GetEnvironmentVariable(env, EnvironmentVariableTarget.User)
						?? Environment.GetEnvironmentVariable(env, EnvironmentVariableTarget.Machine)
						?? Environment.GetEnvironmentVariable(env);
					if (!string.IsNullOrWhiteSpace(k))
					{
						_settings.SetKey(v, k);
						changed = true;
						Add("n", "Found your " + v + " key in Windows (" + env + ") and saved it.");
						break;
					}
				}
			}
			if (!fromSettings && string.IsNullOrEmpty(_settings.GetKey(_settings.Provider)))
			{
				string withKey = VendorOrder.FirstOrDefault(v => !string.IsNullOrEmpty(_settings.GetKey(v)));
				if (withKey != null && withKey != _settings.Provider)
				{
					_settings.Provider = withKey;
					_provider = null;
					changed = true;
				}
			}
			if (changed)
			{
				_settings.Save();
				PostInit();
			}
			string provider = _settings.Provider;
			string key = _settings.GetKey(provider);
			if (string.IsNullOrWhiteSpace(key))
			{
				Post(new { type = "status", text = Status("no key yet", null, null, false), state = StatusState() });
				Add(fromSettings ? "e" : "n", "No API key saved for " + provider + ". Either paste an API key in Settings (the gear), or use your Claude Pro/Max subscription: Settings > Connect to Claude Desktop.");
				return;
			}
			Post(new { type = "status", text = Status("connecting…", null, null, null), state = StatusState() });
			string model = await RefreshModelsAsync(provider, key, true);
			Add("n", provider + " connected · " + model);
			Post(new { type = "status", text = Status("connected", null, null, true), state = StatusState() });
			// Refresh the other vendors' lists quietly when their keys are saved.
			foreach (string v in VendorOrder.Where(v => v != provider && !string.IsNullOrEmpty(_settings.GetKey(v))))
			{
				try
				{
					await RefreshModelsAsync(v, _settings.GetKey(v), false);
				}
				catch
				{
				}
			}
			PostInit();
		}
		catch (Exception ex)
		{
			Post(new { type = "status", text = Status("not connected", null, null, false), state = StatusState() });
			Add("e", Provider.Explain(_settings.Provider, ex));
		}
	}

	/// <summary>Gets the vendor's current model list, keeps a valid model chosen, and returns it.</summary>
	private async Task<string> RefreshModelsAsync(string provider, string key, bool repost)
	{
		List<string> list = await Provider.ListModelsAsync(provider, key);
		if (list.Count > 0)
		{
			// Put the recommended everyday model first.
			string best = provider == "Claude"
				? (list.FirstOrDefault(m => m == "claude-sonnet-5-5") ?? list.FirstOrDefault(m => m.Contains("sonnet")) ?? list[0])
				: list[0];
			list.Remove(best);
			list.Insert(0, best);
			ModelChoices[provider] = list.ToArray();
			_settings.ModelLists[provider] = list;
			if (!_settings.Models.TryGetValue(provider, out string cur) || string.IsNullOrEmpty(cur) || !list.Contains(cur))
			{
				if (!string.IsNullOrEmpty(cur))
				{
					Add("n", provider + ": model '" + cur + "' is no longer offered, switched to " + best + ".");
				}
				_settings.Models[provider] = best;
				if (provider == _settings.Provider && _provider != null)
				{
					_provider.Model = best;
				}
			}
			_settings.Save();
			if (repost)
			{
				PostInit();
			}
		}
		return _settings.Models.TryGetValue(provider, out string m2) && !string.IsNullOrEmpty(m2) ? m2 : ModelChoices[provider][0];
	}

	private Provider MakeProvider()
	{
		string provider = _settings.Provider;
		if (!(provider == "Gemini"))
		{
			if (!(provider == "OpenAI"))
			{
				return new ClaudeProvider();
			}
			return new OpenAiProvider();
		}
		return new GeminiProvider();
	}

	private async System.Threading.Tasks.Task SendAsync(string text, List<Attachment> files)
	{
		if (_busy)
		{
			return;
		}
		text = text.Trim();
		if (text.Length == 0 && files.Count == 0)
		{
			return;
		}
		if (text.Length == 0)
		{
			text = "Read the attachment and list what it shows (members, dimensions, levels). Do not create anything yet.";
		}
		if (!_mcp.Connected && !_warnedNoTekla)
		{
			_warnedNoTekla = true;
			Add("n", "Tekla tools are not connected, so Claude can chat and read attachments but cannot touch the model. Open Tekla with a model, then press Reconnect on the Tools tab.");
		}
		string provider = _settings.Provider;
		string key = _settings.GetKey(provider);
		if (string.IsNullOrWhiteSpace(key))
		{
			Add("e", "Paste your API key first (Settings, the gear).");
			return;
		}
		if (_provider == null)
		{
			_provider = MakeProvider();
			_provider.Reset();
		}
		_provider.ApiKey = key;
		_provider.Model = ((_settings.Models.TryGetValue(provider, out var value) && !string.IsNullOrEmpty(value)) ? value : ModelChoices[provider][0]);
		if (_current == null)
		{
			_current = new ChatRecord
			{
				Id = DateTime.Now.ToString("yyyyMMdd_HHmmss"),
				Started = DateTime.Now,
				Title = ((text.Length > 70) ? (text.Substring(0, 70) + "…") : text.Replace("\r", " ").Replace("\n", " ")),
				Provider = provider,
				Model = _provider.Model
			};
		}
		string userText = ((_contextPrefix != null) ? (_contextPrefix + text) : text);
		_contextPrefix = null;
		Add("u", (files.Count > 0) ? (text + "\n\ud83d\udcce " + string.Join(", ", files.Select((Attachment f) => f.Name))) : text);
		_busy = true;
		Post(new
		{
			type = "busy",
			value = true
		});
		_cts = new CancellationTokenSource();
		try
		{
			_provider.ExtraSystem = _agent?.Instructions;
			await _provider.RunTurnAsync(userText, files, _mcp.Connected ? AgentStore.Filter(_agent, _mcp.Tools) : new List<ToolDef>(), CallToolWithApproval, delegate(string t)
			{
				Add("a", t);
			}, _cts.Token);
		}
		catch (OperationCanceledException)
		{
			Add("n", "Stopped.");
		}
		catch (Exception ex2)
		{
			Add("e", Provider.Explain(provider, ex2));
		}
		finally
		{
			_busy = false;
			Post(new
			{
				type = "busy",
				value = false
			});
			SaveCurrent();
		}
	}

	private async Task<string> CallToolWithApproval(string name, JsonNode args)
	{
		ToolDef toolDef = _mcp.Tools.FirstOrDefault((ToolDef t) => t.Name == name);
		string text = args?.ToJsonString() ?? "{}";
		Add("t", name + " " + text);
		bool flag = toolDef?.ReadOnly ?? false;
		bool flag2 = toolDef?.Destructive ?? false;
		if (flag2 || (!flag && _settings.AskBeforeChanges))
		{
			string id = Guid.NewGuid().ToString("N");
			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
			_approvals[id] = tcs;
			CancellationTokenRegistration reg = _cts.Token.Register(delegate
			{
				tcs.TrySetResult(result: false);
			});
			Post(new
			{
				type = "approval",
				id = id,
				tool = name,
				destructive = flag2,
				args = (args?.ToJsonString(new JsonSerializerOptions
				{
					WriteIndented = true
				}) ?? "{}")
			});
			bool flag3;
			try
			{
				flag3 = await tcs.Task;
			}
			finally
			{
				reg.Dispose();
				_approvals.Remove(id);
			}
			if (!flag3)
			{
				Add("r", "declined");
				return "The user declined this action. Do not retry it; ask what they want instead.";
			}
		}
		string text2;
		try
		{
			text2 = await _mcp.CallAsync(name, args);
		}
		catch (Exception ex)
		{
			text2 = "ERROR: " + ex.Message;
		}
		Add("r", (text2.Length > 1500) ? (text2.Substring(0, 1500) + "…") : text2);
		return (text2.Length > 60000) ? (text2.Substring(0, 60000) + "\n…(truncated)") : text2;
	}

	private static HashSet<string> FindBuiltIn()
	{
		HashSet<string> hashSet = new HashSet<string>();
		try
		{
			Type[] types = typeof(MainForm).Assembly.GetTypes();
			for (int i = 0; i < types.Length; i++)
			{
				MethodInfo[] methods = types[i].GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
				for (int j = 0; j < methods.Length; j++)
				{
					McpServerToolAttribute customAttribute = methods[j].GetCustomAttribute<McpServerToolAttribute>();
					if (customAttribute != null && !string.IsNullOrEmpty(customAttribute.Name))
					{
						hashSet.Add(customAttribute.Name);
					}
				}
			}
		}
		catch
		{
		}
		return hashSet;
	}

	private static string GroupOf(ToolDef t)
	{
		if (!BuiltIn.Contains(t.Name))
		{
			return "Your tools";
		}
		string name = t.Name;
		if (t.Destructive)
		{
			return "Deletes";
		}
		if (name.Contains("drawing"))
		{
			return "Drawings";
		}
		if (name.Contains("export") || name.Contains("report"))
		{
			return "Export";
		}
		if (name.Contains("my_tool") || name.EndsWith("add_tool"))
		{
			return "Manage your tools";
		}
		if (!t.ReadOnly)
		{
			return "Change the model";
		}
		return "Read the model";
	}

	private void PostTools()
	{
		var list = (from t in _mcp.Tools
			orderby t.Name
			select new
			{
				name = t.Name,
				desc = t.Description,
				@group = GroupOf(t),
				ask = (t.Destructive ? "Always" : (t.ReadOnly ? "No" : "Yes")),
				mine = !BuiltIn.Contains(t.Name),
				inputs = (t.Schema?["properties"]?.ToJsonString(new JsonSerializerOptions
				{
					WriteIndented = true
				}) ?? "")
			}).ToList();
		Post(new
		{
			type = "tools",
			list = list
		});
	}

	private void OpenToolsFolder()
	{
		try
		{
			Directory.CreateDirectory(Path.Combine(ToolsDir, "src"));
			Process.Start("explorer.exe", ToolsDir);
		}
		catch (Exception ex)
		{
			Add("e", ex.Message);
		}
	}

	private async System.Threading.Tasks.Task BuildToolsAsync()
	{
		string proj = Path.Combine(ToolsDir, "src", "MyTools.csproj");
		if (!File.Exists(proj))
		{
			Add("e", "No tools yet. Ask me to make a tool first.");
			return;
		}
		Post(new
		{
			type = "status",
			text = "Building your tools…",
			state = ""
		});
		try
		{
			await _mcp.DisconnectAsync();
			Version version = typeof(Model).Assembly.GetName().Version;
			string tv = version.Major + "." + version.Minor + "." + version.Build;
			string text = await System.Threading.Tasks.Task.Run(delegate
			{
				using Process process = Process.Start(new ProcessStartInfo("dotnet", "build \"" + proj + "\" -c Release -p:TeklaVersion=" + tv + " -o \"" + ToolsDir + "\" --nologo -v q")
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					WorkingDirectory = Path.GetDirectoryName(proj)
				});
				string text2 = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
				process.WaitForExit();
				return ((process.ExitCode == 0) ? "OK" : "FAILED") + "\n" + text2;
			});
			if (text.StartsWith("OK"))
			{
				Add("n", "Your tools were built. Loading them now…");
			}
			else
			{
				IEnumerable<string> values = (from l in text.Split(new char[1] { '\n' })
					where l.Contains("error")
					select l).Distinct().Take(12);
				Add("e", "Build failed. Paste this into the chat and I will fix the tool:\n" + string.Join("\n", values));
			}
		}
		catch (Exception ex)
		{
			Add("e", "Could not run the build (is the .NET SDK installed?): " + ex.Message);
		}
		await ConnectAsync();
	}

	private void SaveCurrent()
	{
		if (_current != null)
		{
			_current.Lines = _lines.ToList();
			ChatStore.Save(_current);
		}
	}

	private void PostHistory()
	{
		SaveCurrent();
		var list = (from r in ChatStore.LoadAll()
			select new
			{
				id = r.Id,
				title = (r.Title ?? "(untitled)"),
				when = r.Started.ToString("dd MMM HH:mm"),
				model = r.Model
			}).ToList();
		Post(new
		{
			type = "history",
			list = list
		});
	}

	private static string Plain(IEnumerable<string> lines)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string line in lines)
		{
			string text = ((line.Length > 1 && line[1] == ':') ? line.Substring(0, 1) : "n");
			string text2 = ((line.Length > 1 && line[1] == ':') ? line.Substring(2) : line);
			switch (text)
			{
			case "u":
				stringBuilder.Append("You: ").AppendLine(text2);
				break;
			case "a":
				stringBuilder.Append("Claude: ").AppendLine(text2);
				break;
			case "t":
				stringBuilder.Append("(tool) ").AppendLine((text2.Length > 200) ? (text2.Substring(0, 200) + "…") : text2);
				break;
			default:
				stringBuilder.AppendLine(text2);
				break;
			case "r":
				continue;
			}
			stringBuilder.AppendLine();
		}
		return stringBuilder.ToString().TrimEnd(Array.Empty<char>());
	}

	private void PostHistoryOne(string id)
	{
		ChatRecord chatRecord = ChatStore.LoadAll().FirstOrDefault((ChatRecord x) => x.Id == id);
		if (chatRecord != null)
		{
			Post(new
			{
				type = "historyOne",
				id = id,
				text = Plain(chatRecord.Lines)
			});
		}
	}

	private void NewChat()
	{
		if (!_busy)
		{
			SaveCurrent();
			_current = null;
			_lines.Clear();
			_contextPrefix = null;
			_provider = null;
			Post(new
			{
				type = "clear"
			});
			Post(new
			{
				type = "welcome"
			});
			Post(new
			{
				type = "tab",
				name = "chat"
			});
		}
	}

	private void ContinueChat(string id)
	{
		if (_busy)
		{
			return;
		}
		ChatRecord chatRecord = ChatStore.LoadAll().FirstOrDefault((ChatRecord x) => x.Id == id);
		if (chatRecord != null)
		{
			SaveCurrent();
			_current = chatRecord;
			_provider = null;
			_lines.Clear();
			_lines.AddRange(chatRecord.Lines);
			var lines = chatRecord.Lines.Select((string l) => new
			{
				kind = ((l.Length > 1 && l[1] == ':') ? l.Substring(0, 1) : "n"),
				text = ((l.Length > 1 && l[1] == ':') ? l.Substring(2) : l)
			}).ToList();
			Post(new
			{
				type = "replay",
				lines = lines
			});
			string text = Plain(chatRecord.Lines);
			if (text.Length > 20000)
			{
				text = text.Substring(text.Length - 20000);
			}
			_contextPrefix = "Earlier conversation, only for context (tool results may be out of date, check the model again before relying on them):\n" + text + "\n\n---\nNew message from the user:\n";
		}
	}
}
