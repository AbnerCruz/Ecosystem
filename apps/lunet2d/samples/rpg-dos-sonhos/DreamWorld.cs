using System;
using System.Numerics;

// Slice rules only. No simulation advances while the app is closed.
public sealed class DreamState
{
    public int Version = 1;
    public int Seed = 20261030;
    public float X = 144, Y = 592;
    public int Health = 100, Berries, Stock, ReadyMeals, Meals;
    public bool[] Harvested = new bool[4];
    public bool ScoutClue, SignClue, RuinCleared;
    public int EnemyHealth = 90;
    public float CookProgress;
}

public sealed class DreamWorld
{
    public const int Size = 24, Tile = 32;
    private readonly char[] tiles = new char[Size * Size];
    public DreamState State { get; private set; }
    public Vector2 Position => new Vector2(State.X, State.Y);
    public Vector2 Enemy { get; private set; } = new Vector2(624, 176);
    public Vector2 Cook { get; private set; } = new Vector2(144, 592);
    public Vector2 Scout => new Vector2(208, 624);
    public Vector2 AttackTarget { get; private set; }
    public float Windup { get; private set; }
    public float Slash { get; private set; }
    public bool Paused;
    public string Message = "Colete frutos ao norte da vila. Leve à cozinheira.";
    public string CookGoal => State.Stock > 0 ? "Preparando provisão" : State.ReadyMeals > 0 ? "Provisão pronta" : "Preciso de frutos";
    public string Layout => new string(tiles);
    private float attackCooldown, enemyCooldown, hurtFlash;
    public float HurtFlash => hurtFlash;
    public event Action<string>? Event;
    public static readonly Vector2[] Bushes = { new Vector2(240,528), new Vector2(272,528), new Vector2(240,496), new Vector2(272,496) };

    public DreamWorld(int seed)
    {
        State = new DreamState { Seed = seed };
        // Small reproducible test region, not a final campaign/seed policy.
        uint random = unchecked((uint)seed) ^ 0x9e3779b9u;
        if (random == 0) random = 1;
        for (int y=0; y<Size; y++) for (int x=0; x<Size; x++)
        {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            tiles[y*Size+x] = x==0 || y==0 || x==23 || y==23 ? '#' : random%9==0 ? 'T' : '.';
        }
        for (int y=1; y<23; y++) tiles[y*Size+11] = '~';
        ClearArea(4,18,3); ClearArea(7,16,2); ClearArea(19,5,3);
        // Clear connected corridors with clearance for a player radius of 10.
        for (int x=4; x<=19; x++) ClearArea(x,12,1);
        for (int y=12; y<=19; y++) ClearArea(6,y,1);
        for (int y=5; y<=12; y++) ClearArea(19,y,1);
        for (int y=11; y<=13; y++) tiles[y*Size+11] = '=';
    }

    private void ClearArea(int cx,int cy,int radius)
    {
        for(int y=cy-radius;y<=cy+radius;y++) for(int x=cx-radius;x<=cx+radius;x++)
            if(x>0 && x<23 && y>0 && y<23) tiles[y*Size+x]='.';
    }
    public char TileAt(int x,int y) => x<0 || y<0 || x>=Size || y>=Size ? '#' : tiles[y*Size+x];
    public bool IsBlocked(int x,int y) { char t=TileAt(x,y); return t=='#' || t=='T' || t=='~'; }
    public bool CanStand(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y)
        && !IsBlocked((int)((p.X-10)/Tile),(int)((p.Y-10)/Tile))
        && !IsBlocked((int)((p.X+10)/Tile),(int)((p.Y-10)/Tile))
        && !IsBlocked((int)((p.X-10)/Tile),(int)((p.Y+10)/Tile))
        && !IsBlocked((int)((p.X+10)/Tile),(int)((p.Y+10)/Tile));

    public bool Restore(DreamState s)
    {
        if(s.Version!=1 || s.Seed!=State.Seed || s.Harvested==null || s.Harvested.Length!=4
            || !CanStand(new Vector2(s.X,s.Y)) || s.Health<1 || s.Health>100
            || s.Berries<0 || s.Berries>4 || s.Stock<0 || s.Stock>4 || s.ReadyMeals<0 || s.ReadyMeals>4
            || s.Meals<0 || s.Meals>4 || s.EnemyHealth<0 || s.EnemyHealth>90
            || s.RuinCleared != (s.EnemyHealth==0) || !float.IsFinite(s.CookProgress)
            || s.CookProgress<0 || s.CookProgress>4) return false;
        State=s; Cook=new Vector2(144,592);
        Message="Expedição retomada. O mundo só avança enquanto você joga.";
        return true;
    }

    private void Say(string text) { Message=text; Event?.Invoke(text); }
    private static bool Near(Vector2 a,Vector2 b,float range=42) => Vector2.DistanceSquared(a,b)<=range*range;

