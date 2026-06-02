using AvaloniaBattleground.Core;

namespace AvaloniaBattleground.Tests;

public sealed class LocalProfileStoreTests
{
    [Fact]
    public void Missing_profile_file_loads_fallback_display_name()
    {
        var profilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "profile.json");
        var store = new LocalProfileStore(profilePath);

        var profile = store.Load();

        Assert.Equal("Player", profile.DisplayName);
    }

    [Fact]
    public void Saved_display_name_loads_on_next_read()
    {
        var profilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "profile.json");
        var store = new LocalProfileStore(profilePath);

        store.Save(new LocalProfile("Acryteryx"));
        var reloadedProfile = new LocalProfileStore(profilePath).Load();

        Assert.Equal("Acryteryx", reloadedProfile.DisplayName);
    }

    [Fact]
    public void Blank_display_name_normalizes_to_fallback()
    {
        var profilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "profile.json");
        var store = new LocalProfileStore(profilePath);

        store.Save(new LocalProfile("   "));
        var profile = store.Load();

        Assert.Equal("Player", profile.DisplayName);
    }
}
