using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using ModelContextProtocol.Server;
using Tekla.Structures.Drawing;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;

namespace TeklaMcp;

[McpServerToolType]
public static class TeklaDrawingTools
{
	[CompilerGenerated]
	private sealed class _003CAllDrawings_003Ed__2 : IEnumerable<Drawing>, IEnumerable, IEnumerator<Drawing>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private Drawing _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private DrawingHandler dh;

		public DrawingHandler _003C_003E3__dh;

		private DrawingEnumerator _003Ce_003E5__2;

		Drawing IEnumerator<Drawing>.Current
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
		public _003CAllDrawings_003Ed__2(int _003C_003E1__state)
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
				_003Ce_003E5__2 = dh.GetDrawings();
				break;
			case 1:
				_003C_003E1__state = -1;
				break;
			}
			while (_003Ce_003E5__2.MoveNext())
			{
				if (_003Ce_003E5__2.Current != null)
				{
					_003C_003E2__current = _003Ce_003E5__2.Current;
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
		IEnumerator<Drawing> IEnumerable<Drawing>.GetEnumerator()
		{
			_003CAllDrawings_003Ed__2 _003CAllDrawings_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CAllDrawings_003Ed__ = this;
			}
			else
			{
				_003CAllDrawings_003Ed__ = new _003CAllDrawings_003Ed__2(0);
			}
			_003CAllDrawings_003Ed__.dh = _003C_003E3__dh;
			return _003CAllDrawings_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<Drawing>)this).GetEnumerator();
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

	private static DrawingHandler Handler()
	{
		DrawingHandler drawingHandler = new DrawingHandler();
		if (!drawingHandler.GetConnectionStatus())
		{
			throw new InvalidOperationException("Drawing API not connected. Tekla must be running with a model open.");
		}
		return drawingHandler;
	}

	[IteratorStateMachine(typeof(_003CAllDrawings_003Ed__2))]
	private static IEnumerable<Drawing> AllDrawings(DrawingHandler dh)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CAllDrawings_003Ed__2(-2)
		{
			_003C_003E3__dh = dh
		};
	}

	private static bool Has(string value, string filter)
	{
		if (!string.IsNullOrEmpty(filter))
		{
			return (value ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	[McpServerTool(Name = "tekla_list_drawings", ReadOnly = true)]
	[Description("List drawings in the model: mark, name, type, titles and whether each is up to date, issued or locked.")]
	public static string ListDrawings([Description("Filter on mark, name or title (contains, case-insensitive).")] string filter = "", int limit = 200)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			DrawingHandler dh = Handler();
			List<object> list = new List<object>();
			int num = 0;
			foreach (Drawing item in AllDrawings(dh))
			{
				if (Has(item.Mark, filter) || Has(item.Name, filter) || Has(item.Title1, filter))
				{
					num++;
					if (list.Count < limit)
					{
						list.Add(new
						{
							mark = item.Mark,
							name = item.Name,
							type = item.DrawingTypeStr,
							title1 = item.Title1,
							upToDate = item.UpToDateStatus.ToString(),
							issued = item.IsIssued,
							locked = item.IsLocked
						});
					}
				}
			}
			return TeklaSession.ToJson(new
			{
				total = num,
				returned = list.Count,
				drawings = list
			});
		}, "ListDrawings"));
	}

	[McpServerTool(Name = "tekla_create_assembly_drawings")]
	[Description("Create assembly drawings for assemblies (or for the assemblies of the given parts) using a drawing properties file. Requires up-to-date numbering and no drawing open in the editor. Views are added by the properties file's view-creation rules.")]
	public static string CreateAssemblyDrawings([Description("Comma-separated GUIDs of assemblies or parts (a part's assembly is used)")] string guids, [Description("Drawing properties file name, e.g. 'standard'")] string properties = "standard")
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			string[] array = guids.Split(new char[3] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length == 0)
			{
				return "Error: give at least one GUID.";
			}
			if (array.Length > 50)
			{
				return "Error: create at most 50 drawings per call.";
			}
			if (!Operation.IsNumberingUpToDateAll())
			{
				return "Error: numbering is not up to date. Number the model in Tekla first (Drawings & reports > Number > Modified objects).";
			}
			if (Handler().IsAnyDrawingOpen())
			{
				return "Error: a drawing is open in Tekla's editor. Close it first (tekla_close_drawing).";
			}
			List<object> list = new List<object>();
			List<string> list2 = new List<string>();
			string[] array2 = array;
			foreach (string text in array2)
			{
				Tekla.Structures.Model.ModelObject modelObject = TeklaSession.FindByGuid(model, text.Trim());
				Assembly assembly = (modelObject as Assembly) ?? (modelObject as Tekla.Structures.Model.Part)?.GetAssembly();
				if (assembly == null)
				{
					list2.Add(text + ": not an assembly or part");
				}
				else
				{
					AssemblyDrawing assemblyDrawing = new AssemblyDrawing(assembly.Identifier, properties);
					if (assemblyDrawing.Insert())
					{
						list.Add(new
						{
							assembly = TeklaSession.ReportString(assembly, "ASSEMBLY_POS"),
							mark = assemblyDrawing.Mark,
							name = assemblyDrawing.Name
						});
					}
					else
					{
						list2.Add(text + ": Tekla refused (properties file '" + properties + "' missing?)");
					}
				}
			}
			return TeklaSession.ToJson(new
			{
				createdCount = list.Count,
				created = list,
				failed = list2
			});
		}, "CreateAssemblyDrawings"));
	}

	[McpServerTool(Name = "tekla_open_drawing")]
	[Description("Open a drawing in Tekla's drawing editor by its mark so the user can review it.")]
	public static string OpenDrawing([Description("Drawing mark, e.g. 'A1'")] string mark)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			DrawingHandler drawingHandler = Handler();
			Drawing drawing = AllDrawings(drawingHandler).FirstOrDefault((Drawing x) => string.Equals(x.Mark, mark, StringComparison.OrdinalIgnoreCase));
			if (drawing == null)
			{
				return "Error: no drawing with mark '" + mark + "'.";
			}
			return (!drawingHandler.SetActiveDrawing(drawing)) ? "Error: could not open the drawing." : ("Opened drawing " + drawing.Mark + " (" + drawing.Name + ").");
		}, "OpenDrawing"));
	}

	[McpServerTool(Name = "tekla_close_drawing")]
	[Description("Close the drawing currently open in the drawing editor (optionally saving it).")]
	public static string CloseDrawing([Description("Save before closing")] bool save = true)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			DrawingHandler drawingHandler = Handler();
			if (!drawingHandler.IsAnyDrawingOpen())
			{
				return "No drawing is open.";
			}
			if (save)
			{
				drawingHandler.SaveActiveDrawing();
			}
			drawingHandler.CloseActiveDrawing();
			return "Drawing closed.";
		}, "CloseDrawing"));
	}

	[McpServerTool(Name = "tekla_update_drawing")]
	[Description("Update a drawing after model changes. Numbering must be up to date and the drawing must not be open.")]
	public static string UpdateDrawing([Description("Drawing mark")] string mark)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			if (!Operation.IsNumberingUpToDateAll())
			{
				return "Error: numbering is not up to date. Number the model first.";
			}
			DrawingHandler drawingHandler = Handler();
			if (drawingHandler.IsAnyDrawingOpen())
			{
				return "Error: close the open drawing first.";
			}
			Drawing drawing = AllDrawings(drawingHandler).FirstOrDefault((Drawing x) => string.Equals(x.Mark, mark, StringComparison.OrdinalIgnoreCase));
			if (drawing == null)
			{
				return "Error: no drawing with mark '" + mark + "'.";
			}
			return (!drawingHandler.UpdateDrawing(drawing)) ? "Error: update failed (is it locked or frozen?)." : ("Drawing " + drawing.Mark + " updated.");
		}, "UpdateDrawing"));
	}

	[McpServerTool(Name = "tekla_delete_drawings", Destructive = true)]
	[Description("Delete drawings by mark. Shows a preview unless confirm=true. Issued or locked drawings are skipped.")]
	public static string DeleteDrawings([Description("Comma-separated drawing marks")] string marks, [Description("Must be true to actually delete")] bool confirm = false)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			HashSet<string> wanted = new HashSet<string>(from m in marks.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
				select m.Trim(), StringComparer.OrdinalIgnoreCase);
			TeklaSession.EnsureBulk(wanted.Count, "drawings (delete)");
			List<Drawing> source = (from d in AllDrawings(Handler())
				where wanted.Contains(d.Mark)
				select d).ToList();
			List<string> list = (from d in source
				where d.IsIssued || d.IsLocked
				select d.Mark).ToList();
			List<Drawing> list2 = source.Where((Drawing d) => !d.IsIssued && !d.IsLocked).ToList();
			if (!confirm)
			{
				return TeklaSession.ToJson(new
				{
					preview = true,
					wouldDelete = list2.Select((Drawing d) => new
					{
						mark = d.Mark,
						name = d.Name
					}),
					skippedIssuedOrLocked = list,
					note = "Call again with confirm=true to delete."
				});
			}
			int num = list2.Count((Drawing d) => d.Delete());
			return $"Deleted {num} of {list2.Count} drawing(s)." + ((list.Count > 0) ? (" Skipped issued/locked: " + string.Join(", ", list) + ".") : "");
		}, "DeleteDrawings"));
	}
}
