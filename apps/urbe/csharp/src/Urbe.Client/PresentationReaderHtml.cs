using System.Security.Cryptography;
using System.Text.Json;

namespace Urbe.Client;

/// <summary>Trusted content renderer only. Its nonce-bound script receives one validated read-only package and has no host bridge.</summary>
public static class PresentationReaderHtml
{
    private static readonly Lazy<string> Engine = new(() =>
    {
        using var stream = typeof(PresentationReaderHtml).Assembly.GetManifestResourceStream("reader/presentation.js") ?? throw new InvalidOperationException("Motor de slides não empacotado.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
    });
    public static string Build(ReadOnlyMemory<byte> bytes, int index)
    {
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        string payload = JsonSerializer.Serialize(Convert.ToBase64String(bytes.Span));
        return "<!doctype html><html><head><meta charset='utf-8'/><meta name='viewport' content='width=device-width,initial-scale=1'/>" +
            "<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'nonce-" + nonce + "'; style-src 'unsafe-inline'; img-src data: blob:; font-src data: blob:; connect-src blob:; object-src 'none'; frame-src 'none'; form-action 'none'; base-uri 'none'\"/>" +
            "<style>html,body{margin:0;background:#eee}#slide{width:100%;overflow:auto}#slide>svg{display:block;width:100%;height:auto;user-select:text}#status{font:14px sans-serif;padding:12px}</style></head><body><div id='status'>Renderizando apresentação…</div><div id='slide'></div>" +
            "<script nonce='" + nonce + "'>" + Engine.Value + "</script><script nonce='" + nonce + "'>" +
            "(async()=>{let deck=null,run=0;window.urbeSlideIndex=" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";" +
            "window.urbeRenderSlide=async(i)=>{if(!Number.isInteger(i)||i<0)return;window.urbeSlideIndex=i;if(!deck||i>=deck.slides.length)return;let token=++run;try{let svg=await UrbePresentation.slideToSvgFile(deck,deck.slides[i]);if(token!==run)return;document.getElementById('slide').innerHTML=svg;document.getElementById('status').textContent='';window.scrollTo(0,0)}catch(e){document.getElementById('status').textContent='Este slide não pôde ser renderizado. Use Texto ou confira no aplicativo de origem.'}};" +
            "try{let binary=atob(" + payload + "),data=new Uint8Array(binary.length);for(let i=0;i<binary.length;i++)data[i]=binary.charCodeAt(i);deck=await UrbePresentation.parse(data);await window.urbeRenderSlide(window.urbeSlideIndex)}catch(e){document.getElementById('status').textContent='Apresentação incompatível ou protegida. O original foi preservado.'}})();</script></body></html>";
    }
    public static string ShowPartScript(int index) => "window.urbeSlideIndex=" + Math.Max(0, index).ToString(System.Globalization.CultureInfo.InvariantCulture) + ";if(window.urbeRenderSlide)window.urbeRenderSlide(window.urbeSlideIndex);";
}
