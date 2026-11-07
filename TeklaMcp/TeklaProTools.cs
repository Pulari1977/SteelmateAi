using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;
using Tekla.Structures.Model.UI;

namespace TeklaMcp;

[McpServerToolType]
public static class TeklaProTools
{
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

	private static ModelObjectEnumerator All(Model model, ModelObject.ModelObjectEnum kind)
	{
		ModelObjectEnumerator.AutoFetch = true;
		return model.GetModelObjectSelector().GetAllObjectsWithType(kind);
	}

	private static Point ParsePoint(string text)
	{
		double[] array = (from s in (text ?? "").Split(new char[1] { ',' })
			select double.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
		if (array.Length != 3)
		{
			throw new ArgumentException("Point '" + text + "' must be x,y,z in mm.");
		}
		return new Point(array[0], array[1], array[2]);
	}

	[McpServerTool(Name = "tekla_workflow_guide", ReadOnly = true)]
	[Description("Read this first. Explains how Tekla Structures works (model -> assemblies -> numbering -> drawings/NC/reports) and which tool to use for each step.")]
	public static string WorkflowGuide()
	{
		return "HOW TEKLA STRUCTURES WORKS\n- Tekla is a 3D BIM model. Everything is an object: Parts (beams, columns, plates, polybeams), Bolts, Welds,\n  Components/Connections (parametric joints that auto-create plates, bolts, welds), Assemblies (parts welded/bolted\n  together = one shop-fabricated unit), Grids, Phases, Reinforcement.\n- Units: millimetres. Coordinates are in the CURRENT WORK PLANE (usually global).\n- Every object has a GUID. Use GUIDs to refer to objects between tool calls.\n- Changes are written with CommitChanges (the tools do this). Ctrl+Z in Tekla undoes the last commit.\n\nTYPICAL PRODUCTION WORKFLOW -> TOOL\n1. Check connection ................ tekla_model_info, tekla_project_info\n2. Set up grids/phases ............. tekla_list_grids, tekla_list_phases, tekla_set_phase\n3. Model parts .................... tekla_create_beam / column / plate, tekla_modify_part, tekla_move_objects, tekla_copy_objects, tekla_split_beam\n4. Connect parts ................... tekla_insert_connection (named Tekla component, e.g. 'Moment connection'), tekla_create_bolts, tekla_create_weld\n5. Check the model ................ tekla_find_parts, tekla_weight_summary, tekla_list_assemblies, tekla_bolt_summary\n6. NUMBER THE MODEL ................ do this in Tekla (Drawings & reports > Number > Modified objects). Then verify with tekla_check_numbering.\n   NC files, reports and drawings are only correct when numbering is up to date.\n7. Output .......................... tekla_export_nc (DSTV for CNC machines), tekla_export_ifc (BIM coordination), tekla_create_report (BOM/assembly lists)\n8. Drawings ..................... tekla_create_assembly_drawings (needs numbering), tekla_list_drawings, tekla_open_drawing, tekla_update_drawing, tekla_close_drawing, tekla_delete_drawings.\n   tekla_auto_dimension adds first-pass dimensions to the OPEN drawing (open it with tekla_open_drawing first).\n   Drawing LOOK is controlled by the drawing properties file (e.g. 'standard'), set up in the user's Tekla environment.\n\nRULES OF THUMB\n- Always call tekla_find_parts or tekla_get_selected first to get GUIDs; never invent GUIDs.\n- For bulk changes use tekla_bulk_modify (or tekla_set_uda / tekla_fix_columns): they PREVIEW first. Show the preview to the user, then repeat with apply=true. Delete needs confirm=true.\n- Everything the AI creates is tagged TEKLAAI_CREATED and everything it bulk-changes TEKLAAI_CHANGED; list them with tekla_find_ai_parts to review or remove.\n- tekla_clash_check is a quick bounding-box check, not Tekla's Clash Check Manager. Say so when reporting.\n- Unsure of an Open API class or method? Use tekla_api_search / tekla_api_type before writing a custom tool.\n- For a general quality check use tekla_model_health_check first, then tekla_connection_check (loose/unwelded/floating parts) and tekla_drawing_qa (drawings).\n- Revisions: tekla_snapshot_save at each issue; later tekla_snapshot_compare shows added/removed/modified parts and the drawings affected.\n- tekla_save_report saves a report file; tekla_add_agent creates a saved helper (agent) the user can pick in the Agent drop-down.\n- Concrete: tekla_create_rebar_group puts rebar in a concrete part; tekla_rebar_summary totals it. Rebar API varies by Tekla version, so tell the user to check the result in Tekla.\n- Narrow with tekla_find_parts filters (type, profile, material, class, phase) on big models.\n- Profile/material names must exist in the user's catalogs (e.g. HEA300 vs HE300A depends on the environment).";
	}

	[McpServerTool(Name = "tekla_project_info", ReadOnly = true)]
	[Description("Project properties of the open model (name, number, builder, designer, description).")]
	public static string ProjectInfo()
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			ProjectInfo projectInfo = model.GetProjectInfo();
			return TeklaSession.ToJson(new { projectInfo.Name, projectInfo.ProjectNumber, projectInfo.Builder, projectInfo.Designer, projectInfo.Description });
		}, "ProjectInfo"));
	}

	[McpServerTool(Name = "tekla_check_numbering", ReadOnly = true)]
	[Description("Check whether model numbering is up to date. NC files, reports and drawings need up-to-date numbering. Numbering itself must be run inside Tekla (Drawings & reports > Number > Modified objects).")]
	public static string CheckNumbering()
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			bool numberingAllowed = Operation.IsNumberingAllowed();
			bool flag = Operation.IsNumberingUpToDateAll();
			return TeklaSession.ToJson(new
			{
				numberingAllowed = numberingAllowed,
				numberingUpToDate = flag,
				advice = (flag ? "OK to export NC files / reports." : "Numbering is out of date. In Tekla run Drawings & reports > Number > Modified objects, then check again.")
			});
		}, "CheckNumbering"));
	}

	[McpServerTool(Name = "tekla_export_nc")]
	[Description("Create NC files (e.g. DSTV) for CNC machines from all parts or the current selection. Requires up-to-date numbering.")]
	public static string ExportNc([Description("Name of an NC settings template, e.g. 'DSTV for profiles' (Tekla > Files > Export > NC)")] string template, [Description("Output folder. Empty = the folder set in the template.")] string folder = "", [Description("true = only the parts currently selected in Tekla")] bool selectedOnly = false)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			if (!Operation.IsNumberingUpToDateAll())
			{
				return "Error: numbering is not up to date. Number the model in Tekla first, then retry.";
			}
			return (!(selectedOnly ? Operation.CreateNCFilesFromSelected(template, folder ?? "") : Operation.CreateNCFilesFromAll(template, folder ?? ""))) ? "Error: NC creation failed. Check that the template name exists and the output folder is valid." : ("NC files created with template '" + template + "'" + (string.IsNullOrEmpty(folder) ? " in the template's default folder." : (" in " + folder + ".")));
		}, "ExportNc"));
	}

	[McpServerTool(Name = "tekla_export_ifc")]
	[Description("Export the model (or current selection) to an IFC2x3 file for BIM coordination with other disciplines.")]
	public static string ExportIfc([Description("Full output path including .ifc, e.g. C:\\Exports\\model.ifc")] string filePath, [Description("true = only the selected objects")] bool selectedOnly = false)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			string directoryName = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			Operation.IFC2x3ExportFlags iFC2x3ExportFlags = default(Operation.IFC2x3ExportFlags);
			iFC2x3ExportFlags.ExportLocationsFromOrganizer = true;
			iFC2x3ExportFlags.IsPoursEnabled = true;
			iFC2x3ExportFlags.ExportAllObjects = !selectedOnly;
			Operation.IFC2x3ExportFlags flags = iFC2x3ExportFlags;
			return (!Operation.CreateIFC2x3Export(filePath, Operation.IFC2x3ExportViewTypeEnum.COORDINATION_VIEW_20, new List<string>(), "", Operation.ExportBasePoint.GLOBAL, layersNamesAsPartNames: false, useViewColors: false, flags, "")) ? "Error: IFC export failed. See Tekla's session log for details." : ("IFC exported to " + filePath);
		}, "ExportIfc"));
	}

	[McpServerTool(Name = "tekla_create_report")]
	[Description("Create a report (bill of materials, assembly list, bolt list, etc.) from a Tekla report template. Requires up-to-date numbering for reliable results.")]
	public static string CreateReport([Description("Report template name, e.g. 'Assembly_list', 'Part_list', 'Bolt_list'")] string template, [Description("Output file name (> 3 chars). Without a folder it goes to Tekla's report folder.")] string fileName, string title1 = "", string title2 = "", string title3 = "", [Description("true = only the selected objects")] bool selectedOnly = false)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			if (!(selectedOnly ? Operation.CreateReportFromSelected(template, fileName, title1, title2, title3) : Operation.CreateReportFromAll(template, fileName, title1, title2, title3)))
			{
				return "Error: report creation failed. Check the template name and output folder.";
			}
			if (Path.IsPathRooted(fileName))
			{
				for (int i = 0; i < 10; i++)
				{
					if (File.Exists(fileName))
					{
						break;
					}
					Thread.Sleep(1000);
				}
				if (!File.Exists(fileName))
				{
					return "Report started; " + fileName + " not visible yet.";
				}
				return "Report created: " + fileName;
			}
			return "Report '" + fileName + "' started (saved in Tekla's report output folder).";
		}, "CreateReport"));
	}

	[McpServerTool(Name = "tekla_list_assemblies", ReadOnly = true)]
	[Description("List assemblies (shop-fabricated units) with main part, number of secondary parts, assembly position mark and weight.")]
	public static string ListAssemblies([Description("Filter on assembly name/position (contains, case-insensitive).")] string filter = "", int limit = 200)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<object> list = new List<object>();
			int num = 0;
			ModelObjectEnumerator modelObjectEnumerator = All(model, ModelObject.ModelObjectEnum.ASSEMBLY);
			while (modelObjectEnumerator.MoveNext())
			{
				if (modelObjectEnumerator.Current is Assembly assembly)
				{
					string text = TeklaSession.ReportString(assembly, "ASSEMBLY_POS");
					if (string.IsNullOrEmpty(filter) || (assembly.Name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || (text ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						num++;
						if (list.Count < limit)
						{
							Part part = assembly.GetMainPart() as Part;
							list.Add(new
							{
								guid = assembly.Identifier.GUID.ToString(),
								name = assembly.Name,
								position = text,
								mainPart = part?.Name,
								mainProfile = part?.Profile.ProfileString,
								secondaries = (assembly.GetSecondaries()?.Count ?? 0),
								weight_kg = Math.Round(TeklaSession.ReportDouble(assembly, "WEIGHT"), 1)
							});
						}
					}
				}
			}
			return TeklaSession.ToJson(new
			{
				total = num,
				returned = list.Count,
				assemblies = list
			});
		}, "ListAssemblies"));
	}

	[McpServerTool(Name = "tekla_bolt_summary", ReadOnly = true)]
	[Description("Count bolts in the model grouped by bolt standard and diameter (bolt take-off).")]
	public static string BoltSummary()
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			int num = 0;
			ModelObject.ModelObjectEnum[] array = new ModelObject.ModelObjectEnum[3]
			{
				ModelObject.ModelObjectEnum.BOLT_ARRAY,
				ModelObject.ModelObjectEnum.BOLT_CIRCLE,
				ModelObject.ModelObjectEnum.BOLT_XYLIST
			};
			foreach (ModelObject.ModelObjectEnum kind in array)
			{
				ModelObjectEnumerator modelObjectEnumerator = All(model, kind);
				while (modelObjectEnumerator.MoveNext())
				{
					if (modelObjectEnumerator.Current is BoltGroup boltGroup)
					{
						num++;
						string key = $"{boltGroup.BoltStandard} M{boltGroup.BoltSize}";
						dictionary[key] = (dictionary.TryGetValue(key, out var value) ? value : 0) + (boltGroup.BoltPositions?.Count ?? 0);
					}
				}
			}
			return TeklaSession.ToJson(new
			{
				boltGroups = num,
				totalBolts = dictionary.Values.Sum(),
				bySizeAndStandard = from k in dictionary
					orderby k.Value descending
					select new
					{
						type = k.Key,
						count = k.Value
					}
			});
		}, "BoltSummary"));
	}

	[McpServerTool(Name = "tekla_create_bolts")]
	[Description("Create a rectangular bolt array joining two parts. firstPosition -> secondPosition defines the bolt row direction (mm). Always verify the result in Tekla.")]
	public static string CreateBolts([Description("GUID of the part the bolts belong to (e.g. plate)")] string partToBeBolted, [Description("GUID of the part bolted to (e.g. column flange)")] string partToBoltTo, [Description("First bolt position 'x,y,z'")] string firstPosition, [Description("Second position 'x,y,z' (sets the X direction of the array)")] string secondPosition, [Description("Bolt diameter in mm, e.g. 20")] double size = 20.0, [Description("Bolt standard, e.g. 7990 (EN 14399 / ISO 4014 depends on catalog)")] string standard = "7990", [Description("Number of bolts in a row (X)")] int countX = 2, double spacingX = 70.0, [Description("Number of rows (Y)")] int countY = 2, double spacingY = 70.0, [Description("Cut length in mm")] double cutLength = 100.0)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			BoltArray boltArray = new BoltArray
			{
				PartToBeBolted = (TeklaSession.FindByGuid(model, partToBeBolted) as Part),
				PartToBoltTo = (TeklaSession.FindByGuid(model, partToBoltTo) as Part),
				FirstPosition = ParsePoint(firstPosition),
				SecondPosition = ParsePoint(secondPosition),
				BoltSize = size,
				BoltStandard = standard,
				Tolerance = 3.0,
				BoltType = BoltGroup.BoltTypeEnum.BOLT_TYPE_WORKSHOP,
				CutLength = cutLength,
				Bolt = true
			};
			if (boltArray.PartToBeBolted == null || boltArray.PartToBoltTo == null)
			{
				return "Error: both GUIDs must be parts.";
			}
			boltArray.Position.Depth = Position.DepthEnum.MIDDLE;
			for (int i = 1; i < countX; i++)
			{
				boltArray.AddBoltDistX(spacingX);
			}
			for (int j = 1; j < countY; j++)
			{
				boltArray.AddBoltDistY(spacingY);
			}
			if (!boltArray.Insert())
			{
				return "Error: Tekla refused to insert the bolts (check standard/size exist in your bolt catalog).";
			}
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				created = TeklaSession.Describe(boltArray)
			});
		}, "CreateBolts"));
	}

	[McpServerTool(Name = "tekla_create_weld")]
	[Description("Create a weld between two parts (fillet by default).")]
	public static string CreateWeld([Description("GUID of the main part")] string mainPart, [Description("GUID of the secondary part")] string secondaryPart, [Description("Weld size in mm")] double size = 6.0, [Description("Weld on both sides")] bool bothSides = false, [Description("Shop weld (true) or site weld (false)")] bool shopWeld = true)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			Weld weld = new Weld
			{
				MainObject = TeklaSession.FindByGuid(model, mainPart),
				SecondaryObject = TeklaSession.FindByGuid(model, secondaryPart),
				TypeAbove = BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET,
				SizeAbove = size,
				ShopWeld = shopWeld
			};
			if (bothSides)
			{
				weld.TypeBelow = BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET;
				weld.SizeBelow = size;
			}
			if (!weld.Insert())
			{
				return "Error: Tekla refused to insert the weld (parts must touch).";
			}
			model.CommitChanges();
			return string.Format("Weld created ({0} mm fillet{1}).", size, bothSides ? ", both sides" : "");
		}, "CreateWeld"));
	}

	[McpServerTool(Name = "tekla_insert_connection")]
	[Description("Insert a Tekla component/connection (e.g. 'Moment connection', 'Shear Plate Simple', 'Base plate') between a primary part and one or more secondary parts. Component numbers/names come from Tekla's Applications & components catalog.")]
	public static string InsertConnection([Description("Component name as shown in the catalog, e.g. 'Shear Plate Simple'")] string name, [Description("Component number. Tekla standard components use negative numbers or 1; custom components use their catalog number. Use 1 for 'Shear plate' style defaults if unsure.")] int number, [Description("GUID of the primary part (e.g. column)")] string primaryPart, [Description("Comma-separated GUIDs of secondary parts (e.g. beams)")] string secondaryParts, [Description("Saved attribute file to load, e.g. 'standard'")] string attributeFile = "standard", [Description("Optional overrides 'key=value;key=value' (numbers are sent as numbers)")] string attributes = "")
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			Connection connection = new Connection
			{
				Name = name,
				Number = number
			};
			connection.LoadAttributesFromFile(attributeFile);
			connection.SetPrimaryObject(TeklaSession.FindByGuid(model, primaryPart));
			ArrayList arrayList = new ArrayList();
			string[] array = secondaryParts.Split(new char[3] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text in array)
			{
				arrayList.Add(TeklaSession.FindByGuid(model, text.Trim()));
			}
			connection.SetSecondaryObjects(arrayList);
			array = (attributes ?? "").Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(new char[1] { '=' }, 2);
				if (array2.Length == 2)
				{
					if (double.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
					{
						connection.SetAttribute(array2[0].Trim(), result);
					}
					else
					{
						connection.SetAttribute(array2[0].Trim(), array2[1].Trim());
					}
				}
			}
			if (!connection.Insert())
			{
				return "Error: Tekla could not insert the connection (check name/number, and that the parts suit this component).";
			}
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				inserted = TeklaSession.Describe(connection)
			});
		}, "InsertConnection"));
	}

	[McpServerTool(Name = "tekla_move_objects")]
	[Description("Move objects by a translation vector in mm.")]
	public static string MoveObjects([Description("Comma-separated GUIDs")] string guids, double dx, double dy, double dz)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			int num = 0;
			Vector translationVector = new Vector(dx, dy, dz);
			string[] array = guids.Split(new char[3] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			TeklaSession.EnsureBulk(array.Length, "objects (move)");
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (Operation.MoveObject(TeklaSession.FindByGuid(model, text.Trim()), translationVector))
				{
					num++;
				}
			}
			model.CommitChanges();
			return $"Moved {num} of {array.Length} object(s) by ({dx}, {dy}, {dz}) mm.";
		}, "MoveObjects"));
	}

	[McpServerTool(Name = "tekla_copy_objects")]
	[Description("Copy objects by a translation vector in mm, optionally several times (e.g. repeat a frame every 6000 mm).")]
	public static string CopyObjects([Description("Comma-separated GUIDs")] string guids, double dx, double dy, double dz, [Description("How many copies (each offset again by the vector)")] int copies = 1)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			if (copies < 1 || copies > 200)
			{
				return "Error: copies must be 1-200.";
			}
			List<object> list = new List<object>();
			string[] array = guids.Split(new char[3] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			TeklaSession.EnsureBulk(array.Length * copies, "copies");
			string[] array2 = array;
			foreach (string text in array2)
			{
				ModelObject @object = TeklaSession.FindByGuid(model, text.Trim());
				for (int j = 1; j <= copies; j++)
				{
					ModelObject modelObject = Operation.CopyObject(@object, new Vector(dx * (double)j, dy * (double)j, dz * (double)j));
					if (modelObject != null)
					{
						list.Add(new
						{
							guid = modelObject.Identifier.GUID.ToString()
						});
					}
				}
			}
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				copiesCreated = list.Count,
				copies = list
			});
		}, "CopyObjects"));
	}

	[McpServerTool(Name = "tekla_split_beam")]
	[Description("Split a beam/column in two at a point (mm). The point should lie on the beam axis.")]
	public static string SplitBeam(string guid, [Description("Split point 'x,y,z'")] string point)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			if (!(TeklaSession.FindByGuid(model, guid) is Beam @object))
			{
				return "Error: object is not a beam.";
			}
			Beam beam = Operation.Split(@object, ParsePoint(point));
			model.CommitChanges();
			return (beam != null) ? TeklaSession.ToJson(new
			{
				newPart = TeklaSession.Describe(beam)
			}) : "Error: split failed (point not on beam?).";
		}, "SplitBeam"));
	}

	[McpServerTool(Name = "tekla_list_grids", ReadOnly = true)]
	[Description("List grids with their coordinate strings and labels (grids drive most structural layout).")]
	public static string ListGrids()
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<object> list = new List<object>();
			ModelObjectEnumerator modelObjectEnumerator = All(model, ModelObject.ModelObjectEnum.GRID);
			while (modelObjectEnumerator.MoveNext())
			{
				if (modelObjectEnumerator.Current is Grid grid)
				{
					list.Add(new
					{
						guid = grid.Identifier.GUID.ToString(),
						coordinateX = grid.CoordinateX,
						labelX = grid.LabelX,
						coordinateY = grid.CoordinateY,
						labelY = grid.LabelY,
						coordinateZ = grid.CoordinateZ,
						labelZ = grid.LabelZ
					});
				}
			}
			return TeklaSession.ToJson(new
			{
				count = list.Count,
				grids = list
			});
		}, "ListGrids"));
	}

	[McpServerTool(Name = "tekla_list_phases", ReadOnly = true)]
	[Description("List the model's phases (used for construction sequence and filtering).")]
	public static string ListPhases()
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<object> list = new List<object>();
			foreach (Phase phase in model.GetPhases())
			{
				list.Add(new
				{
					number = phase.PhaseNumber,
					name = phase.PhaseName,
					comment = phase.PhaseComment
				});
			}
			return TeklaSession.ToJson(new
			{
				count = list.Count,
				phases = list
			});
		}, "ListPhases"));
	}

	[McpServerTool(Name = "tekla_set_phase")]
	[Description("Assign objects to a phase number.")]
	public static string SetPhase([Description("Comma-separated GUIDs, or 'selected'")] string guids, [Description("Phase number")] int phase)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<ModelObject> list = new List<ModelObject>();
			if (string.Equals(guids?.Trim(), "selected", StringComparison.OrdinalIgnoreCase))
			{
				ModelObjectEnumerator selectedObjects = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
				while (selectedObjects.MoveNext())
				{
					if (selectedObjects.Current != null)
					{
						list.Add(selectedObjects.Current);
					}
				}
			}
			else
			{
				list = (from g in guids.Split(new char[3] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
					select TeklaSession.FindByGuid(model, g.Trim())).ToList();
			}
			TeklaSession.EnsureBulk(list.Count, "objects (phase)");
			int num = list.Count((ModelObject t) => t.SetPhase(new Phase(phase)));
			model.CommitChanges();
			return $"Set phase {phase} on {num} of {list.Count} object(s).";
		}, "SetPhase"));
	}

	[McpServerTool(Name = "tekla_zoom_to_selection")]
	[Description("Zoom the Tekla view to the currently selected objects, so the user can see them.")]
	public static string ZoomToSelection()
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			Point point = null;
			Point point2 = null;
			ModelObjectEnumerator selectedObjects = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
			while (selectedObjects.MoveNext())
			{
				Solid solid = null;
				ModelObject current = selectedObjects.Current;
				if (!(current is Part part))
				{
					if (!(current is BoltGroup boltGroup))
					{
						if (!(current is Reinforcement reinforcement))
						{
							if (current is Assembly assembly)
							{
								solid = (assembly.GetMainPart() as Part)?.GetSolid();
							}
						}
						else
						{
							solid = reinforcement.GetSolid();
						}
					}
					else
					{
						solid = boltGroup.GetSolid();
					}
				}
				else
				{
					solid = part.GetSolid();
				}
				if (solid != null)
				{
					Point minimumPoint = solid.MinimumPoint;
					Point maximumPoint = solid.MaximumPoint;
					point = ((point == null) ? new Point(minimumPoint) : new Point(Math.Min(point.X, minimumPoint.X), Math.Min(point.Y, minimumPoint.Y), Math.Min(point.Z, minimumPoint.Z)));
					point2 = ((point2 == null) ? new Point(maximumPoint) : new Point(Math.Max(point2.X, maximumPoint.X), Math.Max(point2.Y, maximumPoint.Y), Math.Max(point2.Z, maximumPoint.Z)));
				}
			}
			if (point == null)
			{
				return "Nothing to zoom to: select parts, bolts, rebar or assemblies in Tekla first.";
			}
			return (!ViewHandler.ZoomToBoundingBox(new AABB(point, point2))) ? "Error: Tekla could not zoom the view. Make sure a model view is open." : "Zoomed to selection.";
		}, "ZoomToSelection"));
	}
}
