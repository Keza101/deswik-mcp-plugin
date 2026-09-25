using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Deswik.Addin;

internal sealed class GuardedWriteCoordinator
{
    public const string PreviewLayer = "_MCP_PREVIEW";
    public const string TargetLayer = @"RINGDESIGN\_MCP_APPROVED\HOLES";

    private readonly CadReader _cad;
    private readonly Dictionary<string, CommitRecord> _commits = new(StringComparer.Ordinal);
    private PreviewRecord? _preview;
    private string _drawingPath = "";
    private string _documentGuid = Guid.NewGuid().ToString("D");

    public GuardedWriteCoordinator(CadReader cad) => _cad = cad;

    public PreviewResult Preview(IReadOnlyList<UGHoleSpec> specs)
    {
        if (specs.Count == 0) throw new ArgumentException("At least one hole is required");
        RefreshDocumentIdentity();

        var existing = _cad.GetLayerHandles(PreviewLayer);
        var recorded = _preview?.PreviewHandles ?? Array.Empty<ulong>();
        if (existing.Any(handle => !recorded.Contains(handle)))
            throw new GuardedWriteException("preview_layer_dirty",
                $"{PreviewLayer} contains {existing.Count} unowned or stale entities");

        if (_preview != null)
            _cad.DeleteExactHandles(_preview.PreviewHandles);

        // Bind the approval to every loaded production figure visible through
        // the documented read actions. The preview layer is excluded because
        // this operation owns and replaces only its recorded handles.
        var sourceHandles = _cad.GetAllHandlesExceptLayer(PreviewLayer);
        var sourceFingerprint = _cad.FingerprintHandles(sourceHandles);
        var previewHandles = _cad.DrawPreviewUGDrillHoles(specs);
        var totalMetres = specs.Sum(HoleLength);
        var recordId = Guid.NewGuid().ToString("D");
        var approvalId = Guid.NewGuid().ToString("D");
        var holeIds = specs.Select(spec => spec.HoleId).ToArray();
        var manifest = new ChangeManifest(
            "commit", _documentGuid, _drawingPath, TargetLayer,
            specs.Count, totalMetres, holeIds, sourceHandles.Count);
        var manifestHash = Hash(manifest);
        _preview = new PreviewRecord(
            recordId, approvalId, _documentGuid, _drawingPath, TargetLayer,
            manifestHash, sourceFingerprint, sourceHandles.ToArray(), specs.ToArray(),
            previewHandles.ToArray(), manifest);
        var binding = new ApprovalBinding(approvalId, "commit", _documentGuid,
            _drawingPath, TargetLayer, manifestHash, sourceFingerprint, recordId);
        return new PreviewResult(recordId, approvalId, previewHandles, manifest, manifestHash, binding);
    }

    public CommitResult Commit(ApprovalBinding approved)
    {
        RefreshDocumentIdentity();
        var record = RequirePreview(approved.RecordId);
        var currentFingerprint = TryFingerprint(record.SourceHandles);
        var actual = new ApprovalBinding(record.ApprovalId, "commit", _documentGuid,
            _drawingPath, record.TargetLayer, record.ManifestHash, currentFingerprint, record.RecordId);
        if (!approved.Equals(actual))
            throw new GuardedWriteException("stale_preview",
                "The drawing or preview source changed; the preview remains for inspection");

        var handles = _cad.DrawUGDrillHolesGuarded(record.TargetLayer, record.Specs);
        _cad.DeleteExactHandles(record.PreviewHandles);
        var createdFingerprint = _cad.FingerprintHandles(handles);
        var commitId = Guid.NewGuid().ToString("D");
        var committed = new CommitRecord(commitId, record.DocumentGuid, record.DrawingPath,
            record.TargetLayer, handles.ToArray(), createdFingerprint, record.Manifest);
        _commits[commitId] = committed;
        _preview = null;
        return new CommitResult(commitId, record.TargetLayer, handles);
    }

    public RollbackPreview PrepareRollback(string commitId)
    {
        RefreshDocumentIdentity();
        if (!_commits.TryGetValue(commitId, out var commit))
            throw new GuardedWriteException("commit_unknown", "Commit record was not found in this add-in session");
        var approvalId = Guid.NewGuid().ToString("D");
        var manifest = new ChangeManifest("rollback", _documentGuid, _drawingPath,
            commit.TargetLayer, commit.Handles.Length, 0, Array.Empty<string>(), commit.Handles.Length);
        var hash = Hash(manifest);
        var fingerprint = TryFingerprint(commit.Handles);
        var binding = new ApprovalBinding(approvalId, "rollback", _documentGuid, _drawingPath,
            commit.TargetLayer, hash, fingerprint, commit.CommitId);
        return new RollbackPreview(approvalId, commit.CommitId, manifest, hash, binding);
    }

