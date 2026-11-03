using UglyToad.PdfPig;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Outline;
namespace Urbe.Core.Assistant;

public sealed record DocumentOutlineEntry(string Title, int Page, int Level);
public sealed record DocumentNavigationLink(string Text, int? Page, string? Url);

/// <summary>Navigation projection of the same PDF parser used by AssistantDocumentTools; never executes PDF actions.</summary>
public static class DocumentNavigation
{
    public static IReadOnlyList<DocumentOutlineEntry> Outline(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        var result = new List<DocumentOutlineEntry>();
        if (!pdf.TryGetBookmarks(out var bookmarks)) return result;
        var pending = new Stack<BookmarkNode>(bookmarks.Roots.Reverse());
        while (pending.TryPop(out var node))
        {
            if (node is DocumentBookmarkNode item && item.PageNumber >= 1 && item.PageNumber <= pdf.NumberOfPages)
                result.Add(new(item.Title,item.PageNumber,item.Level));
            foreach (var child in node.Children.Reverse()) pending.Push(child);
        }
        return result;
    }
    public static IReadOnlyList<DocumentNavigationLink> Links(byte[] bytes, int page)
    {
        using var pdf = PdfDocument.Open(bytes);
        return pdf.GetPage(page).GetHyperlinks().Select(link => link.Annotation.Action is GoToAction go
            ? new DocumentNavigationLink(link.Text,go.Destination.PageNumber,null)
            : new DocumentNavigationLink(link.Text,null,ReaderUris.Web(link.Uri)?.AbsoluteUri))
            .Where(link=>link.Page is not null || link.Url is not null).ToArray();
    }
}

public sealed record DocumentWord(string Text,ReaderRegion Bounds);
public static class DocumentWordGeometry
{
    public static IReadOnlyList<DocumentWord> Read(byte[] bytes,int page)
    {
        using var document=PdfDocument.Open(bytes);var source=document.GetPage(page);
        return source.GetWords().Select(word=>
        {
            var box=word.BoundingBox;
            double x=Math.Clamp(box.Left/source.Width,0,1),y=Math.Clamp(1-box.Top/source.Height,0,1);
            return new DocumentWord(word.Text,new ReaderRegion(x,y,Math.Clamp(box.Width/source.Width,0,1-x),Math.Clamp(box.Height/source.Height,0,1-y)));
        }).Where(word=>!string.IsNullOrWhiteSpace(word.Text)&&word.Bounds.Width>0&&word.Bounds.Height>0).ToArray();
    }
}
