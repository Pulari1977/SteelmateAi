using System;
using System.Collections.Generic;

namespace TeklaChat;

public class AgentDef
{
	public string Id;

	public string Name;

	public string Description;

	public string Instructions;

	public bool All;

	public bool ReadOnly;

	public HashSet<string> Tools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
