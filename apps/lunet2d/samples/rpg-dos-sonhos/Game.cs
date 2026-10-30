using System;
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.Input;

public sealed class RpgSonhosGame : Game
{
    public DreamWorld World { get; private set; } = new DreamWorld(20261030);
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private readonly VirtualStick stick = new VirtualStick(new Vector2(100,700),55) { RequireFreshPress=true, RescaleDeadZone=true };
    private readonly VirtualButton attack = Button(396,705,48);
    private readonly VirtualButton interact = Button(394,596,42);
    private readonly VirtualButton eat = Button(272,721,35);
    private readonly VirtualButton pause = Button(433,38,28);
    private bool saveBlocked;
    private string saveStatus="";
    private float autosave;
    private const string Key="rpg-slice-v1";
    private static VirtualButton Button(float x,float y,float r) => new VirtualButton(new Circle(new Vector2(x,y),r)) { RequireFreshPress=true };

    protected override void Initialize() { Configuration.VirtualWidth=480; Configuration.VirtualHeight=800; }
    protected override void LoadContent()
    {
        batch=new SpriteBatch(GraphicsDevice); font=SpriteFont.CreateDefault(GraphicsDevice);
        bool exists=Save.Exists(Key);
        var state=Save.Load<DreamState?>(Key,null);
        if(state!=null)
        {
            var candidate=new DreamWorld(state.Seed);
            if(candidate.Restore(state)) World=candidate;
            else saveBlocked=true;
        }
        else if(exists) saveBlocked=true;
        if(saveBlocked) World.Message="Save inválido preservado. Sessão temporária; salvamento bloqueado.";
        World.Event+=message=>Log.Info("RPG: "+message);
        Log.Info("RPG:seed="+World.State.Seed+"; slice=1; offline");
    }
    private void SaveNow()
    {
        if(saveBlocked) { saveStatus="Save inválido preservado; sessão temporária"; return; }
        try
        {
            Save.Save(Key,World.State);
            var check=Save.Load<DreamState?>(Key,null);
            saveStatus=check!=null && check.Seed==World.State.Seed && check.X==World.State.X && check.Y==World.State.Y
                ? "Estado gravado" : "Falha ao conferir save";
            Log.Info("RPG: "+saveStatus);
        }
        catch(Exception e) { saveStatus="Falha ao salvar"; Log.Error("RPG:save: "+e.Message); }
    }
    private void CancelControls() { stick.Cancel(); attack.Cancel(); interact.Cancel(); eat.Cancel(); pause.Cancel(); }
    protected override void OnPause() { World.Paused=true; CancelControls(); SaveNow(); }
    protected override void OnResume() { CancelControls(); World.Paused=true; }
    protected override void UnloadContent() { SaveNow(); font?.Dispose(); }
    protected override void Update(GameTime time)
    {
        pause.Update(Input);
        if(pause.WasPressed)
        {
            World.Paused=!World.Paused;
            stick.Cancel(); attack.Cancel(); interact.Cancel(); eat.Cancel();
            if(World.Paused) SaveNow();
        }
        if(World.Paused) return;
        stick.Update(Input); attack.Update(Input,stick.TouchId);
        interact.Update(Input,stick.TouchId); eat.Update(Input,stick.TouchId);
        World.Step(time.DeltaSeconds,stick.Direction,attack.WasPressed,interact.WasPressed,eat.WasPressed);
        autosave+=time.DeltaSeconds;
        if(autosave>=10) { autosave=0; SaveNow(); }
    }

