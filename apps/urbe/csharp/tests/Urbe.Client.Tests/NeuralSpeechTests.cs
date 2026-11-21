using System.Net;
using Urbe.Client;
using Urbe.Core.Voice;

namespace Urbe.Client.Tests;

/// <summary>#1004 / DEC-0067 A: a natural voice downloaded once, checked, unpacked privately and speaking Portuguese.</summary>
public sealed class NeuralSpeechTests
{
    // the real model file, when this machine has it (set URBE_TTS_ARCHIVES to the folder holding it)
    private static string? Archive(string name) =>
        Environment.GetEnvironmentVariable("URBE_TTS_ARCHIVES") is { Length: > 0 } dir && File.Exists(Path.Combine(dir, name)) ? Path.Combine(dir, name) : null;

    private sealed class FileServer(string path) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead(path)) });
        }
    }

    [Fact]
    public async Task ANaturalVoiceIsInstalledCheckedAndSpeaksPortuguese()
    {
        var model = NeuralVoices.Models.Single(m => m.Id == "piper-faber");
        if (Archive("vits-piper-pt_BR-faber-medium.tar.bz2") is not { } file) { Assert.Skip("modelo Piper ausente nesta máquina (URBE_TTS_ARCHIVES)"); return; }
        var root = Directory.CreateTempSubdirectory("urbe-vozes-").FullName;
        try
        {
            var server = new FileServer(file);
            var speech = new NeuralSpeech(root, () => new HttpClient(server));
            Assert.False(speech.IsInstalled(model));
            var steps = new List<double>();
            await speech.InstallAsync(model, new Progress<double>(steps.Add), TestContext.Current.CancellationToken);
            Assert.True(speech.IsInstalled(model));
            Assert.Contains("GPL-3.0", File.ReadAllText(Path.Combine(speech.FolderOf(model), "LICENCAS-NUMA.txt")));
            Assert.Empty(Directory.GetFiles(root, "*.baixando"));
            var wav = Path.Combine(root, "fala.wav");
            Assert.True(await speech.SpeakAsync(NeuralVoices.Find("neural:piper-faber:faber")!, "Em 1905, Einstein publicou quatro artigos.", wav, TestContext.Current.CancellationToken));
            Assert.True(new FileInfo(wav).Length > 22050 * 2);   // more than a second of speech
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task AFileThatDoesNotMatchIsRefusedAndNothingIsKept()
    {
        var root = Directory.CreateTempSubdirectory("urbe-vozes-").FullName;
        var fake = Path.Combine(root, "falso.bin");
        File.WriteAllBytes(fake, new byte[1000]);
        try
        {
            var model = NeuralVoices.Models[1] with { Bytes = 1000 };
            var speech = new NeuralSpeech(Path.Combine(root, "vozes"), () => new HttpClient(new FileServer(fake)));
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => speech.InstallAsync(model, null, TestContext.Current.CancellationToken));
            Assert.Contains("SHA-256", error.Message);
            Assert.False(speech.IsInstalled(model));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "vozes")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void APackageWithAPathOutsideItsFolderIsRefused()
    {
        if (Archive("evil.tar.bz2") is not { } evil) { Assert.Skip("pacote de teste ausente (URBE_TTS_ARCHIVES)"); return; }
        var root = Directory.CreateTempSubdirectory("urbe-vozes-").FullName;
        try
        {
            Assert.Throws<InvalidDataException>(() => NeuralSpeech.Unpack(evil, Path.Combine(root, "dentro"), TestContext.Current.CancellationToken));
            Assert.False(File.Exists(Path.Combine(root, "fora.txt")));
        }
        finally { Directory.Delete(root, true); }
    }
}
