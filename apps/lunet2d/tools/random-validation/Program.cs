using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using Lunet;

if (args.Length > 0 && args[0] == "stream")
{
    if (args.Length != 3 || !ulong.TryParse(args[1], out var seed) || args[2] is not ("raw" or "reverse" or "upper" or "lower"))
        throw new ArgumentException("Uso: stream SEED raw|reverse|upper|lower");
    var rng = new RandomSource(seed);
    var data = new byte[65536];
    using var output = Console.OpenStandardOutput();
    try
    {
        while (true)
        {
            switch (args[2])
            {
                case "raw":
                    rng.NextBytes(data);
                    break;
                case "reverse":
                    for (int i = 0; i < data.Length; i += 8)
                        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(i), Reverse(rng.NextUInt64()));
                    break;
                default:
                    var upper = args[2] == "upper";
                    for (int i = 0; i < data.Length; i += 4)
                    {
                        var word = rng.NextUInt64();
                        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i), upper ? (uint)(word >> 32) : unchecked((uint)word));
                    }
                    break;
            }
            output.Write(data);
        }
    }
    catch (IOException) { }
    return;
}
if (args is ["check"])
{
    var probe = new RandomSource(42);
    if (probe.NextUInt64() != 0x15780b2e0c2ec716UL ||
        new RandomField(ulong.MaxValue).UInt64At(-1, -1, uint.MaxValue, uint.MaxValue) != 0x41c83b0e408f276dUL ||
        probe.NextColor().A != 255 || probe.NextInt64(long.MinValue, long.MaxValue) == long.MaxValue)
        throw new InvalidOperationException("Os vetores divergem.");
    probe.NextBytes(new byte[9]);
    Console.WriteLine("Vetores e aritmética checked conferidos.");
    return;
}

var random = new RandomSource(42);
var system = new System.Random();
var field = new RandomField(42);
var bytes8 = new byte[8];
var bytes256 = new byte[256];
double[] weights = [70, 25, 5];
var list = Enumerable.Range(0, 32).ToArray();
ulong old = 42, pcg = 42;

Measure("Xoshiro UInt64", _ => random.NextUInt64());
Measure("Xorshift64* UInt64", _ => Legacy());
Measure("PCG32 x2", _ => ((ulong)Pcg() << 32) | Pcg());
Measure("System.Random 8 bytes", _ => { system.NextBytes(bytes8); return BinaryPrimitives.ReadUInt64LittleEndian(bytes8); });
Measure("NextInt 6", _ => (ulong)random.NextInt(6));
Measure("NextInt64 2^62+1", _ => (ulong)random.NextInt64((1L << 62) + 1));
Measure("NextFloat", _ => BitConverter.SingleToUInt32Bits(random.NextFloat()));
Measure("NextDirection", _ => BitConverter.SingleToUInt32Bits(random.NextDirection().X));
Measure("NextBytes 256", _ => { random.NextBytes(bytes256); return bytes256[0]; }, 100_000);
Measure("Shuffle 32", _ => { random.Shuffle(list); return (ulong)list[0]; }, 100_000);
Measure("Sample 6 of 32", _ => (ulong)random.Sample(list, 6)[0], 30_000);
Measure("Weighted 3", _ => (ulong)random.NextWeightedIndex(weights));
Measure("Fork string", _ => random.Fork("loot").Seed, 30_000);
Measure("Philox UInt64At", x => field.UInt64At(x, -x));
Measure("Construct explicit", x => new RandomSource((ulong)x).Seed, 50_000);
Measure("Construct automatic", _ => new RandomSource().Seed, 30_000);

ulong Legacy()
{
    old ^= old >> 12;
    old ^= old << 25;
    old ^= old >> 27;
    return unchecked(old * 0x2545f4914f6cdd1dUL);
}

uint Pcg()
{
    unchecked
    {
        var state = pcg;
        pcg = state * 6364136223846793005UL + 1442695040888963407UL;
        return BitOperations.RotateRight((uint)(((state >> 18) ^ state) >> 27), (int)(state >> 59));
    }
}

static void Measure(string name, Func<int, ulong> run, int count = 600_000)
{
    ulong accumulator = 0;
    for (int i = 0; i < count; i++) accumulator ^= run(i);
    var times = new double[7];
    long allocation = 0;
    for (int round = 0; round < times.Length; round++)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        for (int i = 0; i < count; i++) accumulator ^= run(i);
        times[round] = Stopwatch.GetElapsedTime(start).TotalNanoseconds / count;
        allocation += GC.GetAllocatedBytesForCurrentThread() - allocated;
    }
    Array.Sort(times);
    Console.WriteLine($"{name,-22} median={times[3]:F2} ns/op IQR={times[1]:F2}–{times[5]:F2} bytes/op={(double)allocation / (times.Length * count):F2} sink={accumulator:X16}");
}

static ulong Reverse(ulong word)
{
    word = (word >> 1 & 0x5555555555555555) | ((word & 0x5555555555555555) << 1);
    word = (word >> 2 & 0x3333333333333333) | ((word & 0x3333333333333333) << 2);
    word = (word >> 4 & 0x0f0f0f0f0f0f0f0f) | ((word & 0x0f0f0f0f0f0f0f0f) << 4);
    return BinaryPrimitives.ReverseEndianness(word);
}
