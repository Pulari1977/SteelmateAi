using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TeklaChat;

public delegate Task<string> ToolCaller(string name, JsonNode args);
