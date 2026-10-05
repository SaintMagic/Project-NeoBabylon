using System.Text.Json;
using NeoBabylon.Core;

// Source-only checks: no WPF, private runtime, provider credentials or live calls.
var failures = new List<string>();
var count = 0;
void Check(string name, Action test)
{
    count++;
    try { test(); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures.Add(name); Console.Error.WriteLine("FAIL " + name + ": " + error.Message); }
}

AgentPolicyChecks.Run(Check);
ToolOutputChecks.Run(Check);
Console.WriteLine($"PRODUCT_PARITY_TESTS: {count - failures.Count}/{count} passed");
return failures.Count == 0 ? 0 : 1;

internal static class Fixture
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static ModelCapabilityRecord Capability(string fileName) =>
        JsonSerializer.Deserialize<ModelCapabilityRecord>(
            File.ReadAllText(Path.Combine("docs", "release", fileName)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("Capability fixture is empty.");
}
