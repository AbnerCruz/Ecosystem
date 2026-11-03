using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>Native touch/mouse selection over the original page raster. Text is projected only from source PDF words.</summary>
public sealed class PdfWordSelection:Control
{
    private IReadOnlyList<DocumentWord> _words=[];
    private int _anchor=-1,_end=-1;
    private bool _dragging;
    public void SetWords(IReadOnlyList<DocumentWord> words){_words=words;_anchor=_end=-1;InvalidateVisual();}
    public (string Text,ReaderRegion Region)? Selection
    {
        get
        {
            if(_anchor<0||_end<0)return null;
            var chosen=_words.Skip(Math.Min(_anchor,_end)).Take(Math.Abs(_anchor-_end)+1).ToArray();
            var text=new StringBuilder();DocumentWord? previous=null;
            foreach(var word in chosen)
            {
                if(previous is not null)text.Append(Math.Abs(previous.Bounds.Y-word.Bounds.Y)>Math.Min(previous.Bounds.Height,word.Bounds.Height)*.6?'\n':' ');
                text.Append(word.Text);previous=word;
            }
            double x=chosen.Min(w=>w.Bounds.X),y=chosen.Min(w=>w.Bounds.Y),right=chosen.Max(w=>w.Bounds.X+w.Bounds.Width),bottom=chosen.Max(w=>w.Bounds.Y+w.Bounds.Height);
            return(text.ToString(),new ReaderRegion(x,y,right-x,bottom-y));
        }
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);context.FillRectangle(Brushes.Transparent,new Rect(Bounds.Size));
        if(_anchor<0||_end<0)return;
        for(int i=Math.Min(_anchor,_end);i<=Math.Max(_anchor,_end);i++)
        {
            var r=_words[i].Bounds;context.FillRectangle(UrbeTheme.Brush(UrbeTheme.Accent,.32),new Rect(r.X*Bounds.Width,r.Y*Bounds.Height,r.Width*Bounds.Width,r.Height*Bounds.Height));
        }
    }
    public void SelectAt(Point point,bool start)
    {
        if(_words.Count==0||Bounds.Width<=0||Bounds.Height<=0)return;
        double x=point.X/Bounds.Width,y=point.Y/Bounds.Height;
        int nearest=Enumerable.Range(0,_words.Count).MinBy(i=>
        {
            var b=_words[i].Bounds;
            double dx=Math.Max(0,Math.Max(b.X-x,x-b.X-b.Width)),dy=Math.Max(0,Math.Max(b.Y-y,y-b.Y-b.Height));
            return dx*dx+dy*dy;
        });
        if(start)_anchor=nearest;_end=nearest;InvalidateVisual();
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {base.OnPointerPressed(e);_dragging=true;SelectAt(e.GetPosition(this),true);e.Pointer.Capture(this);e.Handled=true;}
    protected override void OnPointerMoved(PointerEventArgs e)
    {base.OnPointerMoved(e);if(!_dragging)return;SelectAt(e.GetPosition(this),false);e.Handled=true;}
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {base.OnPointerReleased(e);_dragging=false;e.Pointer.Capture(null);e.Handled=true;}
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e){base.OnPointerCaptureLost(e);_dragging=false;}
}
