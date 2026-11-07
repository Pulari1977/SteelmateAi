using System;
using System.Collections.Generic;

namespace TeklaChat;

public class ChatRecord
{
	public string Id { get; set; }

	public DateTime Started { get; set; }

	public string Title { get; set; }

	public string Provider { get; set; }

	public string Model { get; set; }

	public List<string> Lines { get; set; } = new List<string>();

}
