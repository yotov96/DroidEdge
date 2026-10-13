namespace DroidEdge.Core.Files;

public static class UniqueFileName
{
    /// <summary>Returns <paramref name="name"/>, or "stem (n).ext" with the first n not in <paramref name="existing"/>.</summary>
    public static string Pick(string name, ISet<string> existing)
    {
        if (!existing.Contains(name)) return name;
        string ext = Path.GetExtension(name);
        string stem = Path.GetFileNameWithoutExtension(name);
        if (stem.Length == 0) (stem, ext) = (name, ""); // ".env" is a name, not an extension
        for (int n = 1; ; n++)
        {
            string candidate = $"{stem} ({n}){ext}";
            if (!existing.Contains(candidate)) return candidate;
        }
    }
}
