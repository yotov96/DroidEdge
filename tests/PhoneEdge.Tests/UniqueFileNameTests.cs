using PhoneEdge.Core.Files;

namespace PhoneEdge.Tests;

public class UniqueFileNameTests
{
    private static HashSet<string> Names(params string[] n) => new(n, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Keeps_the_name_when_free()
    {
        Assert.Equal("photo.jpg", UniqueFileName.Pick("photo.jpg", Names("other.jpg")));
    }

    [Fact]
    public void Appends_the_first_free_counter_before_the_extension()
    {
        Assert.Equal("photo (1).jpg", UniqueFileName.Pick("photo.jpg", Names("photo.jpg")));
        Assert.Equal("photo (3).jpg", UniqueFileName.Pick("photo.jpg", Names("photo.jpg", "photo (1).jpg", "photo (2).jpg")));
    }

    [Fact]
    public void Comparison_ignores_case_like_shared_storage()
    {
        Assert.Equal("Photo (1).JPG", UniqueFileName.Pick("Photo.JPG", Names("photo.jpg")));
    }

    [Fact]
    public void Handles_names_without_extension_and_dot_files()
    {
        Assert.Equal("notes (1)", UniqueFileName.Pick("notes", Names("notes")));
        Assert.Equal(".env (1)", UniqueFileName.Pick(".env", Names(".env")));
    }
}
