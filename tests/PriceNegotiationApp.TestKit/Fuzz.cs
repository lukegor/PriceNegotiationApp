using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bogus;

namespace PriceNegotiationApp.TestKit;

/// <summary>
/// Deterministic test-data generation. Faker instances are seeded from
/// (TEST_SEED, call-site), so re-running the same command line replays identical data.
/// Dump() reports every arranged value into test output, which lands in TRX artifacts.
/// </summary>
public static class Fuzz
{
    public static int RunSeed { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("TEST_SEED"), CultureInfo.InvariantCulture, out var seed)
            ? seed
            : 8675309;

    /// <summary>Attached by each test assembly's module initializer to xunit v3 output.</summary>
    public static Action<string>? Sink { get; set; }

    private static readonly ConcurrentDictionary<string, int> SiteCounters = new();
    private static int _uniqueSequence;

    public static Faker NewFaker(
        int salt = 0,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string member = "")
    {
        var site = $"{filePath}:{member}";
        var occurrence = SiteCounters.AddOrUpdate(site, 1, static (_, current) => current + 1);
        var seed = HashCode.Combine(RunSeed, site, salt, occurrence);
        Sink?.Invoke(string.Create(CultureInfo.InvariantCulture,
            $"fuzz run-seed={RunSeed} scope={member} site-occurrence={occurrence} seed={seed}"));
        return new Faker { Random = new Randomizer(seed) };
    }

    public static decimal Price(this Faker faker, decimal min = 0.01m, decimal max = 1000m) =>
        Math.Round(faker.Random.Decimal(min, max), 2);

    public static string ProductName(this Faker faker)
    {
        var name = faker.Commerce.ProductName();
        return name.Length <= 200 ? name : name[..200];
    }

    public static string Text(this Faker faker, int minLen, int maxLen) =>
        faker.Random.String2(faker.Random.Int(minLen, maxLen));

    public static string Email()
    {
        var sequence = Interlocked.Increment(ref _uniqueSequence);
        var faker = new Faker { Random = new Randomizer(HashCode.Combine(RunSeed, sequence)) };
        return faker.Internet.Email();
    }

    public static string UniqueEmail()
    {
        var sequence = Interlocked.Increment(ref _uniqueSequence);
        var local = new Faker { Random = new Randomizer(HashCode.Combine(RunSeed, sequence)) }
            .Internet.UserName().ToLowerInvariant().Replace("'", "").Replace(".", "");
        return $"{local}.f{sequence.ToString(CultureInfo.InvariantCulture)}@test.local";
    }

    public static string Password(int length = 14)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 4);

        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        var all = string.Concat(upper, lower, digits, symbols);

        var randomizer = new Randomizer(HashCode.Combine(RunSeed, Interlocked.Increment(ref _uniqueSequence)));
        var chars = new char[length];
        chars[0] = upper[randomizer.Int(0, upper.Length - 1)];
        chars[1] = lower[randomizer.Int(0, lower.Length - 1)];
        chars[2] = digits[randomizer.Int(0, digits.Length - 1)];
        chars[3] = symbols[randomizer.Int(0, symbols.Length - 1)];
        for (var i = 4; i < length; i++)
        {
            chars[i] = all[randomizer.Int(0, all.Length - 1)];
        }

        for (var i = length - 1; i > 0; i--)
        {
            var swap = randomizer.Int(0, i);
            (chars[i], chars[swap]) = (chars[swap], chars[i]);
        }

        return new string(chars);
    }

    public static string HttpsUrl()
    {
        var sequence = Interlocked.Increment(ref _uniqueSequence);
        var domain = new Faker { Random = new Randomizer(HashCode.Combine(RunSeed, sequence)) }
            .Internet.DomainName();
        return $"https://{domain}";
    }

    public static void Dump(string label, object value) =>
        Sink?.Invoke($"fuzz {label} = {JsonSerializer.Serialize(value)}");
}
