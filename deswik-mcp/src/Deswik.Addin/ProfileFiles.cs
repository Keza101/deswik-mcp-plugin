using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Deswik.Ug.Design.Profiles;

namespace Deswik.Addin;

internal static class ProfileFiles
{
    public static DesignProfile Load(string path) => ProfileJson.Parse(File.ReadAllText(path));

    public static (DesignProfile Profile, string Path) SaveNew(DesignProfile source, string directory)
    {
        var copy = ProfileJson.CopyAsNew(source);
        var slug = Regex.Replace(copy.Name.Trim(), "[^A-Za-z0-9_-]+", "-").Trim('-');
        if (slug.Length == 0) slug = "profile";
        if (slug.Length > 40) slug = slug[..40];
        var json = ProfileJson.Serialize(copy);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{slug}-{copy.ProfileId}.json");
        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
            writer.Write(json);
        return (copy, path);
    }
}
