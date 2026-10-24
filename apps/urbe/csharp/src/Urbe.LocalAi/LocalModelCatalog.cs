using Urbe.Core.Assistant;

namespace Urbe.LocalAi;

/// <summary>
/// The models that can be kept on the device. Google's own quantization-aware (QAT) Q4_0 files of Gemma 4 (Apache-2.0,
/// not gated): the best answers of their size, with the tool calls the Assistente needs. Size and SHA-256 are those of
/// the files on Hugging Face (checked 24/10/2026); a file that does not match is never used.
/// </summary>
public static class LocalModelCatalog
{
    public static LocalModel E2B { get; } = new("gemma4-e2b", "Gemma 4 E2B", "Equilibrado: responde bem e cabe na maioria dos celulares atuais.",
        "gemma-4-E2B_q4_0-it.gguf", "https://huggingface.co/google/gemma-4-E2B-it-qat-q4_0-gguf/resolve/main/gemma-4-E2B_q4_0-it.gguf",
        3_349_516_256, "fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634", 16384, 5_500, true);

    public static LocalModel E4B { get; } = new("gemma4-e4b", "Gemma 4 E4B", "O melhor: respostas mais precisas, pede mais memória (celulares com 10 GB ou mais, ou computador).",
        "gemma-4-E4B_q4_0-it.gguf", "https://huggingface.co/google/gemma-4-E4B-it-qat-q4_0-gguf/resolve/main/gemma-4-E4B_q4_0-it.gguf",
        5_154_941_280, "676c35070db6dbe52f93e9c864ee0fba4eddea94b9c875d9cb10daff453fbaee", 16384, 9_500, true);

    /// <summary>Best first.</summary>
    public static IReadOnlyList<LocalModel> All { get; } = [E4B, E2B];

    /// <summary>The best model that the memory of this device allows (the smaller one when it is not known or low).</summary>
    public static LocalModel For(long totalRamBytes)
    {
        var mb = totalRamBytes / (1024 * 1024);
        return mb >= E4B.MinRamMb ? E4B : E2B;
    }
}
