using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using ModelContextProtocol.Server;
using Tekla.Structures.Drawing;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;

namespace TeklaMcp;

[McpServerToolType]
public static class TeklaQaTools
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

	private static int Count(ModelObjectEnumerator e)
	{
		int num = 0;
		if (e == null)
		{
			return 0;
		}
		while (e.MoveNext())
		{
			num++;
		}
		return num;
	}

	[McpServerTool(Name = "tekla_drawing_qa", ReadOnly = true)]
	[Description("QA of drawings. (1) For ALL drawings: which are out of date, not issued, or locked. (2) For the drawing that is OPEN in Tekla: every view's part count, dimension sets and marks; flags views with no dimensions, parts that may have no mark, and dimension lines that sit on top of each other (same direction and distance). It cannot see text collisions or judge dimension values. Read-only.")]
	public static string DrawingQa([Description("Only drawings whose mark/name contains this text (empty = all)")] string filter = "", [Description("Max out-of-date drawings listed")] int limit = 100)
	{
		return Safe(() => TeklaSession.Run(delegate
		{
			DrawingHandler drawingHandler = new DrawingHandler();
			if (!drawingHandler.GetConnectionStatus())
			{
				return "Error: Drawing API not connected.";
			}
			int num = 0;
			int num2 = 0;
			List<object> list = new List<object>();
			int num3 = 0;
			int num4 = 0;
			DrawingEnumerator drawings = drawingHandler.GetDrawings();
			while (drawings.MoveNext())
			{
				Drawing current = drawings.Current;
				if (current != null)
				{
					string text = current.Mark + " " + current.Name;
					if (string.IsNullOrEmpty(filter) || text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						num++;
						string text2 = current.UpToDateStatus.ToString();
						string text3 = text2.Replace("_", "").ToLowerInvariant();
						if (text3.Contains("uptodate") && !text3.Contains("not"))
						{
							num2++;
						}
						else if (list.Count < limit)
						{
							list.Add(new
							{
								mark = current.Mark,
								name = current.Name,
								status = text2,
								issued = current.IsIssued
							});
						}
						if (current.IsLocked)
						{
							num3++;
						}
						if (current.IsIssued)
						{
							num4++;
						}
					}
				}
			}
			object openDrawing = "no drawing is open (open one with tekla_open_drawing for a content check)";
			Drawing activeDrawing = drawingHandler.GetActiveDrawing();
			if (activeDrawing != null)
			{
				List<object> list2 = new List<object>();
				List<string> list3 = new List<string>();
				DrawingObjectEnumerator views = activeDrawing.GetSheet().GetViews();
				while (views.MoveNext())
				{
					if (views.Current is View view)
					{
						int num5 = Cnt(view, typeof(Tekla.Structures.Drawing.Part));
						int num6 = Cnt(view, typeof(Mark));
						List<StraightDimensionSet> list4 = new List<StraightDimensionSet>();
						DrawingObjectEnumerator allObjects = view.GetAllObjects(typeof(StraightDimensionSet));
						while (allObjects.MoveNext())
						{
							if (allObjects.Current is StraightDimensionSet item)
							{
								list4.Add(item);
							}
						}
						if (num5 > 0 && list4.Count == 0)
						{
							list3.Add($"View '{view.Name}' has {num5} parts but NO dimensions.");
						}
						if (num5 > num6)
						{
							list3.Add($"View '{view.Name}': {num5} parts but only {num6} marks (some parts may be unmarked).");
						}
						foreach (IGrouping<string, StraightDimensionSet> item2 in from s in list4
							group s by $"{UpDirectionKey(s)}/{Math.Round(s.Distance, 1)}" into g
							where g.Count() > 1
							select g)
						{
							list3.Add($"View '{view.Name}': {item2.Count()} dimension lines sit at the same distance/direction (possible overlap).");
						}
						list2.Add(new
						{
							view = view.Name,
							parts = num5,
							marks = num6,
							dimensionSets = list4.Count
						});
					}
				}
				openDrawing = new
				{
					drawing = activeDrawing.Name,
					mark = activeDrawing.Mark,
					views = list2,
					problems = ((list3.Count == 0) ? new List<string> { "none found" } : list3)
				};
			}
			return TeklaSession.ToJson(new
			{
				result = ((list.Count == 0) ? "All checked drawings are up to date" : (list.Count + " drawing(s) NOT up to date")),
				drawingsChecked = num,
				upToDate = num2,
				issued = num4,
				locked = num3,
				notUpToDate = list,
				openDrawing = openDrawing,
				limits = "Does not check dimension values, text collisions or drawing/model consistency in detail."
			});
		}, "DrawingQa"));
	}

	private static string UpDirectionKey(StraightDimensionSet set)
	{
		DrawingObjectEnumerator objects = set.GetObjects();
		while (objects.MoveNext())
		{
			if (objects.Current is StraightDimension straightDimension)
			{
				return $"{Math.Round(straightDimension.UpDirection.X, 2)}/{Math.Round(straightDimension.UpDirection.Y, 2)}";
			}
		}
		return "?";
	}

	private static int Cnt(ViewBase v, Type t)
	{
		int num = 0;
		DrawingObjectEnumerator allObjects = v.GetAllObjects(t);
		while (allObjects.MoveNext())
		{
			num++;
		}
		return num;
	}

	[McpServerTool(Name = "tekla_connection_check", ReadOnly = true)]
	[Description("Heuristic completeness check for connections: (a) secondary parts in an assembly with NO weld and NO bolt, (b) loose plates (no weld, no bolt, own assembly), (c) floating beams (neither end near any other part). Uses bounding boxes, so treat results as 'please look at these', not proof. Optional phase filter. Max 500 parts. Read-only.")]
	public static string ConnectionCheck([Description("Phase number (empty = whole model)")] string phase = "", [Description("How close an end must be to another part to count as connected, mm")] double touchMm = 30.0, [Description("Examples per problem")] int examples = 25)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			List<Tekla.Structures.Model.Part> list = (from p in TeklaTools.EnumerateParts(model, "any")
				where string.IsNullOrWhiteSpace(phase) || TeklaSession.ReportString(p, "PHASE") == phase.Trim()
				select p).ToList();
			TeklaSession.EnsureBulk(list.Count, "parts");
			List<(Tekla.Structures.Model.Part, Point, Point)> list2 = new List<(Tekla.Structures.Model.Part, Point, Point)>();
			foreach (Tekla.Structures.Model.Part item3 in list)
			{
				Solid solid = item3.GetSolid();
				if (solid != null)
				{
					list2.Add((item3, solid.MinimumPoint, solid.MaximumPoint));
				}
			}
			List<object> list3 = new List<object>();
			List<object> list4 = new List<object>();
			List<object> list5 = new List<object>();
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			Stopwatch stopwatch = Stopwatch.StartNew();
			foreach (var item4 in list2)
			{
				if (stopwatch.Elapsed.TotalSeconds > 45.0)
				{
					break;
				}
				Tekla.Structures.Model.Part item = item4.Item1;
				int num4 = Count(item.GetBolts());
				int num5 = Count(item.GetWelds());
				bool flag = false;
				bool flag2 = true;
				try
				{
					Assembly assembly = item.GetAssembly();
					Tekla.Structures.Model.ModelObject modelObject = assembly?.GetMainPart();
					flag2 = modelObject == null || modelObject.Identifier.GUID == item.Identifier.GUID;
					flag = assembly != null && assembly.GetSecondaries().Count > 0;
				}
				catch
				{
				}
				var item2 = new
				{
					guid = item.Identifier.GUID.ToString(),
					name = item.Name,
					profile = item.Profile.ProfileString
				};
				if (!flag2 && num4 == 0 && num5 == 0)
				{
					num++;
					if (list3.Count < examples)
					{
						list3.Add(item2);
					}
				}
				if ((item is ContourPlate || (item.Profile.ProfileString ?? "").StartsWith("PL", StringComparison.OrdinalIgnoreCase)) && flag2 && !flag && num4 == 0 && num5 == 0)
				{
					num2++;
					if (list4.Count < examples)
					{
						list4.Add(item2);
					}
				}
				Beam beam = item as Beam;
				if (beam != null && beam.Type == Beam.BeamTypeEnum.BEAM && flag2 && !flag && num4 == 0 && num5 == 0)
				{
					bool num6 = Near(list2, item, beam.StartPoint, touchMm);
					bool flag3 = Near(list2, item, beam.EndPoint, touchMm);
					if (!num6 && !flag3)
					{
						num3++;
						if (list5.Count < examples)
						{
							list5.Add(item2);
						}
					}
				}
			}
			int num7 = num + num2 + num3;
			return TeklaSession.ToJson(new
			{
				result = ((num7 == 0) ? "PASS (no suspects)" : ("REVIEW: " + num7 + " suspect(s)")),
				partsChecked = list2.Count,
				timedOut = (stopwatch.Elapsed.TotalSeconds > 45.0),
				secondaryPartsWithoutWeldOrBolt = new
				{
					count = num,
					examples = list3
				},
				loosePlates = new
				{
					count = num2,
					examples = list4
				},
				floatingBeams = new
				{
					count = num3,
					examples = list5
				},
				note = "Heuristic. Welds made by components may still show on the parent; check the examples in Tekla."
			});
		}, "ConnectionCheck"));
	}

	private static bool Near(List<(Tekla.Structures.Model.Part p, Point mn, Point mx)> boxes, Tekla.Structures.Model.Part self, Point pt, double tol)
	{
		foreach (var box in boxes)
		{
			if (!(box.p.Identifier.GUID == self.Identifier.GUID) && pt.X >= box.mn.X - tol && pt.X <= box.mx.X + tol && pt.Y >= box.mn.Y - tol && pt.Y <= box.mx.Y + tol && pt.Z >= box.mn.Z - tol && pt.Z <= box.mx.Z + tol)
			{
				return true;
			}
		}
		return false;
	}
}
