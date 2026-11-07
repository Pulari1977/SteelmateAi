using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Tekla.Structures;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;

namespace TeklaMcp;

public static class TeklaSession
{
	private static readonly object Gate = new object();

	private static Model _model;

	public static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		WriteIndented = false
	};

	private static readonly Mutex Cross = new Mutex(initiallyOwned: false, "Global\\SteelMateAI_TeklaCalls");

	public const int MaxBulk = 500;

	public static void Log(string line)
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelMateAI");
			Directory.CreateDirectory(text);
			File.AppendAllText(Path.Combine(text, "mcp.log"), DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + Environment.NewLine);
		}
		catch
		{
		}
	}

	public static T Run<T>(Func<Model, T> action, [CallerMemberName] string caller = "")
	{
		lock (Gate)
		{
			bool flag = false;
			try
			{
				flag = Cross.WaitOne(TimeSpan.FromSeconds(60.0));
			}
			catch (AbandonedMutexException)
			{
				flag = true;
			}
			if (!flag)
			{
				throw new InvalidOperationException("Another SteelMate AI call is still running in Tekla. Try again in a moment.");
			}
			try
			{
				Log("START " + caller);
				if (_model == null || !_model.GetConnectionStatus())
				{
					_model = new Model();
				}
				if (!_model.GetConnectionStatus())
				{
					throw new InvalidOperationException("Not connected to Tekla Structures. Make sure Tekla is running with a model open, and that TeklaVersion in SteelMateAI.csproj matches your installed Tekla version.");
				}
				WorkPlaneHandler workPlaneHandler = _model.GetWorkPlaneHandler();
				TransformationPlane currentTransformationPlane = workPlaneHandler.GetCurrentTransformationPlane();
				bool flag2 = false;
				try
				{
					if (!IsGlobal(currentTransformationPlane))
					{
						workPlaneHandler.SetCurrentTransformationPlane(new TransformationPlane());
						flag2 = true;
					}
					T result = action(_model);
					Log("OK    " + caller);
					return result;
				}
				catch (Exception ex2)
				{
					Log("FAIL  " + caller + ": " + ex2.Message);
					throw;
				}
				finally
				{
					if (flag2)
					{
						try
						{
							workPlaneHandler.SetCurrentTransformationPlane(currentTransformationPlane);
						}
						catch
						{
						}
					}
				}
			}
			finally
			{
				Cross.ReleaseMutex();
			}
		}
	}

	private static bool IsGlobal(TransformationPlane tp)
	{
		try
		{
			Matrix transformationMatrixToGlobal = tp.TransformationMatrixToGlobal;
			new Point(0.0, 0.0, 0.0);
			Point point = transformationMatrixToGlobal.Transform(new Point(1.0, 2.0, 3.0));
			return Math.Abs(point.X - 1.0) < 1E-06 && Math.Abs(point.Y - 2.0) < 1E-06 && Math.Abs(point.Z - 3.0) < 1E-06;
		}
		catch
		{
			return false;
		}
	}

	public static void EnsureBulk(int count, string what)
	{
		if (count > 500)
		{
			throw new InvalidOperationException($"Refusing to touch {count} {what} in one call (limit {500}). Narrow the selection and run in smaller batches.");
		}
	}

	public static string ToJson(object value)
	{
		return JsonSerializer.Serialize(value, Json);
	}

	public static ModelObject FindByGuid(Model model, string guid)
	{
		if (!Guid.TryParse(guid, out var result))
		{
			throw new ArgumentException("'" + guid + "' is not a valid GUID.");
		}
		return model.SelectModelObject(new Identifier(result)) ?? throw new ArgumentException("No object with GUID " + guid + " in the model.");
	}

	public static object Pt(Point p)
	{
		if (!(p == null))
		{
			return new
			{
				x = Math.Round(p.X, 2),
				y = Math.Round(p.Y, 2),
				z = Math.Round(p.Z, 2)
			};
		}
		return null;
	}

	public static double ReportDouble(ModelObject o, string prop)
	{
		double value = 0.0;
		o.GetReportProperty(prop, ref value);
		return value;
	}

	public static string ReportString(ModelObject o, string prop)
	{
		string value = "";
		o.GetReportProperty(prop, ref value);
		return value;
	}

	public static Dictionary<string, object> Describe(ModelObject o, bool detailed = false)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>
		{
			["guid"] = o.Identifier.GUID.ToString(),
			["id"] = o.Identifier.ID,
			["type"] = o.GetType().Name
		};
		if (o is Part part)
		{
			dictionary["name"] = part.Name;
			dictionary["profile"] = part.Profile.ProfileString;
			dictionary["material"] = part.Material.MaterialString;
			dictionary["class"] = part.Class;
			dictionary["finish"] = part.Finish;
			dictionary["partPrefix"] = part.PartNumber?.Prefix;
			dictionary["assemblyPrefix"] = part.AssemblyNumber?.Prefix;
			dictionary["weight_kg"] = Math.Round(ReportDouble(o, "WEIGHT"), 2);
			dictionary["length_mm"] = Math.Round(ReportDouble(o, "LENGTH"), 1);
			if (part is Beam beam)
			{
				dictionary["beamType"] = beam.Type.ToString();
				dictionary["start"] = Pt(beam.StartPoint);
				dictionary["end"] = Pt(beam.EndPoint);
			}
			if (detailed)
			{
				dictionary["partPosition"] = ReportString(o, "PART_POS");
				dictionary["assemblyPosition"] = ReportString(o, "ASSEMBLY_POS");
				dictionary["area_m2"] = Math.Round(ReportDouble(o, "AREA") / 1000000.0, 3);
				dictionary["volume_m3"] = Math.Round(ReportDouble(o, "VOLUME") / 1000000000.0, 4);
				dictionary["phase"] = ReportString(o, "PHASE");
				dictionary["positionPlane"] = part.Position.Plane.ToString();
				dictionary["positionRotation"] = part.Position.Rotation.ToString();
				dictionary["positionDepth"] = part.Position.Depth.ToString();
			}
		}
		else if (o is BoltGroup boltGroup)
		{
			dictionary["boltStandard"] = boltGroup.BoltStandard;
			dictionary["boltSize_mm"] = boltGroup.BoltSize;
			dictionary["boltCount"] = boltGroup.BoltPositions?.Count ?? 0;
		}
		else if (o is BaseComponent baseComponent)
		{
			dictionary["name"] = baseComponent.Name;
			dictionary["number"] = baseComponent.Number;
		}
		return dictionary;
	}
}
