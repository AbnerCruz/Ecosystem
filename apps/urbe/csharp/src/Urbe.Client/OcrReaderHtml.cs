using System.Security.Cryptography;
using System.Text.Json;

namespace Urbe.Client;

/// <summary>Offline, read-only OCR content. Bundled worker, WASM and models; no host bridge or network.</summary>
public static class OcrReaderHtml
{
    public const string ResultScript = "JSON.stringify(window.urbeOcrResult||null)";
    private static string Resource(string name)
    {
        using var stream = Urbe.Readers.Assets.ReaderAssets.Open("reader/ocr/" + name);
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
    private static string Model(string name)
    {
        using var stream = Urbe.Readers.Assets.ReaderAssets.Open("reader/ocr/" + name + ".traineddata.gz");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes); return Convert.ToBase64String(bytes.ToArray());
    }
    public static string Build(byte[] png,string language="por+eng")
    {
        if(language is not ("por+eng" or "por" or "eng"))throw new ArgumentException("Idioma de OCR não empacotado.",nameof(language));
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        string main = Resource("tesseract.min.js").Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
        var payload = JsonSerializer.Serialize(new { worker = Resource("worker.min.js"), core = Resource("core.wasm.js"),
            eng = Model("eng"), por = Model("por"), language, image = "data:image/png;base64," + Convert.ToBase64String(png) });
        return "<!doctype html><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>" +
            "<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; script-src 'nonce-" + nonce + "' blob: 'wasm-unsafe-eval'; worker-src blob:; connect-src blob: data:; img-src data:; style-src 'unsafe-inline'; object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'\">" +
            "<style>html,body{margin:0;background:#eee}img{width:100%;object-fit:contain}</style><img id='source' alt='Região selecionada para reconhecimento'>" +
            "<script nonce='" + nonce + "'>" + main + "</script><script nonce='" + nonce + "'>const payload=" + payload + ";" + Script + "</script>";
    }
    private const string Script = """
    (async()=>{
      let worker;const urls=[];
      const blob=(text)=>{let u=URL.createObjectURL(new Blob([text],{type:'application/javascript'}));urls.push(u);return u};
      window.urbeOcrResult={status:'running',progress:0};
      document.getElementById('source').src=payload.image;
      const prefix='const models='+JSON.stringify({eng:payload.eng,por:payload.por})+';globalThis.fetch=async(url)=>{const names={"https://urbe.reader.invalid/models/eng.traineddata.gz":"eng","https://urbe.reader.invalid/models/por.traineddata.gz":"por"};const name=names[String(url)];if(!name)throw Error("OCR network denied");return new Response(Uint8Array.from(atob(models[name]),c=>c.charCodeAt(0)))};';
      try{
        worker=await Tesseract.createWorker(payload.language,1,{workerPath:blob(prefix+payload.worker),workerBlobURL:false,corePath:blob(payload.core)+'#core.js',langPath:'https://urbe.reader.invalid/models',cacheMethod:'none',logger:m=>{window.urbeOcrResult={status:'running',phase:m.status,progress:m.progress||0}}});
        const {data}=await worker.recognize(payload.image,{}, {text:true,blocks:true});
        const words=[];for(const b of data.blocks||[])for(const p of b.paragraphs||[])for(const l of p.lines||[])for(const w of l.words||[])words.push({text:w.text,confidence:w.confidence,bbox:w.bbox});
        window.urbeOcrResult={status:'done',text:data.text,confidence:data.confidence,words};
      }catch(e){window.urbeOcrResult={status:'error',error:String(e)}}
      finally{if(worker)await worker.terminate();for(const u of urls)URL.revokeObjectURL(u)}
    })();
    """;
}
