using System.ComponentModel;
using System.Text.Json;

namespace Deswik.Ug.Design.Profiles;

public sealed class DesignProfile
{
    [ReadOnly(true)] public int SchemaVersion { get; set; } = 1;
    [ReadOnly(true)] public string ProfileId { get; set; } = "synthetic-starter";
    public string Name { get; set; } = "Synthetic starter";
    [ReadOnly(true)] public int Revision { get; set; }
    [ReadOnly(true)] public string Basis { get; set; } = "synthetic-test-only";
    [ReadOnly(true)] public string Units { get; set; } = "metres";
    public string Notes { get; set; } = "";
    public RingParameters Ring { get; set; } = new();
    public DeviationParameters Deviation { get; set; } = new();
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class RingParameters
{
    public double HoleDiameterMm { get; set; }
    public double BurdenM { get; set; }
    public double CollarSpacingM { get; set; }
    public double ToeSpacingM { get; set; }
    public double MaxHoleLengthM { get; set; }
    public double MinHoleSeparationM { get; set; }
    public double BrowOffsetM { get; set; }
    public double SubdrillM { get; set; }

    public override string ToString() => "Ring design parameters";
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class DeviationParameters
{
    public double AngularDegrees { get; set; }
    public double CollarToleranceM { get; set; }

    public override string ToString() => "Deviation assumptions";
}

public static class ProfileJson
{
    public const string SyntheticBasis = "synthetic-test-only";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
    };

    public static DesignProfile Starter()
    {
        using var stream = typeof(ProfileJson).Assembly.GetManifestResourceStream(
            "Deswik.Ug.Design.synthetic-starter.json")
            ?? throw new InvalidOperationException("Synthetic starter profile is missing");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public static DesignProfile Parse(string json)
    {
        var profile = JsonSerializer.Deserialize<DesignProfile>(json, Options)
            ?? throw new ArgumentException("Profile JSON is empty");
        Validate(profile);
        return profile;
    }

    public static string Serialize(DesignProfile profile)
    {
        Validate(profile);
        return JsonSerializer.Serialize(profile, Options) + Environment.NewLine;
    }

    public static DesignProfile CopyAsNew(DesignProfile source)
    {
        var copy = Parse(Serialize(source));
        copy.ProfileId = Guid.NewGuid().ToString("D");
        copy.Revision = 1;
        copy.Basis = SyntheticBasis;
        Validate(copy);
        return copy;
    }

    public static void Validate(DesignProfile profile)
    {
        if (profile.SchemaVersion != 1) throw new ArgumentException("Unsupported profile schema version");
        if (string.IsNullOrWhiteSpace(profile.ProfileId)) throw new ArgumentException("Profile ID is required");
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 80)
            throw new ArgumentException("Profile name must contain 1–80 characters");
        if (profile.Revision < 0) throw new ArgumentException("Revision cannot be negative");
        if (profile.Basis != SyntheticBasis) throw new ArgumentException("Only synthetic test-only profiles are supported");
        if (profile.Units != "metres") throw new ArgumentException("Coordinates must use metres");
        if (profile.Notes?.Length > 500) throw new ArgumentException("Notes must be at most 500 characters");
        if (profile.Ring == null || profile.Deviation == null)
            throw new ArgumentException("Ring and deviation parameters are required");

        static void Positive(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentException($"{name} must be positive and finite");
        }
        static void NonNegative(double value, string name)
        {
            if (!double.IsFinite(value) || value < 0) throw new ArgumentException($"{name} must be non-negative and finite");
        }

        Positive(profile.Ring.HoleDiameterMm, nameof(profile.Ring.HoleDiameterMm));
        Positive(profile.Ring.BurdenM, nameof(profile.Ring.BurdenM));
        Positive(profile.Ring.CollarSpacingM, nameof(profile.Ring.CollarSpacingM));
        Positive(profile.Ring.ToeSpacingM, nameof(profile.Ring.ToeSpacingM));
        Positive(profile.Ring.MaxHoleLengthM, nameof(profile.Ring.MaxHoleLengthM));
        Positive(profile.Ring.MinHoleSeparationM, nameof(profile.Ring.MinHoleSeparationM));
        NonNegative(profile.Ring.BrowOffsetM, nameof(profile.Ring.BrowOffsetM));
        NonNegative(profile.Ring.SubdrillM, nameof(profile.Ring.SubdrillM));
        NonNegative(profile.Deviation.AngularDegrees, nameof(profile.Deviation.AngularDegrees));
        NonNegative(profile.Deviation.CollarToleranceM, nameof(profile.Deviation.CollarToleranceM));
        if (profile.Ring.MinHoleSeparationM > profile.Ring.ToeSpacingM)
            throw new ArgumentException("Minimum separation cannot exceed toe spacing");
        if (profile.Ring.BrowOffsetM + profile.Ring.SubdrillM >= profile.Ring.MaxHoleLengthM)
            throw new ArgumentException("Offsets must be shorter than the maximum hole length");
        if (profile.Deviation.AngularDegrees >= 90)
            throw new ArgumentException("Angular deviation must be below 90 degrees");
    }
}
