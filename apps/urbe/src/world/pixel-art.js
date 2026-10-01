(function(global){
  'use strict';
  /* Arte pixel do mundo, gerada em código com uma paleta medieval única.
     Resolução de trabalho: 16 px por tile (o jogo desenha em 2× ou mais, sem suavização).
     - chão: texturas por bioma, bordas orgânicas entre biomas, relevo sombreado,
       profundidade e espuma na água, detalhes (flores, pedras, juncos) já embutidos;
     - vegetação: árvores por espécie, com variações e neve;
     - construções: casas e ofícios medievais que mudam de material conforme o bioma. */
  var PX=16;
  function rgb(h){h=h.replace('#','');return[parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)]}
  function hash(x,y,s){var h=Math.imul(x|0,374761393)+Math.imul(y|0,668265263)+Math.imul(s|0,2246822519);h=Math.imul(h^(h>>>13),1274126177);return((h^(h>>>16))>>>0)/4294967296}
  function canvas(w,h){if(typeof OffscreenCanvas!=='undefined'){try{return new OffscreenCanvas(w,h)}catch(_){}}var c=document.createElement('canvas');c.width=w;c.height=h;return c}

  /* ---------- paleta por bioma: base, sombras, luzes, detalhe ---------- */
  var PAL={
    deep:['#23466f','#1f3f66','#284f7a','#2c5683'],
    sea:['#3b77ad','#356ea3','#4282b8','#5594c6'],
    river:['#4a8cc2','#4383ba','#5597cb','#79b3dc'],
    lake:['#4a8cc2','#4383ba','#5597cb','#79b3dc'],
    beach:['#e2d09b','#d6c38b','#ead9a8','#c9b47a'],
    grass:['#6e9a47','#628c3f','#7aa652','#86b25b'],
    meadow:['#7aa550','#6d9846','#86b15a','#93bd63'],
    forest:['#557f38','#4a7331','#608b41','#3f6a2c'],
    dense:['#436b2e','#3a6128','#4c7634','#335a24'],
    swamp:['#5c6a41','#525f39','#66744a','#48675e'],
    taiga:['#5a7250','#506848','#657d59','#46604a'],
    tundra:['#9ba48c','#8f9881','#a7b097','#b8bfab'],
    snow:['#eef2f5','#e1e8ee','#f7f9fb','#cfd9e2'],
    hills:['#86934f','#7a8747','#929f59','#6f7b43'],
    mountain:['#8b8279','#7c736b','#9a9188','#6a625b'],
    peak:['#b8b3ad','#a8a29c','#e9edf0','#948e88'],
    desert:['#dcc487','#d0b779','#e6d196','#c3a86a'],
    savanna:['#c3b264','#b6a45a','#cfbe70','#a99650'],
    steppe:['#aead6f','#a0a063','#bab97b','#939556']
  };
  var IDS=['deep','sea','river','lake','beach','grass','meadow','forest','dense','swamp','taiga','tundra','snow','hills','mountain','peak','desert','savanna','steppe'];
  var WATER={deep:1,sea:1,river:1,lake:1};

  /* ---------- texturas 16×16 (4 variações por bioma) ---------- */
  var TEX={};
  function makeTexture(id,variant){
    var p=PAL[id].map(rgb),d=new Uint8ClampedArray(PX*PX*4),s=IDS.indexOf(id)*97+variant*13;
    for(var y=0;y<PX;y++)for(var x=0;x<PX;x++){
      var r=hash(x,y,s),c=p[0];
      if(WATER[id]){ /* ondulações horizontais curtas */
        var wave=(y+Math.floor(hash(Math.floor(x/5),y,s)*3))%5===0&&hash(x,y,s+1)<.55;c=wave?p[3]:(r<.18?p[1]:r>.9?p[2]:p[0]);
      }else if(id==='snow'||id==='peak'){c=r<.12?p[1]:r>.94?p[2]:p[0];if(id==='peak'&&hash(x>>2,y>>2,s)<.35)c=p[r<.5?3:1]}
      else if(id==='mountain'||id==='hills'){c=r<.2?p[1]:r>.85?p[2]:p[0];if(hash(x>>1,y>>2,s+3)<.08)c=p[3]}
      else if(id==='beach'||id==='desert'){c=r<.14?p[1]:r>.9?p[2]:p[0];if(hash(x,y,s+9)<.03)c=p[3];if(id==='desert'&&(y+Math.floor(x/4))%7===0&&r<.5)c=p[2]}
      else{ /* vegetação rasteira: tufos de 1×2 px */
        c=r<.2?p[1]:r>.86?p[2]:p[0];
        if(hash(x,y>>1,s+5)<.07)c=p[3];
        if(y>0&&hash(x,y-1,s+5)<.07&&hash(x,y,s+6)<.5)c=p[1];
      }
      var o=(y*PX+x)*4;d[o]=c[0];d[o+1]=c[1];d[o+2]=c[2];d[o+3]=255;
    }
    return d;
  }
  IDS.forEach(function(id){TEX[id]=[0,1,2,3].map(function(v){return makeTexture(id,v)})});

  /* ---------- personalização: paleta e texturas próprias por bioma ----------
     cfg.palette  {bioma:['#base','#sombra','#luz','#detalhe']}  (cores faltando mantêm as originais)
     cfg.textures {bioma:[RGBA 16×16 (1024 valores), até 4 variações]}
     Chamado igual na página e no worker, para os dois pintarem o mesmo chão. */
  var PAL0={};IDS.forEach(function(id){PAL0[id]=PAL[id].slice()});
  function isHex(c){return typeof c==='string'&&/^#[0-9a-f]{6}$/i.test(c)}
  function configure(cfg){
    cfg=cfg||{};var pal=cfg.palette||{},tex=cfg.textures||{};
    IDS.forEach(function(id){
      var p=pal[id],base=PAL0[id];
      /* só a cor base? sombra, luz e detalhe saem dela nas mesmas proporções da paleta original */
      PAL[id]=[0,1,2,3].map(function(i){if(p&&isHex(p[i]))return p[i];if(!(p&&isHex(p[0])))return base[i];
        var b0=rgb(base[0]),bi=rgb(base[i]),c=rgb(p[0]);return'#'+c.map(function(v,k){var f=b0[k]?bi[k]/b0[k]:1;return('0'+Math.max(0,Math.min(255,Math.round(v*f))).toString(16)).slice(-2)}).join('')});
      var t=Array.isArray(tex[id])?tex[id].filter(function(a){return a&&a.length===PX*PX*4}):[];
      TEX[id]=[0,1,2,3].map(function(v){if(t.length){var d=new Uint8ClampedArray(t[v%t.length]);for(var o=3;o<d.length;o+=4)d[o]=255;return d}return makeTexture(id,v)});
    });
  }

  /* ---------- chão de um chunk ---------- */
  /* pixels RGBA do chunk; roda tanto na página quanto no worker */
  function chunkPixels(world,cx,cy){
    var CH=world.CH,N=CH*PX,out=new Uint8ClampedArray(N*N*4);
    var x0=cx*CH,y0=cy*CH,W=CH+2,bio=new Array(W*W),elev=new Float32Array(W*W);
    for(var j=-1;j<=CH;j++)for(var i=-1;i<=CH;i++){bio[(j+1)*W+i+1]=IDS[world.biome(x0+i,y0+j)];elev[(j+1)*W+i+1]=world.elevation(x0+i,y0+j)}
    /* sombreamento por declive (luz vinda do noroeste), por tile */
    var slope=new Float32Array(W*W);
    for(j=0;j<W;j++)for(i=0;i<W;i++){var a=elev[Math.max(0,j-1)*W+Math.max(0,i-1)],b=elev[Math.min(W-1,j+1)*W+Math.min(W-1,i+1)];slope[j*W+i]=(a-b)}
    var sea=world.sea;
    /* manchas amplas de tom (ruído de valor em células de 7 tiles) para o chão não ficar chapado */
    function vn(x,y){var X=Math.floor(x),Y=Math.floor(y),fx=x-X,fy=y-Y,u=fx*fx*(3-2*fx),v=fy*fy*(3-2*fy),a=hash(X,Y,91),b=hash(X+1,Y,91),c=hash(X,Y+1,91),d=hash(X+1,Y+1,91);return a+(b-a)*u+(c-a)*v+(a-b-c+d)*u*v}
    var tint=new Float32Array(W*W);for(j=0;j<W;j++)for(i=0;i<W;i++){var tx0=x0+i-1,ty0=y0+j-1;tint[j*W+i]=(vn(tx0/7,ty0/7)-.5)*.16+(vn(tx0/2.5+50,ty0/2.5)-.5)*.05}
    var ROCKY={mountain:1,peak:1,hills:1,snow:1};
    var IDX={};IDS.forEach(function(id,n){IDX[id]=n});
    /* Divisas orgânicas: cada pixel consulta o bioma numa posição deformada por
       ruído suave (≈1 tile de amplitude), então as bordas viram curvas em vez de
       degraus da grade. Entre biomas de terra as texturas se misturam (ecótono);
       água × terra fica nítida, com espuma seguindo a costa. */
    function vns(x,y,s){var X=Math.floor(x),Y=Math.floor(y),fx=x-X,fy=y-Y,u=fx*fx*(3-2*fx),v=fy*fy*(3-2*fy),a=hash(X,Y,s),b=hash(X+1,Y,s),c=hash(X,Y+1,s),d=hash(X+1,Y+1,s);return a+(b-a)*u+(c-a)*v+(a-b-c+d)*u*v}
    function sm(t){t=t<0?0:t>1?1:t;return t*t*(3-2*t)}
    var VAR=new Uint8Array(W*W);for(j=0;j<W;j++)for(i=0;i<W;i++)VAR[j*W+i]=Math.floor(hash(x0+i-1,y0+j-1,5)*4);
    function bioAt(u,v){var iu=Math.floor(u),iv=Math.floor(v);if(iu<-1)iu=-1;if(iu>CH)iu=CH;if(iv<-1)iv=-1;if(iv>CH)iv=CH;return(iv+1)*W+iu+1}
    for(var ty=0;ty<CH;ty++)for(var tx=0;tx<CH;tx++){
      var k=(ty+1)*W+tx+1,id=bio[k],wx=x0+tx,wy=y0+ty;
      var nL=bio[k-1],nR=bio[k+1],nU=bio[k-W],nD=bio[k+W],nUL=bio[k-W-1],nUR=bio[k-W+1],nDL=bio[k+W-1],nDR=bio[k+W+1];
      var uniform=nL===id&&nR===id&&nU===id&&nD===id&&nUL===id&&nUR===id&&nDL===id&&nDR===id,own=TEX[id][VAR[k]];
      var s00=slope[k-W-1]+slope[k-W]+slope[k-1]+slope[k],s10=slope[k-W]+slope[k-W+1]+slope[k]+slope[k+1],s01=slope[k-1]+slope[k]+slope[k+W-1]+slope[k+W],s11=slope[k]+slope[k+1]+slope[k+W]+slope[k+W+1];
      var t00=tint[k-W-1]+tint[k-W]+tint[k-1]+tint[k],t10=tint[k-W]+tint[k-W+1]+tint[k]+tint[k+1],t01=tint[k-1]+tint[k]+tint[k+W-1]+tint[k+W],t11=tint[k]+tint[k+1]+tint[k+W]+tint[k+W+1];
      for(var py=0;py<PX;py++){
        var fy=(py+.5)/PX,row=((ty*PX+py)*N+tx*PX)*4;
        var sl0=s00+(s01-s00)*fy,sl1=s10+(s11-s10)*fy,tn0=t00+(t01-t00)*fy,tn1=t10+(t11-t10)*fy;
        for(var px=0;px<PX;px++){
          var o=(py*PX+px)*4,to=row+px*4,fx=(px+.5)/PX,r,g,bl,shade,water,rocky=ROCKY[id]?10:5;
          if(uniform){r=own[o];g=own[o+1];bl=own[o+2];water=!!WATER[id];if(water){var dp=Math.max(0,Math.min(1,(sea-elev[k])/.12));shade=-dp*.28}}
          else{
            var gx=wx*PX+px,gy=wy*PX+py;
            var uu=tx+fx+(vns(gx/24,gy/24,301)-.5)*.7+(vns(gx/11,gy/11,303)-.5)*.12,vv=ty+fy+(vns(gx/24+57,gy/24-31,302)-.5)*.7+(vns(gx/11-19,gy/11+23,304)-.5)*.12;
            var cu=uu-.5,cv=vv-.5,iu=Math.floor(cu),iv=Math.floor(cv);if(iu<-1)iu=-1;if(iu>CH-1)iu=CH-1;if(iv<-1)iv=-1;if(iv>CH-1)iv=CH-1;
            var fu=Math.max(0,Math.min(1,cu-iu)),fv=Math.max(0,Math.min(1,cv-iv));
            var ks=[(iv+1)*W+iu+1,(iv+1)*W+iu+2,(iv+2)*W+iu+1,(iv+2)*W+iu+2],wp=[(1-fu)*(1-fv),fu*(1-fv),(1-fu)*fv,fu*fv];
            /* água: campo contínuo (fração de água interpolada entre os centros dos tiles) → margens curvas */
            var wv=0,bestW=-1,kw=-1;for(var q=0;q<4;q++){if(WATER[bio[ks[q]]]){wv+=wp[q];if(wp[q]>bestW){bestW=wp[q];kw=ks[q]}}}
            if(wv>0&&wv<1)wv+=(vns(gx/9,gy/9,305)-.5)*.16;
            water=wv>.5;
            if(water){var tw=TEX[bio[kw]][VAR[kw]];r=tw[o];g=tw[o+1];bl=tw[o+2];shade=-Math.max(0,Math.min(1,(sea-elev[kw])/.12))*.28;
              if(wv<.57&&hash(gx,gy,78)<.75){r=236;g=244;bl=246;shade=0}else if(wv<.72)shade+=.12}
            else{ /* terra: mistura das texturas dos tiles de terra vizinhos, transição suave (ecótono) */
              var au=sm(fu),av=sm(fv),ws=[(1-au)*(1-av),au*(1-av),(1-au)*av,au*av],sr=0,sg=0,sb=0,sw=0,rk=0;
              for(q=0;q<4;q++){var bq=bio[ks[q]],wq=ws[q];if(WATER[bq]||wq<=0)continue;var tq=TEX[bq][VAR[ks[q]]];sr+=tq[o]*wq;sg+=tq[o+1]*wq;sb+=tq[o+2]*wq;sw+=wq;rk+=(ROCKY[bq]?10:5)*wq}
              if(sw>0){r=sr/sw;g=sg/sw;bl=sb/sw;rocky=rk/sw}
              else{var kl=ks[0];for(q=0;q<4;q++)if(!WATER[bio[ks[q]]])kl=ks[q];var tn=TEX[bio[kl]][VAR[kl]];r=tn[o];g=tn[o+1];bl=tn[o+2]}
            }
          }
          if(!water){var sl=(sl0+(sl1-sl0)*fx)*.25,tn2=(tn0+(tn1-tn0)*fx)*.25;shade=sl*rocky;shade=(shade>.3?.3:shade<-.3?-.3:shade)+tn2}
          var f=1+shade;out[to]=r*f;out[to+1]=g*f;out[to+2]=bl*f;out[to+3]=255;
        }
      }
      /* detalhes embutidos no chão */
      var dec=world.decor(wx,wy);if(dec){if(typeof dec==='string')dec=[dec];for(var di=0;di<dec.length;di++)paintDecor(out,N,tx*PX,ty*PX,dec[di],wx,wy,di)}
    }
    return out;
  }
  function renderChunk(world,cx,cy){var N=world.CH*PX,c=canvas(N,N),x=c.getContext('2d'),img=x.createImageData(N,N);img.data.set(chunkPixels(world,cx,cy));x.putImageData(img,0,0);return c}
  function canvasFromPixels(px,N){var c=canvas(N,N),x=c.getContext('2d'),img=x.createImageData(N,N);img.data.set(px);x.putImageData(img,0,0);return c}
  function put(out,N,x,y,c){if(x<0||y<0||x>=N||y>=N)return;var o=(y*N+x)*4;out[o]=c[0];out[o+1]=c[1];out[o+2]=c[2];out[o+3]=255}
  var FLOWERS=[rgb('#f2d34f'),rgb('#e8e8f0'),rgb('#d9687a'),rgb('#9b7fe0'),rgb('#f29a4f')];
  var C={};function col(h){return C[h]||(C[h]=rgb(h))}
  function blob(out,N,cx,cy,rx,ry,cols,sh){ /* forma arredondada com luz a noroeste e sombra no chão */
    if(sh)for(var y=-ry;y<=ry;y++)for(var x=-rx;x<=rx;x++){if((x*x)/(rx*rx)+(y*y)/(ry*ry)>1)continue;dim(out,N,cx+x+1,cy+y+1,.78)}
    for(y=-ry;y<=ry;y++)for(x=-rx;x<=rx;x++){var d=(x*x)/(rx*rx)+(y*y)/(ry*ry);if(d>1)continue;var l=(-x/rx-y/ry);put(out,N,cx+x,cy+y,l>.55?cols[2]:l<-.5||d>.8&&y>0?cols[0]:cols[1])}}
  function dim(out,N,x,y,f){if(x<0||y<0||x>=N||y>=N)return;var o=(y*N+x)*4;out[o]*=f;out[o+1]*=f;out[o+2]*=f}
  function paintDecor(out,N,ox,oy,kind,wx,wy,slot){
    var r=function(s){return hash(wx,wy,s+(slot||0)*50)},i,x,y;
    if(kind==='flowers'){var n=5+Math.floor(r(40)*5),pal=FLOWERS[Math.floor(r(41)*FLOWERS.length)];for(i=0;i<n;i++){var fx=ox+1+Math.floor(r(i+1)*14),fy=oy+1+Math.floor(r(i+9)*13),c2=r(i+30)<.7?pal:FLOWERS[Math.floor(r(i+30)*FLOWERS.length)];put(out,N,fx,fy,c2);put(out,N,fx,fy+1,col('#4f7a34'));if(r(i+60)<.4){put(out,N,fx+1,fy,c2);put(out,N,fx-1,fy,c2)}}}
    else if(kind==='rock'){var rx=ox+4+Math.floor(r(2)*6),ry=oy+5+Math.floor(r(3)*6),g1=col('#8f8a84'),g2=col('#6c6761'),g3=col('#b3aea8');
      for(y=0;y<4;y++)for(x=0;x<5;x++){if((x===0||x===4)&&(y===0||y===3))continue;put(out,N,rx+x,ry+y,y===0?g3:y===3||x===4?g2:g1)}dim(out,N,rx+5,ry+3,.75);dim(out,N,rx+2,ry+4,.75);dim(out,N,rx+3,ry+4,.75)}
    else if(kind==='boulder'){blob(out,N,ox+8,oy+8,5,4,[col('#6a645e'),col('#8c867f'),col('#b0aaa3')],true)}
    else if(kind==='pebbles'){for(i=0;i<3;i++){var px=ox+2+Math.floor(r(i+3)*12),py=oy+2+Math.floor(r(i+7)*12);put(out,N,px,py,col('#a39d94'));put(out,N,px+1,py,col('#8a847c'));dim(out,N,px+1,py+1,.8)}}
    else if(kind==='bush'){var bx=ox+4+Math.floor(r(4)*8),by=oy+5+Math.floor(r(5)*6),kd=r(6);blob(out,N,bx,by,4+(kd<.5?1:0),3+(kd<.3?1:0),kd<.5?[col('#35592a'),col('#4d7b36'),col('#6a9a48')]:[col('#3c5f2c'),col('#557f38'),col('#79a54e')],true);if(kd>.75){put(out,N,bx-1,by-1,col('#d9687a'));put(out,N,bx+2,by,col('#d9687a'))}}
    else if(kind==='tallgrass'||kind==='drygrass'){var cs=kind==='drygrass'?[col('#b39b52'),col('#cdb766'),col('#8f7b3f')]:[col('#5d8a3c'),col('#7aa850'),col('#4a7331')];for(i=0;i<5;i++){var gx=ox+1+Math.floor(r(i+11)*14),gy=oy+3+Math.floor(r(i+17)*11),h=2+Math.floor(r(i+23)*2);for(var k=0;k<h;k++)put(out,N,gx+(k===h-1&&r(i+29)<.5?1:0),gy-k,cs[k===h-1?1:(i%3===0?2:0)])}}
    else if(kind==='fern'){var fx2=ox+4+Math.floor(r(8)*8),fy2=oy+6+Math.floor(r(9)*6),fc=[col('#2f5a2a'),col('#44773a')];for(i=-3;i<=3;i++){put(out,N,fx2+i,fy2-Math.abs(i)*.5|0,fc[1]);if(Math.abs(i)<3)put(out,N,fx2+i,fy2+1,fc[0])}put(out,N,fx2,fy2-2,fc[1])}
    else if(kind==='mushroom'){for(i=0;i<1+Math.floor(r(12)*2);i++){var mx=ox+3+Math.floor(r(i+13)*10),my=oy+4+Math.floor(r(i+14)*9),red=r(15)<.6;put(out,N,mx,my+1,col('#efe6d6'));put(out,N,mx-1,my,col(red?'#c94a3a':'#a6784a'));put(out,N,mx,my,col(red?'#e05a47':'#bf8c58'));put(out,N,mx+1,my,col(red?'#c94a3a':'#a6784a'));if(red)put(out,N,mx,my,col('#f4efe6'))}}
    else if(kind==='log'){var lx=ox+2+Math.floor(r(16)*4),ly=oy+6+Math.floor(r(17)*5);for(x=0;x<10;x++){put(out,N,lx+x,ly,col('#8a6340'));put(out,N,lx+x,ly+1,col('#6b4a2f'));dim(out,N,lx+x+1,ly+2,.75)}put(out,N,lx+9,ly,col('#c9a77a'));put(out,N,lx+9,ly+1,col('#a88560'));if(r(18)<.5){put(out,N,lx+4,ly-1,col('#5a8a3c'));put(out,N,lx+5,ly-1,col('#6a9a48'))}}
    else if(kind==='stump'){var sx=ox+6+Math.floor(r(19)*4),sy=oy+7+Math.floor(r(20)*4);put(out,N,sx,sy,col('#c9a77a'));put(out,N,sx+1,sy,col('#b8946a'));put(out,N,sx,sy+1,col('#6b4a2f'));put(out,N,sx+1,sy+1,col('#5a3e27'));dim(out,N,sx+2,sy+1,.75)}
    else if(kind==='reeds'){var c1=col('#6f7d3f'),c2=col('#8c7a45');for(i=0;i<4;i++){var qx=ox+2+Math.floor(r(i+4)*12),qy=oy+4+Math.floor(r(i+7)*8);for(h=0;h<4;h++)put(out,N,qx,qy+h,c1);put(out,N,qx,qy-1,c2);put(out,N,qx,qy-2,c2)}}
    else if(kind==='puddle'){blob(out,N,ox+8,oy+8,4,2,[col('#3d6f8f'),col('#4f86a8'),col('#7fb2cf')],false)}
    else if(kind==='lily'){for(i=0;i<1+Math.floor(r(21)*3);i++){var lx2=ox+3+Math.floor(r(i+22)*10),ly2=oy+3+Math.floor(r(i+25)*10);blob(out,N,lx2,ly2,2,1,[col('#3f7a36'),col('#5a9a48'),col('#79b85e')],false);if(r(i+28)<.4)put(out,N,lx2,ly2,col('#f2a8c2'))}}
    else if(kind==='shorerock'){blob(out,N,ox+8,oy+8,3,2,[col('#5f5a55'),col('#7d7770'),col('#a39c94')],false)}
    else if(kind==='drybush'){var dx=ox+5+Math.floor(r(31)*6),dy=oy+7+Math.floor(r(32)*4),dc=col('#7a6a42'),dc2=col('#9a8752');for(i=-2;i<=2;i++){put(out,N,dx+i,dy,dc);put(out,N,dx+i,dy-1-(i&1),dc2)}put(out,N,dx,dy-3,dc2);dim(out,N,dx+1,dy+1,.8);dim(out,N,dx+2,dy+1,.8)}
    else if(kind==='driftwood'){var wx2=ox+3+Math.floor(r(33)*5),wy2=oy+8+Math.floor(r(34)*4);for(x=0;x<7;x++)put(out,N,wx2+x,wy2+(x>4?1:0),col(x%3?'#b39b7a':'#9c8466'))}
    else if(kind==='snowpatch'){blob(out,N,ox+6+Math.floor(r(35)*5),oy+6+Math.floor(r(36)*5),3,2,[col('#d9e2e8'),col('#e9eff3'),col('#f7fafc')],false)}
    else if(kind==='tuft'){var t=col('#8d8f4f');for(i=0;i<3;i++){var ux=ox+3+Math.floor(r(i+2)*10),uy=oy+4+Math.floor(r(i+5)*9);put(out,N,ux,uy,t);put(out,N,ux-1,uy+1,t);put(out,N,ux+1,uy+1,t)}}
    else if(kind==='shell'){put(out,N,ox+7,oy+8,col('#f4e9dc'));put(out,N,ox+8,oy+8,col('#e7c9b5'))}
  }

  /* ---------- pequeno “pincel” para sprites ---------- */
  function Sprite(w,h){this.c=canvas(w,h);this.x=this.c.getContext('2d');this.w=w;this.h=h}
  Sprite.prototype.px=function(x,y,col){this.x.fillStyle=col;this.x.fillRect(x,y,1,1)};
  Sprite.prototype.rect=function(x,y,w,h,col){this.x.fillStyle=col;this.x.fillRect(x,y,w,h)};
  Sprite.prototype.disc=function(cx,cy,r,col,ry){ry=ry||r;this.x.fillStyle=col;for(var y=-Math.ceil(ry);y<=Math.ceil(ry);y++)for(var x=-Math.ceil(r);x<=Math.ceil(r);x++)if((x*x)/(r*r)+(y*y)/(ry*ry)<=1)this.x.fillRect(Math.round(cx+x),Math.round(cy+y),1,1)};
  Sprite.prototype.poly=function(pts,col){var x=this.x;x.fillStyle=col;x.beginPath();x.moveTo(pts[0],pts[1]);for(var i=2;i<pts.length;i+=2)x.lineTo(pts[i],pts[i+1]);x.closePath();x.fill()};
  function shadow(sp,cx,cy,rx,ry){sp.x.globalAlpha=.28;sp.disc(cx,cy,rx,'#1b2415',ry);sp.x.globalAlpha=1}

  /* ---------- árvores (24×32 px, base no rodapé) ---------- */
  var TREES={};
  function canopy(sp,cx,cy,r,cols,seed){ /* copa arredondada em 3 tons + contorno */
    sp.disc(cx,cy,r+1,cols[3]);sp.disc(cx,cy,r,cols[0]);sp.disc(cx-r*.28,cy-r*.3,r*.62,cols[1]);sp.disc(cx+r*.25,cy+r*.35,r*.55,cols[2]);
    for(var i=0;i<14;i++){var a=hash(i,seed,1)*6.28,d=hash(i,seed,2)*r*.8;sp.px(Math.round(cx+Math.cos(a)*d),Math.round(cy+Math.sin(a)*d),hash(i,seed,3)<.5?cols[1]:cols[2])}
  }
  function makeTree(kind,v,snow){
    var sp=new Sprite(24,32),s=v*31+kind.length*7;
    if(kind!=='cactus'&&kind!=='palm')shadow(sp,12,29,8,2.2);
    if(kind==='oak'||kind==='birch'||kind==='willow'){
      var trunk=kind==='birch'?'#e9e4d8':'#6b4a2e',tdark=kind==='birch'?'#3b3530':'#4b321f';
      sp.rect(11,18,3,12,trunk);sp.rect(13,18,1,12,tdark);if(kind==='birch'){sp.px(11,21,tdark);sp.px(12,25,tdark);sp.px(11,27,tdark)}
      var cols=kind==='birch'?['#8fb25a','#a9c86c','#6f9344','#3f5a26']:kind==='willow'?['#6f8f4a','#86a65c','#56733a','#33472a']:[['#4f8a3a','#67a24a','#3b6f2d','#243f1b'],['#5a8f3c','#74a84e','#437331','#28431c'],['#4c7f36','#63964a','#3a6a2b','#223b19']][v%3];
      canopy(sp,12,11+(v%2),9-(v===2?1:0),cols,s);if(v!==1)canopy(sp,8+v*3,8,4,cols,s+5);
      if(kind==='willow'){for(var i=0;i<7;i++){var wx=5+i*2;sp.rect(wx,14,1,6+Math.floor(hash(i,s,4)*5),cols[2])}}
      if(snow){sp.disc(10,5,5,'#f2f5f8',2.2)}
    }else if(kind==='pine'||kind==='deadpine'){
      sp.rect(11,22,2,8,'#5a3d25');
      if(kind==='deadpine'){sp.rect(11,6,2,18,'#6a5140');for(i=0;i<4;i++){var yy=9+i*4;sp.rect(7+i,yy,4-i,1,'#6a5140');sp.rect(13,yy+1,4-i,1,'#5b4435')}if(snow){sp.rect(11,6,2,1,'#f2f5f8')}}
      else{var pc=[['#2f5a3a','#3f6f47','#224530'],['#355f3b','#467849','#274a2e'],['#2c5536','#3b6a43','#1f402a']][v%3];
        for(i=0;i<4;i++){var top=3+i*5,half=3+i*2;sp.poly([12,top,12+half+1,top+8,12-half-1,top+8],pc[2]);sp.poly([12,top+1,12+half,top+7.5,12-half,top+7.5],pc[0]);sp.poly([12,top+1,12,top+7.5,12-half,top+7.5],pc[1]);
          if(snow)sp.poly([12,top+1,12+half*.6,top+4,12-half*.6,top+4],'#eef3f6')}}
    }else if(kind==='acacia'){
      sp.rect(11,16,2,14,'#6b4a2e');sp.px(10,17,'#6b4a2e');sp.px(13,17,'#6b4a2e');
      sp.disc(12,13,10,'#3f5a24',3.4);sp.disc(12,12,9,'#6f8a3a',2.6);sp.disc(10,11,5,'#86a24a',1.6);
    }else if(kind==='palm'){
      shadow(sp,13,29,6,1.8);for(i=0;i<14;i++)sp.rect(12+Math.round(i*.18),15+i,2,1,i%3?'#8a6a45':'#6b4f33');
      var fr=['#4e8a3c','#65a34a','#3a6d2d'];[[-8,2],[8,2],[-6,-3],[6,-3],[0,-5]].forEach(function(f,j){for(var t=0;t<9;t++){sp.px(12+Math.round(f[0]*t/8),13+Math.round(f[1]*t/8+t*t*.04),fr[j%3])}});
    }else if(kind==='cactus'){
      shadow(sp,12,29,4,1.4);sp.rect(10,14,4,15,'#5f8f47');sp.rect(13,14,1,15,'#4a733a');sp.rect(6,18,2,6,'#5f8f47');sp.rect(6,23,4,2,'#5f8f47');sp.rect(16,16,2,6,'#5f8f47');sp.rect(14,21,4,2,'#5f8f47');sp.px(11,13,'#e7cf73');
    }
    return sp.c;
  }
  function tree(kind,v,snow){var k=kind+v+(snow?'s':'');return TREES[k]||(TREES[k]=makeTree(kind,v%3,snow))}

  /* ---------- construções medievais (48×56 px = 3×3 tiles + altura) ---------- */
  var STYLE={ /* material conforme o bioma do lote */
    temperate:{wall:'#e8dcc0',wallD:'#cbbb98',beam:'#5b3b24',roofs:[['#b4553b','#8f3f2c','#cf6d4f'],['#c9a24e','#a8843a','#dfbb67'],['#6f5a4c','#56453a','#86705f']],base:'#8d857c'},
    cold:{wall:'#9a8f86',wallD:'#7d736b',beam:'#4d3a2c',roofs:[['#56626e','#434d57','#6a7784'],['#5f4b3d','#4a3a2f','#766050']],base:'#6f6962',snow:true},
    dry:{wall:'#d9b98a',wallD:'#bf9c6c',beam:'#8a6038',roofs:[['#c7703f','#a3572f','#dc8a55'],['#b8864f','#98693a','#cf9e63']],base:'#b79d77',flat:true},
    coast:{wall:'#e6e1d3',wallD:'#c9c2b0',beam:'#4d6b86',roofs:[['#4f7aa0','#3c6284','#6591b6'],['#b4553b','#8f3f2c','#cf6d4f']],base:'#a09a8e'},
    wet:{wall:'#b59a74',wallD:'#977d5a',beam:'#4e3a26',roofs:[['#8f8a4a','#6f6a36','#a7a35c']],base:'#6f6a5e'}
  };
  function styleFor(biome){
    if(biome==='taiga'||biome==='tundra'||biome==='snow'||biome==='mountain'||biome==='peak'||biome==='hills')return biome==='hills'?'temperate':'cold';
    if(biome==='desert'||biome==='savanna'||biome==='steppe')return'dry';
    if(biome==='beach')return'coast';if(biome==='swamp')return'wet';return'temperate';
  }
  function roofRows(sp,x0,y0,x1,y1,cols,ridge){ /* telhado de duas águas visto de frente */
    for(var y=y0;y<=y1;y++){var t=(y-y0)/(y1-y0),l=Math.round(ridge.l-(ridge.l-x0)*t),r=Math.round(ridge.r+(x1-ridge.r)*t);
      sp.rect(l,y,r-l+1,1,((y-y0)%3===2)?cols[1]:cols[0]);
      if((y-y0)%3===1)for(var x=l+((y>>1)%2?1:3);x<r;x+=4)sp.px(x,y,cols[1]);}
    sp.rect(ridge.l,y0,ridge.r-ridge.l+1,1,cols[2]);sp.rect(x0,y1,x1-x0+1,1,cols[1]);
  }
  function window2(sp,x,y,frame){sp.rect(x-1,y-1,6,6,frame);sp.rect(x,y,4,4,'#f3c865');sp.rect(x,y,4,1,'#fbe39a');sp.rect(x+2,y,1,4,frame);sp.rect(x,y+2,4,1,frame)}
  function house(opts){
    var st=STYLE[opts.style||'temperate'],sp=new Sprite(48,56),roof=st.roofs[(opts.variant||0)%st.roofs.length],seed=opts.seed||1,kind=opts.kind||'house';
    shadow(sp,24,52,22,3.2);
    if(kind==='tower'){ /* torre de pedra (arquivos de dados) */
      sp.rect(12,20,24,33,'#8f877e');for(var y=21;y<52;y+=4)for(var x=12+((y>>2)%2)*3;x<36;x+=6)sp.rect(x,y,5,3,'#a39b91');sp.rect(33,20,3,33,'#756d65');
      sp.rect(10,14,28,7,'#7c746b');for(x=10;x<38;x+=5)sp.rect(x,10,3,4,'#7c746b');sp.rect(21,40,6,12,'#4a3322');sp.rect(22,41,4,11,'#6b4a2e');window2(sp,22,27,'#4d3a2c');
      sp.poly([24,2,33,14,15,14],roof[0]);sp.poly([24,2,24,14,15,14],roof[2]);if(st.snow)sp.poly([24,2,28,7,20,7],'#f2f5f8');return sp.c;
    }
    /* fundação e paredes */
    var wl=5,wr=43,wt=26,wb=48;if(kind==='hall'){wl=3;wr=45}
    sp.rect(wl-1,wb,wr-wl+3,4,st.base);for(x=wl;x<wr;x+=5)sp.rect(x,wb+1,4,2,'#a7a097');
    sp.rect(wl,wt,wr-wl+1,wb-wt,st.wall);sp.rect(wr-6,wt,7,wb-wt,st.wallD);
    if(!st.flat){ /* enxaimel: vigas e mãos-francesas */
      sp.rect(wl,wt,wr-wl+1,2,st.beam);sp.rect(wl,wt+11,wr-wl+1,1,st.beam);sp.rect(wl,wb-1,wr-wl+1,1,st.beam);
      [wl,wl+11,wr-11,wr].forEach(function(bx){sp.rect(bx,wt,2,wb-wt,st.beam)});
      for(var i=0;i<9;i++){sp.px(wl+2+i,wt+2+i,st.beam);sp.px(wr-2-i,wt+2+i,st.beam)}
    }else{sp.rect(wl,wt,wr-wl+1,1,st.wallD);for(i=0;i<5;i++)sp.px(wl+4+i*7,wt+4+(i%2)*6,st.wallD)}
    /* porta em arco e janelas com luz */
    var dx=kind==='hall'?21:21;sp.rect(dx-1,36,8,12,st.beam);sp.rect(dx,38,6,10,'#6b4a2e');sp.rect(dx+1,37,4,1,'#6b4a2e');sp.rect(dx+3,38,1,10,'#553a24');sp.px(dx+4,43,'#d9b44a');
    window2(sp,wl+4,31,st.beam);window2(sp,wr-9,31,st.beam);if(kind==='hall'){window2(sp,wl+13,31,st.beam);window2(sp,wr-18,31,st.beam)}
    /* telhado */
    if(st.flat){sp.rect(wl-2,wt-5,wr-wl+5,5,roof[0]);sp.rect(wl-2,wt-1,wr-wl+5,1,roof[1]);sp.rect(wl-2,wt-5,wr-wl+5,1,roof[2]);for(x=wl;x<wr;x+=6)sp.rect(x,wt-7,3,2,roof[1])}
    else{roofRows(sp,wl-4,6,wr+4,wt+1,roof,{l:wl+7,r:wr-7});
      if(opts.chimney!==false){sp.rect(wr-12,1,5,10,'#8d857c');sp.rect(wr-12,1,5,1,'#6f6962');sp.rect(wr-8,1,1,10,'#6f6962');if(kind==='workshop'){sp.x.globalAlpha=.55;sp.disc(wr-9,-2,2.5,'#c9c9c9');sp.x.globalAlpha=1}}
      if(st.snow){for(x=wl-3;x<=wr+3;x++)if(hash(x,seed,4)<.75)sp.rect(x,wt-1-Math.floor(hash(x,seed,5)*2),1,2,'#f2f5f8');sp.rect(wl+7,6,wr-wl-13,2,'#f2f5f8')}}
    /* ofícios: pequenos sinais visuais */
    if(kind==='workshop'){sp.rect(wr+1,40,3,8,'#5b5550');sp.rect(wr,39,5,2,'#77706a')}             /* bigorna */
    if(kind==='market'){for(x=wl-3;x<=wr+3;x+=4)sp.rect(x,wt-2,2,5,x%8?'#c9453a':'#efe6d2');sp.rect(wl+2,wb-6,8,4,'#a7743f');sp.rect(wr-10,wb-6,8,4,'#a7743f');sp.rect(wl+3,wb-8,3,2,'#d94f3a');sp.rect(wr-8,wb-8,3,2,'#e0b43d')}
    if(kind==='hall'){sp.rect(23,8,1,10,'#4d3a2c');sp.rect(24,8,6,5,'#7d5ab4');sp.rect(24,12,6,1,'#5f4290')}  /* estandarte */
    if(kind==='store'){sp.rect(wl+1,wb-5,5,5,'#9a6b3e');sp.rect(wl+1,wb-5,5,1,'#b98653');sp.rect(wr-5,wb-5,5,5,'#9a6b3e');sp.rect(wr-5,wb-5,5,1,'#b98653')}
    if(kind==='dyer'){sp.rect(wl-2,wb-12,1,10,'#5b3b24');sp.rect(wl-1,wb-12,3,6,'#3f7ac0');sp.rect(wr+1,wb-12,1,10,'#5b3b24');sp.rect(wr-1,wb-12,2,6,'#c0493f')}
    if(opts.flowers){for(x=wl+3;x<wl+10;x++)sp.px(x,37,hash(x,seed,8)<.5?'#d9687a':'#f2d34f');for(x=wr-8;x<wr-1;x++)sp.px(x,37,hash(x,seed,9)<.5?'#9b7fe0':'#f2d34f')}
    return sp.c;
  }
  var HOUSES={};
  function building(kind,style,variant,flowers){var k=kind+'|'+style+'|'+variant+'|'+(flowers?1:0);return HOUSES[k]||(HOUSES[k]=house({kind:kind,style:style,variant:variant,seed:variant*7+kind.length,flowers:flowers}))}

  /* ponte de tábuas (um tile) */
  var BRIDGE=null;
  function bridge(){if(BRIDGE)return BRIDGE;var sp=new Sprite(16,16);sp.rect(0,2,16,12,'#8a6440');for(var x=0;x<16;x+=3)sp.rect(x,2,1,12,'#6b4a2e');sp.rect(0,1,16,1,'#5a3d25');sp.rect(0,14,16,1,'#5a3d25');sp.rect(0,0,16,1,'#b08a5c');BRIDGE=sp.c;return BRIDGE}

  /* ---------- visão de longe: chão em meia resolução com as copas pintadas de cima ----------
     Usada quando a câmera está afastada: florestas continuam florestas sem desenhar
     milhares de árvores uma a uma. Mesma posição (e o mesmo sorteio) das árvores de perto. */
  var CANOPY={oak:['#355f27','#4f8636','#72a84c'],birch:['#44762f','#6fa246','#9ac765'],pine:['#1f4230','#2f5a3c','#467a4c'],deadpine:['#463b31','#62533f','#7e6c58'],
    acacia:['#56632a','#788a38','#9dad4e'],cactus:['#34603a','#4f8646','#73aa5e'],willow:['#435f2b','#63873f','#88ad58'],palm:['#357030','#559340','#7cbb5a']};
  var CANOPYRGB={};Object.keys(CANOPY).forEach(function(k){CANOPYRGB[k]=CANOPY[k].map(rgb)});
  var SNOWC=rgb('#eef3f6');
  function chunkFarPixels(world,cx,cy,ground){
    var CH=world.CH,N=CH*PX,H=N>>1,out=new Uint8ClampedArray(H*H*4),x0=cx*CH,y0=cy*CH,sc=PX/2; /* 8 px por tile */
    for(var y=0;y<H;y++)for(var x=0;x<H;x++){var o=(y*H+x)*4,a=((y*2)*N+x*2)*4,b=a+4,c=a+N*4,d=c+4;for(var k=0;k<3;k++)out[o+k]=(ground[a+k]+ground[b+k]+ground[c+k]+ground[d+k])>>2;out[o+3]=255}
    var list=[];for(var j=-1;j<=CH;j++)for(var i=-1;i<=CH;i++){var wx=x0+i,wy=y0+j,t=world.tree(wx,wy);if(t)list.push([wx,wy,t])}
    list.sort(function(p,q){return p[1]-q[1]||p[0]-q[0]});
    function put(px,py,c,f){if(px<0||py<0||px>=H||py>=H)return;var o=(py*H+px)*4;out[o]=c[0]*f;out[o+1]=c[1]*f;out[o+2]=c[2]*f}
    function shadow(px,py,f){if(px<0||py<0||px>=H||py>=H)return;var o=(py*H+px)*4;out[o]*=f;out[o+1]*=f;out[o+2]*=f}
    for(var n=0;n<list.length;n++){var wx=list[n][0],wy=list[n][1],t=list[n][2],cols=CANOPYRGB[t.kind]||CANOPYRGB.oak;
      var jx=(hash(wx,wy,31)-.5)*.65,jy=(hash(wx,wy,32)-.5)*.4,ccx=(wx-x0+.5+jx)*sc,ccy=(wy-y0+.35+jy)*sc;
      var r=(t.kind==='cactus'?1.7:t.kind==='deadpine'?2.5:t.kind==='pine'?3.8:4.5+t.v*.5)*(sc/8),r2=r*r;
      /* sombra projetada para sudeste */
      for(var dy=-r;dy<=r;dy++)for(var dx=-r;dx<=r;dx++){if(dx*dx+dy*dy>r2)continue;shadow(Math.round(ccx+dx+1.5),Math.round(ccy+dy+2),.72)}
      for(dy=-r;dy<=r;dy++)for(dx=-r;dx<=r;dx++){var dd=dx*dx+dy*dy;if(dd>r2)continue;
        var l=(-dx-dy)/(r*1.6),c=l>.35?cols[2]:l<-.35?cols[0]:cols[1];
        if(t.snow&&l>.1)c=SNOWC;
        var jit=1+(hash(Math.round(ccx+dx)+wx*31,Math.round(ccy+dy)+wy*17,55)-.5)*.12;put(Math.round(ccx+dx),Math.round(ccy+dy),c,jit)}
      if(t.kind==='pine'||t.kind==='deadpine')put(Math.round(ccx-r*.3),Math.round(ccy-r*.4),cols[2],1.05);
    }
    return out;
  }

  /* ---------- fauna: sprites pequenos desenhados em grade (olhando para a direita) ----------
     letras = cores da paleta de cada bicho; '.' = transparente. */
  var ANIMALS={
    sheep:{pal:{o:'#8c877c',w:'#f3f0e8',W:'#d9d4c7',k:'#3b3632',e:'#f3f0e8'},frames:[
      ['....ooooo.....','..oowwwwwoo...','.owwwwwwwwwokk','.owwwwwwwwwkek','..oWWwwwwWWokk','...ooooooooo..','...k.k...k.k..','...k.k...k.k..'],
      ['....ooooo.....','..oowwwwwoo...','.owwwwwwwwwokk','.owwwwwwwwwkek','..oWWwwwwWWokk','...ooooooooo..','....k.k.k.k...','....k.k.k.k...'],
      ['....ooooo.....','..oowwwwwoo...','.owwwwwwwwwo..','.owwwwwwwwwo..','..oWWwwwwWWokk','...ooooooooekk','...k.k...k.kk.','...k.k...k.k..']]},
    cow:{pal:{o:'#5a4a3c',w:'#f1ece2',b:'#4a3a2e',p:'#e8b4a8',k:'#2e2620',h:'#d8cfbf'},frames:[
      ['.............hh.','..oooooooooookk.','.owwbbwwwwbwokkk','.owbbbwwwwwwokpk','.owwbwwwbbwwo...','..owwwwwwwwo....','..k.k....k.k....','..k.k....k.k....'],
      ['.............hh.','..oooooooooookk.','.owwbbwwwwbwokkk','.owbbbwwwwwwokpk','.owwbwwwbbwwo...','..owwwwwwwwo....','...k.k..k.k.....','...k.k..k.k.....'],
      ['................','..ooooooooooo...','.owwbbwwwwbwo...','.owbbbwwwwwwohh.','.owwbwwwbbwwokkk','..owwwwwwwwokpk.','..k.k....k.k....','..k.k....k.k....']]},
    deer:{pal:{a:'#d9c7a6',b:'#9a6a3c',B:'#7a5230',w:'#f2ebe0',k:'#3a2a1e'},frames:[
      ['..........a.a.','...........a..','..........bbk.','..........bb..','.wbbbbbbbbbb..','..bBBBBBBBb...','..k..k...k.k..','..k..k...k.k..','..k..k...k.k..'],
      ['..........a.a.','...........a..','..........bbk.','..........bb..','.wbbbbbbbbbb..','..bBBBBBBBb...','...k.k..k.k...','...k.k..k.k...','...k.k..k.k...'],
      ['..............','..............','..............','..........a.a.','.wbbbbbbbbbba.','..bBBBBBBBbbbk','..k..k...k.k..','..k..k...k.k..','..k..k...k.k..']]},
    duck:{pal:{g:'#2f6b3f',y:'#e9a53a',w:'#f2efe8',W:'#c9c3b6',r:'#9fc7dd'},frames:[
      ['.....gg..','.....ggy.','.wwwwww..','WwwwwwW..','.rrrrrr..'],
      ['.....gg..','.....ggy.','.wwwwww..','WwwwwwW..','rr.rr.rr.']]},
    bird:{pal:{k:'#2a2a2e'},frames:[['k.....k','.k...k.','..k.k..','...k...'],['.......','kkk.kkk','...k...','.......']]}
  };
  var ANIM={};
  function animal(kind,frame,flip){var key=kind+frame+(flip?'f':'');if(ANIM[key])return ANIM[key];var d=ANIMALS[kind],g=d.frames[frame%d.frames.length],h=g.length,w=0;g.forEach(function(r){w=Math.max(w,r.length)});
    var c=canvas(w,h),x=c.getContext('2d');for(var j=0;j<h;j++)for(var i=0;i<g[j].length;i++){var ch=g[j][i];if(ch==='.')continue;x.fillStyle=d.pal[ch]||'#f0f';x.fillRect(flip?w-1-i:i,j,1,1)}
    return(ANIM[key]=c)}

  /* ---------- moradores: bonequinhos medievais (cabeça grande, legíveis em tela pequena) ----------
     look = {skin,hair,style,shirt,pants,dress,acc}; dir = 'down'|'up'|'side'; frame 0..3 (passos).
     Grade 12×18 + 1 px de contorno escuro em volta (14×20). Olhando para a direita no 'side'. */
  var SKIN=['#f1c9a5','#e0ac85','#c68b62','#9a6644','#6f4a33'],HAIR=['#2b211c','#5a3b24','#8a5a2c','#c9a063','#b8b2a8','#7a2e1f'],
    PANTS=['#5a4a3a','#3f4a5a','#6b5a44','#4a5a3f','#5c3f3a'];
  function shade(h,f){var c=rgb(h);return'rgb('+Math.round(Math.min(255,c[0]*f))+','+Math.round(Math.min(255,c[1]*f))+','+Math.round(Math.min(255,c[2]*f))+')'}
  var VIL={};
  function villager(look,dir,frame){var key=JSON.stringify(look)+dir+frame;if(VIL[key])return VIL[key];
    var W=12,H=18,g=[];for(var i=0;i<W*H;i++)g.push(null);
    function R(x,y,w,h,c){for(var j=0;j<h;j++)for(var k=0;k<w;k++){var xx=x+k,yy=y+j;if(xx>=0&&yy>=0&&xx<W&&yy<H)g[yy*W+xx]=c}}
    function P(x,y,c){R(x,y,1,1,c)}
    var skin=SKIN[look.skin%SKIN.length],skinD=shade(skin,.85),hair=HAIR[look.hair%HAIR.length],hairL=shade(hair,1.25),shirt=look.shirt,shirtD=shade(shirt,.78),shirtL=shade(shirt,1.12),
      pants=PANTS[look.pants%PANTS.length],shoe='#3a2a20',belt='#5a3e27',eye='#1c1a1a',st=look.style,sw=[0,1,0,-1][frame&3];
    var side=dir==='side',back=dir==='up';
    /* pernas e sapatos */
    if(look.dress){R(side?4:3,11,side?5:6,4,shirtD);R(side?4:3,14,side?5:6,1,shade(shirt,.65));
      if(side){R(sw>0?4:sw<0?7:5,15,1,2,skinD);R(sw>0?7:sw<0?4:6,15,1,2,skinD);R(sw>0?3:sw<0?7:5,17,2,1,shoe);R(sw>0?7:sw<0?3:6,17,1,1,shoe)}
      else{R(4,15,1,2,skinD);R(7,15,1,2,skinD);R(3+(sw>0?0:0),17-(sw>0?1:0),2,1,shoe);R(7,17-(sw<0?1:0),2,1,shoe)}}
    else if(side){var fa=sw>0?6:sw<0?4:5,fb=sw>0?4:sw<0?6:5;R(fb,13,2,4,shade(pants,.8));R(fa,13,2,4,pants);R(fb,17,2,1,shoe);R(fa+(sw?1:0),17,2,1,shoe)}
    else{var la=sw>0?1:0,ra=sw<0?1:0;R(4,13,2,4-la,pants);R(6,13,2,4-ra,shade(pants,.85));R(4,17-la,2,1,shoe);R(6,17-ra,2,1,shoe)}
    /* tronco, cinto e braços */
    if(side){R(4,7,4,5,shirt);R(4,7,1,5,shirtL);R(7,7,1,5,shirtD);R(4,11,4,1,look.dress?shirtD:belt);
      var ax=5+(sw>0?1:sw<0?-1:0);R(ax,7,2,4,shirtD);P(ax+(sw>0?1:0),11,skin)}
    else{R(3,7,6,5,shirt);R(3,7,1,5,shirtL);R(8,7,1,5,shirtD);if(!back){P(5,7,shirtD);P(6,7,shirtD)}R(3,11,6,1,look.dress?shirtD:belt);
      R(2,7+(sw<0?1:0),1,4,shirtD);R(9,7+(sw>0?1:0),1,4,shirtD);P(2,11+(sw<0?1:0),skin);P(9,11+(sw>0?1:0),skin)}
    /* pescoço e cabeça */
    R(5,6,2,1,skinD);R(3,1,6,5,skin);R(3,5,6,1,skinD);
    if(side){P(7,3,eye);P(8,4,skinD)}else if(!back){P(4,3,eye);P(7,3,eye);P(5,5,shade(skin,.9));P(6,5,shade(skin,.9))}
    /* cabelo / chapéu */
    if(st==='bald'){R(3,1,6,1,skinD);if(side)R(3,2,2,3,hair);else R(3,3,1,2,hair),R(8,3,1,2,hair);if(back)R(3,3,6,3,hair)}
    else{var longo=st==='long';R(3,0,6,2,hair);R(3,0,6,1,hairL);
      if(back)R(3,1,6,longo?7:5,hair);else if(side){R(3,1,3,longo?6:4,hair);P(8,1,hair)}else{R(3,1,1,longo?6:3,hair);R(8,1,1,longo?6:3,hair)}}
    if(st==='straw'){var hs='#d8b765',hsD='#b38f45';R(3,-1,6,1,hs);R(3,0,6,1,hs);R(3,1,6,1,'#a4552f');R(1,2,10,1,hsD);if(side)R(1,2,11,1,hsD)}
    else if(st==='hood'){var hc=look.hood||'#5b4a6a',hcD=shade(hc,.75);R(2,0,8,6,hc);R(2,0,8,1,shade(hc,1.15));if(!back){R(4,2,4,4,skin);P(4,3,eye);P(7,3,eye)}else R(3,1,6,5,hcD);if(side){R(2,0,5,6,hc);R(6,2,3,4,skin);P(7,3,eye)}R(2,6,8,2,hc)}
    else if(st==='cap'){var cc=look.cap||'#a33c32';R(3,0,6,2,cc);R(3,0,6,1,shade(cc,1.2));if(!back)R(side?7:2,1,side?3:8,1,shade(cc,.8))}
    /* acessórios */
    if(look.acc==='basket'){var bk='#a8763e',bkD='#7a5228';if(side){R(7,9,3,3,bk);R(7,9,3,1,bkD);P(8,8,bkD)}else{R(9,9,3,3,bk);R(9,9,3,1,bkD)}}
    else if(look.acc==='sack'){var sk='#c8b48a';if(back||!side)R(back?4:8,5,4,4,sk),R(back?4:8,5,4,1,shade(sk,.8));else R(2,5,3,5,sk)}
    else if(look.acc==='staff'){R(side?9:10,2,1,15,'#7a5a3a');P(side?9:10,2,'#9a7a52')}
    else if(look.acc==='bucket'){R(side?7:9,11,3,3,'#8c8a86');R(side?7:9,11,3,1,'#b0aea9')}
    /* contorno */
    var OW=W+2,OH=H+2,c=canvas(OW,OH),x=c.getContext('2d'),img=x.createImageData(OW,OH),d=img.data,ol=[34,26,22];
    function has(i,j){return i>=0&&j>=0&&i<W&&j<H&&g[j*W+i]}
    for(var j=-1;j<=H;j++)for(var i2=-1;i2<=W;i2++){var o=((j+1)*OW+(i2+1))*4,v=has(i2,j);
      if(v){var cc2=v.charAt(0)==='#'?rgb(v):v.match(/\d+/g).map(Number);d[o]=cc2[0];d[o+1]=cc2[1];d[o+2]=cc2[2];d[o+3]=255}
      else if(has(i2-1,j)||has(i2+1,j)||has(i2,j-1)||has(i2,j+1)){d[o]=ol[0];d[o+1]=ol[1];d[o+2]=ol[2];d[o+3]=230}}
    x.putImageData(img,0,0);
    return(VIL[key]=c)}
  function villagerFlip(look,frame){var key='F'+JSON.stringify(look)+frame;if(VIL[key])return VIL[key];var src=villager(look,'side',frame),c=canvas(src.width,src.height),x=c.getContext('2d');x.translate(src.width,0);x.scale(-1,1);x.drawImage(src,0,0);return(VIL[key]=c)}

  global.UrbeArt={configure:configure,DEFAULT_PAL:PAL0,villager:villager,villagerFlip:villagerFlip,animal:animal,ANIMALS:ANIMALS,chunkFarPixels:chunkFarPixels,PX:PX,PAL:PAL,IDS:IDS,renderChunk:renderChunk,chunkPixels:chunkPixels,canvasFromPixels:canvasFromPixels,tree:tree,building:building,styleFor:styleFor,bridge:bridge,texture:function(id,v){return TEX[id][v||0]}};
})(typeof window!=='undefined'?window:self);
