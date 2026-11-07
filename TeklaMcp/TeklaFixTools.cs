using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;

namespace TeklaMcp;

[McpServerToolType]
public static class TeklaFixTools
{
	[CompilerGenerated]
	private sealed class _003CTeklaTypes_003Ed__8 : IEnumerable<Type>, IEnumerable, IEnumerator<Type>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private Type _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private System.Reflection.Assembly[] _003C_003E7__wrap1;

		private int _003C_003E7__wrap2;

		private Type[] _003C_003E7__wrap3;

		private int _003C_003E7__wrap4;

		Type IEnumerator<Type>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CTeklaTypes_003Ed__8(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E7__wrap1 = null;
			_003C_003E7__wrap3 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			if (num != 0)
			{
				if (num != 1)
				{
					return false;
				}
				_003C_003E1__state = -1;
				_003C_003E7__wrap4++;
				goto IL_00e0;
			}
			_003C_003E1__state = -1;
			_003C_003E7__wrap1 = AppDomain.CurrentDomain.GetAssemblies();
			_003C_003E7__wrap2 = 0;
			goto IL_0105;
			IL_0105:
			if (_003C_003E7__wrap2 < _003C_003E7__wrap1.Length)
			{
				System.Reflection.Assembly assembly = _003C_003E7__wrap1[_003C_003E7__wrap2];
				if (assembly.GetName().Name.StartsWith("Tekla.Structures", StringComparison.OrdinalIgnoreCase))
				{
					Type[] array;
					try
					{
						array = assembly.GetExportedTypes();
					}
					catch (ReflectionTypeLoadException ex)
					{
						array = ex.Types.Where((Type t) => t != null).ToArray();
					}
					catch
					{
						goto IL_00f7;
					}
					_003C_003E7__wrap3 = array;
					_003C_003E7__wrap4 = 0;
					goto IL_00e0;
				}
				goto IL_00f7;
			}
			_003C_003E7__wrap1 = null;
			return false;
			IL_00e0:
			if (_003C_003E7__wrap4 < _003C_003E7__wrap3.Length)
			{
				Type type = _003C_003E7__wrap3[_003C_003E7__wrap4];
				_003C_003E2__current = type;
				_003C_003E1__state = 1;
				return true;
			}
			_003C_003E7__wrap3 = null;
			goto IL_00f7;
			IL_00f7:
			_003C_003E7__wrap2++;
			goto IL_0105;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<Type> IEnumerable<Type>.GetEnumerator()
		{
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				return this;
			}
			return new _003CTeklaTypes_003Ed__8(0);
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<Type>)this).GetEnumerator();
		}
	}

	private const string AiTag = "TEKLAAI_CREATED";

	private const string AiChanged = "TEKLAAI_CHANGED";

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

	private static List<Part> TargetParts(Model model, string guids, string type, string name, string profile, string material, string partClass, string phase)
	{
		if (!string.IsNullOrWhiteSpace(guids))
		{
			return TeklaTools.ResolveTargets(model, guids).OfType<Part>().ToList();
		}
		return (from p in TeklaTools.EnumerateParts(model, type)
			where TeklaTools.Contains(p.Name, name) && TeklaTools.Contains(p.Profile.ProfileString, profile) && TeklaTools.Contains(p.Material.MaterialString, material) && (string.IsNullOrEmpty(partClass) || p.Class == partClass) && (string.IsNullOrWhiteSpace(phase) || TeklaSession.ReportString(p, "PHASE") == phase.Trim())
			select p).ToList();
	}

	[McpServerTool(Name = "tekla_bulk_modify")]
	[Description("Change profile / material / name / class on MANY parts at once. PREVIEW by default (apply=false): shows how many parts match and a before/after sample. Show that to the user, then call again with apply=true. Choose targets with guids ('selected' or comma list) OR filters (type, name, profile, material, partClass, phase). Changed parts are tagged TEKLAAI_CHANGED. Max 500 parts.")]
	public static string BulkModify([Description("Comma-separated GUIDs or 'selected'. Leave empty to use the filters.")] string guids = "", [Description("Filter: beam | column | plate | polybeam | any")] string type = "any", [Description("Filter: name contains")] string name = "", [Description("Filter: profile contains")] string profile = "", [Description("Filter: material contains")] string material = "", [Description("Filter: class (exact)")] string partClass = "", [Description("Filter: phase number (exact)")] string phase = "", [Description("NEW profile (empty = keep)")] string newProfile = "", [Description("NEW material (empty = keep)")] string newMaterial = "", [Description("NEW name (empty = keep)")] string newName = "", [Description("NEW class (empty = keep)")] string newClass = "", [Description("Must be true to write. Default false = preview only.")] bool apply = false)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			if (string.IsNullOrWhiteSpace(newProfile) && string.IsNullOrWhiteSpace(newMaterial) && string.IsNullOrWhiteSpace(newName) && string.IsNullOrWhiteSpace(newClass))
			{
				return "Error: nothing to change. Give newProfile, newMaterial, newName or newClass.";
			}
			if (string.IsNullOrWhiteSpace(guids) && type == "any" && name + profile + material + partClass + phase == "")
			{
				return "Error: no targets. Give guids/'selected' or at least one filter.";
			}
			List<Part> list = TargetParts(model, guids, type, name, profile, material, partClass, phase);
			TeklaSession.EnsureBulk(list.Count, "parts");
			if (list.Count == 0)
			{
				return "No parts matched.";
			}
			if (!apply)
			{
				return TeklaSession.ToJson(new
				{
					preview = true,
					wouldChange = list.Count,
					change = new { newProfile, newMaterial, newName, newClass },
					sample = (from p in list.Take(10)
						select new
						{
							guid = p.Identifier.GUID.ToString(),
							before = new
							{
								name = p.Name,
								profile = p.Profile.ProfileString,
								material = p.Material.MaterialString,
								cls = p.Class
							},
							after = new
							{
								name = (string.IsNullOrWhiteSpace(newName) ? p.Name : newName),
								profile = (string.IsNullOrWhiteSpace(newProfile) ? p.Profile.ProfileString : newProfile),
								material = (string.IsNullOrWhiteSpace(newMaterial) ? p.Material.MaterialString : newMaterial),
								cls = (string.IsNullOrWhiteSpace(newClass) ? p.Class : newClass)
							}
						}).ToList(),
					note = "Nothing was changed. Ask the user to confirm, then call again with apply=true. Ctrl+Z in Tekla undoes it."
				});
			}
			int num = 0;
			int num2 = 0;
			foreach (Part item in list)
			{
				if (!string.IsNullOrWhiteSpace(newProfile))
				{
					item.Profile.ProfileString = newProfile;
				}
				if (!string.IsNullOrWhiteSpace(newMaterial))
				{
					item.Material.MaterialString = newMaterial;
				}
				if (!string.IsNullOrWhiteSpace(newName))
				{
					item.Name = newName;
				}
				if (!string.IsNullOrWhiteSpace(newClass))
				{
					item.Class = newClass;
				}
				if (item.Modify())
				{
					TeklaTools.Tag(item, "TEKLAAI_CHANGED");
					num++;
				}
				else
				{
					num2++;
				}
			}
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				applied = true,
				changed = num,
				failed = num2,
				of = list.Count,
				note = "Parts tagged TEKLAAI_CHANGED. Ctrl+Z in Tekla undoes it."
			});
		}, "BulkModify"));
	}

	[McpServerTool(Name = "tekla_find_ai_parts", ReadOnly = true)]
	[Description("List parts that the AI created (UDA TEKLAAI_CREATED) or changed (TEKLAAI_CHANGED), with the timestamp. Use this to review or undo AI work: pass the GUIDs to tekla_select or tekla_delete.")]
	public static string FindAiParts([Description("created | changed | all")] string which = "all", [Description("Max rows")] int limit = 200)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<object> list = new List<object>();
			int num = 0;
			foreach (Part item in TeklaTools.EnumerateParts(model, "any"))
			{
				string value = null;
				string value2 = null;
				item.GetUserProperty("TEKLAAI_CREATED", ref value);
				item.GetUserProperty("TEKLAAI_CHANGED", ref value2);
				bool flag = !string.IsNullOrEmpty(value);
				bool flag2 = !string.IsNullOrEmpty(value2);
				if ((which == "created") ? flag : ((which == "changed") ? flag2 : (flag || flag2)))
				{
					num++;
					if (list.Count < limit)
					{
						list.Add(new
						{
							guid = item.Identifier.GUID.ToString(),
							name = item.Name,
							profile = item.Profile.ProfileString,
							created = value,
							changed = value2
						});
					}
				}
			}
			return TeklaSession.ToJson(new
			{
				total = num,
				shown = list.Count,
				parts = list
			});
		}, "FindAiParts"));
	}

	[McpServerTool(Name = "tekla_clash_check", ReadOnly = true)]
	[Description("APPROXIMATE clash check using part bounding boxes (not exact solids), so it can report false positives for diagonal members. Use it to find candidates, then check them in Tekla's own Clash Check Manager. Scope: GUIDs/'selected', or filters. Max 500 parts.")]
	public static string ClashCheck([Description("Comma-separated GUIDs or 'selected' (empty = use filters)")] string guids = "", [Description("Filter: beam | column | plate | polybeam | any")] string type = "any", [Description("Filter: phase number")] string phase = "", [Description("Filter: name contains")] string name = "", [Description("Overlap needed before it counts as a clash, in mm")] double tolerance = 5.0, [Description("Skip pairs in the same assembly (e.g. a beam and its own end plate)")] bool ignoreSameAssembly = true, [Description("Max clashes returned")] int limit = 100)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<Part> list = TargetParts(model, guids, type, name, "", "", "", phase);
			TeklaSession.EnsureBulk(list.Count, "parts");
			List<(Part, Point, Point, string)> list2 = new List<(Part, Point, Point, string)>();
			foreach (Part item2 in list)
			{
				Solid solid = item2.GetSolid();
				if (solid != null)
				{
					string item = "";
					try
					{
						item = item2.GetAssembly()?.Identifier.GUID.ToString() ?? "";
					}
					catch
					{
					}
					list2.Add((item2, solid.MinimumPoint, solid.MaximumPoint, item));
				}
			}
			List<object> list3 = new List<object>();
			int num = 0;
			Stopwatch stopwatch = Stopwatch.StartNew();
			for (int i = 0; i < list2.Count; i++)
			{
				if (!(stopwatch.Elapsed.TotalSeconds < 45.0))
				{
					break;
				}
				for (int j = i + 1; j < list2.Count; j++)
				{
					(Part, Point, Point, string) tuple = list2[i];
					(Part, Point, Point, string) tuple2 = list2[j];
					if (!ignoreSameAssembly || !(tuple.Item4 != "") || !(tuple.Item4 == tuple2.Item4))
					{
						double num2 = Math.Min(tuple.Item3.X, tuple2.Item3.X) - Math.Max(tuple.Item2.X, tuple2.Item2.X);
						double num3 = Math.Min(tuple.Item3.Y, tuple2.Item3.Y) - Math.Max(tuple.Item2.Y, tuple2.Item2.Y);
						double num4 = Math.Min(tuple.Item3.Z, tuple2.Item3.Z) - Math.Max(tuple.Item2.Z, tuple2.Item2.Z);
						if (num2 > tolerance && num3 > tolerance && num4 > tolerance)
						{
							num++;
							if (list3.Count < limit)
							{
								list3.Add(new
								{
									a = new
									{
										guid = tuple.Item1.Identifier.GUID.ToString(),
										name = tuple.Item1.Name,
										profile = tuple.Item1.Profile.ProfileString
									},
									b = new
									{
										guid = tuple2.Item1.Identifier.GUID.ToString(),
										name = tuple2.Item1.Name,
										profile = tuple2.Item1.Profile.ProfileString
									},
									overlapMm = new
									{
										x = Math.Round(num2, 1),
										y = Math.Round(num3, 1),
										z = Math.Round(num4, 1)
									}
								});
							}
						}
					}
				}
			}
			return TeklaSession.ToJson(new
			{
				method = "bounding-box (approximate)",
				checkedParts = list2.Count,
				clashes = num,
				shown = list3.Count,
				timedOut = (stopwatch.Elapsed.TotalSeconds >= 45.0),
				results = list3
			});
		}, "ClashCheck"));
	}

	[McpServerTool(Name = "tekla_fix_columns")]
	[Description("Find crooked (not plumb) or flipped (start higher than end) columns. PREVIEW by default; with apply=true makes crooked columns vertical (end X,Y := start X,Y, only if the lean is within maxLeanMm) and swaps start/end on flipped ones. Scope: GUIDs/'selected' or all columns (optionally one phase). Tagged TEKLAAI_CHANGED. Check with the user before applying.")]
	public static string FixColumns([Description("Comma-separated GUIDs or 'selected'. Empty = all columns.")] string guids = "", [Description("Phase filter (optional)")] string phase = "", [Description("Ignore leans smaller than this (mm) — already OK")] double toleranceMm = 2.0, [Description("Only straighten columns leaning no more than this (mm); bigger ones are reported, not changed (they may be intentionally raked)")] double maxLeanMm = 100.0, [Description("Fix flipped columns (start above end)")] bool fixFlipped = true, [Description("Must be true to write. Default false = preview only.")] bool apply = false)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<Beam> list = (from b in TargetParts(model, guids, "column", "", "", "", "", phase).OfType<Beam>()
				where b.Type == Beam.BeamTypeEnum.COLUMN
				select b).ToList();
			TeklaSession.EnsureBulk(list.Count, "columns");
			List<object> list2 = new List<object>();
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (Beam item in list)
			{
				Point point = item.StartPoint;
				Point point2 = item.EndPoint;
				bool flag = fixFlipped && point.Z > point2.Z;
				if (flag)
				{
					Point point3 = point;
					point = point2;
					point2 = point3;
				}
				double num5 = Math.Sqrt((point2.X - point.X) * (point2.X - point.X) + (point2.Y - point.Y) * (point2.Y - point.Y));
				bool flag2 = num5 > toleranceMm;
				if (flag || flag2)
				{
					if (flag)
					{
						num2++;
					}
					if (flag2 && num5 <= maxLeanMm)
					{
						num3++;
					}
					string action;
					if (flag2 && num5 > maxLeanMm)
					{
						num4++;
						action = (flag ? "flip only (lean too big to straighten)" : "NOT changed (lean exceeds max, may be raked)");
					}
					else
					{
						action = ((flag ? "flip + " : "") + (flag2 ? "make plumb" : "")).TrimEnd(' ', '+');
					}
					list2.Add(new
					{
						guid = item.Identifier.GUID.ToString(),
						name = item.Name,
						profile = item.Profile.ProfileString,
						leanMm = Math.Round(num5, 1),
						flipped = flag,
						action = action
					});
					if (apply)
					{
						Point point4 = new Point(point);
						Point point5 = new Point(point2);
						if (flag2 && num5 <= maxLeanMm)
						{
							point5.X = point4.X;
							point5.Y = point4.Y;
						}
						if (flag || (flag2 && num5 <= maxLeanMm))
						{
							item.StartPoint = point4;
							item.EndPoint = point5;
							if (item.Modify())
							{
								TeklaTools.Tag(item, "TEKLAAI_CHANGED");
								num++;
							}
						}
					}
				}
			}
			if (apply)
			{
				model.CommitChanges();
			}
			return TeklaSession.ToJson(new
			{
				applied = apply,
				columnsChecked = list.Count,
				crooked = num3,
				flipped = num2,
				leanTooBigNotChanged = num4,
				changed = (apply ? new int?(num) : null),
				items = list2.Take(100).ToList(),
				note = (apply ? "Done. Ctrl+Z in Tekla undoes it." : "Preview only. Nothing changed. Call again with apply=true after the user confirms.")
			});
		}, "FixColumns"));
	}

	[IteratorStateMachine(typeof(_003CTeklaTypes_003Ed__8))]
	private static IEnumerable<Type> TeklaTypes()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CTeklaTypes_003Ed__8(-2);
	}

	private static string Sig(MethodBase m)
	{
		string text = string.Join(", ", from p in m.GetParameters()
			select p.ParameterType.Name + " " + p.Name);
		string text2 = ((m is MethodInfo methodInfo) ? (methodInfo.ReturnType.Name + " ") : "");
		return text2 + m.Name + "(" + text + ")";
	}

	[McpServerTool(Name = "tekla_api_search", ReadOnly = true)]
	[Description("Search the installed Tekla Open API (read from the Tekla DLLs on this PC, so it matches the user's version) for classes and members whose name contains the text. Use it BEFORE writing a custom tool or script, to find the right class/method names. Then call tekla_api_type for the full member list.")]
	public static string ApiSearch([Description("Text to find, e.g. 'Bolt', 'Weld', 'GetSolid', 'Phase'")] string query, [Description("Max results")] int limit = 40)
	{
		return Safe(delegate
		{
			if (string.IsNullOrWhiteSpace(query))
			{
				return "Error: give a search text.";
			}
			try
			{
				TeklaSession.Run((Model m) => true, "ApiSearch");
			}
			catch
			{
			}
			string text = query.Trim();
			List<string> list = new List<string>();
			foreach (Type item in TeklaTypes())
			{
				if (item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					list.Add("TYPE   " + item.FullName);
				}
				MemberInfo[] members = item.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
				foreach (MemberInfo memberInfo in members)
				{
					if (memberInfo.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 && !(memberInfo is MethodBase { IsSpecialName: not false }))
					{
						list.Add($"MEMBER {item.FullName}.{memberInfo.Name}  ({memberInfo.MemberType})");
					}
				}
				if (list.Count >= limit * 3)
				{
					break;
				}
			}
			return (list.Count == 0) ? ("Nothing found for '" + text + "' in the loaded Tekla.Structures assemblies.") : (string.Join("\n", list.Distinct().Take(limit)) + ((list.Count > limit) ? "\n… more results, narrow the search." : ""));
		});
	}

	[McpServerTool(Name = "tekla_api_type", ReadOnly = true)]
	[Description("Show the public constructors, properties, methods and enum values of one Tekla Open API class, e.g. 'Beam', 'Tekla.Structures.Model.BoltArray', 'Position.PlaneEnum'. Signatures come from the installed DLLs.")]
	public static string ApiType([Description("Class name (short or full)")] string typeName)
	{
		return Safe(delegate
		{
			try
			{
				TeklaSession.Run((Model m) => true, "ApiType");
			}
			catch
			{
			}
			string name = (typeName ?? "").Trim();
			List<Type> list = (from t in TeklaTypes()
				where t.FullName.Equals(name, StringComparison.OrdinalIgnoreCase) || t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || t.FullName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase) || t.FullName.EndsWith("+" + name, StringComparison.OrdinalIgnoreCase)
				select t).ToList();
			if (list.Count == 0)
			{
				return "No type '" + name + "'. Try tekla_api_search first.";
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (Type item in list.Take(3))
			{
				stringBuilder.AppendLine("== " + item.FullName + ((item.BaseType != null) ? (" : " + item.BaseType.Name) : ""));
				if (item.IsEnum)
				{
					stringBuilder.AppendLine("  values: " + string.Join(", ", Enum.GetNames(item)));
				}
				else
				{
					ConstructorInfo[] constructors = item.GetConstructors();
					foreach (ConstructorInfo m2 in constructors)
					{
						stringBuilder.AppendLine("  ctor     " + Sig(m2));
					}
					PropertyInfo[] properties = item.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
					foreach (PropertyInfo propertyInfo in properties)
					{
						stringBuilder.AppendLine("  property " + propertyInfo.PropertyType.Name + " " + propertyInfo.Name + " {" + (propertyInfo.CanRead ? " get;" : "") + (propertyInfo.CanWrite ? " set;" : "") + " }");
					}
					foreach (MethodInfo item2 in from x in item.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
						where !x.IsSpecialName
						select x)
					{
						stringBuilder.AppendLine("  method   " + Sig(item2));
					}
					stringBuilder.AppendLine();
				}
			}
			return stringBuilder.ToString();
		});
	}
}
