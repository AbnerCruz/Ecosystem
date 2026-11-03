using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

public interface IReaderMediaPosition
{
    Task<double?> ReadMediaSecondAsync();
    Task SeekMediaAsync(double seconds);
}

/// <summary>Native study actions around the platform player. Quotes identify a time in a verified local source.</summary>
public sealed class MediaStudyView : Grid
{
    private readonly Control _player;
    private readonly TextBox _time = new() { Width=90,Text="0",MinHeight=48 };
    private readonly TextBlock _status = new() { TextWrapping=Avalonia.Media.TextWrapping.Wrap };
    private bool _closed;
    private readonly DispatcherTimer _positionTimer=new(){Interval=TimeSpan.FromSeconds(2)};
    public int CurrentSecond {get;private set;}
    public event Action<int>? PositionChanged;
    public event Action<BookReaderQuote>? QuoteRequested;
    public MediaStudyView(string path,IUrbeVaultStorage storage,Control player,int? initialSecond=null)
    {
        _player=player; CurrentSecond=initialSecond??0; RowDefinitions=new RowDefinitions("*,Auto");Children.Add(player);
        var seek=new Ui.UrbeButton("Ir ao instante");seek.Click+=async(_,_)=>
        { if(player is IReaderMediaPosition media&&int.TryParse(_time.Text,out int second)&&second>=0)await media.SeekMediaAsync(second); };
        var mark=new Ui.UrbeButton("Anotar instante");mark.Click+=async(_,_)=>
        {
            mark.IsEnabled=false;
            try
            {
                if(player is not IReaderMediaPosition media||await media.ReadMediaSecondAsync() is not { } current){_status.Text="Inicie a reprodução antes de marcar o instante.";return;}
                _status.Text="Verificando a versão da mídia local…";
                string? hash=await storage.ReaderRevisionAsync(path);
                if(_closed)return;
                if(hash is null){_status.Text="Não foi possível verificar a versão deste arquivo.";return;}
                int second=(int)Math.Clamp(Math.Floor(current),0,int.MaxValue);_time.Text=second.ToString(System.Globalization.CultureInfo.InvariantCulture);
                QuoteRequested?.Invoke(new(path,"time",second,"Mídia no instante "+TimeSpan.FromSeconds(second).ToString(),hash,Kind:StudyAnnotationKind.Bookmark));
                _status.Text="Referência preparada no instante "+second+" s.";
            }
            catch(Exception ex)when(ex is not OutOfMemoryException){_status.Text="Não foi possível marcar esta mídia: "+ex.Message;}
            finally{mark.IsEnabled=true;}
        };
        var footer=new StackPanel{Children={_status,new WrapPanel{Children={_time,seek,mark}}}};SetRow(footer,1);Children.Add(footer);
        if(initialSecond is { } start)
        {
            _time.Text=start.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var retry=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};int attempts=0;
            retry.Tick+=async(_,_)=>{if(_closed||attempts++>30){retry.Stop();return;}if(player is IReaderMediaPosition media&&await media.ReadMediaSecondAsync() is not null){await media.SeekMediaAsync(start);retry.Stop();}};
            Loaded+=(_,_)=>retry.Start();DetachedFromVisualTree+=(_,_)=>retry.Stop();
        }
        _positionTimer.Tick+=async(_,_)=>
        {
            if(player is not IReaderMediaPosition media)return;
            try{if(await media.ReadMediaSecondAsync() is { } second && !_closed){CurrentSecond=(int)Math.Clamp(Math.Floor(second),0,int.MaxValue-1);PositionChanged?.Invoke(CurrentSecond);}}
            catch(Exception ex)when(ex is TimeoutException or InvalidOperationException or ObjectDisposedException){ }
        };
        Loaded+=(_,_)=>_positionTimer.Start();
        DetachedFromVisualTree+=(_,_)=>{_closed=true;_positionTimer.Stop();ReaderActivity.SetActive(player,false);};
        player.HorizontalAlignment=HorizontalAlignment.Stretch;player.VerticalAlignment=VerticalAlignment.Stretch;
    }
}
