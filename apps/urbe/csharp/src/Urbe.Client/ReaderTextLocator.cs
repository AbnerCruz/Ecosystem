using System.Text.Json;

namespace Urbe.Client;

public sealed record ReaderTextPosition(int Offset, int Length, string Exact, double Scroll);
public interface IReaderTextLocation
{
    Task<ReaderTextPosition?> ReadTextPositionAsync(bool selection);
    Task RestoreTextPositionAsync(int offset, int length, double scroll);
    Task GoToReaderAnchorAsync(string id);
    Task ApplyReaderAppearanceAsync(string theme,double margin,bool reflow,bool paged=false);
    /// <summary>CST-234: one page on (1) or back (-1), or to the last page (int.MaxValue); false at the chapter's edge.</summary>
    Task<bool> TurnPageAsync(int direction);
}

/// <summary>Fixed read-only DOM queries. Positions are relative to body text in the verified source revision.</summary>
public static class ReaderTextLocator
{
    private const string Nodes = "const root=document.body;if(!root)return null;const walk=document.createTreeWalker(root,NodeFilter.SHOW_TEXT,{acceptNode:n=>n.parentElement&& !n.parentElement.closest('script,style,noscript')?NodeFilter.FILTER_ACCEPT:NodeFilter.FILTER_REJECT});let nodes=[],n;while(n=walk.nextNode())nodes.push(n);";
    public static string ReadScript(bool selection) => "JSON.stringify((()=>{" + Nodes +
        "let sel=window.getSelection(),r=" + (selection ? "sel&&sel.rangeCount?sel.getRangeAt(0):null" : "(document.documentElement.dataset.urbePaged&&document.caretRangeFromPoint?document.caretRangeFromPoint(window.innerWidth*.15,window.innerHeight*.06):null)") + ";" +
        "if(!r){for(let n of nodes){let q=document.createRange();q.selectNodeContents(n);let b=q.getBoundingClientRect();if(b.bottom>0&&b.right>0&&b.width>0){r=q;break}}}let offset=0,length=0,exact='';if(r){let before=document.createRange();before.selectNodeContents(root);try{before.setEnd(r.startContainer,r.startOffset);const part=before.cloneContents();part.querySelectorAll('style,script,noscript').forEach(n=>n.remove());offset=part.textContent.length}catch(e){}" +
        (selection ? "exact=sel?sel.toString():'';let chosen=r.cloneContents();chosen.querySelectorAll('style,script,noscript').forEach(n=>n.remove());length=chosen.textContent.length;" : "") +
        "}return {Offset:offset,Length:length,Exact:exact,Scroll:window.scrollY/Math.max(1,document.documentElement.scrollHeight-window.innerHeight)}})())";
    public static string RestoreScript(int offset, int length, double scroll) => "(()=>{" + Nodes +
        "let at=" + Math.Max(0, offset) + ",len=" + Math.Max(0, length) + ";let begin=null,end=null;for(let n of nodes){if(!begin&&at<n.length){begin=[n,at];let left=len;for(let k=nodes.indexOf(n);k<nodes.length;k++){let p=k===nodes.indexOf(n)?at:0;if(left<=nodes[k].length-p){end=[nodes[k],p+left];break}left-=nodes[k].length-p}break}at-=n.length}" +
        "if(begin){let r=document.createRange();r.setStart(...begin);r.setEnd(...(end||begin));if(len){let sel=window.getSelection();sel.removeAllRanges();sel.addRange(r)}let element=begin[0].parentElement;if(document.documentElement.dataset.urbePaged){let c=document.createRange();c.setStart(...begin);c.collapse(true);let b=c.getClientRects()[0]||element.getBoundingClientRect(),q=document.scrollingElement,w=window.innerWidth;q.scrollLeft=Math.floor((b.left+q.scrollLeft)/w)*w}else element.scrollIntoView({block:'start'});}else{let k=" + Math.Clamp(double.IsFinite(scroll) ? scroll : 0, 0, 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",s=document.scrollingElement;if(document.documentElement.dataset.urbePaged)s.scrollLeft=k*(s.scrollWidth-window.innerWidth);else window.scrollTo(0,k*(document.documentElement.scrollHeight-window.innerHeight))}" + Snap + "return true})()";
    public static string AppearanceScript(string theme,double margin,bool reflow,bool paged=false)
    {
        string colors=theme switch{"dark"=>"background:#171b21!important;color:#e4e4e7!important;color-scheme:dark", "sepia"=>"background:#f3e7ce!important;color:#45382a!important;color-scheme:light", "light"=>"background:#fdfcf9!important;color:#272b31!important;color-scheme:light",_=>"color-scheme:light dark"};
        string side=Math.Clamp(double.IsFinite(margin)?margin:5,0,25).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string css="body{"+colors+(reflow?";margin:"+side+"%!important":"")+"}";
        // CST-234 pages: the chapter in columns one screen wide, turned by whole pages (the way web book readers do it)
        // columns exactly one screen wide with no gap, so a page is a screen; the side margins are on each block, which a
        // block keeps in every column it runs into
        if(paged&&reflow) css+="html{height:100vh;overflow:hidden}body{margin:0!important;padding:4vh 0!important;box-sizing:border-box;height:100vh;column-width:100vw;column-gap:0;column-fill:auto}body>*{margin-left:"+side+"vw!important;margin-right:"+side+"vw!important}img,svg,table,pre{max-height:88vh;break-inside:avoid}";
        bool on=paged&&reflow;
        return "(()=>{let s=document.getElementById('urbe-reader-appearance');if(!s){s=document.createElement('style');s.id='urbe-reader-appearance';document.head.appendChild(s)}s.textContent="+JsonSerializer.Serialize(css)+";"+
            (on?"document.documentElement.dataset.urbePaged='1';":"delete document.documentElement.dataset.urbePaged;document.scrollingElement.scrollLeft=0;")+"return true})()";
    }
    public static string AnchorScript(string id) => "(()=>{let e=document.getElementById(" + JsonSerializer.Serialize(id) + ");if(e)e.scrollIntoView({block:'start',inline:'start'});" + Snap + "return Boolean(e)})()";

    // in pages, whatever scrolled lands on a whole page
    private const string Snap = "if(document.documentElement.dataset.urbePaged){let w=window.innerWidth,s=document.scrollingElement;s.scrollLeft=Math.min(Math.floor((s.scrollLeft+1)/w),Math.floor((s.scrollWidth-s.clientWidth)/w+.01))*w}";

    /// <summary>The page turn: true when it moved; at the first or last page it stays and says false.</summary>
    public static string PageScript(int direction) =>
        "String((()=>{let s=document.scrollingElement,w=window.innerWidth;if(!document.documentElement.dataset.urbePaged||!s)return false;" +
        "let last=Math.max(0,Math.floor((s.scrollWidth-s.clientWidth)/w+.01)),now=Math.floor((s.scrollLeft+1)/w)," +
        "to=" + (direction == int.MaxValue ? "last" : "now+(" + Math.Sign(direction) + ")") + ";" +
        "if(to<0||to>last||to===now)return false;s.scrollLeft=to*w;return true})())";
    public static ReaderTextPosition? Parse(string json)
    {
        try { return JsonSerializer.Deserialize<ReaderTextPosition>(json); } catch (JsonException) { return null; }
    }
}
