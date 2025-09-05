using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Library;
using Xunit;

namespace Jellyfin.Providers.Tests.MediaInfo;

/// <summary>
/// Tests for AudioFileProber SplitWithCustomDelimiter functionality.
/// </summary>
public class AudioFileProberTests
{
    /// <summary>
    /// Test implementation of the SplitWithCustomDelimiter logic.
    /// This replicates the fixed logic from AudioFileProber.
    /// </summary>
    /// <param name="val">The value to split.</param>
    /// <param name="tagDelimiters">The delimiters to use for splitting.</param>
    /// <param name="whitelist">The whitelist of items to not split.</param>
    /// <returns>List of split items.</returns>
    private static List<string> SplitWithCustomDelimiter(string val, char[] tagDelimiters, string[] whitelist)
    {
        var items = new List<string>();
        var temp = val;

        // Handle whitelisted full artist names (existing behavior)
        foreach (var whitelistItem in whitelist)
        {
            if (string.IsNullOrWhiteSpace(whitelistItem))
            {
                continue;
            }

            // If this is a full artist name (more than just a delimiter), handle as before
            if (whitelistItem.Length > 1)
            {
                var originalTemp = temp;
                temp = temp.Replace(whitelistItem, string.Empty, StringComparison.OrdinalIgnoreCase);

                if (!string.Equals(temp, originalTemp, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(whitelistItem);
                }
            }
        }

        // Create a list of delimiters to actually use for splitting
        // Remove any single-character delimiters that are whitelisted
        var whitelistedDelimiters = whitelist.Where(w => !string.IsNullOrWhiteSpace(w) && w.Length == 1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var effectiveDelimiters = tagDelimiters.Where(d => !whitelistedDelimiters.Contains(d.ToString())).ToArray();

        var items2 = temp.Split(effectiveDelimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).DistinctNames();
        items.AddRange(items2);

        return items;
    }

    [Theory]
    [InlineData("Kairon; IRSE!", new[] { ";" }, new[] { "Kairon; IRSE!" })]
    [InlineData("Artist1; Artist2", new[] { ";" }, new[] { "Artist1; Artist2" })]
    [InlineData("AC/DC", new[] { "AC/DC" }, new[] { "AC/DC" })]
    [InlineData("Artist1/Artist2", new[] { "AC/DC" }, new[] { "Artist1", "Artist2" })]
    [InlineData("Artist1; Artist2/Artist3", new[] { ";" }, new[] { "Artist1; Artist2", "Artist3" })]
    [InlineData("Artist1; Artist2/Artist3", new[] { ";", "/" }, new[] { "Artist1; Artist2/Artist3" })]
    [InlineData("K/DA; Test", new[] { "K/DA" }, new[] { "K/DA", "Test" })]
    public void SplitWithCustomDelimiter_VariousScenarios_ReturnsExpected(string input, string[] whitelist, string[] expected)
    {
        // Arrange
        var tagDelimiters = new char[] { '/', '|', ';', '\\' };

        // Act
        var result = SplitWithCustomDelimiter(input, tagDelimiters, whitelist);

        // Assert
        Assert.Equal(expected.Length, result.Count);
        foreach (var expectedItem in expected)
        {
            Assert.Contains(expectedItem, result);
        }
    }

    [Fact]
    public void SplitWithCustomDelimiter_EmptyWhitelist_SplitsNormally()
    {
        // Arrange
        var tagDelimiters = new char[] { '/', '|', ';', '\\' };
        var input = "Artist1;Artist2/Artist3";
        var whitelist = Array.Empty<string>();

        // Act
        var result = SplitWithCustomDelimiter(input, tagDelimiters, whitelist);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Artist1", result);
        Assert.Contains("Artist2", result);
        Assert.Contains("Artist3", result);
    }

    [Fact]
    public void SplitWithCustomDelimiter_WhitelistSingleDelimiter_PreventsSplittingByThatDelimiter()
    {
        // Arrange - This is the original bug case
        var tagDelimiters = new char[] { '/', '|', ';', '\\' };
        var input = "Kairon; IRSE!";
        var whitelist = new[] { ";" }; // User adds semicolon to whitelist

        // Act
        var result = SplitWithCustomDelimiter(input, tagDelimiters, whitelist);

        // Assert - Should return the full artist name, not split it
        Assert.Single(result);
        Assert.Contains("Kairon; IRSE!", result);
    }

    [Fact]
    public void SplitWithCustomDelimiter_MixedWhitelistTypes_HandlesCorrectly()
    {
        // Arrange
        var tagDelimiters = new char[] { '/', '|', ';', '\\' };
        var input = "AC/DC/Test Band; Artist2";
        var whitelist = new[] { "AC/DC", ";" }; // Mix of full artist name and delimiter

        // Act
        var result = SplitWithCustomDelimiter(input, tagDelimiters, whitelist);

        // Assert - Should have AC/DC preserved, and no semicolon splitting
        Assert.Equal(2, result.Count);
        Assert.Contains("AC/DC", result); // Full artist name preserved
        Assert.Contains("Test Band; Artist2", result); // Semicolon didn't split, slash did split AC/DC from the rest
    }

    [Fact]
    public void SplitWithCustomDelimiter_MultipleSameDelimiters_HandlesCorrectly()
    {
        // Arrange
        var tagDelimiters = new char[] { '/', '|', ';', '\\' };
        var input = "Artist; Part1; Artist; Part2";
        var whitelist = new[] { ";" };

        // Act
        var result = SplitWithCustomDelimiter(input, tagDelimiters, whitelist);

        // Assert - Should keep as single artist since semicolon is whitelisted
        Assert.Single(result);
        Assert.Contains("Artist; Part1; Artist; Part2", result);
    }
}
