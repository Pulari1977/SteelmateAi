using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Server;
using Tekla.Structures;
using Tekla.Structures.Drawing;
using Tekla.Structures.Model;

namespace TeklaMcp;

[McpServerToolType]
public static class TeklaRevisionTools
{
	public class Snap
	{
		public string G { get; set; }

		public string M { get; set; }

		public string N { get; set; }

		public string P { get; set; }

		public string T { get; set; }

		public string C { get; set; }

		public string H { get; set; }

		public string A { get; set; }

		public double W { get; set; }

		public double[] B { get; set; }
	}

	private static string Dir
	{
		get
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelMateAI", "snapshots");
			Directory.CreateDirectory(text);
			return text;
		}
	}

	private static string FileFor(string name)
	{
		string text = new string((name ?? "").Where((char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
		if (text.Length == 0)
		{
			throw new ArgumentException("name: use letters, digits, _ or -");
		}
		return Path.Combine(Dir, text + ".jsonl");
	}

	private static string Safe(Func<string> body)
	{
		try
		{
			return body();
		}
		catch (Exception ex)
		{
			return "Error: " + ex.Message;
		}
	}

	private static Snap Take(Tekla.Structures.Model.Part p)
	{
		Solid solid = p.GetSolid();
		string a = "";
		try
		{
			a = p.GetAssembly()?.Identifier.GUID.ToString() ?? "";
		}
		catch
		{
		}
		double value = 0.0;
		try
		{
			value = TeklaSession.ReportDouble(p, "WEIGHT");
		}
		catch
		{
		}
		Snap snap = new Snap();
		snap.G = p.Identifier.GUID.ToString();
		snap.M = TeklaSession.ReportString(p, "PART_POS");
		snap.N = p.Name;
		snap.P = p.Profile.ProfileString;
		snap.T = p.Material.MaterialString;
		snap.C = p.Class;
		snap.H = TeklaSession.ReportString(p, "PHASE");
		snap.A = a;
		snap.W = Math.Round(value, 2);
		snap.B = ((solid == null) ? null : new double[6]
		{
			Math.Round(solid.MinimumPoint.X, 1),
			Math.Round(solid.MinimumPoint.Y, 1),
			Math.Round(solid.MinimumPoint.Z, 1),
			Math.Round(solid.MaximumPoint.X, 1),
			Math.Round(solid.MaximumPoint.Y, 1),
			Math.Round(solid.MaximumPoint.Z, 1)
		});
		return snap;
	}

	[McpServerTool(Name = "tekla_snapshot_save")]
	[Description("Save a named snapshot of the model (every part: mark, name, profile, material, class, phase, weight, bounding box) to a file on this PC, so a later tekla_snapshot_compare can show what changed. Do this at each issue/revision, e.g. name 'IFA_rev0'. Same name overwrites. Does not change the model.")]
	public static string SnapshotSave([Description("Snapshot name, e.g. IFA_rev0")] string name, [Description("Phase number (empty = whole model)")] string phase = "", [Description("Safety cap on number of parts")] int maxParts = 50000)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			string text = FileFor(name);
			Stopwatch stopwatch = Stopwatch.StartNew();
			int num = 0;
			bool flag = false;
			using (StreamWriter streamWriter = new StreamWriter(text + ".tmp", append: false))
			{
				foreach (Tekla.Structures.Model.Part item in TeklaTools.EnumerateParts(model, "any"))
				{
					if (string.IsNullOrWhiteSpace(phase) || !(TeklaSession.ReportString(item, "PHASE") != phase.Trim()))
					{
						if (num >= maxParts || stopwatch.Elapsed.TotalSeconds > 120.0)
						{
							flag = true;
							break;
						}
						streamWriter.WriteLine(JsonSerializer.Serialize(Take(item)));
						num++;
					}
				}
			}
			if (File.Exists(text))
			{
				File.Delete(text);
			}
			File.Move(text + ".tmp", text);
			return TeklaSession.ToJson(new
			{
				saved = name,
				parts = num,
				incomplete = flag,
				file = text,
				note = (flag ? "Stopped early (part cap or 120 s). Use a phase filter and save per phase." : "Snapshot saved.")
			});
		}, "SnapshotSave"));
	}

	[McpServerTool(Name = "tekla_snapshot_list", ReadOnly = true)]
	[Description("List saved model snapshots with their date and size.")]
	public static string SnapshotList()
	{
		return Safe(() => TeklaSession.ToJson((from f in Directory.GetFiles(Dir, "*.jsonl")
			select new
			{
				name = Path.GetFileNameWithoutExtension(f),
				saved = File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm"),
				parts = File.ReadLines(f).Count()
			} into x
			orderby x.saved descending
			select x).ToList()));
	}

	[McpServerTool(Name = "tekla_snapshot_compare", ReadOnly = true)]
	[Description("Compare the CURRENT model with a saved snapshot: parts added, removed, and modified (profile, material, name, class, phase, weight, or moved/resized more than 1 mm), with the weight difference, and the assembly drawings affected by those changes (drawing mark and whether it is up to date). Read-only.")]
	public static string SnapshotCompare([Description("Snapshot name to compare against")] string name, [Description("Phase number (empty = whole model; use the same as when saved)")] string phase = "", [Description("Max examples per list")] int limit = 40)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			string path = FileFor(name);
			if (!File.Exists(path))
			{
				return "Error: no snapshot '" + name + "'. Use tekla_snapshot_list.";
			}
			Dictionary<string, Snap> dictionary = new Dictionary<string, Snap>();
			foreach (string item in File.ReadLines(path))
			{
				Snap snap = JsonSerializer.Deserialize<Snap>(item);
				if (snap != null && snap.G != null)
				{
					dictionary[snap.G] = snap;
				}
			}
			List<object> list = new List<object>();
			List<object> list2 = new List<object>();
			HashSet<string> seen = new HashSet<string>();
			HashSet<string> hashSet = new HashSet<string>();
			int num = 0;
			int num2 = 0;
			double num3 = 0.0;
			Stopwatch stopwatch = Stopwatch.StartNew();
			foreach (Tekla.Structures.Model.Part item2 in TeklaTools.EnumerateParts(model, "any"))
			{
				if (stopwatch.Elapsed.TotalSeconds > 90.0)
				{
					break;
				}
				if (string.IsNullOrWhiteSpace(phase) || !(TeklaSession.ReportString(item2, "PHASE") != phase.Trim()))
				{
					Snap snap2 = Take(item2);
					seen.Add(snap2.G);
					if (!dictionary.TryGetValue(snap2.G, out var value))
					{
						num++;
						num3 += snap2.W;
						if (!string.IsNullOrEmpty(snap2.A))
						{
							hashSet.Add(snap2.A);
						}
						if (list.Count < limit)
						{
							list.Add(new
							{
								guid = snap2.G,
								mark = snap2.M,
								name = snap2.N,
								profile = snap2.P,
								weightKg = snap2.W
							});
						}
					}
					else
					{
						List<string> list3 = new List<string>();
						if (value.P != snap2.P)
						{
							list3.Add("profile " + value.P + " -> " + snap2.P);
						}
						if (value.T != snap2.T)
						{
							list3.Add("material " + value.T + " -> " + snap2.T);
						}
						if (value.N != snap2.N)
						{
							list3.Add("name " + value.N + " -> " + snap2.N);
						}
						if (value.C != snap2.C)
						{
							list3.Add("class " + value.C + " -> " + snap2.C);
						}
						if (value.H != snap2.H)
						{
							list3.Add("phase " + value.H + " -> " + snap2.H);
						}
						if (Math.Abs(value.W - snap2.W) > 0.05)
						{
							list3.Add($"weight {value.W} -> {snap2.W} kg");
						}
						if (value.B != null && snap2.B != null && value.B.Zip(snap2.B, (double a, double b) => Math.Abs(a - b)).Any((double d) => d > 1.0))
						{
							list3.Add("moved or resized");
						}
						if (list3.Count > 0)
						{
							num2++;
							num3 += snap2.W - value.W;
							if (!string.IsNullOrEmpty(snap2.A))
							{
								hashSet.Add(snap2.A);
							}
							if (list2.Count < limit)
							{
								list2.Add(new
								{
									guid = snap2.G,
									mark = snap2.M,
									name = snap2.N,
									changes = list3
								});
							}
						}
					}
				}
			}
			List<Snap> list4 = dictionary.Values.Where((Snap o) => !seen.Contains(o.G) && (string.IsNullOrWhiteSpace(phase) || o.H == phase.Trim())).ToList();
			foreach (Snap item3 in list4)
			{
				num3 -= item3.W;
				if (!string.IsNullOrEmpty(item3.A))
				{
					hashSet.Add(item3.A);
				}
			}
			List<object> list5 = new List<object>();
			try
			{
				DrawingHandler drawingHandler = new DrawingHandler();
				if (drawingHandler.GetConnectionStatus())
				{
					DrawingEnumerator drawings = drawingHandler.GetDrawings();
					while (drawings.MoveNext())
					{
						if (drawings.Current is AssemblyDrawing assemblyDrawing && assemblyDrawing.GetType().GetProperty("AssemblyIdentifier")?.GetValue(assemblyDrawing) is Identifier identifier && hashSet.Contains(identifier.GUID.ToString()) && list5.Count < limit * 2)
						{
							list5.Add(new
							{
								mark = assemblyDrawing.Mark,
								name = assemblyDrawing.Name,
								status = assemblyDrawing.UpToDateStatus.ToString(),
								issued = assemblyDrawing.IsIssued
							});
						}
					}
				}
			}
			catch
			{
			}
			return TeklaSession.ToJson(new
			{
				comparedWith = name,
				snapshotDate = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm"),
				added = new
				{
					count = num,
					examples = list
				},
				removed = new
				{
					count = list4.Count,
					examples = (from r in list4.Take(limit)
						select new
						{
							guid = r.G,
							mark = r.M,
							name = r.N,
							profile = r.P
						}).ToList()
				},
				modified = new
				{
					count = num2,
					examples = list2
				},
				weightChangeKg = Math.Round(num3, 1),
				assembliesAffected = hashSet.Count,
				drawingsAffected = list5,
				note = "Drawings listed are assembly drawings of changed assemblies; update them (tekla_update_drawing) and re-issue as needed."
			});
		}, "SnapshotCompare"));
	}
}
