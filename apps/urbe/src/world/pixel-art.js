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
    for(var ty=0;ty<CH;ty++)for(var tx=0;tx<CH;tx++){
      var k=(ty+1)*W+tx+1,id=bio[k],wx=x0+tx,wy=y0+ty,v=Math.floor(hash(wx,wy,5)*4),own=TEX[id][v];
      var nL=bio[k-1],nR=bio[k+1],nU=bio[k-W],nD=bio[k+W],edge=nL!==id||nR!==id||nU!==id||nD!==id;
      var texL=TEX[nL][v],texR=TEX[nR][v],texU=TEX[nU][v],texD=TEX[nD][v];
      var water=!!WATER[id],landNear=water&&(!WATER[nL]||!WATER[nR]||!WATER[nU]||!WATER[nD]);
      var deep=water?Math.max(0,Math.min(1,(sea-elev[k])/.12)):0,rocky=ROCKY[id]?10:5;
      /* cantos do declive e do tom para interpolação bilinear dentro do tile */
      var s00=slope[k-W-1]+slope[k-W]+slope[k-1]+slope[k],s10=slope[k-W]+slope[k-W+1]+slope[k]+slope[k+1],s01=slope[k-1]+slope[k]+slope[k+W-1]+slope[k+W],s11=slope[k]+slope[k+1]+slope[k+W]+slope[k+W+1];
      var t00=tint[k-W-1]+tint[k-W]+tint[k-1]+tint[k],t10=tint[k-W]+tint[k-W+1]+tint[k]+tint[k+1],t01=tint[k-1]+tint[k]+tint[k+W-1]+tint[k+W],t11=tint[k]+tint[k+1]+tint[k+W]+tint[k+W+1];
      for(var py=0;py<PX;py++){
        var fy=(py+.5)/PX,row=((ty*PX+py)*N+tx*PX)*4,dU=py,dD=PX-1-py;
        var sl0=s00+(s01-s00)*fy,sl1=s10+(s11-s10)*fy,tn0=t00+(t01-t00)*fy,tn1=t10+(t11-t10)*fy;
        for(var px=0;px<PX;px++){
          var o=(py*PX+px)*4,src=own,sw=water;
          if(edge){ /* bordas orgânicas: perto da divisa, às vezes o pixel é do vizinho */
            var hsh=hash(wx*PX+px,wy*PX+py,77),dL=px,dR=PX-1-px;
            if(nL!==id&&dL<4&&hsh<(4-dL)*.2){src=texL;sw=!!WATER[nL]}
            else if(nR!==id&&dR<4&&hsh<(4-dR)*.2){src=texR;sw=!!WATER[nR]}
            else if(nU!==id&&dU<4&&hsh<(4-dU)*.2){src=texU;sw=!!WATER[nU]}
            else if(nD!==id&&dD<4&&hsh<(4-dD)*.2){src=texD;sw=!!WATER[nD]}
          }
          var r=src[o],g=src[o+1],bl=src[o+2],shade;
          if(sw){
            shade=-deep*.28;
            if(landNear){var dmin=99;if(!WATER[nL])dmin=px;if(!WATER[nR]&&PX-1-px<dmin)dmin=PX-1-px;if(!WATER[nU]&&dU<dmin)dmin=dU;if(!WATER[nD]&&dD<dmin)dmin=dD;
              if(dmin<=1&&hash(wx*PX+px,wy*PX+py,78)<.75){r=236;g=244;bl=246;shade=0}else if(dmin<=3)shade+=.12}
          }else{
            var fx=(px+.5)/PX,sl=(sl0+(sl1-sl0)*fx)*.25,tn=(tn0+(tn1-tn0)*fx)*.25;
            shade=sl*rocky;shade=(shade>.3?.3:shade<-.3?-.3:shade)+tn;
          }
          var f=1+shade,to=row+px*4;out[to]=r*f;out[to+1]=g*f;out[to+2]=bl*f;out[to+3]=255;
        }
      }
      /* detalhes embutidos no chão */
      var dec=world.decor(wx,wy);if(dec)paintDecor(out,N,tx*PX,ty*PX,dec,wx,wy);
    }
    return out;
  }
  function renderChunk(world,cx,cy){var N=world.CH*PX,c=canvas(N,N),x=c.getContext('2d'),img=x.createImageData(N,N);img.data.set(chunkPixels(world,cx,cy));x.putImageData(img,0,0);return c}
  function canvasFromPixels(px,N){var c=canvas(N,N),x=c.getContext('2d'),img=x.createImageData(N,N);img.data.set(px);x.putImageData(img,0,0);return c}
  function put(out,N,x,y,c){if(x<0||y<0||x>=N||y>=N)return;var o=(y*N+x)*4;out[o]=c[0];out[o+1]=c[1];out[o+2]=c[2];out[o+3]=255}
  var FLOWERS=[rgb('#f2d34f'),rgb('#e8e8f0'),rgb('#d9687a'),rgb('#9b7fe0'),rgb('#f29a4f')];
  function paintDecor(out,N,ox,oy,kind,wx,wy){
    var r=function(s){return hash(wx,wy,s)};
    if(kind==='flowers'){for(var i=0;i<4;i++){var fx=ox+2+Math.floor(r(i+1)*12),fy=oy+2+Math.floor(r(i+9)*12),col=FLOWERS[Math.floor(r(i+30)*FLOWERS.length)];put(out,N,fx,fy,col);put(out,N,fx,fy+1,rgb('#4f7a34'))}}
    else if(kind==='rock'){var rx=ox+4+Math.floor(r(2)*6),ry=oy+5+Math.floor(r(3)*6),g1=rgb('#8f8a84'),g2=rgb('#6c6761'),g3=rgb('#b3aea8');
      for(var y=0;y<4;y++)for(var x=0;x<5;x++){if((x===0||x===4)&&(y===0||y===3))continue;put(out,N,rx+x,ry+y,y===0?g3:y===3||x===4?g2:g1)}}
    else if(kind==='reeds'){var c1=rgb('#6f7d3f'),c2=rgb('#8c7a45');for(i=0;i<3;i++){var qx=ox+3+Math.floor(r(i+4)*10),qy=oy+4+Math.floor(r(i+7)*8);for(var h=0;h<4;h++)put(out,N,qx,qy+h,c1);put(out,N,qx,qy-1,c2)}}
    else if(kind==='tuft'){var t=rgb('#8d8f4f');for(i=0;i<3;i++){var ux=ox+3+Math.floor(r(i+2)*10),uy=oy+4+Math.floor(r(i+5)*9);put(out,N,ux,uy,t);put(out,N,ux-1,uy+1,t);put(out,N,ux+1,uy+1,t)}}
    else if(kind==='shell'){put(out,N,ox+7,oy+8,rgb('#f4e9dc'));put(out,N,ox+8,oy+8,rgb('#e7c9b5'))}
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

  global.UrbeArt={PX:PX,PAL:PAL,IDS:IDS,renderChunk:renderChunk,chunkPixels:chunkPixels,canvasFromPixels:canvasFromPixels,tree:tree,building:building,styleFor:styleFor,bridge:bridge,texture:function(id,v){return TEX[id][v||0]}};
})(typeof window!=='undefined'?window:self);
