using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;

namespace TeklaMcp;

[McpServerToolType]
public static class TeklaCheckAndRebarTools
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

	[McpServerTool(Name = "tekla_model_health_check", ReadOnly = true)]
	[Description("One-call quality check of the model: numbering up to date, parts with no name / material / profile / class, zero-length beams, and the same assembly position used by parts with different profile or material (possible duplicate/wrong marks). Optional phase filter. Returns counts plus up to 20 example GUIDs per problem, and a PASS/FAIL line. Read-only.")]
	public static string HealthCheck([Description("Phase number to check (empty = whole model)")] string phase = "", [Description("Examples per problem")] int examples = 20)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			bool flag = false;
			try
			{
				flag = Operation.IsNumberingUpToDateAll();
			}
			catch
			{
			}
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			List<string> list3 = new List<string>();
			List<string> list4 = new List<string>();
			List<string> list5 = new List<string>();
			Dictionary<string, (string, string)> dictionary = new Dictionary<string, (string, string)>();
			List<object> list6 = new List<object>();
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			int num7 = 0;
			foreach (Part item in TeklaTools.EnumerateParts(model, "any"))
			{
				if (stopwatch.Elapsed.TotalSeconds > 45.0)
				{
					break;
				}
				if (string.IsNullOrWhiteSpace(phase) || !(TeklaSession.ReportString(item, "PHASE") != phase.Trim()))
				{
					num++;
					string text = item.Identifier.GUID.ToString();
					if (string.IsNullOrWhiteSpace(item.Name))
					{
						num2++;
						if (list.Count < examples)
						{
							list.Add(text);
						}
					}
					if (string.IsNullOrWhiteSpace(item.Material.MaterialString))
					{
						num3++;
						if (list2.Count < examples)
						{
							list2.Add(text);
						}
					}
					if (string.IsNullOrWhiteSpace(item.Profile.ProfileString))
					{
						num4++;
						if (list3.Count < examples)
						{
							list3.Add(text);
						}
					}
					if (string.IsNullOrWhiteSpace(item.Class))
					{
						num5++;
						if (list4.Count < examples)
						{
							list4.Add(text);
						}
					}
					if (item is Beam beam && Distance.PointToPoint(beam.StartPoint, beam.EndPoint) < 1.0)
					{
						num6++;
						if (list5.Count < examples)
						{
							list5.Add(text);
						}
					}
					string text2 = TeklaSession.ReportString(item, "PART_POS");
					if (!string.IsNullOrEmpty(text2))
					{
						string text3 = item.Profile.ProfileString + "|" + item.Material.MaterialString;
						if (dictionary.TryGetValue(text2, out var value))
						{
							if (value.Item1 != text3)
							{
								num7++;
								if (list6.Count < examples)
								{
									list6.Add(new
									{
										position = text2,
										a = value.Item2,
										b = text,
										aInfo = value.Item1,
										bInfo = text3
									});
								}
							}
						}
						else
						{
							dictionary[text2] = (text3, text);
						}
					}
				}
			}
			int num8 = num2 + num3 + num4 + num5 + num6 + num7 + ((!flag) ? 1 : 0);
			return TeklaSession.ToJson(new
			{
				result = ((num8 == 0) ? "PASS" : ("FAIL: " + num8 + " problem(s)")),
				phase = (string.IsNullOrWhiteSpace(phase) ? "all" : phase),
				partsChecked = num,
				timedOut = (stopwatch.Elapsed.TotalSeconds > 45.0),
				numberingUpToDate = flag,
				noName = new
				{
					count = num2,
					examples = list
				},
				noMaterial = new
				{
					count = num3,
					examples = list2
				},
				noProfile = new
				{
					count = num4,
					examples = list3
				},
				noClass = new
				{
					count = num5,
					examples = list4
				},
				zeroLengthBeams = new
				{
					count = num6,
					examples = list5
				},
				samePositionDifferentPart = new
				{
					count = num7,
					examples = list6
				},
				note = "Position clashes only show when the model has been numbered. Fix numbering first if it is out of date."
			});
		}, "HealthCheck"));
	}

	[McpServerTool(Name = "tekla_create_rebar_group")]
	[Description("Create a straight-leg rebar group (e.g. a row of bars or a stirrup set) in a concrete part. 'polygon' is the bar shape as 'x,y,z;x,y,z;...' in GLOBAL coordinates; the group repeats the shape from startPoint to endPoint at the given spacing. Tagged TEKLAAI_CREATED. Rebar API details differ by Tekla version: if this fails, use tekla_api_type RebarGroup and tell the user. Check the result in Tekla before relying on it.")]
	public static string CreateRebarGroup([Description("GUID of the concrete part (beam/column/slab) that holds the rebar")] string hostGuid, [Description("Bar shape points 'x,y,z;x,y,z;...' (2 points = straight bar; more = bent bar/stirrup)")] string polygon, [Description("Start of the repeat 'x,y,z'")] string startPoint, [Description("End of the repeat 'x,y,z'")] string endPoint, [Description("Bar size/diameter as in your rebar catalog, e.g. '12'")] string size = "12", [Description("Rebar grade, e.g. 'B500B'")] string grade = "B500B", [Description("Spacing between bars in mm")] double spacing = 200.0, [Description("Bend radius in mm")] double bendRadius = 30.0, [Description("Rebar name")] string name = "REBAR", [Description("Rebar class number")] int rebarClass = 1)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			if (!(TeklaTools.ResolveTargets(model, hostGuid).FirstOrDefault() is Part father))
			{
				return "Error: hostGuid is not a part.";
			}
			Polygon polygon2 = new Polygon();
			foreach (Point item in ParseList(polygon))
			{
				polygon2.Points.Add(item);
			}
			if (polygon2.Points.Count < 2)
			{
				return "Error: polygon needs at least 2 points.";
			}
			RebarGroup rebarGroup = new RebarGroup
			{
				Polygons = new ArrayList { polygon2 },
				StartPoint = ParseList(startPoint)[0],
				EndPoint = ParseList(endPoint)[0],
				Father = father,
				Name = name,
				Size = size,
				Grade = grade,
				Class = rebarClass
			};
			rebarGroup.Spacings.Add(spacing);
			rebarGroup.RadiusValues.Add(bendRadius);
			TrySetEnum(rebarGroup, "SpacingType", "SPACING_TYPE_TARGET_SPACE");
			if (!rebarGroup.Insert())
			{
				return "Error: Tekla refused the rebar group. Check size/grade exist in your rebar catalog and that the host is a concrete part.";
			}
			TeklaTools.Tag(rebarGroup, "TEKLAAI_CREATED");
			model.CommitChanges();
			return TeklaSession.ToJson(new
			{
				created = "rebar group",
				guid = rebarGroup.Identifier.GUID.ToString(),
				host = hostGuid,
				size = size,
				grade = grade,
				spacing = spacing
			});
		}, "CreateRebarGroup"));
	}

	[McpServerTool(Name = "tekla_rebar_summary", ReadOnly = true)]
	[Description("Count the rebar groups/single bars in the model (or only those in the selected parts) grouped by size and grade, with total weight where Tekla reports it. Read-only.")]
	public static string RebarSummary([Description("true = only rebar inside the currently selected parts")] bool selectedOnly = false)
	{
		return Safe(() => TeklaSession.Run(delegate(Model model)
		{
			ModelObjectEnumerator.AutoFetch = true;
			HashSet<string> hashSet = new HashSet<string>();
			if (selectedOnly)
			{
				foreach (ModelObject item in TeklaTools.ResolveTargets(model, "selected"))
				{
					hashSet.Add(item.Identifier.GUID.ToString());
				}
			}
			Dictionary<string, (int, double)> dictionary = new Dictionary<string, (int, double)>();
			Stopwatch stopwatch = Stopwatch.StartNew();
			int num = 0;
			ModelObject.ModelObjectEnum[] array = new ModelObject.ModelObjectEnum[7]
			{
				ModelObject.ModelObjectEnum.SINGLEREBAR,
				ModelObject.ModelObjectEnum.REBARGROUP,
				ModelObject.ModelObjectEnum.REBARMESH,
				ModelObject.ModelObjectEnum.REBARSTRAND,
				ModelObject.ModelObjectEnum.CIRCLEREBAR,
				ModelObject.ModelObjectEnum.CIRCLE_REBARGROUP,
				ModelObject.ModelObjectEnum.CURVED_REBARGROUP
			};
			foreach (ModelObject.ModelObjectEnum @enum in array)
			{
				ModelObjectEnumerator allObjectsWithType = model.GetModelObjectSelector().GetAllObjectsWithType(@enum);
				while (allObjectsWithType.MoveNext() && stopwatch.Elapsed.TotalSeconds < 45.0)
				{
					if (allObjectsWithType.Current is Reinforcement reinforcement)
					{
						if (selectedOnly)
						{
							ModelObject father = reinforcement.Father;
							if (father == null || !hashSet.Contains(father.Identifier.GUID.ToString()))
							{
								continue;
							}
						}
						string text = "?";
						string text2 = "?";
						try
						{
							text = ((string)reinforcement.GetType().GetProperty("Size")?.GetValue(reinforcement)) ?? "?";
						}
						catch
						{
						}
						try
						{
							text2 = ((string)reinforcement.GetType().GetProperty("Grade")?.GetValue(reinforcement)) ?? "?";
						}
						catch
						{
						}
						string key = text + " / " + text2;
						double num2 = 0.0;
						try
						{
							num2 = TeklaSession.ReportDouble(reinforcement, "WEIGHT");
						}
						catch
						{
						}
						dictionary.TryGetValue(key, out var value);
						dictionary[key] = (value.Item1 + 1, value.Item2 + num2);
						num++;
					}
				}
			}
			return TeklaSession.ToJson(new
			{
				totalRebarObjects = num,
				bySizeAndGrade = (from x in Enumerable.Select(dictionary, (KeyValuePair<string, (int n, double kg)> kv) => new
					{
						sizeGrade = kv.Key,
						count = kv.Value.n,
						weightKg = Math.Round(kv.Value.kg, 1)
					})
					orderby x.sizeGrade
					select x).ToList(),
				timedOut = (stopwatch.Elapsed.TotalSeconds >= 45.0)
			});
		}, "RebarSummary"));
	}

	private static List<Point> ParseList(string text)
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

	private static void TrySetEnum(object target, string prop, string value)
	{
		try
		{
			PropertyInfo property = target.GetType().GetProperty(prop);
			if (!(property == null) && property.PropertyType.IsEnum && Enum.IsDefined(property.PropertyType, value))
			{
				property.SetValue(target, Enum.Parse(property.PropertyType, value));
			}
		}
		catch
		{
		}
	}
}
