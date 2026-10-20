using System.Security.Cryptography;
using System.Text;

namespace PwdMgr.Core.Security;

/// <summary>
/// 密码生成器。使用 <see cref="RandomNumberGenerator"/> 取随机数，无取模偏差；
/// 每个启用的字符类别至少出现一次，最后整体洗牌，避免“前几位固定属于某类”的规律。
/// </summary>
public static class PasswordGenerator
{
    public const int MinLength = 8;

    public const int MaxLength = 128;

    public const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public const string Lowercase = "abcdefghijklmnopqrstuvwxyz";

    public const string Digits = "0123456789";

    public const string Symbols = "!@#$%^&*()-_=+[]{};:,.?/";

    private const string Ambiguous = "IOl01";

    /// <summary>按选项生成一个密码。</summary>
    /// <exception cref="ArgumentOutOfRangeException">长度超出范围或小于启用的字符类别数。</exception>
    /// <exception cref="ArgumentException">没有启用任何字符类别。</exception>
    public static string Generate(PasswordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Length is < MinLength or > MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"密码长度必须在 {MinLength} 到 {MaxLength} 之间。");
        }

        var pools = BuildPools(options);
        if (pools.Count == 0)
        {
            throw new ArgumentException("至少要选择一种字符类型，或填写自定义字符。", nameof(options));
        }

        if (options.Length < pools.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"长度至少要为每种启用的字符类型各留一位，当前需要 {pools.Count} 位。");
        }

        var all = string.Concat(pools);
        var characters = new char[options.Length];

        // 每个类别先取一个，保证都能出现。
        for (var i = 0; i < pools.Count; i++)
        {
            characters[i] = pools[i][RandomNumberGenerator.GetInt32(pools[i].Length)];
        }

        for (var i = pools.Count; i < characters.Length; i++)
        {
            characters[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        Shuffle(characters);
        return new string(characters);
    }

    /// <summary>当前选项下单个字符的候选池大小（去重后，自定义字符与内置字符重叠时不重复计数）。</summary>
    public static int PoolSize(PasswordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return BuildPools(options).SelectMany(pool => pool).Distinct().Count();
    }

    /// <summary>熵值（比特）：长度 × log2(候选池大小)。</summary>
    public static double EntropyBits(PasswordOptions options)
    {
        var poolSize = PoolSize(options);
        return poolSize <= 1 ? 0 : options.Length * Math.Log2(poolSize);
    }

    /// <summary>把熵值换成一句人话，用于界面提示。</summary>
    public static string DescribeStrength(double entropyBits) => entropyBits switch
    {
        < 50 => "弱",
        < 70 => "中等",
        < 100 => "强",
        _ => "很强",
    };

    /// <summary>按选项返回启用的字符池；排除易混字符时从每个池里去掉它们。</summary>
    public static IReadOnlyList<string> BuildPools(PasswordOptions options)
    {
        var pools = new List<string>(5);

        Add(Include(options.IncludeUppercase, Uppercase));
        Add(Include(options.IncludeLowercase, Lowercase));
        Add(Include(options.IncludeDigits, Digits));
        Add(Include(options.IncludeSymbols, Symbols));
        Add(NormalizeCustom(options.CustomCharacters));

        return pools;

        void Add(string pool)
        {
            if (pool.Length > 0)
            {
                pools.Add(pool);
            }
        }

        string Include(bool enabled, string pool)
        {
            if (!enabled)
            {
                return string.Empty;
            }

            return options.ExcludeAmbiguous ? RemoveAmbiguous(pool) : pool;
        }
    }

    /// <summary>自定义字符：去掉控制字符与重复项，其余原样保留（不受“排除易混字符”影响）。</summary>
    private static string NormalizeCustom(string custom)
    {
        if (string.IsNullOrEmpty(custom))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(custom.Length);
        var seen = new HashSet<char>();

        foreach (var character in custom)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            if (seen.Add(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string RemoveAmbiguous(string pool)
    {
        var builder = new StringBuilder(pool.Length);
        foreach (var character in pool)
        {
            if (!Ambiguous.Contains(character, StringComparison.Ordinal))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static void Shuffle(char[] characters)
    {
        for (var i = characters.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (characters[i], characters[j]) = (characters[j], characters[i]);
        }
    }
}
