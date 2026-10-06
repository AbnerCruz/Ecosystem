using CSharpMath.VectSharp;
using VectSharp.SVG;
using System.Text.Json;
using System.Xml.Linq;
using System.Text;
var corpus=JsonDocument.Parse(File.ReadAllText(args[0]));
var formulas=corpus.RootElement.GetProperty("snippets").EnumerateArray().Select(x=>x.GetProperty("expected").GetProperty("text").GetString()!).Concat(new[]{@"\frac{-b\pm\sqrt{b^2-4ac}}{2a}",@"\int_0^1 x^2\,dx=\frac{1}{3}",@"\begin{pmatrix}a&b\\c&d\end{pmatrix}",@"\newcommand{\foo}{x}\foo",@"\def\foo{x}\foo",@"\doesnotexist",@"\frac{"});
if(args.Length>2)formulas=formulas.Concat(JsonDocument.Parse(File.ReadAllText(args[2])).RootElement.EnumerateArray().Select(x=>x.GetProperty("tex").GetString()!));
var results=new List<object>();int index=0;
foreach(var tex in formulas){try{var painter=new MathPainter{LaTeX=tex};if(painter.ErrorMessage is not null){results.Add(new{index=index++,tex,status="diagnostic",message=painter.ErrorMessage});continue;}using var output=new MemoryStream();painter.DrawToPage().SaveAsSVG(output,SVGContextInterpreter.TextOptions.ConvertIntoPaths);var bytes=output.ToArray();File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(args[1])!, $"formula-{index:D3}.svg"),bytes);var svg=XDocument.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));var paths=svg.Descendants().Count(n=>n.Name.LocalName=="path");var external=svg.Descendants().Attributes().Where(a=>a.Name.LocalName=="href"&&(!a.Value.StartsWith('#'))).Select(a=>a.Value).ToArray();results.Add(new{index=index++,tex,status="rendered",paths,bytes=bytes.Length,external,sha256=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))});}catch(Exception e){results.Add(new{index=index++,tex,status="exception",message=e.Message});}}
File.WriteAllText(args[1],JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{index} actual candidate renderer inputs executed");
