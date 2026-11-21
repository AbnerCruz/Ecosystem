namespace Urbe.Core.Voice;

/// <summary>A natural (neural) voice the app can download and run on the device.</summary>
/// <param name="Id">Stable id: "neural:" + model + ":" + speaker name.</param>
/// <param name="Engine">"kokoro" or "piper" (VITS).</param>
/// <param name="Speaker">The speaker's number inside the model.</param>
public sealed record NeuralVoice(string Id, string Name, string Detail, string Model, string Engine, int Speaker);

/// <summary>A downloadable model: where, how big, its fingerprint and its licences.</summary>
public sealed record NeuralVoiceModel(string Id, string Url, long Bytes, string Sha256, string Folder, string Licences);

/// <summary>
/// #1004 / DEC-0067 (alternative A, chosen by the owner): natural Brazilian Portuguese voices that run on the device
/// after one download. The models are sherpa-onnx's packages of Kokoro-82M (Apache-2.0) and Piper pt_BR faber (voice
/// data CC0); both carry espeak-ng (GPL-3.0) to turn Portuguese text into phonemes. Nothing is in the app until the
/// person chooses a voice; the download is checked against the size and SHA-256 fixed here.
/// </summary>
public static class NeuralVoices
{
    public static readonly IReadOnlyList<NeuralVoiceModel> Models =
    [
        new("kokoro", "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-int8-multi-lang-v1_0.tar.bz2",
            132_353_410, "72c347230107ef8a31098ec4d468a605244beef5f1ed00db97efa13518800653", "kokoro-int8-multi-lang-v1_0",
            "Kokoro-82M: Apache-2.0. espeak-ng (fonemas): GPL-3.0, código-fonte correspondente (versão usada pelo sherpa-onnx 1.13.8, commit ed530aa113046142eb5115cf2fc9157854d0ffe1, zip SHA-256 e4e262cbe34f7fe21f91f1ba3397f2728e1f30eafbae7853f2b753a9ed13f0dd) em https://github.com/csukuangfj/espeak-ng/archive/ed530aa113046142eb5115cf2fc9157854d0ffe1.zip; projeto original https://github.com/espeak-ng/espeak-ng. sherpa-onnx: Apache-2.0."),
        new("piper-faber", "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-pt_BR-faber-medium.tar.bz2",
            67_183_065, "7add3f923ad6bc25ca8a192805fd1a64d1b3893e4611c4a9719545a825039a83", "vits-piper-pt_BR-faber-medium",
            "Voz faber (Piper): dados CC0. espeak-ng (fonemas): GPL-3.0, código-fonte correspondente (versão usada pelo sherpa-onnx 1.13.8, commit ed530aa113046142eb5115cf2fc9157854d0ffe1, zip SHA-256 e4e262cbe34f7fe21f91f1ba3397f2728e1f30eafbae7853f2b753a9ed13f0dd) em https://github.com/csukuangfj/espeak-ng/archive/ed530aa113046142eb5115cf2fc9157854d0ffe1.zip; projeto original https://github.com/espeak-ng/espeak-ng. sherpa-onnx: Apache-2.0."),
    ];

    public static readonly IReadOnlyList<NeuralVoice> All =
    [
        new("neural:kokoro:pf_dora", "Dora (natural)", "Voz feminina, Kokoro · baixa 132 MB uma vez", "kokoro", "kokoro", 42),
        new("neural:kokoro:pm_alex", "Alex (natural)", "Voz masculina, Kokoro · baixa 132 MB uma vez", "kokoro", "kokoro", 43),
        new("neural:piper-faber:faber", "Faber (natural, leve)", "Voz masculina, Piper · baixa 67 MB uma vez, mais rápida", "piper-faber", "piper", 0),
    ];

    public static NeuralVoice? Find(string? id) => All.FirstOrDefault(v => v.Id == id);
    public static NeuralVoiceModel ModelOf(NeuralVoice voice) => Models.First(m => m.Id == voice.Model);
    public static bool IsNeural(string? id) => id?.StartsWith("neural:", StringComparison.Ordinal) == true;
}
