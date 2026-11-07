using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;
using Tekla.Structures.Model.UI;

namespace TeklaMcp;

[McpServerToolType]
public static class TeklaTools
{
	[CompilerGenerated]
	private sealed class _003CEnumerateParts_003Ed__15 : IEnumerable<Part>, IEnumerable, IEnumerator<Part>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private Part _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private string type;

		public string _003C_003E3__type;

		private Model model;

		public Model _003C_003E3__model;

		private string _003Ct_003E5__2;

		private List<ModelObject.ModelObjectEnum>.Enumerator _003C_003E7__wrap2;

		private ModelObjectEnumerator _003Ce_003E5__4;

		Part IEnumerator<Part>.Current
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
		public _003CEnumerateParts_003Ed__15(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if (num == -3 || num == 1)
			{
				try
				{
				}
				finally
				{
					_003C_003Em__Finally1();
				}
			}
			_003Ct_003E5__2 = null;
			_003C_003E7__wrap2 = default(List<ModelObject.ModelObjectEnum>.Enumerator);
			_003Ce_003E5__4 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				int num = _003C_003E1__state;
				if (num != 0)
				{
					if (num != 1)
					{
						return false;
					}
					_003C_003E1__state = -3;
					goto IL_0186;
				}
				_003C_003E1__state = -1;
				ModelObjectEnumerator.AutoFetch = true;
				List<ModelObject.ModelObjectEnum> list = new List<ModelObject.ModelObjectEnum>();
				switch ((type ?? "any").ToLowerInvariant())
				{
				case "beam":
				case "column":
					list.Add(ModelObject.ModelObjectEnum.BEAM);
					break;
				case "plate":
					list.Add(ModelObject.ModelObjectEnum.CONTOURPLATE);
					break;
				case "polybeam":
					list.Add(ModelObject.ModelObjectEnum.POLYBEAM);
					break;
				default:
					list.Add(ModelObject.ModelObjectEnum.BEAM);
					list.Add(ModelObject.ModelObjectEnum.POLYBEAM);
					list.Add(ModelObject.ModelObjectEnum.CONTOURPLATE);
					break;
				}
				_003Ct_003E5__2 = (type ?? "").ToLowerInvariant();
				_003C_003E7__wrap2 = list.GetEnumerator();
				_003C_003E1__state = -3;
				goto IL_019d;
				IL_019d:
				if (_003C_003E7__wrap2.MoveNext())
				{
					ModelObject.ModelObjectEnum current = _003C_003E7__wrap2.Current;
					_003Ce_003E5__4 = model.GetModelObjectSelector().GetAllObjectsWithType(current);
					goto IL_0186;
				}
				_003C_003Em__Finally1();
				_003C_003E7__wrap2 = default(List<ModelObject.ModelObjectEnum>.Enumerator);
				return false;
				IL_0186:
				while (_003Ce_003E5__4.MoveNext())
				{
					if (_003Ce_003E5__4.Current is Part part && (!(_003Ct_003E5__2 == "beam") || !(part is Beam { Type: Beam.BeamTypeEnum.COLUMN })) && (!(_003Ct_003E5__2 == "column") || part is Beam { Type: Beam.BeamTypeEnum.COLUMN }))
					{
						_003C_003E2__current = part;
						_003C_003E1__state = 1;
						return true;
					}
				}
				_003Ce_003E5__4 = null;
				goto IL_019d;
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			((IDisposable)_003C_003E7__wrap2).Dispose();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<Part> IEnumerable<Part>.GetEnumerator()
		{
			_003CEnumerateParts_003Ed__15 _003CEnumerateParts_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CEnumerateParts_003Ed__ = this;
			}
			else
			{
				_003CEnumerateParts_003Ed__ = new _003CEnumerateParts_003Ed__15(0);
			}
			_003CEnumerateParts_003Ed__.model = _003C_003E3__model;
			_003CEnumerateParts_003Ed__.type = _003C_003E3__type;
			return _003CEnumerateParts_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<Part>)this).GetEnumerator();
		}
	}

	[CompilerGenerated]
	private sealed class _003CSelectedParts_003Ed__16 : IEnumerable<Part>, IEnumerable, IEnumerator<Part>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private Part _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private ModelObjectEnumerator _003Ce_003E5__2;

		Part IEnumerator<Part>.Current
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
		public _003CSelectedParts_003Ed__16(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003Ce_003E5__2 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003Ce_003E5__2 = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
				break;
			case 1:
				_003C_003E1__state = -1;
				break;
			}
			while (_003Ce_003E5__2.MoveNext())
			{
				if (_003Ce_003E5__2.Current is Part part)
				{
					_003C_003E2__current = part;
					_003C_003E1__state = 1;
					return true;
				}
			}
			return false;
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
		IEnumerator<Part> IEnumerable<Part>.GetEnumerator()
		{
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				return this;
			}
			return new _003CSelectedParts_003Ed__16(0);
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<Part>)this).GetEnumerator();
		}
	}

	private static string Safe(Func<string> body)
	{
		try
		{
			return body();
		}
		catch (Exception ex)
		{
			return TeklaSession.Fail(ex);
		}
	}

	[McpServerTool(Name = "tekla_model_info", ReadOnly = true)]
	[Description("Check the connection to Tekla Structures and return the open model's name, path and current phase.")]
	public static string ModelInfo()
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			ModelInfo info = model.GetInfo();
			PhaseCollection phases = model.GetPhases();
			return TeklaSession.ToJson(new
			{
				connected = true,
				modelName = info.ModelName,
				modelPath = info.ModelPath,
				currentPhase = info.CurrentPhase,
				phaseCount = (phases?.Count ?? 0),
				units = "All lengths/coordinates are in millimetres (mm)."
			});
		}, "ModelInfo"));
	}

	[McpServerTool(Name = "tekla_get_selected", ReadOnly = true)]
	[Description("Return the objects the user currently has selected in the Tekla model view.")]
	public static string GetSelected([Description("Include extra report properties (positions, area, volume, phase).")] bool detailed = false, [Description("Max objects to return.")] int limit = 100)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			ModelObjectEnumerator selectedObjects = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
			List<object> list = new List<object>();
			int num = 0;
			while (selectedObjects.MoveNext())
			{
				num++;
				if (list.Count < limit && selectedObjects.Current != null)
				{
					list.Add(TeklaSession.Describe(selectedObjects.Current, detailed));
				}
			}
			return TeklaSession.ToJson(new
			{
				total = num,
				returned = list.Count,
				objects = list
			});
		}, "GetSelected"));
	}

	[McpServerTool(Name = "tekla_find_parts", ReadOnly = true)]
	[Description("Search parts (beams, columns, plates, polybeams) in the model. All text filters are case-insensitive 'contains' matches; leave empty to skip.")]
	public static string FindParts([Description("Part type: any | beam | column | plate | polybeam")] string type = "any", [Description("Filter on NAME, e.g. 'COLUMN'")] string name = "", [Description("Filter on profile, e.g. 'HEA300' or 'PL'")] string profile = "", [Description("Filter on material, e.g. 'S355'")] string material = "", [Description("Filter on class (exact), e.g. '3'")] string partClass = "", [Description("Filter on phase number (exact), e.g. '2'")] string phase = "", [Description("Max parts to return.")] int limit = 200)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			IEnumerable<Part> enumerable = from p in EnumerateParts(model, type)
				where Contains(p.Name, name) && Contains(p.Profile.ProfileString, profile) && Contains(p.Material.MaterialString, material) && (string.IsNullOrEmpty(partClass) || p.Class == partClass) && (string.IsNullOrWhiteSpace(phase) || TeklaSession.ReportString(p, "PHASE") == phase.Trim())
				select p;
			List<object> list = new List<object>();
			int num = 0;
			bool flag = false;
			Stopwatch stopwatch = Stopwatch.StartNew();
			foreach (Part item in enumerable)
			{
				if (stopwatch.Elapsed.TotalSeconds > 45.0)
				{
					flag = true;
					break;
				}
				num++;
				if (list.Count < limit)
				{
					list.Add(TeklaSession.Describe(item));
				}
			}
			return TeklaSession.ToJson(new
			{
				total = num,
				returned = list.Count,
				partial = flag,
				note = (flag ? "Search stopped after 45 s on a large model; add filters (profile/name/material) to narrow it." : null),
				parts = list
			});
		}, "FindParts"));
	}

	[McpServerTool(Name = "tekla_get_object", ReadOnly = true)]
	[Description("Get full details of one model object by its GUID, including user-defined attributes (UDAs).")]
	public static string GetObject([Description("Object GUID")] string guid)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			ModelObject modelObject = TeklaSession.FindByGuid(model, guid);
			Dictionary<string, object> dictionary = TeklaSession.Describe(modelObject, detailed: true);
			Hashtable values = new Hashtable();
			modelObject.GetAllUserProperties(ref values);
			dictionary["udas"] = values.Cast<DictionaryEntry>().ToDictionary((DictionaryEntry x) => x.Key.ToString(), (DictionaryEntry x) => x.Value?.ToString());
			return TeklaSession.ToJson(dictionary);
		}, "GetObject"));
	}

	[McpServerTool(Name = "tekla_get_report_property", ReadOnly = true)]
	[Description("Read any Tekla report/template property of an object, e.g. WEIGHT, WEIGHT_NET, LENGTH, AREA, PROFILE, ASSEMBLY_POS, PART_POS, PHASE, USERDEFINED.XYZ.")]
	public static string GetReportProperty([Description("Object GUID")] string guid, [Description("Report property name, e.g. WEIGHT")] string property)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			ModelObject modelObject = TeklaSession.FindByGuid(model, guid);
			double value = 0.0;
			string value2 = "";
			int value3 = 0;
			if (modelObject.GetReportProperty(property, ref value))
			{
				return TeklaSession.ToJson(new { property, value });
			}
			if (modelObject.GetReportProperty(property, ref value3))
			{
				return TeklaSession.ToJson(new
				{
					property = property,
					value = value3
				});
			}
			return modelObject.GetReportProperty(property, ref value2) ? TeklaSession.ToJson(new
			{
				property = property,
				value = value2
			}) : ("Property '" + property + "' not available for this object.");
		}, "GetReportProperty"));
	}

	[McpServerTool(Name = "tekla_weight_summary", ReadOnly = true)]
	[Description("Total count, length and weight of parts grouped by profile, material, name or class. Good for quick material take-offs.")]
	public static string WeightSummary([Description("Group by: profile | material | name | class")] string groupBy = "profile", [Description("Part type: any | beam | column | plate | polybeam")] string type = "any", [Description("Only use the current selection instead of the whole model.")] bool selectedOnly = false)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			IEnumerable<Part> source = (selectedOnly ? SelectedParts() : EnumerateParts(model, type));
			Func<Part, string> key;
			switch ((groupBy ?? "profile").ToLowerInvariant())
			{
			case "material":
				key = (Part p) => p.Material.MaterialString;
				break;
			case "name":
				key = (Part p) => p.Name;
				break;
			case "class":
				key = (Part p) => p.Class;
				break;
			default:
				key = (Part p) => p.Profile.ProfileString;
				break;
			}
			var list = (from p in source
				select new
				{
					k = (key(p) ?? ""),
					w = TeklaSession.ReportDouble(p, "WEIGHT"),
					l = TeklaSession.ReportDouble(p, "LENGTH")
				} into x
				group x by x.k into g
				select new
				{
					@group = g.Key,
					count = g.Count(),
					totalLength_m = Math.Round(g.Sum(x => x.l) / 1000.0, 2),
					totalWeight_kg = Math.Round(g.Sum(x => x.w), 1)
				} into r
				orderby r.totalWeight_kg descending
				select r).ToList();
			return TeklaSession.ToJson(new
			{
				groupBy = groupBy,
				grandTotalWeight_kg = Math.Round(list.Sum(r => r.totalWeight_kg), 1),
				grandTotalCount = list.Sum(r => r.count),
				rows = list
			});
		}, "WeightSummary"));
	}

	[McpServerTool(Name = "tekla_create_beam")]
	[Description("Create a steel/concrete beam between two points (mm). Changes are committed to the model; undo with Ctrl+Z in Tekla.")]
	public static string CreateBeam(double startX, double startY, double startZ, double endX, double endY, double endZ, [Description("Profile, e.g. HEA300, IPE240, UB305*165*40, 300*600")] string profile, [Description("Material, e.g. S355JR, C30/37")] string material = "S355JR", string name = "BEAM", [Description("Class (colour), e.g. 3")] string partClass = "3", [Description("Position plane: LEFT | MIDDLE | RIGHT")] string plane = "MIDDLE", [Description("Position rotation: FRONT | TOP | BACK | BELOW")] string rotation = "TOP", [Description("Position depth: FRONT | MIDDLE | BEHIND")] string depth = "BEHIND")
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			Beam beam = new Beam(new Point(startX, startY, startZ), new Point(endX, endY, endZ));
			ApplyCommon(beam, profile, material, name, partClass);
			ApplyPosition(beam, plane, rotation, depth);
			if (!beam.Insert())
			{
				return "Error: Tekla refused to insert the beam (check profile/material names exist in your catalogs).";
			}
			Tag(beam, "TEKLAAI_CREATED");
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				created = TeklaSession.Describe(beam)
			});
		}, "CreateBeam"));
	}

	[McpServerTool(Name = "tekla_create_column")]
	[Description("Create a vertical column at (x, y) from baseZ to topZ (mm). Committed to the model; undo with Ctrl+Z.")]
	public static string CreateColumn(double x, double y, double baseZ, double topZ, [Description("Profile, e.g. HEB300")] string profile, string material = "S355JR", string name = "COLUMN", string partClass = "2", [Description("Rotation: FRONT | TOP | BACK | BELOW")] string rotation = "FRONT")
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			Beam beam = new Beam(Beam.BeamTypeEnum.COLUMN)
			{
				StartPoint = new Point(x, y, baseZ),
				EndPoint = new Point(x, y, topZ)
			};
			ApplyCommon(beam, profile, material, name, partClass);
			ApplyPosition(beam, "MIDDLE", rotation, "MIDDLE");
			if (!beam.Insert())
			{
				return "Error: Tekla refused to insert the column.";
			}
			Tag(beam, "TEKLAAI_CREATED");
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				created = TeklaSession.Describe(beam)
			});
		}, "CreateColumn"));
	}

	[McpServerTool(Name = "tekla_create_plate")]
	[Description("Create a contour plate from 3+ corner points (mm). Points format: 'x,y,z; x,y,z; x,y,z; ...'.")]
	public static string CreatePlate([Description("Corner points, e.g. '0,0,0; 1000,0,0; 1000,500,0; 0,500,0'")] string points, [Description("Plate profile, e.g. PL20 (20 mm thick)")] string profile = "PL10", string material = "S355JR", string name = "PLATE", string partClass = "4")
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<Point> list = ParsePoints(points);
			if (list.Count < 3)
			{
				return "Error: a plate needs at least 3 points.";
			}
			ContourPlate contourPlate = new ContourPlate();
			foreach (Point item in list)
			{
				contourPlate.AddContourPoint(new ContourPoint(item, null));
			}
			ApplyCommon(contourPlate, profile, material, name, partClass);
			if (!contourPlate.Insert())
			{
				return "Error: Tekla refused to insert the plate.";
			}
			Tag(contourPlate, "TEKLAAI_CREATED");
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				created = TeklaSession.Describe(contourPlate)
			});
		}, "CreatePlate"));
	}

	[McpServerTool(Name = "tekla_modify_part")]
	[Description("Change properties of one part by GUID. Leave a field empty to keep it unchanged. Coordinates only apply to beams/columns.")]
	public static string ModifyPart([Description("Part GUID")] string guid, string profile = "", string material = "", string name = "", string partClass = "", string finish = "", [Description("New start point 'x,y,z' (beams only)")] string start = "", [Description("New end point 'x,y,z' (beams only)")] string end = "")
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			if (!(TeklaSession.FindByGuid(model, guid) is Part part))
			{
				return "Error: object is not a part.";
			}
			if (!string.IsNullOrWhiteSpace(profile))
			{
				part.Profile.ProfileString = profile;
			}
			if (!string.IsNullOrWhiteSpace(material))
			{
				part.Material.MaterialString = material;
			}
			if (!string.IsNullOrWhiteSpace(name))
			{
				part.Name = name;
			}
			if (!string.IsNullOrWhiteSpace(partClass))
			{
				part.Class = partClass;
			}
			if (!string.IsNullOrWhiteSpace(finish))
			{
				part.Finish = finish;
			}
			if (part is Beam beam)
			{
				if (!string.IsNullOrWhiteSpace(start))
				{
					beam.StartPoint = ParsePoints(start)[0];
				}
				if (!string.IsNullOrWhiteSpace(end))
				{
					beam.EndPoint = ParsePoints(end)[0];
				}
			}
			if (!part.Modify())
			{
				return "Error: Tekla refused the modification.";
			}
			Tag(part, "TEKLAAI_CHANGED");
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				modified = TeklaSession.Describe(part)
			});
		}, "ModifyPart"));
	}

	[McpServerTool(Name = "tekla_set_uda")]
	[Description("Set a user-defined attribute (UDA) on one or more objects. Value type: string | int | double. PREVIEW ONLY unless apply=true: first call without apply to see how many objects would change, show it to the user, then call again with apply=true.")]
	public static string SetUda([Description("Comma-separated GUIDs, or 'selected' for the current selection")] string guids, [Description("UDA name, e.g. comment, USER_FIELD_1")] string attribute, string value, [Description("string | int | double")] string valueType = "string", [Description("Must be true to actually write. Default false = preview only.")] bool apply = false)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<ModelObject> list = ResolveTargets(model, guids);
			TeklaSession.EnsureBulk(list.Count, "objects");
			if (!apply)
			{
				return TeklaSession.ToJson(new
				{
					preview = true,
					attribute = attribute,
					value = value,
					wouldUpdate = list.Count,
					sample = (from o in list.Take(10)
						select TeklaSession.Describe(o)).ToList(),
					note = "Nothing was changed. Call again with apply=true to write the UDA."
				});
			}
			int num = 0;
			foreach (ModelObject item in list)
			{
				string text = valueType.ToLowerInvariant();
				if ((text == "int") ? item.SetUserProperty(attribute, int.Parse(value, CultureInfo.InvariantCulture)) : ((!(text == "double")) ? item.SetUserProperty(attribute, value) : item.SetUserProperty(attribute, double.Parse(value, CultureInfo.InvariantCulture))))
				{
					num++;
				}
			}
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				attribute = attribute,
				value = value,
				updated = num,
				of = list.Count
			});
		}, "SetUda"));
	}

	[McpServerTool(Name = "tekla_select")]
	[Description("Highlight/select objects in the Tekla model view so the user can see them.")]
	public static string Select([Description("Comma-separated GUIDs")] string guids)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<ModelObject> list = ResolveTargets(model, guids);
			new Tekla.Structures.Model.UI.ModelObjectSelector().Select(new ArrayList(list));
			return $"Selected {list.Count} object(s) in Tekla.";
		}, "Select"));
	}

	[McpServerTool(Name = "tekla_delete", Destructive = true)]
	[Description("Delete objects from the model. Requires confirm=true. Always show the user what will be deleted and get their OK first.")]
	public static string Delete([Description("Comma-separated GUIDs, or 'selected'")] string guids, [Description("Must be true to actually delete")] bool confirm = false)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<ModelObject> list = ResolveTargets(model, guids);
			TeklaSession.EnsureBulk(list.Count, "objects (delete)");
			if (!confirm)
			{
				return TeklaSession.ToJson(new
				{
					preview = true,
					wouldDelete = list.Select((ModelObject o) => TeklaSession.Describe(o)).ToList(),
					note = "Call again with confirm=true to delete."
				});
			}
			int num = list.Count((ModelObject o) => o.Delete());
			model.CommitChanges();
			return $"Deleted {num} of {list.Count} object(s). Use Ctrl+Z in Tekla to undo.";
		}, "Delete"));
	}

	[McpServerTool(Name = "tekla_run_macro")]
	[Description("Run a Tekla macro (.cs file name in the macros folder, e.g. 'MyMacro.cs').")]
	public static string RunMacro([Description("Macro file name")] string macroName)
	{
		return Safe(() => TeklaSession.Run((Model model) => (!Operation.RunMacro(macroName)) ? ("Error: could not run macro '" + macroName + "'.") : ("Macro '" + macroName + "' started."), "RunMacro"));
	}

	[IteratorStateMachine(typeof(_003CEnumerateParts_003Ed__15))]
	internal static IEnumerable<Part> EnumerateParts(Model model, string type)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CEnumerateParts_003Ed__15(-2)
		{
			_003C_003E3__model = model,
			_003C_003E3__type = type
		};
	}

	[IteratorStateMachine(typeof(_003CSelectedParts_003Ed__16))]
	private static IEnumerable<Part> SelectedParts()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CSelectedParts_003Ed__16(-2);
	}

	internal static List<ModelObject> ResolveTargets(Model model, string guids)
	{
		if (string.Equals(guids?.Trim(), "selected", StringComparison.OrdinalIgnoreCase))
		{
			List<ModelObject> list = new List<ModelObject>();
			ModelObjectEnumerator selectedObjects = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
			while (selectedObjects.MoveNext())
			{
				if (selectedObjects.Current != null)
				{
					list.Add(selectedObjects.Current);
				}
			}
			return list;
		}
		return (from g in (guids ?? "").Split(new char[4] { ',', ';', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			select TeklaSession.FindByGuid(model, g.Trim())).ToList();
	}

	internal static void Tag(ModelObject o, string attribute)
	{
		try
		{
			o.SetUserProperty(attribute, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
		}
		catch
		{
		}
	}

	private static void ApplyCommon(Part part, string profile, string material, string name, string partClass)
	{
		part.Profile.ProfileString = profile;
		part.Material.MaterialString = material;
		part.Name = name;
		part.Class = partClass;
	}

	private static void ApplyPosition(Part part, string plane, string rotation, string depth)
	{
		if (!Enum.TryParse<Position.PlaneEnum>(plane, ignoreCase: true, out var result))
		{
			throw new ArgumentException("Invalid plane '" + plane + "'. Use LEFT, MIDDLE or RIGHT.");
		}
		if (!Enum.TryParse<Position.RotationEnum>(rotation, ignoreCase: true, out var result2))
		{
			throw new ArgumentException("Invalid rotation '" + rotation + "'. Use FRONT, TOP, BACK or BELOW.");
		}
		if (!Enum.TryParse<Position.DepthEnum>(depth, ignoreCase: true, out var result3))
		{
			throw new ArgumentException("Invalid depth '" + depth + "'. Use FRONT, MIDDLE or BEHIND.");
		}
		part.Position.Plane = result;
		part.Position.Rotation = result2;
		part.Position.Depth = result3;
	}

	private static List<Point> ParsePoints(string text)
	{
		return (text ?? "").Split(new char[2] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(delegate(string chunk)
		{
			double[] array = (from s in chunk.Split(new char[1] { ',' })
				select double.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
			if (array.Length != 3)
			{
				throw new ArgumentException("Point '" + chunk.Trim() + "' must be x,y,z.");
			}
			return new Point(array[0], array[1], array[2]);
		}).ToList();
	}

	internal static bool Contains(string value, string filter)
	{
		if (!string.IsNullOrEmpty(filter))
		{
			return (value ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}
}
