using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Deswik.Addin;

/// <summary>
/// Fixed entry point invoked by an in-process Process Map macro. The macro
/// locates the already-loaded add-in assembly and calls this method by
/// reflection, so it never loads a plugin or changes Plugin Manager state.
/// </summary>
public static class ProcessMapActions
{
    public static void Run(string commandId) => RunAsync(commandId);

    private static async void RunAsync(string commandId)
    {
        var result = await LoopbackBridgeRequester.InvokeAsync(commandId, CancellationToken.None);
        var text = result.Success
            ? $"Allowlisted bridge action completed.\n\n{result.Message}"
            : $"Bridge action refused ({result.ErrorCode}).\n\n{result.Message}";
        MessageBox.Show(text, "Deswik MCP Bridge", MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }
}
