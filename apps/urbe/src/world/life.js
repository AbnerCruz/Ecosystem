/* Vida do mundo: o aquário. Quando ninguém mexe em nada, a cidade continua viva.
   - Luz contínua: aurora, sol dourado, crepúsculo e noite (no modo "ciclo", um dia a cada 24 min).
   - Clima: nuvens que passam, vento, folhas, chuva (ou neve no frio) com arco-íris depois, neblina.
   - Água: brilhos do sol e da lua, peixes pulando, marolas dos patos e dos barcos.
   - Fauna e flora: borboletas, vaga-lumes, pombos, um cachorro que segue um morador, raposa à noite.
   - Cidade: fumaça nas chaminés, lampiões que acendem ao anoitecer, lanternas e guarda-chuvas.
   - Eventos: balão, festa no bairro (fogos à noite), barco à vela, estrelas cadentes, revoada.
   Interações: pombos e cervos fogem de quem passa, ovelhas fogem da raposa, a chuva apressa o
   passo dos moradores. Tudo em pixel-art na escala do mundo, com qualidade que se adapta ao aparelho. */
(function(global){
  'use strict';
  var core=global.UrbeCore;if(!core)return;
  var H=core.service('world.life.host');if(!H)return;
  var ctx=H.ctx,TILE=H.TILE,B=H.B||{},R=Math.random,PI=Math.PI;

  /* ---------------- utilidades ---------------- */
  function rnd(a,b){return a+R()*(b-a)}
  function pick(a){return a[Math.floor(R()*a.length)]}
  function clamp(v,a,b){return v<a?a:v>b?b:v}
  function hash(x,y){var h=(Math.imul(x|0,374761393)+Math.imul(y|0,668265263))|0;h=Math.imul(h^(h>>>13),1274126177);return(h^(h>>>16))>>>0}
  function P(x,y){return H.w2s(x*TILE,y*TILE)}
  function view(){var cam=H.camera(),z=cam.z;return{z:z,s:TILE*z/16,t:TILE*z,f:H.faixa(),w:H.largura(),h:H.altura()}}
  function dentro(f,x,y,m){m=m||0;return x>=f.x0-m&&x<=f.x1+m&&y>=f.y0-m&&y<=f.y1+m}
  var CAMPO={},MATA={},FRIO={};
  [B.GRASS,B.MEADOW,B.STEPPE,B.SAVANNA,B.HILLS].forEach(function(b){if(b!=null)CAMPO[b]=1});
  [B.FOREST,B.DENSE,B.TAIGA,B.SWAMP].forEach(function(b){if(b!=null)MATA[b]=1});
  [B.SNOW,B.TUNDRA,B.PEAK,B.TAIGA].forEach(function(b){if(b!=null)FRIO[b]=1});
  function campo(x,y){var b=H.bioma(x,y);return !!CAMPO[b]}
  function mata(x,y){var b=H.bioma(x,y);return !!MATA[b]}
  function livre(x,y){return !H.agua(x,y)&&!H.rua(x,y)&&!H.casaEm(x,y)}
  function tileNaVista(f,pred,tentativas){for(var i=0;i<(tentativas||8);i++){var x=Math.floor(rnd(f.x0,f.x1+1)),y=Math.floor(rnd(f.y0,f.y1+1));if(pred(x,y))return{x:x,y:y}}return null}

  /* sprites em grade (mesmo jeito da arte do mundo) */
  var SPR={};
  function sprite(key,rows,pal,flip){var k=key+(flip?'f':'');if(SPR[k])return SPR[k];var h=rows.length,w=0;rows.forEach(function(r){w=Math.max(w,r.length)});
    var c=document.createElement('canvas');c.width=w;c.height=h;var g=c.getContext('2d');
    for(var j=0;j<h;j++)for(var i=0;i<rows[j].length;i++){var ch=rows[j][i];if(ch==='.')continue;g.fillStyle=pal[ch]||'#f0f';g.fillRect(flip?w-1-i:i,j,1,1)}
    return(SPR[k]=c)}
  function desenha(img,x,y,s,ancora){s=Math.max(s,1.1);/* x,y em tiles; âncora nos pés */var p=P(x,y),w=img.width*s,h=img.height*s;
    ctx.drawImage(img,Math.round(p.x-w/2),Math.round(p.y-h*(ancora==null?1:ancora)),Math.ceil(w),Math.ceil(h))}
  var GLOW={};
  function brilhoImg(cor){if(GLOW[cor])return GLOW[cor];var c=document.createElement('canvas');c.width=c.height=64;var g=c.getContext('2d'),gr=g.createRadialGradient(32,32,0,32,32,32);
    gr.addColorStop(0,'rgba('+cor+',1)');gr.addColorStop(.25,'rgba('+cor+',.55)');gr.addColorStop(1,'rgba('+cor+',0)');g.fillStyle=gr;g.fillRect(0,0,64,64);return(GLOW[cor]=c)}
  function brilho(cor,px,py,r,a){if(a<=.01)return;ctx.globalAlpha=Math.min(1,a);ctx.drawImage(brilhoImg(cor),px-r,py-r,r*2,r*2)}
  var NUV={};
  function nuvemImg(v,branca){var k=v+(branca?'b':'');if(NUV[k])return NUV[k];var c=document.createElement('canvas');c.width=200;c.height=120;var g=c.getContext('2d'),rng=mulberry(9173+v*31);
    for(var i=0;i<9;i++){var x=40+rng()*120,y=35+rng()*50,r=22+rng()*30,gr=g.createRadialGradient(x,y,0,x,y,r);var cc=branca?'255,255,255':'0,0,0';
      gr.addColorStop(0,'rgba('+cc+',.55)');gr.addColorStop(1,'rgba('+cc+',0)');g.fillStyle=gr;g.fillRect(x-r,y-r,r*2,r*2)}
    return(NUV[k]=c)}
  function mulberry(a){return function(){a|=0;a=a+0x6D2B79F5|0;var t=Math.imul(a^a>>>15,1|a);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296}}

  /* ---------------- hora e luz ---------------- */
  var CICLO=24*60;   /* modo "ciclo": um dia inteiro a cada 24 minutos */
  function hora(){var m=H.opcoes().ambiente;if(m==='dia')return 12.5;if(m==='entardecer')return 18.2;if(m==='noite')return 23;
    if(m==='ciclo')return((Date.now()/1000/CICLO)%1)*24;var d=new Date();return d.getHours()+d.getMinutes()/60+d.getSeconds()/3600}
  var CHAVES=[[0,[78,92,158],1],[4.6,[84,96,162],1],[5.4,[166,132,178],.72],[6.2,[246,186,166],.32],[7.2,[255,234,212],.06],[8.2,[255,255,255],0],
    [15.8,[255,255,255],0],[16.6,[255,244,222],0],[17.3,[255,214,160],.06],[18.1,[248,164,124],.3],[18.9,[180,120,158],.6],[19.8,[106,104,170],.88],[20.6,[80,94,160],1],[24,[78,92,158],1]];
  function luzDa(h){for(var i=0;i<CHAVES.length-1;i++){var a=CHAVES[i],b=CHAVES[i+1];if(h>=a[0]&&h<=b[0]){var k=(h-a[0])/(b[0]-a[0]),e=k*k*(3-2*k);
    return{cor:[0,1,2].map(function(j){return Math.round(a[1][j]+(b[1][j]-a[1][j])*e)}),escuro:a[2]+(b[2]-a[2])*e}}}return{cor:[255,255,255],escuro:0}}
  var LUZ={cor:[255,255,255],escuro:0,hora:12};
  function atualizarLuz(){var h=hora(),l=luzDa(h),k=chuva.k;
    if(k>0){l.cor=l.cor.map(function(c,j){return Math.round(c*(1-k*[.26,.22,.14][j]))});l.escuro=Math.min(1,l.escuro+k*.12)}
    LUZ={cor:l.cor,escuro:l.escuro,hora:h}}

  /* ---------------- estado ---------------- */
  var T=0,ult=0,q=1,lagMedio=33,lagT=0;
  var vento={a:rnd(0,PI*2),s:.7,x:.5,y:.1};
  var nuvens=[],neblinas=[],gotas=[],respingos=[],ondas=[],brilhos=[],peixes=[],borboletas=[],vagalumes=[],fumaca=[],pombos=[],folhas=[],rajadas=[];
  var baloes=[],confete=[],foguetes=[],faiscas=[],claroes=[],estrelas=[],emotes=new Map();
  var chuva={k:0,alvo:0,fim:0,neve:false},neblina={k:0,alvo:0,fim:0},arco=null,balao=null,barco=null,raposa=null,cao=null,festa=null,meteoros=0;
  var proxEvento=rnd(14,26),proxPeixe=rnd(3,7),proxPombos=rnd(2,5),proxEmote=rnd(4,9),proxEstrela=rnd(15,40),tFumaca=0;
  var lampioes=null,lampKey=-1;

  /* ---------------- aviso discreto dos eventos ---------------- */
  var aviso=null,avisoT=0;
  function avisar(txt){if(!H.opcoes().eventos)return;try{if(!aviso){aviso=document.createElement('div');aviso.className='urbe-vida-aviso';aviso.setAttribute('role','status');
      aviso.style.cssText='position:fixed;left:50%;top:calc(env(safe-area-inset-top,0px) + 70px);transform:translateX(-50%) translateY(-8px);z-index:60;pointer-events:none;padding:7px 14px;border-radius:999px;background:rgba(14,18,26,.74);color:#eef2fa;font:600 13px/1.2 system-ui,sans-serif;letter-spacing:.2px;opacity:0;transition:opacity .6s,transform .6s;box-shadow:0 4px 18px rgba(0,0,0,.25);max-width:80vw;text-align:center;white-space:nowrap;overflow:hidden;text-overflow:ellipsis';
      document.body.appendChild(aviso)}
    aviso.textContent=txt;aviso.style.opacity='1';aviso.style.transform='translateX(-50%) translateY(0)';avisoT=T+4.5}catch(_){}}
  function atualizarAviso(){if(aviso&&avisoT&&(T>avisoT||!H.visivel())){aviso.style.opacity='0';aviso.style.transform='translateX(-50%) translateY(-8px)';avisoT=0}}

  /* ---------------- sprites ---------------- */
  var PAL_POMBO={g:'#9a9ba6',G:'#6c6d78',w:'#5e8c7a',y:'#e0a64a',k:'#c46b5b'};
  var POMBO={em:['...gg..','..gwgy.','.gggg..','gGGgg..','..k.k..'],bica:['.......','...gg..','.gggwgy','gGGgg..','..k.k..'],
    voo1:['g.....g','gg...gg','.ggwgg.','..ggy..'],voo2:['.......','ggggggg','.ggwgg.','..ggy..']};
  var PAL_CAO={b:'#9a6438',B:'#74492a',w:'#eadcc4',k:'#2a2018',p:'#d9747a'};
  var CAO={a:['........bb.','b......bbkb','.bbbbbbbbbp','..bBBBBBb..','..k.k..k.k.'],b:['........bb.','b......bbkb','.bbbbbbbbbp','..bBBBBBb..','...k.kk.k..'],
    senta:['.......bb..','......bbkb.','......bbbp.','.b..bbbbb..','bbbbbbbbb..','.kk...k.k..']};
  var PAL_RAPOSA={o:'#d7732e',O:'#a9531b',w:'#f4ede2',k:'#2a1c14'};
  var RAPOSA={a:['..........oo','w........ook','wwoooooooooo','.woOOOOOOo..','...k.k..k.k.'],b:['..........oo','w........ook','wwoooooooooo','.woOOOOOOo..','....kk..kk..']};
  var PAL_BARCO={h:'#7a5232',H:'#553821',s:'#f4efe2',S:'#d6d0c0',m:'#3a2a1c',f:'#d9483b'};
  var BARCO=['.....mf.....','.....ms.....','....sms.....','...ssmsS....','..sssmSS....','.ssssmSSS...','sssssmSSSS..','.....m......','hhhhhhhhhhhh','.hhhhhhhhhh.','..HHHHHHHH..'];
  var PEIXE=['..ss.','sswws','..ss.'],PAL_PEIXE={s:'#9fb7c9',w:'#e8f1f6'};
  var CORES_BORB=['#f7d046','#f4f0e6','#ee8a4a','#8fb8ff','#e59ad8','#b7e36a'];
  var CORES_FESTA=['#ff5d5d','#ffd24a','#5dd6ff','#9d7bff','#6dffa0','#ff8ad8','#ffffff'];
  var CORES_BALAO=[['#d9483b','#f4d35e'],['#3d7dd8','#f2f2f2'],['#6c4ab6','#f4a259'],['#2a9d8f','#e9c46a'],['#e76f51','#264653']];
  function balaoImg(cs){var k='balao'+cs.join();if(SPR[k])return SPR[k];var w=16,h=24,c=document.createElement('canvas');c.width=w;c.height=h;var g=c.getContext('2d');
    for(var y=0;y<16;y++){var ry=(y-7.5)/8.2,half=Math.sqrt(Math.max(0,1-ry*ry))*7.6*(y>10?1-(y-10)*.09:1);for(var x=0;x<w;x++){var dx=x-7.5;if(Math.abs(dx)>half)continue;
      var faixa=Math.floor((dx+8)/2.7)%2;g.fillStyle=faixa?cs[1]:cs[0];g.fillRect(x,y,1,1)}}
    g.fillStyle='rgba(255,255,255,.35)';g.fillRect(4,3,2,4);g.fillStyle='#4a3424';[[5,16],[10,16],[5,17],[10,17],[6,18],[9,18]].forEach(function(p){g.fillRect(p[0],p[1],1,1)});
    g.fillStyle='#8a5a34';g.fillRect(6,19,4,3);g.fillStyle='#6b4426';g.fillRect(6,21,4,1);return(SPR[k]=c)}

  /* ---------------- eventos ---------------- */
  function bairroNaVista(f){var w=H.mundo(),cand=[],sub=[];for(var i=0;i<w.regions.length;i++){var r=w.regions[i];if(!r.w)continue;var c=H.centroBairro(r);if(!dentro(f,c.x,c.y,-2))continue;cand.push({r:r,c:c});if(r.parentId)sub.push({r:r,c:c})}
    return sub.length?pick(sub):cand.length?pick(cand):null}
  function aguaGrande(f){return tileNaVista(f,function(x,y){if(!H.agua(x,y))return false;for(var d=0;d<8;d++){var a=d*PI/4;if(!H.agua(x+Math.round(Math.cos(a)*3),y+Math.round(Math.sin(a)*3)))return false}return true},30)}
  function sortearEvento(v){
    var o=H.opcoes(),L=LUZ,h=L.hora,op=[],f=v.f;
    if(o.clima!==false){if(!chuva.alvo&&chuva.k<.05)op.push(['chuva',2.2]);if(neblina.k<.05&&((h>4.5&&h<9.5)?4:.4))op.push(['neblina',(h>4.5&&h<9.5)?4:.4])}
    if(o.eventos!==false){
      if(L.escuro<.2&&chuva.k<.1&&!balao)op.push(['balao',1.6]);
      if(!festa&&v.z>=.3)op.push(['festa',L.escuro>.55?3:1.4]);
      if(!raposa&&(h>=17.3||h<6.3)&&v.z>=.45)op.push(['raposa',1.8]);
      if(L.escuro>.75&&!meteoros)op.push(['meteoros',.8]);
      if(o.fauna!==false)op.push(['revoada',1.1]);
      if(!barco&&v.z>=.35)op.push(['barco',1.8]);
    }
    if(!op.length)return;var tot=op.reduce(function(a,b){return a+b[1]},0),r=R()*tot;
    for(var i=0;i<op.length;i++){r-=op[i][1];if(r<=0){iniciar(op[i][0],v);return}}
  }
  function iniciar(tipo,v){
    atualizarLuz();
    var f=v.f,cx=(f.x0+f.x1)/2,cy=(f.y0+f.y1)/2;
    if(tipo==='chuva'){chuva.alvo=rnd(.55,1);chuva.fim=T+rnd(40,85);chuva.neve=!!FRIO[H.bioma(cx,cy)];avisar(chuva.neve?'❄ Começou a nevar':'🌧 Chuva passageira')}
    else if(tipo==='neblina'){neblina.alvo=rnd(.6,1);neblina.fim=T+rnd(45,90);avisar('🌫 Neblina')}
    else if(tipo==='balao'){var ang=Math.atan2(vento.y,vento.x),vx=Math.cos(ang),vy=Math.sin(ang)*.5,span=Math.max(f.x1-f.x0,f.y1-f.y0);
      var lx3=(f.x1-f.x0)/2+3,ly3=(f.y1-f.y0)/2+3,kk=Math.min(lx3/Math.max(.01,Math.abs(vx)),ly3/Math.max(.01,Math.abs(vy)));
      balao={x:cx-vx*kk,y:cy-vy*kk+rnd(-2,2),vx:vx*.9,vy:vy*.9,alt:3.4,cs:pick(CORES_BALAO),fim:T+kk*2/.9+4};avisar('🎈 Um balão cruza o céu')}
    else if(tipo==='festa'){var b=bairroNaVista(f);if(!b)return;var noite=LUZ.escuro>.55;festa={x:b.c.x,y:b.c.y,nome:H.nomeBairro(b.r),noite:noite,fim:T+(noite?38:30),prox:T+.5};
      avisar(noite?'🎆 Fogos sobre '+festa.nome:'🎉 Festa em '+festa.nome);
      H.povo().forEach(function(a){if(a.x&&Math.abs(a.x-festa.x)<7&&Math.abs(a.y-festa.y)<6)emotes.set(a,{c:pick(['♪','♥','♪','!']),fim:T+rnd(3,6)})})}
    else if(tipo==='raposa'){var t=tileNaVista(f,function(x,y){return campo(x,y)&&livre(x,y)},40);if(!t)return;var dir=R()<.5?1:-1;
      raposa={x:dir>0?f.x0-2:f.x1+2,y:t.y+.5,dir:dir,fim:T+60,fr:0};avisar('🦊 Uma raposa atravessa os campos')}
    else if(tipo==='meteoros'){meteoros=T+25;avisar('✦ Chuva de estrelas cadentes')}
    else if(tipo==='revoada'){var fa=H.fauna(),dl=R()<.5,y0=rnd(f.y0+2,f.y1-2),x0=dl?f.x0-6:f.x1+6,vx=(dl?1:-1)*rnd(3.2,4),n=9+Math.floor(R()*7);
      for(var i=0;i<n;i++){var lado=i%2?1:-1,k=Math.ceil(i/2);fa.push({kind:'bird',x:x0-(dl?1:-1)*k*.85,y:y0+lado*k*.55,vx:vx,vy:rnd(-.2,.2),fr:i%2,ft:R()*.2,flip:!dl,alt:2.6+R()*.4})}
      avisar('🕊 Uma revoada passa pela cidade')}
    else if(tipo==='barco'){var w=aguaGrande(f);if(!w)return;var dirs=[[1,0],[-1,0],[0,1],[0,-1]];barco={x:w.x+.5,y:w.y+.5,d:pick(dirs),fim:T+rnd(70,120),esteira:0,fr:0}}
  }

  /* ---------------- passo da simulação ---------------- */
  function passo(dt,v){
    var f=v.f,o=H.opcoes(),L=LUZ,dia=L.escuro<.3,noite=L.escuro>.55;
    /* vento: muda devagar, com rajadas */
    vento.a+=rnd(-.03,.03)*dt*10;vento.s=clamp(.75+.45*Math.sin(T*.07)+.25*Math.sin(T*.31),.2,1.5);vento.x=Math.cos(vento.a)*vento.s;vento.y=Math.sin(vento.a)*vento.s*.45;

    /* eventos */
    if(T>proxEvento){proxEvento=T+rnd(40,95);sortearEvento(v)}
    if(chuva.alvo&&T>chuva.fim){chuva.alvo=0;if(!chuva.neve&&LUZ.escuro<.3&&R()<.6)arco={t0:T+6,fim:T+46,cx:rnd(.25,.75)}}
    chuva.k+=((chuva.alvo||0)-chuva.k)*Math.min(1,dt*.35);if(chuva.k<.004&&!chuva.alvo)chuva.k=0;
    H.pressa(1+chuva.k*.7);
    if(neblina.alvo&&T>neblina.fim)neblina.alvo=0;neblina.k+=((neblina.alvo||0)-neblina.k)*Math.min(1,dt*.25);
    if(arco&&T>arco.fim)arco=null;
    if(festa&&T>festa.fim)festa=null;
    if(meteoros&&T>meteoros)meteoros=0;

    var cx=(f.x0+f.x1)/2,cy=(f.y0+f.y1)/2,lx=(f.x1-f.x0),ly=(f.y1-f.y0),area=lx*ly;

    /* nuvens (sombras que passam por cima de tudo) */
    if(o.clima!==false){var nAlvo=Math.round(clamp(area/520,3,11)*(q*.5+.5)*(1+chuva.k));
      while(nuvens.length<nAlvo)nuvens.push({x:cx+rnd(-lx,lx),y:cy+rnd(-ly,ly),r:rnd(5,11),v:Math.floor(R()*3)});
      for(var i=nuvens.length-1;i>=0;i--){var nu=nuvens[i];nu.x+=vento.x*.55*dt;nu.y+=vento.y*.55*dt;
        if(Math.abs(nu.x-cx)>lx*1.3+nu.r*2||Math.abs(nu.y-cy)>ly*1.3+nu.r*2||nuvens.length>nAlvo+2){
          if(nuvens.length>nAlvo){nuvens.splice(i,1);continue}nu.x=cx-Math.sign(vento.x||1)*(lx*.9+nu.r*1.5)+rnd(-2,2);nu.y=cy+rnd(-ly*.8,ly*.8)}}
    }else nuvens.length=0;
    if(neblina.k>.02){while(neblinas.length<Math.round(8*q))neblinas.push({x:cx+rnd(-lx,lx),y:cy+rnd(-ly,ly),r:rnd(6,12),v:Math.floor(R()*3)});
      neblinas.forEach(function(n){n.x+=vento.x*.25*dt;if(Math.abs(n.x-cx)>lx*1.2+n.r)n.x=cx-Math.sign(vento.x||1)*(lx*.9+n.r)})}else neblinas.length=0;

    /* chuva/neve (na tela) */
    var W=v.w,Hh=v.h,nG=Math.round(chuva.k*170*q*Math.min(2.2,W*Hh/(420*860)));
    while(gotas.length<nG)gotas.push({x:rnd(-40,W+40),y:rnd(-Hh,Hh),v:rnd(520,760),l:rnd(9,16),ph:R()*6});
    if(gotas.length>nG)gotas.length=nG;
    for(i=0;i<gotas.length;i++){var g=gotas[i];if(chuva.neve){g.y+=g.v*.12*dt;g.x+=(vento.x*40+Math.sin(T*1.7+g.ph)*18)*dt}else{g.y+=g.v*dt;g.x+=vento.x*140*dt}
      if(g.y>Hh+10){g.y=rnd(-40,-5);g.x=rnd(-40,W+40);if(!chuva.neve&&R()<.35)respingos.push({x:rnd(0,W),y:rnd(0,Hh),t:0})}if(g.x>W+60)g.x-=W+100;if(g.x<-60)g.x+=W+100}
    for(i=respingos.length-1;i>=0;i--){respingos[i].t+=dt;if(respingos[i].t>.25)respingos.splice(i,1)}
    if(chuva.k>.15&&!chuva.neve){for(var r=0;r<Math.round(chuva.k*5*q);r++){var tw=tileNaVista(f,H.agua,2);if(tw)ondas.push({x:tw.x+R(),y:tw.y+R(),t:0,d:rnd(.7,1.1),r:rnd(.25,.5)})}}

    /* água: brilhos, peixes, marolas dos patos */
    var nb=Math.round((dia?9:noite?3:5)*q*dt*10);
    for(i=0;i<nb;i++){var tb=tileNaVista(f,H.agua,2);if(tb)brilhos.push({x:tb.x+R(),y:tb.y+R(),t:0,d:rnd(.5,1.2)})}
    for(i=brilhos.length-1;i>=0;i--){brilhos[i].t+=dt;if(brilhos[i].t>brilhos[i].d)brilhos.splice(i,1)}
    if(o.fauna!==false&&v.z>=.35&&T>proxPeixe){proxPeixe=T+rnd(3.5,9);var tp=tileNaVista(f,function(x,y){return H.agua(x,y)&&H.agua(x+1,y)&&H.agua(x-1,y)&&H.agua(x,y+1)},14);
      if(tp){var dir=R()<.5?1:-1;peixes.push({x:tp.x+.5,y:tp.y+.5,dir:dir,t:0,d:rnd(.8,1.1),alt:rnd(.5,.9)});ondas.push({x:tp.x+.5,y:tp.y+.5,t:0,d:.9,r:.45})}}
    for(i=peixes.length-1;i>=0;i--){var pe=peixes[i];pe.t+=dt;if(pe.t>pe.d){ondas.push({x:pe.x+pe.dir*.9,y:pe.y,t:0,d:1,r:.55});ondas.push({x:pe.x+pe.dir*.9,y:pe.y,t:-.15,d:1.2,r:.8});peixes.splice(i,1)}}
    var fa=H.fauna();for(i=0;i<fa.length;i++){var an=fa[i];if(an.kind==='duck'&&an.st==='walk'&&R()<dt*1.6)ondas.push({x:an.x,y:an.y+.1,t:0,d:.9,r:.35})}
    for(i=ondas.length-1;i>=0;i--){ondas[i].t+=dt;if(ondas[i].t>ondas[i].d)ondas.splice(i,1)}
    if(ondas.length>140)ondas.splice(0,ondas.length-140);

    /* borboletas (de dia, em campo e jardins) */
    var nBorb=o.fauna!==false&&dia&&chuva.k<.2&&v.z>=.38?Math.round(8*q):0;
    if(borboletas.length<nBorb&&R()<dt*2){var tc=tileNaVista(f,function(x,y){return (campo(x,y)||!!H.casaEm(x+2,y))&&!H.agua(x,y)},14);
      if(tc)borboletas.push({cx:tc.x+.5,cy:tc.y+.5,x:tc.x+.5,y:tc.y+.5,ph:R()*9,cor:pick(CORES_BORB),vida:rnd(18,40),t:0,alt:rnd(.3,.8)})}
    for(i=borboletas.length-1;i>=0;i--){var bo=borboletas[i];bo.t+=dt;bo.ph+=dt;bo.cx+=(Math.sin(bo.ph*.37)*.5+vento.x*.15)*dt;bo.cy+=Math.cos(bo.ph*.29)*.35*dt;
      bo.x=bo.cx+Math.sin(bo.ph*1.9)*.55;bo.y=bo.cy+Math.sin(bo.ph*2.6)*.3;bo.alt=.45+Math.sin(bo.ph*3.1)*.2;
      if(bo.t>bo.vida||!dentro(f,bo.x,bo.y,4)||nBorb===0&&bo.t>2)borboletas.splice(i,1)}

    /* vaga-lumes (à noite, sobre o mato) */
    var nVag=o.fauna!==false&&L.escuro>.45&&chuva.k<.3&&v.z>=.4?Math.round(30*q):0;
    if(vagalumes.length<nVag){var tv=tileNaVista(f,function(x,y){return (campo(x,y)||mata(x,y))&&!H.agua(x,y)},10);if(tv)vagalumes.push({x:tv.x+R(),y:tv.y+R(),vx:0,vy:0,ph:R()*9,alt:rnd(.2,.9),vida:rnd(12,30),t:0})}
    for(i=vagalumes.length-1;i>=0;i--){var va=vagalumes[i];va.t+=dt;va.vx+=rnd(-.8,.8)*dt;va.vy+=rnd(-.8,.8)*dt;va.vx*=.97;va.vy*=.97;va.x+=va.vx*dt*.6;va.y+=va.vy*dt*.6;va.alt=clamp(va.alt+rnd(-.3,.3)*dt,.15,1.1);
      if(va.t>va.vida||!dentro(f,va.x,va.y,3)||vagalumes.length>nVag+4)vagalumes.splice(i,1)}

    /* folhas caindo das matas e rajadas de vento nos campos */
    if(o.clima!==false&&v.z>=.4){if(R()<dt*(2.5+vento.s*3)*q){var tf=tileNaVista(f,mata,6);if(tf)folhas.push({x:tf.x+R(),y:tf.y+R(),alt:rnd(1,1.8),t:0,ph:R()*9,cor:pick(['#d9a13b','#c8622f','#9bb54a','#e3c15a','#b04a2a'])})}
      if(vento.s>1.05&&R()<dt*6*q){var tr=tileNaVista(f,campo,6);if(tr)rajadas.push({x:tr.x+R(),y:tr.y+R(),t:0,d:rnd(.8,1.4),l:rnd(1.2,2.2)})}}
    for(i=folhas.length-1;i>=0;i--){var fo=folhas[i];fo.t+=dt;fo.alt-=dt*.32;fo.x+=(vento.x*.9+Math.sin(fo.ph+T*3)*.35)*dt;fo.y+=vento.y*.5*dt;if(fo.alt<-.8||folhas.length>80)folhas.splice(i,1)}
    for(i=rajadas.length-1;i>=0;i--){var ra=rajadas[i];ra.t+=dt;ra.x+=vento.x*2.2*dt;ra.y+=vento.y*2.2*dt;if(ra.t>ra.d)rajadas.splice(i,1)}

    /* fumaça das chaminés */
    tFumaca-=dt;if(tFumaca<=0&&v.z>=.35){tFumaca=.12;var casas=H.mundo().buildings,h=L.hora,slot=Math.floor(h*2),
      base=(h>=5.5&&h<9.5)?.85:(h>=16.5&&h<23)?.75:(h>=23||h<5.5)?.4:.22;
      for(i=0;i<casas.length;i++){var ca=casas[i];if(!dentro(f,ca.x+ca.w/2,ca.y,1))continue;var sem=hash(ca.x*31+ca.y,slot),frio=FRIO[H.bioma(ca.x,ca.y)]?.3:0;
        if((sem%100)/100>base+frio)continue;if(R()>.3)continue;
        fumaca.push({x:ca.x+ca.w*.72+rnd(-.05,.05),y:ca.y+.25,t:0,d:rnd(2.6,4),r0:rnd(.06,.1),vx:vento.x*.35+rnd(-.05,.05),vy:-rnd(.32,.45)})}}
    for(i=fumaca.length-1;i>=0;i--){var fu=fumaca[i];fu.t+=dt;fu.x+=(fu.vx+vento.x*.15*fu.t)*dt;fu.y+=fu.vy*dt;if(fu.t>fu.d)fumaca.splice(i,1)}
    if(fumaca.length>110*q)fumaca.splice(0,fumaca.length-Math.round(110*q));

    /* pombos: bicam perto das ruas e voam quando alguém chega perto */
    var povo=H.povo();
    if(o.fauna!==false&&v.z>=.4&&L.escuro<.6&&chuva.k<.4&&T>proxPombos){proxPombos=T+rnd(5,11);var chao=pombos.filter(function(p){return p.st==='chao'}).length;
      if(chao<Math.round(9*q)){var tr2=tileNaVista(f,function(x,y){return H.rua(x,y)&&(H.casaEm(x+1,y)||H.casaEm(x-1,y)||H.casaEm(x,y+1)||H.casaEm(x,y-1)||H.casaEm(x+2,y)||H.casaEm(x,y+2))},40)||tileNaVista(f,H.rua,40);
        if(tr2)for(var n=3+Math.floor(R()*3),k2=0;k2<n;k2++)pombos.push({x:tr2.x+.5+rnd(-.6,.6),y:tr2.y+.5+rnd(-.5,.5),st:'chao',t:rnd(0,1),fr:0,flip:R()<.5,alt:0,vx:0,vy:0})}}
    for(i=pombos.length-1;i>=0;i--){var po=pombos[i];po.t-=dt;
      if(po.st==='chao'){if(po.t<=0){po.t=rnd(.3,1.1);po.fr=po.fr?0:1;if(R()<.25){var nx=po.x+rnd(-.3,.3),ny=po.y+rnd(-.2,.2);if(!H.agua(nx,ny)){po.flip=nx<po.x;po.x=nx;po.y=ny}}}
        var assusta=null;for(var j=0;j<povo.length&&!assusta;j++){var pv=povo[j];if(pv.x&&Math.abs(pv.x-po.x)<1.3&&Math.abs(pv.y-po.y)<1)assusta=pv}
        if(!assusta&&cao&&Math.abs(cao.x-po.x)<1.6&&Math.abs(cao.y-po.y)<1.2)assusta=cao;if(!assusta&&raposa&&Math.abs(raposa.x-po.x)<2.5&&Math.abs(raposa.y-po.y)<2)assusta=raposa;
        if(assusta){var ddx=po.x-assusta.x,ddy=po.y-assusta.y,dd=Math.hypot(ddx,ddy)||1;po.st='voo';po.vx=ddx/dd*rnd(2.6,3.6)+rnd(-.5,.5);po.vy=ddy/dd*rnd(1.5,2.5)+rnd(-.5,.5);po.t=rnd(2.5,3.5);po.flip=po.vx<0}}
      else{po.x+=po.vx*dt;po.y+=po.vy*dt;po.alt=Math.min(2.4,po.alt+dt*1.6);po.fr=(Math.floor(T*10+i)%2);if(po.t<=0||!dentro(f,po.x,po.y,4))pombos.splice(i,1)}}

    /* cachorro que acompanha um morador */
    atualizarCao(dt,v,povo);

    /* raposa: atravessa os campos; ovelhas, vacas e pombos se afastam */
    if(raposa){raposa.x+=raposa.dir*1.35*dt;raposa.fr=Math.floor(T*6)%2;raposa.y+=Math.sin(T*.8)*.1*dt;
      for(i=0;i<fa.length;i++){var ov=fa[i];if(ov.kind!=='sheep'&&ov.kind!=='cow')continue;var dx2=ov.x-raposa.x,dy2=ov.y-raposa.y,d2=Math.hypot(dx2,dy2);
        if(d2<3.2&&d2>0){var ax=ov.x+dx2/d2*3,ay=ov.y+dy2/d2*2;if(!H.agua(ax,ay)){ov.tx=ax;ov.ty=ay;ov.st='walk'}}}
      if((raposa.dir>0&&raposa.x>f.x1+3)||(raposa.dir<0&&raposa.x<f.x0-3)||T>raposa.fim)raposa=null}
    /* cervos se afastam de quem passa perto */
    for(i=0;i<fa.length;i++){var ce=fa[i];if(ce.kind!=='deer'||ce.st==='walk')continue;
      for(j=0;j<povo.length;j++){var pw=povo[j];if(!pw.x)continue;var dx3=ce.x-pw.x,dy3=ce.y-pw.y,d3=Math.hypot(dx3,dy3);
        if(d3<1.9&&d3>0){var bx=ce.x+dx3/d3*3.2,by=ce.y+dy3/d3*2;if(!H.agua(bx,by)&&!H.rua(bx,by)){ce.tx=bx;ce.ty=by;ce.st='walk'}break}}}

    /* balão */
    if(balao){balao.x+=balao.vx*dt;balao.y+=balao.vy*dt;if(T>balao.fim)balao=null}
    /* barco à vela */
    if(barco){var vb=.55*dt,nx2=barco.x+barco.d[0]*vb,ny2=barco.y+barco.d[1]*vb;
      if(!H.agua(barco.x+barco.d[0]*.9,barco.y+barco.d[1]*.9)){var opts=[[1,0],[-1,0],[0,1],[0,-1]].filter(function(d){return !(d[0]===-barco.d[0]&&d[1]===-barco.d[1])&&H.agua(barco.x+d[0]*1.4,barco.y+d[1]*1.4)});
        if(opts.length)barco.d=pick(opts);else barco.d=[-barco.d[0],-barco.d[1]]}
      else{barco.x=nx2;barco.y=ny2}
      barco.esteira-=dt;if(barco.esteira<=0){barco.esteira=.3;ondas.push({x:barco.x-barco.d[0]*.7,y:barco.y-barco.d[1]*.5+.15,t:0,d:1.4,r:.5})}
      if(T>barco.fim||!dentro(f,barco.x,barco.y,12))barco=null}

    /* festa: balões e confete de dia, fogos à noite */
    if(festa&&T>festa.prox){
      if(festa.noite){festa.prox=T+rnd(.6,1.5);var lx2=(f.x1-f.x0),ly2=(f.y1-f.y0),fx=clamp(festa.x+rnd(-2.5,2.5),f.x0+lx2*.12,f.x1-lx2*.12),fy=clamp(festa.y+rnd(-1.5,1.5),f.y0+ly2*.35,f.y1-ly2*.1),
          chao2=P(fx,fy).y,maxAlt=Math.max(2.2,(chao2-v.h*.14)/v.t);
        foguetes.push({x:fx,y:fy,alt:0,valt:rnd(7.5,9.5),topo:Math.min(rnd(5,8.5),maxAlt),cor:pick(CORES_FESTA),tipo:pick(['peonia','anel','salgueiro','peonia']),rastro:[]})}
      else{festa.prox=T+rnd(.25,.6);if(baloes.length<26)baloes.push({x:festa.x+rnd(-1.8,1.8),y:festa.y+rnd(-1,1),alt:rnd(0,.4),vy:rnd(.55,.95),cor:pick(CORES_FESTA.slice(0,6)),ph:R()*9});
        if(R()<.25)for(var cf=0;cf<Math.round(26*q);cf++)confete.push({x:festa.x+rnd(-.5,.5),y:festa.y+rnd(-.4,.4),alt:rnd(.5,1),vx:rnd(-1.6,1.6),vy:rnd(-1,1),valt:rnd(2,4),cor:pick(CORES_FESTA),t:0,ph:R()*9})}}
    for(i=baloes.length-1;i>=0;i--){var bl=baloes[i];bl.alt+=bl.vy*dt;bl.ph+=dt;bl.x+=(Math.sin(bl.ph*1.3)*.25+vento.x*.35)*dt;if(bl.alt>11)baloes.splice(i,1)}
    for(i=confete.length-1;i>=0;i--){var co=confete[i];co.t+=dt;co.valt-=4.5*dt;co.valt=Math.max(co.valt,-.7);co.alt+=co.valt*dt;co.x+=(co.vx+Math.sin(co.ph+T*5)*.3)*dt;co.y+=co.vy*dt*.4;co.vx*=.98;if(co.alt<0||co.t>4)confete.splice(i,1)}
    for(i=foguetes.length-1;i>=0;i--){var fg=foguetes[i];fg.alt+=fg.valt*dt;fg.rastro.push({x:fg.x,y:fg.y,alt:fg.alt,t:0});if(fg.rastro.length>8)fg.rastro.shift();
      if(fg.alt>=fg.topo){explodir(fg);foguetes.splice(i,1)}}
    for(i=faiscas.length-1;i>=0;i--){var fs=faiscas[i];fs.t+=dt;fs.valt-=(fs.pesada?2.6:1.5)*dt;fs.vx*=.985;fs.vy*=.985;fs.x+=fs.vx*dt;fs.y+=fs.vy*dt;fs.alt+=fs.valt*dt;if(fs.t>fs.d)faiscas.splice(i,1)}
    for(i=claroes.length-1;i>=0;i--){claroes[i].t+=dt;if(claroes[i].t>.45)claroes.splice(i,1)}

    /* estrelas cadentes (na tela) */
    if(L.escuro>.7&&o.eventos!==false&&(T>proxEstrela||(meteoros&&R()<dt*2.5))){proxEstrela=T+rnd(18,55);var dl2=R()<.5;
      estrelas.push({x:rnd(W*.1,W*.9),y:rnd(0,Hh*.45),vx:(dl2?-1:1)*rnd(W*.6,W*1.1),vy:rnd(Hh*.2,Hh*.4),t:0,d:rnd(.6,1.1)})}
    for(i=estrelas.length-1;i>=0;i--){estrelas[i].t+=dt;if(estrelas[i].t>estrelas[i].d)estrelas.splice(i,1)}

    /* moradores: um assobio aqui, um coração ali */
    if(o.moradores!==false&&v.z>=.45&&T>proxEmote){proxEmote=T+rnd(5,11);var vis=povo.filter(function(a){return a.x&&dentro(f,a.x,a.y,-1)&&!(a.pausa>0&&a.rota&&(a.s<=0||a.s>=a.rota.length-1))});
      if(vis.length){var escolhido=pick(vis);emotes.set(escolhido,{c:noite?pick(['z','♪','…']):chuva.k>.3?pick(['!','☂']):pick(['♪','♥','♪','?','!','☀']),fim:T+rnd(2.2,3.5)})}}
    emotes.forEach(function(e,a){if(T>e.fim)emotes.delete(a)});
  }
  function explodir(fg){
    var n=Math.round((fg.tipo==='salgueiro'?46:60)*(q*.6+.4)),cor2=pick(CORES_FESTA);
    claroes.push({x:fg.x,y:fg.y,alt:fg.alt,cor:fg.cor,t:0});
    for(var i=0;i<n;i++){var a=i/n*PI*2+rnd(-.05,.05),sp=fg.tipo==='anel'?3.2:rnd(1.2,3.6),elev=fg.tipo==='anel'?0:rnd(-1,1);
      faiscas.push({x:fg.x+Math.cos(a)*.15,y:fg.y+Math.sin(a)*.07,alt:fg.alt+Math.sin(a)*.1,vx:Math.cos(a)*sp,vy:Math.sin(a)*sp*.45,valt:Math.sin(a)*sp*.55+elev,cor:(i%3===0&&fg.tipo!=='salgueiro')?cor2:fg.cor,t:0,d:fg.tipo==='salgueiro'?rnd(2,2.8):rnd(1.1,1.8),pesada:fg.tipo==='salgueiro'})}
  }
  function atualizarCao(dt,v,povo){
    if(H.opcoes().moradores===false||v.z<.4){cao=null;return}
    var dentroDeCasa=function(a){return a.pausa>0&&a.rota&&(a.s<=0||a.s>=a.rota.length-1)};
    if(!cao||!cao.dono||T>cao.troca&&!dentroDeCasa(cao.dono)&&R()<.02){var cand=povo.filter(function(a){return a.tipo==='andarilho'&&a.x&&dentro(v.f,a.x,a.y,-1)&&!dentroDeCasa(a)});
      if(!cand.length){cao=null;return}var d=pick(cand);if(!cao)cao={x:d.x-.8,y:d.y+.2,fr:0,flip:false,senta:false};cao.dono=d;cao.troca=T+rnd(40,80)}
    var a=cao.dono;if(!a.x){cao=null;return}
    var alvoX=a.x-(a.dx||0)*.75-.25,alvoY=a.y-(a.dy||0)*.55+.18,dx=alvoX-cao.x,dy=alvoY-cao.y,d=Math.hypot(dx,dy);
    if(dentroDeCasa(a)){cao.senta=true}
    else if(d>.28){var vel=Math.min(d*2.4,2.6)*dt;cao.x+=dx/d*vel;cao.y+=dy/d*vel;cao.senta=false;if(Math.abs(dx)>.05)cao.flip=dx<0;cao.fr=Math.floor(T*8)%2}
    else cao.senta=a.pausa>0;
    if(!dentro(v.f,cao.x,cao.y,6))cao=null;
  }

  /* ---------------- lampiões nas ruas ---------------- */
  function listaLampioes(){var w=H.mundo();if(lampioes&&lampKey===w.roads.size)return lampioes;lampKey=w.roads.size;lampioes=[];
    w.roads.forEach(function(k){var i=k.indexOf(','),x=+k.slice(0,i),y=+k.slice(i+1);if(hash(x,y)%7!==0)return;
      var lados=[[1,0],[-1,0],[0,1],[0,-1]];for(var j=0;j<4;j++){var d=lados[(hash(y,x)+j)%4],nx=x+d[0],ny=y+d[1];if(w.roads.has(nx+','+ny)||H.agua(nx,ny)||H.casaEm(nx,ny))continue;
        lampioes.push({x:x+.5+d[0]*.62,y:y+.5+d[1]*.62,ph:hash(x,y)%100});break}});return lampioes}

  /* ---------------- desenho ---------------- */
  function chao(){  /* antes das árvores, bichos e moradores */
    var v=view(),f=v.f,s=v.s,z=v.z,L=LUZ;ctx.save();ctx.imageSmoothingEnabled=false;
    /* marolas */
    if(ondas.length){ctx.lineWidth=Math.max(1,s*.55);for(var i=0;i<ondas.length;i++){var o=ondas[i];if(o.t<0||!dentro(f,o.x,o.y,1))continue;var k=o.t/o.d,p=P(o.x,o.y),rx=o.r*v.t*(.3+k*.9);
      ctx.strokeStyle='rgba(225,238,255,'+((1-k)*.55).toFixed(3)+')';ctx.beginPath();ctx.ellipse(p.x,p.y,rx,rx*.42,0,0,PI*2);ctx.stroke()}}
    /* brilhos do sol na água */
    if(L.escuro<.5&&brilhos.length){for(i=0;i<brilhos.length;i++){var b=brilhos[i];if(!dentro(f,b.x,b.y))continue;var a=Math.sin(PI*b.t/b.d)*(1-L.escuro*1.6),pb=P(b.x,b.y),u=Math.max(1,Math.round(s*.9));if(a<=.05)continue;
      ctx.globalAlpha=a;ctx.fillStyle='#ffffff';ctx.fillRect(Math.round(pb.x),Math.round(pb.y),u,u);if(a>.7){ctx.fillRect(Math.round(pb.x-u),Math.round(pb.y),u,u);ctx.fillRect(Math.round(pb.x+u),Math.round(pb.y),u,u);ctx.fillRect(Math.round(pb.x),Math.round(pb.y-u),u,u);ctx.fillRect(Math.round(pb.x),Math.round(pb.y+u),u,u)}}
      ctx.globalAlpha=1}
    /* peixes pulando */
    for(i=0;i<peixes.length;i++){var pe=peixes[i],k2=pe.t/pe.d,px=pe.x+pe.dir*.9*k2,alt=Math.sin(PI*k2)*pe.alt,pp=P(px,pe.y);
      ctx.save();ctx.translate(pp.x,pp.y-alt*v.t);ctx.rotate((pe.dir>0?1:-1)*(k2-.5)*1.6);var im=sprite('peixe',PEIXE,PAL_PEIXE,pe.dir<0);ctx.drawImage(im,-im.width*s/2,-im.height*s/2,im.width*s,im.height*s);ctx.restore()}
    /* barco */
    if(barco&&z>=.35){var ib=sprite('barco',BARCO,PAL_BARCO,barco.d[0]<0),pb2=P(barco.x,barco.y),bob=Math.sin(T*2)*s*.5;
      ctx.fillStyle='rgba(20,40,60,.22)';ctx.beginPath();ctx.ellipse(pb2.x,pb2.y+s,ib.width*s*.5,s*1.4,0,0,PI*2);ctx.fill();
      ctx.drawImage(ib,Math.round(pb2.x-ib.width*s*.5),Math.round(pb2.y-ib.height*s+s*2+bob),Math.ceil(ib.width*s),Math.ceil(ib.height*s))}
    /* lampiões (poste) */
    if(z>=.45){var lamp=listaLampioes(),acesa=L.escuro>.32;for(i=0;i<lamp.length;i++){var l=lamp[i];if(!dentro(f,l.x,l.y,1))continue;var pl=P(l.x,l.y);
      ctx.fillStyle='rgba(0,0,0,.18)';ctx.fillRect(Math.round(pl.x-s),Math.round(pl.y),Math.ceil(s*2.5),Math.ceil(s*.8));
      ctx.fillStyle='#2e2a2a';ctx.fillRect(Math.round(pl.x),Math.round(pl.y-s*9),Math.ceil(s),Math.ceil(s*9));
      ctx.fillRect(Math.round(pl.x-s),Math.round(pl.y-s*11),Math.ceil(s*3),Math.ceil(s*2.2));ctx.fillStyle=acesa?'#ffe29a':'#d9d2b8';ctx.fillRect(Math.round(pl.x-s*.5),Math.round(pl.y-s*10.5),Math.ceil(s*2),Math.ceil(s*1.4))}}
    /* pombos no chão */
    for(i=0;i<pombos.length;i++){var po=pombos[i];if(po.st!=='chao'||!dentro(f,po.x,po.y,1))continue;desenha(sprite('pombo'+po.fr,po.fr?POMBO.bica:POMBO.em,PAL_POMBO,po.flip),po.x,po.y,s)}
    /* cachorro e raposa */
    if(cao){var ic=cao.senta?sprite('cao-s',CAO.senta,PAL_CAO,cao.flip):sprite('cao'+cao.fr,cao.fr?CAO.b:CAO.a,PAL_CAO,cao.flip),pc=P(cao.x,cao.y);
      ctx.fillStyle='rgba(0,0,0,.2)';ctx.beginPath();ctx.ellipse(pc.x,pc.y,ic.width*s*.35,s*1.1,0,0,PI*2);ctx.fill();desenha(ic,cao.x,cao.y,s)}
    if(raposa&&dentro(f,raposa.x,raposa.y,2)){var ir=sprite('raposa'+raposa.fr,raposa.fr?RAPOSA.b:RAPOSA.a,PAL_RAPOSA,raposa.dir<0),pr=P(raposa.x,raposa.y);
      ctx.fillStyle='rgba(0,0,0,.2)';ctx.beginPath();ctx.ellipse(pr.x,pr.y,ir.width*s*.35,s*1.1,0,0,PI*2);ctx.fill();desenha(ir,raposa.x,raposa.y,s)}
    ctx.restore();
  }

  function ceu(){  /* por cima das casas, antes de escurecer */
    var v=view(),f=v.f,s=v.s,z=v.z,L=LUZ,W=v.w,Hh=v.h;ctx.save();ctx.imageSmoothingEnabled=false;
    /* fumaça */
    for(var i=0;i<fumaca.length;i++){var fu=fumaca[i];if(!dentro(f,fu.x,fu.y,2))continue;var k=fu.t/fu.d,pf=P(fu.x,fu.y),r=(fu.r0+fu.t*.1)*v.t;
      ctx.globalAlpha=(k<.15?k/.15:1-k)*.34;ctx.fillStyle=L.escuro>.5?'#9aa0b4':'#ececec';ctx.beginPath();ctx.arc(pf.x,pf.y,r,0,PI*2);ctx.fill()}
    ctx.globalAlpha=1;
    /* folhas e rajadas */
    for(i=0;i<folhas.length;i++){var fo=folhas[i];if(!dentro(f,fo.x,fo.y,1))continue;var pfo=P(fo.x,fo.y),u=Math.max(1,Math.round(s));
      ctx.globalAlpha=fo.alt<0?Math.max(0,1+fo.alt/.8):1;ctx.fillStyle=fo.cor;var vira=Math.floor(T*4+fo.ph)%2;ctx.fillRect(Math.round(pfo.x),Math.round(pfo.y-Math.max(0,fo.alt)*v.t),vira?u*2:u,vira?u:u*2)}
    ctx.globalAlpha=1;
    if(rajadas.length){ctx.strokeStyle='rgba(255,255,255,.35)';ctx.lineWidth=Math.max(1,s*.5);for(i=0;i<rajadas.length;i++){var ra=rajadas[i],kr=ra.t/ra.d,p1=P(ra.x,ra.y),comp=ra.l*v.t*Math.sin(PI*kr),ang=Math.atan2(vento.y,vento.x);
      ctx.globalAlpha=Math.sin(PI*kr)*.8;ctx.beginPath();ctx.moveTo(p1.x,p1.y);ctx.lineTo(p1.x-Math.cos(ang)*comp,p1.y-Math.sin(ang)*comp);ctx.stroke()}ctx.globalAlpha=1}
    /* borboletas */
    for(i=0;i<borboletas.length;i++){var bo=borboletas[i];if(!dentro(f,bo.x,bo.y,1))continue;var pbo=P(bo.x,bo.y),yy=pbo.y-bo.alt*v.t,u2=Math.max(1.5,Math.round(s)),ab=Math.floor(bo.ph*13)%2;
      ctx.fillStyle=bo.cor;if(ab){ctx.fillRect(Math.round(pbo.x-u2*2),Math.round(yy-u2),u2*2,u2*2);ctx.fillRect(Math.round(pbo.x+u2),Math.round(yy-u2),u2*2,u2*2)}else{ctx.fillRect(Math.round(pbo.x-u2),Math.round(yy-u2),u2,u2*2);ctx.fillRect(Math.round(pbo.x+u2),Math.round(yy-u2),u2,u2*2)}
      ctx.fillStyle='#3a2e28';ctx.fillRect(Math.round(pbo.x),Math.round(yy-u2),u2,u2*2)}
    /* pombos voando */
    for(i=0;i<pombos.length;i++){var po=pombos[i];if(po.st!=='voo')continue;var pp=P(po.x,po.y),im=sprite('pv'+po.fr,po.fr?POMBO.voo1:POMBO.voo2,PAL_POMBO,po.flip);
      ctx.fillStyle='rgba(0,0,0,.14)';ctx.fillRect(Math.round(pp.x-s*2),Math.round(pp.y),Math.ceil(s*4),Math.ceil(s));ctx.drawImage(im,Math.round(pp.x-im.width*s/2),Math.round(pp.y-po.alt*v.t-im.height*s),Math.ceil(im.width*s),Math.ceil(im.height*s))}
    /* moradores: guarda-chuvas e balõezinhos */
    var povo=H.povo(),sv=TILE*z/20;
    if(z>=.38&&(chuva.k>.25||emotes.size)){for(i=0;i<povo.length;i++){var a=povo[i];if(!a.x||!dentro(f,a.x,a.y,1))continue;if(a.pausa>0&&a.rota&&(a.s<=0||a.s>=a.rota.length-1))continue;var pa=P(a.x,a.y);
      if(chuva.k>.25&&!chuva.neve){var cu=['#d9483b','#3d7dd8','#f4d35e','#2a9d8f','#6c4ab6'][hash(a.look&&a.look.shirt?a.look.shirt.length:i,i)%5],cy=pa.y-sv*20;
        ctx.strokeStyle='#3a2e28';ctx.lineWidth=Math.max(1,sv*.8);ctx.beginPath();ctx.moveTo(pa.x+sv*3,cy);ctx.lineTo(pa.x+sv*3,cy+sv*7);ctx.stroke();
        ctx.fillStyle=cu;ctx.beginPath();ctx.ellipse(pa.x+sv*3,cy,sv*8,sv*4.2,0,PI,0);ctx.fill();ctx.fillStyle='rgba(0,0,0,.18)';ctx.fillRect(Math.round(pa.x+sv*3-sv*8),Math.round(cy-sv*.6),Math.ceil(sv*16),Math.ceil(sv*.8))}
      var e=emotes.get(a);if(e&&z>=.45&&!(a.conversa>0)){var bx=pa.x+sv*3,by=pa.y-sv*23,bw=sv*9,bh=sv*8,dur=e.fim-T,al=Math.min(1,dur*2,(T-(e.fim-3.5))*4+.2);
        ctx.globalAlpha=clamp(al,0,1);ctx.fillStyle='rgba(255,252,240,.96)';ctx.strokeStyle='rgba(40,30,25,.8)';ctx.lineWidth=Math.max(1,sv*.6);ctx.beginPath();if(ctx.roundRect)ctx.roundRect(bx,by-bh,bw,bh,sv*2.5);else ctx.rect(bx,by-bh,bw,bh);ctx.fill();ctx.stroke();
        ctx.beginPath();ctx.moveTo(bx+sv*1.5,by);ctx.lineTo(bx,by+sv*2);ctx.lineTo(bx+sv*3.5,by);ctx.fill();
        ctx.fillStyle=e.c==='♥'?'#d9483b':e.c==='☀'?'#e0a02a':'#3a2e28';ctx.font='700 '+Math.round(sv*6.4)+'px system-ui,sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(e.c,bx+bw/2,by-bh/2+sv*.3);ctx.globalAlpha=1}}}
    /* balões da festa e confete */
    for(i=0;i<baloes.length;i++){var bl=baloes[i],pbl=P(bl.x,bl.y),yb=pbl.y-bl.alt*v.t,ub=Math.max(1,s);
      ctx.strokeStyle='rgba(60,50,40,.6)';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(pbl.x,yb);ctx.lineTo(pbl.x+Math.sin(bl.ph*2)*ub*2,yb+ub*6);ctx.stroke();
      ctx.fillStyle=bl.cor;ctx.beginPath();ctx.ellipse(pbl.x,yb-ub*2,ub*2.2,ub*2.8,0,0,PI*2);ctx.fill();ctx.fillStyle='rgba(255,255,255,.55)';ctx.fillRect(Math.round(pbl.x-ub),Math.round(yb-ub*3.5),Math.ceil(ub),Math.ceil(ub))}
    for(i=0;i<confete.length;i++){var co=confete[i],pco=P(co.x,co.y),uc=Math.max(1,Math.round(s*.8));ctx.fillStyle=co.cor;ctx.fillRect(Math.round(pco.x),Math.round(pco.y-co.alt*v.t),Math.floor(T*9+co.ph)%2?uc*2:uc,uc)}
    /* balão de ar quente, com a sombra no chão */
    if(balao){var ib2=balaoImg(balao.cs),sc=s*1.25,pbh=P(balao.x,balao.y),bob=Math.sin(T*.9)*s*1.2;
      ctx.fillStyle='rgba(0,0,0,.16)';ctx.beginPath();ctx.ellipse(pbh.x+balao.alt*v.t*.25,pbh.y,ib2.width*sc*.4,ib2.width*sc*.16,0,0,PI*2);ctx.fill();
      ctx.drawImage(ib2,Math.round(pbh.x-ib2.width*sc/2),Math.round(pbh.y-balao.alt*v.t-ib2.height*sc+bob),Math.ceil(ib2.width*sc),Math.ceil(ib2.height*sc))}
    /* nuvens: sombras que passam sobre a cidade */
    if(nuvens.length){var an=(L.escuro>.5?.07:.11)*(1+chuva.k*1.4);ctx.globalAlpha=Math.min(.3,an);
      for(i=0;i<nuvens.length;i++){var nu=nuvens[i],pn=P(nu.x,nu.y),rw=nu.r*v.t*1.6,rh=nu.r*v.t;if(pn.x+rw<0||pn.x-rw>W||pn.y+rh<0||pn.y-rh>Hh)continue;ctx.drawImage(nuvemImg(nu.v),pn.x-rw,pn.y-rh,rw*2,rh*2)}ctx.globalAlpha=1}
    /* neblina */
    if(neblina.k>.02){ctx.globalAlpha=Math.min(.55,neblina.k*.5);for(i=0;i<neblinas.length;i++){var nb=neblinas[i],pnb=P(nb.x,nb.y),rw2=nb.r*v.t*1.8,rh2=nb.r*v.t;ctx.drawImage(nuvemImg(nb.v,true),pnb.x-rw2,pnb.y-rh2,rw2*2,rh2*2)}
      ctx.globalAlpha=neblina.k*.12;ctx.fillStyle='#e8eef4';ctx.fillRect(0,0,W,Hh);ctx.globalAlpha=1}
    /* chuva ou neve */
    if(gotas.length){if(chuva.neve){ctx.fillStyle='rgba(255,255,255,.85)';for(i=0;i<gotas.length;i++){var g=gotas[i],ug=g.l>13?2:1;ctx.fillRect(Math.round(g.x),Math.round(g.y),ug+1,ug+1)}}
      else{ctx.strokeStyle='rgba(196,210,235,.5)';ctx.lineWidth=1;ctx.beginPath();for(i=0;i<gotas.length;i++){var gg=gotas[i];ctx.moveTo(gg.x,gg.y);ctx.lineTo(gg.x-vento.x*gg.l*.35,gg.y-gg.l)}ctx.stroke();
        ctx.fillStyle='rgba(215,228,245,.55)';for(i=0;i<respingos.length;i++){var rp=respingos[i],kk=rp.t/.25;ctx.fillRect(Math.round(rp.x-2-kk*2),Math.round(rp.y),1,1);ctx.fillRect(Math.round(rp.x+2+kk*2),Math.round(rp.y),1,1);ctx.fillRect(Math.round(rp.x),Math.round(rp.y-2-kk),1,1)}}}
    /* arco-íris depois da chuva */
    if(arco&&T>arco.t0){var ka=Math.min(1,(T-arco.t0)/6,(arco.fim-T)/8),cxA=W*arco.cx,cyA=Hh*1.08,RA=Math.max(W,Hh)*.78,lw=RA*.028,cores=['255,70,70','255,160,60','255,230,80','90,210,110','70,160,255','110,90,220','170,90,210'];
      ctx.lineWidth=lw;for(i=0;i<cores.length;i++){ctx.strokeStyle='rgba('+cores[i]+','+(.2*ka).toFixed(3)+')';ctx.beginPath();ctx.arc(cxA,cyA,RA-i*lw,PI*1.06,PI*1.94);ctx.stroke()}}
    ctx.restore();
  }

  function brilhoPasso(){  /* depois de escurecer: tudo que acende */
    var v=view(),f=v.f,s=v.s,z=v.z,L=LUZ,W=v.w,Hh=v.h,esc=L.escuro;ctx.save();ctx.globalCompositeOperation='lighter';
    /* lampiões */
    if(esc>.32&&z>=.45){var kL=Math.min(1,(esc-.32)/.35),lamp=listaLampioes();for(var i=0;i<lamp.length;i++){var l=lamp[i];if(!dentro(f,l.x,l.y,2))continue;var pl=P(l.x,l.y),fl=.92+.08*Math.sin(T*9+l.ph);
      brilho('255,196,110',pl.x+s*.5,pl.y-s*10,s*9,.75*kL*fl);brilho('255,170,90',pl.x+s*.5,pl.y-s*1,s*16,.28*kL*fl)}}
    /* lanternas de quem anda à noite */
    if(esc>.55&&z>=.38){var povo=H.povo(),sv=TILE*z/20;for(i=0;i<povo.length;i++){var a=povo[i];if(!a.x||!dentro(f,a.x,a.y,1)||a.pausa>0)continue;var pa=P(a.x,a.y);
      brilho('255,190,105',pa.x+sv*5,pa.y-sv*8,sv*11,.55*(esc-.4))}}
    /* vaga-lumes */
    for(i=0;i<vagalumes.length;i++){var va=vagalumes[i];if(!dentro(f,va.x,va.y))continue;var pv=P(va.x,va.y),b=Math.pow((Math.sin(T*2.1+va.ph)+1)/2,3)*Math.min(1,va.t,(va.vida-va.t));if(b<.04)continue;
      var yv=pv.y-va.alt*v.t;brilho('205,255,120',pv.x,yv,s*5,b*.9);ctx.globalAlpha=Math.min(1,b*1.4);ctx.fillStyle='#f4ffc8';ctx.fillRect(Math.round(pv.x),Math.round(yv),Math.max(1,Math.round(s*.8)),Math.max(1,Math.round(s*.8)))}
    ctx.globalAlpha=1;
    /* brilho da lua na água */
    if(esc>.6){for(i=0;i<brilhos.length;i++){var bb=brilhos[i];if(!dentro(f,bb.x,bb.y))continue;var ab=Math.sin(PI*bb.t/bb.d)*.55,pb=P(bb.x,bb.y);if(ab<.05)continue;ctx.globalAlpha=ab;ctx.fillStyle='#bcd2ff';ctx.fillRect(Math.round(pb.x),Math.round(pb.y),Math.max(1,Math.round(s*.9)),Math.max(1,Math.round(s*.9)))}ctx.globalAlpha=1}
    /* fogos */
    for(i=0;i<foguetes.length;i++){var fg=foguetes[i];for(var j=0;j<fg.rastro.length;j++){var r=fg.rastro[j],pr=P(r.x,r.y);brilho('255,220,160',pr.x,pr.y-r.alt*v.t,Math.max(3,s*2.6),(j+1)/fg.rastro.length*.8)}}
    for(i=0;i<claroes.length;i++){var c=claroes[i],pc=P(c.x,c.y);brilho(hexRgb(c.cor),pc.x,pc.y-c.alt*v.t,Math.max(50,v.t*5),(1-c.t/.45)*.32);brilho(hexRgb(c.cor),pc.x,pc.y,Math.max(70,v.t*7),(1-c.t/.45)*.22)}
    var rf=Math.max(5,s*5),nuc=Math.max(2,Math.round(s*1.1));ctx.lineCap='round';
    for(i=0;i<faiscas.length;i++){var fs=faiscas[i],k=fs.t/fs.d,pf=P(fs.x,fs.y),a2=(1-k*k)*(k<.06?k/.06:1);if(a2<.03)continue;var yy=pf.y-fs.alt*v.t,cr=hexRgb(fs.cor);
      /* rastro: de onde a faísca vinha até onde está */
      var tx=pf.x-fs.vx*v.t*.09,ty=yy-(fs.vy-fs.valt)*v.t*.09;ctx.globalAlpha=a2*.8;ctx.strokeStyle='rgb('+cr+')';ctx.lineWidth=Math.max(1.2,s*.7);ctx.beginPath();ctx.moveTo(tx,ty);ctx.lineTo(pf.x,yy);ctx.stroke();
      brilho(cr,pf.x,yy,rf*(fs.pesada?.8:1),a2);ctx.globalAlpha=a2;ctx.fillStyle=(k>.08&&k<.4)?'#ffffff':'rgb('+cr+')';ctx.fillRect(Math.round(pf.x-nuc/2),Math.round(yy-nuc/2),nuc,nuc)}
    ctx.globalAlpha=1;
    /* estrelas cadentes */
    for(i=0;i<estrelas.length;i++){var e=estrelas[i],ke=e.t/e.d,x=e.x+e.vx*e.t,y=e.y+e.vy*e.t,cauda=.16,g=ctx.createLinearGradient(x,y,x-e.vx*cauda,y-e.vy*cauda),ae=Math.sin(PI*ke);
      g.addColorStop(0,'rgba(255,255,255,'+(.95*ae).toFixed(3)+')');g.addColorStop(1,'rgba(180,200,255,0)');ctx.strokeStyle=g;ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x-e.vx*cauda,y-e.vy*cauda);ctx.stroke();brilho('220,230,255',x,y,6,ae)}
    ctx.restore();
  }
  var RGB={};function hexRgb(h){if(RGB[h])return RGB[h];var n=parseInt(h.slice(1),16);return(RGB[h]=((n>>16)&255)+','+((n>>8)&255)+','+(n&255))}

  /* ---------------- relógio ---------------- */
  function tick(){
    if(!H.visivel()){ult=0;atualizarAviso();return}
    var agora=performance.now();if(!ult)ult=agora;var dt=Math.min(.1,(agora-ult)/1000);
    /* qualidade adaptativa: se o aparelho não acompanha, menos partículas */
    var intervalo=agora-ult;ult=agora;lagMedio=lagMedio*.92+intervalo*.08;lagT+=dt;
    if(lagT>2){lagT=0;if(lagMedio>58)q=Math.max(.35,q-.15);else if(lagMedio<40)q=Math.min(1,q+.05)}
    T+=dt;atualizarLuz();
    var v=view();if(v.z<.12){atualizarAviso();return}
    passo(dt,v);atualizarAviso();H.animar();
  }
  var vida={chao:chao,ceu:ceu,brilho:brilhoPasso,luz:function(){return LUZ},
    /* para testes e para quem quiser provocar um evento (paleta de comandos) */
    evento:function(tipo){iniciar(tipo,view());return true},
    estado:function(){return{q:q,hora:LUZ.hora,escuro:LUZ.escuro,chuva:chuva.k,neve:chuva.neve,neblina:neblina.k,arco:!!arco,balao:!!balao,barco:!!barco,raposa:!!raposa,raposaXY:raposa?[raposa.x,raposa.y,raposa.dir]:null,cao:!!cao,festa:festa&&festa.nome,
      nuvens:nuvens.length,gotas:gotas.length,ondas:ondas.length,peixes:peixes.length,borboletas:borboletas.length,vagalumes:vagalumes.length,fumaca:fumaca.length,pombos:pombos.length,pombosVoando:pombos.filter(function(p){return p.st==='voo'}).length,folhas:folhas.length,faiscas:faiscas.length,estrelas:estrelas.length,lampioes:lampioes?lampioes.length:0}},
    _passo:function(dt){T+=dt;atualizarLuz();passo(dt,view())},_luzDa:luzDa};
  atualizarLuz();H.registrar(vida);global.UrbeVida=vida;
  var sch=core.service('scheduler');if(sch)sch.add('world.life',tick,{interval:33,whenVisible:true});else setInterval(tick,33);
  if(core.commands&&core.commands.register){
    [['chuva','Chuva passageira'],['neblina','Neblina'],['balao','Balão de ar quente'],['festa','Festa no bairro'],['raposa','Raposa nos campos'],['meteoros','Chuva de estrelas cadentes'],['revoada','Revoada'],['barco','Barco à vela']].forEach(function(e){
      core.commands.register('world.event.'+e[0],{title:'Cidade: '+e[1],category:'Cidade',execute:function(){iniciar(e[0],view())}})});
  }
})(window);
