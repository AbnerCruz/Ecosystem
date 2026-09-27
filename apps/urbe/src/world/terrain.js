(function(global){
  'use strict';
  /* Gerador do mundo: geografia plausível e determinística.
     Tudo é função pura de (x, y) e da semente — o mundo é infinito e nada é salvo.
     Campos: elevação (continentes + cordilheiras), temperatura (faixas de latitude,
     esfria com a altitude), umidade (massas de ar, perto de água é mais úmido) e rios
     (vales sinuosos que descem até o mar). Os biomas saem dessa combinação. */

  /* ---------- ruído gradiente 2D com semente ---------- */
  function hash32(s){var h=2166136261>>>0;s=String(s);for(var i=0;i<s.length;i++){h^=s.charCodeAt(i);h=Math.imul(h,16777619)}return h>>>0}
  function mulberry(a){return function(){a=(a+0x6D2B79F5)>>>0;var t=Math.imul(a^(a>>>15),1|a);t=(t+Math.imul(t^(t>>>7),61|t))^t;return((t^(t>>>14))>>>0)/4294967296}}
  function makeNoise(seed){
    var r=mulberry(seed),p=new Uint8Array(512),g=new Float32Array(512),perm=[];for(var i=0;i<256;i++)perm[i]=i;
    for(i=255;i>0;i--){var j=Math.floor(r()*(i+1)),t=perm[i];perm[i]=perm[j];perm[j]=t}
    for(i=0;i<512;i++){p[i]=perm[i&255];var a=r()*Math.PI*2;g[i]=a}
    var gx=new Float32Array(256),gy=new Float32Array(256);for(i=0;i<256;i++){gx[i]=Math.cos(g[i]);gy[i]=Math.sin(g[i])}
    function fade(t){return t*t*t*(t*(t*6-15)+10)}
    return function(x,y){ /* devolve aproximadamente [-0.7, 0.7] */
      var X=Math.floor(x),Y=Math.floor(y),fx=x-X,fy=y-Y,xi=X&255,yi=Y&255;
      var aa=p[p[xi]+yi],ab=p[p[xi]+yi+1],ba=p[p[xi+1]+yi],bb=p[p[xi+1]+yi+1];
      var n00=gx[aa]*fx+gy[aa]*fy,n10=gx[ba]*(fx-1)+gy[ba]*fy,n01=gx[ab]*fx+gy[ab]*(fy-1),n11=gx[bb]*(fx-1)+gy[bb]*(fy-1);
      var u=fade(fx),v=fade(fy),a1=n00+(n10-n00)*u,a2=n01+(n11-n01)*u;return a1+(a2-a1)*v;
    };
  }
  function fbm(n,x,y,oct,lac,gain){var s=0,a=1,f=1,norm=0;for(var i=0;i<oct;i++){s+=a*n(x*f,y*f);norm+=a;a*=gain;f*=lac}return s/norm}
  function smooth(a,b,x){var t=Math.max(0,Math.min(1,(x-a)/(b-a)));return t*t*(3-2*t)}
  function mix(a,b,t){return a+(b-a)*t}

  /* ---------- biomas ---------- */
  var B={DEEP:0,SEA:1,RIVER:2,LAKE:3,BEACH:4,GRASS:5,MEADOW:6,FOREST:7,DENSE:8,SWAMP:9,TAIGA:10,TUNDRA:11,SNOW:12,HILLS:13,MOUNTAIN:14,PEAK:15,DESERT:16,SAVANNA:17,STEPPE:18};
  var INFO=[
    {id:'deep',nome:'Mar profundo',build:false,road:false,cost:99},
    {id:'sea',nome:'Mar',build:false,road:false,cost:99},
    {id:'river',nome:'Rio',build:false,road:true,cost:4,bridge:true},
    {id:'lake',nome:'Lago',build:false,road:true,cost:6,bridge:true},
    {id:'beach',nome:'Praia',build:true,road:true,cost:1.3},
    {id:'grass',nome:'Campo',build:true,road:true,cost:1},
    {id:'meadow',nome:'Prado florido',build:true,road:true,cost:1},
    {id:'forest',nome:'Floresta',build:true,road:true,cost:1.6},
    {id:'dense',nome:'Mata fechada',build:true,road:true,cost:2.2},
    {id:'swamp',nome:'Pântano',build:false,road:true,cost:3},
    {id:'taiga',nome:'Taiga',build:true,road:true,cost:1.7},
    {id:'tundra',nome:'Tundra',build:true,road:true,cost:1.4},
    {id:'snow',nome:'Neve eterna',build:false,road:false,cost:99},
    {id:'hills',nome:'Colinas',build:true,road:true,cost:2},
    {id:'mountain',nome:'Montanha',build:false,road:false,cost:99},
    {id:'peak',nome:'Pico',build:false,road:false,cost:99},
    {id:'desert',nome:'Deserto',build:true,road:true,cost:1.4},
    {id:'savanna',nome:'Savana',build:true,road:true,cost:1.1},
    {id:'steppe',nome:'Estepe',build:true,road:true,cost:1.1}
  ];
  var REASON={0:'Mar aberto',1:'Mar',2:'Rio',3:'Lago',9:'Pântano: terreno alagado',12:'Neve eterna',14:'Montanha íngreme',15:'Pico'};

  /* ---------- fila de prioridade (menor primeiro) para o preenchimento de depressões ---------- */
  function Heap(n,key){var h=new Int32Array(n),size=0;
    function up(i){var v=h[i],k=key[v];while(i>0){var p=(i-1)>>1;if(key[h[p]]<=k)break;h[i]=h[p];i=p}h[i]=v}
    function down(i){var v=h[i],k=key[v];for(;;){var l=i*2+1,r=l+1,m=i;if(l<size&&key[h[l]]<k)m=l;if(r<size&&key[h[r]]<(m===i?k:key[h[m]]))m=r;if(m===i)break;h[i]=h[m];i=m;h[i]=v}}
    return{push:function(v){h[size]=v;up(size++)},pop:function(){var t=h[0];size--;if(size>0){h[0]=h[size];down(0)}return t},get size(){return size}}}

  function createWorld(seedText,opts){
    opts=opts||{};
    var seed=hash32(seedText||'urbe'),rnd=mulberry(seed^0x5bd1e995);
    var nE=makeNoise(seed),nR=makeNoise(seed^0x9e3779b9),nT=makeNoise(seed^0x85ebca6b),nM=makeNoise(seed^0xc2b2ae35),nW=makeNoise(seed^0x27d4eb2f),nD=makeNoise(seed^0x165667b1);
    var SX=opts.spawnX==null?36:opts.spawnX,SY=opts.spawnY==null?25:opts.spawnY,SEA=0.42;
    /* o mundo é um continente finito: N×N células de CELL tiles, cercado de oceano */
    var N=opts.grid||512,CELL=opts.cell||4,NN=N*N;
    var t0=Date.now();

    /* ===== 1. placas tectônicas =====
       Voronoi com as bordas deformadas por ruído (placas reais não são polígonos retos).
       Cada placa é continental (grossa, alta) ou oceânica (fina, baixa) e tem um vetor de movimento. */
    var NP=opts.plates||(16+Math.floor(rnd()*6)),plates=[];
    for(var p=0;p<NP;p++){
      var cont=rnd()<.42,ang=rnd()*Math.PI*2,vel=.4+rnd()*.6;
      plates.push({x:(.06+rnd()*.88)*N,y:(.06+rnd()*.88)*N,cont:cont,base:cont?.62+rnd()*.08:.22+rnd()*.08,vx:Math.cos(ang)*vel,vy:Math.sin(ang)*vel});
    }
    /* as placas continentais tendem ao centro do mapa: o continente não encosta na borda */
    plates.forEach(function(q){if(q.cont){q.x=N/2+(q.x-N/2)*.62;q.y=N/2+(q.y-N/2)*.62}});
    var plate=new Uint8Array(NN),H=N>>1,half=new Uint8Array(H*H);
    for(var y=0;y<H;y++)for(var x=0;x<H;x++){
      var X2=x*2+1,Y2=y*2+1,wx=X2+fbm(nW,X2/80,Y2/80,4,2,.5)*58,wy=Y2+fbm(nW,X2/80+19,Y2/80-7,4,2,.5)*58,best=0,bd=1e18;
      for(p=0;p<NP;p++){var dx=wx-plates[p].x,dy=wy-plates[p].y,d=dx*dx+dy*dy;if(d<bd){bd=d;best=p}}
      half[y*H+x]=best;
    }
    for(y=0;y<N;y++)for(x=0;x<N;x++)plate[y*N+x]=half[(y>>1)*H+(x>>1)];

    /* ===== 2. limites de placa =====
       Para cada célula na borda, o movimento relativo ao vizinho diz o que acontece ali:
       convergência continente×continente → cordilheira alta; oceano sob continente → serra costeira
       e fossa; oceano×oceano → arco de ilhas; divergência → rift (continente) ou dorsal (oceano). */
    var upl=new Float32Array(NN),isB=new Uint8Array(NN),D4=[[1,0],[-1,0],[0,1],[0,-1]];
    for(y=0;y<N;y++)for(x=0;x<N;x++){
      var i=y*N+x,a=plates[plate[i]],sum=0,cnt=0;
      for(var k=0;k<4;k++){var xx=x+D4[k][0],yy=y+D4[k][1];if(xx<0||yy<0||xx>=N||yy>=N)continue;var j=yy*N+xx;if(plate[j]===plate[i])continue;
        var b2=plates[plate[j]],conv=(a.vx-b2.vx)*D4[k][0]+(a.vy-b2.vy)*D4[k][1],u;   /* >0: aproximando */
        if(conv>0){
          if(a.cont&&b2.cont)u=.95*conv;                 /* colisão: Himalaia */
          else if(a.cont&&!b2.cont)u=.62*conv;           /* serra costeira: Andes */
          else if(!a.cont&&b2.cont)u=-.25*conv;          /* fossa oceânica */
          else u=.42*conv;                               /* arco de ilhas: Japão */
        }else{
          if(a.cont)u=.2*conv;                           /* rift: vale afundado */
          else u=-.08*conv;                              /* dorsal meso-oceânica (levemente alta) */
        }
        sum+=u;cnt++}
      if(cnt){isB[i]=1;upl[i]=sum/cnt}
    }
    /* o soerguimento se espalha a partir da borda (BFS multi-fonte), caindo com a distância */
    var dist=new Uint16Array(NN).fill(65535),src=new Float32Array(NN),q=new Int32Array(NN),qh=0,qt=0;
    for(i=0;i<NN;i++)if(isB[i]){dist[i]=0;src[i]=upl[i];q[qt++]=i}
    while(qh<qt){i=q[qh++];x=i%N;y=(i/N)|0;for(k=0;k<4;k++){xx=x+D4[k][0];yy=y+D4[k][1];if(xx<0||yy<0||xx>=N||yy>=N)continue;j=yy*N+xx;if(dist[j]!==65535)continue;dist[j]=dist[i]+1;src[j]=src[i];q[qt++]=j}}

    /* ===== 3. elevação =====
       base da placa suavizada (plataformas e taludes continentais) + soerguimento com picos
       (ruído "ridged" ao longo da cordilheira) + relevo regional. */
    var base=new Float32Array(NN);
    for(i=0;i<NN;i++){x=i%N;y=(i/N)|0;base[i]=plates[plate[i]].base+fbm(nE,x/130,y/130,4,2,.5)*.34+fbm(nE,x/38-50,y/38+50,3,2,.5)*.08}
    function blur(f,r,passes){var tmp=new Float32Array(NN);for(var ps=0;ps<passes;ps++){
      for(var yy=0;yy<N;yy++){var acc=0,row=yy*N;for(var xx=-r;xx<=r;xx++)acc+=f[row+Math.max(0,Math.min(N-1,xx))];for(xx=0;xx<N;xx++){tmp[row+xx]=acc/(2*r+1);acc+=f[row+Math.min(N-1,xx+r+1)]-f[row+Math.max(0,xx-r)]}}
      for(xx=0;xx<N;xx++){acc=0;for(yy=-r;yy<=r;yy++)acc+=tmp[Math.max(0,Math.min(N-1,yy))*N+xx];for(yy=0;yy<N;yy++){f[yy*N+xx]=acc/(2*r+1);acc+=tmp[Math.min(N-1,yy+r+1)*N+xx]-tmp[Math.max(0,yy-r)*N+xx]}}}}
    blur(base,6,3);
    var U=new Float32Array(NN);
    for(i=0;i<NN;i++){var R0=src[i]>0?10:5,dd0=dist[i];U[i]=src[i]*Math.exp(-(dd0*dd0)/(R0*R0))}
    blur(U,4,3);
    var E=new Float32Array(NN);
    for(i=0;i<NN;i++){x=i%N;y=(i/N)|0;var u0=U[i],fall=1;
      var ridge=1-Math.abs(fbm(nR,x/26,y/26,4,2.1,.5)*1.7);ridge=Math.max(0,ridge);
      var up=u0>0?u0*fall*(.3+ridge*ridge*1.05):u0*fall;
      var e=base[i]+up+fbm(nE,x/22+300,y/22-300,4,2,.5)*.07;
      /* longe de tudo, a borda do mapa afunda no oceano */
      var dx0=x/N-.5,dy0=y/N-.5,rr=Math.max(Math.abs(dx0),Math.abs(dy0))*.55+Math.hypot(dx0,dy0)*.45+fbm(nW,x/60-200,y/60+200,3,2,.5)*.07,edge=1-smooth(.34,.49,rr);
      E[i]=e*edge+(-.05)*(1-edge);
    }
    /* nível do mar pelo quantil: ~40% de terra; depois a escala vira [0,SEA) mar e [SEA,1] terra */
    var sorted=Float32Array.from(E).sort(),landFrac=opts.land||.40,seaQ=sorted[Math.floor(NN*(1-landFrac))],lo=sorted[0],hi=sorted[NN-1];
    var hiQ=sorted[Math.floor(NN*.9995)];
    /* terra pela posição (curva hipsométrica real): muita planície, colinas e poucas serras altas */
    function rankLand(f,test,curve){var idx=[];for(var c=0;c<NN;c++)if(test(c))idx.push(c);idx.sort(function(a,b){return f[a]-f[b]});
      var L=idx.length;for(var r=0;r<L;r++)f[idx[r]]=curve(r/(L-1||1))}
    var landMask=new Uint8Array(NN);for(i=0;i<NN;i++)landMask[i]=E[i]>=seaQ?1:0;
    for(i=0;i<NN;i++)if(!landMask[i])E[i]=(E[i]-lo)/(seaQ-lo)*SEA;
    rankLand(E,function(c){return landMask[c]},function(r){var f=r<.8?r*.45:.36+.64*Math.pow((r-.8)/.2,1.3);return SEA+.0005+f*(1-SEA-.0005)});

    /* ===== 4. clima =====
       Temperatura: latitude (polos nas bordas norte e sul) e altitude.
       Umidade: células de Hadley (equador úmido, ~30° seco, ~60° úmido) + vento por faixa de
       latitude trazendo umidade do mar, que chove na subida das serras (sombra de chuva atrás). */
    var T=new Float32Array(NN),M=new Float32Array(NN),rain=new Float32Array(NN),air=new Float32Array(NN);
    for(y=0;y<N;y++){
      var lat=(y/(N-1))*2-1,al=Math.abs(lat),dir=al<.33?-1:(al<.66?1:-1),m=.8;
      for(var s2=0;s2<N;s2++){x=dir>0?s2:N-1-s2;i=y*N+x;
        if(E[i]<SEA){m=Math.min(1,m+.12);rain[i]=m*.05;air[i]=m;continue}
        var prev=dir>0?(x>0?E[i-1]:E[i]):(x<N-1?E[i+1]:E[i]),rise=Math.max(0,E[i]-prev);
        var r=m*(.006+rise*7);r=Math.min(r,m);m-=r*.72;m=Math.max(0,m-.0004);rain[i]=r;air[i]=m}
    }
    blur(rain,3,2);blur(air,6,2);
    var rs=Float32Array.from(rain).sort(),r95=rs[Math.floor(NN*.96)]||1;
    for(i=0;i<NN;i++){x=i%N;y=(i/N)|0;lat=(y/(N-1))*2-1;al=Math.abs(lat);
      var hadley=.5+.5*Math.cos(al*Math.PI*3);
      M[i]=Math.max(0,Math.min(1,.5*air[i]+.3*Math.min(1,rain[i]/r95)+.38*hadley-.12+fbm(nM,x/60,y/60,3,2,.5)*.25));
      T[i]=Math.max(0,Math.min(1,1.06-Math.pow(al,1.6)*1.0-Math.max(0,E[i]-SEA)*1.15+fbm(nT,x/80,y/80,3,2,.5)*.12));
    }

    rankLand(M,function(c){return E[c]>=SEA},function(r){return Math.pow(r,.95)});

    /* ===== 5. hidrologia =====
       Priority-flood a partir do oceano: cada célula de terra ganha a célula por onde ela escoa
       (sempre em direção ao mar) e depressões fechadas são preenchidas; as fundas viram lagos.
       O fluxo acumulado (chuva de toda a bacia a montante) diz onde há rio e quão largo ele é. */
    /* microrrelevo só para a drenagem: em planície a água serpenteia em vez de correr em linha reta */
    var Eh=new Float32Array(NN);for(i=0;i<NN;i++){x=i%N;y=(i/N)|0;Eh[i]=E[i]+(E[i]>=SEA?(fbm(nD,x/7+411,y/7-411,3,2,.55)*.012+fbm(nD,x/23-91,y/23+91,2,2,.5)*.01)*smooth(SEA,SEA+.02,E[i]):0)}
    var F=new Float32Array(NN),down=new Int32Array(NN).fill(-1),done=new Uint8Array(NN),order=new Int32Array(NN),no=0,heap=Heap(NN,F);
    for(i=0;i<NN;i++){if(E[i]<SEA){F[i]=E[i];done[i]=1;x=i%N;y=(i/N)|0;
      var coast=false;for(k=0;k<4;k++){xx=x+D4[k][0];yy=y+D4[k][1];if(xx>=0&&yy>=0&&xx<N&&yy<N&&E[yy*N+xx]>=SEA)coast=true}
      if(coast)heap.push(i)}}
    var D8=[[1,0],[-1,0],[0,1],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1]];
    while(heap.size){i=heap.pop();x=i%N;y=(i/N)|0;
      for(k=0;k<8;k++){xx=x+D8[k][0];yy=y+D8[k][1];if(xx<0||yy<0||xx>=N||yy>=N)continue;j=yy*N+xx;if(done[j])continue;done[j]=1;
        F[j]=Math.max(Eh[j],F[i]+1e-5);down[j]=i;order[no++]=j;heap.push(j)}}
    var lake=new Uint8Array(NN);for(i=0;i<NN;i++)if(E[i]>=SEA&&F[i]-Eh[i]>.006)lake[i]=1;
    var acc=new Float32Array(NN);for(i=0;i<NN;i++)if(E[i]>=SEA)acc[i]=.15+M[i];
    for(var o=no-1;o>=0;o--){i=order[o];if(down[i]>=0)acc[down[i]]+=acc[i]}
    var landAcc=[];for(i=0;i<NN;i++)if(E[i]>=SEA&&!lake[i])landAcc.push(acc[i]);landAcc.sort(function(a,b){return a-b});
    var QR=landAcc[Math.floor(landAcc.length*.965)]||1e9,QMAX=landAcc[landAcc.length-1]||1;
    /* segmentos de rio: do centro (deslocado) de cada célula ao centro da célula de baixo */
    function jitter(c,s){return(h2(c%N,(c/N)|0,s)-.5)*.7}
    function cx(c){return(c%N)+.5+jitter(c,71)}function cy(c){return((c/N)|0)+.5+jitter(c,73)}
    var segs=[],bucket=new Map();
    for(i=0;i<NN;i++){if(E[i]<SEA||acc[i]<QR||down[i]<0)continue;var dn=down[i];
      var w=Math.min(5.5,1.35+Math.log2(acc[i]/QR)*.62),seg={ax:cx(i),ay:cy(i),bx:E[dn]<SEA?(dn%N)+.5:cx(dn),by:E[dn]<SEA?((dn/N)|0)+.5:cy(dn),w:w};segs.push(seg);
      var x0=Math.floor(Math.min(seg.ax,seg.bx)-1),x1=Math.floor(Math.max(seg.ax,seg.bx)+1),y0=Math.floor(Math.min(seg.ay,seg.by)-1),y1=Math.floor(Math.max(seg.ay,seg.by)+1);
      for(yy=y0;yy<=y1;yy++)for(xx=x0;xx<=x1;xx++){var kk=yy*N+xx,l=bucket.get(kk);if(!l)bucket.set(kk,l=[]);l.push(seg)}}

    /* ===== 6. onde a cidade começa =====
       Clima temperado, relevo suave, um rio por perto (mas não no centro), longe da costa e das serras. */
    function bfsFrom(test){var d=new Uint16Array(NN).fill(65535),qq=new Int32Array(NN),h=0,t=0;for(var c=0;c<NN;c++)if(test(c)){d[c]=0;qq[t++]=c}
      while(h<t){var c2=qq[h++],x2=c2%N,y2=(c2/N)|0;for(var k2=0;k2<4;k2++){var x3=x2+D4[k2][0],y3=y2+D4[k2][1];if(x3<0||y3<0||x3>=N||y3>=N)continue;var c3=y3*N+x3;if(d[c3]!==65535)continue;d[c3]=d[c2]+1;qq[t++]=c3}}return d}
    var dCoast=bfsFrom(function(c){return E[c]<SEA}),dRiver=bfsFrom(function(c){return E[c]>=SEA&&acc[c]>=QR}),dMount=bfsFrom(function(c){return E[c]>.72}),dLake=bfsFrom(function(c){return lake[c]===1});
    var spawnCell=-1,bestScore=-1e9;
    for(i=0;i<NN;i++){if(E[i]<SEA+.03||E[i]>.6||lake[i])continue;
      var sc=-Math.abs(T[i]-.56)*6-Math.abs(M[i]-.56)*4-Math.abs(dRiver[i]-9)*.18-Math.max(0,24-dCoast[i])*.45-Math.max(0,16-dMount[i])*.5-Math.max(0,6-dLake[i])*.6-Math.max(0,5-dRiver[i])*2;
      /* entre bons lugares, os mais perto do centro do continente */
      x=i%N;y=(i/N)|0;sc-=Math.hypot(x-N/2,y-N/2)/N*1.5;
      if(sc>bestScore){bestScore=sc;spawnCell=i}}
    if(spawnCell<0)spawnCell=(N/2|0)*N+(N/2|0);
    var OX=SX-((spawnCell%N)+.5)*CELL,OY=SY-(((spawnCell/N)|0)+.5)*CELL;
    var built=Date.now()-t0;

    /* ===== 7. amostra por tile =====
       Interpolação das grades + detalhe fino; rio = distância ao traçado dos segmentos
       (com um leve serpentear); lago = máscara da bacia com margem orgânica. */
    function bil(f,gx,gy){var x0=Math.max(0,Math.min(N-2,Math.floor(gx))),y0=Math.max(0,Math.min(N-2,Math.floor(gy))),fx=Math.max(0,Math.min(1,gx-x0)),fy=Math.max(0,Math.min(1,gy-y0)),i0=y0*N+x0;
      return(f[i0]*(1-fx)+f[i0+1]*fx)*(1-fy)+(f[i0+N]*(1-fx)+f[i0+N+1]*fx)*fy}
    function lakeAt(gx,gy){var x0=Math.max(0,Math.min(N-2,Math.floor(gx))),y0=Math.max(0,Math.min(N-2,Math.floor(gy))),fx=Math.max(0,Math.min(1,gx-x0)),fy=Math.max(0,Math.min(1,gy-y0)),i0=y0*N+x0;
      return(lake[i0]*(1-fx)+lake[i0+1]*fx)*(1-fy)+(lake[i0+N]*(1-fx)+lake[i0+N+1]*fx)*fy}
    function riverAt(x,y){
      var qx=(x+.5+fbm(nD,x/11,y/11,2,2,.5)*1.8-OX)/CELL-.5+.5,qy=(y+.5+fbm(nD,x/11+40,y/11-40,2,2,.5)*1.8-OY)/CELL-.5+.5,l=bucket.get(Math.floor(qy)*N+Math.floor(qx));if(!l)return 0;
      var best=0;for(var n=0;n<l.length;n++){var sg=l[n],vx=sg.bx-sg.ax,vy=sg.by-sg.ay,t=((qx-sg.ax)*vx+(qy-sg.ay)*vy)/(vx*vx+vy*vy||1);t=Math.max(0,Math.min(1,t));
        var ddx=(sg.ax+vx*t-qx)*CELL,ddy=(sg.ay+vy*t-qy)*CELL,half=Math.max(.72,sg.w/2);if(ddx*ddx+ddy*ddy<half*half&&sg.w>best)best=sg.w}
      return best}
    function raw(x,y){
      var gx=(x+.5-OX)/CELL-.5,gy=(y+.5-OY)/CELL-.5;
      if(gx<0||gy<0||gx>N-1||gy>N-1)return{e:.1,t:.5,m:.5,river:false,lake:false,rv:1};
      var wgx=gx+fbm(nD,x/46,y/46,3,2,.5)*.9,wgy=gy+fbm(nD,x/46+23,y/46-23,3,2,.5)*.9;
      var ec=bil(E,wgx,wgy),land=smooth(SEA+.01,SEA+.1,ec);
      /* relevo fino: colinas suaves nas planícies, rocha recortada nas montanhas */
      var mnt=smooth(.62,.8,ec),rg=1-Math.abs(fbm(nR,x/18,y/18,3,2.1,.5)*1.8);rg=Math.max(0,rg);
      var e=ec+fbm(nE,x/34,y/34,3,2,.5)*.03*land+(rg*rg-.35)*.07*mnt+fbm(nE,x/9-70,y/9+70,2,2,.5)*.01*land;
      e=Math.max(0,Math.min(1,e));
      var t=bil(T,gx,gy)-(e-ec)*1.1,m=bil(M,gx,gy)+fbm(nM,x/24,y/24,2,2,.5)*.08;
      var lk=lakeAt(gx,gy)+fbm(nD,x/7,y/7,2,2,.5)*.35>.5&&e>=SEA,rw=e>=SEA&&!lk?riverAt(x,y):0;
      return{e:e,t:Math.max(0,Math.min(1,t)),m:Math.max(0,Math.min(1,m)),river:rw>0,riverW:rw,lake:lk,rv:rw>0?0:1};
    }
    function classify(s){
      var e=s.e,t=s.t,m=s.m;
      if(e<SEA-.07)return B.DEEP;
      if(e<SEA)return B.SEA;
      if(s.lake)return B.LAKE;
      if(s.river)return B.RIVER;
      if(e<SEA+.006)return t<.22?B.TUNDRA:(m>.82?B.SWAMP:B.BEACH);
      if(e>.9)return t<.45?B.SNOW:B.PEAK;
      if(e>.78)return t<.28?B.SNOW:B.MOUNTAIN;
      if(t<.12)return B.SNOW;
      if(e>.68)return t<.3?B.TUNDRA:B.HILLS;
      if(t<.28)return m>.45?B.TAIGA:B.TUNDRA;
      /* Whittaker: quente → deserto/savana/floresta tropical; temperado → estepe/campo/floresta */
      if(t>.7){if(m<.28)return B.DESERT;if(m<.5)return B.SAVANNA;return m>.8?B.DENSE:B.FOREST}
      if(m<.22)return t>.5?B.DESERT:B.STEPPE;
      if(m<.34)return B.STEPPE;
      if(m>.9&&e<SEA+.04)return B.SWAMP;
      if(m>.78)return B.DENSE;
      if(m>.56)return B.FOREST;
      if(m>.45)return B.MEADOW;
      return B.GRASS;
    }
    /* ---------- cache por chunk ---------- */
    var CH=16,cache=new Map(),LIM=opts.cacheChunks||900;
    function chunk(cx,cy){
      var k=cx+':'+cy,c=cache.get(k);if(c)return c;
      var bio=new Uint8Array(CH*CH),elev=new Float32Array(CH*CH),x0=cx*CH,y0=cy*CH;
      for(var j=0;j<CH;j++)for(var i=0;i<CH;i++){var s=raw(x0+i,y0+j);bio[j*CH+i]=classify(s);elev[j*CH+i]=s.e}
      c={cx:cx,cy:cy,bio:bio,elev:elev};
      if(cache.size>=LIM){var first=cache.keys().next().value;cache.delete(first)}
      cache.set(k,c);return c;
    }
    function at(x,y){x=Math.floor(x);y=Math.floor(y);var cx=Math.floor(x/CH),cy=Math.floor(y/CH),c=chunk(cx,cy);return c.bio[(y-cy*CH)*CH+(x-cx*CH)]}
    function elevation(x,y){x=Math.floor(x);y=Math.floor(y);var cx=Math.floor(x/CH),cy=Math.floor(y/CH),c=chunk(cx,cy);return c.elev[(y-cy*CH)*CH+(x-cx*CH)]}
    /* vegetação e detalhes: estáveis por tile */
    function h2(x,y,s){var h=Math.imul(x|0,374761393)+Math.imul(y|0,668265263)+Math.imul(s|0,2246822519);h=Math.imul(h^(h>>>13),1274126177);return((h^(h>>>16))>>>0)/4294967296}
    var TREE={}; /* probabilidade e espécie por bioma */
    TREE[B.GRASS]=[.035,'oak'];TREE[B.MEADOW]=[.05,'birch'];TREE[B.FOREST]=[.34,'oak'];TREE[B.DENSE]=[.55,'oak'];TREE[B.TAIGA]=[.36,'pine'];
    TREE[B.TUNDRA]=[.03,'deadpine'];TREE[B.HILLS]=[.08,'pine'];TREE[B.SAVANNA]=[.05,'acacia'];TREE[B.DESERT]=[.018,'cactus'];TREE[B.SWAMP]=[.14,'willow'];TREE[B.STEPPE]=[.012,'oak'];TREE[B.BEACH]=[.01,'palm'];
    function tree(x,y){var b0=at(x,y);if(b0<=3||b0===B.SNOW||b0===B.MOUNTAIN||b0===B.PEAK)return null;
      /* ecótono: a densidade e a espécie vêm de um ponto sorteado até ~3 tiles em volta,
         então bosques avançam sobre o campo aos poucos em vez de parar numa linha */
      var jx=Math.round((h2(x,y,41)-.5)*6),jy=Math.round((h2(x,y,43)-.5)*6),bj=at(x+jx,y+jy),b=(bj<=3||bj===B.SNOW||bj===B.MOUNTAIN||bj===B.PEAK)?b0:bj,r=TREE[b];if(!r)return null;
      /* bosques em manchas, não sal-e-pimenta */
      var clump=.55+fbm(nM,x/9+300,y/9-300,2,2,.5)*1.4;
      if(h2(x,y,7)>r[0]*Math.max(.2,clump))return null;
      var kind=r[1];if(b===B.FOREST&&h2(x,y,9)<.28)kind='birch';if(b===B.DENSE&&h2(x,y,9)<.35)kind='pine';if(b===B.TAIGA&&at(x,y)===B.TAIGA&&h2(x,y,3)<.1)kind='deadpine';
      return{kind:kind,v:Math.floor(h2(x,y,11)*3),snow:b===B.TUNDRA||(b===B.TAIGA&&elevation(x,y)>.62)};
    }
    /* detalhes do chão: até dois por tile, em manchas (flores em canteiros, capim em tufos) */
    function decor(x,y){var b=at(x,y),r=h2(x,y,21),q=h2(x,y,22),o=[];
      var patch=fbm(nM,x/6-900,y/6+900,2,2,.5),wet=b<=3;
      if(wet){if((b===B.LAKE||b===B.RIVER||b===B.SEA)&&r<(b===B.SEA?.02:.16)){var nl=at(x+1,y)>3||at(x-1,y)>3||at(x,y+1)>3||at(x,y-1)>3;if(nl)o.push(b===B.SEA?'shorerock':'lily')}return o.length?o:null}
      switch(b){
        case B.GRASS:if(patch>.12?r<.55:r<.07)o.push('flowers');if(q<.13)o.push('bush');else if(q<.36)o.push('tallgrass');else if(q<.42)o.push('pebbles');break;
        case B.MEADOW:if(patch>0?r<.7:r<.22)o.push('flowers');if(q<.32)o.push('tallgrass');else if(q>.96)o.push('bush');break;
        case B.FOREST:case B.DENSE:if(r<.1)o.push('mushroom');else if(r<.26)o.push('fern');if(q<.05)o.push('log');else if(q<.09)o.push('stump');else if(q<.2)o.push('bush');break;
        case B.TAIGA:if(r<.18)o.push('fern');else if(r<.24)o.push('mushroom');if(q<.08)o.push('rock');else if(q<.12)o.push('stump');break;
        case B.SWAMP:if(r<.4)o.push('reeds');if(q<.22)o.push('puddle');else if(q<.3)o.push('lily');break;
        case B.BEACH:if(r<.05)o.push('shell');if(q<.03)o.push('driftwood');else if(q<.1)o.push('pebbles');break;
        case B.DESERT:if(r<.05)o.push('rock');if(q<.07)o.push('drybush');else if(q<.1)o.push('pebbles');break;
        case B.SAVANNA:if(r<.4)o.push('drygrass');if(q<.08)o.push('drybush');else if(q<.11)o.push('rock');break;
        case B.STEPPE:if(r<.18)o.push('tuft');if(q<.25)o.push('drygrass');else if(q<.3)o.push('pebbles');break;
        case B.HILLS:if(r<.14)o.push('rock');else if(r<.2)o.push('flowers');if(q<.16)o.push('pebbles');else if(q<.4)o.push('tallgrass');break;
        case B.MOUNTAIN:case B.PEAK:if(r<.22)o.push('rock');if(q<.07)o.push('boulder');break;
        case B.TUNDRA:if(r<.22)o.push('snowpatch');if(q<.1)o.push('rock');else if(q<.2)o.push('tuft');break;
        case B.SNOW:if(r<.06)o.push('rock');break;
      }
      return o.length?o:null}
    function info(x,y){return INFO[at(x,y)]}
    return{
      seed:seedText||'urbe',CH:CH,B:B,INFO:INFO,sea:SEA,
      /* limites do continente em tiles e dados da simulação (mapa, testes) */
      bounds:{x0:Math.floor(OX),y0:Math.floor(OY),x1:Math.floor(OX+N*CELL),y1:Math.floor(OY+N*CELL)},grid:{N:N,CELL:CELL,E:E,T:T,M:M,F:F,down:down,acc:acc,lake:lake,plate:plate,plates:plates,riverQ:QR,rivers:segs,spawnCell:spawnCell,ms:built},
      raw:raw,biome:at,elevation:elevation,chunk:chunk,
      /* amostra avulsa (mapa em escala grande): não guarda o chunk inteiro no cache */
      sample:function(x,y){var cx=Math.floor(x/CH),cy=Math.floor(y/CH),c=cache.get(cx+':'+cy);if(c){var k=(Math.floor(y)-cy*CH)*CH+(Math.floor(x)-cx*CH);return{b:c.bio[k],e:c.elev[k]}}var r=raw(Math.floor(x),Math.floor(y));return{b:classify(r),e:r.e}},
      buildable:function(x,y){return INFO[at(x,y)].build},
      roadable:function(x,y){return INFO[at(x,y)].road},
      roadCost:function(x,y){return INFO[at(x,y)].cost},
      isWater:function(x,y){var b=at(x,y);return b<=3},
      reason:function(x,y){var b=at(x,y);return INFO[b].build?null:(REASON[b]||INFO[b].nome)},
      tree:tree,decor:decor,info:info,hash:h2,
      /* o worker calcula o chunk e devolve biomas/elevação: a página não refaz o ruído */
      has:function(cx,cy){return cache.has(cx+':'+cy)},
      inject:function(cx,cy,bio,elev){var k=cx+':'+cy;if(cache.has(k))return;if(cache.size>=LIM)cache.delete(cache.keys().next().value);cache.set(k,{cx:cx,cy:cy,bio:bio,elev:elev})},
      clear:function(){cache.clear()}
    };
  }
  global.UrbeTerrain={createWorld:createWorld,BIOMES:B};
  if(typeof module!=='undefined'&&module.exports)module.exports=global.UrbeTerrain;
})(typeof window!=='undefined'?window:globalThis);
