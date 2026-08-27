using ResoDrive.Core;

namespace ResoDrive.Core.Tests;

public sealed class ProductLinksTests
{
    [Fact]
    public void ConfiguredLinksAreAbsoluteHttpsUris()
    {
        foreach (var link in new[]
                 {
                     ProductLinks.Developer,
                     ProductLinks.Repository,
                     ProductLinks.Releases,
                     ProductLinks.Issues,
                     ProductLinks.LatestReleaseApi
                 })
        {
            Assert.True(link.IsAbsoluteUri);
            Assert.Equal(Uri.UriSchemeHttps, link.Scheme);
        }
    }

    [Fact]
    public void ReleaseResourcesBelongToConfiguredRepository()
    {
        Assert.StartsWith(ProductLinks.Repository.AbsoluteUri, ProductLinks.Releases.AbsoluteUri);
        Assert.EndsWith("/releases/latest", ProductLinks.LatestReleaseApi.AbsolutePath);
    }
}
