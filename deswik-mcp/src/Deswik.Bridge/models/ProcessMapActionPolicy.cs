namespace Deswik.Bridge.Models;

public static class ProcessMapActionPolicy
{
    public const string ReadDocumentCommand = "MCP_READ_DOCUMENT";
    public const string ReadDocumentAction = "get_cad_document";

    public static bool TryGetAction(string commandId, out string action)
    {
        if (string.Equals(commandId, ReadDocumentCommand, StringComparison.Ordinal))
        {
            action = ReadDocumentAction;
            return true;
        }

        action = "";
        return false;
    }
}
