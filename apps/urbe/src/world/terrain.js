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

  function createWorld(seedText,opts){
    opts=opts||{};
    var seed=hash32(seedText||'urbe'),nE=makeNoise(seed),nR=makeNoise(seed^0x9e3779b9),nT=makeNoise(seed^0x85ebca6b),nM=makeNoise(seed^0xc2b2ae35),nV=makeNoise(seed^0x27d4eb2f),nD=makeNoise(seed^0x165667b1);
    var SX=opts.spawnX==null?36:opts.spawnX,SY=opts.spawnY==null?25:opts.spawnY,SEA=0.42;

    function raw(x,y){
      /* continentes: ruído grande deformado (warp) para costas menos redondas */
      var wx=x+fbm(nD,x/170,y/170,3,2,.5)*70,wy=y+fbm(nD,x/170+31,y/170+17,3,2,.5)*70;
      var cont=fbm(nE,wx/300,wy/300,5,2.03,.5);                 /* ~[-.5,.5] */
      var e=.52+cont*1.05;
      /* cordilheiras: ruído “ridged” só onde já é terra alta */
      var rid=1-Math.abs(fbm(nR,x/120,y/120,4,2.1,.52)*1.6);rid=Math.max(0,rid);rid=rid*rid;
      e+=rid*.26*smooth(.5,.7,e)-.05;
      /* colinas e detalhe */
      e+=fbm(nE,x/38+100,y/38-40,3,2,.5)*.08;
      /* o lugar onde as cidades começam é sempre terra temperada e amigável */
      var d=Math.hypot(x-SX,y-SY),w=1-smooth(50,170,d);
      /* só corrige extremos (mar e picos) — o relevo local continua o do ruído */
      e=mix(e,Math.max(.48,Math.min(.63,e)),w);
      e=Math.max(0,Math.min(1,e));
      /* temperatura: faixas amplas de latitude + variação regional, esfria com a altitude */
      var lat=Math.sin((y-SY)/560*Math.PI*.5+.3);
      var t=.55+lat*.24+fbm(nT,x/260,y/260,3,2,.5)*.45-Math.max(0,e-.58)*1.35;
      t=mix(t,Math.max(.36,Math.min(.68,t)),w);
      /* umidade: massas de ar + costas e rios mais úmidos */
      var m=.5+fbm(nM,x/190,y/190,4,2,.5)*1.1+(e<SEA+.05?.18:0);
      m=mix(m,Math.max(.34,Math.min(.62,m)),w);
      /* rios: vales onde um ruído de baixa frequência cruza o meio; mais largos quando úmido */
      var rv=Math.abs(fbm(nV,x/150,y/150,3,2,.45)),rw=.0075+Math.max(0,m-.4)*.012;
      var river=e>SEA+.012&&e<.72&&rv<rw&&d>6;
      return{e:e,t:Math.max(0,Math.min(1,t)),m:Math.max(0,Math.min(1,m)),river:river,rv:rv};
    }
    function classify(s){
      var e=s.e,t=s.t,m=s.m;
      if(e<SEA-.07)return B.DEEP;
      if(e<SEA)return B.SEA;
      if(s.river)return B.RIVER;
      if(e<SEA+.02&&m<.78)return t<.25?B.TUNDRA:B.BEACH;
      if(e>.9)return B.PEAK;
      if(e>.79)return t<.3?B.SNOW:B.MOUNTAIN;
      if(t<.1)return B.SNOW;
      if(e>.69)return t<.3?B.TUNDRA:B.HILLS;
      if(t<.3)return m>.5?B.TAIGA:B.TUNDRA;
      if(t>.72){if(m<.36)return B.DESERT;if(m<.58)return B.SAVANNA;return m>.8?B.DENSE:B.FOREST}
      if(m<.3)return B.STEPPE;
      if(m>.8&&e<SEA+.09)return B.SWAMP;
      if(m>.7)return B.DENSE;
      if(m>.56)return B.FOREST;
      if(m>.44)return B.MEADOW;
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
    function tree(x,y){var b=at(x,y),r=TREE[b];if(!r)return null;
      /* bosques em manchas, não sal-e-pimenta */
      var clump=.55+fbm(nM,x/9+300,y/9-300,2,2,.5)*1.4;
      if(h2(x,y,7)>r[0]*Math.max(.2,clump))return null;
      var kind=r[1];if(b===B.FOREST&&h2(x,y,9)<.28)kind='birch';if(b===B.DENSE&&h2(x,y,9)<.35)kind='pine';if(b===B.TAIGA&&at(x,y)===B.TAIGA&&h2(x,y,3)<.1)kind='deadpine';
      return{kind:kind,v:Math.floor(h2(x,y,11)*3),snow:b===B.TUNDRA||(b===B.TAIGA&&elevation(x,y)>.62)};
    }
    function decor(x,y){var b=at(x,y),r=h2(x,y,21);
      if(b===B.MEADOW&&r<.16)return'flowers';if(b===B.GRASS&&r<.05)return'flowers';if((b===B.HILLS||b===B.MOUNTAIN)&&r<.1)return'rock';
      if(b===B.DESERT&&r<.04)return'rock';if(b===B.SWAMP&&r<.22)return'reeds';if(b===B.BEACH&&r<.03)return'shell';if(b===B.STEPPE&&r<.08)return'tuft';return null}
    function info(x,y){return INFO[at(x,y)]}
    return{
      seed:seedText||'urbe',CH:CH,B:B,INFO:INFO,sea:SEA,
      raw:raw,biome:at,elevation:elevation,chunk:chunk,
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