    public RollbackResult Rollback(ApprovalBinding approved)
    {
        RefreshDocumentIdentity();
        if (!_commits.TryGetValue(approved.RecordId, out var commit))
            throw new GuardedWriteException("commit_unknown", "Commit record was not found in this add-in session");
        var fingerprint = TryFingerprint(commit.Handles);
        if (!string.Equals(approved.Operation, "rollback", StringComparison.Ordinal) ||
            !string.Equals(approved.DocumentGuid, _documentGuid, StringComparison.Ordinal) ||
            !string.Equals(approved.DrawingPath, _drawingPath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(approved.TargetLayer, commit.TargetLayer, StringComparison.Ordinal) ||
            !string.Equals(approved.SourceFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
            throw new GuardedWriteException("stale_preview", "The committed geometry or drawing changed");
        var deleted = _cad.DeleteExactHandles(commit.Handles);
        _commits.Remove(commit.CommitId);
        return new RollbackResult(commit.CommitId, deleted);
    }

    private PreviewRecord RequirePreview(string recordId)
    {
        if (_preview == null || !string.Equals(_preview.RecordId, recordId, StringComparison.Ordinal))
            throw new GuardedWriteException("preview_unknown", "Preview record was not found in this add-in session");
        return _preview;
    }

    private string TryFingerprint(IEnumerable<ulong> handles)
    {
        try { return _cad.FingerprintHandles(handles); }
        catch { return "missing-or-changed"; }
    }

    private void RefreshDocumentIdentity()
    {
        var current = _cad.DrawingPath;
        if (string.IsNullOrWhiteSpace(current))
            throw new GuardedWriteException("drawing_unsaved",
                "Save the drawing before previewing or committing guarded writes");
        if (string.Equals(current, _drawingPath, StringComparison.OrdinalIgnoreCase)) return;
        _drawingPath = current;
        _documentGuid = Guid.NewGuid().ToString("D");
        _preview = null;
        _commits.Clear();
    }

    private static double HoleLength(UGHoleSpec spec)
    {
        var dx = spec.Toe[0] - spec.Collar[0];
        var dy = spec.Toe[1] - spec.Collar[1];
        var dz = spec.Toe[2] - spec.Collar[2];
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static string Hash(ChangeManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private sealed record PreviewRecord(
        string RecordId, string ApprovalId, string DocumentGuid, string DrawingPath,
        string TargetLayer, string ManifestHash, string SourceFingerprint,
        ulong[] SourceHandles, UGHoleSpec[] Specs, ulong[] PreviewHandles, ChangeManifest Manifest);

    private sealed record CommitRecord(
        string CommitId, string DocumentGuid, string DrawingPath, string TargetLayer,
        ulong[] Handles, string Fingerprint, ChangeManifest Manifest);
}

internal sealed class GuardedWriteException : Exception
{
    public string ErrorCode { get; }
    public GuardedWriteException(string errorCode, string message) : base(message) => ErrorCode = errorCode;
}

internal sealed record ChangeManifest(
    string Operation, string DocumentGuid, string DrawingPath, string TargetLayer,
    int HoleCount, double TotalMetres, string[] HoleIds, int SourceEntityCount);
internal sealed record PreviewResult(
    string RecordId, string ApprovalId, IReadOnlyList<ulong> PreviewHandles,
    ChangeManifest Manifest, string ManifestHash, ApprovalBinding Binding);
internal sealed record ApprovalBinding(
    string ApprovalId, string Operation, string DocumentGuid, string DrawingPath,
    string TargetLayer, string ManifestHash, string SourceFingerprint, string RecordId);
internal sealed record CommitResult(string CommitId, string TargetLayer, IReadOnlyList<ulong> Handles);
internal sealed record RollbackPreview(
    string ApprovalId, string CommitId, ChangeManifest Manifest, string ManifestHash,
    ApprovalBinding Binding);
internal sealed record RollbackResult(string CommitId, int Deleted);