    public void Step(float dt,Vector2 direction,bool attack,bool interact,bool eat)
    {
        if(Paused || !float.IsFinite(dt) || dt<=0 || !float.IsFinite(direction.X) || !float.IsFinite(direction.Y)) return;
        dt=MathF.Min(dt,0.05f);
        attackCooldown=MathF.Max(0,attackCooldown-dt); enemyCooldown=MathF.Max(0,enemyCooldown-dt);
        Slash=MathF.Max(0,Slash-dt); hurtFlash=MathF.Max(0,hurtFlash-dt);
        if(direction.LengthSquared()>1) direction=Vector2.Normalize(direction);
        Vector2 next=Position+direction*125*dt;
        if(CanStand(new Vector2(next.X,State.Y))) State.X=next.X;
        if(CanStand(new Vector2(State.X,next.Y))) State.Y=next.Y;

        if(interact) Interact();
        if(eat)
        {
            if(State.Meals==0) Say("Sem provisão. Entregue frutos à cozinheira.");
            else if(State.Health==100) Say("Você já está recuperado. Guarde a provisão.");
            else { State.Meals--; State.Health=Math.Min(100,State.Health+35); Say("Provisão consumida: +35 de vida."); }
        }
        if(State.Stock>0)
        {
            State.CookProgress+=dt;
            Cook=Vector2.Lerp(new Vector2(144,592),new Vector2(208,592),MathF.Min(State.CookProgress,1));
            if(State.CookProgress>=4)
            {
                State.Stock--; State.ReadyMeals++; State.CookProgress=0;
                Say("Cozinheira: provisão pronta! Volte para buscar.");
            }
        }
        else Cook=Vector2.Lerp(Cook,new Vector2(144,592),MathF.Min(1,dt*3));

        if(attack && attackCooldown<=0)
        {
            attackCooldown=0.55f; Slash=0.18f;
            if(State.EnemyHealth>0 && Near(Position,Enemy,54))
            {
                State.EnemyHealth=Math.Max(0,State.EnemyHealth-30);
                Say("Acerto: 30 de dano.");
                if(State.EnemyHealth==0)
                {
                    Windup=0; State.RuinCleared=true;
                    Say("Ruína segura. Fatia concluída! Este não é um dos sete portais.");
                }
            }
            else Say("Golpe fora de alcance. Aproxime-se e recue após atacar.");
        }
        UpdateEnemy(dt);
    }

    private void Interact()
    {
        if(Near(Position,new Vector2(144,592)))
        {
            if(State.ReadyMeals>0) { State.ReadyMeals--; State.Meals++; Say("Você recebeu uma provisão. COMER recupera 35 de vida."); }
            else if(State.Berries>0) { State.Berries--; State.Stock++; Say("Fruto entregue. A cozinheira vai até o fogão e prepara uma provisão."); }
            else Say("Cozinheira: "+CookGoal+". Colete nos arbustos ao norte.");
            return;
        }
        if(Near(Position,Scout))
        {
            State.ScoutClue=true;
            Say("Batedor: vá ao norte até a ponte. Cruze e siga a trilha ao norte, na margem leste."); return;
        }
        if(Near(Position,new Vector2(336,400)))
        {
            State.SignClue=true;
            Say("Placa da ponte: ruína na margem leste. Depois da ponte, siga a trilha para o norte."); return;
        }
        for(int i=0;i<Bushes.Length;i++) if(!State.Harvested[i] && Near(Position,Bushes[i],28))
        {
            State.Harvested[i]=true; State.Berries++; Say("Fruto coletado. Estoque finito: leve à cozinheira na vila."); return;
        }
        Say("Nada ao alcance. Aproxime-se de um arbusto, da cozinheira, do batedor ou da placa.");
    }

    private void UpdateEnemy(float dt)
    {
        if(State.EnemyHealth<=0) return;
        if(Windup>0)
        {
            Windup=MathF.Max(0,Windup-dt);
            if(Windup==0)
            {
                if(Near(Position,AttackTarget,36))
                {
                    State.Health-=24; hurtFlash=0.25f; Say("Impacto: -24 de vida. Saia do círculo antes do golpe.");
                    if(State.Health<=0)
                    {
                        State.Health=100; State.X=144; State.Y=592; Enemy=new Vector2(624,176);
                        Say("Você recuou para a vila. Inventário preservado; tente outro posicionamento.");
                    }
                }
                enemyCooldown=1.1f;
            }
            return;
        }
        if(enemyCooldown>0) return;
        if(Near(Position,Enemy,56))
        {
            AttackTarget=Position; Windup=0.85f; Say("O guardião prepara um golpe. Saia do círculo vermelho!"); return;
        }
        var home=new Vector2(624,176);
        Vector2 target=Near(Position,home,115) ? Position : home;
        var delta=target-Enemy;
        if(delta.LengthSquared()>4)
        {
            var next=Enemy+Vector2.Normalize(delta)*65*dt;
            if(CanStand(next)) Enemy=next;
        }
    }
}
