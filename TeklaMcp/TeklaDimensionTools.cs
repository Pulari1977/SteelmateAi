using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using Tekla.Structures.Drawing;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;

namespace TeklaMcp;

[McpServerToolType]
public static class TeklaDimensionTools
{
	[McpServerTool(Name = "tekla_auto_dimension")]
	[Description("Add dimensions to the drawing that is OPEN in Tekla (open it first with tekla_open_drawing). For every view it adds a horizontal and a vertical dimension chain along the extreme edges of the parts AND the bolt/hole centres in that view, plus an overall dimension. Simple 'first pass' dimensions: the user should review them in Tekla. Ctrl+Z undoes. Dimension look comes from Tekla's default dimension settings.")]
	public static string AutoDimension([Description("Distance of the first dimension line from the part, in drawing mm (paper)")] double offset = 12.0, [Description("true = also add one overall dimension line outside the detail chain")] bool overall = true, [Description("Only dimension views whose name contains this text (empty = all views)")] string viewFilter = "", [Description("Dimension horizontally")] bool horizontal = true, [Description("Dimension vertically")] bool vertical = true, [Description("true = also dimension bolt/hole positions, so hole centres are dimensioned")] bool includeBolts = true)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			DrawingHandler drawingHandler = new DrawingHandler();
			if (!drawingHandler.GetConnectionStatus())
			{
				return "Error: Drawing API not connected.";
			}
			Drawing activeDrawing = drawingHandler.GetActiveDrawing();
			if (activeDrawing == null)
			{
				return "Error: no drawing is open. Open one with tekla_open_drawing first.";
			}
			StraightDimensionSetHandler h = new StraightDimensionSetHandler();
			List<object> list = new List<object>();
			int num = 0;
			DrawingObjectEnumerator views = activeDrawing.GetSheet().GetViews();
			while (views.MoveNext())
			{
				if (views.Current is View view && (string.IsNullOrEmpty(viewFilter) || (view.Name ?? "").IndexOf(viewFilter, StringComparison.OrdinalIgnoreCase) >= 0))
				{
					try
					{
						Matrix matrix = MatrixFactory.ToCoordinateSystem(view.DisplayCoordinateSystem);
						List<double> list2 = new List<double>();
						List<double> list3 = new List<double>();
						int num2 = 0;
						DrawingObjectEnumerator allObjects = view.GetAllObjects(typeof(Tekla.Structures.Drawing.Part));
						while (allObjects.MoveNext())
						{
							if (allObjects.Current is Tekla.Structures.Drawing.Part part)
							{
								Tekla.Structures.Model.Part part2 = model.SelectModelObject(part.ModelIdentifier) as Tekla.Structures.Model.Part;
								Solid solid = part2?.GetSolid();
								if (solid != null)
								{
									num2++;
									Point minimumPoint = solid.MinimumPoint;
									Point maximumPoint = solid.MaximumPoint;
									double? num3 = null;
									double? num4 = null;
									double? num5 = null;
									double? num6 = null;
									double[] array = new double[2] { minimumPoint.X, maximumPoint.X };
									foreach (double x in array)
									{
										double[] array2 = new double[2] { minimumPoint.Y, maximumPoint.Y };
										foreach (double y in array2)
										{
											double[] array3 = new double[2] { minimumPoint.Z, maximumPoint.Z };
											foreach (double z in array3)
											{
												Point point = matrix.Transform(new Point(x, y, z));
												num3 = (num3.HasValue ? Math.Min(num3.Value, point.X) : point.X);
												num4 = (num4.HasValue ? Math.Max(num4.Value, point.X) : point.X);
												num5 = (num5.HasValue ? Math.Min(num5.Value, point.Y) : point.Y);
												num6 = (num6.HasValue ? Math.Max(num6.Value, point.Y) : point.Y);
											}
										}
									}
									list2.Add(num3.Value);
									list2.Add(num4.Value);
									list3.Add(num5.Value);
									list3.Add(num6.Value);
									if (includeBolts)
									{
										ModelObjectEnumerator bolts = part2.GetBolts();
										while (bolts != null && bolts.MoveNext())
										{
											if (bolts.Current is BoltGroup { BoltPositions: not null } boltGroup)
											{
												foreach (object boltPosition in boltGroup.BoltPositions)
												{
													if (boltPosition is Point p)
													{
														Point point2 = matrix.Transform(p);
														list2.Add(point2.X);
														list3.Add(point2.Y);
													}
												}
											}
										}
									}
								}
							}
						}
						if (num2 == 0)
						{
							list.Add(new
							{
								view = view.Name,
								note = "no parts found"
							});
						}
						else
						{
							double x0 = list2.Min();
							double x2 = list2.Max();
							double y0 = list3.Min();
							double y2 = list3.Max();
							int num7 = 0;
							if (horizontal)
							{
								List<double> list4 = Distinct(list2);
								if (list4.Count >= 2)
								{
									Make(h, view, list4.Select((double v) => new Point(v, y0, 0.0)), new Vector(0.0, -1.0, 0.0), offset);
									num7++;
								}
								if (overall && list4.Count > 2)
								{
									Make(h, view, new Point[2]
									{
										new Point(x0, y0, 0.0),
										new Point(x2, y0, 0.0)
									}, new Vector(0.0, -1.0, 0.0), offset * 2.2);
									num7++;
								}
							}
							if (vertical)
							{
								List<double> list5 = Distinct(list3);
								if (list5.Count >= 2)
								{
									Make(h, view, list5.Select((double v) => new Point(x0, v, 0.0)), new Vector(-1.0, 0.0, 0.0), offset);
									num7++;
								}
								if (overall && list5.Count > 2)
								{
									Make(h, view, new Point[2]
									{
										new Point(x0, y0, 0.0),
										new Point(x0, y2, 0.0)
									}, new Vector(-1.0, 0.0, 0.0), offset * 2.2);
									num7++;
								}
							}
							num += num7;
							list.Add(new
							{
								view = view.Name,
								partsUsed = num2,
								dimensionSetsAdded = num7
							});
						}
					}
					catch (Exception ex)
					{
						list.Add(new
						{
							view = view.Name,
							error = ex.Message
						});
					}
				}
			}
			activeDrawing.CommitChanges();
			return TeklaSession.ToJson(new
			{
				drawing = activeDrawing.Name,
				dimensionSetsAdded = num,
				views = list,
				note = "First-pass dimensions along part extremes. Review in Tekla; Ctrl+Z undoes."
			});
		}, "AutoDimension"));
	}

	private static void Make(StraightDimensionSetHandler h, ViewBase view, IEnumerable<Point> pts, Vector dir, double dist)
	{
		PointList pointList = new PointList();
		foreach (Point pt in pts)
		{
			pointList.Add(pt);
		}
		h.CreateDimensionSet(view, pointList, dir, dist);
	}

	private static List<double> Distinct(List<double> vals)
	{
		List<double> list = vals.OrderBy((double v) => v).ToList();
		List<double> list2 = new List<double>();
		foreach (double item in list)
		{
			if (list2.Count == 0 || item - list2[list2.Count - 1] > 1.0)
			{
				list2.Add(item);
			}
		}
		return list2;
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
}