    private Vector2 Camera => new Vector2(Math.Clamp(World.State.X-240,0,288),Math.Clamp(World.State.Y-240,0,288));
    private Vector2 Screen(Vector2 p) => p-Camera+new Vector2(0,94);
    private void Box(float x,float y,float w,float h,uint color) => batch.FillRect(new RectangleF(x,y,w,h),Color.FromHex(color));
    private void Text(string text,float x,float y,uint color=0xeee6cd,float scale=1.5f) => batch.DrawString(font,text,new Vector2(x,y),Color.FromHex(color),scale);
    private void Person(Vector2 p,uint color,string label)
    {
        p=Screen(p); Box(p.X-9,p.Y-8,18,22,color); Box(p.X-6,p.Y-16,12,10,0xf1c998);
        Text(label,p.X-14,p.Y-30,0xeee6cd,1);
    }
    private void Control(VirtualButton button,string text,uint color)
    {
        var a=button.Area;
        batch.Circle(a.Center,a.Radius,Color.FromHex(button.IsDown?0xffffff:color),3);
        Text(text,a.Center.X-text.Length*4.5f,a.Center.Y-6,0xeee6cd,1.5f);
    }
    private void MessageLines(string message)
    {
        var words=message.Split(' '); string line=""; int row=0;
        foreach(var word in words)
        {
            if(line.Length+word.Length>48) { Text(line,14,512+row*18,0xeee6cd,1.5f); line=""; row++; }
            line+=word+" ";
        }
        Text(line,14,512+row*18,0xeee6cd,1.5f);
    }
    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.FromHex(0x111c23));
        batch.Begin(sampler:SamplerState.PointClamp);
        var camera=Camera;
        for(int y=0;y<DreamWorld.Size;y++) for(int x=0;x<DreamWorld.Size;x++)
        {
            var p=Screen(new Vector2(x*32,y*32));
            if(p.X < -32 || p.X>480 || p.Y < 62 || p.Y>500) continue;
            char tile=World.TileAt(x,y);
            uint color=tile=='~'?0x315b6c:tile=='='?0xaa8859:tile=='#'?0x334147:tile=='T'?0x263f32:((x+y)%2==0?0x51634a:0x4d5e45);
            Box(p.X,p.Y,32,32,color);
            if(tile=='T') { Box(p.X+13,p.Y+15,6,15,0x6c5141); Box(p.X+4,p.Y+2,24,19,0x284b3b); }
            if(tile=='.' && (y==12 || x==19 && y>=5 && y<=12 || x==6 && y>=12 && y<=19)) Box(p.X+3,p.Y+5,26,22,0x897b59);
        }
        var ruin=Screen(new Vector2(624,176));
        batch.Rect(new RectangleF(ruin.X-63,ruin.Y-63,126,126),Color.FromHex(0x9caaa4),6);
        Text("RUÍNA",ruin.X-24,ruin.Y-54,0xf1dfae,1);
        var stove=Screen(new Vector2(208,592)); Box(stove.X-13,stove.Y-12,26,24,0x9c7559);
        Text("FOGÃO",stove.X-18,stove.Y+16,0xffc478,1);
        var table=Screen(new Vector2(144,592)); batch.Circle(table,26,Color.FromHex(0xb69367),2);
        for(int i=0;i<4;i++) if(!World.State.Harvested[i])
        {
            var p=Screen(DreamWorld.Bushes[i]); Box(p.X-10,p.Y-9,20,18,0x284b39); Box(p.X-5,p.Y-5,5,5,0xe8928b); Box(p.X+3,p.Y+2,5,5,0xe8928b);
        }
        var sign=Screen(new Vector2(336,400)); Box(sign.X-11,sign.Y-10,22,15,0xd1b57c); Box(sign.X-2,sign.Y+5,4,12,0x6a523c);
        Person(World.Cook,0xc48b62,"COZ."); Person(World.Scout,0x819f8b,"PISTA");
        if(World.State.EnemyHealth>0)
        {
            Person(World.Enemy,World.Windup>0?0xf0ab72u:0x91596bu,"GUARDIÃO");
            var e=Screen(World.Enemy); Box(e.X-20,e.Y-38,40,4,0x31252a); Box(e.X-20,e.Y-38,40*World.State.EnemyHealth/90f,4,0xe08d82);
        }
        if(World.Windup>0) batch.Circle(Screen(World.AttackTarget),36,Color.Red,4);
        Person(World.Position,World.HurtFlash>0?0xee7474u:0x95c6d1u,"");
        if(World.Slash>0) batch.Circle(Screen(World.Position),54,Color.FromHex(0xeace87),3);
        // Opaque HUD bands keep world geometry away from touch targets/text.
        Box(0,0,480,94,0x111c23); Box(0,500,480,300,0x111c23);
        Text("RPG DOS SONHOS · FATIA 1",14,16,0xe9d4a5,1.5f);
        Text("Vida "+World.State.Health+"   Frutos "+World.State.Berries+"   Provisões "+World.State.Meals,14,47,0xa9c1b4,1.5f);
        Text("N ↑  |  "+World.CookGoal+"  |  seed "+World.State.Seed,14,75,0x9ba89e,1);
        MessageLines(World.Message);
        batch.Circle(stick.Center,55,Color.FromHex(0x66877f),3); batch.Circle(stick.Knob,19,Color.FromHex(0xc5d4be),3);
        Text("MOVER",77,770,0x9ba89e,1);
        Control(attack,"ATACAR",0xcf947f); Control(interact,"USAR",0x93bba2); Control(eat,"COMER",0xcbb984);
        Control(pause,World.Paused?">":"II",0xcbb984);
        if(World.Paused)
        {
            Box(20,210,440,230,0x192b32);
            Text("PAUSADO",146,242,0xe9d4a5,3);
            Text("Toque > no alto para continuar.",54,302,0xeee6cd,1.5f);
            Text(saveStatus,40,343,0xa9c1b4,1.3f);
            Text("O tempo do mundo está parado.",58,386,0xa9c1b4,1.5f);
        }
        batch.End();
    }
}
