namespace Deswik.Bridge.Models;

public static class GuardedWriteSourcePolicy
{
    public static bool HandleSetMatches(
        IEnumerable<ulong> recordedHandles,
        IEnumerable<ulong> currentHandles) =>
        recordedHandles.Distinct().OrderBy(handle => handle)
            .SequenceEqual(currentHandles.Distinct().OrderBy(handle => handle));
}
