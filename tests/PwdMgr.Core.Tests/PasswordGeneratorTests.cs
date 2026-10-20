using PwdMgr.Core.Security;
using Xunit;

namespace PwdMgr.Core.Tests;

public sealed class PasswordGeneratorTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(64)]
    [InlineData(128)]
    public void Generate_ReturnsRequestedLength(int length)
    {
        var password = PasswordGenerator.Generate(new PasswordOptions { Length = length });

        Assert.Equal(length, password.Length);
    }

    [Fact]
    public void Generate_DigitsOnly_ContainsOnlyDigits()
    {
        var password = PasswordGenerator.Generate(new PasswordOptions
        {
            Length = 32,
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeSymbols = false,
        });

        Assert.All(password, character => Assert.Contains(character, PasswordGenerator.Digits));
    }

    [Fact]
    public void Generate_EachEnabledClassAppears()
    {
        var options = new PasswordOptions { Length = 8 };

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var password = PasswordGenerator.Generate(options);

            Assert.Contains(password, character => char.IsAsciiLetterUpper(character));
            Assert.Contains(password, character => char.IsAsciiLetterLower(character));
            Assert.Contains(password, character => char.IsAsciiDigit(character));
            Assert.Contains(password, character => !char.IsAsciiLetterOrDigit(character));
        }
    }

    [Fact]
    public void Generate_ExcludesAmbiguousCharacters()
    {
        var password = PasswordGenerator.Generate(new PasswordOptions { Length = 128, ExcludeAmbiguous = true });

        Assert.DoesNotContain('I', password);
        Assert.DoesNotContain('O', password);
        Assert.DoesNotContain('l', password);
        Assert.DoesNotContain('0', password);
        Assert.DoesNotContain('1', password);
    }

    [Fact]
    public void Generate_WithAmbiguousAllowed_CanProduceThem()
    {
        var options = new PasswordOptions
        {
            Length = 128,
            ExcludeAmbiguous = false,
            IncludeSymbols = false,
            IncludeUppercase = false,
            IncludeLowercase = false,
        };

        var sawZero = false;
        var sawOne = false;
        for (var attempt = 0; attempt < 200 && !(sawZero && sawOne); attempt++)
        {
            var password = PasswordGenerator.Generate(options);
            sawZero |= password.Contains('0', StringComparison.Ordinal);
            sawOne |= password.Contains('1', StringComparison.Ordinal);
        }

        Assert.True(sawZero && sawOne);
    }

    [Fact]
    public void Generate_WithCustomCharacters_AlwaysContainsAtLeastOne()
    {
        var options = new PasswordOptions { Length = 16, CustomCharacters = "★※" };

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var password = PasswordGenerator.Generate(options);
            Assert.Contains(password, character => character is '★' or '※');
        }
    }

    [Fact]
    public void Generate_WithOnlyCustomCharacters_UsesOnlyThem()
    {
        var options = new PasswordOptions
        {
            Length = 12,
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeDigits = false,
            IncludeSymbols = false,
            CustomCharacters = "甲乙丙",
        };

        var password = PasswordGenerator.Generate(options);

        Assert.All(password, character => Assert.Contains(character, "甲乙丙"));
    }

    [Fact]
    public void Generate_CustomCharactersAreDeduplicated()
    {
        var options = new PasswordOptions
        {
            Length = 12,
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeDigits = false,
            IncludeSymbols = false,
            CustomCharacters = "★★※※",
        };

        Assert.Equal(2, PasswordGenerator.PoolSize(options));
        Assert.Equal(2, PasswordGenerator.BuildPools(options)[0].Length);
    }

    [Fact]
    public void Generate_CustomCharactersExcludeWhitespace()
    {
        var options = new PasswordOptions
        {
            Length = 12,
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeDigits = false,
            IncludeSymbols = false,
            CustomCharacters = " A\tB\u3000C ",
        };

        Assert.Equal("ABC", PasswordGenerator.BuildPools(options)[0]);

        var password = PasswordGenerator.Generate(options);
        Assert.All(password, character => Assert.Contains(character, "ABC"));
    }

    [Fact]
    public void Generate_CustomCharactersIgnoreAmbiguousFilter()
    {
        var options = new PasswordOptions { Length = 16, CustomCharacters = "IOl01", ExcludeAmbiguous = true };

        var password = PasswordGenerator.Generate(options);

        Assert.Contains(password, character => character is 'I' or 'O' or 'l' or '0' or '1');
    }

    [Fact]
    public void Generate_WithoutAnyClass_Throws()
    {
        var options = new PasswordOptions
        {
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeDigits = false,
            IncludeSymbols = false,
        };

        Assert.Throws<ArgumentException>(() => PasswordGenerator.Generate(options));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(129)]
    public void Generate_WithLengthOutOfRange_Throws(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PasswordGenerator.Generate(new PasswordOptions { Length = length }));
    }

    [Fact]
    public void Generate_WithLengthSmallerThanClassCount_Throws()
    {
        var options = new PasswordOptions { Length = 8 };
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.Generate(options with { Length = 3 }));
    }

    [Fact]
    public void Generate_IsReasonablyUniform()
    {
        var options = new PasswordOptions
        {
            Length = 16,
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeSymbols = false,
        };

        var counts = new Dictionary<char, int>();
        const int samples = 8000;

        for (var i = 0; i < samples; i++)
        {
            foreach (var character in PasswordGenerator.Generate(options))
            {
                counts[character] = counts.GetValueOrDefault(character) + 1;
            }
        }

        var expected = samples * options.Length / (double)PasswordGenerator.PoolSize(options);
        foreach (var (character, count) in counts)
        {
            Assert.True(
                Math.Abs(count - expected) < expected * 0.25,
                $"字符 {character} 出现 {count} 次，期望约 {expected:0}");
        }
    }

    [Fact]
    public void EntropyBits_IncreasesWithLengthAndPool()
    {
        var shortPassword = new PasswordOptions { Length = 8 };
        var longPassword = shortPassword with { Length = 32 };
        var digitsOnly = shortPassword with { IncludeUppercase = false, IncludeLowercase = false, IncludeSymbols = false };

        Assert.True(PasswordGenerator.EntropyBits(longPassword) > PasswordGenerator.EntropyBits(shortPassword));
        Assert.True(PasswordGenerator.EntropyBits(shortPassword) > PasswordGenerator.EntropyBits(digitsOnly));
        Assert.True(PasswordGenerator.EntropyBits(digitsOnly) > 0);
    }

    [Theory]
    [InlineData(45, "弱")]
    [InlineData(60, "中等")]
    [InlineData(80, "强")]
    [InlineData(120, "很强")]
    public void DescribeStrength_MapsEntropyToLabel(double entropy, string expected)
    {
        Assert.Equal(expected, PasswordGenerator.DescribeStrength(entropy));
    }
}
