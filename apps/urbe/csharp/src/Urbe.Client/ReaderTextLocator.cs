using System.Text.Json;

namespace Urbe.Client;

public sealed record ReaderTextPosition(int Offset, int Length, string Exact, double Scroll);
public interface IReaderTextLocation
{
    Task<ReaderTextPosition?> ReadTextPositionAsync(bool selection);
    Task RestoreTextPositionAsync(int offset, int length, double scroll);
    Task GoToReaderAnchorAsync(string id);
    Task ApplyReaderAppearanceAsync(string theme,double margin,bool reflow);
}

/// <summary>Fixed read-only DOM queries. Positions are relative to body text in the verified source revision.</summary>
public static class ReaderTextLocator
{
    private const string Nodes = "const root=document.body;if(!root)return null;const walk=document.createTreeWalker(root,NodeFilter.SHOW_TEXT,{acceptNode:n=>n.parentElement&& !n.parentElement.closest('script,style,noscript')?NodeFilter.FILTER_ACCEPT:NodeFilter.FILTER_REJECT});let nodes=[],n;while(n=walk.nextNode())nodes.push(n);";
    public static string ReadScript(bool selection) => "JSON.stringify((()=>{" + Nodes +
        "let sel=window.getSelection(),r=" + (selection ? "sel&&sel.rangeCount?sel.getRangeAt(0):null" : "null") + ";" +
        "if(!r){for(let n of nodes){let q=document.createRange();q.selectNodeContents(n);let b=q.getBoundingClientRect();if(b.bottom>0&&b.width>0){r=q;break}}}let offset=0,length=0,exact='';if(r){let before=document.createRange();before.selectNodeContents(root);try{before.setEnd(r.startContainer,r.startOffset);const part=before.cloneContents();part.querySelectorAll('style,script,noscript').forEach(n=>n.remove());offset=part.textContent.length}catch(e){}" +
        (selection ? "exact=sel?sel.toString():'';let chosen=r.cloneContents();chosen.querySelectorAll('style,script,noscript').forEach(n=>n.remove());length=chosen.textContent.length;" : "") +
        "}return {Offset:offset,Length:length,Exact:exact,Scroll:window.scrollY/Math.max(1,document.documentElement.scrollHeight-window.innerHeight)}})())";
    public static string RestoreScript(int offset, int length, double scroll) => "(()=>{" + Nodes +
        "let at=" + Math.Max(0, offset) + ",len=" + Math.Max(0, length) + ";let begin=null,end=null;for(let n of nodes){if(!begin&&at<n.length){begin=[n,at];let left=len;for(let k=nodes.indexOf(n);k<nodes.length;k++){let p=k===nodes.indexOf(n)?at:0;if(left<=nodes[k].length-p){end=[nodes[k],p+left];break}left-=nodes[k].length-p}break}at-=n.length}" +
        "if(begin){let r=document.createRange();r.setStart(...begin);r.setEnd(...(end||begin));if(len){let sel=window.getSelection();sel.removeAllRanges();sel.addRange(r)}let element=begin[0].parentElement;element.scrollIntoView({block:'start'});}else window.scrollTo(0," + Math.Clamp(double.IsFinite(scroll) ? scroll : 0, 0, 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "*(document.documentElement.scrollHeight-window.innerHeight));return true})()";
    public static string AppearanceScript(string theme,double margin,bool reflow)
    {
        string colors=theme switch{"dark"=>"background:#171b21!important;color:#e4e4e7!important;color-scheme:dark", "sepia"=>"background:#f3e7ce!important;color:#45382a!important;color-scheme:light", "light"=>"background:#fdfcf9!important;color:#272b31!important;color-scheme:light",_=>"color-scheme:light dark"};
        string css="body{"+colors+(reflow?";margin:"+Math.Clamp(double.IsFinite(margin)?margin:5,0,25).ToString(System.Globalization.CultureInfo.InvariantCulture)+"%!important":"")+"}";
        return "(()=>{let s=document.getElementById('urbe-reader-appearance');if(!s){s=document.createElement('style');s.id='urbe-reader-appearance';document.head.appendChild(s)}s.textContent="+JsonSerializer.Serialize(css)+";return true})()";
    }
    public static string AnchorScript(string id) => "(()=>{let e=document.getElementById(" + JsonSerializer.Serialize(id) + ");if(e)e.scrollIntoView({block:'start'});return Boolean(e)})()";
    public static ReaderTextPosition? Parse(string json)
    {
        try { return JsonSerializer.Deserialize<ReaderTextPosition>(json); } catch (JsonException) { return null; }
    }
}
