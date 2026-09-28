using System.Text;
using Deswik.Mcp.Server;

Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
var adapter = new McpAdapter(new LoopbackBridgeClient());
while (await Console.In.ReadLineAsync() is { } line)
{
    var response = await adapter.ProcessLineAsync(line);
    if (response == null) continue;
    await Console.Out.WriteLineAsync(response);
    await Console.Out.FlushAsync();
}
