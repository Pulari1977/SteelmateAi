using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using Tekla.Structures.Model;

namespace MyTools
{
    /// <summary>First sample tool: counts parts in the open model, grouped by profile.</summary>
    [McpServerToolType]
    public static class CountParts
    {
        [McpServerTool(Name = "tekla_count_parts", ReadOnly = true)]
        [Description("Count the parts in the open Tekla model of one type (BEAM, COLUMN-like parts are BEAM too, CONTOURPLATE, POLYBEAM) and group them by profile. Read only.")]
        public static string Count(
            [Description("Part type: BEAM (default), CONTOURPLATE or POLYBEAM.")] string type = "BEAM")
        {
            if (!Enum.TryParse(type, true, out ModelObject.ModelObjectEnum kind)
                || (kind != ModelObject.ModelObjectEnum.BEAM
                    && kind != ModelObject.ModelObjectEnum.CONTOURPLATE
                    && kind != ModelObject.ModelObjectEnum.POLYBEAM))
                throw new ArgumentException("type must be BEAM, CONTOURPLATE or POLYBEAM.");

            return TeklaMcp.TeklaSession.Run(model =>
            {
                var byProfile = new Dictionary<string, int>();
                int total = 0;
                var e = model.GetModelObjectSelector().GetAllObjectsWithType(kind);
                while (e.MoveNext())
                {
                    var part = e.Current as Part;
                    if (part == null) continue;
                    total++;
                    string key = part.Profile != null ? part.Profile.ProfileString : "(no profile)";
                    byProfile[key] = byProfile.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                return TeklaMcp.TeklaSession.ToJson(new
                {
                    type = kind.ToString(),
                    total,
                    byProfile = byProfile.OrderByDescending(kv => kv.Value)
                                         .Select(kv => new { profile = kv.Key, count = kv.Value })
                });
            });
        }
    }
}
