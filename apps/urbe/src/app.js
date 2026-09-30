
(()=>{
"use strict";
/* Sprites das casas: vêm do atlas de pixel-art (src/world/pixel-art.js). As imagens
   antigas embutidas aqui (grama, água, ruas, árvores e casas em PNG, ~150 KB) saíram na 1.0:
   chão, ruas, árvores e construções já são desenhados pelo atlas. */
/* definidas mais abaixo, na versão em uso (as antigas saíram na 1.0) */
var pintarMapao=null,v21OpenGlobalSettings=function(){};
const A={};['house1','house2','house3'].forEach(function(k,v){Object.defineProperty(A,k,{configurable:true,get:function(){var ART=window.UrbeArt;if(!ART)return document.createElement('canvas');var c=ART.building('house',ART.styleFor('grass'),v,false);Object.defineProperty(A,k,{value:c,writable:true,configurable:true});return c}})});
function urbeSpriteURL(nome){try{var c=A[nome]||A.house1,d=document.createElement('canvas');d.width=c.width;d.height=c.height;d.getContext('2d').drawImage(c,0,0);return d.toDataURL('image/png')}catch(_){return''}}
/* ============ v0.13.5 — render sob demanda (correcao de travamento no celular)
   Antes o loop redesenhava a cidade inteira 60x por segundo mesmo com a tela
   parada. No celular isso saturava a thread principal e os toques ficavam
   presos na fila: parecia que o app tinha travado. Agora so se desenha quando
   algo muda (mais um heartbeat de seguranca a cada 500ms). ============ */
let precisaDesenhar=true;
function pedirDesenho(){precisaDesenhar=true}
let padraoGrama=null;
let roadsArr=null,roadsN=-1;

const cv=document.getElementById("game"),ctx=cv.getContext("2d"),wrap=document.getElementById("wrap");
ctx.imageSmoothingEnabled=false;
const TILE=32,BASE_W=72,BASE_H=50;
/* BASE_W/BASE_H definem apenas o enquadramento inicial. O mundo não possui bordas. */
let camera={x:BASE_W*TILE/2,y:BASE_H*TILE/2,z:1.15},tool="select",selected=null,regionDraft=null,ghost=null;
const world={regions:[],buildings:[],roads:new Set(),links:[]};
const colors=["#5bc2ff","#e38eff","#7ee3a0","#ffd567","#ff9a88"];
const K=(x,y)=>x+","+y,clamp=(v,a,b)=>Math.max(a,Math.min(b,v)),id=p=>p+Date.now().toString(36)+Math.random().toString(36).slice(2,5);
function slug(s){return String(s).toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g,"").replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"")||"item"}
function nowDate(){return new Date().toISOString().slice(0,10)}
function propertiesFor(b){
  const r=world.regions.find(x=>x.id===b.regionId);
  return {titulo:b.name,categoria:r?caminhoRegiao(r).map(x=>x.name).join("/"):"raiz",tags:b.tags||[],criado:b.created||nowDate(),modificado:b.modified||nowDate()}
}
function noise(x,y){const n=Math.sin(x*12.9898+y*78.233)*43758.5453;return n-Math.floor(n)}
/* terreno infinito: nada é guardado, tudo é função determinística de (x,y) */
function suave(x,y,e){
  const gx=Math.floor(x/e),gy=Math.floor(y/e),fx=x/e-gx,fy=y/e-gy;
  const s=t=>t*t*(3-2*t),u=s(fx),v=s(fy);
  const a=noise(gx,gy),b=noise(gx+1,gy),c=noise(gx,gy+1),d=noise(gx+1,gy+1);
  return a+(b-a)*u+(c-a)*v+(a-b-c+d)*u*v;
}
function ehAgua(x,y){return suave(x,y,11)>.70}
function arvoreEm(x,y){
  if(ehAgua(x,y))return 0;
  if(suave(x+40,y+70,8)<.44)return 0;
  return noise(x*3+11,y*3+29)>.90?1+Math.floor(noise(x+3,y+8)*3):0;
}
/* compatibilidade: código antigo consulta world.water.has("x,y") */
world.water={has:k=>{const p=k.split(",");return ehAgua(+p[0],+p[1])}};
/* índice espacial por chunk — sem ele nada disso escala */
const CH=16;
const chk=(x,y)=>Math.floor(x/CH)+":"+Math.floor(y/CH);
let idxB=new Map(),idxR=new Map(),idxSujo=true;
function marcarIndice(){idxSujo=true;pedirDesenho()}
function indexar(){
  idxB=new Map();idxR=new Map();
  const add=(m,k,o)=>{let a=m.get(k);if(!a)m.set(k,a=[]);a.push(o)};
  for(const b of world.buildings)
    for(let y=Math.floor(b.y/CH);y<=Math.floor((b.y+b.h-1)/CH);y++)
      for(let x=Math.floor(b.x/CH);x<=Math.floor((b.x+b.w-1)/CH);x++)add(idxB,x+":"+y,b);
  for(const r of world.regions)
    for(let y=Math.floor(r.y/CH);y<=Math.floor((r.y+r.h-1)/CH);y++)
      for(let x=Math.floor(r.x/CH);x<=Math.floor((r.x+r.w-1)/CH);x++)add(idxR,x+":"+y,r);
  idxSujo=false;
}
function indexarUm(m,o){
  const add=(k)=>{let a=m.get(k);if(!a)m.set(k,a=[]);a.push(o)};
  for(let y=Math.floor(o.y/CH);y<=Math.floor((o.y+o.h-1)/CH);y++)
    for(let x=Math.floor(o.x/CH);x<=Math.floor((o.x+o.w-1)/CH);x++)add(x+":"+y);
}
/* agua e arvores sao funcao pura de (x,y): calcula uma vez por chunk e guarda */
const TCH=16,terrCache=new Map();
function chunkTerreno(cx,cy){
  const k=cx+":"+cy;let c=terrCache.get(k);if(c)return c;
  const agua=[],arvores=[],x0=cx*TCH,y0=cy*TCH;
  for(let y=y0;y<y0+TCH;y++)for(let x=x0;x<x0+TCH;x++){
    if(ehAgua(x,y)){agua.push(x,y);continue}
    const s=arvoreEm(x,y);if(s)arvores.push(x,y,s);
  }
  c={agua,arvores};
  if(terrCache.size>600)terrCache.clear();
  terrCache.set(k,c);return c;
}
function chunksVisiveis(f){
  const out=[];
  for(let cy=Math.floor(f.y0/TCH);cy<=Math.floor(f.y1/TCH);cy++)
    for(let cx=Math.floor(f.x0/TCH);cx<=Math.floor(f.x1/TCH);cx++)out.push(chunkTerreno(cx,cy));
  return out;
}
function faixaVisivel(){
  const a=s2w(0,0),b=s2w(cv.w,cv.h);
  return {x0:Math.floor(a.x/TILE)-1,y0:Math.floor(a.y/TILE)-2,
          x1:Math.ceil(b.x/TILE)+1,y1:Math.ceil(b.y/TILE)+2};
}
function semente(txt){let h=2166136261;for(let i=0;i<txt.length;i++){h^=txt.charCodeAt(i);h=Math.imul(h,16777619)}return h>>>0}
function rng(s){let a=s>>>0;return()=>{a=(a+0x6D2B79F5)>>>0;let t=Math.imul(a^(a>>>15),1|a);t=(t+Math.imul(t^(t>>>7),61|t))^t;return((t^(t>>>14))>>>0)/4294967296}}
/* terreno gerado sob demanda; nada a pré-calcular */
function resize(){const d=Math.max(1,Math.min(innerWidth<900?1.5:2,devicePixelRatio||1));cv.width=Math.floor(wrap.clientWidth*d);cv.height=Math.floor(wrap.clientHeight*d);ctx.setTransform(d,0,0,d,0,0);ctx.imageSmoothingEnabled=false;cv.w=wrap.clientWidth;cv.h=wrap.clientHeight;padraoGrama=null;pedirDesenho()}
new ResizeObserver(resize).observe(wrap);resize();
function w2s(x,y){return{x:(x-camera.x)*camera.z+cv.w/2,y:(y-camera.y)*camera.z+cv.h/2}}
function s2w(x,y){return{x:(x-cv.w/2)/camera.z+camera.x,y:(y-cv.h/2)/camera.z+camera.y}}
function tileFromClient(x,y){const r=cv.getBoundingClientRect(),p=s2w(x-r.left,y-r.top);return{x:Math.floor(p.x/TILE),y:Math.floor(p.y/TILE)}}
function regionHasTile(r,x,y){
  if(!(x>=r.x&&x<r.x+r.w&&y>=r.y&&y<r.y+r.h))return false;
  if(!r.cells)return true;
  if(!r._cellSet)r._cellSet=new Set(r.cells);
  return r._cellSet.has(K(x,y));
}
function regAt(t){ /* devolve a região mais interna que contém o tile */
  const a=idxR.get(chk(t.x,t.y))||[];let m=null;
  for(const r of a)if(regionHasTile(r,t.x,t.y))
    if(!m||r.w*r.h<m.w*m.h)m=r;
  return m||null;
}
function regPai(r){return r&&r.parentId?world.regions.find(x=>x.id===r.parentId):null}
function caminhoRegiao(r){const p=[];let c=r;while(c){p.unshift(c);c=regPai(c)}return p}
function bAt(t){const a=idxB.get(chk(t.x,t.y))||[];
  for(let i=a.length-1;i>=0;i--){const b=a[i];if(t.x>=b.x&&t.x<b.x+b.w&&t.y>=b.y&&t.y<b.y+b.h)return b}
  return null;}

/* ---------- Regra única de lote (a mesma do import) ----------
   3x3 seco + vão de acesso abaixo livre + 2 tiles de afastamento das vizinhas.
   Passar r=região para exigir que tudo caia dentro dos tiles reais dela. */
const LOTE_GAP=2;
function acessosLote(x,y,r=null){
  /* A construção 3x3 pertence à região; a via de acesso NÃO precisa ficar
     confinada nela. Isso evita que uma máscara orgânica estreita inutilize
     lotes perfeitamente construíveis nas bordas da pasta/região. */
  const candidatos=[
    {dir:"norte",cells:[[x+1,y-1],[x+1,y-2]]},
    {dir:"sul",cells:[[x+1,y+3],[x+1,y+4]]},
    {dir:"oeste",cells:[[x-1,y+1],[x-2,y+1]]},
    {dir:"leste",cells:[[x+3,y+1],[x+4,y+1]]}
  ];
  return candidatos.map(a=>({...a,ok:a.cells.every(([xx,yy])=>
    !ehAgua(xx,yy)&&!tileBlockedByBuilding(xx,yy)
  )}));
}
function loteValido(x,y,ignorarIds=new Set(),r=null){
  for(let yy=y;yy<y+3;yy++)for(let xx=x;xx<x+3;xx++){
    if(ehAgua(xx,yy))return false;
    if(r){if(!regionHasTile(r,xx,yy))return false;const maisInterna=regAt({x:xx,y:yy});if(maisInterna&&maisInterna.id!==r.id)return false;}
  }
  if(!acessosLote(x,y,r).some(a=>a.ok))return false;
  for(const b of world.buildings){
    if(ignorarIds.has(b.id))continue;
    if(x<b.x+b.w+LOTE_GAP&&x+3+LOTE_GAP>b.x&&y<b.y+b.h+LOTE_GAP&&y+3+LOTE_GAP>b.y)return false;
  }
  return true;
}
function canHouse(t){const r=regAt(t);return loteValido(t.x,t.y,new Set(),r)}
function centroBuscaRaiz(){
  const bs=world.buildings.filter(b=>!b.regionId);
  if(bs.length){return{x:Math.round(bs.reduce((a,b)=>a+b.x,0)/bs.length),y:Math.round(bs.reduce((a,b)=>a+b.y,0)/bs.length)}}
  return{x:Math.floor(camera.x/TILE),y:Math.floor(camera.y/TILE)};
}
function loteForaDeRegioes(x,y,w=3,h=3){
  for(let yy=y;yy<y+h;yy++)for(let xx=x;xx<x+w;xx++)if(regAt({x:xx,y:yy}))return false;
  return true;
}
function vagaAleatoria(sem,w=3,h=3,area){
  /* Busca expansiva sem fronteira de mundo. O raio cresce quando a vizinhança
     atual lota; coordenadas negativas e distâncias arbitrárias são válidas. */
  const rand=rng(sem),c=area||centroBuscaRaiz();
  for(let faixa=0;faixa<14;faixa++){
    const raio=12*Math.pow(2,faixa);
    for(let i=0;i<260;i++){
      const ang=rand()*Math.PI*2,dist=Math.sqrt(rand())*raio;
      const x=Math.round(c.x+Math.cos(ang)*dist),y=Math.round(c.y+Math.sin(ang)*dist);
      if(loteForaDeRegioes(x,y,w,h)&&loteValido(x,y))return{x,y};
    }
  }
  return null;
}
/* Knowledge graph -> city road network */
function normName(s){
  return slug(String(s||"").replace(/\.md$/i,"")).toLowerCase();
}
function linksFromContent(b){
  const core=window.UrbeCore,docs=core&&core.service('documents'),knowledge=core&&core.service('knowledge');
  const doc=docs&&b&&docs.get(b.documentId||b.path||b.name);
  if(doc&&knowledge)return knowledge.links(doc.id).map(d=>normName(d.title));
  return doc?(doc.links||[]).map(normName):[];
}
function buildingByLinkName(name){
  const core=window.UrbeCore,docs=core&&core.service('documents');
  const doc=docs&&docs.list().find(d=>normName(d.title)===name||normName(d.path)===name);
  return doc?world.buildings.find(b=>b.documentId===doc.id):null;
}
function tileBlockedByBuilding(x,y){
  return world.buildings.some(b=>x>=b.x&&x<b.x+b.w&&y>=b.y&&y<b.y+b.h);
}
/* Toda casa acessa a malha pela parte inferior.
   O primeiro tile imediatamente abaixo da casa fica livre; a rua começa no segundo tile. */
function lowerRoadStart(b){
  const x=Math.round(b.x+(b.w-1)/2);
  const y=b.y+b.h+1;
  return {x,y};
}
function isHouseAccessGap(x,y){
  return world.buildings.some(b=>x===Math.round(b.x+(b.w-1)/2)&&y===b.y+b.h);
}
function roadComponentFrom(start){
  const sk=K(start.x,start.y);
  if(!world.roads.has(sk))return null;
  const seen=new Set([sk]),q=[start],nodes=[];
  for(let qi=0;qi<q.length;qi++){
    const cur=q[qi];nodes.push(cur);
    for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
      const x=cur.x+dx,y=cur.y+dy,k=K(x,y);
      if(seen.has(k)||!world.roads.has(k))continue;
      seen.add(k);q.push({x,y});
    }
  }
  return nodes;
}
/* Nó viário real: a estrada ocupa os dois eixos neste tile.
   Segmento somente horizontal ou somente vertical NÃO é nó. Curvas, T e cruzamentos são. */
function roadAxesAt(x,y,roadSet=world.roads){
  const h=roadSet.has(K(x-1,y))||roadSet.has(K(x+1,y));
  const v=roadSet.has(K(x,y-1))||roadSet.has(K(x,y+1));
  return {h,v};
}
function isRoadNode(x,y,roadSet=world.roads){
  if(!roadSet.has(K(x,y)))return false;
  const a=roadAxesAt(x,y,roadSet);
  return a.h&&a.v;
}
function manhattan(a,b){return Math.abs(a.x-b.x)+Math.abs(a.y-b.y)}
function nearestRoadNode(from,nodes){
  let best=null,bestD=Infinity;
  for(const n of nodes||[]){
    if(!isRoadNode(n.x,n.y))continue;
    const d=manhattan(from,n);
    if(d<bestD){best=n;bestD=d}
  }
  return best;
}
/* Escolhe o melhor ponto da rua para NASCER um nó. Segmentos retos são
   candidatos: a nova rua deve chegar pelo eixo perpendicular. */
function nearestCreatableRoadNode(from,component){
  let best=null;
  for(const join of component||[]){
    const axes=roadAxesAt(join.x,join.y), approaches=[];
    if(axes.h&&!axes.v) approaches.push({x:join.x,y:join.y-1},{x:join.x,y:join.y+1});
    else if(axes.v&&!axes.h) approaches.push({x:join.x-1,y:join.y},{x:join.x+1,y:join.y});
    else continue; // curva/T/cruzamento já seria nó existente
    for(const ap of approaches){
      if(world.water.has(K(ap.x,ap.y))||isHouseAccessGap(ap.x,ap.y)||tileBlockedByBuilding(ap.x,ap.y))continue;
      const d=manhattan(from,ap)+1;
      if(!best||d<best.d)best={join,ap,d};
    }
  }
  return best;
}
function layPath(path){for(const p of path)world.roads.add(K(p.x,p.y))}
function connectToExistingNetwork(from,component){
  const existing=nearestRoadNode(from,component);
  const creatable=nearestCreatableRoadNode(from,component);
  /* Prioridade espacial real: se criar um nó numa avenida reta for mais perto
     do que viajar até uma curva distante, cria-se o nó novo. */
  const de=existing?manhattan(from,existing):Infinity;
  const dc=creatable?creatable.d:Infinity;
  if(creatable&&dc<de){
    const path=routeAStar(from,creatable.ap);
    if(path.length){layPath(path);world.roads.add(K(creatable.join.x,creatable.join.y));return creatable.join}
  }
  if(existing){
    const path=routeAStar(from,existing);
    if(path.length){layPath(path);return existing}
  }
  if(creatable){
    const path=routeAStar(from,creatable.ap);
    if(path.length){layPath(path);world.roads.add(K(creatable.join.x,creatable.join.y));return creatable.join}
  }
  return null;
}
function routeAStar(start,goal){
  const dirs=[[1,0],[-1,0],[0,1],[0,-1]],distBase=manhattan(start,goal);
  /* Limite apenas do ALGORITMO por tentativa, nunca do mundo: se água/prédios
     bloquearem o caminho, o envelope aumenta e a busca recomeça. */
  for(const extra of [24,48,96,192,384]){
    const minX=Math.min(start.x,goal.x)-extra,maxX=Math.max(start.x,goal.x)+extra;
    const minY=Math.min(start.y,goal.y)-extra,maxY=Math.max(start.y,goal.y)+extra;
    const key=(x,y,d)=>x+","+y+","+d;
    const open=[{x:start.x,y:start.y,d:-1,g:0,f:distBase}];
    const best=new Map([[key(start.x,start.y,-1),0]]),came=new Map();let final=null,passos=0;
    while(open.length&&passos++<120000){
      open.sort((a,b)=>a.f-b.f);const cur=open.shift();
      if(cur.x===goal.x&&cur.y===goal.y){final=cur;break}
      for(let nd=0;nd<4;nd++){
        const [dx,dy]=dirs[nd],x=cur.x+dx,y=cur.y+dy;
        if(x<minX||x>maxX||y<minY||y>maxY||world.water.has(K(x,y))||isHouseAccessGap(x,y))continue;
        if(tileBlockedByBuilding(x,y)&&!(x===goal.x&&y===goal.y))continue;
        const reuse=world.roads.has(K(x,y)),turn=(cur.d!==-1&&cur.d!==nd)?1.35:0,step=reuse?.06:1;
        const g=cur.g+step+turn,h=manhattan({x,y},goal),nk=key(x,y,nd);
        if(g<(best.get(nk)??Infinity)){best.set(nk,g);const node={x,y,d:nd,g,f:g+h};came.set(nk,{prev:cur,node});open.push(node)}
      }
    }
    if(final){const path=[];let node=final;while(node){path.push({x:node.x,y:node.y});const rec=came.get(key(node.x,node.y,node.d));node=rec?.prev||null}return path.reverse()}
  }
  return [];
}
function addSemanticRoute(a,b){
  const sa=lowerRoadStart(a),sb=lowerRoadStart(b);
  const netB=roadComponentFrom(sb);
  if(netB&&netB.length&&connectToExistingNetwork(sa,netB))return;
  const netA=roadComponentFrom(sa);
  if(netA&&netA.length&&connectToExistingNetwork(sb,netA))return;
  /* Primeira ligação da componente: o ponto final é o primeiro nó de referência. */
  layPath(routeAStar(sa,sb));
}
function roadConnectedBetweenBuildings(a,b){
  const sa=lowerRoadStart(a),sb=lowerRoadStart(b);
  if(!world.roads.has(K(sa.x,sa.y))||!world.roads.has(K(sb.x,sb.y)))return false;
  const comp=roadComponentFrom(sa);
  return !!comp&&comp.some(p=>p.x===sb.x&&p.y===sb.y);
}
function ensureSemanticRoute(a,b){
  if(roadConnectedBetweenBuildings(a,b))return true;
  addSemanticRoute(a,b);
  return roadConnectedBetweenBuildings(a,b);
}
function rebuildRoadNetwork(){
  world.roads.clear();
  world.links=[];
  const pairs=[];
  const seen=new Set();

  for(const b of world.buildings){
    for(const targetName of linksFromContent(b)){
      const t=buildingByLinkName(targetName);
      if(!t||t===b)continue;
      const pair=[b.id,t.id].sort().join("|");
      if(seen.has(pair))continue;
      seen.add(pair);
      pairs.push([b,t]);
      world.links.push({from:b.id,to:t.id});
    }
  }

  // Shorter semantic connections first; later routes reuse the street network they create.
  pairs.sort((a,b)=>{
    const da=Math.abs(a[0].x-a[1].x)+Math.abs(a[0].y-a[1].y);
    const db=Math.abs(b[0].x-b[1].x)+Math.abs(b[0].y-b[1].y);
    return da-db;
  });

  for(const [a,b] of pairs)addSemanticRoute(a,b);
  /* validação final: todo [[link]] resolvido precisa pertencer à mesma componente viária */
  for(const [a,b] of pairs)ensureSemanticRoute(a,b);
  roadsArr=null;roadsN=-1;pedirDesenho();
  counts();
}
let roadRebuildTimer=null;
function scheduleRoadRebuild(){
  clearTimeout(roadRebuildTimer);
  roadRebuildTimer=setTimeout(function(){
    /* A semântica vem do KnowledgeIndex/RoadGraph; o roteador legado apenas materializa tiles. */
    try{var _roads=window.UrbeCore&&window.UrbeCore.service('aquarium.roads');if(_roads&&_roads.pending().length===0){pedirDesenho();return}}catch(_){}
    rebuildRoadNetwork();
  },120);
}

function drawImg(img,x,y,w=1,h=1){const p=w2s(x*TILE,y*TILE);ctx.drawImage(img,p.x,p.y,w*TILE*camera.z,h*TILE*camera.z)}
function drawSprite(img,x,y,w,h,sy=1.22){const p=w2s(x*TILE,y*TILE),ww=w*TILE*camera.z,base=h*TILE*camera.z,hh=base*sy;ctx.drawImage(img,p.x,p.y-(hh-base),ww,hh)}
function drawGround(){
  const f=faixaVisivel(),g=A.grass;
  /* grama: 1 fillRect com pattern no lugar de ~1200 drawImage por quadro */
  let pintou=false;
  if(g.complete&&g.naturalWidth){
    if(!padraoGrama)padraoGrama=ctx.createPattern(g,"repeat");
    if(padraoGrama&&padraoGrama.setTransform&&typeof DOMMatrix==="function"){
      const o=w2s(f.x0*TILE,f.y0*TILE);
      padraoGrama.setTransform(new DOMMatrix().translateSelf(o.x,o.y)
        .scaleSelf(TILE*camera.z/g.naturalWidth,TILE*camera.z/g.naturalHeight));
      ctx.fillStyle=padraoGrama;ctx.fillRect(0,0,cv.w,cv.h);pintou=true;
    }
  }
  if(!pintou){ctx.fillStyle="#20321d";ctx.fillRect(0,0,cv.w,cv.h)}
  /* agua: poucos tiles, desenho normal, ja pre-calculados por chunk */
  for(const c of chunksVisiveis(f)){const a=c.agua;
    for(let i=0;i<a.length;i+=2){const x=a[i],y=a[i+1];
      if(x<f.x0||x>f.x1||y<f.y0||y>f.y1)continue;drawImg(A.water,x,y)}}
}
function drawRegions(){
  const f=faixaVisivel();
  const vis=world.regions.filter(r=>r.x<f.x1&&r.x+r.w>f.x0&&r.y<f.y1&&r.y+r.h>f.y0)
    .sort((a,b)=>b.w*b.h-a.w*a.h);
  for(const r of vis){
    const nivel=caminhoRegiao(r).length-1;
    ctx.fillStyle=r.color+(nivel?"18":"22");ctx.strokeStyle=r.color;ctx.lineWidth=nivel?1:2;
    if(r.cells){
      if(!r._cellSet)r._cellSet=new Set(r.cells);
      for(const k of r.cells){
        const [x,y]=k.split(",").map(Number);if(x<f.x0||x>f.x1||y<f.y0||y>f.y1)continue;
        const p=w2s(x*TILE,y*TILE),sz=TILE*camera.z;ctx.fillRect(p.x,p.y,sz,sz);
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]])if(!r._cellSet.has(K(x+dx,y+dy))){ctx.beginPath();
          if(dx===1){ctx.moveTo(p.x+sz,p.y);ctx.lineTo(p.x+sz,p.y+sz)}
          if(dx===-1){ctx.moveTo(p.x,p.y);ctx.lineTo(p.x,p.y+sz)}
          if(dy===1){ctx.moveTo(p.x,p.y+sz);ctx.lineTo(p.x+sz,p.y+sz)}
          if(dy===-1){ctx.moveTo(p.x,p.y);ctx.lineTo(p.x+sz,p.y)}ctx.stroke()}
      }
    }else{
      const p=w2s(r.x*TILE,r.y*TILE),ww=r.w*TILE*camera.z,hh=r.h*TILE*camera.z;ctx.fillRect(p.x,p.y,ww,hh);
      ctx.setLineDash(nivel?[3,3]:[6,4]);ctx.strokeRect(p.x+1,p.y+1,ww-2,hh-2);ctx.setLineDash([]);
    }
    const p=w2s(r.x*TILE,r.y*TILE);ctx.fillStyle=nivel?"#cfe0ea":"#fff";
    ctx.font=`${nivel?"":"bold "}${Math.max(10,(nivel?11:13)*camera.z)}px ui-monospace`;ctx.fillText(r.name,p.x+8,p.y+18);
  }}
function roadsParsed(){
  if(roadsArr&&roadsN===world.roads.size)return roadsArr;
  roadsArr=[];for(const s of world.roads){const i=s.indexOf(",");roadsArr.push(+s.slice(0,i),+s.slice(i+1))}
  roadsN=world.roads.size;return roadsArr;
}
function drawRoads(){const f=faixaVisivel(),a=roadsParsed();
  for(let i=0;i<a.length;i+=2){const x=a[i],y=a[i+1];
    if(x<f.x0||x>f.x1||y<f.y0||y>f.y1)continue;drawImg(A.road,x,y)}}
function drawTrees(){
  if(camera.z<.45)return; /* longe demais: não vale o custo */
  const f=faixaVisivel(),p={x:0,y:0};
  for(const c of chunksVisiveis(f)){const a=c.arvores;
    for(let i=0;i<a.length;i+=3){
      const x=a[i],y=a[i+1];
      if(x<f.x0||x>f.x1||y<f.y0||y>f.y1)continue;
      if(world.roads.has(K(x,y)))continue;
      p.x=x;p.y=y;if(bAt(p)||regAt(p))continue;
      drawSprite(A["tree"+a[i+2]],x-.15,y-.8,1.45,2.1,1);
    }}}

function drawArquivoBuilding(b){
  const p=w2s(b.x*TILE,b.y*TILE),sz=TILE*camera.z,w=b.w*sz,h=b.h*sz;
  const cfg={image:["#6f8f63","IMG"],audio:["#7c648f","AUD"],video:["#596f87","VID"],pdf:["#8a5d55","PDF"],html:["#526f91","HTML"],zip:["#8a7a50","ZIP"],other:["#66747d","FILE"]}[b.fileClass||"other"];
  ctx.save();ctx.imageSmoothingEnabled=false;ctx.fillStyle="#202a30";ctx.fillRect(p.x,p.y+h*.28,w,h*.72);ctx.fillStyle=cfg[0];ctx.fillRect(p.x+w*.08,p.y+h*.12,w*.84,h*.22);ctx.fillStyle="#0d151a";ctx.fillRect(p.x+w*.18,p.y+h*.46,w*.64,h*.32);ctx.strokeStyle="#d7e2e8";ctx.lineWidth=Math.max(1,camera.z);ctx.strokeRect(p.x+w*.18,p.y+h*.46,w*.64,h*.32);ctx.fillStyle="#edf4f7";ctx.font=`bold ${Math.max(7,Math.round(9*camera.z))}px ui-monospace`;ctx.textAlign="center";ctx.fillText(cfg[1],p.x+w*.5,p.y+h*.67);ctx.textAlign="left";ctx.restore();
}
function drawBuildings(){
  const f=faixaVisivel();
  for(const b of world.buildings){
    if(b.x>f.x1||b.x+b.w<f.x0||b.y>f.y1||b.y+b.h<f.y0)continue;
    if(b.tipo==="arquivo"||b.tipo==="anexo")drawArquivoBuilding(b);else drawSprite(A[b.sprite]||A.house1,b.x,b.y,b.w,b.h,1.18);
    if(b.anexos&&b.anexos.length){ /* galpão colado na casa que usa o anexo */
      const p=w2s((b.x+b.w-.9)*TILE,(b.y+b.h-.75)*TILE),t=TILE*camera.z;
      ctx.fillStyle="#6b5a3e";ctx.fillRect(p.x,p.y,t*.8,t*.62);
      ctx.fillStyle="#8a7550";ctx.fillRect(p.x,p.y,t*.8,t*.16);
      if(camera.z>.9){ctx.fillStyle="#e8dfcd";ctx.font=`${Math.round(9*camera.z)}px ui-monospace`;
        ctx.fillText(b.anexos.length,p.x+t*.3,p.y+t*.5)}
    }
    if(selected===b){const p=w2s(b.x*TILE,b.y*TILE);ctx.strokeStyle="#fff";ctx.lineWidth=2;
      ctx.strokeRect(p.x,p.y,b.w*TILE*camera.z,b.h*TILE*camera.z)}
    if(camera.z>=.72){
      const p=w2s((b.x+b.w/2)*TILE,(b.y+b.h)*TILE),raw=nomeExibidoArquivo(b),label=raw.length>34?raw.slice(0,31)+"…":raw;
      ctx.save();ctx.font=`${Math.max(9,Math.round(10*camera.z))}px ui-monospace`;ctx.textAlign="center";ctx.textBaseline="top";
      const mw=ctx.measureText(label).width,pad=4*camera.z,yy=p.y+4*camera.z;
      ctx.fillStyle="#081017c9";ctx.fillRect(p.x-mw/2-pad,yy-2,mw+pad*2,Math.max(14,13*camera.z));
      ctx.fillStyle="#eef5f8";ctx.fillText(label,p.x,yy);ctx.restore();
    }
  }}
function drawOverlay(){
  if(tool==="house"&&ghost){
    const r=regAt(ghost),ok=loteValido(ghost.x,ghost.y,new Set(),r),p=w2s(ghost.x*TILE,ghost.y*TILE);
    ctx.globalAlpha=.65;drawSprite(A.house1,ghost.x,ghost.y,3,3,1.18);ctx.globalAlpha=1;
    ctx.strokeStyle=ok?"#75ff88":"#ff6767";ctx.lineWidth=3;ctx.strokeRect(p.x,p.y,3*TILE*camera.z,3*TILE*camera.z);
    for(const acesso of acessosLote(ghost.x,ghost.y,r)){
      for(const [ax,ay] of acesso.cells){
        const ap=w2s(ax*TILE,ay*TILE);
        ctx.fillStyle=acesso.ok?"#75ff8844":"#ff676744";ctx.fillRect(ap.x,ap.y,TILE*camera.z,TILE*camera.z);
        ctx.strokeStyle=acesso.ok?"#75ff88":"#ff6767";ctx.lineWidth=1;ctx.strokeRect(ap.x,ap.y,TILE*camera.z,TILE*camera.z);
      }
    }
  }
  if(regionDraft){const x=Math.min(regionDraft.a.x,regionDraft.b.x),y=Math.min(regionDraft.a.y,regionDraft.b.y),w=Math.abs(regionDraft.a.x-regionDraft.b.x)+1,h=Math.abs(regionDraft.a.y-regionDraft.b.y)+1,p=w2s(x*TILE,y*TILE);ctx.fillStyle="#61c8ff22";ctx.fillRect(p.x,p.y,w*TILE*camera.z,h*TILE*camera.z);ctx.strokeStyle="#9fe4ff";ctx.setLineDash([6,4]);ctx.strokeRect(p.x,p.y,w*TILE*camera.z,h*TILE*camera.z);ctx.setLineDash([])}
}
let ultimoDesenho=-1e9,ultimoMini=-1e9,ultimoBase=-1e9,pedidoAnim=false,baseCv=null,baseCtx=null,baseChave='',ultimaChave='';
/* Moradores e bichos pedem só "animação": chão, bairros e ruas (as camadas de baixo, que
   custam milhares de operações) são reaproveitados da última pintura enquanto a câmera não
   se mexe. Antes cada passo de um morador repintava a cidade inteira (lag no celular). */
function pedirAnimacao(){pedidoAnim=true}
function frame(t){
  t=t||0;
  if(!(precisaDesenhar||pedidoAnim||idxSujo||t-ultimoDesenho>500)){requestAnimationFrame(frame);return}
  const chave=camera.x+'|'+camera.y+'|'+camera.z+'|'+cv.width+'|'+cv.height;
  const soAnim=!precisaDesenhar&&!idxSujo&&baseCv&&baseChave===chave&&t-ultimoBase<1000;
  precisaDesenhar=false;pedidoAnim=false;ultimoDesenho=t;
  if(idxSujo)indexar();
  if(soAnim){ctx.save();ctx.setTransform(1,0,0,1,0,0);ctx.clearRect(0,0,cv.width,cv.height);ctx.drawImage(baseCv,0,0);ctx.restore()}
  else{
    ctx.clearRect(0,0,cv.w,cv.h);
    drawGround();drawRegions();drawRoads();
    /* guarda a camada só com a câmera parada (arrastando, ela nunca seria reaproveitada) */
    if(chave===ultimaChave)try{if(!baseCv){baseCv=document.createElement('canvas');baseCtx=baseCv.getContext('2d')}
      if(baseCv.width!==cv.width||baseCv.height!==cv.height){baseCv.width=cv.width;baseCv.height=cv.height}
      baseCtx.clearRect(0,0,baseCv.width,baseCv.height);baseCtx.drawImage(cv,0,0);baseChave=chave;ultimoBase=t}catch(_){baseChave=''}
    else baseChave='';
    ultimaChave=chave;
  }
  drawTrees();drawBuildings();drawOverlay();
  if(t-ultimoMini>300){ultimoMini=t;desenharMini()}
  requestAnimationFrame(frame);}
requestAnimationFrame(frame);

const tools=[...document.querySelectorAll(".tool[data-tool]")],hint=document.getElementById("hint");
function setTool(t){tool=t;ghost=null;pedirDesenho();tools.forEach(b=>b.classList.toggle("active",b.dataset.tool===t));hint.textContent={select:"Arraste para mover. Toque para selecionar. Pinça para zoom.",region:"Arraste para delimitar uma nova região.",house:"Toque em terra seca para construir. O acesso pode ficar em qualquer lado do lote."}[t]}
tools.forEach(b=>b.onclick=()=>setTool(b.dataset.tool));
/* Diálogos próprios (src/ui/dialogs.js); nenhum prompt/confirm nativo na interface. */
const UD=window.UrbeDialogs;
function uiDestinoRegiao(titulo){return UD.choose({title:titulo||'Mover para',options:[{value:null,icon:'notes',label:'Raiz',detail:'Fora de qualquer pasta'}].concat(world.regions.map(r=>({value:r.id,icon:'folder',label:r.name,detail:caminhosRegioes().get(r.id)||r.name})))})}
function toast(s){const t=document.getElementById("toast");t.textContent=s;t.classList.add("show");clearTimeout(toast._t);toast._t=setTimeout(()=>t.classList.remove("show"),1800)}
function counts(){document.getElementById("noteCount").textContent=world.buildings.filter(b=>b.tipo==="nota").length;document.getElementById("fileCount").textContent=world.buildings.filter(b=>b.tipo!=="nota").length;document.getElementById("regionCount").textContent=world.regions.length;document.getElementById("linkCount").textContent=world.links.length;document.getElementById("zoomPct").textContent=Math.round(camera.z*100)+"%";pedirDesenho()}counts();

/* Touch gestures */
const pointers=new Map();let gesture=null;
cv.addEventListener("pointerdown",e=>{cv.setPointerCapture(e.pointerId);pointers.set(e.pointerId,{x:e.clientX,y:e.clientY});if(pointers.size===1){const t=tileFromClient(e.clientX,e.clientY);gesture={mode:tool,start:t,sx:e.clientX,sy:e.clientY,cx:camera.x,cy:camera.y,moved:false};if(tool==="region")regionDraft={a:t,b:t}}else if(pointers.size===2){const ps=[...pointers.values()];gesture={mode:"pinch",dist:Math.hypot(ps[0].x-ps[1].x,ps[0].y-ps[1].y),z:camera.z}}});
cv.addEventListener("pointermove",e=>{if(!pointers.has(e.pointerId))return;const p=pointers.get(e.pointerId);p.x=e.clientX;p.y=e.clientY;if(pointers.size===2&&gesture?.mode==="pinch"){const ps=[...pointers.values()],d=Math.hypot(ps[0].x-ps[1].x,ps[0].y-ps[1].y);camera.z=clamp(gesture.z*d/gesture.dist,.5,2.8);counts();return}if(pointers.size!==1||!gesture)return;const t=tileFromClient(e.clientX,e.clientY),dx=e.clientX-gesture.sx,dy=e.clientY-gesture.sy;if(Math.abs(dx)+Math.abs(dy)>7)gesture.moved=true;if(tool==="select"&&gesture.moved){camera.x=gesture.cx-dx/camera.z;camera.y=gesture.cy-dy/camera.z}else if(tool==="region"&&regionDraft)regionDraft.b=t;if(tool==="house")ghost=t});
cv.addEventListener("pointerup",e=>{pointers.delete(e.pointerId);if(!gesture||gesture.mode==="pinch"){if(!pointers.size)gesture=null;return}const t=tileFromClient(e.clientX,e.clientY);if(tool==="select"&&!gesture.moved){const b=bAt(t),rSel=regAt(t);selected=b||rSel||null;if(b){if(b.tipo==="nota")openHouseSummary(b);else openFilePreview(b)}else if(rSel){closeHouseSummary();closeFilePreview();openRegionEditDialog(rSel)}else{closeHouseSummary();closeFilePreview()}}else if(tool==="region"&&regionDraft){const a=regionDraft.a,b=regionDraft.b;regionDraft=null;const x=Math.min(a.x,b.x),y=Math.min(a.y,b.y),w=Math.abs(a.x-b.x)+1,h=Math.abs(a.y-b.y)+1;if(w>=3&&h>=3)openRegionDialog({x,y,w,h});else toast("A região precisa ser maior.")}else if(tool==="house"&&!gesture.moved){if(canHouse(t)){const r=regAt(t),n=world.buildings.length%3+1,b={id:id("b"),kind:"building",regionId:r?r.id:null,x:t.x,y:t.y,w:3,h:3,name:"Nova nota",description:"",content:"",sprite:"house"+n,tipo:"nota",tags:[],created:nowDate(),modified:nowDate()};world.buildings.push(b);selected=b;marcarIndice();indexar();openHouseSummary(b);counts();scheduleRoadRebuild();agendarSalvar();toast("Nota criada"+(r?" em "+r.name:" na raiz")+".")}else toast("Lote inválido: precisa de 3x3 seco, acesso livre em pelo menos um dos 4 lados e 2 tiles de folga das vizinhas.")}gesture=null});
cv.addEventListener("pointercancel",e=>{pointers.delete(e.pointerId);gesture=null;regionDraft=null});
/* qualquer interacao com o canvas pede um quadro novo */
["pointerdown","pointermove","pointerup","pointercancel","wheel"]
  .forEach(ev=>cv.addEventListener(ev,pedirDesenho,{passive:true}));
cv.addEventListener("wheel",e=>{e.preventDefault();camera.z=clamp(camera.z*(e.deltaY<0?1.12:.89),.5,2.8);counts()},{passive:false});


/* ---------- Fase 2: preview nativo de arquivos ---------- */
const filePreview=document.getElementById("filePreview"),filePreviewBody=document.getElementById("filePreviewBody"),filePreviewSelect=document.getElementById("filePreviewSelect");
let previewBuilding=null,previewIndex=0,previewObjectUrl=null,previewHtmlMode="render";
function limparPreviewObjectUrl(){if(previewObjectUrl){URL.revokeObjectURL(previewObjectUrl);previewObjectUrl=null}}
function arquivosDoPredio(b){const a=b?.files||b?.anexos||[];return a.length?a:[{nome:b?.fileName||b?.name||"arquivo",tipo:b?.fileClass||"other"}]}
/* (blobDoArquivo: definição antiga removida na 1.0 — a versão em uso está mais abaixo) */
function closeFilePreview(){limparPreviewObjectUrl();filePreview.classList.remove("open");filePreview.setAttribute("aria-hidden","true");previewBuilding=null;filePreviewBody.innerHTML=""}
async function renderFilePreview(){
  if(!previewBuilding)return;limparPreviewObjectUrl();
  const files=arquivosDoPredio(previewBuilding),a=files[Math.max(0,Math.min(previewIndex,files.length-1))],classe=previewBuilding.fileClass||classificarArquivo(a.nome||"");
  document.getElementById("filePreviewTitle").textContent=a.nome||previewBuilding.name;
  document.getElementById("filePreviewMeta").textContent=rotuloClasse(classe)+(a.tamanho?" · "+formatBytes(a.tamanho):"")+(previewBuilding.regionId?" · "+(caminhoRegiao(world.regions.find(r=>r.id===previewBuilding.regionId)||{}).map(x=>x.name).join(" / ")||"raiz"):" · raiz");
  const srcBtn=document.getElementById("htmlSourceBtn"),renderBtn=document.getElementById("htmlRenderBtn"),extBtn=document.getElementById("openExternalBtn"),downBtn=document.getElementById("downloadPreviewBtn");
  srcBtn.hidden=renderBtn.hidden=classe!=="html";extBtn.hidden=classe!=="pdf";downBtn.hidden=true;
  filePreviewBody.innerHTML='<div class="previewEmpty">Carregando…</div>';
  const blob=await blobDoArquivo(a);
  if(!blob){filePreviewBody.innerHTML='<div class="previewEmpty"><b>Preview não disponível</b>O arquivo está catalogado, mas os bytes não estão neste dispositivo. Reimporte o arquivo para recriar o cache offline.</div>';return}
  previewObjectUrl=URL.createObjectURL(blob);downBtn.hidden=false;downBtn.onclick=()=>baixarBlob(blob,a.nome||previewBuilding.name);
  extBtn.onclick=()=>{const u=URL.createObjectURL(blob);window.open(u,"_blank","noopener");setTimeout(()=>URL.revokeObjectURL(u),60000)};
  if(classe==="image")filePreviewBody.innerHTML='<img alt="">',filePreviewBody.querySelector("img").src=previewObjectUrl;
  else if(classe==="audio")filePreviewBody.innerHTML='<audio controls preload="metadata"></audio>',filePreviewBody.querySelector("audio").src=previewObjectUrl;
  else if(classe==="video")filePreviewBody.innerHTML='<video controls playsinline preload="metadata"></video>',filePreviewBody.querySelector("video").src=previewObjectUrl;
  else if(classe==="pdf")filePreviewBody.innerHTML='<div class="previewEmpty"><b>PDF pronto</b>Use “Abrir” para visualizar em uma nova aba ou “Baixar arquivo” para salvar uma cópia.</div>';
  else if(classe==="html"){
    const txt=await blob.text();
    if(previewHtmlMode==="source"){const pre=document.createElement("pre");pre.textContent=txt;filePreviewBody.replaceChildren(pre)}
    else{const fr=document.createElement("iframe");fr.setAttribute("sandbox","");fr.setAttribute("referrerpolicy","no-referrer");fr.srcdoc=txt;filePreviewBody.replaceChildren(fr)}
  }else filePreviewBody.innerHTML='<div class="previewEmpty"><b>'+escapeHTML(a.nome||"Arquivo")+'</b>Este formato não possui visualização nativa, mas pode ser baixado.</div>';
}
function openFilePreview(b){
  if(!b||b.tipo==="nota")return;closeHouseSummary();previewBuilding=b;previewIndex=0;previewHtmlMode="render";
  const files=arquivosDoPredio(b);filePreviewSelect.innerHTML=files.map((a,i)=>'<option value="'+i+'">'+escapeHTML(a.nome||("Arquivo "+(i+1)))+'</option>').join("");filePreviewSelect.hidden=files.length<2;
  filePreview.classList.add("open");filePreview.setAttribute("aria-hidden","false");renderFilePreview();
}
filePreviewSelect.onchange=()=>{previewIndex=+filePreviewSelect.value||0;previewHtmlMode="render";renderFilePreview()};
document.getElementById("closeFilePreview").onclick=closeFilePreview;
document.getElementById("htmlSourceBtn").onclick=()=>{previewHtmlMode="source";renderFilePreview()};
document.getElementById("htmlRenderBtn").onclick=()=>{previewHtmlMode="render";renderFilePreview()};
filePreview.addEventListener("pointerdown",e=>{if(e.target===filePreview)closeFilePreview()});
document.addEventListener("keydown",e=>{if(e.key==="Escape"&&filePreview.classList.contains("open"))closeFilePreview()});
function formatBytes(n){if(!Number.isFinite(+n))return"";const u=["B","KB","MB","GB"];let i=0,v=+n;while(v>=1024&&i<u.length-1){v/=1024;i++}return(v<10&&i? v.toFixed(1):Math.round(v))+" "+u[i]}

/* House summary */
const housePanel=document.getElementById("housePanel");
function openHouseSummary(b){
  if(b&&b.tipo!=="nota")return openFilePreview(b);
  selected=b;pedirDesenho();const r=world.regions.find(x=>x.id===b.regionId),btn=document.getElementById("openNoteBtn");
  document.getElementById("houseTitle").textContent=b.name;
  document.getElementById("housePreviewImg").src=(b.anexos||[]).find(a=>a.dataUrl)?.dataUrl||urbeSpriteURL(b.sprite);
  const tipo=b.tipo==="nota"?"Nota Markdown":b.tipo==="anexo"?(rotuloClasse(b.fileClass)+" citado"):(rotuloClasse(b.fileClass)+" independente");
  document.getElementById("houseMeta").innerHTML=`Tipo: ${tipo}${(b.files||[]).length>1?" ("+b.files.length+" arquivos)":""}<br>Pasta: ${r?caminhoRegiao(r).map(x=>x.name).join("/"):"raiz"}<br>Criado: ${b.created||nowDate()}<br>Editado: ${b.modified||nowDate()}`;
  btn.style.display=b.tipo==="nota"?"":"none";housePanel.classList.add("open")
}
function closeHouseSummary(){housePanel.classList.remove("open");if(selected?.kind==="building")selected=null;pedirDesenho()}
document.getElementById("closeHouse").onclick=closeHouseSummary;
document.getElementById("openNoteBtn").onclick=()=>{if(selected?.kind==="building")openFullEditor(selected)};
document.getElementById("deleteBtn").onclick=()=>{if(selected?.kind==="building"&&selected.tipo==="nota"){const b=selected;closeHouseSummary();removerNota(b);return}if(selected?.kind==="building"){for(const a of (selected.files||selected.anexos||[]))if(a.cacheId)DBK.bDel(a.cacheId).catch(()=>{});world.buildings=world.buildings.filter(b=>b!==selected);closeHouseSummary();counts();scheduleRoadRebuild();agendarSalvar();toast("Construção excluída.")}};
document.getElementById("moveBtn").onclick=()=>{if(selected?.kind!=="building")return;escolherDestinoEMover([selected.id]);closeHouseSummary()};
document.getElementById("copyBtn").onclick=()=>{if(selected?.kind==="building"){const b=selected,r=b.regionId?world.regions.find(x=>x.id===b.regionId)||null:null,pos=r?vagaNaRegiao(r,semente("copia"+Date.now()),new Set()):vagaAleatoria(semente("copia"+Date.now()));if(!pos)return toast("Sem espaço livre para a cópia.");const nb={...b,id:id("b"),name:b.name+" cópia",x:pos.x,y:pos.y,created:nowDate(),modified:nowDate()};world.buildings.push(nb);marcarIndice();indexar();counts();scheduleRoadRebuild();agendarSalvar();toast("Construção copiada.")}};

/* Full editor */
const editorFull=document.getElementById("editorFull"),tree=document.getElementById("tree"),bodyEditor=document.getElementById("bodyEditor"),propsBox=document.getElementById("propertiesBox"),fileSidebar=document.getElementById("fileSidebar");
const editorMain=document.getElementById("editorMain"),editorScroll=document.getElementById("editorScroll"),renderedPreview=document.getElementById("renderedPreview"),viewModeBtn=document.getElementById("viewModeBtn");
let editorViewMode="preview";
let currentFile=null,saveTimer=null;
const wikiSuggest=document.getElementById("wikiSuggest");
let wikiState={open:false,start:-1,query:"",items:[],index:0};

function getWikiContext(){
  const pos=bodyEditor.selectionStart;
  const before=bodyEditor.value.slice(0,pos);
  const open=before.lastIndexOf("[[");
  if(open<0)return null;

  // If a closing token appears after the last opening token, we're no longer in a wiki-link.
  const afterOpen=before.slice(open+2);
  if(afterOpen.includes("]]"))return null;
  if(afterOpen.includes("\n"))return null;

  return {start:open,query:afterOpen};
}
function wikiCandidates(query){
  const core=window.UrbeCore,knowledge=core&&core.service('knowledge'),docs=core&&core.service('documents');
  if(knowledge&&docs){
    const currentId=currentFile&&currentFile.documentId;
    return knowledge.search(String(query||''),20).filter(d=>d.id!==currentId).map(d=>({id:d.id,name:d.title,path:d.path,documentId:d.id}));
  }
  return [];
}
function caretScreenPosition(textarea,pos){
  const mirror=document.createElement("div");
  const cs=getComputedStyle(textarea);
  const props=["boxSizing","width","height","overflowX","overflowY","borderTopWidth","borderRightWidth","borderBottomWidth","borderLeftWidth","paddingTop","paddingRight","paddingBottom","paddingLeft","fontStyle","fontVariant","fontWeight","fontStretch","fontSize","fontFamily","lineHeight","textAlign","textTransform","textIndent","textDecoration","letterSpacing","wordSpacing","tabSize","MozTabSize"];
  props.forEach(p=>mirror.style[p]=cs[p]);
  mirror.style.position="fixed";
  mirror.style.visibility="hidden";
  mirror.style.whiteSpace="pre-wrap";
  mirror.style.wordWrap="break-word";
  mirror.style.left=textAreaRectLeft(textarea)+"px";
  mirror.style.top=textAreaRectTop(textarea)+"px";
  mirror.style.pointerEvents="none";
  mirror.textContent=textarea.value.substring(0,pos);
  const marker=document.createElement("span");
  marker.textContent=textarea.value.substring(pos) || ".";
  mirror.appendChild(marker);
  document.body.appendChild(mirror);
  const r=marker.getBoundingClientRect();
  mirror.remove();
  return {x:r.left,y:r.top};
}
function textAreaRectLeft(ta){return ta.getBoundingClientRect().left}
function textAreaRectTop(ta){return ta.getBoundingClientRect().top}

function positionWikiPopup(){
  if(!wikiState.open)return;
  const r=bodyEditor.getBoundingClientRect();
  let x=r.left+12, y=r.top+42;
  try{
    const c=caretScreenPosition(bodyEditor,bodyEditor.selectionStart);
    if(Number.isFinite(c.x)&&Number.isFinite(c.y)){x=c.x;y=c.y+24}
  }catch(_){}
  const maxX=window.innerWidth-Math.min(360,window.innerWidth*.78)-8;
  const maxY=window.innerHeight-250;
  wikiSuggest.style.left=Math.max(8,Math.min(x,maxX))+"px";
  wikiSuggest.style.top=Math.max(8,Math.min(y,maxY))+"px";
}
function renderWikiSuggestions(){
  if(!wikiState.open){wikiSuggest.classList.remove("open");wikiSuggest.innerHTML="";return}
  wikiSuggest.classList.add("open");
  if(!wikiState.items.length){
    wikiSuggest.innerHTML='<div class="wikiEmpty">Nenhuma nota encontrada.</div>';
    positionWikiPopup();
    return;
  }
  wikiSuggest.innerHTML=wikiState.items.map((b,i)=>
    `<button class="wikiItem ${i===wikiState.index?"active":""}" data-wiki="${b.id}" role="option">${b.name}</button>`
  ).join("");
  wikiSuggest.querySelectorAll("[data-wiki]").forEach(el=>{
    el.addEventListener("pointerdown",e=>{
      e.preventDefault();
      const b=wikiState.items.find(x=>x.id===el.dataset.wiki);
      if(b)commitWikiSuggestion(b);
    });
  });
  positionWikiPopup();
}
function updateWikiAutocomplete(){
  const ctx=getWikiContext();
  if(!ctx){
    wikiState={open:false,start:-1,query:"",items:[],index:0};
    renderWikiSuggestions();
    return;
  }
  const items=wikiCandidates(ctx.query);
  wikiState.open=true;
  wikiState.start=ctx.start;
  wikiState.query=ctx.query;
  wikiState.items=items;
  wikiState.index=Math.min(wikiState.index,Math.max(0,items.length-1));
  renderWikiSuggestions();
}
function commitWikiSuggestion(b){
  if(!wikiState.open)return;
  const start=wikiState.start;
  const end=bodyEditor.selectionStart;
  const replacement="[[${NAME}]]".replace("${NAME}",b.name);
  bodyEditor.setRangeText(replacement,start,end,"end");
  wikiState={open:false,start:-1,query:"",items:[],index:0};
  renderWikiSuggestions();
  bodyEditor.focus();
  markChanged();
}


function escapeHTML(s){
  return String(s).replace(/[&<>"']/g,ch=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[ch]));
}
function inlineMarkdown(s){
  /* trechos de código saem antes: dentro deles, [[...]], ** e _ são texto */
  const codigos=[];
  let x=escapeHTML(s).replace(/`([^`\n]+)`/g,(m,c)=>{codigos.push(c);return `\u0002${codigos.length-1}\u0003`});

  // Wiki links are rendered as internal-note pills.
  x=x.replace(/\[\[([^\]|#]+)(?:\|([^\]]+))?\]\]/g,(m,target,label)=>{
    const name=(label||target).trim();
    return `<span class="wikilink" data-note-name="${escapeHTML(target.trim())}">${escapeHTML(name)}</span>`;
  });

  // Images before ordinary links.
  x=x.replace(/!\[([^\]]*)\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g,'<img src="$2" alt="$1">');
  x=x.replace(/\[([^\]]+)\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g,'<a href="$2" target="_blank" rel="noopener">$1</a>');

  x=x.replace(/\*\*([^*\n]+)\*\*/g,'<strong>$1</strong>');
  x=x.replace(/__([^_\n]+)__/g,'<strong>$1</strong>');
  x=x.replace(/(?<!\*)\*([^*\n]+)\*(?!\*)/g,'<em>$1</em>');
  x=x.replace(/(?<!_)_([^_\n]+)_(?!_)/g,'<em>$1</em>');
  x=x.replace(/~~([^~\n]+)~~/g,'<del>$1</del>');
  return x.replace(/\u0002(\d+)\u0003/g,(m,i)=>`<code>${codigos[+i]}</code>`);
}
function splitFrontmatter(md){
  const src=String(md||"").replace(/\r\n?/g,"\n");
  if(!src.startsWith("---\n"))return {raw:"",body:src,fields:[]};
  const end=src.indexOf("\n---",4);
  if(end<0)return {raw:"",body:src,fields:[]};
  const raw=src.slice(0,end+4);
  const body=src.slice(end+4).replace(/^\n+/,"");
  const lines=raw.split("\n").slice(1,-1);
  const fields=[];let current=null;
  for(const line of lines){
    const m=line.match(/^([A-Za-z0-9_-]+):\s*(.*)$/);
    if(m){current={key:m[1],value:m[2]||""};fields.push(current);continue}
    const li=line.match(/^\s*-\s*(.+)$/);
    if(li&&current)current.value+=(current.value?", ":"")+li[1];
  }
  return {raw,body,fields};
}
function renderFrontmatter(fields){
  if(!fields.length)return "";
  const chipKeys=new Set(["tags","tag","aliases","alias"]);
  const valueHTML=f=>{
    const raw=String(f.value||"").trim();
    if(!chipKeys.has(String(f.key).toLowerCase()))return inlineMarkdown(raw||"—");
    const clean=raw.replace(/^\[|\]$/g,"");
    const values=clean.split(",").map(x=>x.trim().replace(/^['"]|['"]$/g,"")).filter(Boolean);
    if(!values.length)return "—";
    return values.map(v=>`<span class="frontmatterChip">${escapeHTML(v)}</span>`).join("");
  };
  return `<section class="frontmatterCard" contenteditable="false" data-frontmatter-card="1">${fields.map(f=>`<div class="frontmatterRow"><span class="frontmatterKey">${escapeHTML(f.key)}</span><span class="frontmatterValue">${valueHTML(f)}</span></div>`).join("")}</section>`;
}
/* Markdown → HTML do editor Visual. Tudo o que é desenhado aqui volta igual em
   markdownFromVisual: tabelas (com alinhamento), citações de várias linhas, callouts
   (> [!tipo] Título), listas aninhadas, tarefas, código com linguagem e links com rótulo. */
const MD_CALLOUTS={note:"Nota",info:"Informação",tip:"Dica",success:"Pronto",question:"Pergunta",warning:"Atenção",danger:"Perigo",bug:"Erro",example:"Exemplo",quote:"Citação",abstract:"Resumo",todo:"A fazer"};
function mdTableCells(line){
  let t=line.trim();if(t.startsWith("|"))t=t.slice(1);if(t.endsWith("|")&&!t.endsWith("\\|"))t=t.slice(0,-1);
  const cells=[];let cur="";for(let i=0;i<t.length;i++){if(t[i]==="\\"&&t[i+1]==="|"){cur+="|";i++;continue}if(t[i]==="|"){cells.push(cur.trim());cur="";continue}cur+=t[i]}cells.push(cur.trim());return cells;
}
function mdIsTableSep(line){return /^\s*\|?\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)*\|?\s*$/.test(line)&&line.includes("-")}
function renderMarkdown(md){
  const fm=splitFrontmatter(md);
  const lines=fm.body.replace(/\r\n?/g,"\n").split("\n");
  let out=renderFrontmatter(fm.fields), inCode=false, code=[], lang="", stack=[];

  function closeLevel(){const t=stack.pop();out+=`</li></${t.type}>`}
  function closeList(){while(stack.length)closeLevel()}
  function item(type,indent,html){
    while(stack.length&&indent<stack[stack.length-1].indent)closeLevel();
    if(stack.length&&indent===stack[stack.length-1].indent&&stack[stack.length-1].type!==type)closeLevel();
    if(!stack.length||indent>stack[stack.length-1].indent){out+=`<${type}>`;stack.push({type,indent})}
    else out+="</li>";
    out+=html;
  }
  function flushCode(){
    if(inCode){
      out+=`<pre${lang?` data-lang="${escapeHTML(lang)}"`:""}><code>${escapeHTML(code.join("\n"))}</code></pre>`;
      code=[];inCode=false;lang="";
    }
  }
  function indentOf(l){return l.match(/^[ \t]*/)[0].replace(/\t/g,"    ").length}

  for(let i=0;i<lines.length;i++){
    const line=lines[i];
    let m;

    if((m=line.match(/^```\s*([\w+#.-]*)\s*$/))||/^```/.test(line)){
      if(inCode) flushCode();
      else {closeList();inCode=true;code=[];lang=m?m[1]:""}
      continue;
    }
    if(inCode){code.push(line);continue}

    if(!line.trim()){closeList();continue}

    if((m=line.match(/^(#{1,6})\s+(.+)$/))){
      closeList();
      const n=m[1].length;
      out+=`<h${n}>${inlineMarkdown(m[2])}</h${n}>`;
      continue;
    }
    if(/^ {0,3}([-*_])(?:\s*\1){2,}\s*$/.test(line)){
      closeList();out+="<hr>";continue;
    }
    /* tabela: linha com | seguida da linha separadora */
    if(line.includes("|")&&i+1<lines.length&&mdIsTableSep(lines[i+1])){
      closeList();
      const head=mdTableCells(line),aligns=mdTableCells(lines[i+1]).map(c=>/^:-+:$/.test(c)?"center":/-:$/.test(c)?"right":/^:-/.test(c)?"left":"");
      const td=(tag,c,k)=>`<${tag}${aligns[k]?` style="text-align:${aligns[k]}"`:""}>${inlineMarkdown(c)||"<br>"}</${tag}>`;
      let html=`<table data-md-table="1" data-align="${aligns.join(",")}"><thead><tr>${head.map((c,k)=>td("th",c,k)).join("")}</tr></thead><tbody>`;
      i+=2;
      while(i<lines.length&&lines[i].trim()&&lines[i].includes("|")){const cells=mdTableCells(lines[i]);html+=`<tr>${head.map((_,k)=>td("td",cells[k]||"",k)).join("")}</tr>`;i++}
      i--;out+=html+"</tbody></table>";
      continue;
    }
    /* citação: linhas seguidas com > formam um bloco só; > [!tipo] Título vira um callout */
    if(/^>\s?/.test(line)){
      closeList();
      const grupo=[];while(i<lines.length&&/^>\s?/.test(lines[i])){grupo.push(lines[i].replace(/^>\s?/,""));i++}i--;
      const c=grupo[0].match(/^\[!(\w+)\][+-]?\s*(.*)$/);
      if(c){
        const tipo=c[1].toLowerCase(),titulo=c[2]||"";
        out+=`<div class="callout callout-${escapeHTML(tipo)}" data-callout="${escapeHTML(tipo)}"><div class="callout-title" data-default="${escapeHTML(MD_CALLOUTS[tipo]||tipo)}">${inlineMarkdown(titulo)||"<br>"}</div><div class="callout-body">${grupo.slice(1).map(inlineMarkdown).join("<br>")||"<br>"}</div></div>`;
      }else out+=`<blockquote>${grupo.map(inlineMarkdown).join("<br>")}</blockquote>`;
      continue;
    }
    if((m=line.match(/^([ \t]*)[-*+]\s+\[([ xX])\]\s+(.*)$/))){
      const checked=m[2].toLowerCase()==="x"?" checked":"";
      item("ul",indentOf(line),`<li class="task"><input type="checkbox"${checked}>${inlineMarkdown(m[3])}`);
      continue;
    }
    if((m=line.match(/^([ \t]*)[-*+]\s+(.+)$/))){
      item("ul",indentOf(line),`<li>${inlineMarkdown(m[2])}`);
      continue;
    }
    if((m=line.match(/^([ \t]*)(\d+)[.)]\s+(.+)$/))){
      item("ol",indentOf(line),`<li>${inlineMarkdown(m[3])}`);
      continue;
    }

    closeList();
    out+=`<p>${inlineMarkdown(line)}</p>`;
  }
  closeList();
  flushCode();
  return out||'<p><br></p>';
}

let visualSyncLock=false;

function markdownFromVisual(root){
  const out=[];

  function textInline(node){
    if(node.nodeType===Node.TEXT_NODE)return node.nodeValue;
    if(node.nodeType!==Node.ELEMENT_NODE)return "";
    const el=node, tag=el.tagName.toLowerCase();
    const inner=[...el.childNodes].map(textInline).join("");

    if(tag==="strong"||tag==="b")return `**${inner}**`;
    if(tag==="em"||tag==="i")return `_${inner}_`;
    if(tag==="del"||tag==="s")return `~~${inner}~~`;
    if(tag==="code" && el.parentElement?.tagName.toLowerCase()!=="pre")return `\`${inner}\``;
    if(tag==="a")return `[${inner}](${el.getAttribute("href")||""})`;
    if(tag==="img")return `![${el.getAttribute("alt")||""}](${el.getAttribute("src")||""})`;
    if(el.classList.contains("wikilink")){
      const alvo=el.dataset.noteName||inner,rotulo=inner.trim();
      return rotulo&&rotulo!==alvo?`[[${alvo}|${rotulo}]]`:`[[${alvo}]]`;
    }
    if(tag==="ul"||tag==="ol"||tag==="input")return "";
    if(tag==="br")return "\n";
    return inner;
  }

  function block(el){
    if(el.nodeType===Node.TEXT_NODE){
      const t=el.nodeValue.trim();
      if(t)out.push(t);
      return;
    }
    if(el.nodeType!==Node.ELEMENT_NODE)return;
    const tag=el.tagName.toLowerCase();
    if(el.hasAttribute("data-frontmatter-card"))return;
    if(el.dataset&&el.dataset.callout){
      const titulo=el.querySelector(".callout-title"),corpo=el.querySelector(".callout-body");
      const t=titulo?textInline(titulo).replace(/\n/g," ").trim():"",b=corpo?textInline(corpo).replace(/\n+$/,""):"";
      out.push(["> [!"+el.dataset.callout+"]"+(t?" "+t:"")].concat(b.trim()?b.split("\n").map(x=>x?"> "+x:">"):[]).join("\n"));
      return;
    }

    if(/^h[1-6]$/.test(tag)){
      out.push("#".repeat(Number(tag[1]))+" "+textInline(el).trim());
      return;
    }
    // O navegador cria <div> ao sair de uma lista; se ele contém blocos, serializa cada um.
    if(tag==="div"&&[...el.children].some(c=>/^(P|DIV|H[1-6]|UL|OL|BLOCKQUOTE|PRE|HR|TABLE)$/.test(c.tagName))){
      [...el.childNodes].forEach(block);
      return;
    }
    if(tag==="p"||tag==="div"){
      const t=textInline(el).trim();
      if(t)out.push(t);
      return;
    }
    if(tag==="blockquote"){
      const linhas=[...el.childNodes].map(n=>/^(P|DIV)$/.test(n.nodeName)?textInline(n)+"\n":textInline(n)).join("").replace(/\n+$/,"").split("\n");
      out.push(linhas.map(x=>x?"> "+x:">").join("\n"));
      return;
    }
    if(tag==="table"){
      const rows=[...el.querySelectorAll("tr")];if(!rows.length)return;
      const cel=c=>textInline(c).replace(/\n/g," ").replace(/\|/g,"\\|").trim();
      const head=[...rows[0].children].map(cel),al=(el.dataset.align||"").split(",");
      const sep=head.map((_,k)=>{const a=al[k]||(rows[0].children[k]&&rows[0].children[k].style.textAlign)||"";return a==="center"?":---:":a==="right"?"---:":a==="left"?":---":"---"});
      const linhas=["| "+head.join(" | ")+" |","| "+sep.join(" | ")+" |"];
      rows.slice(1).forEach(r=>{const cs=[...r.children].map(cel);while(cs.length<head.length)cs.push("");linhas.push("| "+cs.join(" | ")+" |")});
      out.push(linhas.join("\n"));
      return;
    }
    if(tag==="pre"){
      out.push("```"+(el.dataset.lang||"")+"\n"+el.innerText.replace(/\n+$/,"")+"\n```");
      return;
    }
    if(tag==="hr"){
      out.push("---");
      return;
    }
    if(tag==="ul"||tag==="ol"){
      /* itens de uma lista ficam em linhas seguidas, sem linha em branco entre eles;
         sublistas descem até o início do texto do item de cima */
      const itens=[];
      (function lista(ul,recuo){
        const ord=ul.tagName.toLowerCase()==="ol";let n=0;
        [...ul.children].forEach(li=>{
          if(li.tagName.toLowerCase()!=="li")return;n++;
          const checkbox=[...li.children].find(c=>c.tagName==="INPUT"&&c.type==="checkbox");
          const content=textInline(li).replace(/\n+/g," ").trim();
          const marca=checkbox?`- [${checkbox.checked?"x":" "}] `:ord?n+". ":"- ";
          itens.push(recuo+marca+content);
          [...li.children].filter(c=>/^(UL|OL)$/.test(c.tagName)).forEach(sub=>lista(sub,recuo+" ".repeat(ord?String(n).length+2:2)));
        });
      })(el,"");
      if(itens.length)out.push(itens.join("\n"));
      return;
    }

    [...el.childNodes].forEach(block);
  }

  [...root.childNodes].forEach(block);
  const body=out.join("\n\n").replace(/\n{3,}/g,"\n\n").trim();
  const fm=splitFrontmatter(bodyEditor.value).raw;
  return (fm?fm+(body?"\n":""):"")+(body?body+"\n":"");
}

function bindPreviewWikiLinks(){
  renderedPreview.querySelectorAll("[data-note-name]").forEach(el=>{
    el.onclick=e=>{
      e.preventDefault();
      e.stopPropagation();
      const name=el.dataset.noteName,core=window.UrbeCore,docs=core&&core.service('documents');
      const exact=docs&&(docs.get(name)||docs.get(name+'.md'));
      const matches=docs?docs.list().filter(d=>d.title.toLowerCase()===name.toLowerCase()):[];
      const target=exact||(matches.length===1?matches[0]:null);
      if(target)core.commands.execute('document.open',{id:target.id,source:'wikilink'});
      else toast(matches.length>1?'Link ambíguo: use o caminho da nota.':'A nota vinculada não existe.');
    };
  });
}

function renderCurrentPreview(){
  visualSyncLock=true;
  renderedPreview.innerHTML=renderMarkdown(bodyEditor.value);
  bindPreviewWikiLinks();
  updateVisualEmptyState();
  visualSyncLock=false;
}

/* O aviso de nota vazia é só visual (CSS); nunca entra no conteúdo editável. */
function updateVisualEmptyState(){
  renderedPreview.classList.toggle("isEmpty",!renderedPreview.textContent.replace(/\u200b/g,"").trim()&&!renderedPreview.querySelector("img,hr,table,input,iframe,li"));
}

function syncVisualToMarkdown(){
  if(visualSyncLock||editorViewMode!=="preview"||!currentFile)return;
  const antes=currentFile.content||"";
  const md=markdownFromVisual(renderedPreview);
  bodyEditor.value=md;
  currentFile.content=md;
  currentFile.modified=nowDate();
  if(assinaturaLinks(antes)!==assinaturaLinks(md))scheduleRoadRebuild();
  agendarSalvar();
  document.getElementById("saveState").textContent="Salvando...";
  clearTimeout(saveTimer);
  updateStats();
}


/* v0.13.3 LIVE PREVIEW MOBILE
   Esta camada só observa renderedPreview/wikiSuggest. Não altera canvas, câmera,
   HUD, pointer handlers do jogo ou o loop de renderização da cidade. */
let visualWikiState={open:false,node:null,start:-1,query:"",items:[],index:0};

function visualSelection(){
  const sel=window.getSelection();
  if(!sel||!sel.rangeCount||!renderedPreview.contains(sel.anchorNode))return null;
  return sel;
}
function visualCaretRect(){
  const sel=visualSelection();
  if(!sel)return null;
  // Com texto selecionado, nunca mexe no DOM nem colapsa a seleção do usuário.
  if(!sel.isCollapsed)return sel.getRangeAt(0).getBoundingClientRect();
  const r=sel.getRangeAt(0).cloneRange();
  r.collapse(true);
  const rect=r.getBoundingClientRect();
  if(rect && (rect.width||rect.height))return rect;
  const probe=document.createElement("span");
  probe.textContent="\u200b";
  r.insertNode(probe);
  const pr=probe.getBoundingClientRect();
  probe.remove();
  sel.removeAllRanges();sel.addRange(r);
  return pr;
}
function closestEditableBlock(node){
  let el=node?.nodeType===Node.ELEMENT_NODE?node:node?.parentElement;
  while(el&&el!==renderedPreview){
    if(/^(DIV|P|H1|H2|H3|H4|H5|H6|BLOCKQUOTE|LI)$/.test(el.tagName))return el;
    el=el.parentElement;
  }
  return el===renderedPreview?el:null;
}
function setCaretEnd(el){
  const r=document.createRange(),s=window.getSelection();
  r.selectNodeContents(el);r.collapse(false);s.removeAllRanges();s.addRange(r);
}
function replaceBlockTag(el,tag){
  if(!el||el===renderedPreview)return el;
  const n=document.createElement(tag);
  while(el.firstChild)n.appendChild(el.firstChild);
  for(const a of [...el.attributes])if(a.name!=="class")n.setAttribute(a.name,a.value);
  el.replaceWith(n);setCaretEnd(n);return n;
}
function transformLiveMarkdownBlock(){
  const sel=visualSelection(); if(!sel||!sel.isCollapsed)return false;
  let block=closestEditableBlock(sel.anchorNode); if(!block||block===renderedPreview)return false;
  const tag=block.tagName;
  const raw=(block.textContent||"").replace(/\u200b/g,"");

  // Mobile-first: detecta o prefixo pelo conteúdo atual da linha. Não depende
  // de KeyboardEvent/InputEvent.data, que varia entre Gboard/SwiftKey/IME.
  let m=raw.match(/^(#{1,6})\s+(.*)$/s);
  if(m && !/^H[1-6]$/.test(tag)){
    const level=m[1].length, rest=m[2];
    block.textContent=rest || "\u200b";
    block=replaceBlockTag(block,"h"+level);
    if(!rest){ block.textContent=""; block.appendChild(document.createElement("br")); setCaretEnd(block); }
    syncVisualToMarkdown(); return true;
  }
  m=raw.match(/^>\s+(.*)$/s);
  if(m && tag!=="BLOCKQUOTE"){
    const rest=m[1]; block.textContent=rest||"\u200b";
    block=replaceBlockTag(block,"blockquote");
    if(!rest){ block.textContent=""; block.appendChild(document.createElement("br")); setCaretEnd(block); }
    syncVisualToMarkdown(); return true;
  }
  if(/^([-*_])(?:\s*\1){2,}\s*$/.test(raw) && tag!=="HR"){
    const hr=document.createElement("hr"), p=document.createElement("p");
    p.innerHTML="<br>"; block.replaceWith(hr,p); setCaretEnd(p);
    syncVisualToMarkdown(); return true;
  }
  m=raw.match(/^[-*+]\s+(.*)$/s);
  if(m && tag!=="LI"){
    const rest=m[1],ul=document.createElement("ul"),li=document.createElement("li");
    li.textContent=rest||""; if(!rest)li.appendChild(document.createElement("br"));
    ul.appendChild(li); block.replaceWith(ul); setCaretEnd(li);
    syncVisualToMarkdown(); return true;
  }
  m=raw.match(/^\d+[.)]\s+(.*)$/s);
  if(m && tag!=="LI"){
    const rest=m[1],ol=document.createElement("ol"),li=document.createElement("li");
    li.textContent=rest||""; if(!rest)li.appendChild(document.createElement("br"));
    ol.appendChild(li); block.replaceWith(ol); setCaretEnd(li);
    syncVisualToMarkdown(); return true;
  }
  m=raw.match(/^[-*+]\s+\[([ xX])\]\s+(.*)$/s);
  if(m && tag!=="LI"){
    const checked=m[1].toLowerCase()==="x", rest=m[2];
    const ul=document.createElement("ul"),li=document.createElement("li"),cb=document.createElement("input");
    li.className="task"; cb.type="checkbox"; cb.checked=checked;
    li.append(cb,document.createTextNode(" "));
    if(rest)li.appendChild(document.createTextNode(rest)); else li.appendChild(document.createElement("br"));
    ul.appendChild(li); block.replaceWith(ul); setCaretEnd(li);
    syncVisualToMarkdown(); return true;
  }
  return false;
}

function normalizeLivePreviewAfterInput(){
  if(editorViewMode!=="preview")return;
  // Executa depois que o IME/Chrome terminou de atualizar o DOM e a seleção.
  requestAnimationFrame(()=>{
    if(editorViewMode!=="preview")return;
    transformLiveMarkdownBlock();
    convertClosedVisualWiki();
    updateVisualWiki();
  });
}
function getVisualWikiContext(){
  const sel=visualSelection(); if(!sel||!sel.isCollapsed)return null;
  const node=sel.anchorNode;
  if(!node||node.nodeType!==Node.TEXT_NODE)return null;
  const pos=sel.anchorOffset, before=node.nodeValue.slice(0,pos);
  const open=before.lastIndexOf("[[");
  if(open<0)return null;
  const q=before.slice(open+2);
  if(q.includes("]]")||q.includes("\n"))return null;
  return {node,start:open,end:pos,query:q};
}
function closeVisualWiki(){
  visualWikiState={open:false,node:null,start:-1,query:"",items:[],index:0};
  wikiSuggest.classList.remove("open","visualWiki");wikiSuggest.innerHTML="";
}
function renderVisualWiki(){
  if(!visualWikiState.open)return closeVisualWiki();
  wikiSuggest.classList.add("open","visualWiki");
  if(!visualWikiState.items.length){
    wikiSuggest.innerHTML='<div class="wikiEmpty">Nenhuma nota encontrada.</div>';
  }else{
    wikiSuggest.innerHTML=visualWikiState.items.map((b,i)=>
      `<button class="wikiItem ${i===visualWikiState.index?"active":""}" data-vwiki="${b.id}" role="option">${escapeHTML(b.name)}</button>`
    ).join("");
    wikiSuggest.querySelectorAll("[data-vwiki]").forEach(el=>el.addEventListener("pointerdown",e=>{
      e.preventDefault();
      const b=world.buildings.find(x=>x.id===el.dataset.vwiki); if(b)commitVisualWiki(b);
    }));
  }
  const r=visualCaretRect()||renderedPreview.getBoundingClientRect();
  const w=Math.min(360,window.innerWidth*.82);
  wikiSuggest.style.left=Math.max(8,Math.min(r.left,window.innerWidth-w-8))+"px";
  wikiSuggest.style.top=Math.max(8,Math.min(r.bottom+8,window.innerHeight-250))+"px";
}
function updateVisualWiki(){
  if(editorViewMode!=="preview")return closeVisualWiki();
  const c=getVisualWikiContext(); if(!c)return closeVisualWiki();
  visualWikiState={open:true,node:c.node,start:c.start,query:c.query,
    items:wikiCandidates(c.query),index:Math.min(visualWikiState.index,Math.max(0,wikiCandidates(c.query).length-1))};
  renderVisualWiki();
}
function commitVisualWiki(b){
  const st=visualWikiState,node=st.node;
  if(!st.open||!node||!node.isConnected)return;
  const sel=window.getSelection(),pos=sel?.anchorNode===node?sel.anchorOffset:node.nodeValue.length;
  const before=node.nodeValue.slice(0,st.start),after=node.nodeValue.slice(pos);
  const parent=node.parentNode;
  const a=document.createTextNode(before);
  const link=document.createElement("span");
  link.className="wikilink";link.dataset.noteName=b.name;link.textContent=b.name;
  const tail=document.createTextNode(after||"\u200b");
  parent.insertBefore(a,node);parent.insertBefore(link,node);parent.insertBefore(tail,node);node.remove();
  const r=document.createRange();r.setStart(tail,after?0:1);r.collapse(true);
  sel.removeAllRanges();sel.addRange(r);
  closeVisualWiki();bindPreviewWikiLinks();syncVisualToMarkdown();
}
function convertClosedVisualWiki(){
  const sel=visualSelection();if(!sel||!sel.isCollapsed||sel.anchorNode?.nodeType!==Node.TEXT_NODE)return false;
  const node=sel.anchorNode,pos=sel.anchorOffset,before=node.nodeValue.slice(0,pos);
  const m=before.match(/\[\[([^\]\n]+)\]\]$/);if(!m)return false;
  const name=m[1].split("|")[0].trim(),label=(m[1].split("|")[1]||name).trim();
  const target=buildingByLinkName(normName(name)); if(!target)return false;
  const start=pos-m[0].length,parent=node.parentNode;
  const a=document.createTextNode(node.nodeValue.slice(0,start));
  const link=document.createElement("span");link.className="wikilink";link.dataset.noteName=target.name;link.textContent=label;
  const tail=document.createTextNode(node.nodeValue.slice(pos)||"\u200b");
  parent.insertBefore(a,node);parent.insertBefore(link,node);parent.insertBefore(tail,node);node.remove();
  const r=document.createRange();r.setStart(tail,tail.nodeValue==="\u200b"?1:0);r.collapse(true);
  sel.removeAllRanges();sel.addRange(r);bindPreviewWikiLinks();syncVisualToMarkdown();return true;
}

renderedPreview.addEventListener("input",normalizeLivePreviewAfterInput);
renderedPreview.addEventListener("keyup",e=>{
  if(["ArrowUp","ArrowDown","Enter","Tab","Escape"].includes(e.key))return;
  updateVisualWiki();
});
renderedPreview.addEventListener("click",updateVisualWiki);
renderedPreview.addEventListener("keydown",e=>{
  if(!visualWikiState.open)return;
  if(e.key==="ArrowDown"){e.preventDefault();visualWikiState.index=(visualWikiState.index+1)%Math.max(1,visualWikiState.items.length);renderVisualWiki()}
  else if(e.key==="ArrowUp"){e.preventDefault();visualWikiState.index=(visualWikiState.index-1+Math.max(1,visualWikiState.items.length))%Math.max(1,visualWikiState.items.length);renderVisualWiki()}
  else if((e.key==="Enter"||e.key==="Tab")&&visualWikiState.items.length){e.preventDefault();commitVisualWiki(visualWikiState.items[visualWikiState.index])}
  else if(e.key==="Escape"){e.preventDefault();closeVisualWiki()}
});

// Wrappers: syncVisualToMarkdown é redefinida por camadas posteriores.
renderedPreview.addEventListener("input",()=>{updateVisualEmptyState();syncVisualToMarkdown()});
renderedPreview.addEventListener("blur",()=>syncVisualToMarkdown());

renderedPreview.addEventListener("change",e=>{
  if(editorViewMode==="preview" && e.target.matches('input[type="checkbox"]'))syncVisualToMarkdown();
});


function setEditorViewMode(mode){
  // Before leaving visual mode, serialize what the user edited.
  if(editorViewMode==="preview" && mode!=="preview")syncVisualToMarkdown();

  editorViewMode=mode==="preview"?"preview":"source";
  const isPreview=editorViewMode==="preview";
  editorMain.classList.toggle("previewMode",isPreview);
  editorScroll.classList.toggle("previewMode",isPreview);
  viewModeBtn.dataset.mode=editorViewMode;
  viewModeBtn.title=isPreview?"Editar fonte Markdown":"Editar visualmente";
  viewModeBtn.setAttribute("aria-label",viewModeBtn.title);

  if(isPreview){
    renderCurrentPreview();
    requestAnimationFrame(()=>renderedPreview.focus());
  }else{
    requestAnimationFrame(()=>bodyEditor.focus());
  }
}
viewModeBtn.onclick=()=>setEditorViewMode(editorViewMode==="source"?"preview":"source");

function renderProps(b){const p=propertiesFor(b);propsBox.textContent=`---\ntítulo: ${p.titulo}\ncategoria: ${p.categoria}\ntags: [${p.tags.join(", ")}]\ncriado: ${p.criado}\nmodificado: ${p.modificado}\n---`}
const explorerSelection=new Set();
let explorerHoldTimer=null,explorerHoldFired=false,explorerHoldPointer=null;
const EXPLORER_HOLD_MS=480,EXPLORER_HOLD_MOVE=9;
function explorerSelected(){return world.buildings.filter(b=>explorerSelection.has(b.id))}
function updateExplorerActions(){
  const bar=document.getElementById("explorerActions"),count=document.getElementById("selectionCount"),n=explorerSelection.size;
  if(!bar)return;
  bar.classList.toggle("selectionMode",n>0);
  if(count)count.textContent=n+" selecionado"+(n===1?"":"s");
}
function toggleExplorerSelection(id,force){
  if(force===true)explorerSelection.add(id);
  else if(force===false)explorerSelection.delete(id);
  else explorerSelection.has(id)?explorerSelection.delete(id):explorerSelection.add(id);
  buildTree();
}
/* Quando a pasta lota, ela cresce por flood-fill sobre terra livre — mesma
   reserva de 81 tiles secos por arquivo que o import usa. */
/* (expandirRegiao: definição antiga removida na 1.0 — a versão em uso está mais abaixo) */
function vagaNaRegiao(r,sem,ignorar){
  let pos=posicaoAleatoriaNaRegiao(r,sem,ignorar);
  for(let t=0;t<4&&!pos;t++){
    if(!expandirRegiao(r,81))break;
    pos=posicaoAleatoriaNaRegiao(r,sem+t+1,ignorar);
  }
  return pos;
}

let explorerRegionTarget=undefined; // undefined = nenhuma seleção explícita; null = raiz

function selecionarRegiaoExplorer(idReg){
  explorerRegionTarget=idReg||null;buildTree();
}
function destinoCriacaoExplorer(){
  if(explorerRegionTarget!==undefined)return explorerRegionTarget;
  return currentFile?.regionId||null;
}

/* ---------- arrastar e soltar no explorador ---------- */
let dragState=null;
function nomesArrastados(ids){
  if(ids.length===1)return (world.buildings.find(b=>b.id===ids[0])||{}).name||"arquivo";
  return ids.length+" arquivos";
}
function iniciarArrasto(ids,x,y){
  if(!ids.length||dragState)return;
  const g=document.createElement("div");
  g.id="dragGhost";g.textContent="↪ "+nomesArrastados(ids);
  document.body.appendChild(g);
  dragState={ids,ghost:g,alvo:undefined,alvoEl:null};
  for(const idd of ids){
    const el=tree.querySelector('[data-file="'+idd+'"]');
    if(el)el.classList.add("dragging");
  }
  atualizarArrasto(x,y);
}
function atualizarArrasto(x,y){
  if(!dragState)return;
  dragState.ghost.style.left=x+"px";dragState.ghost.style.top=y+"px";
  const sob=document.elementFromPoint(x,y);
  const t=sob&&sob.closest?sob.closest(".treeRegionTitle"):null;
  if(dragState.alvoEl&&dragState.alvoEl!==t)dragState.alvoEl.classList.remove("dropTarget");
  dragState.alvoEl=t;
  dragState.alvo=t?t.dataset.region:undefined;
  if(t)t.classList.add("dropTarget");
  /* rola a lista quando o dedo chega perto das bordas */
  const rc=tree.getBoundingClientRect();
  if(y<rc.top+40)tree.scrollTop-=12;
  else if(y>rc.bottom-40)tree.scrollTop+=12;
}
function soltarArrasto(){
  if(!dragState)return false;
  const {ids,alvo,alvoEl,ghost}=dragState;
  if(alvoEl)alvoEl.classList.remove("dropTarget");
  ghost.remove();dragState=null;
  if(alvo===undefined){buildTree();return false}
  moverArquivosParaRegiao(ids,alvo||null);
  const destino=alvo?(world.regions.find(r=>r.id===alvo)||{}).name:"raiz";
  explorerSelection.clear();buildTree();
  toast(ids.length+" arquivo(s) movido(s) para "+destino+".");
  return true;
}
function cancelarArrasto(){
  if(!dragState)return;
  if(dragState.alvoEl)dragState.alvoEl.classList.remove("dropTarget");
  dragState.ghost.remove();dragState=null;buildTree();
}
function moverArquivosParaRegiao(ids,regionId){
  const alvo=regionId?world.regions.find(r=>r.id===regionId):null,ign=new Set(ids);
  for(const idd of ids){
    const b=world.buildings.find(x=>x.id===idd);if(!b)continue;
    let pos=alvo?vagaNaRegiao(alvo,semente(b.id+Date.now()),ign):vagaAleatoria(semente(b.id+Date.now()),3,3);
    if(!pos){toast("Sem espaço construível no destino.");continue}
    b.regionId=alvo?.id||null;b.x=pos.x;b.y=pos.y;b.modified=nowDate();try{var _wp=window.UrbeCore&&window.UrbeCore.service('world.projection');var _docs=window.UrbeCore&&window.UrbeCore.service('documents');var _doc=_docs&&_docs.list().find(function(d){return d.title.toLowerCase()===b.name.toLowerCase()});if(_wp&&_doc)_wp.setSpatial(_doc.id,{x:b.x,y:b.y})}catch(_){}
  }
  marcarIndice();indexar();scheduleRoadRebuild();buildTree();counts();agendarSalvar();
}
async function escolherDestinoEMover(ids){
  if(!ids.length)return toast("Selecione arquivos primeiro.");
  const dest=await uiDestinoRegiao("Mover para");if(dest===undefined)return;
  moverArquivosParaRegiao(ids,dest);
}
async function agruparSelecionados(){
  const bs=explorerSelected();if(bs.length<2)return toast("Selecione pelo menos dois arquivos.");
  const nome=((await UD.prompt({title:"Agrupar em nova pasta",label:"Nome da pasta",value:"Nova pasta",confirm:"Criar pasta"}))||"").trim();if(!nome)return;
  const cx=Math.round(bs.reduce((a,b)=>a+b.x,0)/bs.length),cy=Math.round(bs.reduce((a,b)=>a+b.y,0)/bs.length);
  const r=criarRegiaoOrganica(nome,bs.length,semente(nome+Date.now()),null,{x:cx,y:cy},"Pasta criada pelo Explorador.");
  if(!r)return toast("Não encontrei terra seca conectada para a nova pasta.");
  moverArquivosParaRegiao(bs.map(b=>b.id),r.id);explorerSelection.clear();buildTree();
}
async function excluirSelecionados(){
  const bs=explorerSelected();if(!bs.length)return toast("Selecione arquivos primeiro.");
  const lista=bs.map(b=>"• "+nomeExibidoArquivo(b)).join("\n");
  if(!(await UD.confirm({title:"Excluir "+bs.length+" arquivo(s)?",message:lista+"\n\nEsta ação não pode ser desfeita.",confirm:"Excluir",danger:true})))return;
  const ids=new Set(bs.map(b=>b.id));for(const b of bs)for(const a of (b.files||b.anexos||[]))if(a.cacheId)DBK.bDel(a.cacheId).catch(()=>{});world.buildings=world.buildings.filter(b=>!ids.has(b.id));
  for(const idd of ids)explorerSelection.delete(idd);
  if(currentFile&&ids.has(currentFile.id))currentFile=null;
  marcarIndice();indexar();scheduleRoadRebuild();counts();buildTree();agendarSalvar();toast(bs.length+" arquivo(s) excluído(s).");
}
function iconeArquivo(b){
  if(b.tipo==="nota")return "📄";
  const c=b.fileClass||"other";return {image:"🖼",audio:"♫",video:"▶",pdf:"PDF",html:"HTML",zip:"ZIP",other:"FILE"}[c]||"FILE";
}
function nomeExibidoArquivo(b){
  if(b.tipo==="nota")return b.name+".md";
  if(b.tipo==="anexo")return b.name;
  return b.fileName||b.name;
}
function buildTree(){
  const alvoAtual=explorerRegionTarget?world.regions.find(r=>r.id===explorerRegionTarget):null;
  const pathEl=document.getElementById("explorerPath");if(pathEl)pathEl.textContent=alvoAtual?caminhoRegiao(alvoAtual).map(x=>x.name).join(" / "):"raiz";
  const filhos=id=>world.regions.filter(r=>(r.parentId||null)===id);
  const arqs=id=>world.buildings.filter(b=>(b.regionId||null)===id);
  const linha=b=>`<button class="treeFile ${currentFile===b?"active":""} ${explorerSelection.has(b.id)?"selected":""}" data-file="${b.id}"><span class="fileKind">${iconeArquivo(b)}</span><span class="fileLabel">${escapeHTML(nomeExibidoArquivo(b))}</span></button>`;
  const ramo=(r,n)=>`<div class="treeRegion" style="margin-left:${n*10}px"><div class="treeRegionTitle ${explorerRegionTarget===r.id?"target":""}" data-region="${r.id}">📁 ${escapeHTML(r.name)}</div>${arqs(r.id).map(linha).join("")}${filhos(r.id).map(x=>ramo(x,n+1)).join("")}</div>`;
  const raiz=arqs(null);
  tree.innerHTML=`<div class="treeRegion"><div class="treeRegionTitle ${explorerRegionTarget===null?"target":""}" data-region="">⌂ raiz</div>${raiz.map(linha).join("")}</div>`+filhos(null).map(x=>ramo(x,0)).join("");
  updateExplorerActions();
  tree.querySelectorAll(".treeRegionTitle").forEach(el=>{
    el.onclick=e=>{if(dragState)return; e.stopPropagation();selecionarRegiaoExplorer(el.dataset.region||null)};
  });
  tree.querySelectorAll("[data-file]").forEach(el=>{
    let touchState=null,lastTouchHandled=0;
    const activate=()=>{
      const idd=el.dataset.file,b=world.buildings.find(x=>x.id===idd);if(!b)return;
      if(explorerSelection.size){toggleExplorerSelection(idd);return}
      explorerRegionTarget=b.regionId||null;
      if(b.tipo==="nota"){openFullEditor(b);fileSidebar.classList.remove("open")}else{selected=b;openFilePreview(b);pedirDesenho();buildTree()}
    };
    const startHold=(x,y,kind,id)=>{
      explorerHoldFired=false;clearTimeout(explorerHoldTimer);explorerHoldPointer={id,kind,x,y};
      explorerHoldTimer=setTimeout(()=>{
        explorerHoldFired=true;explorerHoldPointer=null;if(touchState)touchState.held=true;
        toggleExplorerSelection(el.dataset.file,true);if(navigator.vibrate)navigator.vibrate(18);
        iniciarArrasto([...explorerSelection],x,y);
      },EXPLORER_HOLD_MS);
    };
    const cancelHold=()=>{clearTimeout(explorerHoldTimer);explorerHoldPointer=null};
    el.addEventListener("touchstart",e=>{if(e.touches.length!==1){cancelHold();return}const t=e.touches[0];touchState={x:t.clientX,y:t.clientY,moved:false,held:false};startHold(t.clientX,t.clientY,"touch",0)},{passive:true});
    el.addEventListener("touchmove",e=>{if(dragState){const t=e.touches[0];if(!t)return;e.preventDefault();atualizarArrasto(t.clientX,t.clientY);return}if(!touchState||!e.touches.length)return;const t=e.touches[0];if(Math.hypot(t.clientX-touchState.x,t.clientY-touchState.y)>EXPLORER_HOLD_MOVE){touchState.moved=true;cancelHold()}},{passive:false});
    el.addEventListener("touchend",()=>{cancelHold();const st=touchState;touchState=null;if(dragState){soltarArrasto();explorerHoldFired=false;lastTouchHandled=Date.now();return}if(!st||st.moved)return;lastTouchHandled=Date.now();if(st.held||explorerHoldFired){explorerHoldFired=false;return}activate()},{passive:true});
    el.addEventListener("touchcancel",()=>{cancelHold();cancelarArrasto();touchState=null},{passive:true});
    el.onclick=()=>{if(Date.now()-lastTouchHandled<700)return;if(explorerHoldFired){explorerHoldFired=false;return}activate()};
    el.addEventListener("pointerdown",e=>{if(e.pointerType==="touch")return;if(e.pointerType==="mouse"&&e.button!==0)return;try{el.setPointerCapture(e.pointerId)}catch(_){}startHold(e.clientX,e.clientY,e.pointerType,e.pointerId)});
    el.addEventListener("pointermove",e=>{if(e.pointerType==="touch")return;if(dragState){atualizarArrasto(e.clientX,e.clientY);return}if(!explorerHoldPointer||explorerHoldPointer.id!==e.pointerId)return;if(Math.hypot(e.clientX-explorerHoldPointer.x,e.clientY-explorerHoldPointer.y)>EXPLORER_HOLD_MOVE)cancelHold()});
    ["pointerup","pointercancel","pointerleave"].forEach(ev=>el.addEventListener(ev,e=>{if(e.pointerType==="touch")return;cancelHold();if(dragState&&ev==="pointerup")soltarArrasto()}));
  });
}
function loadFile(b){wikiState={open:false,start:-1,query:"",items:[],index:0};renderWikiSuggestions();currentFile=b;selected=b;try{var _p=window.UrbeCore&&window.UrbeCore.service("legacy.documents");if(_p){var _d=_p.syncBuilding(b,"editor.open");window.UrbeCore.service("editor.session")?.open(_d&&_d.id)}}catch(_){}document.getElementById("fileNameDisplay").textContent=nomeCompletoNota(b);document.getElementById("documentWatermark").textContent=nomeCompletoNota(b);bodyEditor.value=b.content;renderProps(b);buildTree();updateStats();setEditorViewMode(editorViewMode)}
function openFullEditor(b){closeHouseSummary();editorFull.classList.add("open");setEditorViewMode("preview");loadFile(b)}
function closeFullEditor(){wikiState={open:false,start:-1,query:"",items:[],index:0};renderWikiSuggestions();editorFull.classList.remove("open");fileSidebar.classList.remove("open");currentFile=null}
document.getElementById("closeFullEditor").onclick=closeFullEditor;
document.getElementById("sidebarToggleMobile").onclick=()=>{var c=window.UrbeCore&&window.UrbeCore.commands;if(c&&c.has("ui.explorer.open"))c.execute("ui.explorer.open",{source:"editor"});else fileSidebar.classList.add("open")};
document.getElementById("closeSidebar").onclick=()=>fileSidebar.classList.remove("open");
function markChanged(){
  if(!currentFile)return;
  const antes=currentFile.content||"",depois=bodyEditor.value;
  if(editorViewMode==="preview")renderCurrentPreview();
  currentFile.content=depois;currentFile.modified=nowDate();
  if(assinaturaLinks(antes)!==assinaturaLinks(depois))scheduleRoadRebuild();
  document.getElementById("saveState").textContent="Salvando...";
  clearTimeout(saveTimer);
  saveTimer=setTimeout(()=>document.getElementById("saveState").textContent="Salvo",450);
  updateStats()
}
bodyEditor.addEventListener("input",()=>{markChanged();updateWikiAutocomplete()});
function updateStats(){const txt=bodyEditor.value.trim();const words=txt?txt.split(/\s+/).length:0;const lines=bodyEditor.value.split("\n").length;document.getElementById("editorStats").textContent=`Markdown · ${lines} linhas · ${words} palavras`}
bodyEditor.addEventListener("keydown",e=>{
  if(!wikiState.open)return;
  if(e.key==="ArrowDown"){
    e.preventDefault();
    if(wikiState.items.length)wikiState.index=(wikiState.index+1)%wikiState.items.length;
    renderWikiSuggestions();
    return;
  }
  if(e.key==="ArrowUp"){
    e.preventDefault();
    if(wikiState.items.length)wikiState.index=(wikiState.index-1+wikiState.items.length)%wikiState.items.length;
    renderWikiSuggestions();
    return;
  }
  if((e.key==="Enter"||e.key==="Tab")&&wikiState.items.length){
    e.preventDefault();
    commitWikiSuggestion(wikiState.items[wikiState.index]);
    return;
  }
  if(e.key==="Escape"){
    e.preventDefault();
    wikiState={open:false,start:-1,query:"",items:[],index:0};
    renderWikiSuggestions();
  }
});
bodyEditor.addEventListener("click",updateWikiAutocomplete);
bodyEditor.addEventListener("keyup",e=>{
  if(["ArrowDown","ArrowUp","Enter","Tab","Escape"].includes(e.key))return;
  updateWikiAutocomplete();
});
document.addEventListener("pointerdown",e=>{
  if(wikiState.open && e.target!==bodyEditor && !wikiSuggest.contains(e.target)){
    wikiState={open:false,start:-1,query:"",items:[],index:0};
    renderWikiSuggestions();
  }
});
window.addEventListener("resize",positionWikiPopup);

function insertAtCursor(prefix,suffix=""){const ta=bodyEditor,start=ta.selectionStart,end=ta.selectionEnd,sel=ta.value.slice(start,end);ta.setRangeText(prefix+sel+suffix,start,end,"end");ta.focus();markChanged()}
document.querySelectorAll(".mdBtn").forEach(btn=>btn.onclick=()=>{
  if(btn.dataset.special==="link"){insertAtCursor("[","](url)");return}
  if(btn.dataset.special==="noteLink"){
    const choices=world.buildings.filter(b=>b!==currentFile).map(b=>b.name);
    if(!choices.length){toast("Crie outra nota para poder vinculá-la.");return}
    const pos=[bodyEditor.selectionStart,bodyEditor.selectionEnd];
    UD.choose({title:"Vincular nota",options:choices.map(n=>({value:n,icon:"file",label:n}))}).then(target=>{if(!target)return;bodyEditor.focus();bodyEditor.setSelectionRange(pos[0],pos[1]);insertAtCursor("[["+target+"]]")});
    return;
  }
  if(btn.dataset.wrap){insertAtCursor(btn.dataset.wrap,btn.dataset.wrap);return}
  insertAtCursor(btn.dataset.md||"")
});
document.getElementById("newFileBtn").onclick=()=>{
  const sem=semente("novo"+Date.now()),dest=destinoCriacaoExplorer();
  const r=dest?world.regions.find(x=>x.id===dest)||null:null;
  const pos=r?vagaNaRegiao(r,sem,new Set()):vagaAleatoria(sem,3,3);
  if(!pos){toast(r?"Sem lote 3x3 livre nesta pasta/região.":"Não encontrei lote livre próximo; mova a câmera e tente novamente.");return}
  const b={id:id("b"),kind:"building",regionId:r?r.id:null,x:pos.x,y:pos.y,w:3,h:3,
    name:"Novo arquivo",description:"",content:"# Novo arquivo\n\n",sprite:"house1",tipo:"nota",tags:[],created:nowDate(),modified:nowDate()};
  world.buildings.push(b);explorerRegionTarget=r?r.id:null;marcarIndice();indexar();counts();scheduleRoadRebuild();agendarSalvar();
  openFullEditor(b);fileSidebar.classList.remove("open");toast("Nova nota criada"+(r?" em "+r.name:" na raiz")+".");
};
document.getElementById("newFolderBtn").onclick=async()=>{
  const nome=((await UD.prompt({title:"Nova pasta",label:"Nome da pasta",value:"Nova pasta",confirm:"Criar pasta"}))||"").trim();if(!nome)return;
  const parentId=destinoCriacaoExplorer(),parent=parentId?world.regions.find(x=>x.id===parentId)||null:null;
  const r=criarRegiaoOrganica(nome,1,semente(nome+Date.now()),parent?parent.id:null,null,"Pasta criada pelo Explorador.");
  if(!r){toast(parent?"A pasta selecionada não tem área seca disponível para uma sub-região.":"Não encontrei terra seca para a nova região.");return}
  explorerRegionTarget=r.id;marcarIndice();indexar();counts();buildTree();agendarSalvar();
  toast("Pasta \""+nome+"\" criada"+(parent?" dentro de "+parent.name:"")+".");
};
document.getElementById("groupFilesBtn").onclick=agruparSelecionados;
document.getElementById("moveFilesBtn").onclick=()=>escolherDestinoEMover([...explorerSelection]);
document.getElementById("deleteFilesBtn").onclick=excluirSelecionados;
document.getElementById("clearSelectionBtn").onclick=()=>{explorerSelection.clear();buildTree()};

/* AI panel */
const aiPanel=document.getElementById("aiPanel");




/* Region dialog */
let pendingRegion=null,dlg=document.getElementById("dialog");
function openRegionDialog(bounds){pendingRegion=bounds;dlg.classList.add("open");document.getElementById("regionName").focus()}
document.getElementById("cancelRegion").onclick=()=>{dlg.classList.remove("open");pendingRegion=null};
/* (mascaraRetanguloRegiao: definição antiga removida na 1.0 — a versão em uso está mais abaixo) */
document.getElementById("confirmRegion").onclick=()=>{
  if(!pendingRegion)return;
  /* Desenhar no mundo cria região de topo. Sub-regiões são criadas de forma
     explícita pelo Explorador, evitando que uma sobreposição casual transforme
     a seleção em filha e descarte quase toda a área. */
  const m=mascaraRetanguloRegiao(pendingRegion,null);
  if(!m.cells.length){toast("A seleção ficou totalmente sobre água.");return}
  const pts=m.cells.map(k=>k.split(",").map(Number)),xs=pts.map(p=>p[0]),ys=pts.map(p=>p[1]);
  const r={id:id("r"),kind:"region",x:Math.min(...xs),y:Math.min(...ys),w:Math.max(...xs)-Math.min(...xs)+1,h:Math.max(...ys)-Math.min(...ys)+1,cells:m.cells,name:document.getElementById("regionName").value||"Região",description:document.getElementById("regionDesc").value,color:colors[world.regions.length%colors.length],parentId:null};
  r._cellSet=new Set(r.cells);world.regions.push(r);explorerRegionTarget=r.id;marcarIndice();indexar();dlg.classList.remove("open");pendingRegion=null;counts();buildTree();agendarSalvar();
  if(m.total&&m.secos/m.total<.5)toast("Região criada: a água foi recortada automaticamente.");else toast("Região criada sobre a maior área de terra conectada.");
  setTool("select")
};

/* Explorador do projeto usa a árvore do editor */
document.getElementById("archiveBtn").onclick=()=>{buildTree();fileSidebar.classList.add("open")};

/* ============================================================
   v0.12 — persistência, vault e IA
   ============================================================ */

/* ---------- IndexedDB (adaptador src/persistence/adapters/idb.js) ---------- */
const _idb=window.UrbeAdapters.idb.create({mime:mimePorNome}),DBK=_idb.store;

/* ---------- serialização da cidade ---------- */
function serializarCidade(){
  return {v:1, salvo:new Date().toISOString(), camera:{...camera},
    regions:world.regions.map(r=>{const {_cellSet,...clean}=r;return clean}),
    buildings:world.buildings.map(b=>({...b}))};
}
function aplicarCidade(d){
  if(!d||!d.buildings)return false;
  world.regions.length=0;world.buildings.length=0;
  for(const r of d.regions||[]){if(r.cells)r._cellSet=new Set(r.cells);world.regions.push(r);}
  for(const b of d.buildings||[])world.buildings.push(b);
  if(d.camera)camera={...camera,...d.camera};
  marcarIndice();indexar();rebuildRoadNetwork();counts();
  return true;
}
let saveTimerCity=null,salvando=false,salvarPendente=false;
function assinaturaLinks(md=""){
  const links=[];
  String(md).replace(/\[\[([^\]\n]+)\]\]/g,(_,raw)=>{
    const alvo=String(raw).split("|")[0].split("#")[0].trim().toLowerCase();
    if(alvo)links.push(alvo);
    return _;
  });
  return links.sort().join("\u001f");
}
/* v0.14: agendarSalvar virou o gatilho da sincronizacao com o disco.
   Quem chama nao muda; o que acontece embaixo, sim. */
function agendarSalvar(){ try{marcarSinc()}catch(_){ } }
async function salvarCidade(){ try{await rodarSinc(true)}catch(_){ } }
/* v0.13.4 — persistência desacoplada do HUD/câmera.
   counts() é UI e pode rodar dezenas de vezes durante zoom/pan: nunca salva.
   markChanged() continua persistindo conteúdo, com debounce. */
const _counts=counts;
counts=function(){_counts.apply(this,arguments)};
const _markChanged=markChanged;
markChanged=function(){_markChanged.apply(this,arguments);agendarSalvar()};
addEventListener("pagehide",()=>{salvarCidade()});
document.addEventListener("visibilitychange",()=>{if(document.hidden)salvarCidade()});

/* ---------- vault: importação ---------- */
const EXT_MD=window.UrbeArtifacts.RE.note;
const EXT_TEXTO=window.UrbeArtifacts.RE.note; // compatibilidade: somente Markdown vira nota/casa
const EXT_IMG=/\.(png|jpe?g|gif|webp|svg)$/i;
const EXT_AUDIO=/\.(mp3|wav|ogg|m4a|aac|flac)$/i;
const EXT_VIDEO=/\.(mp4|webm|mov|mkv|avi|m4v)$/i;
const EXT_PDF=/\.pdf$/i;
const EXT_HTML=/\.(html?|xhtml)$/i;
const EXT_ZIP=/\.(zip|7z|rar|tar|gz)$/i;
const LIMITE_IMG=300*1024;
const PREVIEW_CLASSES=new Set(["image","audio","video","pdf","html"]);
function mimePorNome(nome){const e=(nome.split(".").pop()||"").toLowerCase();return {png:"image/png",jpg:"image/jpeg",jpeg:"image/jpeg",gif:"image/gif",webp:"image/webp",svg:"image/svg+xml",mp3:"audio/mpeg",wav:"audio/wav",ogg:"audio/ogg",m4a:"audio/mp4",aac:"audio/aac",flac:"audio/flac",mp4:"video/mp4",webm:"video/webm",mov:"video/quicktime",m4v:"video/mp4",pdf:"application/pdf",html:"text/html",htm:"text/html",xhtml:"application/xhtml+xml"}[e]||"application/octet-stream"}
function cacheAssetKey(){return (Disco.cidade||"local")+"|"+id("asset")}

function limparNome(n){return n.replace(/\.[^.]+$/," ").trim().split("/").pop()}
function baseNome(n){return n.split("/").pop().toLowerCase()}
function classificarArquivo(rel){if(EXT_IMG.test(rel))return "image";if(EXT_AUDIO.test(rel))return "audio";if(EXT_VIDEO.test(rel))return "video";if(EXT_PDF.test(rel))return "pdf";if(EXT_HTML.test(rel))return "html";if(EXT_ZIP.test(rel))return "zip";return "other"}
function rotuloClasse(c){return {image:"Imagem",audio:"Áudio",video:"Vídeo",pdf:"PDF",html:"HTML",zip:"Arquivo compactado",other:"Arquivo"}[c]||"Arquivo"}

/* ---------- montagem da árvore de pastas ---------- */
function montarArvore(itens){
  const raiz={nome:"",filhas:new Map(),arquivos:[]};
  for(const it of itens){const seg=it.rel.split("/").filter(Boolean);let no=raiz;for(let i=0;i<seg.length-1;i++){if(!no.filhas.has(seg[i]))no.filhas.set(seg[i],{nome:seg[i],filhas:new Map(),arquivos:[]});no=no.filhas.get(seg[i])}no.arquivos.push(it)}
  return raiz;
}
/* (pontoLivreRegiao: definição antiga removida na 1.0 — a versão em uso está mais abaixo) */
/* (construirMascaraRegiao: definição antiga removida na 1.0 — a versão em uso está mais abaixo) */
function criarRegiaoOrganica(nome,quantidade,sem,parentId=null,origem=null,descricao="Região automática adaptada ao terreno."){
  const parent=parentId?world.regions.find(r=>r.id===parentId)||null:null;
  const m=construirMascaraRegiao(sem,quantidade,origem,parent);if(!m)return null;
  const r={id:id("r"),kind:"region",x:m.x,y:m.y,w:m.w,h:m.h,cells:m.cells,name:nome,description:descricao,parentId:parent?parent.id:null,color:colors[world.regions.length%colors.length]};
  r._cellSet=new Set(r.cells);world.regions.push(r);indexarUm(idxR,r);return r;
}
function casaCabeNaRegiao(r,x,y,ignorarIds=new Set()){return loteValido(x,y,ignorarIds,r)}
function posicaoAleatoriaNaRegiao(r,sem,ignorarIds=new Set()){
  const rand=rng(sem);
  for(let i=0;i<1400;i++){
    const x=r.x+Math.floor(rand()*Math.max(1,r.w-2)),y=r.y+Math.floor(rand()*Math.max(1,r.h-2));
    if(casaCabeNaRegiao(r,x,y,ignorarIds))return{x,y};
  }
  for(let y=r.y;y<r.y+r.h-2;y++)for(let x=r.x;x<r.x+r.w-2;x++)if(casaCabeNaRegiao(r,x,y,ignorarIds))return{x,y};
  return null;
}
function criarCasa(arq,x,y,regId){
  const b={id:id("b"),kind:"building",regionId:regId||null,x,y,w:3,h:3,name:arq.nome,description:"",content:arq.texto||"",sprite:["house1","house2","house3"][semente(arq.rel)%3],tipo:"nota",tags:[],anexos:[],created:nowDate(),modified:nowDate()};
  world.buildings.push(b);indexarUm(idxB,b);return b;
}
function criarPredioArquivo(arq,x,y,regId,opts={}){
  const c=opts.fileClass||classificarArquivo(arq.rel||arq.nome||"");
  const files=opts.files||[arq.anexo||{nome:baseNome(arq.rel||arq.nome),tipo:(arq.rel||"").split(".").pop().toLowerCase()}];
  const b={id:id("b"),kind:"building",regionId:regId||null,x,y,w:3,h:3,name:opts.name||arq.nome||rotuloClasse(c),fileName:arq.anexo?.nome||(arq.rel?arq.rel.split("/").pop():undefined),description:opts.description||rotuloClasse(c),content:"",sprite:"file-"+c,tipo:opts.aux?"anexo":"arquivo",fileClass:c,parentNoteId:opts.parentNoteId||null,files,anexos:files,tags:[],created:nowDate(),modified:nowDate()};
  world.buildings.push(b);indexarUm(idxB,b);return b;
}
function posicaoAdjacenteArquivo(dono,r,sem){
  const d=LOTE_GAP+3,cands=[[d,0],[-d,0],[0,d],[0,-d],[d,d],[-d,d],[d,-d],[-d,-d]];
  const shift=semente(String(sem))%cands.length;
  for(let i=0;i<cands.length;i++){const [dx,dy]=cands[(i+shift)%cands.length],x=dono.x+dx,y=dono.y+dy;if(loteValido(x,y,new Set(),r))return{x,y}}
  return null;
}
function distribuirNotas(r,arquivos,mapa){
  for(let i=0;i<arquivos.length;i++){const arq=arquivos[i],pos=r?vagaNaRegiao(r,semente(arq.rel+":"+i),new Set()):vagaAleatoria(semente(arq.rel+":"+i));if(!pos){toast("Sem lote para "+arq.nome+" em "+(r?r.name:"raiz")+".");continue}const b=criarCasa(arq,pos.x,pos.y,r?r.id:null);mapa.set(baseNome(arq.rel),b);mapa.set(baseNome(arq.rel).replace(/\.md$/i,""),b)}
}
function contarPastasNo(no){let n=0;for(const f of no.filhas.values())n+=1+contarPastasNo(f);return n}
function contarItensNo(no){let n=no.arquivos.length;for(const f of no.filhas.values())n+=contarItensNo(f);return n}
function importarItens(itens,opcoes={}){
  const mapa=new Map(),arvore=montarArvore(itens),itemRegion=new Map();
  const destinoId=opcoes.destinoRegionId||null;let destino=destinoId?world.regions.find(r=>r.id===destinoId)||null:null;
  if(opcoes.wrapRootName){
    const qtd=Math.max(1,contarItensNo(arvore));
    const wr=criarRegiaoOrganica(opcoes.wrapRootName,qtd,semente(opcoes.wrapRootName+Date.now()),destino?destino.id:null,null,"Pasta importada do Vault.");
    if(wr)destino=wr;
  }
  const importarNo=(no,parent,isRoot=false)=>{
    let r=parent;
    if(!isRoot){
      const qtd=Math.max(1,contarItensNo(no)+contarPastasNo(no)*2);
      r=criarRegiaoOrganica(no.nome,qtd,semente(no.nome+qtd+":"+(parent?.id||"root")),parent?.id||null,null,"Pasta importada do Vault.");
      if(!r){toast("Não coube criar a sub-região "+no.nome+".");return}
    }
    for(const it of no.arquivos)itemRegion.set(it,r||null);
    /* subpastas primeiro: reservam o espaço delas; as casas desta pasta ocupam o resto */
    for(const filha of no.filhas.values())importarNo(filha,r,false);
    distribuirNotas(r,no.arquivos.filter(i=>EXT_MD.test(i.rel)),mapa);
  };
  importarNo(arvore,destino,true);

  /* Referências a anexos: um lote por TIPO para cada nota, não um lote por arquivo. */
  const citados=new Map();
  for(const b of world.buildings.filter(x=>x.tipo==="nota")){
    let m;const re=/!?\[\[([^\]|#]+)/g;
    while((m=re.exec(b.content||""))!==null){const alvo=m[1].trim().toLowerCase(),bn=baseNome(alvo);citados.set(alvo,b);citados.set(bn,b);citados.set(bn.replace(/\.[^.]+$/,""),b)}
  }
  const grupos=new Map(),orfaos=[];
  for(const a of itens.filter(i=>!EXT_MD.test(i.rel))){
    const bn=baseNome(a.rel),sem=bn.replace(/\.[^.]+$/,""),dono=citados.get(a.rel.toLowerCase())||citados.get(bn)||citados.get(sem);
    if(!dono){orfaos.push(a);continue}
    const c=classificarArquivo(a.rel),k=dono.id+"|"+c;if(!grupos.has(k))grupos.set(k,{dono,c,items:[]});grupos.get(k).items.push(a);
  }
  let agrupados=0;
  for(const g of grupos.values()){
    const r=g.dono.regionId?world.regions.find(x=>x.id===g.dono.regionId)||null:null,pos=posicaoAdjacenteArquivo(g.dono,r,g.c+g.dono.id);
    if(!pos){toast("Sem lote adjacente para anexos de "+g.dono.name+".");continue}
    const camImport=caminhosRegioes();
    const files=g.items.map(i=>{const rr=itemRegion.get(i)||null;i.anexo.folderPath=rr?(camImport.get(rr.id)||""):"";return i.anexo});criarPredioArquivo(g.items[0],pos.x,pos.y,r?r.id:null,{aux:true,fileClass:g.c,parentNoteId:g.dono.id,files,name:files.length===1?files[0].nome:(rotuloClasse(g.c)+" ("+files.length+") · "+g.dono.name),description:files.length+" arquivo(s) citado(s) pela nota"});
    g.dono.anexos=(g.dono.anexos||[]).concat(files);agrupados+=files.length;
  }
  for(const a of orfaos){
    const r=itemRegion.get(a)||destino||null,pos=r?vagaNaRegiao(r,semente(a.rel),new Set()):vagaAleatoria(semente(a.rel));
    if(!pos){toast("Sem lote para "+baseNome(a.rel)+".");continue}
    if(a.anexo){const camImport=caminhosRegioes();a.anexo.folderPath=r?(camImport.get(r.id)||""):""}
    criarPredioArquivo(a,pos.x,pos.y,r?r.id:null);
  }
  marcarIndice();indexar();rebuildRoadNetwork();counts();buildTree();agendarSalvar();
  return {notas:itens.filter(i=>EXT_MD.test(i.rel)).length,citados:agrupados,orfaos:orfaos.length,regioes:world.regions.length};
}
async function lerEntrada(files,{expandZip=false}={}){
  const itens=[];
  const registrar=async(rel,pegar)=>{
    if(EXT_TEXTO.test(rel)){
      itens.push({rel,nome:limparNome(rel),texto:await pegar("string")});
      return;
    }
    const classe=classificarArquivo(rel),a={nome:rel.split("/").pop(),tipo:(rel.split(".").pop()||"").toLowerCase(),mime:mimePorNome(rel)};
    /* v0.18: todo binário importado é preservado; o cache é staging/fallback e
       a sincronização o materializa como arquivo real no vault físico. */
    const blob=await pegar("blob");
    if(blob){
      a.tamanho=blob.size;a.mime=blob.type||a.mime;a.cacheId=cacheAssetKey();
      try{await DBK.bSet(a.cacheId,blob)}catch(err){console.warn("cache de preview indisponível",err);delete a.cacheId}
      if(classe==="image"&&blob.size<LIMITE_IMG){const b64=await blob.arrayBuffer().then(ab=>{let out="",u=new Uint8Array(ab);for(let i=0;i<u.length;i++)out+=String.fromCharCode(u[i]);return btoa(out)});a.dataUrl="data:"+a.mime+";base64,"+b64}
    }else a.tamanho=await pegar("tamanho");
    itens.push({rel,nome:limparNome(rel),anexo:a});
  };
  for(const entrada of files){
    const f=entrada&&entrada.file?entrada.file:entrada,relEntrada=(entrada&&entrada.rel)||f.webkitRelativePath||f.name;
    if(expandZip&&/\.zip$/i.test(f.name)){
      const JSZipLib=await exigirJSZip(),zip=await JSZipLib.loadAsync(f);
      const nomes=Object.keys(zip.files).filter(n=>!zip.files[n].dir&&!n.includes("__MACOSX")&&!/^\./.test(baseNome(n)));
      let prefixo="";if(nomes.length){const partes=nomes.map(n=>n.split("/"));if(partes.every(p=>p.length>1&&p[0]===partes[0][0]))prefixo=partes[0][0]+"/"}
      for(const n of nomes){const ent=zip.files[n];await registrar(n.slice(prefixo.length),t=>t==="string"?ent.async("string"):t==="blob"?ent.async("blob"):t==="tamanho"?ent.async("uint8array").then(u=>u.length):ent.async(t))}
    }else{
      await registrar(relEntrada,t=>t==="string"?f.text():t==="blob"?Promise.resolve(f):t==="tamanho"?Promise.resolve(f.size):f.arrayBuffer().then(ab=>{let out="",u=new Uint8Array(ab);for(let i=0;i<u.length;i++)out+=String.fromCharCode(u[i]);return btoa(out)}));
    }
  }
  return itens;
}
/* ---------- JSZip embutido: importação/exportação ZIP 100% offline ---------- */
function carregarJSZip(){
  return window.JSZip ? Promise.resolve(window.JSZip) : Promise.reject(new Error("JSZip local indisponível"));
}
function exigirJSZip(){return carregarJSZip()}

/* ---------- vault: exportação ---------- */
async function exportarVault(){
  if(!Disco.cidade)return toast('Abra o Urbe antes de exportar.');
  try{
    await rodarSinc(true);
    const persistence=window.UrbeCore&&window.UrbeCore.service('persistence');
    if(persistence){await persistence.flush();while(persistence.busy)await new Promise(r=>setTimeout(r,20))}
    const JSZipLib=await exigirJSZip(),zip=new JSZipLib(),paths=await FS.listar(Disco.cidade);
    const physical=new Set(paths);
    for(const path of estadoDesejado().binarios.keys())if(!physical.has(path))throw Error('Anexo não gravado: '+path);
    for(const path of paths){
      if(path==='.urbe/journal.json')continue;
      const blob=await FS.lerBlob(Disco.cidade,path);
      if(!blob)throw Error('Arquivo indisponível: '+path);
      zip.file(path,blob);
    }
    baixarBlob(await zip.generateAsync({type:'blob'}),'Urbe-vault.zip');
    toast('Vault completo exportado.');
  }catch(error){toast('Falhou ao exportar: '+error.message);throw error}
}
function baixarBlob(blob,nome){
  const u=URL.createObjectURL(blob),a=document.createElement("a");
  a.href=u;a.download=nome;a.click();setTimeout(()=>URL.revokeObjectURL(u),4000);
}

/* ---------- importação/exportação pelo Explorador ---------- */
const vaultDlg=document.getElementById("vaultDlg"),vaultMsg=document.getElementById("vaultMsg");
function nomeRaizEntrada(files){
  for(const f of files){const rel=(f.webkitRelativePath||"").split("/").filter(Boolean);if(rel.length>1)return rel[0]}return "Vault";
}
async function resolverDestinoImportacao(nomePasta,ehPasta){
  if(explorerRegionTarget!==undefined)return {destinoRegionId:explorerRegionTarget||null};
  if(!ehPasta)return {destinoRegionId:null};
  const criar=await UD.confirm({title:"Onde importar?",message:`Criar a pasta “${nomePasta}” na raiz, ou importar os arquivos soltos na raiz?`,confirm:"Criar pasta",cancel:"Soltos na raiz"});
  return criar?{destinoRegionId:null,wrapRootName:nomePasta}:{destinoRegionId:null};
}
async function importarArquivosUI(entradas,ehPasta=false,nomePasta="Vault"){
  if(!entradas.length)return;toast("Lendo importação...");
  try{const itens=await lerEntrada(entradas);if(!itens.length){toast("Nenhum arquivo aproveitável.");return}const opts=await resolverDestinoImportacao(nomePasta,ehPasta),r=importarItens(itens,opts);toast(`${r.notas} nota(s) · ${r.citados} citado(s) · ${r.orfaos} arquivo(s) independente(s)`)}catch(err){console.error(err);toast("Falhou ao importar: "+err.message)}
}
async function listarHandlePasta(handle){
  const out=[];async function walk(dir,prefix=""){for await(const [nome,entry] of dir.entries()){if(nome.startsWith("."))continue;if(entry.kind==="directory")await walk(entry,prefix+nome+"/");else out.push({file:await entry.getFile(),rel:prefix+nome})}}await walk(handle,"");return out;
}
document.getElementById("explorerImportFolderBtn").onclick=async()=>{
  if(typeof window.showDirectoryPicker==="function"){try{const h=await window.showDirectoryPicker({mode:"read"});const itens=await listarHandlePasta(h);await importarArquivosUI(itens,true,h.name)}catch(e){if(e?.name!=="AbortError"){console.warn(e);toast("Não consegui abrir a pasta.")}}}
  else document.getElementById("vaultFolderIn").click();
};
document.getElementById("explorerImportFilesBtn").onclick=()=>document.getElementById("vaultFilesIn").click();
document.getElementById("vaultFolderIn").onchange=async e=>{const fs=[...e.target.files],nome=nomeRaizEntrada(fs),prefixo=nome+"/";const entradas=fs.map(f=>({file:f,rel:(f.webkitRelativePath||f.name).startsWith(prefixo)?(f.webkitRelativePath||f.name).slice(prefixo.length):(f.webkitRelativePath||f.name)}));await importarArquivosUI(entradas,true,nome);e.target.value=""};
document.getElementById("vaultFilesIn").onchange=async e=>{await importarArquivosUI([...e.target.files],false,"Arquivos");e.target.value=""};
document.getElementById("explorerExportBtn").onclick=exportarVault;
/* diálogo antigo fica apenas para compatibilidade da importação/exportação JSON */
document.getElementById("vaultClose").onclick=()=>vaultDlg.style.display="none";
document.getElementById("vaultExport").onclick=exportarVault;
document.getElementById("vaultIn").onchange=async e=>{const fs=[...e.target.files];try{const itens=await lerEntrada(fs,{expandZip:fs.length===1&&/\.zip$/i.test(fs[0].name)});const r=importarItens(itens,await resolverDestinoImportacao("Vault",false));toast(`${r.notas} nota(s) · ${r.orfaos} arquivo(s)`) }catch(err){toast("Falhou ao importar: "+err.message)}e.target.value=""};
document.getElementById("cityExport").onclick=()=>{
  baixarBlob(new Blob([JSON.stringify(serializarCidade(),null,2)],{type:"application/json"}),"cidade.json");
};
document.getElementById("cityIn").onchange=async e=>{
  try{
    const d=JSON.parse(await e.target.files[0].text());
    if(!aplicarCidade(d))throw new Error("arquivo n\u00e3o parece uma cidade");
    buildTree();salvarCidade();toast("Cidade carregada.");vaultDlg.style.display="none";
  }catch(err){vaultMsg.textContent="Falhou: "+err.message}
  e.target.value="";
};

/* ---------- IA ---------- */
















/* ---------- minimapa, mapa grande e busca ---------- */
const miniCv=document.getElementById("miniCv"),mini=miniCv.getContext("2d");
const mapao=document.getElementById("mapao"),mapaoCv=document.getElementById("mapaoCv"),
      mapaoCtx=mapaoCv.getContext("2d");
let limitesCache=null,limitesT=0;

function limitesMundo(){
  const agora=performance.now();
  if(limitesCache&&agora-limitesT<800)return limitesCache;
  let x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;
  const eng=(x,y,w,h)=>{x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x+w);y1=Math.max(y1,y+h)};
  for(const b of world.buildings)eng(b.x,b.y,b.w,b.h);
  for(const r of world.regions)eng(r.x,r.y,r.w,r.h);
  /* o mapa sempre mostra uma boa região em volta de onde se está: é para explorar */
  const c={x:camera.x/TILE,y:camera.y/TILE};eng(c.x-120,c.y-90,240,180);
  if(x0>x1){x0=-25;y0=-25;x1=25;y1=25}
  const m=Math.max(4,(x1-x0)*.06);x0-=m;y0-=m;x1+=m;y1+=m;
  /* mesma proporção do quadro do mapa: sem faixas vazias, e o toque continua batendo */
  const cvM=document.getElementById('mapaoCv'),asp=cvM&&cvM.height?cvM.width/cvM.height:1,w=x1-x0,h=y1-y0;
  if(w/h<asp){const nw=h*asp;x0-=(nw-w)/2;x1+=(nw-w)/2}else{const nh=w/asp;y0-=(nh-h)/2;y1+=(nh-h)/2}
  limitesCache={x0,y0,x1,y1};limitesT=agora;
  return limitesCache;
}
function pintarMapa(c,L,W2,H2,detalhe){
  const esc=Math.min(W2/(L.x1-L.x0),H2/(L.y1-L.y0));
  const px=x=>(x-L.x0)*esc,py=y=>(y-L.y0)*esc;
  c.fillStyle="#0d161c";c.fillRect(0,0,W2,H2);
  for(const r of [...world.regions].sort((a,b)=>b.w*b.h-a.w*a.h)){
    c.fillStyle=r.color+"33";c.fillRect(px(r.x),py(r.y),r.w*esc,r.h*esc);
    c.strokeStyle=r.color+"99";c.lineWidth=1;c.strokeRect(px(r.x),py(r.y),r.w*esc,r.h*esc);
    if(detalhe&&r.w*esc>44&&!r.parentId){
      c.fillStyle="#cfe0ea";c.font="10px ui-monospace";
      c.fillText(r.name,px(r.x)+3,py(r.y)+11);
    }
  }
  c.fillStyle="#ffd36a";
  for(const b of world.buildings){
    const t=Math.max(1.6,2.2*esc);
    c.fillRect(px(b.x),py(b.y),t,t);
  }
  const a=s2w(0,0),b=s2w(cv.w,cv.h);
  c.strokeStyle="#fff";c.lineWidth=1.5;
  c.strokeRect(px(a.x/TILE),py(a.y/TILE),(b.x-a.x)/TILE*esc,(b.y-a.y)/TILE*esc);
  return {esc,px,py};
}
/* o minimapa legado está oculto na interface atual: não gastar CPU pintando-o */
function desenharMini(){var el=document.getElementById('mini');if(!el||!el.offsetParent)return;pintarMapa(mini,limitesMundo(),miniCv.width,miniCv.height,false)}
function abrirMapao(){mapao.classList.add("open");pintarMapao()}
/* (pintarMapao: definição antiga removida na 1.0 — a versão em uso está mais abaixo) */
document.getElementById("mini").onclick=abrirMapao;
mapao.onclick=e=>{
  if(e.target!==mapaoCv){mapao.classList.remove("open");return}
  const L=limitesMundo(),r=mapaoCv.getBoundingClientRect();
  const esc=Math.min(mapaoCv.width/(L.x1-L.x0),mapaoCv.height/(L.y1-L.y0));
  const cx=(e.clientX-r.left)*(mapaoCv.width/r.width),cy=(e.clientY-r.top)*(mapaoCv.height/r.height);
  camera.x=(L.x0+cx/esc)*TILE;camera.y=(L.y0+cy/esc)*TILE;
  mapao.classList.remove("open");counts();
};

function irPara(b){
  camera.x=(b.x+b.w/2)*TILE;camera.y=(b.y+b.h/2)*TILE;
  camera.z=Math.max(camera.z,1.1);
  selected=b;openHouseSummary(b);counts();
}
const buscaIn=document.getElementById("buscaIn"),buscaLista=document.getElementById("buscaLista");
buscaIn.addEventListener("input",()=>{
  const q=buscaIn.value.trim().toLowerCase();
  if(!q){buscaLista.style.display="none";buscaLista.innerHTML="";return}
  const achados=world.buildings.filter(b=>b.name.toLowerCase().includes(q)).slice(0,8);
  buscaLista.innerHTML=achados.length
    ? achados.map(b=>`<button data-ir="${b.id}">${iconeArquivo(b)} ${b.name}</button>`).join("")
    : '<button disabled style="opacity:.6">nada encontrado</button>';
  buscaLista.style.display="block";
});
buscaLista.addEventListener("click",e=>{
  const t=e.target.closest("[data-ir]");if(!t)return;
  const b=world.buildings.find(x=>x.id===t.dataset.ir);
  if(b)irPara(b);
  buscaLista.style.display="none";buscaIn.value="";
});
addEventListener("keydown",e=>{if(e.key==="Escape")mapao.classList.remove("open")});

/* ============================================================
   v0.14 — pasta do aplicativo, varias cidades, sincronizacao total
   ------------------------------------------------------------
   Cada cidade e uma pasta. Dentro dela, as regioes sao subpastas
   e as notas sao arquivos .md com o conteudo puro do usuario:
   nada e injetado no texto, entao o vault abre no Obsidian sem
   ficar sujo. Toda a geometria (posicoes, regioes, camera) mora
   em .urbe/mapa.json, que o Obsidian ignora por comecar com ponto.
   ============================================================ */
const APP_NOME="Urbe";                     /* troque aqui para renomear o app */
const TEM_FSA=typeof window.showDirectoryPicker==="function";
const Disco={modo:null,raiz:null,hCidade:null,cidade:null};

document.getElementById("menuTitulo").textContent=APP_NOME.toUpperCase();
document.title=APP_NOME;

/* ---------- nomes de arquivo seguros ---------- */
function nomeSeguro(s){
  return String(s==null?"":s).replace(/[\\/:*?"<>|\u0000-\u001f]/g,"-")
    .replace(/\s+/g," ").trim().replace(/^\.+/,"").replace(/\.+$/,"").slice(0,90)||"sem-nome";
}

/* ---------- camada de disco: adaptadores de persistência (pasta real × IndexedDB), REQ-028 ---------- */
const _fsa=window.UrbeAdapters.fsa.create({root:()=>Disco.raiz}),FS=window.UrbeAdapters.router({mode:()=>Disco.modo,idb:_idb,fsa:_fsa});
/* v0.32: FS legado é apenas adapter do Persistence Core. */
try{var _persistence=window.UrbeCore&&window.UrbeCore.service('persistence');if(_persistence)_persistence.configure(FS)}catch(_){}

/* ---------- caminhos derivados do mundo ---------- */
function caminhosRegioes(){
  const cache=new Map(),usados=new Set();
  const calc=(r,prof)=>{
    if(cache.has(r.id))return cache.get(r.id);
    cache.set(r.id,nomeSeguro(r.name));           /* trava contra ciclo em parentId */
    const pai=r.parentId&&prof<24?world.regions.find(x=>x.id===r.parentId):null;
    const base=(pai?calc(pai,prof+1)+"/":"")+nomeSeguro(r.name);
    let p=base,n=2;
    while(usados.has(p.toLowerCase()))p=base+" ("+(n++)+")";
    usados.add(p.toLowerCase());cache.set(r.id,p);return p;
  };
  for(const r of world.regions)calc(r,0);
  return cache;
}
/* ---------- sincronizacao ---------- */
var snapArq=new Map(),snapPastas=new Set();
var sincTimer=null,sincRodando=false,sincPend=false,sincSuspenso=true;
const seloSinc=document.getElementById("syncSelo");
function statusSinc(estado,extra){
  if(!Disco.cidade){seloSinc.style.display="none";return}
  seloSinc.style.display="block";seloSinc.classList.toggle("erro",estado==="erro");
  seloSinc.textContent=estado==="gravando"?"gravando...":
    estado==="erro"?("erro ao gravar"+(extra?": "+extra:"")):
    Disco.cidade+(Disco.modo==="pasta"?" · pasta":" · interno");
  seloSinc.style.opacity=estado==="ok"?".55":"1";
}
/* Coalescencia de 120ms: e imperceptivel digitando e evita disparar uma
   gravacao atomica por tecla, que na SAF do Android engasga o teclado. */
function marcarSinc(){
  if(sincSuspenso||!Disco.cidade)return;
  sincPend=true;
  if(sincTimer)return;
  sincTimer=setTimeout(()=>{sincTimer=null;rodarSinc()},120);
}

/* ---------- abrir uma cidade do disco ---------- */
function dirDe(rel){const i=rel.lastIndexOf("/");return i<0?"":rel.slice(0,i)}
function enquadrarConteudoDoMundo(){
  const itens=[...world.regions,...world.buildings];if(!itens.length)return false;
  const f=faixaVisivel(),visivel=itens.some(o=>o.x<f.x1&&o.x+(o.w||1)>f.x0&&o.y<f.y1&&o.y+(o.h||1)>f.y0);
  if(visivel)return false;
  let minX=Infinity,minY=Infinity,maxX=-Infinity,maxY=-Infinity;
  for(const o of itens){minX=Math.min(minX,o.x);minY=Math.min(minY,o.y);maxX=Math.max(maxX,o.x+(o.w||1));maxY=Math.max(maxY,o.y+(o.h||1))}
  const ww=Math.max(4,maxX-minX),hh=Math.max(4,maxY-minY),vw=Math.max(240,cv.w||wrap.clientWidth||innerWidth),vh=Math.max(240,cv.h||wrap.clientHeight||innerHeight);
  camera.x=(minX+maxX)*TILE/2;camera.y=(minY+maxY)*TILE/2;
  camera.z=clamp(Math.min((vw*.78)/(ww*TILE),(vh*.72)/(hh*TILE),1.35),.5,2.8);
  pedirDesenho();return true;
}
async function abrirCidade(nome){
  sincSuspenso=true;
  try{
    const persistence=window.UrbeCore&&window.UrbeCore.service('persistence');
    const loaded=persistence?await persistence.load(nome):null;
    const rels=loaded?loaded.paths:await FS.listar(nome);
    const mds=loaded?loaded.documents.map(d=>d.path):rels.filter(r=>window.UrbeArtifacts.RE.note.test(r)&&!r.startsWith(".urbe/")&&!r.split("/").pop().startsWith("."));
    let mapa=loaded?loaded.metadata:null;
    if(!loaded){try{mapa=JSON.parse(await FS.ler(nome,".urbe/mapa.json")||"null")}catch(_){}}
    const geoReg=new Map(),geoNota=new Map();
    if(mapa){
      for(const r of mapa.regioes||[])geoReg.set(r.caminho,r);
      for(const k in (mapa.notas||{}))geoNota.set(k,mapa.notas[k]);
    }
    /* le o conteudo de todas as notas antes de mexer no mundo */
    const conteudos=new Map();
    if(loaded)for(const d of loaded.documents)conteudos.set(d.path,d.content||"");
    else for(const rel of mds)conteudos.set(rel,(await FS.ler(nome,rel))||"");
    const worldProjection=window.UrbeCore&&window.UrbeCore.service('world.projection');
    if(worldProjection)worldProjection.load(mapa);
    /* v0.28: documentos são a fonte lógica; a cidade passa a ser projeção. */
    const documentStore=window.UrbeCore&&window.UrbeCore.service('documents');
    if(documentStore&&!loaded)documentStore.replaceAll(mds.map(rel=>({id:rel,path:rel,content:conteudos.get(rel)||"",created:(geoNota.get(rel)&&geoNota.get(rel).criado)||null,modified:(geoNota.get(rel)&&geoNota.get(rel).modificado)||null})),{source:'vault.open',vault:nome});

    world.regions.length=0;world.buildings.length=0;world.roads.clear();
    world.links.length=0;selected=null;currentFile=null;
    idxB=new Map();idxR=new Map();

    /* pastas: as do mapa.json mais as deduzidas dos .md encontrados */
    const caminhos=new Set();
    for(const r of (mapa&&mapa.regioes)||[])if(r.caminho)caminhos.add(r.caminho);
    for(const rel of mds){let d=dirDe(rel);while(d){caminhos.add(d);d=dirDe(d)}}
    const ordenados=[...caminhos].sort((a,b)=>a.split("/").length-b.split("/").length||a.localeCompare(b));
    const regPorCaminho=new Map();
    for(const cam of ordenados){
      const paiCam=dirDe(cam),pai=paiCam?regPorCaminho.get(paiCam):null;
      const g=geoReg.get(cam),nomeR=(g&&g.nome)||cam.split("/").pop();
      const quantos=mds.filter(r=>dirDe(r)===cam).length||1;
      let r;
      if(g&&typeof g.x==="number"&&g.w>0){
        r={id:id("r"),kind:"region",x:g.x,y:g.y,w:g.w,h:g.h,cells:g.cells||null,
          name:nomeR,description:g.descricao||"",parentId:pai?pai.id:null,
          color:g.cor||colors[world.regions.length%colors.length]};
        if(r.cells)r._cellSet=new Set(r.cells);
        world.regions.push(r);indexarUm(idxR,r);
      }else{
        r=criarRegiaoOrganica(nomeR,quantos,semente(cam+quantos),pai?pai.id:null,urbeOrigemPelasCasas(cam,mds,geoNota),
          "Pasta do vault.");
      }
      regPorCaminho.set(cam,r);
    }
    /* notas */
    const semLugar=[];
    for(const rel of mds){
      const cam=dirDe(rel),r=cam?regPorCaminho.get(cam):null;
      const g=geoNota.get(rel);
      const _docLoaded=documentStore&&documentStore.get(rel);const b={id:id("b"),documentId:_docLoaded&&_docLoaded.id||(g&&g.id)||null,kind:"building",regionId:r?r.id:null,
        x:0,y:0,w:3,h:3,name:limparNome(rel),ext:(rel.match(window.UrbeArtifacts.RE.note)||[".md"])[0].toLowerCase(),description:"",
        content:conteudos.get(rel)||"",
        sprite:(g&&g.sprite)||["house1","house2","house3"][semente(rel)%3],
        tipo:"nota",tags:(g&&g.tags)||[],anexos:(g&&g.anexos)||[],
        created:(g&&g.criado)||nowDate(),modified:(g&&g.modificado)||nowDate()};
      if(g&&typeof g.x==="number"){b.x=g.x;b.y=g.y;world.buildings.push(b);indexarUm(idxB,b)}
      else semLugar.push({b,r,rel});
    }
    for(const {b,r,rel} of semLugar){
      const pos=r?posicaoAleatoriaNaRegiao(r,semente(rel)):vagaAleatoria(semente(rel),3,3);
      b.x=pos?pos.x:0;b.y=pos?pos.y:0;
      world.buildings.push(b);indexarUm(idxB,b);
    }
    /* v0.16: prédios de arquivos/anexos persistem no mapa mesmo quando o
       arquivo binário ainda não faz parte do app shell. */
    for(const g of (mapa&&mapa.construcoes)||[]){
      const r=g.caminho?regPorCaminho.get(g.caminho)||null:null;
      const paiNota=g.parentNoteName?world.buildings.find(n=>n.tipo==="nota"&&n.name===g.parentNoteName):null;
      const b={id:id("b"),kind:"building",regionId:r?r.id:null,x:g.x||0,y:g.y||0,w:g.w||3,h:g.h||3,
        name:g.name||g.fileName||"Arquivo",fileName:g.fileName||null,description:g.description||"",content:"",
        sprite:g.sprite||("file-"+(g.fileClass||"other")),tipo:g.tipo||"arquivo",fileClass:g.fileClass||"other",
        parentNoteId:paiNota?.id||null,files:g.files||g.anexos||[],anexos:g.anexos||g.files||[],tags:[],
        created:g.created||nowDate(),modified:g.modified||nowDate()};
      world.buildings.push(b);indexarUm(idxB,b);
    }
    if(mapa&&mapa.camera)camera={...camera,...mapa.camera};

    Disco.cidade=nome;
    Disco.hCidade=null;
    snapArq=new Map();snapPastas=new Set(caminhos);
    for(const rel of mds)snapArq.set(rel,conteudos.get(rel)||"");
    const mj=await FS.ler(nome,".urbe/mapa.json");
    if(mj!=null)snapArq.set(".urbe/mapa.json",mj);

    marcarIndice();indexar();rebuildRoadNetwork();enquadrarConteudoDoMundo();counts();buildTree();pedirDesenho();
    await DBK.set("ultimaCidade",nome);
    fecharMenu();
    sincSuspenso=false;
    statusSinc("ok");
    marcarSinc();                        /* grava o mapa.json inicial */
    toast(nome+" · "+mds.length+" nota(s)");
  }catch(e){
    console.warn(e);sincSuspenso=true;
    toast("Nao consegui abrir: "+(e&&e.message||e));
  }
}

/* ---------- menu ---------- */
const menuEl=document.getElementById("menuCidades");
function abrirMenu(){menuEl.classList.add("open");pintarModo();listarCidadesUI()}
function fecharMenu(){menuEl.classList.remove("open")}
document.getElementById("cidadesBtn").onclick=()=>{salvarCidade();abrirMenu()};

function pintarModo(){
  const selo=document.getElementById("seloModo"),txt=document.getElementById("textoPasta"),
        btn=document.getElementById("btnEscolherPasta");
  if(Disco.modo==="pasta"){
    selo.className="selo ok";selo.textContent="📂 pasta do dispositivo";
    txt.innerHTML="As cidades sao pastas dentro de <b>"+escapeHTML(Disco.raiz.name)+
      "</b>. Da para abrir tudo no Obsidian ou em qualquer editor.";
    btn.textContent="Trocar de pasta";
  }else if(TEM_FSA){
    selo.className="selo aviso";selo.textContent="armazenamento interno";
    txt.innerHTML="Ainda nao ha uma pasta escolhida. Sem ela, tudo fica guardado dentro do "+
      "navegador e some se voce limpar os dados do site.";
    btn.textContent="📂 Escolher a pasta do aplicativo";
  }else{
    selo.className="selo aviso";selo.textContent="armazenamento interno";
    txt.innerHTML="Este navegador nao deixa um site gravar em pasta do aparelho. "+
      "Tudo funciona igual, mas fica guardado dentro do navegador — use o botao <b>Vault</b> "+
      "para exportar um ZIP de vez em quando. No Android, o Chrome 150+ e o Samsung Internet 29+ "+
      "liberam a pasta de verdade.";
    btn.style.display="none";
  }
}
async function listarCidadesUI(){
  const el=document.getElementById("listaCidades");
  el.innerHTML='<div class="vazio">Carregando...</div>';
  let nomes=[];
  try{nomes=await FS.cidades()}catch(e){el.innerHTML='<div class="vazio">Erro ao listar.</div>';return}
  if(!nomes.length){el.innerHTML='<div class="vazio">Nenhuma cidade ainda. Crie a primeira ali embaixo.</div>';return}
  el.innerHTML="";
  for(const nome of nomes){
    const div=document.createElement("div");div.className="cidadeItem";
    const b=document.createElement("button");b.className="nome";
    b.innerHTML=escapeHTML(nome)+"<small>"+(nome===Disco.cidade?"aberta agora":"toque para abrir")+"</small>";
    b.onclick=()=>abrirCidade(nome);
    const x=document.createElement("button");x.className="lixo";x.textContent="🗑";
    x.onclick=async()=>{
      if(!(await UD.confirm({title:'Excluir a cidade “'+nome+'”?',message:'Todos os arquivos dela serão apagados.',confirm:'Excluir',danger:true})))return;
      try{
        if(Disco.cidade===nome){sincSuspenso=true;Disco.cidade=null;Disco.hCidade=null;statusSinc("ok")}
        await FS.excluirCidade(nome);listarCidadesUI();toast("Cidade excluida.");
      }catch(e){toast("Nao consegui excluir.")}
    };
    div.appendChild(b);div.appendChild(x);el.appendChild(div);
  }
}
document.getElementById("btnEscolherPasta").onclick=async()=>{
  if(!TEM_FSA)return;
  try{
    const h=await window.showDirectoryPicker({mode:"readwrite",id:"urbe",startIn:"documents"});
    if(h.requestPermission&&await h.requestPermission({mode:"readwrite"})!=="granted"){
      toast("Permissao de escrita negada.");return;
    }
    Disco.raiz=h;Disco.modo="pasta";Disco.cidade=null;Disco.hCidade=null;
    await DBK.set("pastaRaiz",h);
    pintarModo();listarCidadesUI();
    toast("Pasta ligada: "+h.name);
  }catch(e){
    if(e&&e.name==="AbortError")return;
    console.warn(e);
    toast("Este navegador recusou o acesso a pasta.");
  }
};
document.getElementById("btnNovaCidade").onclick=async()=>{
  const inp=document.getElementById("novaCidadeNome");
  const nome=nomeSeguro(inp.value.trim());
  if(!inp.value.trim()){toast("Da um nome pra cidade.");return}
  try{
    const jaTem=(await FS.cidades()).some(c=>c.toLowerCase()===nome.toLowerCase());
    if(jaTem){toast("Ja existe uma cidade com esse nome.");return}
    await FS.criarCidade(nome);
    inp.value="";
    await abrirCidade(nome);
  }catch(e){console.warn(e);toast("Nao consegui criar: "+(e&&e.message||e))}
};

/* ---------- migracao da cidade unica das versoes antigas ---------- */
async function migrarAntiga(){
  const d=await DBK.get("cidade");
  if(!d||!d.buildings||!d.buildings.length)return null;
  const nome="Cidade anterior";
  const modo=Disco.modo;Disco.modo="interno";
  await FS.criarCidade(nome);
  const salvarWorld=[world.regions.slice(),world.buildings.slice()];
  world.regions.length=0;world.buildings.length=0;
  for(const r of d.regions||[]){if(r.cells)r._cellSet=new Set(r.cells);world.regions.push(r)}
  for(const b of d.buildings||[])world.buildings.push(b);
  const {arquivos,pastas}=estadoDesejado();
  for(const p of [...pastas].sort((a,b)=>a.length-b.length))await FS.criarPasta(nome,p);
  for(const [rel,txt] of arquivos)await FS.escrever(nome,rel,txt);
  world.regions.length=0;world.buildings.length=0;
  salvarWorld[0].forEach(r=>world.regions.push(r));
  salvarWorld[1].forEach(b=>world.buildings.push(b));
  Disco.modo=modo;
  await DBK.del("cidade");
  return nome;
}


/* ============================================================
   v0.18 — Explorer de árvore real + storage físico + Fase 3
   ============================================================ */

/* ---------- regiões: contorno respeita regiões e lotes existentes ---------- */
function tileReservadoPorLote(x,y){
  for(const b of world.buildings){
    if(x>=b.x-LOTE_GAP&&x<b.x+b.w+LOTE_GAP&&y>=b.y-LOTE_GAP&&y<b.y+b.h+LOTE_GAP)return true;
  }
  return false;
}
function tilePermitidoNovaRegiao(x,y,parent=null,ignorarRegiaoId=null){
  if(ehAgua(x,y)||tileReservadoPorLote(x,y))return false;
  const atual=regAt({x,y});
  if(parent){
    if(!regionHasTile(parent,x,y))return false;
    if(atual&&atual.id!==parent.id&&atual.id!==ignorarRegiaoId)return false;
    return true;
  }
  return !atual||atual.id===ignorarRegiaoId;
}
function pontoLivreRegiao(sem,parent=null){
  const rand=rng(sem),livre=(x,y)=>tilePermitidoNovaRegiao(x,y,parent,null);
  if(parent){
    const cells=parent.cells||[];
    for(let i=0;i<Math.min(3200,Math.max(200,cells.length*3));i++){
      const k=cells[Math.floor(rand()*cells.length)];if(!k)break;const [x,y]=k.split(',').map(Number);if(livre(x,y))return{x,y};
    }
    return null;
  }
  const c=centroBuscaRaiz();
  for(let faixa=0;faixa<16;faixa++){
    const raio=18*Math.pow(2,faixa);
    for(let i=0;i<260;i++){const ang=rand()*Math.PI*2,dist=Math.sqrt(rand())*raio,x=Math.round(c.x+Math.cos(ang)*dist),y=Math.round(c.y+Math.sin(ang)*dist);if(livre(x,y))return{x,y}}
  }
  return null;
}
/* Bairro recriado sem forma salva (mapa.json antigo, app fechado antes de gravar): nasce onde
   as casas dele já estavam. Antes nascia num ponto livre qualquer e a casa ficava fora dele. */
function urbeOrigemPelasCasas(cam,rels,geo){
  if(!geo||!rels)return null;var sx=0,sy=0,n=0;
  rels.forEach(function(rel){var d=dirDe(rel);if(d!==cam&&d.indexOf(cam+'/')!==0)return;var g=geo.get(rel);if(g&&typeof g.x==='number'&&typeof g.y==='number'){sx+=g.x+1;sy+=g.y+1;n++}});
  return n?{x:Math.round(sx/n),y:Math.round(sy/n)}:null;
}
/* Buracos fechados de um bairro (área cercada pelo próprio bairro, sem água nem outro bairro)
   passam a ser parte dele. Devolve quantos tiles entraram. */
function urbeTaparBuracos(r){
  urbeCelulas(r);if(!r.cells.length)return 0;
  var x0=r.x-1,y0=r.y-1,x1=r.x+r.w,y1=r.y+r.h,W=x1-x0+1,H=y1-y0+1,fora=new Uint8Array(W*H),fila=[],i,x,y;
  var pai=r.parentId?world.regions.find(function(o){return o.id===r.parentId}):null;if(pai)urbeCelulas(pai);
  var ancestral=new Set();for(var a=r;a;a=a.parentId?world.regions.find(function(o){return o.id===a.parentId}):null){ancestral.add(a.id);if(ancestral.size>24)break}
  for(x=0;x<W;x++){fila.push(x,0,x,H-1)}for(y=0;y<H;y++){fila.push(0,y,W-1,y)}
  while(fila.length){y=fila.pop();x=fila.pop();if(x<0||y<0||x>=W||y>=H)continue;i=y*W+x;if(fora[i])continue;if(r._cellSet.has(K(x+x0,y+y0)))continue;fora[i]=1;fila.push(x+1,y,x-1,y,x,y+1,x,y-1)}
  var add=0;
  for(y=1;y<H-1;y++)for(x=1;x<W-1;x++){i=y*W+x;var k=K(x+x0,y+y0);if(fora[i]||r._cellSet.has(k))continue;
    if(ehAgua(x+x0,y+y0))continue;if(pai&&!pai._cellSet.has(k))continue;
    var dono=world.regions.find(function(o){return o!==r&&!ancestral.has(o.id)&&!urbeDentroDe(o.id,r)&&o.cells&&(o._cellSet||new Set(o.cells)).has(k)});if(dono)continue;
    var xx=x+x0,yy=y+y0,casaAlheia=world.buildings.some(function(b){return xx>=b.x&&xx<b.x+b.w&&yy>=b.y&&yy<b.y+b.h&&!(b.regionId&&urbeDentroDe(b.regionId,r))});if(casaAlheia)continue;
    r.cells.push(k);r._cellSet.add(k);add++}
  if(add){marcarIndice();indexar()}
  return add;
}
/* tapa os buracos de todos os bairros, do bairro de cima para os subbairros */
function urbeTaparTodos(){
  var nivel=function(r){var n=0,p=r;while(p&&p.parentId&&n<24){var pid=p.parentId;p=world.regions.find(function(o){return o.id===pid});n++}return n};
  var total=0;
  /* subbairro fica sempre dentro do bairro de cima: o que o filho tem e o pai não, passa a ser do pai
     (do mais fundo para cima, para chegar até o avô). Água e bairro de outro ramo não entram. */
  world.regions.slice().sort(function(a,b){return nivel(b)-nivel(a)}).forEach(function(f){
    if(!f.parentId||!f.cells||!f.cells.length)return;var p=world.regions.find(function(o){return o.id===f.parentId});if(!p)return;urbeCelulas(p);var add=0;
    f.cells.forEach(function(k){if(p._cellSet.has(k))return;var j=k.indexOf(','),x=+k.slice(0,j),y=+k.slice(j+1);if(ehAgua(x,y))return;
      if(world.regions.some(function(o){return o!==p&&o!==f&&!urbeDentroDe(o.id,p)&&!urbeDentroDe(p.id,o)&&o.cells&&(o._cellSet||new Set(o.cells)).has(k)}))return;
      p.cells.push(k);p._cellSet.add(k);add++});
    /* alisa os dentes de 1 tile que sobram na emenda (tile livre com 3 ou 4 vizinhos do bairro) */
    for(var volta=0;add&&volta<3;volta++){var novos=[];p.cells.forEach(function(k){var j=k.indexOf(','),x=+k.slice(0,j),y=+k.slice(j+1);
      [[1,0],[-1,0],[0,1],[0,-1]].forEach(function(d){var nx=x+d[0],ny=y+d[1],nk=K(nx,ny);if(p._cellSet.has(nk)||novos.indexOf(nk)>=0||ehAgua(nx,ny))return;
        var viz=[[1,0],[-1,0],[0,1],[0,-1]].filter(function(e){return p._cellSet.has(K(nx+e[0],ny+e[1]))}).length;if(viz<3)return;
        var pp=p.parentId?world.regions.find(function(o){return o.id===p.parentId}):null;if(pp&&!(pp._cellSet||new Set(pp.cells)).has(nk))return;
        if(world.regions.some(function(o){return o!==p&&!urbeDentroDe(o.id,p)&&!urbeDentroDe(p.id,o)&&o.cells&&(o._cellSet||new Set(o.cells)).has(nk)}))return;
        if(world.buildings.some(function(b){return nx>=b.x&&nx<b.x+b.w&&ny>=b.y&&ny<b.y+b.h&&!(b.regionId&&urbeDentroDe(b.regionId,p))}))return;novos.push(nk)})});
      if(!novos.length)break;novos.forEach(function(k){p.cells.push(k);p._cellSet.add(k)});add+=novos.length}
    if(add){v20RecalcularBounds(p);total+=add}});
  if(total){marcarIndice();indexar()}
  world.regions.slice().sort(function(a,b){return nivel(a)-nivel(b)}).forEach(function(r){if(r.cells&&r.cells.length)total+=urbeTaparBuracos(r)});
  return total;
}
function urbeDistanciaAoBairro(r,x,y){var m=1e9;urbeCelulas(r).forEach(function(k){var j=k.indexOf(','),dx=+k.slice(0,j)-x,dy=+k.slice(j+1)-y,d=dx*dx+dy*dy;if(d<m)m=d});return Math.sqrt(m)}
/* o bairro passa a incluir o terreno em volta de (x,y), dentro do bairro pai e sem invadir água nem outro bairro */
function urbeAbsorverEmVolta(r,x,y,raio){
  urbeCelulas(r);var pai=r.parentId?world.regions.find(function(o){return o.id===r.parentId}):null;if(pai)urbeCelulas(pai);
  var ancestral=new Set();for(var a=r;a;a=a.parentId?world.regions.find(function(o){return o.id===a.parentId}):null){ancestral.add(a.id);if(ancestral.size>24)break}
  var add=0,R=Math.ceil(raio);
  for(var yy=y-R;yy<=y+R;yy++)for(var xx=x-R;xx<=x+R;xx++){if((xx-x)*(xx-x)+(yy-y)*(yy-y)>raio*raio)continue;var k=K(xx,yy);
    if(r._cellSet.has(k)||ehAgua(xx,yy)||(pai&&!pai._cellSet.has(k)))continue;
    var dono=world.regions.find(function(o){if(o===r||ancestral.has(o.id)||!o.cells)return false;for(var d=o;d;d=d.parentId?world.regions.find(function(q){return q.id===d.parentId}):null)if(d===r)return false;return (o._cellSet||new Set(o.cells)).has(k)});if(dono)continue;
    r.cells.push(k);r._cellSet.add(k);add++}
  if(add){var pts=r.cells.map(function(c){return c.split(',').map(Number)}),xs=pts.map(function(p){return p[0]}),ys=pts.map(function(p){return p[1]});
    r.x=Math.min.apply(null,xs);r.y=Math.min.apply(null,ys);r.w=Math.max.apply(null,xs)-r.x+1;r.h=Math.max.apply(null,ys)-r.y+1;marcarIndice();indexar()}
  return add;
}
/* Regra da cidade: toda casa de nota fica dentro do próprio bairro. Uma casa que ficou fora
   (bairro refeito, mapa incompleto, casa movida por fora) volta para dentro; se não couber,
   o bairro cresce. Devolve quantas casas mudaram de lugar. */
function urbeCasasNosBairros(){
  var movidas=0;
  for(var i=0;i<world.buildings.length;i++){var b=world.buildings[i];if(b.tipo!=='nota'||!b.regionId)continue;
    var r=world.regions.find(function(x){return x.id===b.regionId});if(!r)continue;urbeCelulas(r);
    var cx=b.x+Math.floor(b.w/2),cy=b.y+Math.floor(b.h/2);
    if(r._cellSet.has(K(cx,cy)))continue;   /* o centro da casa está no bairro */
    /* bairro com buraco em volta da casa (cresceu desviando dela): tapa os buracos, de cima para baixo */
    var cadeia=[],c0=r,g0=0;while(c0&&g0++<24){cadeia.unshift(c0);c0=c0.parentId?world.regions.find(function(x){return x.id===c0.parentId}):null}
    var tapou=0;cadeia.forEach(function(rr){tapou+=urbeTaparBuracos(rr)});
    if(tapou&&r._cellSet.has(K(cx,cy))){movidas++;continue}
    /* casa perto do bairro (no vão de uma ferradura, na beirada): o bairro absorve o terreno dela */
    if(urbeDistanciaAoBairro(r,cx,cy)<=5){var absorveu=0;cadeia.forEach(function(rr){absorveu+=urbeAbsorverEmVolta(rr,cx,cy,3.2)});cadeia.forEach(function(rr){urbeTaparBuracos(rr)});
      if(absorveu&&r._cellSet.has(K(cx,cy))){movidas++;continue}}
    var ign=new Set([b.id]),pos=vagaNaRegiao(r,semente((b.documentId||b.id)+':volta'),ign);
    for(var t=0;!pos&&t<3;t++){if(!expandirRegiao(r,40))break;pos=vagaNaRegiao(r,semente((b.documentId||b.id)+':volta'+t),ign)}
    if(pos){b.x=pos.x;b.y=pos.y;movidas++}
  }
  if(movidas){marcarIndice();indexar();scheduleRoadRebuild();pedirDesenho();marcarSinc()}
  return movidas;
}
function construirMascaraRegiao(sem,quantidade,origem,parent=null){
  const alvo=Math.max(36,quantidade*81),rand=rng(sem);
  if(parent&&parent.cells&&parent.cells.length<alvo)expandirRegiao(parent,alvo-parent.cells.length+24);
  const permitido=(x,y)=>tilePermitidoNovaRegiao(x,y,parent,null);
  const seed=origem&&permitido(origem.x,origem.y)?origem:pontoLivreRegiao(sem,parent);if(!seed)return null;
  const seen=new Set(),cells=[],q=[seed];
  while(q.length&&cells.length<alvo){
    const cur=q.shift(),k=K(cur.x,cur.y);if(seen.has(k))continue;seen.add(k);if(!permitido(cur.x,cur.y))continue;
    cells.push(k);const dirs=[[1,0],[-1,0],[0,1],[0,-1]];
    for(let i=dirs.length-1;i>0;i--){const j=Math.floor(rand()*(i+1));[dirs[i],dirs[j]]=[dirs[j],dirs[i]]}
    for(const [dx,dy] of dirs)q.push({x:cur.x+dx,y:cur.y+dy});
  }
  if(!cells.length)return null;const pts=cells.map(k=>k.split(',').map(Number)),xs=pts.map(p=>p[0]),ys=pts.map(p=>p[1]);
  return {cells,x:Math.min(...xs),y:Math.min(...ys),w:Math.max(...xs)-Math.min(...xs)+1,h:Math.max(...ys)-Math.min(...ys)+1};
}
function expandirRegiao(r,tilesExtras){
  if(!r.cells){r.cells=[];for(let y=r.y;y<r.y+r.h;y++)for(let x=r.x;x<r.x+r.w;x++)r.cells.push(K(x,y))}
  if(!r._cellSet)r._cellSet=new Set(r.cells);
  const parent=r.parentId?world.regions.find(x=>x.id===r.parentId)||null:null,dirs=[[1,0],[-1,0],[0,1],[0,-1]],q=[],seen=new Set(r.cells);
  for(const k of r.cells){const [x,y]=k.split(',').map(Number);for(const [dx,dy] of dirs)q.push({x:x+dx,y:y+dy})}
  let add=0;
  while(q.length&&add<tilesExtras){const c=q.shift(),k=K(c.x,c.y);if(seen.has(k))continue;seen.add(k);if(!tilePermitidoNovaRegiao(c.x,c.y,parent,r.id))continue;r.cells.push(k);r._cellSet.add(k);add++;for(const [dx,dy] of dirs)q.push({x:c.x+dx,y:c.y+dy})}
  if(!add)return 0;const pts=r.cells.map(k=>k.split(',').map(Number)),xs=pts.map(p=>p[0]),ys=pts.map(p=>p[1]);r.x=Math.min(...xs);r.y=Math.min(...ys);r.w=Math.max(...xs)-r.x+1;r.h=Math.max(...ys)-r.y+1;marcarIndice();indexar();return add;
}
function mascaraRetanguloRegiao(bounds,parent=null){
  const dentro=(x,y)=>x>=bounds.x&&y>=bounds.y&&x<bounds.x+bounds.w&&y<bounds.y+bounds.h;
  const permitido=(x,y)=>dentro(x,y)&&tilePermitidoNovaRegiao(x,y,parent,null);
  let secos=0,total=0;const candidatos=[];
  for(let y=bounds.y;y<bounds.y+bounds.h;y++)for(let x=bounds.x;x<bounds.x+bounds.w;x++){total++;if(permitido(x,y)){secos++;candidatos.push({x,y})}}
  if(!candidatos.length)return{cells:[],secos,total};
  const globalSeen=new Set();let melhor=[];
  for(const seed of candidatos){const sk=K(seed.x,seed.y);if(globalSeen.has(sk))continue;const q=[seed],comp=[];while(q.length){const cur=q.shift(),k=K(cur.x,cur.y);if(globalSeen.has(k))continue;globalSeen.add(k);if(!permitido(cur.x,cur.y))continue;comp.push(k);q.push({x:cur.x+1,y:cur.y},{x:cur.x-1,y:cur.y},{x:cur.x,y:cur.y+1},{x:cur.x,y:cur.y-1})}if(comp.length>melhor.length)melhor=comp}
  return{cells:melhor,secos,total};
}

/* ---------- Fase 3: edição da região ---------- */
let regionEditTarget=null;
function openRegionDialog(bounds){
  regionEditTarget=null;pendingRegion=bounds;document.getElementById('regionDialogTitle').textContent='Nova região';document.getElementById('regionName').value='Nova região';document.getElementById('regionDesc').value='Área temática do workspace.';document.getElementById('regionColor').value=colors[world.regions.length%colors.length]||'#4db7ff';document.getElementById('confirmRegion').textContent='Criar região';dlg.classList.add('open');document.getElementById('regionName').focus();
}
function openRegionEditDialog(r){
  if(!r)return;regionEditTarget=r;pendingRegion=null;document.getElementById('regionDialogTitle').textContent='Editar região';document.getElementById('regionName').value=r.name||'';document.getElementById('regionDesc').value=r.description||'';document.getElementById('regionColor').value=/^#[0-9a-f]{6}$/i.test(r.color||'')?r.color:'#4db7ff';document.getElementById('confirmRegion').textContent='Salvar alterações';dlg.classList.add('open');
}
function atualizarFolderPathAoRenomear(antigo,novo){
  if(!antigo||antigo===novo)return;
  for(const b of world.buildings)for(const a of (b.files||b.anexos||[]))if(typeof a.folderPath==='string'&&(a.folderPath===antigo||a.folderPath.startsWith(antigo+'/')))a.folderPath=novo+a.folderPath.slice(antigo.length);
}
document.getElementById('cancelRegion').onclick=()=>{dlg.classList.remove('open');pendingRegion=null;regionEditTarget=null};
document.getElementById('confirmRegion').onclick=()=>{
  const nome=(document.getElementById('regionName').value||'Região').trim()||'Região',desc=document.getElementById('regionDesc').value||'',cor=document.getElementById('regionColor').value||'#4db7ff';
  if(regionEditTarget){const camAntes=caminhosRegioes().get(regionEditTarget.id)||'';regionEditTarget.name=nome;regionEditTarget.description=desc;regionEditTarget.color=cor;const camDepois=caminhosRegioes().get(regionEditTarget.id)||'';atualizarFolderPathAoRenomear(camAntes,camDepois);dlg.classList.remove('open');const editada=regionEditTarget;regionEditTarget=null;marcarIndice();indexar();buildTree();pedirDesenho();agendarSalvar();marcarSinc();toast('Região “'+editada.name+'” atualizada.');return}
  if(!pendingRegion)return;const m=mascaraRetanguloRegiao(pendingRegion,null);if(!m.cells.length){toast('A área está ocupada por água, outra região ou lotes existentes.');return}
  const pts=m.cells.map(k=>k.split(',').map(Number)),xs=pts.map(p=>p[0]),ys=pts.map(p=>p[1]);const r={id:id('r'),kind:'region',x:Math.min(...xs),y:Math.min(...ys),w:Math.max(...xs)-Math.min(...xs)+1,h:Math.max(...ys)-Math.min(...ys)+1,cells:m.cells,name:nome,description:desc,color:cor,parentId:null};r._cellSet=new Set(r.cells);world.regions.push(r);explorerRegionTarget=r.id;marcarIndice();indexar();dlg.classList.remove('open');pendingRegion=null;counts();buildTree();agendarSalvar();marcarSinc();toast(m.secos<m.total?'Região criada contornando água, regiões e lotes existentes.':'Região criada.');setTool('select');
};

let previewStorageSource='';
async function blobDoArquivo(a,relPreferido){
  const rel=relPreferido||a?.relPath;
  if(rel&&Disco.cidade){try{const b=await FS.lerBlob(Disco.cidade,rel);if(b){previewStorageSource=Disco.modo==='pasta'?'dispositivo':'armazenamento interno';return b}}catch(_){}}
  if(a?.cacheId){try{const b=await DBK.bGet(a.cacheId);if(b){previewStorageSource='cache offline';return b}}catch(_){}}
  if(a?.dataUrl){try{previewStorageSource='cache embutido';return await(await fetch(a.dataUrl)).blob()}catch(_){}}
  previewStorageSource='';return null;
}
function relBinUnico(dir,nome,usados){
  const seguro=nomeSeguro(nome||'arquivo'),i=seguro.lastIndexOf('.'),stem=i>0?seguro.slice(0,i):seguro,ext=i>0?seguro.slice(i):'';let n=1,base=seguro,rel=(dir?dir+'/':'')+base;while(usados.has(rel.toLowerCase())){n++;base=stem+' ('+n+')'+ext;rel=(dir?dir+'/':'')+base}usados.add(rel.toLowerCase());return rel;
}
function metaAssetLimpa(a,rel){const o={...a,relPath:rel,folderPath:dirDe(rel)};delete o._blob;delete o.file;delete o.handle;delete o.dataUrl;return o}
function metaAnexoNota(a){const o={...a};delete o._blob;delete o.file;delete o.handle;delete o.dataUrl;return o}
var snapBin=new Map();
function assinaturaBin(a,rel){return rel+'|'+(a?.tamanho||0)+'|'+(a?.cacheId||'')}
async function rodarSinc(agora){
  if(sincSuspenso||!Disco.cidade||(urbePersistence&&urbePersistence.readOnly))return; /* vault de formato futuro: nada é gravado (REQ-035) */if(agora){clearTimeout(sincTimer);sincTimer=null;sincPend=true}if(sincRodando){sincPend=true;return}sincRodando=true;
  try{while(sincPend){sincPend=false;const {arquivos,pastas,binarios}=estadoDesejado();statusSinc('gravando');
    for(const p of [...pastas].sort((a,b)=>a.length-b.length))if(p&&!snapPastas.has(p)){await FS.criarPasta(Disco.cidade,p);snapPastas.add(p)}
    for(const [rel,txt] of arquivos)if(rel!=='.urbe/mapa.json'&&snapArq.get(rel)!==txt){await FS.escrever(Disco.cidade,rel,txt);snapArq.set(rel,txt)}
    for(const [rel,item] of binarios){const sig=assinaturaBin(item.asset,rel);if(snapBin.get(rel)===sig)continue;const blob=await blobDoArquivo(item.asset,item.sourceRel);if(!blob)throw new Error('bytes indisponíveis para '+rel);await FS.escreverBlob(Disco.cidade,rel,blob);item.asset.relPath=rel;item.asset.folderPath=dirDe(rel);item.asset.tamanho=blob.size;item.asset.mime=blob.type||item.asset.mime||mimePorNome(rel);snapBin.set(rel,assinaturaBin(item.asset,rel))}
    for(const rel of [...snapArq.keys()])if(rel!=='.urbe/mapa.json'&&!arquivos.has(rel)){await FS.apagar(Disco.cidade,rel);snapArq.delete(rel)}
    for(const rel of [...snapBin.keys()])if(!binarios.has(rel)){await FS.apagar(Disco.cidade,rel);snapBin.delete(rel)}
    for(const p of [...snapPastas].sort((a,b)=>b.length-a.length))if(!pastas.has(p)){await FS.apagarPasta(Disco.cidade,p);snapPastas.delete(p)}
    /* .urbe/mapa.json tem um único escritor: WorkspacePersistence (REQ-040), que pede o mapa atual a urbeMapaAtual */
    if(agora)await urbePersistence.flush();else urbePersistence.schedule();
  }statusSinc('ok')}catch(e){console.warn('sync',e);statusSinc('erro',e&&e.name==='NotAllowedError'?'sem permissao':(e?.message||''))}finally{sincRodando=false}
}

/* ---------- preview: sempre tenta arquivo físico primeiro ---------- */
async function renderFilePreview(){
  if(!previewBuilding)return;limparPreviewObjectUrl();const files=arquivosDoPredio(previewBuilding),a=files[Math.max(0,Math.min(previewIndex,files.length-1))],classe=classificarArquivo(a.nome||previewBuilding.fileName||previewBuilding.name||'');document.getElementById('filePreviewTitle').textContent=a.nome||previewBuilding.name;
  const srcBtn=document.getElementById('htmlSourceBtn'),renderBtn=document.getElementById('htmlRenderBtn'),extBtn=document.getElementById('openExternalBtn'),downBtn=document.getElementById('downloadPreviewBtn');srcBtn.hidden=renderBtn.hidden=classe!=='html';extBtn.hidden=classe!=='pdf';downBtn.hidden=true;filePreviewBody.innerHTML='<div class="previewEmpty">Carregando do dispositivo…</div>';
  const blob=await blobDoArquivo(a);document.getElementById('filePreviewMeta').innerHTML=escapeHTML(rotuloClasse(classe)+(a.tamanho?' · '+formatBytes(a.tamanho):'')+(a.relPath?' · '+a.relPath:''))+(previewStorageSource?'<span class="previewSourceBadge">'+escapeHTML(previewStorageSource)+'</span>':'');
  if(!blob){filePreviewBody.innerHTML='<div class="previewEmpty"><b>Arquivo não encontrado</b>O item existe no índice da cidade, mas não foi encontrado nem na pasta física nem no cache offline.</div>';return}
  previewObjectUrl=URL.createObjectURL(blob);downBtn.hidden=false;downBtn.onclick=()=>baixarBlob(blob,a.nome||previewBuilding.name);extBtn.onclick=()=>{const u=URL.createObjectURL(blob);window.open(u,'_blank','noopener');setTimeout(()=>URL.revokeObjectURL(u),60000)};
  if(classe==='image'){const img=document.createElement('img');img.alt=a.nome||'';img.src=previewObjectUrl;filePreviewBody.replaceChildren(img)}
  else if(classe==='audio'){const el=document.createElement('audio');el.controls=true;el.preload='metadata';el.src=previewObjectUrl;filePreviewBody.replaceChildren(el)}
  else if(classe==='video'){const el=document.createElement('video');el.controls=true;el.playsInline=true;el.preload='metadata';el.src=previewObjectUrl;filePreviewBody.replaceChildren(el)}
  else if(classe==='pdf'){const fr=document.createElement('iframe');fr.className='pdfFrame';fr.src=previewObjectUrl;fr.title=a.nome||'PDF';filePreviewBody.replaceChildren(fr)}
  else if(classe==='html'){const txt=await blob.text();if(previewHtmlMode==='source'){const pre=document.createElement('pre');pre.textContent=txt;filePreviewBody.replaceChildren(pre)}else{const fr=document.createElement('iframe');fr.setAttribute('sandbox','');fr.setAttribute('referrerpolicy','no-referrer');fr.srcdoc=txt;filePreviewBody.replaceChildren(fr)}}
  else if(classe==='zip'){try{const z=await exigirJSZip(),zip=await z.loadAsync(blob),nomes=Object.keys(zip.files).filter(n=>!zip.files[n].dir);const pre=document.createElement('pre');pre.textContent=(nomes.length?nomes.slice(0,400).join('\n'):'Arquivo compactado vazio')+(nomes.length>400?'\n… +'+(nomes.length-400)+' itens':'');filePreviewBody.replaceChildren(pre)}catch(_){filePreviewBody.innerHTML='<div class="previewEmpty"><b>Arquivo compactado</b>'+escapeHTML(a.nome||'')+'</div>'}}
  else if((blob.type||'').startsWith('text/')||blob.size<2*1024*1024){try{const pre=document.createElement('pre');pre.textContent=await blob.text();filePreviewBody.replaceChildren(pre)}catch(_){filePreviewBody.innerHTML='<div class="previewEmpty"><b>'+escapeHTML(a.nome||'Arquivo')+'</b>Sem visualização nativa para este formato.</div>'}}
  else filePreviewBody.innerHTML='<div class="previewEmpty"><b>'+escapeHTML(a.nome||'Arquivo')+'</b>Sem visualização nativa para este formato.</div>';
}
function openFilePreview(b,index=0){if(!b||b.tipo==='nota')return;closeHouseSummary();previewBuilding=b;previewIndex=Math.max(0,index|0);previewHtmlMode='render';const files=arquivosDoPredio(b);filePreviewSelect.innerHTML=files.map((a,i)=>'<option value="'+i+'">'+escapeHTML(a.nome||('Arquivo '+(i+1)))+'</option>').join('');filePreviewSelect.value=String(previewIndex);filePreviewSelect.hidden=files.length<2;filePreview.classList.add('open');filePreview.setAttribute('aria-hidden','false');renderFilePreview()}

/* ---------- árvore do Explorer + ações por toque prolongado ---------- */
const explorerCollapsed=new Set();
function caminhoRegiaoPorId(idReg){if(!idReg)return'';const r=world.regions.find(x=>x.id===idReg);return r?(caminhosRegioes().get(r.id)||''):''}
function regiaoPorCaminho(cam){if(!cam)return null;const paths=caminhosRegioes();for(const r of world.regions)if(paths.get(r.id)===cam)return r;return null}
function pastaDoAsset(a,b){const cam=typeof a?.folderPath==='string'?a.folderPath:(a?.relPath?dirDe(a.relPath):caminhoRegiaoPorId(b.regionId));return regiaoPorCaminho(cam)}
function ensureExplorerActionSheet(){let sh=document.getElementById('explorerActionSheet');if(sh)return sh;sh=document.createElement('div');sh.id='explorerActionSheet';sh.innerHTML='<div class="explorerSheet"><div class="explorerSheetHead"><strong id="explorerSheetTitle">Ações</strong><button id="explorerSheetClose" aria-label="Fechar">×</button></div><div id="explorerSheetActions"></div></div>';document.body.appendChild(sh);sh.addEventListener('pointerdown',e=>{if(e.target===sh)fecharMenuExplorer()});sh.querySelector('#explorerSheetClose').onclick=fecharMenuExplorer;return sh}
function fecharMenuExplorer(){document.getElementById('explorerActionSheet')?.classList.remove('open')}
function abrirMenuExplorer(titulo,acoes){const sh=ensureExplorerActionSheet(),box=sh.querySelector('#explorerSheetActions');sh.querySelector('#explorerSheetTitle').textContent=titulo;box.innerHTML='';for(const a of acoes){const b=document.createElement('button');b.className='explorerAction'+(a.danger?' danger':'');b.innerHTML='<span class="ico">'+((window.UrbeIcons&&UrbeIcons.fromGlyph(a.ico))||escapeHTML(a.ico||'•'))+'</span><span>'+escapeHTML(a.label)+'</span>';b.onclick=()=>{fecharMenuExplorer();a.run()};box.appendChild(b)}sh.classList.add('open')}
function bindExplorerGesture(el,tap,hold){let tm=null,sx=0,sy=0,moved=false,held=false;const cancel=()=>{clearTimeout(tm);tm=null};el.addEventListener('pointerdown',e=>{if(e.pointerType==='mouse'&&e.button!==0)return;sx=e.clientX;sy=e.clientY;moved=false;held=false;cancel();tm=setTimeout(()=>{held=true;if(navigator.vibrate)navigator.vibrate(16);hold(e)},EXPLORER_HOLD_MS)});el.addEventListener('pointermove',e=>{if(Math.hypot(e.clientX-sx,e.clientY-sy)>EXPLORER_HOLD_MOVE){moved=true;cancel()}});el.addEventListener('pointerup',e=>{cancel();if(!moved&&!held)tap(e)});el.addEventListener('pointercancel',cancel);el.addEventListener('contextmenu',e=>{e.preventDefault();cancel();hold(e)})}
function explorerEntries(regionId){const out=[];for(const b of world.buildings){if(b.tipo==='nota'){if((b.regionId||null)===(regionId||null))out.push({kind:'note',b,name:b.name+'.md'})}else{const fs=b.files||b.anexos||[];if(fs.length){fs.forEach((a,i)=>{const rr=pastaDoAsset(a,b);if((rr?.id||null)===(regionId||null))out.push({kind:'asset',b,a,index:i,name:a.nome||b.fileName||b.name})})}else if((b.regionId||null)===(regionId||null))out.push({kind:'asset',b,a:null,index:0,name:b.fileName||b.name})}}return out.sort((a,b)=>a.name.localeCompare(b.name,undefined,{sensitivity:'base'}))}
function iconeEntrada(e){if(e.kind==='note')return'📄';const c=classificarArquivo(e.name||'');return{image:'🖼',audio:'♫',video:'▶',pdf:'PDF',html:'HTML',zip:'ZIP',other:'FILE'}[c]||'FILE'}
function escolherDestinoRegiao(titulo='Mover para'){return uiDestinoRegiao(titulo)}
function criarNotaNoDestino(regionId){const r=regionId?world.regions.find(x=>x.id===regionId)||null:null,sem=semente('novo'+Date.now()),pos=r?vagaNaRegiao(r,sem,new Set()):vagaAleatoria(sem,3,3);if(!pos)return toast(r?'Sem lote livre nesta pasta/região.':'Não encontrei lote livre próximo.');const b={id:id('b'),kind:'building',regionId:r?r.id:null,x:pos.x,y:pos.y,w:3,h:3,name:'Novo arquivo',description:'',content:'',sprite:'house1',tipo:'nota',tags:[],created:nowDate(),modified:nowDate()};world.buildings.push(b);explorerRegionTarget=r?r.id:null;marcarIndice();indexar();counts();scheduleRoadRebuild();agendarSalvar();marcarSinc();openFullEditor(b);fileSidebar.classList.remove('open')}
async function criarPastaNoDestino(parentId){const nome=((await UD.prompt({title:'Nova pasta',label:'Nome da pasta',value:'Nova pasta',confirm:'Criar pasta'}))||'').trim();if(!nome)return;const parent=parentId?world.regions.find(x=>x.id===parentId)||null:null,r=criarRegiaoOrganica(nome,1,semente(nome+Date.now()),parent?parent.id:null,null,'Pasta criada pelo Explorador.');if(!r)return toast(parent?'Não há área livre dentro desta pasta para a sub-região.':'Não encontrei terra livre para a nova região.');explorerRegionTarget=r.id;marcarIndice();indexar();counts();buildTree();agendarSalvar();marcarSinc();toast('Pasta “'+nome+'” criada.')}
async function renomearNota(b){let n=((await UD.prompt({title:'Renomear nota',label:'Nome',value:b.name,confirm:'Renomear'}))||'').trim();if(!n)return;n=n.replace(/\.md$/i,'');const core=window.UrbeCore,docs=core&&core.service('documents'),doc=docs&&docs.get(b.documentId);if(core&&doc&&core.commands.has('explorer.rename')){const next=core.commands.execute('explorer.rename',{id:doc.id,name:n});b.name=next.title;b.documentId=next.id;b.modified=nowDate()}else{b.name=n;b.modified=nowDate()}buildTree();agendarSalvar();marcarSinc();toast('Nota renomeada.')}
async function renomearAsset(b,i){const a=(b.files||b.anexos||[])[i];if(!a)return;const atual=a.nome||b.fileName||b.name,n=((await UD.prompt({title:'Renomear arquivo',label:'Nome e extensão',value:atual,confirm:'Renomear'}))||'').trim();if(!n||n===atual)return;const oldExt=(atual.match(/\.[^.]+$/)||[''])[0].toLowerCase(),newExt=(n.match(/\.[^.]+$/)||[''])[0].toLowerCase();if(oldExt&&newExt&&oldExt!==newExt){toast('Para preservar o tipo e o preview, mantenha a extensão do arquivo.');return}a.nome=nomeSeguro(n);if((b.files||[]).length===1){b.fileName=a.nome;b.name=limparNome(a.nome);b.fileClass=classificarArquivo(a.nome);b.sprite='file-'+b.fileClass}b.modified=nowDate();buildTree();agendarSalvar();marcarSinc();toast('Arquivo renomeado.')}
async function removerAsset(b,i){const fs=b.files||b.anexos||[],a=fs[i];if(!a)return;if(!(await UD.confirm({title:'Excluir “'+(a.nome||b.name)+'”?',message:'O arquivo será apagado do vault.',confirm:'Excluir',danger:true})))return;if(a.cacheId)DBK.bDel(a.cacheId).catch(()=>{});fs.splice(i,1);b.files=fs;b.anexos=fs;if(b.parentNoteId){const n=world.buildings.find(x=>x.id===b.parentNoteId);if(n)n.anexos=(n.anexos||[]).filter(x=>!(x.cacheId&&x.cacheId===a.cacheId)&&!(x.relPath&&x.relPath===a.relPath)&&x.nome!==a.nome)}if(!fs.length)world.buildings=world.buildings.filter(x=>x!==b);marcarIndice();indexar();counts();buildTree();agendarSalvar();marcarSinc();toast('Arquivo excluído.')}
async function removerNota(b){if(!(await UD.confirm({title:'Mover para a lixeira?',message:'“'+nomeCompletoNota(b)+'” poderá ser restaurada pela Lixeira, em Notas.',confirm:'Mover para a lixeira',danger:true})))return;const core=window.UrbeCore,docs=core&&core.service('documents'),doc=docs&&docs.get(b.documentId);if(core&&doc&&core.commands.has('explorer.delete'))core.commands.execute('explorer.delete',{ids:[doc.id]});world.buildings=world.buildings.filter(x=>x!==b);if(currentFile===b){currentFile=null;editorFull.classList.remove('open')}marcarIndice();indexar();scheduleRoadRebuild();counts();buildTree();agendarSalvar();marcarSinc();toast('Nota movida para a lixeira.')}
async function moverAsset(b,i){const fs=b.files||b.anexos||[],a=fs[i];if(!a)return;const dest=await escolherDestinoRegiao();if(dest===undefined)return;a.folderPath=caminhoRegiaoPorId(dest);if(fs.length===1){const r=dest?world.regions.find(x=>x.id===dest)||null:null,pos=r?vagaNaRegiao(r,semente(a.nome+Date.now()),new Set([b.id])):vagaAleatoria(semente(a.nome+Date.now()),3,3);if(pos){b.regionId=dest||null;b.x=pos.x;b.y=pos.y}}buildTree();agendarSalvar();marcarSinc();toast('Arquivo movido.')}
function descendentesRegiao(r){const ids=new Set([r.id]);let mudou=true;while(mudou){mudou=false;for(const x of world.regions)if(x.parentId&&ids.has(x.parentId)&&!ids.has(x.id)){ids.add(x.id);mudou=true}}return ids}
async function excluirRegiaoExplorer(r){if(!(await UD.confirm({title:'Excluir a pasta “'+r.name+'”?',message:'Todo o conteúdo dentro dela também será excluído.',confirm:'Excluir pasta',danger:true})))return;apagarRegiao(r)}
function apagarRegiao(r,silencioso){const ids=descendentesRegiao(r),paths=caminhosRegioes(),base=paths.get(r.id)||r.name;for(const b of [...world.buildings]){if(b.tipo==='nota'&&ids.has(b.regionId)){world.buildings=world.buildings.filter(x=>x!==b);continue}if(b.tipo!=='nota'){const fs=b.files||b.anexos||[],keep=[];for(const a of fs){const fp=typeof a.folderPath==='string'?a.folderPath:dirDe(a.relPath||'');if(fp===base||fp.startsWith(base+'/')){if(a.cacheId)DBK.bDel(a.cacheId).catch(()=>{})}else keep.push(a)}b.files=keep;b.anexos=keep;if(!keep.length)world.buildings=world.buildings.filter(x=>x!==b)}}world.regions=world.regions.filter(x=>!ids.has(x.id));explorerRegionTarget=null;marcarIndice();indexar();scheduleRoadRebuild();counts();buildTree();agendarSalvar();marcarSinc();pedirDesenho();if(!silencioso)toast('Pasta excluída.')}
function menuRaizExplorer(){abrirMenuExplorer('Raiz',[{ico:'＋',label:'Nova nota',run:()=>criarNotaNoDestino(null)},{ico:'📁',label:'Nova pasta',run:()=>criarPastaNoDestino(null)},{ico:'⇩',label:'Adicionar do dispositivo',run:()=>abrirImportadorUnificado(null)},{ico:'⇧',label:'Exportar vault .zip',run:exportarVault}])}
function menuRegiaoExplorer(r){abrirMenuExplorer(r.name,[{ico:'＋',label:'Nova nota aqui',run:()=>criarNotaNoDestino(r.id)},{ico:'📁',label:'Nova subpasta',run:()=>criarPastaNoDestino(r.id)},{ico:'⇩',label:'Adicionar do dispositivo',run:()=>abrirImportadorUnificado(r.id)},{ico:'✎',label:'Editar nome, cor e descrição',run:()=>openRegionEditDialog(r)},{ico:'🗑',label:'Excluir pasta/região',danger:true,run:()=>excluirRegiaoExplorer(r)}])}
function menuEntradaExplorer(e){if(e.kind==='note')abrirMenuExplorer(e.name,[{ico:'✎',label:'Abrir e editar',run:()=>{openFullEditor(e.b);fileSidebar.classList.remove('open')}},{ico:'Aa',label:'Renomear',run:()=>renomearNota(e.b)},{ico:'↪',label:'Mover',run:async()=>{const d=await escolherDestinoRegiao();if(d!==undefined)moverArquivosParaRegiao([e.b.id],d)}},{ico:'🗑',label:'Excluir',danger:true,run:()=>removerNota(e.b)}]);else abrirMenuExplorer(e.name,[{ico:'◉',label:'Visualizar',run:()=>openFilePreview(e.b,e.index)},{ico:'Aa',label:'Renomear',run:()=>renomearAsset(e.b,e.index)},{ico:'↪',label:'Mover',run:()=>moverAsset(e.b,e.index)},{ico:'🗑',label:'Excluir',danger:true,run:()=>removerAsset(e.b,e.index)}])}
function buildTree(){
  const treeEl=document.getElementById('tree'),paths=caminhosRegioes(),alvo=explorerRegionTarget?world.regions.find(r=>r.id===explorerRegionTarget):null,pathEl=document.getElementById('explorerPath');if(pathEl)pathEl.textContent=alvo?(paths.get(alvo.id)||alvo.name):'/';
  const filhos=id=>world.regions.filter(r=>(r.parentId||null)===(id||null)).sort((a,b)=>a.name.localeCompare(b.name,undefined,{sensitivity:'base'}));
  const entryHtml=(e,d)=>'<div class="treeNode"><button class="treeRow '+(e.kind==='note'&&currentFile===e.b?'active ':'')+'" style="--depth:'+d+'" data-entry="'+e.kind+'" data-building="'+e.b.id+'" data-index="'+(e.index||0)+'" role="treeitem"><span class="treeCaret"></span><span class="treeIcon">'+iconeEntrada(e)+'</span><span class="treeName">'+escapeHTML(e.name)+'</span></button></div>';
  const branch=(r,d)=>{const closed=explorerCollapsed.has(r.id),kids=filhos(r.id),entries=explorerEntries(r.id);return '<div class="treeNode"><button class="treeRow '+(explorerRegionTarget===r.id?'target ':'')+'" style="--depth:'+d+'" data-region="'+r.id+'" role="treeitem" aria-expanded="'+(!closed)+'"><span class="treeCaret '+(!closed?'open':'')+'">›</span><span class="treeIcon">📁</span><span class="treeName">'+escapeHTML(r.name)+'</span><span class="treeMeta">'+(kids.length+entries.length||'')+'</span></button><div class="treeChildren" '+(closed?'hidden':'')+'>'+kids.map(x=>branch(x,d+1)).join('')+entries.map(e=>entryHtml(e,d+1)).join('')+'</div></div>'};
  const topFolders=filhos(null),rootEntries=explorerEntries(null);treeEl.innerHTML=topFolders.map(r=>branch(r,0)).join('')+rootEntries.map(e=>entryHtml(e,0)).join('')+(!topFolders.length&&!rootEntries.length?'<div class="treeEmpty">Vault vazio.<br>Toque e segure aqui para criar ou importar conteúdo.</div>':'');
  treeEl.querySelectorAll('[data-region]').forEach(el=>{const r=world.regions.find(x=>x.id===el.dataset.region);bindExplorerGesture(el,e=>{if(e.target?.classList.contains('treeCaret')){explorerCollapsed.has(r.id)?explorerCollapsed.delete(r.id):explorerCollapsed.add(r.id);buildTree();return}explorerRegionTarget=r.id;if(explorerCollapsed.has(r.id))explorerCollapsed.delete(r.id);buildTree()},()=>menuRegiaoExplorer(r))});
  treeEl.querySelectorAll('[data-entry]').forEach(el=>{const b=world.buildings.find(x=>x.id===el.dataset.building),kind=el.dataset.entry,index=+el.dataset.index||0,e={kind,b,index,a:kind==='asset'?(b?.files||b?.anexos||[])[index]:null,name:el.querySelector('.treeName')?.textContent||''};bindExplorerGesture(el,()=>{if(!b)return;if(kind==='note'){explorerRegionTarget=b.regionId||null;openFullEditor(b);fileSidebar.classList.remove('open')}else{const rr=pastaDoAsset(e.a,b);explorerRegionTarget=rr?.id||null;openFilePreview(b,index)}},()=>menuEntradaExplorer(e))});
  updateExplorerActions();
}
const treeElV18=document.getElementById('tree');bindExplorerGesture(treeElV18,e=>{if(e.target===treeElV18||e.target.classList.contains('treeEmpty'))menuRaizExplorer()},e=>{if(e.target===treeElV18||e.target.classList.contains('treeEmpty'))menuRaizExplorer()});
document.getElementById('explorerRootBtn').onclick=()=>{explorerRegionTarget=null;buildTree()};

/* um único botão de importação; a escolha pasta/arquivo acontece no sheet */
let pendingImportTarget=null;
async function importarEntradasDestino(entradas,ehPasta,nomePasta,target){if(!entradas?.length)return;toast('Lendo arquivos…');try{const itens=await lerEntrada(entradas);if(!itens.length)return toast('Nenhum arquivo aproveitável.');const opts={destinoRegionId:target||null};if(ehPasta)opts.wrapRootName=nomePasta;const r=importarItens(itens,opts);agendarSalvar();marcarSinc();toast(r.notas+' nota(s) · '+r.orfaos+' arquivo(s) · '+r.citados+' citado(s)')}catch(err){console.error(err);toast('Falhou ao importar: '+err.message)}}
async function escolherPastaImportacao(target){pendingImportTarget=target||null;if(typeof window.showDirectoryPicker==='function'){try{const h=await window.showDirectoryPicker({mode:'read'}),itens=await listarHandlePasta(h);await importarEntradasDestino(itens,true,h.name,pendingImportTarget)}catch(e){if(e?.name!=='AbortError'){console.warn(e);toast('Não consegui abrir a pasta.')}}}else document.getElementById('vaultFolderIn').click()}
async function escolherArquivosImportacao(target){pendingImportTarget=target||null;if(typeof window.showOpenFilePicker==='function'){try{const hs=await window.showOpenFilePicker({multiple:true}),entradas=[];for(const h of hs)entradas.push({file:await h.getFile(),rel:h.name});await importarEntradasDestino(entradas,false,'Arquivos',pendingImportTarget)}catch(e){if(e?.name!=='AbortError'){console.warn(e);toast('Não consegui abrir os arquivos.')}}}else document.getElementById('vaultFilesIn').click()}
function abrirImportadorUnificado(target){abrirMenuExplorer(target?(world.regions.find(r=>r.id===target)?.name||'Pasta'):'Adicionar à Raiz',[{ico:'📁',label:'Selecionar uma pasta inteira',run:()=>escolherPastaImportacao(target)},{ico:'📄',label:'Selecionar arquivo(s)',run:()=>escolherArquivosImportacao(target)}])}
document.getElementById('explorerImportFolderBtn').onclick=()=>abrirImportadorUnificado(null);
document.getElementById('vaultFolderIn').onchange=async e=>{const fs=[...e.target.files],nome=nomeRaizEntrada(fs),prefixo=nome+'/';const entradas=fs.map(f=>({file:f,rel:(f.webkitRelativePath||f.name).startsWith(prefixo)?(f.webkitRelativePath||f.name).slice(prefixo.length):(f.webkitRelativePath||f.name)}));await importarEntradasDestino(entradas,true,nome,pendingImportTarget);e.target.value=''};
document.getElementById('vaultFilesIn').onchange=async e=>{await importarEntradasDestino([...e.target.files],false,'Arquivos',pendingImportTarget);e.target.value=''};

/* ---------- ao abrir um vault, indexa também os binários físicos ---------- */
async function garantirRegiaoParaCaminho(cam){if(!cam)return null;const segs=cam.split('/').filter(Boolean);let parent=null,path='';for(const seg of segs){path=path?path+'/'+seg:seg;let r=regiaoPorCaminho(path);if(!r){r=criarRegiaoOrganica(seg,1,semente('disk:'+path),parent?parent.id:null,null,'Pasta encontrada no armazenamento do dispositivo.');if(!r)return parent}parent=r}return parent}
async function indexarBinariosDoDisco(){if(!Disco.cidade)return;const rels=await FS.listar(Disco.cidade),assets=rels.filter(r=>!EXT_MD.test(r)&&!r.startsWith('.urbe/')&&!r.split('/').some(p=>p.startsWith('.'))&&!/\/\.pasta$|^\.pasta$/.test(r));if(!assets.length){snapBin=new Map();return}
  for(const rel of assets){const d=dirDe(rel);if(d)await garantirRegiaoParaCaminho(d)}
  const represented=new Set();for(const b of world.buildings)if(b.tipo!=='nota')for(const a of (b.files||b.anexos||[]))if(a.relPath)represented.add(a.relPath);
  const refs=new Map();for(const n of world.buildings.filter(b=>b.tipo==='nota')){let m;const re=/!?\[\[([^\]|#]+)/g;while((m=re.exec(n.content||''))!==null){const v=m[1].trim().toLowerCase(),bn=baseNome(v),stem=bn.replace(/\.[^.]+$/,'');refs.set(v,n);refs.set(bn,n);refs.set(stem,n)}}
  const grupos=new Map(),orf=[];
  for(const rel of assets){if(represented.has(rel))continue;const blob=await FS.lerBlob(Disco.cidade,rel);if(!blob)continue;const nome=rel.split('/').pop(),a={nome,tipo:(nome.split('.').pop()||'').toLowerCase(),mime:blob.type||mimePorNome(nome),tamanho:blob.size,relPath:rel,folderPath:dirDe(rel)},item={rel,nome:limparNome(rel),anexo:a},bn=baseNome(rel),stem=bn.replace(/\.[^.]+$/,''),dono=refs.get(rel.toLowerCase())||refs.get(bn)||refs.get(stem);if(dono){const c=classificarArquivo(rel),k=dono.id+'|'+c;if(!grupos.has(k))grupos.set(k,{dono,c,items:[]});grupos.get(k).items.push(item)}else orf.push(item)}
  for(const g of grupos.values()){const r=g.dono.regionId?world.regions.find(x=>x.id===g.dono.regionId)||null:null,pos=posicaoAdjacenteArquivo(g.dono,r,g.c+g.dono.id)|| (r?vagaNaRegiao(r,semente(g.dono.id+g.c),new Set()):vagaAleatoria(semente(g.dono.id+g.c)));if(!pos)continue;const files=g.items.map(i=>i.anexo);criarPredioArquivo(g.items[0],pos.x,pos.y,r?r.id:null,{aux:true,fileClass:g.c,parentNoteId:g.dono.id,files,name:files.length===1?files[0].nome:(rotuloClasse(g.c)+' ('+files.length+') · '+g.dono.name),description:files.length+' arquivo(s) do vault'});g.dono.anexos=(g.dono.anexos||[]).concat(files)}
  for(const it of orf){const r=regiaoPorCaminho(dirDe(it.rel)),pos=r?vagaNaRegiao(r,semente(it.rel),new Set()):vagaAleatoria(semente(it.rel));if(pos)criarPredioArquivo(it,pos.x,pos.y,r?r.id:null)}
  marcarIndice();indexar();counts();buildTree();pedirDesenho();snapBin=new Map();for(const b of world.buildings)if(b.tipo!=='nota')for(const a of (b.files||b.anexos||[]))if(a.relPath)snapBin.set(a.relPath,assinaturaBin(a,a.relPath));snapPastas=new Set([...caminhosRegioes().values()].filter(Boolean));agendarSalvar();
}
const _abrirCidadeBase=abrirCidade;
abrirCidade=async function(nome){await _abrirCidadeBase(nome);await indexarBinariosDoDisco();marcarSinc()};


/* v0.19 patch é inserido aqui pelo build */

/* ============================================================
   v0.19 final — gestos coerentes, regiões absorventes,
   identidade de arquivo, PDF inline e PWA
   ============================================================ */

/* ---------- identidade do arquivo Markdown ---------- */
function extNota(b){
  let ext=String(b?.ext||'.md').trim().toLowerCase();
  if(!window.UrbeArtifacts.RE.noteExt.test(ext))ext='.md';
  return ext;
}
function nomeCompletoNota(b){return (b?.name||'arquivo')+extNota(b)}
function separarNomeExtNota(valor,atual){
  let v=String(valor||'').trim().replace(/[\\/]/g,'-');
  if(!v)return null;
  const m=v.match(/^(.*?)(\.(?:md|markdown|txt))$/i);
  if(m&&m[1].trim())return {name:nomeSeguro(m[1].trim()),ext:m[2].toLowerCase()};
  return {name:nomeSeguro(v.replace(/\.[^.]+$/,'')),ext:extNota(atual)};
}
function aplicarNomeCompletoNota(b,valor){
  const n=separarNomeExtNota(valor,b);if(!n)return false;
  b.name=n.name;b.ext=n.ext;b.modified=nowDate();
  if(currentFile===b){fileNameDisplay.textContent=nomeCompletoNota(b);document.getElementById('documentWatermark').textContent=nomeCompletoNota(b)}
  buildTree();scheduleRoadRebuild();agendarSalvar();marcarSinc();pedirDesenho();return true;
}
renomearNota=async function(b){const v=await UD.prompt({title:'Renomear',label:'Nome e extensão',value:nomeCompletoNota(b),confirm:'Renomear'});if(v!==null&&aplicarNomeCompletoNota(b,v))toast('Arquivo renomeado.')};
renomearAsset=async function(b,i){
  const a=(b.files||b.anexos||[])[i];if(!a)return;
  const atual=a.nome||b.fileName||b.name,n=((await UD.prompt({title:'Renomear arquivo',label:'Nome e extensão',value:atual,confirm:'Renomear'}))||'').trim();if(!n||n===atual)return;
  a.nome=nomeSeguro(n);a.tipo=(a.nome.split('.').pop()||'').toLowerCase();a.mime=mimePorNome(a.nome);
  const fs=b.files||b.anexos||[];
  if(fs.length===1){b.fileName=a.nome;b.name=limparNome(a.nome);b.fileClass=classificarArquivo(a.nome);b.sprite='file-'+b.fileClass}
  b.modified=nowDate();buildTree();agendarSalvar();marcarSinc();pedirDesenho();toast('Arquivo renomeado.');
};

/* Importações novas preservam a extensão original da nota. */
criarCasa=function(arq,x,y,regId){
  const em=String(arq.rel||'').match(/(\.(?:md|markdown|txt))$/i);
  const b={id:id('b'),kind:'building',regionId:regId||null,x,y,w:3,h:3,name:arq.nome,ext:em?em[1].toLowerCase():'.md',description:'',content:arq.texto||'',sprite:['house1','house2','house3'][semente(arq.rel)%3],tipo:'nota',tags:[],anexos:[],created:nowDate(),modified:nowDate()};
  world.buildings.push(b);indexarUm(idxB,b);return b;
};

/* O nome no topo é a identidade do documento, não conteúdo Markdown. */
const fileNameDisplay=document.getElementById('fileNameDisplay');
let editandoNomeEditor=false,nomeEditorAntes='';
function iniciarEdicaoNomeEditor(){
  if(!currentFile||editandoNomeEditor)return;editandoNomeEditor=true;nomeEditorAntes=nomeCompletoNota(currentFile);
  fileNameDisplay.contentEditable='true';fileNameDisplay.setAttribute('role','textbox');fileNameDisplay.focus();
  const sel=getSelection(),r=document.createRange();r.selectNodeContents(fileNameDisplay);sel.removeAllRanges();sel.addRange(r);
}
function finalizarEdicaoNomeEditor(cancelar=false){
  if(!editandoNomeEditor)return;editandoNomeEditor=false;fileNameDisplay.contentEditable='false';fileNameDisplay.setAttribute('role','button');
  if(cancelar||!currentFile){fileNameDisplay.textContent=currentFile?nomeCompletoNota(currentFile):nomeEditorAntes;return}
  const v=fileNameDisplay.textContent.trim();if(!aplicarNomeCompletoNota(currentFile,v))fileNameDisplay.textContent=nomeEditorAntes;
}
fileNameDisplay.onclick=e=>{e.stopPropagation();iniciarEdicaoNomeEditor()};
fileNameDisplay.onkeydown=e=>{if(e.key==='Enter'){e.preventDefault();finalizarEdicaoNomeEditor(false)}else if(e.key==='Escape'){e.preventDefault();finalizarEdicaoNomeEditor(true)}};
fileNameDisplay.onblur=()=>finalizarEdicaoNomeEditor(false);

const _loadFileV19=loadFile;
loadFile=function(b){if(!b.ext)b.ext='.md';_loadFileV19(b);fileNameDisplay.textContent=nomeCompletoNota(b);document.getElementById('documentWatermark').textContent=nomeCompletoNota(b)};

/* Evita voltar a inserir "# Nome" automaticamente ao criar nota. */
document.getElementById('newFileBtn').onclick=()=>criarNotaNoDestino(destinoCriacaoExplorer()||null);

/* ---------- diálogo de região sem ghost-click ---------- */
function protegerDialogRegiao(){
  dlg.classList.add('regionInputGuard');
  clearTimeout(protegerDialogRegiao._t);
  protegerDialogRegiao._t=setTimeout(()=>dlg.classList.remove('regionInputGuard'),260);
}
openRegionDialog=function(bounds){
  regionEditTarget=null;pendingRegion=bounds;document.getElementById('regionDialogTitle').textContent='Nova região';document.getElementById('regionName').value='Nova região';document.getElementById('regionDesc').value='Área temática do workspace.';document.getElementById('regionColor').value=colors[world.regions.length%colors.length]||'#4db7ff';document.getElementById('confirmRegion').textContent='Criar região';
  protegerDialogRegiao();dlg.classList.add('open');setTimeout(()=>document.getElementById('regionName').focus(),285);
};
openRegionEditDialog=function(r){
  if(!r)return;regionEditTarget=r;pendingRegion=null;document.getElementById('regionDialogTitle').textContent='Editar região';document.getElementById('regionName').value=r.name||'';document.getElementById('regionDesc').value=r.description||'';document.getElementById('regionColor').value=/^#[0-9a-f]{6}$/i.test(r.color||'')?r.color:'#4db7ff';document.getElementById('confirmRegion').textContent='Salvar alterações';
  protegerDialogRegiao();dlg.classList.add('open');
};

/* ---------- região pode absorver construções que ainda estão na raiz ---------- */
tileReservadoPorLote=function(x,y,adotarRaiz=false){
  for(const b of world.buildings){
    if(adotarRaiz&&!b.regionId)continue;
    if(x>=b.x-LOTE_GAP&&x<b.x+b.w+LOTE_GAP&&y>=b.y-LOTE_GAP&&y<b.y+b.h+LOTE_GAP)return true;
  }
  return false;
};
tilePermitidoNovaRegiao=function(x,y,parent=null,ignorarRegiaoId=null){
  if(ehAgua(x,y))return false;
  const atual=regAt({x,y});
  if(parent){
    if(!regionHasTile(parent,x,y))return false;
    if(atual&&atual.id!==parent.id&&atual.id!==ignorarRegiaoId)return false;
    /* arquivos já pertencentes à pasta pai continuam sendo obstáculos para subpastas */
    if(tileReservadoPorLote(x,y,false))return false;
    return true;
  }
  if(atual&&atual.id!==ignorarRegiaoId)return false;
  /* no topo, lotes da raiz podem ser incorporados pela nova região */
  return !tileReservadoPorLote(x,y,true);
};
function caminhoDestinoRegiao(r){return r?(caminhosRegioes().get(r.id)||''):''}
function atualizarPastaFisicaConstrucao(b,r){
  const dir=caminhoDestinoRegiao(r);b.regionId=r?.id||null;b.modified=nowDate();
  if(b.tipo!=='nota')for(const a of (b.files||b.anexos||[]))a.folderPath=dir;
}
function construcaoTocaRegiao(b,r){for(let y=b.y;y<b.y+b.h;y++)for(let x=b.x;x<b.x+b.w;x++)if(regionHasTile(r,x,y))return true;return false}
function absorverConstrucoesDaRaiz(r){
  let n=0;for(const b of world.buildings){if(b.regionId)continue;if(!construcaoTocaRegiao(b,r))continue;atualizarPastaFisicaConstrucao(b,r);n++}return n;
}

document.getElementById('confirmRegion').onclick=()=>{
  if(dlg.classList.contains('regionInputGuard'))return;
  const nome=(document.getElementById('regionName').value||'Região').trim()||'Região',desc=document.getElementById('regionDesc').value||'',cor=document.getElementById('regionColor').value||'#4db7ff';
  if(regionEditTarget){
    const camAntes=caminhosRegioes().get(regionEditTarget.id)||'';regionEditTarget.name=nome;regionEditTarget.description=desc;regionEditTarget.color=cor;const camDepois=caminhosRegioes().get(regionEditTarget.id)||'';atualizarFolderPathAoRenomear(camAntes,camDepois);dlg.classList.remove('open');const editada=regionEditTarget;regionEditTarget=null;marcarIndice();indexar();buildTree();pedirDesenho();agendarSalvar();marcarSinc();toast('Região “'+editada.name+'” atualizada.');return;
  }
  if(!pendingRegion)return;const m=mascaraRetanguloRegiao(pendingRegion,null);if(!m.cells.length){toast('A área está ocupada por água, outra região ou lotes já pertencentes a outras pastas.');return}
  const pts=m.cells.map(k=>k.split(',').map(Number)),xs=pts.map(p=>p[0]),ys=pts.map(p=>p[1]);
  const r={id:id('r'),kind:'region',x:Math.min(...xs),y:Math.min(...ys),w:Math.max(...xs)-Math.min(...xs)+1,h:Math.max(...ys)-Math.min(...ys)+1,cells:m.cells,name:nome,description:desc,color:cor,parentId:null};r._cellSet=new Set(r.cells);world.regions.push(r);explorerRegionTarget=r.id;marcarIndice();indexar();
  const adotadas=absorverConstrucoesDaRaiz(r);dlg.classList.remove('open');pendingRegion=null;counts();buildTree();agendarSalvar();marcarSinc();scheduleRoadRebuild();pedirDesenho();
  toast('Região criada'+(adotadas?' · '+adotadas+' item(ns) da raiz incorporado(s).':'.'));setTool('select');
};

/* ---------- arrastar construções no mundo ---------- */
function regiaoDestinoLote(x,y,w=3,h=3){return regAt({x:x+Math.floor(w/2),y:y+Math.floor(h/2)})}
function validarPosicaoMovel(b,x,y){
  const ign=new Set([b.id]),r=regiaoDestinoLote(x,y,b.w,b.h);
  if(r)return loteValido(x,y,ign,r)?{ok:true,r}: {ok:false,r};
  return loteForaDeRegioes(x,y,b.w,b.h)&&loteValido(x,y,ign,null)?{ok:true,r:null}:{ok:false,r:null};
}
const _drawOverlayV19=drawOverlay;
drawOverlay=function(){
  _drawOverlayV19();
  if(gesture?.mode==='moveBuilding'&&gesture.dragPos&&gesture.building){
    const b=gesture.building,p=gesture.dragPos,v=validarPosicaoMovel(b,p.x,p.y),sp=w2s(p.x*TILE,p.y*TILE);
    ctx.save();ctx.globalAlpha=.58;if(b.tipo==='nota')drawSprite(A[b.sprite]||A.house1,p.x,p.y,b.w,b.h,1.18);else drawArquivoBuilding({...b,x:p.x,y:p.y});ctx.globalAlpha=1;ctx.strokeStyle=v.ok?'#75ff88':'#ff6767';ctx.lineWidth=3;ctx.strokeRect(sp.x,sp.y,b.w*TILE*camera.z,b.h*TILE*camera.z);ctx.restore();
  }
};
function finalizarMovimentoMundo(b,p){
  const v=validarPosicaoMovel(b,p.x,p.y);if(!v.ok){toast('Lote inválido para mover a construção.');return false}
  b.x=p.x;b.y=p.y;atualizarPastaFisicaConstrucao(b,v.r);marcarIndice();indexar();scheduleRoadRebuild();buildTree();counts();agendarSalvar();marcarSinc();pedirDesenho();toast(v.r?'Movido para '+v.r.name+'.':'Movido para a raiz.');return true;
}

/* captura os gestos antes dos handlers legados do canvas */
function canvasDownV19(e){
  e.stopImmediatePropagation();try{cv.setPointerCapture(e.pointerId)}catch(_){}pointers.set(e.pointerId,{x:e.clientX,y:e.clientY});
  if(pointers.size===1){const t=tileFromClient(e.clientX,e.clientY),hit=tool==='select'?bAt(t):null;gesture={mode:tool==='select'?'selectCandidate':tool,start:t,sx:e.clientX,sy:e.clientY,cx:camera.x,cy:camera.y,moved:false,building:hit,offX:hit?t.x-hit.x:0,offY:hit?t.y-hit.y:0};if(tool==='region')regionDraft={a:t,b:t}}
  else if(pointers.size===2){const ps=[...pointers.values()];gesture={mode:'pinch',dist:Math.hypot(ps[0].x-ps[1].x,ps[0].y-ps[1].y),z:camera.z}}
  pedirDesenho();
}
function canvasMoveV19(e){
  e.stopImmediatePropagation();if(!pointers.has(e.pointerId))return;const pp=pointers.get(e.pointerId);pp.x=e.clientX;pp.y=e.clientY;
  if(pointers.size===2&&gesture?.mode==='pinch'){const ps=[...pointers.values()],d=Math.hypot(ps[0].x-ps[1].x,ps[0].y-ps[1].y);camera.z=clamp(gesture.z*d/gesture.dist,.5,2.8);counts();pedirDesenho();return}
  if(pointers.size!==1||!gesture)return;const t=tileFromClient(e.clientX,e.clientY),dx=e.clientX-gesture.sx,dy=e.clientY-gesture.sy;if(Math.abs(dx)+Math.abs(dy)>7)gesture.moved=true;
  if(gesture.mode==='selectCandidate'&&gesture.moved){gesture.mode=gesture.building?'moveBuilding':'pan'}
  if(gesture.mode==='pan'){camera.x=gesture.cx-dx/camera.z;camera.y=gesture.cy-dy/camera.z}
  else if(gesture.mode==='moveBuilding'){gesture.dragPos={x:t.x-gesture.offX,y:t.y-gesture.offY}}
  else if(gesture.mode==='region'&&regionDraft)regionDraft.b=t;else if(gesture.mode==='house')ghost=t;pedirDesenho();
}
function canvasUpV19(e){
  e.stopImmediatePropagation();pointers.delete(e.pointerId);if(!gesture||gesture.mode==='pinch'){if(!pointers.size)gesture=null;pedirDesenho();return}
  const t=tileFromClient(e.clientX,e.clientY),g=gesture;
  if(g.mode==='moveBuilding'&&g.dragPos&&g.building){finalizarMovimentoMundo(g.building,g.dragPos)}
  else if(g.mode==='pan'){}
  else if(g.mode==='selectCandidate'&&!g.moved){const b=bAt(t),rSel=regAt(t);selected=b||rSel||null;if(b){if(b.tipo==='nota')openHouseSummary(b);else openFilePreview(b)}else if(rSel){closeHouseSummary();closeFilePreview();openRegionEditDialog(rSel)}else{closeHouseSummary();closeFilePreview()}}
  else if(g.mode==='region'&&regionDraft){const a=regionDraft.a,b=regionDraft.b;regionDraft=null;const x=Math.min(a.x,b.x),y=Math.min(a.y,b.y),w=Math.abs(a.x-b.x)+1,h=Math.abs(a.y-b.y)+1;if(w>=3&&h>=3)openRegionDialog({x,y,w,h});else toast('A região precisa ser maior.')}
  else if(g.mode==='house'&&!g.moved){if(canHouse(t)){const r=regAt(t),n=world.buildings.length%3+1,b={id:id('b'),kind:'building',regionId:r?r.id:null,x:t.x,y:t.y,w:3,h:3,name:'Nova nota',ext:'.md',description:'',content:'',sprite:'house'+n,tipo:'nota',tags:[],created:nowDate(),modified:nowDate()};world.buildings.push(b);selected=b;marcarIndice();indexar();openHouseSummary(b);counts();scheduleRoadRebuild();agendarSalvar();marcarSinc();toast('Nota criada'+(r?' em '+r.name:' na raiz')+'.')}else toast('Lote inválido: precisa de 3x3 seco, acesso livre e folga das vizinhas.')}
  gesture=null;pedirDesenho();
}
function canvasCancelV19(e){e.stopImmediatePropagation();pointers.delete(e.pointerId);gesture=null;regionDraft=null;pedirDesenho()}
cv.addEventListener('pointerdown',canvasDownV19,true);cv.addEventListener('pointermove',canvasMoveV19,true);cv.addEventListener('pointerup',canvasUpV19,true);cv.addEventListener('pointercancel',canvasCancelV19,true);

/* ---------- árvore: toque abre, arraste move, hold abre menu, duplo toque renomeia ---------- */
let treeGesture=null,treeDrag=null,treeTapTimer=null,lastTreeTap={key:'',t:0};
const TREE_HOLD=520,TREE_MOVE=10,TREE_DOUBLE=300;
function entradaPorEl(el){const b=world.buildings.find(x=>x.id===el.dataset.building),kind=el.dataset.entry,index=+el.dataset.index||0;return {kind,b,index,a:kind==='asset'?(b?.files||b?.anexos||[])[index]:null,name:el.querySelector('.treeName')?.textContent||''}}
function moverEntradaPara(e,dest){
  const r=dest?world.regions.find(x=>x.id===dest)||null:null;
  if(e.kind==='note'){const pos=r?vagaNaRegiao(r,semente(e.b.id+Date.now()),new Set([e.b.id])):vagaAleatoria(semente(e.b.id+Date.now()),3,3);if(!pos)return toast('Sem espaço construível no destino.');e.b.x=pos.x;e.b.y=pos.y;atualizarPastaFisicaConstrucao(e.b,r)}
  else{const fs=e.b.files||e.b.anexos||[],a=fs[e.index];if(!a)return;a.folderPath=caminhoDestinoRegiao(r);if(fs.length===1){const pos=r?vagaNaRegiao(r,semente(e.b.id+Date.now()),new Set([e.b.id])):vagaAleatoria(semente(e.b.id+Date.now()),3,3);if(pos){e.b.x=pos.x;e.b.y=pos.y}atualizarPastaFisicaConstrucao(e.b,r)}else if(fs.every(x=>(x.folderPath||'')===a.folderPath)){e.b.regionId=r?.id||null}}
  marcarIndice();indexar();buildTree();counts();agendarSalvar();marcarSinc();pedirDesenho();
}
function alvoDropTree(x,y){
  const sob=document.elementFromPoint(x,y),reg=sob?.closest?.('[data-region]');if(reg)return reg.dataset.region;
  if(sob?.closest?.('#tree')){const ent=sob.closest('[data-entry]');if(ent)return ent.dataset.parentRegion||null;return null}
  return undefined;
}
function limparDropTree(){document.querySelectorAll('#tree .dropTarget').forEach(x=>x.classList.remove('dropTarget'));document.getElementById('tree').classList.remove('rootDropTarget')}
function atualizarTreeDrag(x,y){
  if(!treeDrag)return;treeDrag.ghost.style.left=x+'px';treeDrag.ghost.style.top=y+'px';limparDropTree();const alvo=alvoDropTree(x,y);treeDrag.alvo=alvo;const sob=document.elementFromPoint(x,y),reg=sob?.closest?.('[data-region]');if(reg)reg.classList.add('dropTarget');else if(alvo===null)document.getElementById('tree').classList.add('rootDropTarget');const rc=tree.getBoundingClientRect();if(y<rc.top+52)tree.scrollTop-=14;else if(y>rc.bottom-52)tree.scrollTop+=14;
}
function iniciarTreeDrag(entry,x,y){
  const ghost=document.createElement('div');ghost.id='treeDragGhost';ghost.textContent='↪ '+entry.name;document.body.appendChild(ghost);treeDrag={entry,ghost,alvo:undefined};atualizarTreeDrag(x,y)
}
function finalizarTreeDrag(){if(!treeDrag)return;const d=treeDrag;treeDrag=null;d.ghost.remove();limparDropTree();if(d.alvo!==undefined){moverEntradaPara(d.entry,d.alvo);toast('Movido para '+(d.alvo?(world.regions.find(r=>r.id===d.alvo)?.name||'pasta'):'raiz')+'.')}else buildTree()}
function abrirEntradaTree(e){if(!e.b)return;if(e.kind==='note'){explorerRegionTarget=e.b.regionId||null;openFullEditor(e.b);fileSidebar.classList.remove('open')}else{const rr=pastaDoAsset(e.a,e.b);explorerRegionTarget=rr?.id||null;openFilePreview(e.b,e.index)}}
function renomearEntradaTree(e){e.kind==='note'?renomearNota(e.b):renomearAsset(e.b,e.index)}
function bindTreeEntryV19(el,entry){
  const key=entry.kind+':'+entry.b?.id+':'+entry.index;
  el.addEventListener('pointerdown',ev=>{if(ev.pointerType==='mouse'&&ev.button!==0)return;try{el.setPointerCapture(ev.pointerId)}catch(_){}clearTimeout(treeGesture?.hold);treeGesture={el,entry,key,id:ev.pointerId,x:ev.clientX,y:ev.clientY,moved:false,held:false,hold:setTimeout(()=>{if(!treeGesture||treeGesture.id!==ev.pointerId||treeGesture.moved)return;treeGesture.held=true;if(navigator.vibrate)navigator.vibrate(14);menuEntradaExplorer(entry)},TREE_HOLD)}});
  el.addEventListener('pointermove',ev=>{if(!treeGesture||treeGesture.id!==ev.pointerId)return;const dist=Math.hypot(ev.clientX-treeGesture.x,ev.clientY-treeGesture.y);if(dist>TREE_MOVE&&!treeGesture.held){treeGesture.moved=true;clearTimeout(treeGesture.hold);if(!treeDrag)iniciarTreeDrag(entry,ev.clientX,ev.clientY);atualizarTreeDrag(ev.clientX,ev.clientY);ev.preventDefault()}});
  el.addEventListener('pointerup',ev=>{if(!treeGesture||treeGesture.id!==ev.pointerId)return;clearTimeout(treeGesture.hold);const st=treeGesture;treeGesture=null;if(treeDrag){finalizarTreeDrag();return}if(st.held||st.moved)return;const now=Date.now();if(lastTreeTap.key===key&&now-lastTreeTap.t<TREE_DOUBLE){clearTimeout(treeTapTimer);treeTapTimer=null;lastTreeTap={key:'',t:0};renomearEntradaTree(entry);return}lastTreeTap={key,t:now};clearTimeout(treeTapTimer);treeTapTimer=setTimeout(()=>{if(lastTreeTap.key===key){lastTreeTap={key:'',t:0};abrirEntradaTree(entry)}},215)});
  el.addEventListener('pointercancel',()=>{if(treeGesture){clearTimeout(treeGesture.hold);treeGesture=null}if(treeDrag){treeDrag.ghost.remove();treeDrag=null;limparDropTree()}});
  el.addEventListener('contextmenu',ev=>{ev.preventDefault();menuEntradaExplorer(entry)});
  el.addEventListener('keydown',ev=>{if(ev.key==='Enter'||ev.key===' '){ev.preventDefault();abrirEntradaTree(entry)}else if(ev.key==='F2'){ev.preventDefault();renomearEntradaTree(entry)}});
}
function bindTreeRegionV19(el,r){
  let st=null;el.addEventListener('pointerdown',ev=>{if(ev.pointerType==='mouse'&&ev.button!==0)return;st={id:ev.pointerId,x:ev.clientX,y:ev.clientY,moved:false,held:false,t:setTimeout(()=>{if(!st||st.id!==ev.pointerId||st.moved)return;st.held=true;if(navigator.vibrate)navigator.vibrate(14);menuRegiaoExplorer(r)},TREE_HOLD)}});
  el.addEventListener('pointermove',ev=>{if(st&&st.id===ev.pointerId&&Math.hypot(ev.clientX-st.x,ev.clientY-st.y)>TREE_MOVE){st.moved=true;clearTimeout(st.t)}});
  el.addEventListener('pointerup',ev=>{if(!st||st.id!==ev.pointerId)return;clearTimeout(st.t);const x=st;st=null;if(x.held||x.moved)return;if(ev.target?.classList.contains('treeCaret'))explorerCollapsed.has(r.id)?explorerCollapsed.delete(r.id):explorerCollapsed.add(r.id);else{explorerRegionTarget=r.id;if(explorerCollapsed.has(r.id))explorerCollapsed.delete(r.id)}buildTree()});
  el.addEventListener('contextmenu',ev=>{ev.preventDefault();menuRegiaoExplorer(r)});
}
menuRaizExplorer=function(){abrirMenuExplorer('Raiz',[{ico:'＋',label:'Nova nota',run:()=>criarNotaNoDestino(null)},{ico:'📁',label:'Nova pasta',run:()=>criarPastaNoDestino(null)},{ico:'⇧',label:'Exportar vault .zip',run:exportarVault}])};
menuRegiaoExplorer=function(r){abrirMenuExplorer(r.name,[{ico:'＋',label:'Nova nota aqui',run:()=>criarNotaNoDestino(r.id)},{ico:'📁',label:'Nova subpasta',run:()=>criarPastaNoDestino(r.id)},{ico:'✎',label:'Editar nome, cor e descrição',run:()=>openRegionEditDialog(r)},{ico:'🗑',label:'Excluir pasta/região',danger:true,run:()=>excluirRegiaoExplorer(r)}])};

buildTree=function(){
  const treeEl=document.getElementById('tree'),paths=caminhosRegioes(),alvo=explorerRegionTarget?world.regions.find(r=>r.id===explorerRegionTarget):null,pathEl=document.getElementById('explorerPath');if(pathEl)pathEl.textContent=alvo?(paths.get(alvo.id)||alvo.name):'/';
  const filhos=id=>world.regions.filter(r=>(r.parentId||null)===(id||null)).sort((a,b)=>a.name.localeCompare(b.name,undefined,{sensitivity:'base'}));
  const entryHtml=(e,d,parent)=>'<div class="treeNode"><button class="treeRow '+(e.kind==='note'&&currentFile===e.b?'active ':'')+'" style="--depth:'+d+'" data-entry="'+e.kind+'" data-building="'+e.b.id+'" data-index="'+(e.index||0)+'" data-parent-region="'+(parent||'')+'" role="treeitem"><span class="treeCaret"></span><span class="treeIcon">'+iconeEntrada(e)+'</span><span class="treeName">'+escapeHTML(e.name)+'</span></button></div>';
  const branch=(r,d)=>{const closed=explorerCollapsed.has(r.id),kids=filhos(r.id),entries=explorerEntries(r.id);return '<div class="treeNode"><button class="treeRow '+(explorerRegionTarget===r.id?'target ':'')+'" style="--depth:'+d+'" data-region="'+r.id+'" role="treeitem" aria-expanded="'+(!closed)+'"><span class="treeCaret '+(!closed?'open':'')+'">›</span><span class="treeIcon">📁</span><span class="treeName">'+escapeHTML(r.name)+'</span><span class="treeMeta">'+(kids.length+entries.length||'')+'</span></button><div class="treeChildren" '+(closed?'hidden':'')+'>'+kids.map(x=>branch(x,d+1)).join('')+entries.map(e=>entryHtml(e,d+1,r.id)).join('')+'</div></div>'};
  const top=filhos(null),root=explorerEntries(null);treeEl.innerHTML=top.map(r=>branch(r,0)).join('')+root.map(e=>entryHtml(e,0,'')).join('')+(!top.length&&!root.length?'<div class="treeEmpty">Vault vazio.<br>Segure aqui para criar conteúdo ou use ⇩ para importar.</div>':'');
  treeEl.querySelectorAll('[data-region]').forEach(el=>{const r=world.regions.find(x=>x.id===el.dataset.region);if(r)bindTreeRegionV19(el,r)});
  treeEl.querySelectorAll('[data-entry]').forEach(el=>{const e=entradaPorEl(el);if(e.b)bindTreeEntryV19(el,e)});updateExplorerActions();
};
const treeRoot=document.getElementById('tree');
treeRoot.addEventListener('contextmenu',e=>{if(e.target===treeRoot||e.target.classList.contains('treeEmpty')){e.preventDefault();menuRaizExplorer()}});
let rootHold=null;treeRoot.addEventListener('pointerdown',e=>{if(!(e.target===treeRoot||e.target.classList.contains('treeEmpty')))return;rootHold=setTimeout(()=>menuRaizExplorer(),TREE_HOLD)});treeRoot.addEventListener('pointerup',()=>{clearTimeout(rootHold)});treeRoot.addEventListener('pointermove',()=>{clearTimeout(rootHold)});

/* ---------- importação: um ícone, nenhuma escolha intermediária no Urbe ---------- */
pendingImportTarget=null;
async function importarHandlesArquivos(handles,target){const entradas=[];for(const h of handles)if(h?.kind==='file')entradas.push({file:await h.getFile(),rel:h.name});await importarEntradasDestino(entradas,false,'Arquivos',target)}
async function abrirImportacaoSistema(target){
  pendingImportTarget=target||null;
  /* A Web File System API não possui um picker único arquivo-ou-diretório.
     Mantemos uma única ação no Urbe e delegamos ao seletor de arquivos do SO.
     Diretórios recebidos por drag/drop são percorridos recursivamente abaixo. */
  if(typeof window.showOpenFilePicker==='function'){
    try{const hs=await window.showOpenFilePicker({multiple:true});await importarHandlesArquivos(hs,pendingImportTarget)}catch(e){if(e?.name!=='AbortError'){console.warn(e);toast('Não consegui abrir o armazenamento.')}}return;
  }
  document.getElementById('vaultFilesIn').click();
}
document.getElementById('explorerImportFolderBtn').onclick=()=>abrirImportacaoSistema(explorerRegionTarget||null);
document.getElementById('vaultFilesIn').onchange=async e=>{await importarEntradasDestino([...e.target.files],false,'Arquivos',pendingImportTarget);e.target.value=''};
/* o input de diretório permanece oculto somente para compatibilidade de vaults antigos */

/* Pastas/arquivos soltos no Explorer usam o mesmo pipeline e preservam caminhos. */
async function entradasDeDataTransfer(items){
  const out=[];
  async function walk(h,prefix=''){
    if(h.kind==='file'){const f=await h.getFile();out.push({file:f,rel:prefix+h.name});return}
    if(h.kind==='directory')for await(const child of h.values())await walk(child,prefix+h.name+'/');
  }
  for(const it of items){if(it.getAsFileSystemHandle){const h=await it.getAsFileSystemHandle();if(h)await walk(h)}else{const f=it.getAsFile?.();if(f)out.push({file:f,rel:f.name})}}
  return out;
}
fileSidebar.addEventListener('dragover',e=>{e.preventDefault()});
fileSidebar.addEventListener('drop',async e=>{e.preventDefault();const entradas=await entradasDeDataTransfer([...e.dataTransfer.items]);if(!entradas.length)return;const top=entradas[0].rel.split('/')[0],temPasta=entradas.some(x=>x.rel.includes('/'));await importarEntradasDestino(entradas,temPasta,top,explorerRegionTarget||null)});

/* ---------- PDF inline com PDF.js ---------- */
const PDFJS_URL='https://cdn.jsdelivr.net/npm/pdfjs-dist@6.3.289/build/pdf.min.mjs';
const PDFJS_WORKER_URL='https://cdn.jsdelivr.net/npm/pdfjs-dist@6.3.289/build/pdf.worker.min.mjs';
let pdfJsPromise=null,pdfRenderToken=0;
async function obterPdfJs(){if(!pdfJsPromise)pdfJsPromise=import(PDFJS_URL).then(m=>{m.GlobalWorkerOptions.workerSrc=PDFJS_WORKER_URL;return m});return pdfJsPromise}
async function renderPdfInline(blob){
  const token=++pdfRenderToken;filePreviewBody.innerHTML='<div class="previewEmpty">Renderizando PDF…</div>';
  try{const pdfjs=await obterPdfJs(),data=new Uint8Array(await blob.arrayBuffer()),doc=await pdfjs.getDocument({data}).promise;if(token!==pdfRenderToken)return;const wrap=document.createElement('div');wrap.className='pdfPages';filePreviewBody.replaceChildren(wrap);let pagina=1;
    const renderLote=async()=>{const fim=Math.min(doc.numPages,pagina+3);for(;pagina<=fim;pagina++){const p=await doc.getPage(pagina),base=p.getViewport({scale:1}),maxW=Math.min(1000,Math.max(280,filePreviewBody.clientWidth-24)),scale=Math.max(.55,Math.min(2,maxW/base.width)),vp=p.getViewport({scale}),c=document.createElement('canvas'),dpr=Math.min(2,devicePixelRatio||1);c.className='pdfPage';c.width=Math.floor(vp.width*dpr);c.height=Math.floor(vp.height*dpr);c.style.width=Math.floor(vp.width)+'px';c.style.height=Math.floor(vp.height)+'px';wrap.appendChild(c);await p.render({canvasContext:c.getContext('2d'),viewport:vp,transform:dpr!==1?[dpr,0,0,dpr,0,0]:null}).promise;if(token!==pdfRenderToken)return}
      const old=wrap.querySelector('.pdfLoadMore');if(old)old.remove();if(pagina<=doc.numPages){const bt=document.createElement('button');bt.className='btn pdfLoadMore';bt.textContent='Renderizar mais páginas ('+(doc.numPages-pagina+1)+')';bt.onclick=renderLote;wrap.appendChild(bt)}};await renderLote();
  }catch(err){console.warn('PDF.js',err);const fr=document.createElement('iframe');fr.className='pdfFrame';fr.src=previewObjectUrl;fr.title='PDF';filePreviewBody.replaceChildren(fr);toast('Renderer PDF indisponível; usando visualizador do navegador.')}
}
const _renderFilePreviewV19=renderFilePreview;
renderFilePreview=async function(){
  if(!previewBuilding)return;limparPreviewObjectUrl();const files=arquivosDoPredio(previewBuilding),a=files[Math.max(0,Math.min(previewIndex,files.length-1))],classe=classificarArquivo(a.nome||previewBuilding.fileName||previewBuilding.name||'');document.getElementById('filePreviewTitle').textContent=a.nome||previewBuilding.name;
  const srcBtn=document.getElementById('htmlSourceBtn'),renderBtn=document.getElementById('htmlRenderBtn'),extBtn=document.getElementById('openExternalBtn'),downBtn=document.getElementById('downloadPreviewBtn');srcBtn.hidden=renderBtn.hidden=classe!=='html';extBtn.hidden=classe!=='pdf';downBtn.hidden=true;filePreviewBody.innerHTML='<div class="previewEmpty">Carregando do dispositivo…</div>';
  const blob=await blobDoArquivo(a);document.getElementById('filePreviewMeta').innerHTML=escapeHTML(rotuloClasse(classe)+(a.tamanho?' · '+formatBytes(a.tamanho):'')+(a.relPath?' · '+a.relPath:''))+(previewStorageSource?'<span class="previewSourceBadge">'+escapeHTML(previewStorageSource)+'</span>':'');
  if(!blob){filePreviewBody.innerHTML='<div class="previewEmpty"><b>Arquivo não encontrado</b>O item está no índice, mas os bytes não foram encontrados no vault físico nem no cache offline.</div>';return}
  previewObjectUrl=URL.createObjectURL(blob);downBtn.hidden=false;downBtn.onclick=()=>baixarBlob(blob,a.nome||previewBuilding.name);extBtn.onclick=()=>{const u=URL.createObjectURL(blob);window.open(u,'_blank','noopener');setTimeout(()=>URL.revokeObjectURL(u),60000)};
  if(classe==='pdf'){await renderPdfInline(blob);return}
  if(classe==='image'){const img=document.createElement('img');img.alt=a.nome||'';img.src=previewObjectUrl;filePreviewBody.replaceChildren(img)}
  else if(classe==='audio'){const el=document.createElement('audio');el.controls=true;el.preload='metadata';el.src=previewObjectUrl;filePreviewBody.replaceChildren(el)}
  else if(classe==='video'){const el=document.createElement('video');el.controls=true;el.playsInline=true;el.preload='metadata';el.src=previewObjectUrl;filePreviewBody.replaceChildren(el)}
  else if(classe==='html'){const txt=await blob.text();if(previewHtmlMode==='source'){const pre=document.createElement('pre');pre.textContent=txt;filePreviewBody.replaceChildren(pre)}else{const fr=document.createElement('iframe');fr.setAttribute('sandbox','');fr.setAttribute('referrerpolicy','no-referrer');fr.srcdoc=txt;filePreviewBody.replaceChildren(fr)}}
  else if(classe==='zip'){try{const z=await exigirJSZip(),zip=await z.loadAsync(blob),nomes=Object.keys(zip.files).filter(n=>!zip.files[n].dir);const pre=document.createElement('pre');pre.textContent=(nomes.length?nomes.slice(0,400).join('\n'):'Arquivo compactado vazio')+(nomes.length>400?'\n… +'+(nomes.length-400)+' itens':'');filePreviewBody.replaceChildren(pre)}catch(_){filePreviewBody.innerHTML='<div class="previewEmpty"><b>Arquivo compactado</b>'+escapeHTML(a.nome||'')+'</div>'}}
  else if((blob.type||'').startsWith('text/')||blob.size<2*1024*1024){try{const pre=document.createElement('pre');pre.textContent=await blob.text();filePreviewBody.replaceChildren(pre)}catch(_){filePreviewBody.innerHTML='<div class="previewEmpty"><b>'+escapeHTML(a.nome||'Arquivo')+'</b>Sem visualização nativa para este formato.</div>'}}
  else filePreviewBody.innerHTML='<div class="previewEmpty"><b>'+escapeHTML(a.nome||'Arquivo')+'</b>Sem visualização nativa para este formato.</div>';
};
const _closeFilePreviewV19=closeFilePreview;closeFilePreview=function(){pdfRenderToken++;_closeFilePreviewV19()};


/* Consistência de extensão/caminho em árvore, rótulos e movimento por menu. */
nomeExibidoArquivo=function(b){if(b.tipo==='nota')return nomeCompletoNota(b);if(b.tipo==='anexo')return b.name;return b.fileName||b.name};
explorerEntries=function(regionId){
  const out=[];for(const b of world.buildings){
    if(b.tipo==='nota'){if((b.regionId||null)===(regionId||null))out.push({kind:'note',b,name:nomeCompletoNota(b)})}
    else{const fs=b.files||b.anexos||[];if(fs.length){fs.forEach((a,i)=>{const rr=pastaDoAsset(a,b);if((rr?.id||null)===(regionId||null))out.push({kind:'asset',b,a,index:i,name:a.nome||b.fileName||b.name})})}else if((b.regionId||null)===(regionId||null))out.push({kind:'asset',b,a:null,index:0,name:b.fileName||b.name})}
  }
  return out.sort((a,b)=>a.name.localeCompare(b.name,undefined,{sensitivity:'base'}));
};
moverArquivosParaRegiao=function(ids,regionId){
  const r=regionId?world.regions.find(x=>x.id===regionId)||null:null,ign=new Set(ids);
  for(const idd of ids){const b=world.buildings.find(x=>x.id===idd);if(!b)continue;const pos=r?vagaNaRegiao(r,semente(b.id+Date.now()),ign):vagaAleatoria(semente(b.id+Date.now()),3,3);if(!pos){toast('Sem espaço construível no destino.');continue}b.x=pos.x;b.y=pos.y;atualizarPastaFisicaConstrucao(b,r)}
  marcarIndice();indexar();scheduleRoadRebuild();buildTree();counts();agendarSalvar();marcarSinc();pedirDesenho();
};

/* ---------- PWA / atualização ---------- */
(function registrarPWA(){
  const banner=document.getElementById('updateBanner'),bt=document.getElementById('reloadUpdateBtn');
  if(window.UrbeNative){const U=window.UrbeNative.update;if(!U)return;const txt=banner.querySelector('span');
    U.onStatus(s=>{if(!s)return;
      if(s.state==='ready'){txt.textContent='Atualização '+(s.version||'')+' pronta.';bt.textContent='Reiniciar e atualizar';bt.disabled=false;banner.classList.add('open')}
      else if(s.state==='available'&&s.manualInstall){txt.textContent='Nova versão do Urbe: '+(s.version||'');bt.textContent='Baixar e instalar';bt.disabled=false;banner.classList.add('open')}
      else if(s.state==='downloading'){txt.textContent='Baixando a versão '+(s.version||'')+(s.percent!=null?' · '+Math.round(s.percent)+'%':'')+'…';bt.textContent='Aguarde';bt.disabled=true;banner.classList.add('open')}});
    bt.onclick=()=>{banner.classList.remove('open');U.install()};
    setTimeout(()=>{U.check().catch(()=>{})},6000);return}
  if(!('serviceWorker' in navigator)||location.protocol==='file:')return;
  let regAtual=null,recarregando=false;
  // O sw.js busca na rede primeiro, então a página aberta já é a versão publicada;
  // quando um service worker novo assume, só avisa (nada de recarregar no meio da edição).
  // Na primeira instalação clients.claim() também dispara controllerchange: ignora.
  const tinhaControlador=!!navigator.serviceWorker.controller;
  navigator.serviceWorker.addEventListener('controllerchange',()=>{if(recarregando||!tinhaControlador)return;fetch('./index.html',{cache:'no-store'}).then(r=>r.text()).then(t=>{if(!t.includes('Urbe v'+V21_VERSION+'<'))banner.classList.add('open')}).catch(()=>{})});
  navigator.serviceWorker.register('./sw.js',{scope:'./'}).then(reg=>{regAtual=reg;if(reg.waiting&&navigator.serviceWorker.controller)banner.classList.add('open');reg.addEventListener('updatefound',()=>{const nw=reg.installing;if(!nw)return;nw.addEventListener('statechange',()=>{if(nw.state==='installed'&&navigator.serviceWorker.controller)banner.classList.add('open')})})}).catch(e=>console.warn('service worker',e));
  bt.onclick=()=>{if(regAtual?.waiting)regAtual.waiting.postMessage({type:'SKIP_WAITING'});else location.reload()};
})();

buildTree();


/* ============================================================
   v0.20 — prioridade territorial + Explorer toolbar + Fase 5 IA
   ============================================================ */

/* ---------- regiões: a geometria nova prevalece sobre a anterior ---------- */
function v20BoundsRegiao(r){return{x:r.x,y:r.y,w:Math.max(1,r.w||1),h:Math.max(1,r.h||1)}}
function v20RecalcularBounds(r){
  if(!r.cells||!r.cells.length){r.cells=[];r._cellSet=new Set();r.w=0;r.h=0;return}
  r._cellSet=new Set(r.cells);const pts=r.cells.map(k=>k.split(',').map(Number)),xs=pts.map(p=>p[0]),ys=pts.map(p=>p[1]);r.x=Math.min(...xs);r.y=Math.min(...ys);r.w=Math.max(...xs)-r.x+1;r.h=Math.max(...ys)-r.y+1;
}
/* o bairro é o próprio `anc` ou fica dentro dele (subbairro, sub-subbairro…) */
function urbeDentroDe(rid,anc){if(!anc)return false;for(let r=world.regions.find(o=>o.id===rid),g=0;r&&g<24;g++){if(r.id===anc.id)return true;const p=r.parentId;r=p?world.regions.find(o=>o.id===p):null}return false}
function v20TileProtegidoPorConstrucaoAlheia(x,y,owner){
  /* casa de um subbairro também é do bairro: antes ela abria um buraco no bairro de cima */
  for(const b of world.buildings){if(!b.regionId||b.regionId===owner?.id||urbeDentroDe(b.regionId,owner))continue;if(x>=b.x-LOTE_GAP&&x<b.x+b.w+LOTE_GAP&&y>=b.y-LOTE_GAP&&y<b.y+b.h+LOTE_GAP)return true}return false;
}
function v20TileEmSubregiaoAlheia(x,y,owner){
  for(const r of world.regions){if(r.id===owner?.id||!r.parentId)continue;if(owner&&urbeDentroDe(r.id,owner))continue;if(owner&&urbeDentroDe(owner.id,r))continue;if(regionHasTile(r,x,y))return true}return false;
}
function v20LimitarBoundsAConstrucoes(r,b){
  if(!r)return b;let x0=b.x,y0=b.y,x1=b.x+b.w-1,y1=b.y+b.h-1;
  for(const c of world.buildings)if(c.regionId===r.id){x0=Math.min(x0,c.x-LOTE_GAP);y0=Math.min(y0,c.y-LOTE_GAP);x1=Math.max(x1,c.x+c.w-1+LOTE_GAP);y1=Math.max(y1,c.y+c.h-1+LOTE_GAP)}
  return{x:x0,y:y0,w:x1-x0+1,h:y1-y0+1};
}
function v20MascaraPrioritaria(bounds,owner=null){
  bounds=v20LimitarBoundsAConstrucoes(owner,bounds);const parent=owner?.parentId?world.regions.find(r=>r.id===owner.parentId)||null:null;
  const dentro=(x,y)=>x>=bounds.x&&y>=bounds.y&&x<bounds.x+bounds.w&&y<bounds.y+bounds.h;
  const permitido=(x,y)=>{if(!dentro(x,y)||ehAgua(x,y))return false;if(parent&&!regionHasTile(parent,x,y))return false;if(v20TileProtegidoPorConstrucaoAlheia(x,y,owner))return false;if(v20TileEmSubregiaoAlheia(x,y,owner))return false;return true};
  const cand=[];let secos=0,total=0;for(let y=bounds.y;y<bounds.y+bounds.h;y++)for(let x=bounds.x;x<bounds.x+bounds.w;x++){total++;if(permitido(x,y)){secos++;cand.push({x,y})}}
  if(!cand.length)return{cells:[],secos,total,bounds};
  const seen=new Set(),comps=[];for(const seed of cand){const sk=K(seed.x,seed.y);if(seen.has(sk))continue;const q=[seed],cells=[];while(q.length){const c=q.shift(),k=K(c.x,c.y);if(seen.has(k))continue;seen.add(k);if(!permitido(c.x,c.y))continue;cells.push(k);q.push({x:c.x+1,y:c.y},{x:c.x-1,y:c.y},{x:c.x,y:c.y+1},{x:c.x,y:c.y-1})}if(cells.length)comps.push(cells)}
  comps.sort((a,b)=>b.length-a.length);let keep=[...(comps[0]||[])];
  if(owner){const required=new Set();for(const b of world.buildings)if(b.regionId===owner.id)for(let y=b.y;y<b.y+b.h;y++)for(let x=b.x;x<b.x+b.w;x++)required.add(K(x,y));for(const comp of comps.slice(1))if(comp.some(k=>required.has(k)))keep.push(...comp)}
  return{cells:[...new Set(keep)],secos,total,bounds};
}
function v20CederTerritorio(nova){
  if(!nova.cells?.length)return;const take=new Set(nova.cells),mesmoPai=nova.parentId||null;
  for(const old of world.regions){if(old===nova||(old.parentId||null)!==mesmoPai||!old.cells?.length)continue;const antes=old.cells.length;old.cells=old.cells.filter(k=>!take.has(k));if(old.cells.length!==antes)v20RecalcularBounds(old)}
}
function v20AplicarMascaraRegiao(r,m){r.cells=m.cells;r._cellSet=new Set(r.cells);v20RecalcularBounds(r);v20CederTerritorio(r);absorverConstrucoesDaRaiz(r);try{urbeTaparTodos()}catch(e){}marcarIndice();indexar();scheduleRoadRebuild();counts();buildTree();agendarSalvar();marcarSinc();pedirDesenho()}

/* criação manual usa a máscara prioritária */
document.getElementById('confirmRegion').onclick=()=>{
  if(dlg.classList.contains('regionInputGuard'))return;const nome=(document.getElementById('regionName').value||'Região').trim()||'Região',desc=document.getElementById('regionDesc').value||'',cor=document.getElementById('regionColor').value||'#4db7ff';
  if(regionEditTarget){const camAntes=caminhosRegioes().get(regionEditTarget.id)||'';regionEditTarget.name=nome;regionEditTarget.description=desc;regionEditTarget.color=cor;const camDepois=caminhosRegioes().get(regionEditTarget.id)||'';atualizarFolderPathAoRenomear(camAntes,camDepois);dlg.classList.remove('open');const rr=regionEditTarget;regionEditTarget=null;marcarIndice();indexar();buildTree();pedirDesenho();agendarSalvar();marcarSinc();toast('Região “'+rr.name+'” atualizada.');return}
  if(!pendingRegion)return;const m=v20MascaraPrioritaria(pendingRegion,null);if(!m.cells.length){toast('Não há terreno disponível nesta seleção.');return}const r={id:id('r'),kind:'region',x:m.bounds.x,y:m.bounds.y,w:m.bounds.w,h:m.bounds.h,cells:m.cells,name:nome,description:desc,color:cor,parentId:null};r._cellSet=new Set(r.cells);world.regions.push(r);v20CederTerritorio(r);explorerRegionTarget=r.id;const adotadas=absorverConstrucoesDaRaiz(r);marcarIndice();indexar();dlg.classList.remove('open');pendingRegion=null;counts();buildTree();agendarSalvar();marcarSinc();scheduleRoadRebuild();pedirDesenho();toast('Região criada com prioridade territorial'+(adotadas?' · '+adotadas+' item(ns) da raiz incorporado(s).':'.'));setTool('select')
};

/* ---------- edição de forma por pressão prolongada no mundo ---------- */
let v20RegionShape=null,v20CanvasHold=null;
const v20RegionHint=document.createElement('div');v20RegionHint.className='regionShapeHint';v20RegionHint.textContent='Editar forma · arraste as bordas/alças · toque fora para concluir';document.getElementById('app').appendChild(v20RegionHint);
function v20HandlePoints(r){const b=v20RegionShape?.draft||v20BoundsRegiao(r),x=b.x*TILE,y=b.y*TILE,w=b.w*TILE,h=b.h*TILE;return[{k:'nw',x,y},{k:'n',x:x+w/2,y},{k:'ne',x:x+w,y},{k:'e',x:x+w,y:y+h/2},{k:'se',x:x+w,y:y+h},{k:'s',x:x+w/2,y:y+h},{k:'sw',x,y:y+h},{k:'w',x,y:y+h/2}]}
function v20HandleAt(cx,cy){if(!v20RegionShape)return null;const rc=cv.getBoundingClientRect();for(const h of v20HandlePoints(v20RegionShape.r)){const p=w2s(h.x,h.y);if(Math.hypot(cx-rc.left-p.x,cy-rc.top-p.y)<=18)return h.k}return null}
function v20EntrarShape(r){if(!r||!r.cells?.length)return toast('Esta pasta não possui área desenhada.');v20RegionShape={r,draft:v20BoundsRegiao(r)};selected=r;v20RegionHint.classList.add('open');if(navigator.vibrate)navigator.vibrate(18);pedirDesenho()}
function v20SairShape(aplicar=true){if(!v20RegionShape)return;const st=v20RegionShape;v20RegionShape=null;v20RegionHint.classList.remove('open');
  /* só tocou e saiu, sem mexer nas alças: a forma fica exatamente como estava */
  const o=v20BoundsRegiao(st.r),d=st.draft;if(aplicar&&d&&d.x===o.x&&d.y===o.y&&d.w===o.w&&d.h===o.h)aplicar=false;
  if(aplicar){const m=v20MascaraPrioritaria(st.draft,st.r);if(m.cells.length){v20AplicarMascaraRegiao(st.r,m);toast('Formato da região atualizado.')}else toast('A alteração foi descartada: não restou terreno válido.')}pedirDesenho()}
function v20ResizeDraft(base,handle,t){let x0=base.x,y0=base.y,x1=base.x+base.w-1,y1=base.y+base.h-1;if(handle.includes('w'))x0=Math.min(t.x,x1-2);if(handle.includes('e'))x1=Math.max(t.x,x0+2);if(handle.includes('n'))y0=Math.min(t.y,y1-2);if(handle.includes('s'))y1=Math.max(t.y,y0+2);return{x:x0,y:y0,w:x1-x0+1,h:y1-y0+1}}
const _drawOverlayV20=drawOverlay;drawOverlay=function(){_drawOverlayV20();if(!v20RegionShape)return;const r=v20RegionShape.r,b=v20RegionShape.draft,p=w2s(b.x*TILE,b.y*TILE),ww=b.w*TILE*camera.z,hh=b.h*TILE*camera.z;ctx.save();ctx.strokeStyle='#b9ecff';ctx.lineWidth=2;ctx.setLineDash([7,4]);ctx.strokeRect(p.x,p.y,ww,hh);ctx.setLineDash([]);for(const h of v20HandlePoints(r)){const hp=w2s(h.x,h.y);ctx.fillStyle='#0d1b24';ctx.strokeStyle='#b9ecff';ctx.lineWidth=2;ctx.beginPath();ctx.arc(hp.x,hp.y,Math.max(6,7*camera.z),0,Math.PI*2);ctx.fill();ctx.stroke()}ctx.restore()};

/* substitui os handlers v0.19 do canvas */
cv.removeEventListener('pointerdown',canvasDownV19,true);cv.removeEventListener('pointermove',canvasMoveV19,true);cv.removeEventListener('pointerup',canvasUpV19,true);cv.removeEventListener('pointercancel',canvasCancelV19,true);
function canvasDownV20(e){
  e.stopImmediatePropagation();try{cv.setPointerCapture(e.pointerId)}catch(_){}pointers.set(e.pointerId,{x:e.clientX,y:e.clientY});clearTimeout(v20CanvasHold);if(pointers.size!==1){if(pointers.size===2){const ps=[...pointers.values()];gesture={mode:'pinch',dist:Math.hypot(ps[0].x-ps[1].x,ps[0].y-ps[1].y),z:camera.z}}return}
  const t=tileFromClient(e.clientX,e.clientY),handle=tool==='select'?v20HandleAt(e.clientX,e.clientY):null;if(handle&&v20RegionShape){gesture={mode:'resizeRegion',handle,base:{...v20RegionShape.draft},sx:e.clientX,sy:e.clientY,moved:false};return}
  const hit=tool==='select'?bAt(t):null,rhit=tool==='select'&&!hit?regAt(t):null;gesture={mode:tool==='select'?'selectCandidate':tool,start:t,sx:e.clientX,sy:e.clientY,cx:camera.x,cy:camera.y,moved:false,building:hit,region:rhit,offX:hit?t.x-hit.x:0,offY:hit?t.y-hit.y:0,held:false};if(tool==='region')regionDraft={a:t,b:t};if(rhit&&!hit&&tool==='select')v20CanvasHold=setTimeout(()=>{if(gesture&&gesture.region===rhit&&!gesture.moved){gesture.held=true;v20EntrarShape(rhit)}},520);pedirDesenho();
}
function canvasMoveV20(e){
  e.stopImmediatePropagation();if(!pointers.has(e.pointerId))return;const pp=pointers.get(e.pointerId);pp.x=e.clientX;pp.y=e.clientY;if(pointers.size===2&&gesture?.mode==='pinch'){clearTimeout(v20CanvasHold);const ps=[...pointers.values()],d=Math.hypot(ps[0].x-ps[1].x,ps[0].y-ps[1].y);camera.z=clamp(gesture.z*d/gesture.dist,.5,2.8);counts();pedirDesenho();return}if(pointers.size!==1||!gesture)return;
  const t=tileFromClient(e.clientX,e.clientY),dx=e.clientX-gesture.sx,dy=e.clientY-gesture.sy;if(Math.abs(dx)+Math.abs(dy)>7){gesture.moved=true;clearTimeout(v20CanvasHold)}if(gesture.mode==='resizeRegion'){v20RegionShape.draft=v20ResizeDraft(gesture.base,gesture.handle,t)}else if(gesture.mode==='selectCandidate'&&gesture.moved){gesture.mode=gesture.building?'moveBuilding':'pan'}if(gesture.mode==='pan'){camera.x=gesture.cx-dx/camera.z;camera.y=gesture.cy-dy/camera.z}else if(gesture.mode==='moveBuilding'){gesture.dragPos={x:t.x-gesture.offX,y:t.y-gesture.offY}}else if(gesture.mode==='region'&&regionDraft)regionDraft.b=t;else if(gesture.mode==='house')ghost=t;pedirDesenho();
}
function canvasUpV20(e){
  e.stopImmediatePropagation();clearTimeout(v20CanvasHold);pointers.delete(e.pointerId);if(!gesture||gesture.mode==='pinch'){if(!pointers.size)gesture=null;pedirDesenho();return}const t=tileFromClient(e.clientX,e.clientY),g=gesture;if(g.mode==='resizeRegion'){const m=v20MascaraPrioritaria(v20RegionShape.draft,v20RegionShape.r);if(m.cells.length){v20RegionShape.draft=m.bounds;v20AplicarMascaraRegiao(v20RegionShape.r,m)}}else if(g.held){gesture=null;return}else if(g.mode==='moveBuilding'&&g.dragPos&&g.building){finalizarMovimentoMundo(g.building,g.dragPos)}else if(g.mode==='pan'){}else if(g.mode==='selectCandidate'&&!g.moved){const b=bAt(t),r=regAt(t);if(v20RegionShape&&(!r||r.id!==v20RegionShape.r.id)){v20SairShape(true)}else{selected=b||r||null;if(b){if(b.tipo==='nota')openHouseSummary(b);else openFilePreview(b)}else if(r){closeHouseSummary();closeFilePreview();openRegionEditDialog(r)}else{closeHouseSummary();closeFilePreview()}}}else if(g.mode==='region'&&regionDraft){const a=regionDraft.a,b=regionDraft.b;regionDraft=null;const x=Math.min(a.x,b.x),y=Math.min(a.y,b.y),w=Math.abs(a.x-b.x)+1,h=Math.abs(a.y-b.y)+1;if(w>=3&&h>=3)openRegionDialog({x,y,w,h});else toast('A região precisa ser maior.')}else if(g.mode==='house'&&!g.moved){if(canHouse(t)){const r=regAt(t),n=world.buildings.length%3+1,b={id:id('b'),kind:'building',regionId:r?r.id:null,x:t.x,y:t.y,w:3,h:3,name:'Nova nota',ext:'.md',description:'',content:'',sprite:'house'+n,tipo:'nota',tags:[],created:nowDate(),modified:nowDate()};world.buildings.push(b);selected=b;marcarIndice();indexar();openHouseSummary(b);counts();scheduleRoadRebuild();agendarSalvar();marcarSinc();toast('Nota criada'+(r?' em '+r.name:' na raiz')+'.')}else toast('Lote inválido.')}gesture=null;pedirDesenho();
}
function canvasCancelV20(e){e.stopImmediatePropagation();clearTimeout(v20CanvasHold);pointers.delete(e.pointerId);gesture=null;regionDraft=null;pedirDesenho()}
cv.addEventListener('pointerdown',canvasDownV20,true);cv.addEventListener('pointermove',canvasMoveV20,true);cv.addEventListener('pointerup',canvasUpV20,true);cv.addEventListener('pointercancel',canvasCancelV20,true);

/* ---------- Explorer: todas as ações no topo + multiseleção ---------- */
const v20EntrySelection=new Set();
function v20EntryKey(e){return e.kind+':'+e.b?.id+':'+(e.index||0)}
function v20EntryFromKey(k){const [kind,bid,idxs]=k.split(':'),b=world.buildings.find(x=>x.id===bid),index=+(idxs||0);if(!b)return null;const a=kind==='asset'?(b.files||b.anexos||[])[index]:null;return{kind,b,index,a,name:kind==='note'?nomeCompletoNota(b):(a?.nome||b.fileName||b.name)}}
function v20SelectedEntries(){return [...v20EntrySelection].map(v20EntryFromKey).filter(Boolean)}
function v20EnsureExplorerToolbar(){
  let bar=document.getElementById('explorerTopActions');if(bar)return bar;bar=document.createElement('div');bar.id='explorerTopActions';bar.innerHTML='<span id="explorerSelectionLabel"></span><button class="explorerTopBtn" id="exNewNote" title="Nova nota">＋</button><button class="explorerTopBtn" id="exNewFolder" title="Nova pasta">📁</button><button class="explorerTopBtn" id="exImport" title="Importar do dispositivo">⇩</button><button class="explorerTopBtn" id="exEdit" title="Editar/renomear">✎</button><button class="explorerTopBtn" id="exMove" title="Mover">↪</button><button class="explorerTopBtn" id="exGroup" title="Criar pasta com seleção">▣</button><button class="explorerTopBtn danger" id="exDelete" title="Excluir">🗑</button><button class="explorerTopBtn" id="exClear" title="Limpar seleção">×</button>';
  const top=fileSidebar.querySelector('.sidebarTop'),old=document.getElementById('explorerImportFolderBtn');if(old)old.hidden=true;top.insertBefore(bar,document.getElementById('closeSidebar'));
  bar.querySelector('#exNewNote').onclick=()=>criarNotaNoDestino(explorerRegionTarget||null);bar.querySelector('#exNewFolder').onclick=()=>criarPastaNoDestino(explorerRegionTarget||null);bar.querySelector('#exImport').onclick=()=>abrirImportacaoSistema(explorerRegionTarget||null);bar.querySelector('#exEdit').onclick=()=>v20EditarContextoExplorer();bar.querySelector('#exMove').onclick=()=>v20MoverSelecao();bar.querySelector('#exGroup').onclick=()=>v20AgruparSelecao();bar.querySelector('#exDelete').onclick=()=>v20ExcluirContexto();bar.querySelector('#exClear').onclick=()=>{v20EntrySelection.clear();buildTree()};return bar;
}
function v20UpdateExplorerToolbar(){
  const bar=v20EnsureExplorerToolbar(),sel=v20SelectedEntries(),r=explorerRegionTarget?world.regions.find(x=>x.id===explorerRegionTarget)||null:null,label=bar.querySelector('#explorerSelectionLabel');label.textContent=sel.length?sel.length+' sel.':(r?r.name:'Raiz');bar.querySelector('#exNewNote').hidden=sel.length>0;bar.querySelector('#exNewFolder').hidden=sel.length>0;bar.querySelector('#exImport').hidden=sel.length>0;bar.querySelector('#exEdit').hidden=sel.length>1;bar.querySelector('#exMove').hidden=sel.length===0;bar.querySelector('#exGroup').hidden=sel.length<2;bar.querySelector('#exDelete').hidden=sel.length===0&&!r;bar.querySelector('#exClear').hidden=sel.length===0;
}
function v20EditarContextoExplorer(){const sel=v20SelectedEntries();if(sel.length===1){const e=sel[0];renomearEntradaTree(e);return}const r=explorerRegionTarget?world.regions.find(x=>x.id===explorerRegionTarget):null;if(r)openRegionEditDialog(r)}
async function v20MoverSelecao(){const entries=v20SelectedEntries();if(!entries.length)return;const d=await escolherDestinoRegiao('Mover seleção para');if(d===undefined)return;for(const e of entries)moverEntradaPara(e,d);v20EntrySelection.clear();buildTree();toast(entries.length+' item(ns) movido(s).')}
async function v20AgruparSelecao(){const entries=v20SelectedEntries();if(entries.length<2)return;const nome=((await UD.prompt({title:'Agrupar em nova pasta',label:'Nome da pasta',value:'Nova pasta',confirm:'Criar pasta'}))||'').trim();if(!nome)return;const parent=explorerRegionTarget?world.regions.find(x=>x.id===explorerRegionTarget)||null:null;const r=criarRegiaoOrganica(nome,entries.length,semente(nome+Date.now()),parent?.id||null,null,'Pasta criada pela multiseleção.');if(!r)return toast('Não foi possível criar a pasta.');for(const e of entries)moverEntradaPara(e,r.id);v20EntrySelection.clear();explorerRegionTarget=r.id;buildTree();toast('Pasta criada com '+entries.length+' itens.')}
async function v20ExcluirContexto(){const entries=v20SelectedEntries();if(entries.length){if(!(await UD.confirm({title:'Excluir '+entries.length+' item(ns)?',message:'Os itens selecionados serão excluídos do vault.',confirm:'Excluir',danger:true})))return;const assets=[...entries].filter(e=>e.kind==='asset').sort((a,b)=>b.index-a.index);for(const e of entries.filter(e=>e.kind==='note')){world.buildings=world.buildings.filter(x=>x!==e.b);if(currentFile===e.b){currentFile=null;editorFull.classList.remove('open')}}for(const e of assets){const fs=e.b.files||e.b.anexos||[],a=fs[e.index];if(!a)continue;if(a.cacheId)DBK.bDel(a.cacheId).catch(()=>{});fs.splice(e.index,1);e.b.files=fs;e.b.anexos=fs;if(!fs.length)world.buildings=world.buildings.filter(x=>x!==e.b)}v20EntrySelection.clear();marcarIndice();indexar();scheduleRoadRebuild();counts();buildTree();agendarSalvar();marcarSinc();toast('Seleção excluída.');return}const r=explorerRegionTarget?world.regions.find(x=>x.id===explorerRegionTarget):null;if(r)excluirRegiaoExplorer(r)}
/* segurar seleciona; não há mais action sheet */
menuEntradaExplorer=function(e){const k=v20EntryKey(e);v20EntrySelection.has(k)?v20EntrySelection.delete(k):v20EntrySelection.add(k);if(e.kind==='note')explorerRegionTarget=e.b.regionId||null;else explorerRegionTarget=pastaDoAsset(e.a,e.b)?.id||null;buildTree()};
menuRegiaoExplorer=function(r){v20EntrySelection.clear();explorerRegionTarget=r.id;buildTree()};
menuRaizExplorer=function(){v20EntrySelection.clear();explorerRegionTarget=null;buildTree()};
const _buildTreeV20=buildTree;buildTree=function(){_buildTreeV20();for(const el of tree.querySelectorAll('[data-entry]')){const e=entradaPorEl(el),k=v20EntryKey(e);el.classList.toggle('multiSelected',v20EntrySelection.has(k))}v20UpdateExplorerToolbar()};
const _abrirEntradaTreeV20=abrirEntradaTree;abrirEntradaTree=function(e){if(v20EntrySelection.size){const k=v20EntryKey(e);v20EntrySelection.has(k)?v20EntrySelection.delete(k):v20EntrySelection.add(k);buildTree();return}_abrirEntradaTreeV20(e)};
/* clicar no vazio apenas muda contexto para raiz; nunca abre painel */
treeRoot.addEventListener('click',e=>{if(e.target===treeRoot||e.target.classList.contains('treeEmpty')){v20EntrySelection.clear();explorerRegionTarget=null;buildTree()}},true);

/* botão legado de Vault/ZIP fica definitivamente fora da experiência */
const vd=document.getElementById('vaultDlg');if(vd)vd.remove();

/* ---------- PDF.js local no PWA ---------- */
obterPdfJs=async function(){if(!pdfJsPromise)pdfJsPromise=import(PDFJS_URL).then(m=>{m.GlobalWorkerOptions.workerSrc=PDFJS_WORKER_URL;return m});return pdfJsPromise};

/* ---------- Fase 5: adaptador OpenRouter + configuração + métricas ---------- */
























buildTree();


/* ============================================================
   Urbe v0.21 — unified files, mobile-first interactions & AI UX
   ============================================================ */
var V21_VERSION=window.UrbeCore.version;
var V21_EDIT_EXT_RE=window.UrbeArtifacts.RE.text;
var V21_EXT_RE=window.UrbeArtifacts.RE.text;
var v21Placement=null,v21PlacementCandidate=null,v21FileDialogResolve=null,v21MultiMode=false,v21ExplorerQuery='',v21OpenTabs=[],v21MoveHold=null;


function v21ExtFromName(name,fallback='.md'){
  var m=String(name||'').trim().match(/(\.[A-Za-z0-9]+)$/);if(!m)return fallback;
  var x=m[1].toLowerCase();return V21_EXT_RE.test(x)?x:fallback;
}
function v21Stem(name){var s=String(name||'').trim().replace(/[\\/]/g,'-');var m=s.match(V21_EXT_RE);if(m)s=s.slice(0,-m[0].length);return nomeSeguro(s||'Nota')}
function v21IsEditablePath(path){return V21_EDIT_EXT_RE.test(String(path||''))}
function v21Kind(b){var e=extNota(b);return e.slice(1).toLowerCase()}
function v21DefaultContent(ext){return /^\.(md|markdown)$/i.test(ext)?'# ':''}
function v21UniqueName(raw,regionId,ignore){
  var txt=String(raw||'').trim().replace(/[\\/]/g,'-');if(!txt)txt='Nota.md';
  var ext=v21ExtFromName(txt,'.md'),stem=v21Stem(txt),taken=new Set(world.buildings.filter(function(b){return b.tipo==='nota'&&b!==ignore&&(b.regionId||null)===(regionId||null)}).map(function(b){return nomeCompletoNota(b).toLowerCase()}));
  var candidate=stem+ext,n=2;while(taken.has(candidate.toLowerCase()))candidate=stem+' ('+(n++)+')'+ext;return candidate;
}
function v21FileAccent(ext){return {md:'#6e9a79',markdown:'#6e9a79',txt:'#8798a0',html:'#d17a55',htm:'#d17a55',js:'#d1b855',mjs:'#d1b855',css:'#618aca',json:'#ad795e',yaml:'#8b79ad',yml:'#8b79ad',csv:'#5f9d85'}[ext]||'#668995'}


/* generic editable identity */
extNota=function(b){var e=String(b&&b.ext||'.md').trim().toLowerCase();return V21_EXT_RE.test(e)?e:'.md'};
nomeCompletoNota=function(b){return (b&&b.name||'arquivo')+extNota(b)};
separarNomeExtNota=function(valor,atual){var v=String(valor||'').trim().replace(/[\\/]/g,'-');if(!v)return null;var m=v.match(window.UrbeArtifacts.RE.splitText);if(m&&m[1].trim())return{name:nomeSeguro(m[1].trim()),ext:m[2].toLowerCase()};return{name:nomeSeguro(v.replace(/\.[^.]+$/,'')),ext:extNota(atual)}};
renomearNota=async function(b){var v=await UD.prompt({title:'Renomear',label:'Nome do arquivo',value:nomeCompletoNota(b),hint:'Mantenha a extensão (.md, .txt, .html…) para preservar o tipo.',confirm:'Renomear'});if(v!==null){var u=v21UniqueName(v,b.regionId,b);if(aplicarNomeCompletoNota(b,u)){v21RefreshEditorChrome();toast('Arquivo renomeado.')}}};

/* chrome injection */
(function v21InjectChrome(){
  document.title='Urbe v'+V21_VERSION;
  var hp=document.querySelector('#housePanel .panelHead strong');if(hp)hp.textContent='Arquivo';var ob=document.getElementById('openNoteBtn');if(ob)ob.innerHTML=(window.UrbeIcons?UrbeIcons.icon('edit'):'')+'<b>Abrir e editar</b>';var ch=document.getElementById('closeHouse');if(ch){ch.innerHTML=window.UrbeIcons?UrbeIcons.icon('close'):'×';ch.setAttribute('aria-label','Fechar')}
  var toolsEl=document.getElementById('tools');if(toolsEl){
    var selectBtn=toolsEl.querySelector('[data-tool="select"]');if(selectBtn)selectBtn.classList.remove('active');
    var menuBtn=document.getElementById('cidadesBtn');if(menuBtn){menuBtn.innerHTML='🚪';menuBtn.title='Vaults';menuBtn.setAttribute('aria-label','Vaults')}
    if(!document.getElementById('v21RailToggle')){var sep=document.createElement('div');sep.className='v21RailSep v21RailAction';toolsEl.appendChild(sep);var gear=document.createElement('button');gear.id='v21GlobalSettingsBtn';gear.className='v21RailBtn v21RailAction';gear.textContent='⚙';gear.title='Configurações';toolsEl.appendChild(gear);var tog=document.createElement('button');tog.id='v21RailToggle';tog.className='v21RailBtn';tog.textContent='‹';tog.title='Recolher barra';toolsEl.appendChild(tog);tog.onclick=function(){toolsEl.classList.toggle('collapsed');tog.textContent=toolsEl.classList.contains('collapsed')?'':'‹'};gear.onclick=function(){v21OpenGlobalSettings()}}
  }
  var loading=document.createElement('div');loading.id='v21Loading';loading.innerHTML='<div class="v21LoadCard"><div class="v21LoadMark">URBE</div><div class="v21LoadTrack"><span id="v21LoadBar"></span></div><small id="v21LoadText">Abrindo vault</small></div>';document.body.appendChild(loading);
  var fd=document.createElement('div');fd.id='v21FileDialog';fd.innerHTML='<div class="v21DialogCard"><div class="v21DialogHead"><strong id="v21FileDialogTitle">Novo arquivo</strong><button id="v21FileClose">×</button></div><label class="v21Field"><span>NOME + EXTENSÃO</span><input id="v21FileName" autocomplete="off" autocapitalize="none" spellcheck="false"></label><div class="v21Hint">Formatos editáveis: .md, .txt, .html, .js, .css, .json, .yaml/.yml e .csv. Sem extensão, será .md.</div><label class="v21Field"><span>DESCRIÇÃO OPCIONAL</span><textarea id="v21FileDesc"></textarea></label><div class="v21DialogActions"><button id="v21FileCancel">Cancelar</button><button class="primary" id="v21FileConfirm">Criar</button></div></div>';document.body.appendChild(fd);
  document.getElementById('v21FileClose').onclick=function(){v21CloseFileDialog(null)};document.getElementById('v21FileCancel').onclick=function(){v21CloseFileDialog(null)};document.getElementById('v21FileConfirm').onclick=function(){var n=document.getElementById('v21FileName').value,desc=document.getElementById('v21FileDesc').value;v21CloseFileDialog({name:n,description:desc})};
  var dock=document.createElement('div');dock.id='v21PlacementDock';dock.innerHTML='<span id="v21PlacementText">Posicione no mapa</span><button id="v21PlacementCancel" title="Cancelar">×</button><button id="v21PlacementOk" title="Confirmar">✓</button>';document.body.appendChild(dock);document.getElementById('v21PlacementCancel').onclick=v21CancelPlacement;document.getElementById('v21PlacementOk').onclick=v21ConfirmPlacement;
  var tabs=document.createElement('div');tabs.id='v21EditorTabs';document.getElementById('editorTop').insertAdjacentElement('afterend',tabs);
  var code=document.createElement('div');code.className='v21CodeTools';code.id='v21CodeTools';code.innerHTML='<button id="v21Indent">⇥ Indentar</button><button id="v21Format">{ } Formatar</button>';document.getElementById('mdToolbar').insertAdjacentElement('afterend',code);document.getElementById('v21Indent').onclick=v21IndentSelection;document.getElementById('v21Format').onclick=v21FormatCurrent;
})();

function v21SetLoading(on,p,text){var el=document.getElementById('v21Loading');if(!el)return;el.classList.toggle('open',!!on);if(p!=null)document.getElementById('v21LoadBar').style.width=Math.max(0,Math.min(100,p))+'%';if(text)document.getElementById('v21LoadText').textContent=text}
function v21OpenFileDialog(opts){opts=opts||{};var nova=!opts.title||opts.title==='Novo arquivo';return UD.prompt({title:nova?'Nova nota':opts.title,label:'Nome',value:opts.name||'',placeholder:'Ex.: Ideias',hint:'Sem extensão, vira .md. Também aceita .txt, .html, .css, .js, .json, .yaml e .csv.',confirm:opts.confirm||'Criar'}).then(function(n){return n==null?null:{name:n,description:opts.description||''}})}
function v21CloseFileDialog(v){document.getElementById('v21FileDialog').classList.remove('open');var r=v21FileDialogResolve;v21FileDialogResolve=null;if(r)r(v)}
/* (v21OpenGlobalSettings: definição antiga removida na 1.0 — a versão em uso está mais abaixo) */


/* tool semantics: no selected side tool == selection */
setTool=function(t){
  if(t!=='select'&&tool===t)t='select';tool=t||'select';ghost=null;v21PlacementCandidate=null;pedirDesenho();
  document.querySelectorAll('#tools .tool[data-tool]').forEach(function(b){b.classList.toggle('active',tool!=='select'&&b.dataset.tool===tool)});
};
document.querySelectorAll('#tools .tool[data-tool]').forEach(function(b){b.onclick=function(){setTool(b.dataset.tool)}});

/* region dialogs: blank creation; automatic numbering */
function v21UniqueRegionName(raw){var base=String(raw||'').trim()||'Região',taken=new Set(world.regions.map(function(r){return String(r.name||'').toLowerCase()}));if(!taken.has(base.toLowerCase()))return base;var n=2,name=base+' ('+n+')';while(taken.has(name.toLowerCase()))name=base+' ('+(++n)+')';return name}
openRegionDialog=function(bounds){regionEditTarget=null;pendingRegion=bounds;document.getElementById('regionDialogTitle').textContent='Nova região';document.getElementById('regionName').value='';document.getElementById('regionDesc').value='';document.getElementById('regionColor').value=colors[world.regions.length%colors.length]||'#4db7ff';document.getElementById('confirmRegion').textContent='Criar';protegerDialogRegiao();dlg.classList.add('open');setTimeout(function(){document.getElementById('regionName').focus()},285)};
var v21RegionConfirm=document.getElementById('confirmRegion').onclick;document.getElementById('confirmRegion').onclick=function(){if(!regionEditTarget){document.getElementById('regionName').value=v21UniqueRegionName(document.getElementById('regionName').value)}v21RegionConfirm()};

/* placement workflow */
function v21PlacementStatus(){var dock=document.getElementById('v21PlacementDock'),ok=v21PlacementCandidate&&(!v21Placement||v21Placement.kind!=='copy'?canHouse(v21PlacementCandidate):validarPosicaoMovel(v21Placement.source,v21PlacementCandidate.x,v21PlacementCandidate.y).ok);dock.classList.toggle('open',!!v21Placement);document.getElementById('v21PlacementOk').disabled=!ok;document.getElementById('v21PlacementText').textContent=!v21Placement?'':(v21Placement.kind==='copy'?'Posicione a cópia':'Posicione a construção')+(v21PlacementCandidate?' · '+v21PlacementCandidate.x+','+v21PlacementCandidate.y:' · toque no mapa')}
function v21BeginNewPlacement(){v21Placement={kind:'new'};v21PlacementCandidate=null;setTool('house');v21PlacementStatus()}
function v21BeginCopyPlacement(b){if(!b||b.tipo!=='nota')return;closeHouseSummary();v21Placement={kind:'copy',source:b};v21PlacementCandidate=null;tool='select';document.querySelectorAll('#tools .tool').forEach(function(x){x.classList.remove('active')});v21PlacementStatus();toast('Toque no mapa para posicionar a cópia.')}
function v21CancelPlacement(){v21Placement=null;v21PlacementCandidate=null;ghost=null;document.getElementById('v21PlacementDock').classList.remove('open');setTool('select');pedirDesenho()}
async function v21ConfirmPlacement(){if(!v21Placement||!v21PlacementCandidate)return;var p=v21PlacementCandidate,mode=v21Placement.kind,src=v21Placement.source;if(mode==='copy'){
    var valid=validarPosicaoMovel(src,p.x,p.y);if(!valid.ok)return toast('Posição inválida.');var cfgCopy=await v21OpenFileDialog({title:'Configurar cópia',confirm:'Copiar'});if(!cfgCopy)return;var requested=String(cfgCopy.name||'').trim();if(requested&&!V21_EXT_RE.test(requested))requested+=extNota(src);var base=v21UniqueName(requested||nomeCompletoNota(src),valid.r&&valid.r.id||null,null),parts=separarNomeExtNota(base,src);var clone={...src,id:id('b'),name:parts.name,ext:parts.ext,x:p.x,y:p.y,regionId:valid.r&&valid.r.id||null,description:cfgCopy.description||src.description||'',created:nowDate(),modified:nowDate(),anexos:[],aiLocal:src.aiLocal?JSON.parse(JSON.stringify(src.aiLocal)):undefined};world.buildings.push(clone);selected=clone;v21CancelPlacement();marcarIndice();indexar();counts();scheduleRoadRebuild();agendarSalvar();marcarSinc();openHouseSummary(clone);v21BuildTree();toast('Cópia posicionada.');return;
  }
  if(!canHouse(p))return toast('Posição inválida.');var cfg=await v21OpenFileDialog({title:'Configurar arquivo',confirm:'Construir'});if(!cfg)return;var r=regAt(p),full=v21UniqueName(cfg.name,r&&r.id||null,null),x=separarNomeExtNota(full,{ext:'.md'}),ext=x.ext,b={id:id('b'),kind:'building',regionId:r?r.id:null,x:p.x,y:p.y,w:3,h:3,name:x.name,ext:ext,description:cfg.description||'',content:v21DefaultContent(ext),sprite:'house'+(world.buildings.length%3+1),tipo:'nota',tags:[],created:nowDate(),modified:nowDate(),aiLocal:{instructions:'',memory:'',artifacts:[]}};world.buildings.push(b);selected=b;v21CancelPlacement();marcarIndice();indexar();counts();scheduleRoadRebuild();agendarSalvar();marcarSinc();openHouseSummary(b);v21BuildTree();toast('Arquivo construído.')
}
var v21CopyBtn=document.getElementById('copyBtn');if(v21CopyBtn)v21CopyBtn.onclick=function(){if(selected&&selected.kind==='building')v21BeginCopyPlacement(selected)};

/* new file from Explorer uses same config pipeline, without forcing editor open */
async function v21CreateFileInRegion(regionId){var r=regionId?world.regions.find(function(x){return x.id===regionId})||null:null;var cfg=await v21OpenFileDialog({title:'Novo arquivo',confirm:'Criar nota'});if(!cfg)return;var full=v21UniqueName(cfg.name,r&&r.id||null,null),parts=separarNomeExtNota(full,{ext:'.md'}),sem=semente(full+Date.now()),pos=r?vagaNaRegiao(r,sem,new Set()):vagaAleatoria(sem,3,3);if(!pos)return toast('Não encontrei lote livre.');var b={id:id('b'),kind:'building',regionId:r?r.id:null,x:pos.x,y:pos.y,w:3,h:3,name:parts.name,ext:parts.ext,description:cfg.description||'',content:v21DefaultContent(parts.ext),sprite:'house'+(world.buildings.length%3+1),tipo:'nota',tags:[],created:nowDate(),modified:nowDate(),aiLocal:{instructions:'',memory:'',artifacts:[]}};world.buildings.push(b);explorerRegionTarget=r?r.id:null;marcarIndice();indexar();counts();scheduleRoadRebuild();agendarSalvar();marcarSinc();v21BuildTree();irPara(b);pedirDesenho();openFullEditor(b)}
criarNotaNoDestino=v21CreateFileInRegion;
criarPastaNoDestino=async function(parentId){var nome=((await UD.prompt({title:'Nova pasta',label:'Nome da pasta',placeholder:'Ex.: Projetos',hint:'Cada pasta vira uma região da cidade.',confirm:'Criar pasta'}))||'').trim();if(!nome)return;var parent=parentId?world.regions.find(function(x){return x.id===parentId})||null:null,r=criarRegiaoOrganica(v21UniqueRegionName(nome),1,semente(nome+Date.now()),parent?parent.id:null,null,'');if(!r)return toast('Não encontrei área livre.');explorerRegionTarget=r.id;marcarIndice();indexar();counts();v21BuildTree();agendarSalvar();marcarSinc();toast('Pasta criada.')};

/* world pointer model: click region -> handles; hold region -> settings; hold building -> move */
cv.removeEventListener('pointerdown',canvasDownV20,true);cv.removeEventListener('pointermove',canvasMoveV20,true);cv.removeEventListener('pointerup',canvasUpV20,true);cv.removeEventListener('pointercancel',canvasCancelV20,true);
function v21CanvasDown(e){e.stopImmediatePropagation();try{cv.setPointerCapture(e.pointerId)}catch(_){ }pointers.set(e.pointerId,{x:e.clientX,y:e.clientY});clearTimeout(v20CanvasHold);clearTimeout(v21MoveHold);if(pointers.size===2){var ps=[...pointers.values()];gesture={mode:'pinch',dist:Math.hypot(ps[0].x-ps[1].x,ps[0].y-ps[1].y),z:camera.z};return}if(pointers.size!==1)return;var t=tileFromClient(e.clientX,e.clientY),handle=tool==='select'?v20HandleAt(e.clientX,e.clientY):null;if(handle&&v20RegionShape){gesture={mode:'resizeRegion',handle:handle,base:{...v20RegionShape.draft},sx:e.clientX,sy:e.clientY,moved:false};return}
  if(v21Placement){gesture={mode:'place',sx:e.clientX,sy:e.clientY,start:t,moved:false};v21PlacementCandidate={x:t.x,y:t.y};ghost=t;v21PlacementStatus();pedirDesenho();return}
  var hit=tool==='select'?bAt(t):null,rhit=tool==='select'&&!hit?regAt(t):null;gesture={mode:tool==='select'?'selectCandidate':tool,start:t,sx:e.clientX,sy:e.clientY,cx:camera.x,cy:camera.y,moved:false,building:hit,region:rhit,offX:hit?t.x-hit.x:0,offY:hit?t.y-hit.y:0,held:false};if(tool==='region')regionDraft={a:t,b:t};
  if(rhit&&!hit&&tool==='select')v20CanvasHold=setTimeout(function(){if(gesture&&gesture.region===rhit&&!gesture.moved){gesture.held=true;openRegionEditDialog(rhit);if(navigator.vibrate)navigator.vibrate(16)}},520);
  if(hit&&tool==='select')v21MoveHold=setTimeout(function(){if(gesture&&gesture.building===hit&&!gesture.moved){gesture.held=true;gesture.mode='moveBuilding';gesture.dragPos={x:hit.x,y:hit.y};if(navigator.vibrate)navigator.vibrate(16)}},430);
}
function v21CanvasMove(e){e.stopImmediatePropagation();if(!pointers.has(e.pointerId))return;var pp=pointers.get(e.pointerId);pp.x=e.clientX;pp.y=e.clientY;if(pointers.size===2&&gesture&&gesture.mode==='pinch'){clearTimeout(v20CanvasHold);clearTimeout(v21MoveHold);var ps=[...pointers.values()],d=Math.hypot(ps[0].x-ps[1].x,ps[0].y-ps[1].y);camera.z=clamp(gesture.z*d/gesture.dist,.22,2.8);counts();pedirDesenho();return}if(pointers.size!==1||!gesture)return;var t=tileFromClient(e.clientX,e.clientY),dx=e.clientX-gesture.sx,dy=e.clientY-gesture.sy;if(Math.abs(dx)+Math.abs(dy)>9){gesture.moved=true;clearTimeout(v20CanvasHold);if(gesture.mode!=='moveBuilding')clearTimeout(v21MoveHold)}
  if(gesture.mode==='resizeRegion'){v20RegionShape.draft=v20ResizeDraft(gesture.base,gesture.handle,t)}else if(gesture.mode==='place'){v21PlacementCandidate={x:t.x,y:t.y};ghost=t;v21PlacementStatus()}else if(gesture.mode==='selectCandidate'&&gesture.moved&&!gesture.building){gesture.mode='pan'}else if(gesture.mode==='pan'){camera.x=gesture.cx-dx/camera.z;camera.y=gesture.cy-dy/camera.z}else if(gesture.mode==='moveBuilding'&&gesture.held){gesture.dragPos={x:t.x-gesture.offX,y:t.y-gesture.offY}}else if(gesture.mode==='region'&&regionDraft)regionDraft.b=t;else if(gesture.mode==='house'){v21Placement={kind:'new'};v21PlacementCandidate={x:t.x,y:t.y};ghost=t;v21PlacementStatus()}pedirDesenho()}
function v21CanvasUp(e){e.stopImmediatePropagation();clearTimeout(v20CanvasHold);clearTimeout(v21MoveHold);pointers.delete(e.pointerId);if(!gesture||gesture.mode==='pinch'){if(!pointers.size)gesture=null;return}var t=tileFromClient(e.clientX,e.clientY),g=gesture;if(g.mode==='resizeRegion'){var m=v20MascaraPrioritaria(v20RegionShape.draft,v20RegionShape.r);if(m.cells.length){v20RegionShape.draft=m.bounds;v20AplicarMascaraRegiao(v20RegionShape.r,m)}}else if(g.mode==='place'){v21PlacementCandidate={x:t.x,y:t.y};ghost=t;v21PlacementStatus()}else if(g.mode==='moveBuilding'&&g.held&&g.dragPos&&g.building){finalizarMovimentoMundo(g.building,g.dragPos)}else if(g.held){}else if(g.mode==='selectCandidate'&&!g.moved){var b=bAt(t),r=regAt(t);if(v20RegionShape&&(!r||r.id!==v20RegionShape.r.id))v20SairShape(true);selected=b||r||null;if(b){if(b.tipo==='nota')openHouseSummary(b);else openFilePreview(b)}else if(r){closeHouseSummary();closeFilePreview();if(v20RegionShape&&v20RegionShape.r===r)v20SairShape(true);else v20EntrarShape(r)}else{closeHouseSummary();closeFilePreview()}}else if(g.mode==='pan'){}else if(g.mode==='region'&&regionDraft){var a=regionDraft.a,z=regionDraft.b;regionDraft=null;var x=Math.min(a.x,z.x),y=Math.min(a.y,z.y),w=Math.abs(a.x-z.x)+1,h=Math.abs(a.y-z.y)+1;if(w>=3&&h>=3)openRegionDialog({x:x,y:y,w:w,h:h});else toast('A região precisa ser maior.')}else if(g.mode==='house'){v21Placement={kind:'new'};v21PlacementCandidate={x:t.x,y:t.y};ghost=t;v21PlacementStatus()}gesture=null;pedirDesenho()}
function v21CanvasCancel(e){e.stopImmediatePropagation();clearTimeout(v20CanvasHold);clearTimeout(v21MoveHold);pointers.delete(e.pointerId);gesture=null;regionDraft=null;pedirDesenho()}
cv.addEventListener('pointerdown',v21CanvasDown,true);cv.addEventListener('pointermove',v21CanvasMove,true);cv.addEventListener('pointerup',v21CanvasUp,true);cv.addEventListener('pointercancel',v21CanvasCancel,true);
cv.addEventListener('wheel',function(e){e.preventDefault();e.stopImmediatePropagation();camera.z=clamp(camera.z*(e.deltaY<0?1.12:.89),.22,2.8);counts();pedirDesenho()},{passive:false,capture:true});

/* draw distinct editable-file buildings */
function v21DrawEditableBuilding(b){var e=v21Kind(b);if(e==='md'||e==='markdown'){drawSprite(A[b.sprite]||A.house1,b.x,b.y,b.w,b.h,1.18);return}var p=w2s(b.x*TILE,b.y*TILE),sz=TILE*camera.z,w=b.w*sz,h=b.h*sz,accent=v21FileAccent(e);ctx.save();ctx.imageSmoothingEnabled=false;ctx.fillStyle='#182229';ctx.fillRect(p.x,p.y+h*.30,w,h*.70);ctx.fillStyle=accent;ctx.fillRect(p.x+w*.06,p.y+h*.18,w*.88,h*.18);ctx.fillStyle='#ced8d7';ctx.fillRect(p.x+w*.17,p.y+h*.47,w*.66,h*.27);ctx.fillStyle='#0c1418';ctx.fillRect(p.x+w*.23,p.y+h*.54,w*.18,h*.20);ctx.fillRect(p.x+w*.58,p.y+h*.54,w*.18,h*.20);if(e==='html'){ctx.fillStyle='#f0d8c9';ctx.fillRect(p.x+w*.39,p.y+h*.40,w*.22,h*.10)}else if(e==='js'||e==='mjs'){ctx.fillStyle='#e8d76f';ctx.fillRect(p.x+w*.43,p.y+h*.39,w*.14,h*.14)}else if(e==='css'){ctx.fillStyle='#759ee0';ctx.fillRect(p.x+w*.33,p.y+h*.39,w*.34,h*.08)}else if(e==='json'||e==='yaml'||e==='yml'){ctx.fillStyle='#ae826d';ctx.fillRect(p.x+w*.29,p.y+h*.39,w*.42,h*.08)}else if(e==='csv'){ctx.strokeStyle='#7fb795';ctx.lineWidth=Math.max(1,camera.z*2);ctx.strokeRect(p.x+w*.31,p.y+h*.39,w*.38,h*.12)}ctx.restore()}
var v21OldDrawBuildings=drawBuildings;drawBuildings=function(){var f=faixaVisivel();for(var i=0;i<world.buildings.length;i++){var b=world.buildings[i];if(b.x>f.x1||b.x+b.w<f.x0||b.y>f.y1||b.y+b.h<f.y0)continue;if(b.tipo==='nota')v21DrawEditableBuilding(b);else drawArquivoBuilding(b);if(selected===b){var p=w2s(b.x*TILE,b.y*TILE);ctx.strokeStyle='#fff';ctx.lineWidth=2;ctx.strokeRect(p.x,p.y,b.w*TILE*camera.z,b.h*TILE*camera.z)}if(camera.z>=.72){var q=w2s((b.x+b.w/2)*TILE,(b.y+b.h)*TILE),raw=nomeExibidoArquivo(b),label=raw.length>34?raw.slice(0,31)+'…':raw;ctx.save();ctx.font=Math.max(9,Math.round(10*camera.z))+'px ui-monospace';ctx.textAlign='center';ctx.textBaseline='top';var mw=ctx.measureText(label).width,pad=4*camera.z,yy=q.y+4*camera.z;ctx.fillStyle='#081017c9';ctx.fillRect(q.x-mw/2-pad,yy-2,mw+pad*2,Math.max(14,13*camera.z));ctx.fillStyle='#eef5f8';ctx.fillText(label,q.x,yy);ctx.restore()}}};
var v21OldDrawOverlay=drawOverlay;drawOverlay=function(){v21OldDrawOverlay();if(v21Placement&&v21PlacementCandidate){var p=v21PlacementCandidate,src=v21Placement.source,ok=src?validarPosicaoMovel(src,p.x,p.y).ok:canHouse(p),sp=w2s(p.x*TILE,p.y*TILE);ctx.save();ctx.globalAlpha=.62;if(src&&src.tipo==='nota')v21DrawEditableBuilding({...src,x:p.x,y:p.y});else drawSprite(A.house1,p.x,p.y,3,3,1.18);ctx.globalAlpha=1;ctx.strokeStyle=ok?'#7ef29a':'#ef766f';ctx.lineWidth=3;ctx.strokeRect(sp.x,sp.y,3*TILE*camera.z,3*TILE*camera.z);ctx.restore()}};

/* improved terrain and map: coherent low-frequency biomes + water/coast detail */
var v21OldDrawGround=drawGround,v21Ruido=new Map(),V21_RUIDO_CH=16;
/* textura suave do chão: cada quadradinho (step×step tiles) é um pixel de uma imagem pequena
   por bloco de 16×16 tiles, feita uma vez e ampliada sem suavizar (antes: centenas de
   retângulos e cálculos de ruído a cada quadro) */
function v21RuidoBloco(bx,by,step){
  var k=bx+','+by+','+step,c=v21Ruido.get(k);if(c)return c;
  var n=V21_RUIDO_CH/step;c=document.createElement('canvas');c.width=c.height=n;var g=c.getContext('2d'),im=g.createImageData(n,n),d=im.data;
  for(var j=0;j<n;j++)for(var i=0;i<n;i++){var x=bx*V21_RUIDO_CH+i*step,y=by*V21_RUIDO_CH+j*step;if(ehAgua(x,y))continue;
    var v=(suave(x*.42,y*.42,23)+suave(x*.18,y*.18,47)*.6)/1.6,o=(j*n+i)*4;
    if(v<.34){d[o]=0x8a;d[o+1]=0x7a;d[o+2]=0x4b;d[o+3]=255}else if(v>.69){d[o]=0x35;d[o+1]=0x5b;d[o+2]=0x34;d[o+3]=255}}
  g.putImageData(im,0,0);if(v21Ruido.size>3000)v21Ruido.clear();v21Ruido.set(k,c);return c;
}
drawGround=function(){v21OldDrawGround();if(camera.z<.28)return;var f=faixaVisivel(),step=camera.z<.45?4:2,L=V21_RUIDO_CH,lado=L*TILE*camera.z;
  ctx.save();ctx.globalAlpha=.11;ctx.imageSmoothingEnabled=false;
  for(var by=Math.floor(f.y0/L);by<=Math.floor(f.y1/L);by++)for(var bx=Math.floor(f.x0/L);bx<=Math.floor(f.x1/L);bx++){var p=w2s(bx*L*TILE,by*L*TILE);ctx.drawImage(v21RuidoBloco(bx,by,step),p.x,p.y,lado,lado)}
  ctx.restore()};
pintarMapa=function(c,L,W2,H2,detalhe){var esc=Math.min(W2/(L.x1-L.x0),H2/(L.y1-L.y0)),px=function(x){return(x-L.x0)*esc},py=function(y){return(y-L.y0)*esc};c.fillStyle='#263b26';c.fillRect(0,0,W2,H2);var step=Math.max(1,Math.ceil(2/Math.max(.001,esc)));for(var y=Math.floor(L.y0);y<L.y1;y+=step)for(var x=Math.floor(L.x0);x<L.x1;x+=step){var sx=px(x),sy=py(y),sz=Math.max(1,step*esc+.5);if(ehAgua(x,y))c.fillStyle='#234d5a';else{var n=suave(x*.35,y*.35,31);c.fillStyle=n<.35?'#776b43':n>.70?'#2f5130':'#385d34'}c.fillRect(sx,sy,sz,sz)}
  for(var ri=0;ri<world.regions.length;ri++){var r=world.regions[ri];c.globalAlpha=.28;c.fillStyle=r.color;if(r.cells)for(var ci=0;ci<r.cells.length;ci++){var a=r.cells[ci].split(',');c.fillRect(px(+a[0]),py(+a[1]),Math.max(1,esc),Math.max(1,esc))}else c.fillRect(px(r.x),py(r.y),r.w*esc,r.h*esc);c.globalAlpha=.9;c.strokeStyle=r.color;c.lineWidth=1;c.strokeRect(px(r.x),py(r.y),r.w*esc,r.h*esc);if(detalhe&&r.w*esc>45&&!r.parentId){c.fillStyle='#e0ece5';c.font='600 10px '+(typeof URBE_FONTE==='string'?URBE_FONTE:'sans-serif');c.fillText(r.name,px(r.x)+3,py(r.y)+10)}}c.globalAlpha=1;
  c.fillStyle='#a98a57';for(var road of world.roads){var q=road.split(',');c.fillRect(px(+q[0]),py(+q[1]),Math.max(1,esc),Math.max(1,esc))}for(var bi=0;bi<world.buildings.length;bi++){var b=world.buildings[bi],ex=b.tipo==='nota'?v21Kind(b):'asset';c.fillStyle=b.tipo==='nota'?v21FileAccent(ex):'#b8a16c';var z=Math.max(2,Math.min(5,3*esc));c.fillRect(px(b.x),py(b.y),z,z)}var a0=s2w(0,0),a1=s2w(cv.w,cv.h);c.strokeStyle='#ecf5f7';c.lineWidth=1.5;c.strokeRect(px(a0.x/TILE),py(a0.y/TILE),(a1.x-a0.x)/TILE*esc,(a1.y-a0.y)/TILE*esc);return{esc:esc,px:px,py:py}};

/* persistence: every supported textual file is first-class */
var estadoDesejado=function(){var cam=caminhosRegioes(),arquivos=new Map(),pastas=new Set(),usados=new Set(),usadosBin=new Set(),notas={},binarios=new Map();world.regions.forEach(function(r){pastas.add(cam.get(r.id))});world.buildings.forEach(function(b){if(b.tipo!=='nota')return;var dir=b.regionId?(cam.get(b.regionId)||''):'',base=nomeSeguro(b.name),ext=extNota(b),rel=(dir?dir+'/':'')+base+ext,n=2;while(usados.has(rel.toLowerCase()))rel=(dir?dir+'/':'')+base+' ('+(n++)+')'+ext;usados.add(rel.toLowerCase());arquivos.set(rel,b.content||'');notas[rel]={x:b.x,y:b.y,sprite:b.sprite,tags:b.tags||[],anexos:(b.anexos||[]).map(metaAnexoNota),criado:b.created||nowDate(),modificado:b.modified||nowDate(),aiLocal:b.aiLocal||null}});var construcoes=[];world.buildings.forEach(function(b){if(b.tipo==='nota')return;var fallbackDir=b.regionId?(cam.get(b.regionId)||''):'',src=b.files||b.anexos||[],filesOut=[];src.forEach(function(a){var dir=typeof a.folderPath==='string'?a.folderPath:fallbackDir,rel=relBinUnico(dir,a.nome||b.fileName||b.name,usadosBin);filesOut.push(metaAssetLimpa(a,rel));binarios.set(rel,{asset:a,sourceRel:a.relPath||null})});var pai=b.parentNoteId?world.buildings.find(function(n){return n.id===b.parentNoteId}):null;construcoes.push({id:window.UrbeStableIds.ensure(b,'ast'),parentId:pai&&pai.documentId||null,tipo:b.tipo,fileClass:b.fileClass||'other',name:b.name,fileName:b.fileName||null,caminho:fallbackDir,x:b.x,y:b.y,w:b.w||3,h:b.h||3,description:b.description||'',sprite:b.sprite||('file-'+(b.fileClass||'other')),parentNoteName:b.parentNoteId?(world.buildings.find(function(n){return n.id===b.parentNoteId})||{}).name||null:null,files:filesOut,anexos:filesOut,created:b.created||nowDate(),modified:b.modified||nowDate()})});var mapa={v:4,app:APP_NOME,version:V21_VERSION,mundo:URBE_MUNDO,salvo:new Date().toISOString(),camera:{x:camera.x,y:camera.y,z:camera.z},regioes:world.regions.map(function(r){var pr=r.parentId?world.regions.find(function(x){return x.id===r.parentId}):null;return{id:window.UrbeStableIds.ensure(r,'reg'),parentId:pr?window.UrbeStableIds.ensure(pr,'reg'):null,caminho:cam.get(r.id),nome:r.name,cor:r.color,x:r.x,y:r.y,w:r.w,h:r.h,cells:r.cells||null,descricao:r.description||''}}),notas:notas,construcoes:construcoes};arquivos.set('.urbe/mapa.json',JSON.stringify(mapa,null,1));return{arquivos:arquivos,pastas:pastas,binarios:binarios}};

async function v21OpenCity(nome){sincSuspenso=true;v21SetLoading(true,7,'Lendo vault');try{var rels=await FS.listar(nome),texts=rels.filter(function(r){return v21IsEditablePath(r)&&!r.startsWith('.urbe/')&&!r.split('/').some(function(p){return p.startsWith('.')})});v21SetLoading(true,23,'Indexando arquivos');var mapa=null,pm=window.UrbeCore.service('persistence');/* o mapa vem do load da persistência (já reconciliado com renames externos, REQ-042) */if(pm&&pm.vault===nome)mapa=pm.meta&&Object.keys(pm.meta).length?JSON.parse(JSON.stringify(pm.meta)):null;else try{mapa=JSON.parse(await FS.ler(nome,'.urbe/mapa.json')||'null')}catch(_){ }var sids=window.UrbeStableIds.assign(mapa),usedIds=new Set([...sids.regions.values(),...sids.assets]),geoReg=new Map(),geoNota=new Map();if(mapa){(mapa.regioes||[]).forEach(function(r){geoReg.set(r.caminho,r)});Object.keys(mapa.notas||{}).forEach(function(k){geoNota.set(k,mapa.notas[k])})}var conteudos=new Map();for(var i=0;i<texts.length;i++){conteudos.set(texts[i],(await FS.ler(nome,texts[i]))||'');if(i%8===0)v21SetLoading(true,23+Math.round(27*(i/Math.max(1,texts.length))),'Lendo '+(i+1)+' / '+texts.length)}
    world.regions.length=0;world.buildings.length=0;world.roads.clear();world.links.length=0;selected=null;currentFile=null;idxB=new Map();idxR=new Map();var caminhos=new Set();(mapa&&mapa.regioes||[]).forEach(function(r){if(r.caminho)caminhos.add(r.caminho)});texts.forEach(function(rel){var d=dirDe(rel);while(d){caminhos.add(d);d=dirDe(d)}});var ordenados=[...caminhos].sort(function(a,b){return a.split('/').length-b.split('/').length||a.localeCompare(b)}),regPorCaminho=new Map();ordenados.forEach(function(cam){var paiCam=dirDe(cam),pai=paiCam?regPorCaminho.get(paiCam):null,g=geoReg.get(cam),nomeR=g&&g.nome||cam.split('/').pop(),quantos=(texts.filter(function(r){return r.indexOf(cam+'/')===0}).length+ordenados.filter(function(c){return c.indexOf(cam+'/')===0}).length*2)||1,r;if(g&&typeof g.x==='number'&&g.w>0){r={id:id('r'),uid:g.id,kind:'region',x:g.x,y:g.y,w:g.w,h:g.h,cells:g.cells||null,name:nomeR,description:g.descricao||'',parentId:pai?pai.id:null,color:g.cor||colors[world.regions.length%colors.length]};if(r.cells)r._cellSet=new Set(r.cells);world.regions.push(r);indexarUm(idxR,r)}else r=criarRegiaoOrganica(nomeR,quantos,semente(cam+quantos),pai?pai.id:null,urbeOrigemPelasCasas(cam,texts,geoNota),'');if(r&&!r.uid)r.uid=g&&g.id||window.UrbeStableIds.regionFor(cam,usedIds);regPorCaminho.set(cam,r)});v21SetLoading(true,58,'Construindo cidade');var semLugar=[];texts.forEach(function(rel){var cam=dirDe(rel),r=cam?regPorCaminho.get(cam):null,g=geoNota.get(rel),ext=(rel.match(V21_EXT_RE)||['.md'])[0].toLowerCase(),b={id:id('b'),kind:'building',regionId:r?r.id:null,x:0,y:0,w:3,h:3,name:v21Stem(rel.split('/').pop()),ext:ext,description:'',content:conteudos.get(rel)||'',sprite:g&&g.sprite||['house1','house2','house3'][semente(rel)%3],tipo:'nota',tags:g&&g.tags||[],anexos:g&&g.anexos||[],created:g&&g.criado||nowDate(),modified:g&&g.modificado||nowDate(),aiLocal:g&&g.aiLocal||{instructions:'',memory:'',artifacts:[]},documentId:g&&g.id||null};if(g&&typeof g.x==='number'){b.x=g.x;b.y=g.y;world.buildings.push(b);indexarUm(idxB,b)}else semLugar.push({b:b,r:r,rel:rel})});semLugar.forEach(function(o){var pos=o.r?posicaoAleatoriaNaRegiao(o.r,semente(o.rel)):vagaAleatoria(semente(o.rel),3,3);o.b.x=pos?pos.x:0;o.b.y=pos?pos.y:0;world.buildings.push(o.b);indexarUm(idxB,o.b)});(mapa&&mapa.construcoes||[]).forEach(function(g){var r=g.caminho?regPorCaminho.get(g.caminho)||null:null,paiNota=(g.parentId?world.buildings.find(function(n){return n.tipo==='nota'&&n.documentId===g.parentId}):null)||(g.parentNoteName?world.buildings.find(function(n){return n.tipo==='nota'&&n.name===g.parentNoteName}):null),b={id:id('b'),uid:g.id,kind:'building',regionId:r?r.id:null,x:g.x||0,y:g.y||0,w:g.w||3,h:g.h||3,name:g.name||g.fileName||'Arquivo',fileName:g.fileName||null,description:g.description||'',content:'',sprite:g.sprite||('file-'+(g.fileClass||'other')),tipo:g.tipo||'arquivo',fileClass:g.fileClass||'other',parentNoteId:paiNota&&paiNota.id||null,files:g.files||g.anexos||[],anexos:g.anexos||g.files||[],tags:[],created:g.created||nowDate(),modified:g.modified||nowDate()};world.buildings.push(b);indexarUm(idxB,b)});if(mapa&&mapa.camera)camera={...camera,...mapa.camera};camera.z=clamp(camera.z,.22,2.8);Disco.cidade=nome;Disco.hCidade=null;snapArq=new Map();snapPastas=new Set(caminhos);texts.forEach(function(rel){snapArq.set(rel,conteudos.get(rel)||'')});var mj=await FS.ler(nome,'.urbe/mapa.json');if(mj!=null)snapArq.set('.urbe/mapa.json',mj);v21SetLoading(true,76,'Sincronizando mundo');await v21IndexBinary(nome,rels,regPorCaminho,mapa);
    if(mapa&&mapa.mundo!==URBE_MUNDO&&world.buildings.length){v21SetLoading(true,86,'Adaptando a cidade ao mundo novo');await new Promise(function(r){setTimeout(r,30)});try{urbeReorganizarCidade({carregando:true,centro:urbeInicioDoMundo()});urbeCidadeMigrada=true}catch(eMig){console.warn('migração do mundo',eMig)}}
    v21SetLoading(true,92,'Traçando as ruas');await new Promise(function(r){setTimeout(r,30)});marcarIndice();indexar();try{if(urbeCasasNosBairros()+urbeTaparTodos()){indexar();agendarSalvar()}}catch(e){console.warn('casas nos bairros',e)}rebuildRoadNetwork();counts();v21BuildTree();pedirDesenho();await DBK.set('ultimaCidade',nome);fecharMenu();sincSuspenso=false;statusSinc('ok');marcarSinc();v21SetLoading(true,100,'Pronto');setTimeout(function(){v21SetLoading(false)},130);if(urbeCidadeMigrada){urbeCidadeMigrada=false;agendarSalvar();setTimeout(function(){try{urbeEnquadrarNotas(true)}catch(_){}toast('O mundo do Urbe mudou: sua cidade foi reorganizada no terreno novo. Notas, pastas e ligações continuam iguais.')},700)}
  }catch(e){console.warn(e);sincSuspenso=true;v21SetLoading(false);toast('Não consegui abrir: '+(e&&e.message||e))}}
async function v21IndexBinary(nome,rels,regPorCaminho,mapa){snapBin=new Map();var existingByRel=new Set();world.buildings.filter(function(b){return b.tipo!=='nota'}).forEach(function(b){(b.files||b.anexos||[]).forEach(function(a){if(a.relPath)existingByRel.add(a.relPath.toLowerCase())})});var assets=rels.filter(function(r){return !v21IsEditablePath(r)&&!r.startsWith('.urbe/')&&!r.split('/').some(function(p){return p.startsWith('.')})&&!/\/\.pasta$|^\.pasta$/.test(r)});for(var i=0;i<assets.length;i++){var rel=assets[i];if(existingByRel.has(rel.toLowerCase()))continue;var dir=dirDe(rel),r=dir?regPorCaminho.get(dir)||null:null,pos=r?vagaNaRegiao(r,semente(rel),new Set()):vagaAleatoria(semente(rel),3,3);if(!pos)continue;var nomeA=rel.split('/').pop(),meta={nome:nomeA,tipo:(nomeA.split('.').pop()||'').toLowerCase(),mime:mimePorNome(nomeA),relPath:rel,folderPath:dir};try{var blob=await FS.lerBlob(nome,rel);if(blob){meta.tamanho=blob.size;meta.mime=blob.type||meta.mime;meta.cacheId=cacheAssetKey();try{await DBK.bSet(meta.cacheId,blob)}catch(_){delete meta.cacheId}}}catch(_){ }criarPredioArquivo({rel:rel,nome:v21Stem(nomeA),anexo:meta},pos.x,pos.y,r?r.id:null,{files:[meta],fileClass:classificarArquivo(nomeA),name:nomeA})}}
abrirCidade=v21OpenCity;

/* import textual formats as editable buildings */
var v21OriginalLerEntrada=lerEntrada;lerEntrada=async function(files,opts){var raw=await v21OriginalLerEntrada(files,opts);for(var i=0;i<raw.length;i++){var it=raw[i];if(v21IsEditablePath(it.rel)&&it.texto==null&&it.anexo){try{var blob=it.anexo.cacheId?await DBK.bGet(it.anexo.cacheId):null;if(blob){it.texto=await blob.text();delete it.anexo}}catch(_){}}}return raw};
var v21OldImportarItens=importarItens;importarItens=function(itens,opcoes){var texts=itens.filter(function(i){return v21IsEditablePath(i.rel)}),bins=itens.filter(function(i){return !v21IsEditablePath(i.rel)});/* temporarily make supported text look like MD to legacy distributor, then restore extension */var patched=texts.map(function(i){return{...i,rel:i.rel.replace(V21_EXT_RE,'.md'),_v21Rel:i.rel}}),result=v21OldImportarItens(patched.concat(bins),opcoes||{}),byStem=new Map(texts.map(function(i){return[v21Stem(i.rel.split('/').pop()).toLowerCase(),i]}));world.buildings.filter(function(b){return b.tipo==='nota'}).forEach(function(b){var src=byStem.get(String(b.name).toLowerCase());if(src){b.ext=(src.rel.match(V21_EXT_RE)||['.md'])[0].toLowerCase();b.content=src.texto||''}});marcarSinc();v21BuildTree();return result};

/* Explorer v0.21 */
function v21EnsureExplorer(){var top=document.querySelector('#fileSidebar .sidebarTop'),legacy=document.getElementById('explorerTopActions');if(legacy)legacy.remove();if(!document.getElementById('v21ExplorerToolbar')){var bar=document.createElement('div');bar.id='v21ExplorerToolbar';bar.innerHTML='<button class="v21ExBtn" id="v21ExNew" title="Novo arquivo">＋</button><button class="v21ExBtn" id="v21ExFolder" title="Nova pasta">▣</button><button class="v21ExBtn" id="v21ExImport" title="Importar">⇩</button><button class="v21ExBtn" id="v21ExMulti" title="Multiseleção">✓</button><button class="v21ExBtn" id="v21ExMove" title="Mover seleção" hidden>↪</button><button class="v21ExBtn danger" id="v21ExDelete" title="Excluir seleção" hidden>×</button>';top.insertBefore(bar,document.getElementById('closeSidebar'));document.getElementById('v21ExNew').onclick=function(){v21CreateFileInRegion(explorerRegionTarget||null)};document.getElementById('v21ExFolder').onclick=function(){criarPastaNoDestino(explorerRegionTarget||null)};document.getElementById('v21ExImport').onclick=function(){v21ImportPicker(explorerRegionTarget||null)};document.getElementById('v21ExMulti').onclick=function(){v21MultiMode=!v21MultiMode;if(!v21MultiMode)v20EntrySelection.clear();v21BuildTree()};document.getElementById('v21ExMove').onclick=v21MoveSelected;document.getElementById('v21ExDelete').onclick=v21DeleteSelected}
  if(!document.getElementById('v21ExplorerSearch')){var s=document.createElement('input');s.id='v21ExplorerSearch';s.type='search';s.placeholder='Buscar arquivos e pastas';s.oninput=function(){v21ExplorerQuery=s.value.trim().toLowerCase();v21BuildTree()};document.getElementById('explorerContext').insertAdjacentElement('afterend',s)}
  document.getElementById('explorerRootBtn').onclick=function(){explorerRegionTarget=null;v21BuildTree()};
}
function v21IconHtml(e){if(e.kind==='folder')return '<span class="v21FolderIcon"></span>';if(e.kind==='note'){var ext=v21Kind(e.b);return '<span class="v21FileIcon" style="--file-accent:'+v21FileAccent(ext)+'"></span>'}return '<span class="v21FileIcon" style="--file-accent:#9b7d63"></span>'}

function v21BuildTree(){v21EnsureExplorer();var treeEl=document.getElementById('tree'),rootName=Disco.cidade||'Vault',rootBtn=document.getElementById('explorerRootBtn');if(rootBtn)rootBtn.textContent=rootName;var paths=caminhosRegioes(),target=explorerRegionTarget?world.regions.find(function(r){return r.id===explorerRegionTarget}):null;document.getElementById('explorerPath').textContent=target?(paths.get(target.id)||target.name):'/';var q=v21ExplorerQuery;
  function matchName(s){return !q||String(s||'').toLowerCase().includes(q)}function children(pid){return world.regions.filter(function(r){return(r.parentId||null)===(pid||null)}).sort(function(a,b){return a.name.localeCompare(b.name)})}function entries(pid){return explorerEntries(pid).filter(function(e){return matchName(e.name)})}
  var html='';function branch(r,d){var kids=children(r.id),ens=entries(r.id),visible=matchName(r.name)||ens.length||kids.some(function(k){return matchName(k.name)});if(q&&!visible)return'';var e={kind:'folder',r:r};var open=!explorerCollapsed.has(r.id)||!!q;return '<div><button class="v21TreeRow '+(explorerRegionTarget===r.id?'target ':'')+'" style="--depth:'+d+'" data-v21-region="'+r.id+'"><span class="v21TreeIcon">'+v21IconHtml(e)+'</span><span class="v21TreeName">'+escapeHTML(r.name)+'</span><span class="v21TreeMeta">'+(kids.length+ens.length||'')+'</span></button>'+(open?kids.map(function(x){return branch(x,d+1)}).join('')+ens.map(function(x){return row(x,d+1)}).join(''):'')+'</div>'}function row(e,d){var key=v20EntryKey(e),sel=v20EntrySelection.has(key);return '<button class="v21TreeRow '+(sel?'selected ':'')+'" style="--depth:'+d+'" data-v21-entry="'+escapeHTML(key)+'"><span class="v21TreeIcon">'+v21IconHtml(e)+'</span><span class="v21TreeName">'+escapeHTML(e.name)+'</span><span class="v21TreeMeta"></span></button>'}
  children(null).forEach(function(r){html+=branch(r,0)});entries(null).forEach(function(e){html+=row(e,0)});if(!html)html='<div class="v21TreeEmpty">Vault vazio</div>';treeEl.innerHTML=html;treeEl.querySelectorAll('[data-v21-region]').forEach(function(el){v21BindFolder(el,world.regions.find(function(r){return r.id===el.dataset.v21Region}))});treeEl.querySelectorAll('[data-v21-entry]').forEach(function(el){var e=v20EntryFromKey(el.dataset.v21Entry);if(e)v21BindEntry(el,e)});var has=v20EntrySelection.size>0;document.getElementById('v21ExMulti').classList.toggle('active',v21MultiMode);document.getElementById('v21ExMove').hidden=!has;document.getElementById('v21ExDelete').hidden=!has}
buildTree=v21BuildTree;
function v21BindFolder(el,r){var st=null;el.onpointerdown=function(ev){if(ev.pointerType==='mouse'&&ev.button!==0)return;st={x:ev.clientX,y:ev.clientY,id:ev.pointerId,moved:false,t:setTimeout(function(){if(st&&!st.moved){st.held=true;openRegionEditDialog(r);if(navigator.vibrate)navigator.vibrate(12)}},520)}};el.onpointermove=function(ev){if(st&&Math.hypot(ev.clientX-st.x,ev.clientY-st.y)>8){st.moved=true;clearTimeout(st.t)}};el.onpointerup=function(){if(!st)return;clearTimeout(st.t);var old=st;st=null;if(old.held||old.moved)return;explorerRegionTarget=r.id;v21BuildTree()};el.onpointercancel=function(){if(st)clearTimeout(st.t);st=null}}
function v21BindEntry(el,e){var st=null;el.onpointerdown=function(ev){if(ev.pointerType==='mouse'&&ev.button!==0)return;st={x:ev.clientX,y:ev.clientY,id:ev.pointerId,moved:false,t:setTimeout(function(){if(st&&!st.moved){st.held=true;v21MoveEntry(e);if(navigator.vibrate)navigator.vibrate(12)}},520)}};el.onpointermove=function(ev){if(st&&Math.hypot(ev.clientX-st.x,ev.clientY-st.y)>9){st.moved=true;clearTimeout(st.t)}};el.onpointerup=function(){if(!st)return;clearTimeout(st.t);var old=st;st=null;if(old.held||old.moved)return;var key=v20EntryKey(e);if(v21MultiMode){v20EntrySelection.has(key)?v20EntrySelection.delete(key):v20EntrySelection.add(key);v21BuildTree();return}if(e.kind==='note'){explorerRegionTarget=e.b.regionId||null;openFullEditor(e.b)}else openFilePreview(e.b,e.index)};el.onpointercancel=function(){if(st)clearTimeout(st.t);st=null}}
function v21ChooseDestination(cb){var acts=[{ico:'⌂',label:Disco.cidade||'Raiz',run:function(){cb(null)}}].concat(world.regions.slice().sort(function(a,b){return(caminhosRegioes().get(a.id)||a.name).localeCompare(caminhosRegioes().get(b.id)||b.name)}).map(function(r){return{ico:'▣',label:caminhosRegioes().get(r.id)||r.name,run:function(){cb(r.id)}}}));abrirMenuExplorer('Mover para…',acts)}
function v21MoveEntry(e){v21ChooseDestination(function(dest){moverEntradaPara(e,dest);v21BuildTree();toast('Movido.')})}
function v21MoveSelected(){var arr=v20SelectedEntries();if(!arr.length)return;v21ChooseDestination(function(dest){arr.forEach(function(e){moverEntradaPara(e,dest)});v20EntrySelection.clear();v21MultiMode=false;v21BuildTree();toast(arr.length+' item(ns) movido(s).')})}
async function v21DeleteSelected(){var arr=v20SelectedEntries();if(!arr.length)return;if(!(await UD.confirm({title:'Excluir '+arr.length+' item(ns)?',message:'Os itens selecionados serão excluídos do vault.',confirm:'Excluir',danger:true})))return;var ids=new Set(arr.filter(function(e){return e.kind==='note'}).map(function(e){return e.b.id}));arr.filter(function(e){return e.kind==='asset'}).forEach(function(e){var fs=e.b.files||e.b.anexos||[];fs.splice(e.index,1);if(!fs.length)ids.add(e.b.id)});world.buildings=world.buildings.filter(function(b){return !ids.has(b.id)});v20EntrySelection.clear();v21MultiMode=false;marcarIndice();indexar();counts();scheduleRoadRebuild();agendarSalvar();marcarSinc();v21BuildTree();pedirDesenho();toast('Excluído.')}
async function v21ImportPicker(target){pendingImportTarget=target||null;if(typeof window.showDirectoryPicker==='function'){var choose=await v21ChooseImportMode();if(choose==='folder'){try{var h=await window.showDirectoryPicker({mode:'read'}),entries=[];async function walk(dir,prefix){for await(var ch of dir.values()){if(ch.kind==='file')entries.push({file:await ch.getFile(),rel:prefix+ch.name});else await walk(ch,prefix+ch.name+'/')}}await walk(h,'');await importarEntradasDestino(entries,true,h.name,pendingImportTarget)}catch(e){if(e&&e.name!=='AbortError')toast('Falha ao importar pasta.')}return}if(choose!=='files')return}return abrirImportacaoSistema(pendingImportTarget)}
function v21ChooseImportMode(){return new Promise(function(resolve){abrirMenuExplorer('Importar',[{ico:'▣',label:'Pasta completa',run:function(){resolve('folder')}},{ico:'⇩',label:'Arquivos',run:function(){resolve('files')}}]);setTimeout(function(){var sh=document.getElementById('explorerActionSheet');if(sh)sh.addEventListener('pointerdown',function h(e){if(e.target===sh){sh.removeEventListener('pointerdown',h);resolve(null)}},{once:true})},0)})}

/* Editor tabs + adaptive behavior */
var v21BaseLoadFile=loadFile;loadFile=function(b){if(!b.ext)b.ext='.md';v21BaseLoadFile(b);v21TouchTab(b);v21RefreshEditorChrome()};
openFullEditor=function(b){closeHouseSummary();editorFull.classList.add('open');v21TouchTab(b);loadFile(b);setEditorViewMode(v21CanPreview(b)&&urbeOpcoes.editor!=='texto'?'preview':'source')};
closeFullEditor=function(){if(editorViewMode==='preview')syncVisualToMarkdown();editorFull.classList.remove('open');fileSidebar.classList.remove('open');currentFile=null;v21RenderTabs()};document.getElementById('closeFullEditor').onclick=closeFullEditor;
function v21TouchTab(b){if(!b)return;v21OpenTabs=v21OpenTabs.filter(function(x){return world.buildings.includes(x)});if(!v21OpenTabs.includes(b))v21OpenTabs.push(b);v21RenderTabs()}
function v21RenderTabs(){var box=document.getElementById('v21EditorTabs');if(!box)return;box.innerHTML=v21OpenTabs.map(function(b){return '<button class="v21Tab '+(b===currentFile?'active':'')+'" data-tab="'+b.id+'"><span class="v21TabLabel">'+escapeHTML(nomeCompletoNota(b))+'</span><span class="v21TabClose" data-close-tab="'+b.id+'">×</span></button>'}).join('');box.querySelectorAll('[data-tab]').forEach(function(t){t.onclick=function(e){var close=e.target.closest('[data-close-tab]'),b=world.buildings.find(function(x){return x.id===t.dataset.tab});if(close){e.stopPropagation();v21OpenTabs=v21OpenTabs.filter(function(x){return x.id!==close.dataset.closeTab});if(currentFile&&currentFile.id===close.dataset.closeTab){var next=v21OpenTabs[v21OpenTabs.length-1];if(next)loadFile(next);else closeFullEditor()}v21RenderTabs();return}if(b&&b!==currentFile){if(editorViewMode==='preview')syncVisualToMarkdown();loadFile(b)}}})}
function v21CanPreview(b){var e=v21Kind(b);return e==='md'||e==='markdown'||e==='html'||e==='htm'}
function v21RefreshEditorChrome(){if(!currentFile)return;var e=v21Kind(currentFile),isMd=e==='md'||e==='markdown',can=v21CanPreview(currentFile);document.getElementById('mdToolbar').style.display=isMd?'flex':'none';document.getElementById('v21CodeTools').classList.toggle('open',!isMd);viewModeBtn.style.display=can?'grid':'none';document.getElementById('editorStats').textContent=e.toUpperCase()+' · '+bodyEditor.value.split('\n').length+' linhas';fileNameDisplay.textContent=nomeCompletoNota(currentFile);document.getElementById('documentWatermark').textContent=nomeCompletoNota(currentFile);v21RenderTabs()}
var v21BaseUpdateStats=updateStats;updateStats=function(){if(!currentFile)return v21BaseUpdateStats();var txt=bodyEditor.value.trim(),words=txt?txt.split(/\s+/).length:0,lines=bodyEditor.value.split('\n').length,e=v21Kind(currentFile).toUpperCase();document.getElementById('editorStats').textContent=e+' · '+lines+' linhas · '+words+' palavras'};
var v21BaseSync=syncVisualToMarkdown;syncVisualToMarkdown=function(){if(visualSyncLock||editorViewMode!=='preview'||!currentFile)return;var before=currentFile.content||'',e=v21Kind(currentFile),next;if(e==='html'||e==='htm')return;next=markdownFromVisual(renderedPreview);if(next===before){bodyEditor.value=next;return}bodyEditor.value=next;currentFile.content=next;currentFile.modified=nowDate();if(assinaturaLinks(before)!==assinaturaLinks(next))scheduleRoadRebuild();agendarSalvar();document.getElementById('saveState').textContent='Salvando...';clearTimeout(saveTimer);saveTimer=setTimeout(function(){document.getElementById('saveState').textContent='Salvo'},450);updateStats()};
var v21BaseRenderCurrent=renderCurrentPreview;renderCurrentPreview=function(){if(currentFile&&(v21Kind(currentFile)==='html'||v21Kind(currentFile)==='htm')){visualSyncLock=true;renderedPreview.innerHTML='';var fr=document.createElement('iframe');fr.setAttribute('sandbox','');fr.setAttribute('referrerpolicy','no-referrer');fr.srcdoc=bodyEditor.value;renderedPreview.appendChild(fr);renderedPreview.contentEditable='false';visualSyncLock=false;return}renderedPreview.contentEditable='true';v21BaseRenderCurrent()};
setEditorViewMode=function(mode){var can=currentFile?v21CanPreview(currentFile):true;if(mode==='preview'&&!can)mode='source';if(editorViewMode==='preview'&&mode!=='preview')syncVisualToMarkdown();editorViewMode=mode==='preview'?'preview':'source';var isPreview=editorViewMode==='preview';editorMain.classList.toggle('previewMode',isPreview);editorScroll.classList.toggle('previewMode',isPreview);viewModeBtn.dataset.mode=editorViewMode;viewModeBtn.title=isPreview?'Editar fonte':'Visualizar';viewModeBtn.setAttribute('aria-label',viewModeBtn.title);if(isPreview){renderCurrentPreview();requestAnimationFrame(function(){if(renderedPreview.contentEditable==='true')renderedPreview.focus()})}else requestAnimationFrame(function(){bodyEditor.focus()})};viewModeBtn.onclick=function(e){e.preventDefault();e.stopPropagation();setEditorViewMode(editorViewMode==='source'?'preview':'source')};
function v21IndentSelection(){var s=bodyEditor.selectionStart,e=bodyEditor.selectionEnd,v=bodyEditor.value,left=v.slice(0,s),sel=v.slice(s,e),right=v.slice(e),out=sel.split('\n').map(function(l){return'  '+l}).join('\n');bodyEditor.value=left+out+right;bodyEditor.selectionStart=s;bodyEditor.selectionEnd=s+out.length;markChanged()}
function v21FormatCurrent(){if(!currentFile)return;var e=v21Kind(currentFile);try{if(e==='json'){bodyEditor.value=JSON.stringify(JSON.parse(bodyEditor.value),null,2);markChanged();toast('JSON formatado.')}else if(e==='css'){bodyEditor.value=bodyEditor.value.replace(/\s*{\s*/g,' {\n  ').replace(/;\s*/g,';\n  ').replace(/\s*}\s*/g,'\n}\n').replace(/\n\s+\n/g,'\n');markChanged()}else toast('Formatação automática disponível para JSON e CSS nesta versão.')}catch(_){toast('Não foi possível formatar: sintaxe inválida.')}}

/* menu: one compact city panel, folder remembered once, safe deletion */
function v21MenuMarkup(){return '<div class="v21MenuHead"><div class="v21MenuLogo">URBE</div><span class="v21Version">v'+V21_VERSION+'</span></div><div class="v21StorageRow"><span class="v21StorageName" id="v21StorageName"></span><button class="v21IconBtn" id="v21PickRoot" title="Pasta dos vaults">▣</button></div><div class="v21CityPanel"><div class="v21CityToolbar"><strong>VAULTS</strong><button class="v21IconBtn" id="v21AddCity" title="Adicionar cidade">＋</button></div><div class="v21CityNew" id="v21CityNew"><input id="v21CityName" autocomplete="off"><button class="v21IconBtn" id="v21CreateCity">✓</button></div><div class="v21CityList" id="v21CityList"></div></div>'}
abrirMenu=function(){menuEl.querySelector('.caixa').innerHTML=v21MenuMarkup();menuEl.classList.add('open');document.getElementById('v21PickRoot').onclick=v21PickRoot;document.getElementById('v21AddCity').onclick=function(){var row=document.getElementById('v21CityNew');row.classList.toggle('open');if(row.classList.contains('open'))document.getElementById('v21CityName').focus()};document.getElementById('v21CreateCity').onclick=v21CreateCity;document.getElementById('v21CityName').onkeydown=function(e){if(e.key==='Enter')v21CreateCity()};v21UpdateStorageName();listarCidadesUI()};
function v21UpdateStorageName(){var e=document.getElementById('v21StorageName');if(e)e.textContent=Disco.modo==='pasta'?(Disco.raiz&&Disco.raiz.name||'Pasta selecionada'):'Armazenamento do aplicativo'}
async function v21PickRoot(){if(window.UrbeNative&&!window.UrbeNative.pickVault)return UD.alert({title:'Pasta do Urbe',message:'No Android, suas notas ficam em Documentos/Urbe (no armazenamento interno do aparelho). Dá para abrir essa pasta no gerenciador de arquivos e copiar para o computador.'});if(!TEM_FSA)return toast('Este navegador usa o armazenamento do aplicativo.');try{var h=await window.showDirectoryPicker({mode:'readwrite',id:'urbe-vaults',startIn:'documents'});if(h.requestPermission&&await h.requestPermission({mode:'readwrite'})!=='granted')return;while(sincRodando)await new Promise(r=>setTimeout(r,20));await rodarSinc(true);var p=window.UrbeCore&&window.UrbeCore.service('persistence');if(p){await p.flush();while(p.busy)await new Promise(r=>setTimeout(r,20));p.suspend(true)}await v21StopSync();Disco.raiz=h;Disco.modo='pasta';Disco.cidade=null;Disco.hCidade=null;_fsa.resetCache();if(!window.UrbeNative)await DBK.set('pastaRaiz',h);await urbeEnsureSingleVault();await abrirCidade('Urbe');v21UpdateStorageName();toast('Pasta do Urbe definida.')}catch(e){if(e&&e.name!=='AbortError')toast('Não consegui abrir a pasta: '+e.message)}}
async function v21CreateCity(){var input=document.getElementById('v21CityName'),nome=(input&&input.value||'').trim();if(!nome)nome='Cidade';var existing=await FS.cidades(),base=nome,n=2;while(existing.some(function(x){return x.toLowerCase()===nome.toLowerCase()}))nome=base+' ('+(n++)+')';try{await FS.criarCidade(nome);await abrirCidade(nome)}catch(e){toast('Não consegui criar o vault.')}}
async function v21StopSync(){sincSuspenso=true;clearTimeout(sincTimer);sincTimer=null;sincPend=false;var guard=0;while(sincRodando&&guard++<100)await new Promise(function(r){setTimeout(r,20)})}
listarCidadesUI=async function(){var el=document.getElementById('v21CityList')||document.getElementById('listaCidades');if(!el)return;el.innerHTML='<div class="v21MenuEmpty">...</div>';var nomes=[];try{nomes=await FS.cidades()}catch(_){el.innerHTML='<div class="v21MenuEmpty">Falha ao listar</div>';return}if(!nomes.length){el.innerHTML='<div class="v21MenuEmpty">Nenhum vault</div>';return}el.innerHTML='';nomes.forEach(function(nome){var div=document.createElement('div');div.className='v21City';div.innerHTML='<button class="v21CityOpen"><span></span><small></small></button><button class="v21CityDelete" title="Excluir">×</button>';div.querySelector('span').textContent=nome;div.querySelector('small').textContent=nome===Disco.cidade?'aberto':'';div.querySelector('.v21CityOpen').onclick=function(){abrirCidade(nome)};div.querySelector('.v21CityDelete').onclick=async function(){if(!(await UD.confirm({title:'Excluir o vault “'+nome+'”?',message:'Todos os arquivos dele serão apagados.',confirm:'Excluir',danger:true})))return;try{if(Disco.cidade===nome){await v21StopSync();Disco.cidade=null;Disco.hCidade=null;statusSinc('ok')}await FS.excluirCidade(nome);await listarCidadesUI();toast('Vault excluído.')}catch(e){console.warn(e);toast('Não consegui excluir o vault.')}};el.appendChild(div)})};
document.getElementById('cidadesBtn').onclick=async function(){try{await rodarSinc(true)}catch(_){ }abrirMenu()};

/* AI: minimal assistant, model ordering/prices, scope picker, history, local settings */




















/* refresh balance UI after v20 model loads */


/* startup helpers */
v21EnsureExplorer();v21BuildTree();






/* ============================================================
   Urbe v0.22 — correções sobre a v0.21
   Cada bloco abaixo substitui um comportamento da versão anterior
   sem reescrever o código original: o patch só reata os nomes.
   ============================================================ */

/* ---------- 1. abrir um arquivo fecha o Explorador ----------
   No celular o Explorador é uma folha em tela cheia (z-index 65) acima do
   editor (z-index 50). A v0.21 abria o editor por baixo e o usuário
   continuava vendo a lista, sem alcançar nenhum botão do cabeçalho. */
var v22OpenFull=openFullEditor;
openFullEditor=function(b){try{fileSidebar.classList.remove('open')}catch(_){ }return v22OpenFull(b)};
var v22OpenPrev=openFilePreview;
openFilePreview=function(b,i){try{fileSidebar.classList.remove('open')}catch(_){ }return v22OpenPrev(b,i)};

/* ---------- 2. abas do editor ----------
   A linha própria vem do CSS; aqui só escondemos a barra quando há um
   único arquivo aberto, para não roubar 36px de altura no celular. */
var v22RenderTabs=v21RenderTabs;
v21RenderTabs=function(){
  v22RenderTabs();
  var box=document.getElementById('v21EditorTabs');
  if(box)box.classList.toggle('v22Single',v21OpenTabs.length<2);
};

/* ---------- 3. toolbar Markdown fora do modo visual ----------
   v21RefreshEditorChrome fixava display:flex inline e vencia a regra de
   CSS que esconde a barra no preview. Os botões chamam insertAtCursor,
   que escreve no textarea escondido: o texto entrava cru no arquivo. */
var v22RefreshChrome=v21RefreshEditorChrome;
v21RefreshEditorChrome=function(){
  v22RefreshChrome();
  var t=document.getElementById('mdToolbar');
  if(t&&editorViewMode==='preview')t.style.display='';
};
var v22Insert=insertAtCursor;
insertAtCursor=function(prefix,suffix){
  if(editorViewMode==='preview')setEditorViewMode('source');
  return v22Insert(prefix,suffix);
};

/* ---------- 4. cabeçalho vazio ("# ") ----------
   renderMarkdown exigia conteúdo depois do #, então o template padrão
   virava <p># </p> e o primeiro texto digitado saía como "#Texto".
   Agora "# " vira um <h1> editável e o caminho de volta preserva o "# ". */
var v22BaseRenderMarkdown=renderMarkdown;
renderMarkdown=function(md){
  var linhas=String(md==null?'':md).replace(/\r\n?/g,'\n').split('\n'),emCodigo=false;
  for(var i=0;i<linhas.length;i++){
    if(/^```/.test(linhas[i])){emCodigo=!emCodigo;continue}
    if(emCodigo)continue;
    var m=linhas[i].match(/^(#{1,6})[ \t]*$/);
    if(m)linhas[i]=m[1]+' \u0001';
  }
  return v22BaseRenderMarkdown(linhas.join('\n')).replace(/\u0001/g,'<br>');
};
var v22BaseFromVisual=markdownFromVisual;
markdownFromVisual=function(root){
  return v22BaseFromVisual(root).replace(/\u200b/g,'').replace(/^(#{1,6})[ \t]*$/gm,'$1 ');
};

/* ---------- matemática (src/math) ----------
   As fórmulas saem do Markdown antes da renderização e voltam como blocos
   atômicos; no caminho de volta cada uma é reescrita com os delimitadores
   originais. Tudo o que é específico de matemática vive em src/math. */
var urbeMathBaseRender=renderMarkdown,urbeMathBaseFromVisual=markdownFromVisual;
renderMarkdown=function(md){var M=window.UrbeMath;return M?M.renderWith(urbeMathBaseRender,md,{editable:true}):urbeMathBaseRender(md)};
markdownFromVisual=function(root){var E=window.UrbeMathEditor;return E&&E.serialize?E.serialize(urbeMathBaseFromVisual,root):urbeMathBaseFromVisual(root)};

/* ---------- 5. mapa.json só é gravado quando muda de verdade ----------
   O campo "salvo" trazia a hora atual, então a comparação com o snapshot
   nunca batia e cada rajada de digitação (120ms) regravava o mapa inteiro
   no disco — caro na SAF do Android. */
var v22MapaChave=null,v22MapaSalvo=null;
var v22BaseEstado=estadoDesejado;
estadoDesejado=function(){
  var st=v22BaseEstado();
  try{
    var txt=st.arquivos.get('.urbe/mapa.json');
    if(txt){
      var o=JSON.parse(txt),salvo=o.salvo;
      delete o.salvo;
      var chave=JSON.stringify(o);
      if(v22MapaChave===chave&&v22MapaSalvo)salvo=v22MapaSalvo;
      else{v22MapaChave=chave;v22MapaSalvo=salvo}
      o.salvo=salvo;
      st.arquivos.set('.urbe/mapa.json',JSON.stringify(o,null,1));
    }
  }catch(_){ }
  return st;
};

/* ---------- 6. binários não são regravados a cada abertura ----------
   v21IndexBinary zerava snapBin e não o semeava de volta, então o primeiro
   sync relia e reescrevia todas as imagens/PDFs do vault. */
var v22BaseIndexBin=v21IndexBinary;
v21IndexBinary=async function(nome,rels,regPorCaminho,mapa){
  await v22BaseIndexBin(nome,rels,regPorCaminho,mapa);
  snapBin=new Map();
  for(var i=0;i<world.buildings.length;i++){
    var b=world.buildings[i];if(b.tipo==='nota')continue;
    var fs=b.files||b.anexos||[];
    for(var j=0;j<fs.length;j++){var a=fs[j];if(a&&a.relPath)snapBin.set(a.relPath,assinaturaBin(a,a.relPath))}
  }
};

/* ---------- 7. malha viária e validação de lote pelo índice espacial ----------
   tileBlockedByBuilding, isHouseAccessGap e loteValido varriam
   world.buildings inteiro. O A* chama os dois primeiros dezenas de milhares
   de vezes por rota: com algumas centenas de notas o celular travava. */
var v22Occ=null,v22Gap=null;
tileBlockedByBuilding=function(x,y){
  if(v22Occ)return v22Occ.has(K(x,y));
  if(idxSujo)indexar();
  var a=idxB.get(chk(x,y));if(!a)return false;
  for(var i=0;i<a.length;i++){var b=a[i];if(x>=b.x&&x<b.x+b.w&&y>=b.y&&y<b.y+b.h)return true}
  return false;
};
isHouseAccessGap=function(x,y){
  if(v22Gap)return v22Gap.has(K(x,y));
  if(idxSujo)indexar();
  /* o vão fica logo abaixo do lote e pode cair no chunk seguinte */
  var chaves=[chk(x,y),chk(x,y-1)];
  for(var k=0;k<chaves.length;k++){
    var a=idxB.get(chaves[k]);if(!a)continue;
    for(var i=0;i<a.length;i++){var b=a[i];if(x===Math.round(b.x+(b.w-1)/2)&&y===b.y+b.h)return true}
  }
  return false;
};
function v22LoteConflita(x,y,ign){
  if(idxSujo)indexar();
  var x0=x-8,y0=y-8,x1=x+8,y1=y+8;
  for(var cy=Math.floor(y0/CH);cy<=Math.floor(y1/CH);cy++)
    for(var cx=Math.floor(x0/CH);cx<=Math.floor(x1/CH);cx++){
      var a=idxB.get(cx+':'+cy);if(!a)continue;
      for(var i=0;i<a.length;i++){
        var b=a[i];
        if(ign&&ign.has(b.id))continue;
        if(x<b.x+b.w+LOTE_GAP&&x+3+LOTE_GAP>b.x&&y<b.y+b.h+LOTE_GAP&&y+3+LOTE_GAP>b.y)return true;
      }
    }
  return false;
}
loteValido=function(x,y,ignorarIds,r){
  ignorarIds=ignorarIds||new Set();
  for(var yy=y;yy<y+3;yy++)for(var xx=x;xx<x+3;xx++){
    if(ehAgua(xx,yy))return false;
    if(r){
      if(!regionHasTile(r,xx,yy))return false;
      var maisInterna=regAt({x:xx,y:yy});
      if(maisInterna&&maisInterna.id!==r.id)return false;
    }
  }
  if(!acessosLote(x,y,r).some(function(a){return a.ok}))return false;
  return !v22LoteConflita(x,y,ignorarIds);
};
var v22BaseRebuild=rebuildRoadNetwork;
rebuildRoadNetwork=function(){
  v22Occ=new Set();v22Gap=new Set();
  for(var i=0;i<world.buildings.length;i++){
    var b=world.buildings[i];
    for(var y=b.y;y<b.y+b.h;y++)for(var x=b.x;x<b.x+b.w;x++)v22Occ.add(K(x,y));
    v22Gap.add(K(Math.round(b.x+(b.w-1)/2),b.y+b.h));
  }
  try{return v22BaseRebuild()}finally{v22Occ=null;v22Gap=null}
};

/* ---------- 7b. o gargalo real: A* com fila ordenada e BFS repetido ----------
   O perfil de CPU ao abrir um vault de 120 notas mostrou routeAStar,
   roadComponentFrom e ehAgua no topo. O A* reordenava a lista aberta inteira
   a cada passo (open.sort) e a componente viária era refeita por BFS a cada
   consulta. Aqui: heap binário, rótulos de componente reaproveitados enquanto
   a malha não cresce, e memória para o ruído do terreno. */
var v22AguaCache=new Map();
ehAgua=function(x,y){
  var k=x+','+y,v=v22AguaCache.get(k);
  if(v!==undefined)return v;
  v=suave(x,y,11)>.70;
  if(v22AguaCache.size>150000)v22AguaCache.clear();
  v22AguaCache.set(k,v);
  return v;
};
var v22CompTam=-1,v22CompId=null,v22CompNos=null;
function v22Componentes(){
  if(v22CompId&&v22CompTam===world.roads.size)return;
  v22CompId=new Map();v22CompNos=[];
  world.roads.forEach(function(s){
    if(v22CompId.has(s))return;
    var idc=v22CompNos.length,lista=[],fila=[s];
    v22CompId.set(s,idc);
    while(fila.length){
      var k=fila.pop(),c=k.indexOf(','),x=+k.slice(0,c),y=+k.slice(c+1);
      lista.push({x:x,y:y});
      var viz=[K(x+1,y),K(x-1,y),K(x,y+1),K(x,y-1)];
      for(var j=0;j<4;j++){
        var nk=viz[j];
        if(world.roads.has(nk)&&!v22CompId.has(nk)){v22CompId.set(nk,idc);fila.push(nk)}
      }
    }
    v22CompNos.push(lista);
  });
  v22CompTam=world.roads.size;
}
roadComponentFrom=function(start){
  var sk=K(start.x,start.y);
  if(!world.roads.has(sk))return null;
  v22Componentes();
  var idc=v22CompId.get(sk);
  return idc==null?null:v22CompNos[idc];
};
roadConnectedBetweenBuildings=function(a,b){
  var sa=lowerRoadStart(a),sb=lowerRoadStart(b),ka=K(sa.x,sa.y),kb=K(sb.x,sb.y);
  if(!world.roads.has(ka)||!world.roads.has(kb))return false;
  v22Componentes();
  var ia=v22CompId.get(ka),ib=v22CompId.get(kb);
  return ia!=null&&ia===ib;
};
routeAStar=function(start,goal){
  var dirs=[[1,0],[-1,0],[0,1],[0,-1]],distBase=manhattan(start,goal),envelopes=[24,48,96,192,384];
  /* ponta na água, na montanha ou no mar: não existe rua possível; sem isso a busca
     varria o mundo inteiro a cada ligação (casas antigas que o mundo novo pôs na água) */
  if(urbeBloqueiaRua(start.x,start.y)||urbeBloqueiaRua(goal.x,goal.y))return [];
  /* ilha: uma inundação curta a partir de cada ponta; se ela se esgota sem achar a
     outra ponta, as duas estão separadas por água ou montanha */
  function ilhada(a,b){var vis=new Set([K(a.x,a.y)]),q=[a],h=0;
    while(h<q.length){if(q.length>2500)return false;var c=q[h++];
      for(var i=0;i<4;i++){var x=c.x+dirs[i][0],y=c.y+dirs[i][1],k=K(x,y);if(vis.has(k))continue;vis.add(k);
        if(x===b.x&&y===b.y)return false;if(urbeBloqueiaRua(x,y)||isHouseAccessGap(x,y)||tileBlockedByBuilding(x,y))continue;q.push({x:x,y:y})}}
    return true}
  if(ilhada(goal,start)||ilhada(start,goal))return [];
  var orcamento=150000; /* passos no total, somando todas as janelas */
  for(var e=0;e<envelopes.length&&orcamento>0;e++){var tocouBorda=false,limite=Math.min(120000,orcamento);
    var extra=envelopes[e];
    var minX=Math.min(start.x,goal.x)-extra,maxX=Math.max(start.x,goal.x)+extra;
    var minY=Math.min(start.y,goal.y)-extra,maxY=Math.max(start.y,goal.y)+extra;
    var heap=[],melhor=new Map(),veio=new Map(),alvo=null,passos=0;
    var chave=function(x,y,d){return x+','+y+','+d};
    var empurra=function(no){
      heap.push(no);
      var i=heap.length-1;
      while(i>0){var pai=(i-1)>>1;if(heap[pai].f<=heap[i].f)break;var t=heap[pai];heap[pai]=heap[i];heap[i]=t;i=pai}
    };
    var retira=function(){
      var topo=heap[0],ult=heap.pop();
      if(heap.length){
        heap[0]=ult;var i=0;
        for(;;){var l=2*i+1,r=l+1,m=i;
          if(l<heap.length&&heap[l].f<heap[m].f)m=l;
          if(r<heap.length&&heap[r].f<heap[m].f)m=r;
          if(m===i)break;var t=heap[m];heap[m]=heap[i];heap[i]=t;i=m}
      }
      return topo;
    };
    empurra({x:start.x,y:start.y,d:-1,g:0,f:distBase});
    melhor.set(chave(start.x,start.y,-1),0);
    while(heap.length&&passos++<limite){
      var cur=retira();
      var ck=chave(cur.x,cur.y,cur.d);
      if(cur.g>(melhor.has(ck)?melhor.get(ck):Infinity))continue;
      if(cur.x===goal.x&&cur.y===goal.y){alvo=cur;break}
      for(var nd=0;nd<4;nd++){
        var x=cur.x+dirs[nd][0],y=cur.y+dirs[nd][1];
        if(x<minX||x>maxX||y<minY||y>maxY){tocouBorda=true;continue}
        if(urbeBloqueiaRua(x,y)||isHouseAccessGap(x,y))continue;
        if(tileBlockedByBuilding(x,y)&&!(x===goal.x&&y===goal.y))continue;
        var reuso=world.roads.has(K(x,y)),curva=(cur.d!==-1&&cur.d!==nd)?1.35:0,passo=reuso?.06:urbeCustoRua(x,y);
        var g=cur.g+passo+curva,nk=chave(x,y,nd);
        if(g<(melhor.has(nk)?melhor.get(nk):Infinity)){
          melhor.set(nk,g);
          var no={x:x,y:y,d:nd,g:g,f:g+manhattan({x:x,y:y},goal)};
          veio.set(nk,{prev:cur,node:no});
          empurra(no);
        }
      }
    }
    if(alvo){
      var caminho=[],no2=alvo;
      while(no2){caminho.push({x:no2.x,y:no2.y});var rec=veio.get(chave(no2.x,no2.y,no2.d));no2=rec?rec.prev:null}
      return caminho.reverse();
    }
    orcamento-=passos;
    /* sem chegar à borda da janela (ilha) ou sem passos sobrando: janela maior não ajuda */
    if(!tocouBorda)break;
  }
  return [];
};

/* ---------- 8. sanitização do Markdown ----------
   A chave da API fica no IndexedDB desta origem; um .md importado não pode
   ganhar execução de script por um link javascript: nem por HTML injetado
   no nome de uma nota. */
/* A versão anterior gerava <a target="_blank"> e só depois aplicava itálico:
   com dois links no mesmo parágrafo, o regex de "_" casava de um _blank ao
   outro e destruía o HTML. Aqui os trechos já convertidos ficam guardados
   como marcadores até o fim, e as URLs passam por uma checagem de esquema. */
function v22Enfase(t){
  return t.replace(/\*\*([^*\n]+)\*\*/g,'<strong>$1</strong>')
          .replace(/__([^_\n]+)__/g,'<strong>$1</strong>')
          .replace(/(?<!\*)\*([^*\n]+)\*(?!\*)/g,'<em>$1</em>')
          .replace(/(?<!_)_([^_\n]+)_(?!_)/g,'<em>$1</em>')
          .replace(/~~([^~\n]+)~~/g,'<del>$1</del>');
}
function v22Url(u){
  var t=String(u).replace(/&(?:#\d+|#x[0-9a-fA-F]+|\w+);/g,'').replace(/[\s\u0000-\u001f]/g,'').toLowerCase();
  if(/^(javascript|vbscript|file):/.test(t))return '#';
  if(/^data:/.test(t)&&!/^data:image\//.test(t))return '#';
  return u;
}
inlineMarkdown=function(s){
  var x=escapeHTML(s),guarda=[];
  var guardar=function(html){guarda.push(html);return '\u0002'+(guarda.length-1)+'\u0002'};
  x=x.replace(/\[\[([^\]|#]+)(?:\|([^\]]+))?\]\]/g,function(m,alvo,rotulo){
    var nome=(rotulo||alvo).trim();
    return guardar('<span class="wikilink" data-note-name="'+escapeHTML(alvo.trim())+'">'+escapeHTML(nome)+'</span>');
  });
  x=x.replace(/!\[([^\]]*)\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g,function(m,alt,src){
    return guardar('<img src="'+v22Url(src)+'" alt="'+alt+'">');
  });
  x=x.replace(/\[([^\]]+)\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g,function(m,txt,href){
    return guardar('<a href="'+v22Url(href)+'" target="_blank" rel="noopener">'+v22Enfase(txt)+'</a>');
  });
  x=x.replace(/`([^`\n]+)`/g,function(m,c){return guardar('<code>'+c+'</code>')});
  x=v22Enfase(x);
  return x.replace(/\u0002(\d+)\u0002/g,function(m,i){return guarda[+i]});
};
renderWikiSuggestions=function(){
  if(!wikiState.open){wikiSuggest.classList.remove('open');wikiSuggest.innerHTML='';return}
  wikiSuggest.classList.add('open');
  if(!wikiState.items.length){
    wikiSuggest.innerHTML='<div class="wikiEmpty">Nenhuma nota encontrada.</div>';
    positionWikiPopup();return;
  }
  wikiSuggest.innerHTML=wikiState.items.map(function(b,i){
    return '<button class="wikiItem '+(i===wikiState.index?'active':'')+'" data-wiki="'+escapeHTML(b.id)+'" role="option">'+escapeHTML(b.name)+'</button>';
  }).join('');
  wikiSuggest.querySelectorAll('[data-wiki]').forEach(function(el){
    el.addEventListener('pointerdown',function(e){
      e.preventDefault();
      var b=world.buildings.find(function(x){return x.id===el.dataset.wiki});
      if(b)commitWikiSuggestion(b);
    });
  });
  positionWikiPopup();
};
commitWikiSuggestion=function(b){
  if(!wikiState.open)return;
  var inicio=wikiState.start,fim=bodyEditor.selectionStart;
  bodyEditor.setRangeText('[['+b.name+']]',inicio,fim,'end');
  wikiState={open:false,start:-1,query:'',items:[],index:0};
  renderWikiSuggestions();bodyEditor.focus();markChanged();
};

/* ---------- 9. contexto da IA à prova de nota sem conteúdo ---------- */



/* ---------- 10. detalhes do painel de IA ---------- */






/* ============================================================
   Urbe v0.23 — casca mobile-first
   O que muda é a navegação, não o motor: tudo abaixo delega para
   as funções que já existiam. Padrões aplicados: dock inferior de
   destinos, uma ação primária (FAB) com menu por pressão longa,
   bottom sheets no lugar de modais, barra de modo com saída visível.
   ============================================================ */

var v23Dock=null,v23Fab=null,v23ModeBar=null,v23Seg=null,v23Destino='mundo';

(function v23Montar(){
  var app=document.getElementById('app');

  /* barra superior: identidade do vault, estado da gravação e atalhos raros */
  var top=document.createElement('header');top.id='v23Top';
  var ic=function(n){return window.UrbeIcons?UrbeIcons.icon(n):''};
  top.innerHTML='<div class="v23Brand" title="Estado da gravação"><span id="v23VaultDot"></span><span id="v23VaultName">Urbe</span></div><span class="v23Spacer"></span>'+
    '<button class="v23TopBtn" id="v23SearchBtn" title="Buscar notas" aria-label="Buscar notas">'+ic('search')+'</button>'+
    '<button class="v23TopBtn" id="v23MapBtn" title="Mapa da cidade" aria-label="Mapa da cidade">'+ic('map')+'</button>'+
    '<button class="v23TopBtn" id="v23SettingsBtn" title="Configurações" aria-label="Configurações">'+ic('settings')+'</button>';
  app.appendChild(top);
  top.querySelector('#v23SearchBtn').onclick=function(){var c=window.UrbeCore&&window.UrbeCore.commands;if(c&&c.has('ui.quickOpen.open'))c.execute('ui.quickOpen.open',{source:'topbar'})};
  top.querySelector('#v23MapBtn').onclick=function(){abrirMapao()};
  top.querySelector('#v23SettingsBtn').onclick=function(){var c=window.UrbeCore&&window.UrbeCore.commands;if(c&&c.has('ui.settings'))c.execute('ui.settings');else v21OpenGlobalSettings()};

  /* barra de modo: nenhuma ferramenta fica ativa sem uma saída à vista */
  v23ModeBar=document.createElement('div');v23ModeBar.id='v23ModeBar';
  v23ModeBar.innerHTML='<span id="v23ModeTxt"></span><button id="v23ModeCancel">Cancelar</button>';
  app.appendChild(v23ModeBar);
  v23ModeBar.querySelector('#v23ModeCancel').onclick=function(){
    if(v21Placement)v21CancelPlacement();else setTool('select');
    v23Atualizar();
  };

  /* dock de destinos */
  v23Dock=document.createElement('nav');v23Dock.id='v23Dock';
  v23Dock.setAttribute('aria-label','Navegação principal');
  v23Dock.innerHTML=[['mundo','city','Cidade'],['arquivos','notes','Notas'],['ia','sparkle','Assistente']]
    .map(function(x){return '<button data-nav="'+x[0]+'" aria-label="'+x[2]+'"><span class="v23NavIco">'+ic(x[1])+'</span><span>'+x[2]+'</span></button>'}).join('');
  app.appendChild(v23Dock);
  v23Dock.querySelectorAll('[data-nav]').forEach(function(b){b.onclick=function(){v23Navegar(b.dataset.nav)}});

  /* ação primária */
  v23Fab=document.createElement('button');v23Fab.id='v23Fab';v23Fab.innerHTML=ic('plus');
  v23Fab.setAttribute('aria-label','Criar');v23Fab.title='Criar';app.appendChild(v23Fab);
  var seg=null;
  v23Fab.addEventListener('pointerdown',function(e){
    clearTimeout(seg);
    seg=setTimeout(function(){seg=null;if(navigator.vibrate)navigator.vibrate(14);v23Fab._marcarHold();v23MenuCriar()},480);
  });
  ['pointerup','pointercancel','pointerleave'].forEach(function(ev){
    v23Fab.addEventListener(ev,function(){if(seg){clearTimeout(seg);seg=null}});
  });
  var v23HoldVenceu=false;
  v23Fab.addEventListener('pointerdown',function(){v23HoldVenceu=false});
  v23Fab.onclick=function(){if(v23HoldVenceu){v23HoldVenceu=false;return}v23AcaoPrimaria()};
  v23Fab._marcarHold=function(){v23HoldVenceu=true};
})();

function v23Aberto(el){return !!el&&el.classList.contains('open')}

function v23Navegar(dest){
  var explorer=window.UrbeCore&&window.UrbeCore.service('explorer.ui');
  if(dest!=='arquivos'&&explorer)explorer.close();
  if(dest!=='mundo'&&v23Aberto(editorFull))closeFullEditor();
  if(dest==='mundo'){
    if(v23Aberto(editorFull))closeFullEditor();
    fileSidebar.classList.remove('open');aiPanel.classList.remove('open');
    fecharMenu();closeFilePreview();closeHouseSummary();
  }else if(dest==='arquivos'){
    aiPanel.classList.remove('open');fecharMenu();closeFilePreview();closeHouseSummary();
    var exCmd=window.UrbeCore&&window.UrbeCore.commands;if(exCmd&&exCmd.has('ui.explorer.open'))exCmd.execute('ui.explorer.open',{source:'navigation'});else{buildTree();fileSidebar.classList.add('open');}
  }else if(dest==='ia'){
    fileSidebar.classList.remove('open');fecharMenu();
    document.getElementById('openAI').click();
  }
  v23Destino=dest;v23Atualizar();
}

/* A ação primária muda com o destino, mas nunca deixa de existir. */
function v23AcaoPrimaria(){
  if(v23Aberto(menuEl))return;
  v23MenuCriar();
}
function v23MenuCriar(){
  var alvo=v23Aberto(fileSidebar)?(explorerRegionTarget||null):null;
  abrirMenuExplorer('Criar',[
    {ico:'＋',label:'Nova nota',run:function(){v21CreateFileInRegion(alvo)}},
    {ico:'▣',label:'Nova pasta',run:function(){criarPastaNoDestino(alvo)}},
    {ico:'◰',label:'Desenhar pasta no mapa',run:function(){
      v23Navegar('mundo');setTool('region');v23Atualizar();
    }},
    {ico:'⌂',label:'Adicionar construção',run:function(){v23Navegar('mundo');v21BeginNewPlacement();v23Atualizar()}},
    {ico:'⇩',label:'Importar arquivos',run:function(){v21ImportPicker(alvo)}}
  ]);
}

/* versões anteriores de uma nota: o histórico guarda até 40 por nota, agrupando edições próximas */
function urbeVersoes(b){
  var core=window.UrbeCore,docs=core&&core.service('documents'),hist=core&&core.service('history');if(!b||!docs||!hist)return;
  if(editorViewMode==='preview')syncVisualToMarkdown();
  var d=docs.get(b.documentId||nomeCompletoNota(b));if(!d)return;var lista=hist.list(d.id);
  if(!lista.length){UD.alert({title:'Versões anteriores',message:'Esta nota ainda não tem versões guardadas. A cada mudança o Urbe guarda a versão anterior (até 40 por nota).'});return}
  function quando(t){var dt=new Date(t),hoje=new Date();return(dt.toDateString()===hoje.toDateString()?'Hoje':dt.toLocaleDateString('pt-BR'))+' às '+dt.toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit'})}
  UD.choose({title:'Versões anteriores',message:'Escolha uma versão para ver e restaurar. A versão atual também fica guardada, então dá para voltar.',
    options:lista.map(function(v,i){var txt=v.content.replace(/\s+/g,' ').trim();return{value:i,icon:'clock',label:quando(v.timestamp),detail:(txt.length>70?txt.slice(0,70)+'…':txt)||'(vazia)'}})})
  .then(function(i){if(i===undefined)return;var v=lista[i];
    UD.confirm({title:'Restaurar esta versão?',message:quando(v.timestamp)+'\n\n'+(v.content.length>600?v.content.slice(0,600)+'…':v.content),confirm:'Restaurar'}).then(function(ok){
      if(!ok)return;hist.restore(d.id,i);var nd=docs.get(d.id);
      if(nd&&currentFile&&v23Aberto(editorFull)){currentFile.content=nd.content;loadFile(currentFile);if(editorViewMode==='preview')renderCurrentPreview()}toast('Versão restaurada')})});
}

/* ---------- estado visível da casca ---------- */
function v23Atualizar(){
  var editor=v23Aberto(editorFull),menu=v23Aberto(menuEl),ia=v23Aberto(aiPanel),
      arq=v23Aberto(fileSidebar)||!!(window.UrbeCore&&window.UrbeCore.service('explorer.ui')&&window.UrbeCore.service('explorer.ui').isOpen()),preview=v23Aberto(filePreview);
  var atual=editor?'':menu?'vaults':ia?'ia':arq?'arquivos':'mundo';
  if(v23Dock){
    v23Dock.hidden=editor;
    v23Dock.querySelectorAll('[data-nav]').forEach(function(b){b.classList.toggle('ativo',b.dataset.nav===atual)});
  }
  var topo=document.getElementById('v23Top');
  if(topo)topo.hidden=editor||menu||ia||arq;
  if(v23Fab)v23Fab.hidden=editor||menu||preview||ia||arq||!!v21Placement||tool!=='select';
  /* barra de modo */
  var txt=null;
  if(v21Placement)txt=null; /* o dock de posicionamento já explica o passo */
  else if(tool==='region')txt='Arraste no mapa para delimitar a nova pasta.';
  else if(tool==='house')txt='Toque no mapa para escolher o terreno.';
  if(v23ModeBar){
    v23ModeBar.classList.toggle('open',!!txt&&!editor&&!menu&&!arq&&!ia);
    if(txt)document.getElementById('v23ModeTxt').textContent=txt;
  }
  var nome=document.getElementById('v23VaultName');
  if(nome){nome.textContent=Disco.cidade||'Urbe';nome.parentElement.title=Disco.modo==='pasta'?'Salvo na pasta escolhida no dispositivo':'Salvo neste navegador'}
}
(function v23AtualizacaoReativa(){
  var core=window.UrbeCore;
  if(core){['workspace:loaded','workspace:dirty','workspace:saving','workspace:saved','workspace:saveError','document:created','document:updated','document:removed','explorer:selection','explorer:visibility'].forEach(function(ev){core.events.on(ev,v23Atualizar)})}
  var obs=new MutationObserver(v23Atualizar);[editorFull,fileSidebar,aiPanel,menuEl,filePreview].filter(Boolean).forEach(function(el){obs.observe(el,{attributes:true,attributeFilter:['class','style','hidden']})});
  addEventListener('visibilitychange',v23Atualizar);v23Atualizar();
})();

/* primeiro acesso: cidade vazia explica o conceito e oferece o primeiro passo */
(function urbeBoasVindas(){
  var core=window.UrbeCore,docs=core&&core.service('documents');if(!core||!docs)return;
  var el=document.createElement('section');el.id='urbeWelcome';el.hidden=true;el.setAttribute('aria-label','Boas-vindas');
  el.innerHTML='<h2>Sua cidade começa aqui</h2><p>Cada nota vira uma casa e cada pasta, um bairro. Quando uma nota cita outra com [[nome]], uma rua liga as duas.</p>'+
    '<div class="uw-actions"><button type="button" class="ui-btn ui-btn-primary" data-first>Criar nota</button><button type="button" class="ui-btn" data-import>Importar</button></div>';
  document.getElementById('app').appendChild(el);
  el.querySelector('[data-first]').onclick=function(){v21CreateFileInRegion(null)};
  el.querySelector('[data-import]').onclick=function(){v21ImportPicker(null)};
  var persist=core.service('persistence'),carregado=!!(persist&&persist.vault&&!persist.suspended);
  function atualizar(){el.hidden=!carregado||docs.list().length>0||world.buildings.length>0||v23Aberto(editorFull)||tool!=='select'}
  core.events.on('workspace:loaded',function(){carregado=true;atualizar()});
  ['document:created','document:removed','documents:reset','explorer:visibility'].forEach(function(ev){core.events.on(ev,atualizar)});
  new MutationObserver(atualizar).observe(editorFull,{attributes:true,attributeFilter:['class']});
})();

/* o ponto da barra superior substitui o antigo selo de sincronização */
var v23BaseStatus=statusSinc;
statusSinc=function(estado,extra){
  v23BaseStatus(estado,extra);
  if(estado==='erro')document.getElementById('saveState').textContent='Erro ao salvar';
  var d=document.getElementById('v23VaultDot');
  if(d){d.classList.toggle('gravando',estado==='gravando');d.classList.toggle('erro',estado==='erro')}
};

/* ---------- editor: voltar, segmentado e menu de contexto ---------- */
(function v23Editor(){
  var topo=document.getElementById('editorTop'),
      voltar=document.getElementById('sidebarToggleMobile'),
      overflow=document.getElementById('closeFullEditor'),
      rodape=document.getElementById('editorBottom'),
      ia=document.getElementById('openAI'),
      salvo=document.getElementById('saveState');

  var ic=function(n){return window.UrbeIcons?UrbeIcons.icon(n):''};
  voltar.innerHTML=ic('back');voltar.title='Voltar para a cidade';voltar.setAttribute('aria-label','Voltar');
  voltar.onclick=function(){closeFullEditor();v23Atualizar()};

  v23Seg=document.createElement('div');v23Seg.id='v23Seg';
  v23Seg.innerHTML='<button data-mode="preview">Visual</button><button data-mode="source">Fonte</button>';
  topo.insertBefore(v23Seg,overflow);
  v23Seg.querySelectorAll('[data-mode]').forEach(function(b){
    b.onclick=function(e){e.preventDefault();setEditorViewMode(b.dataset.mode)};
  });

  overflow.innerHTML=ic('more');overflow.title='Mais ações';overflow.setAttribute('aria-label','Mais ações');
  overflow.onclick=function(){
    if(!currentFile)return closeFullEditor();
    abrirMenuExplorer(nomeCompletoNota(currentFile),[
      {ico:'Aa',label:'Renomear',run:function(){renomearNota(currentFile)}},
      {ico:'✦',label:'Assistente',run:function(){ia.click()}},
      {ico:'▣',label:'Mostrar nas notas',run:function(){var c=window.UrbeCore&&window.UrbeCore.commands;if(c&&c.has('ui.explorer.open'))c.execute('ui.explorer.open',{source:'editor'})}},
      {ico:'↶',label:'Versões anteriores',run:function(){urbeVersoes(currentFile)}},
      {ico:'☷',label:'Estrutura e backlinks',run:function(){var c=window.UrbeCore&&window.UrbeCore.commands;if(c&&c.has('editor.contextPanel'))c.execute('editor.contextPanel',{source:'editor-menu'})}},
      {ico:'🗑',label:'Mover para a lixeira',danger:true,run:function(){removerNota(currentFile)}}
    ]);
  };

  /* o estado de gravação sai do cabeçalho, onde empurrava os botões */
  if(salvo&&rodape)rodape.insertBefore(salvo,ia);
  ia.innerHTML=ic('sparkle')+'<span>Assistente</span>';ia.title='Assistente de IA';
  var abrirIa=ia.onclick;
  ia.onclick=function(){
    aiPanel.classList.toggle('v23SobreEditor',v23Aberto(editorFull));
    if(abrirIa)abrirIa.call(this);
  };
})();

/* ---------- barra de formatação que funciona no modo visual ----------
   Na v0.21 os botões escreviam no textarea escondido e corrompiam o arquivo;
   na v0.22 eu simplesmente escondi a barra. Aqui ela volta: no modo fonte usa
   o caminho antigo, no modo visual age sobre o bloco do cursor — o mesmo
   caminho que a digitação ao vivo já usava. */
function v23BlocoAtual(){
  var sel=window.getSelection();
  if(!sel||!sel.rangeCount||!renderedPreview.contains(sel.anchorNode))return null;
  var b=closestEditableBlock(sel.anchorNode);
  return b&&b!==renderedPreview?b:null;
}
function v23GarantirBloco(){
  var b=v23BlocoAtual();
  if(b)return b;
  renderedPreview.focus();
  b=v23BlocoAtual();
  if(b)return b;
  var p=document.createElement('p');p.appendChild(document.createElement('br'));
  renderedPreview.appendChild(p);setCaretEnd(p);return p;
}
/* Trocar o tipo do bloco preservando o que já estava escrito dentro dele:
   converter por texto apagaria negrito, links e wiki-links da linha. */
function v23SairDaLista(bloco){
  if(bloco.tagName!=='LI')return bloco;
  var lista=bloco.parentElement,p=document.createElement('p');
  while(bloco.firstChild){
    var n=bloco.firstChild;
    if(n.nodeType===Node.ELEMENT_NODE&&n.tagName==='INPUT'){bloco.removeChild(n);continue}
    p.appendChild(n);
  }
  lista.parentNode.insertBefore(p,lista.nextSibling);
  bloco.remove();
  if(!lista.querySelector('li'))lista.remove();
  setCaretEnd(p);
  return p;
}
function v23ParaLista(bloco,tipo,tarefa){
  var lista=document.createElement(tipo),li=document.createElement('li');
  while(bloco.firstChild)li.appendChild(bloco.firstChild);
  if(tarefa){
    var cb=document.createElement('input');cb.type='checkbox';li.className='task';
    li.insertBefore(document.createTextNode(' '),li.firstChild);li.insertBefore(cb,li.firstChild);
  }
  lista.appendChild(li);bloco.replaceWith(lista);setCaretEnd(li);
  return li;
}
function v23PrefixarBloco(prefixo){
  var bloco=v23GarantirBloco(),m,naLista=bloco.tagName==='LI';
  if((m=prefixo.match(/^(#{1,6})\s$/))){
    var alvo='h'+m[1].length;
    if(naLista)bloco=v23SairDaLista(bloco);
    bloco=replaceBlockTag(bloco,bloco.tagName.toLowerCase()===alvo?'p':alvo);
  }else if(/^>\s$/.test(prefixo)){
    if(naLista)bloco=v23SairDaLista(bloco);
    bloco=replaceBlockTag(bloco,bloco.tagName==='BLOCKQUOTE'?'p':'blockquote');
  }else if(/^[-*+]\s\[\s\]\s$/.test(prefixo)){
    bloco=naLista?v23SairDaLista(bloco):v23ParaLista(bloco,'ul',true);
  }else if(/^[-*+]\s$/.test(prefixo)){
    bloco=naLista?v23SairDaLista(bloco):v23ParaLista(bloco,'ul',false);
  }else{
    var txt=(bloco.textContent||'').replace(/\u200b/g,'');
    bloco.textContent=prefixo+txt;setCaretEnd(bloco);transformLiveMarkdownBlock();
    bloco=v23BlocoAtual()||bloco;
  }
  if(bloco&&!(bloco.textContent||'').trim()&&!bloco.querySelector('br,input'))bloco.appendChild(document.createElement('br'));
  if(bloco)setCaretEnd(bloco);
  syncVisualToMarkdown();
}
function v23Envolver(marca){
  renderedPreview.focus();
  var sel=window.getSelection(),texto=sel&&!sel.isCollapsed?sel.toString():'';
  if(marca==='**')document.execCommand('bold');
  else if(marca==='_')document.execCommand('italic');
  else if(marca==='`')document.execCommand('insertHTML',false,'<code>'+escapeHTML(texto||'código')+'</code>');
  syncVisualToMarkdown();
}
document.querySelectorAll('.mdBtn').forEach(function(btn){
  var base=btn.onclick;
  btn.onclick=function(){
    if(editorViewMode!=='preview')return base.call(this);
    var sel,texto;
    if(btn.dataset.special==='noteLink'){
      renderedPreview.focus();
      document.execCommand('insertText',false,'[[');
      updateVisualWiki();return;
    }
    if(btn.dataset.special==='link'){
      sel=window.getSelection();
      var faixa=sel&&sel.rangeCount&&renderedPreview.contains(sel.anchorNode)?sel.getRangeAt(0).cloneRange():null;
      texto=faixa&&!faixa.collapsed?faixa.toString():'';
      UD.prompt({title:'Inserir link',label:'Endereço',value:'https://',selectBaseName:false,autocapitalize:'none',confirm:'Inserir',
        validate:function(v){return /^\s*(https?:\/\/|mailto:)\S+/i.test(v)?'':'Use um endereço começando com https://'}}).then(function(u){
        if(!u)return;renderedPreview.focus();var s2=window.getSelection();
        if(faixa){s2.removeAllRanges();s2.addRange(faixa)}
        document.execCommand('insertHTML',false,'<a href="'+escapeHTML(u.trim())+'" target="_blank" rel="noopener">'+escapeHTML(texto||u.trim())+'</a>');
        syncVisualToMarkdown();
      });
      return;
    }
    if(btn.dataset.wrap)return v23Envolver(btn.dataset.wrap);
    var md=btn.dataset.md||'';
    if(/^\s*---\s*$/.test(md)){renderedPreview.focus();document.execCommand('insertHTML',false,'<hr><p><br></p>');syncVisualToMarkdown();return}
    if(md&&!md.trim()){renderedPreview.focus();document.execCommand('insertParagraph');syncVisualToMarkdown();return}
    v23PrefixarBloco(md);
  };
});
function v23AtualizarToolbar(){
  var t=document.getElementById('mdToolbar');if(!t||!currentFile)return;
  var e=v21Kind(currentFile),isMd=e==='md'||e==='markdown';
  t.style.setProperty('display',isMd?'flex':'none','important');
}

var v23BaseSetMode=setEditorViewMode;
setEditorViewMode=function(mode){
  v23BaseSetMode(mode);
  v23AtualizarToolbar();
  if(v23Seg)v23Seg.querySelectorAll('[data-mode]').forEach(function(b){
    b.classList.toggle('ativo',b.dataset.mode===editorViewMode);
  });
};
var v23BaseChrome=v21RefreshEditorChrome;
v21RefreshEditorChrome=function(){
  v23BaseChrome();
  v23AtualizarToolbar();
  if(!v23Seg)return;
  v23Seg.hidden=!(currentFile&&v21CanPreview(currentFile));
  v23Seg.querySelectorAll('[data-mode]').forEach(function(b){
    b.classList.toggle('ativo',b.dataset.mode===editorViewMode);
  });
};

/* ---------- ferramenta ativa avisa; o mapa mostra os números ---------- */
var v23BaseSetTool=setTool;
setTool=function(t){v23BaseSetTool(t);v23Atualizar()};
var v23BaseAbrirMapao=abrirMapao;
abrirMapao=function(){
  v23BaseAbrirMapao();
  var d=document.querySelector('#mapao .dica');
  if(d)d.textContent=world.buildings.length+' arquivos · '+world.regions.length+' pastas · '+
    world.links.length+' conexões — toque para ir até lá';
};

/* o painel de arquivo e o preview passam a conviver com o dock */
var v23BaseCloseHouse=closeHouseSummary;
closeHouseSummary=function(){v23BaseCloseHouse();v23Atualizar()};

v23Atualizar();


/* ============================================================
   Urbe v0.25 — Fase 2: os moradores saem de casa
   Pedestres percorrendo as estradas entre notas ligadas. Reaproveita
   o A* já otimizado na v0.22 e não guarda nada em disco: o vaivém é
   leitura do vault, não estado de jogo.
   Orçamento de CPU é a restrição de projeto aqui — teto de andarilhos,
   cache de rotas, uma rota nova por ciclo e animação a 12 quadros.
   ============================================================ */

var V25_MAX=22;              /* andarilhos vivos ao mesmo tempo */
var V25_VEL=1.35;            /* tiles por segundo (média; cada um tem o seu passo) */
var V25_FPS=33;              /* intervalo do quadro, em ms (≈30 por segundo: passos fluidos) */
var urbeVelPovo=1;           /* a chuva apressa o passo (src/world/life.js) */
var V25_CACHE_MAX=180;
var V25_OCIOSOS=8;           /* moradores à toa na frente de casa */

var v25Povo=[],v25Rotas=new Map(),v25Ligado=true,v25UltimoElenco=0,v25Relogio=null;

function v25CorDaGuilda(b){
  if(typeof sim!=='undefined'&&sim&&sim.moradores){
    var m=sim.moradores[b.id];
    if(m&&m.guilda){
      var paleta={Lavradores:'#d9c26a',Lenhadores:'#8fbf72',Pedreiros:'#a9b3ba',Ferreiros:'#e08b5a',
                  Escribas:'#7fd4e6',Mercadores:'#c79ae0',Curandeiros:'#7ee0a6',Guardas:'#e0786f'};
      if(paleta[m.guilda])return paleta[m.guilda];
    }
  }
  return '#d7e6ec';
}

/* Quem anda: só nota ligada a outra nota. Rua vazia é o diagnóstico
   de nota órfã — o silêncio ali é informação, não falta de polimento. */
function v25Vizinhos(b){
  var saida=[];
  for(var i=0;i<world.links.length;i++){
    var l=world.links[i];
    if(l.from===b.id)saida.push(l.to);
    else if(l.to===b.id)saida.push(l.from);
  }
  return saida;
}
function v25Predio(id){
  for(var i=0;i<world.buildings.length;i++)if(world.buildings[i].id===id)return world.buildings[i];
  return null;
}
/* Andarilho anda por estrada, não por atalho: em vez de rodar o A* do
   terreno (que recusa justamente o vão de acesso das casas, por onde a
   pessoa sai), percorremos a malha viária que já está desenhada. */
function v25EstradaPerto(b){
  var base=lowerRoadStart(b);
  for(var raio=0;raio<=4;raio++){
    for(var dy=-raio;dy<=raio;dy++)for(var dx=-raio;dx<=raio;dx++){
      if(Math.max(Math.abs(dx),Math.abs(dy))!==raio)continue;
      var x=base.x+dx,y=base.y+dy;
      if(world.roads.has(K(x,y)))return {x:x,y:y};
    }
  }
  return null;
}
function v25Rota(a,b){
  var chave=a.id+'>'+b.id,r=v25Rotas.get(chave);
  if(r!==undefined)return r;
  r=null;
  var ini=v25EstradaPerto(a),fim=v25EstradaPerto(b);
  if(ini&&fim){
    var ki=K(ini.x,ini.y),kf=K(fim.x,fim.y);
    if(ki===kf)r=null;
    else{
      var veio=new Map(),fila=[ki],cab=0;
      veio.set(ki,null);
      while(cab<fila.length){
        var k=fila[cab++];
        if(k===kf)break;
        var c=k.indexOf(','),x=+k.slice(0,c),y=+k.slice(c+1);
        var viz=[K(x+1,y),K(x-1,y),K(x,y+1),K(x,y-1)];
        for(var j=0;j<4;j++){
          var nk=viz[j];
          if(world.roads.has(nk)&&!veio.has(nk)){veio.set(nk,k);fila.push(nk)}
        }
      }
      if(veio.has(kf)){
        var caminho=[],cur=kf;
        while(cur){var cc=cur.indexOf(',');caminho.push({x:+cur.slice(0,cc),y:+cur.slice(cc+1)});cur=veio.get(cur)}
        caminho.reverse();
        if(caminho.length>2)r=caminho;
      }
    }
  }
  if(v25Rotas.size>V25_CACHE_MAX)v25Rotas.clear();
  v25Rotas.set(chave,r);
  return r;
}

/* O elenco é recontado de vez em quando, e só com o que está à vista:
   o custo tem que caber no orçamento de quadro de um celular. */
function v25Elenco(){
  var f=faixaVisivel(),candidatos=[];
  for(var i=0;i<world.buildings.length;i++){
    var b=world.buildings[i];
    if(b.tipo!=='nota')continue;
    if(b.x+b.w<f.x0||b.x>f.x1||b.y+b.h<f.y0||b.y>f.y1)continue;
    var viz=v25Vizinhos(b);
    if(!viz.length)continue;
    candidatos.push({b:b,viz:viz,peso:Math.min(4,viz.length)});
  }
  var teto=V25_MAX;
  if(typeof sim!=='undefined'&&sim&&typeof sim.humor==='number')
    teto=Math.max(4,Math.round(V25_MAX*(.35+sim.humor/100*.65)));
  candidatos.sort(function(x,y){return y.peso-x.peso});
  return candidatos.slice(0,teto);
}
/* v0.48: cada morador tem aparência própria (pele, cabelo, chapéu, roupa
   do ofício, o que carrega), anda no seu ritmo, para na porta antes de
   voltar, conversa quando cruza com alguém e há quem fique à toa em frente
   de casa. O desenho é pixel-art do mesmo atlas do mundo. */
var V25_OFICIO={Lavradores:{shirt:['#c9a24a','#b58b3c'],style:'straw',acc:'basket'},Lenhadores:{shirt:['#5f8a3e','#4e7a44'],style:'cap',cap:'#6d8a3a',acc:'sack'},
  Pedreiros:{shirt:['#8e8f8a','#7b7466'],style:'bald',acc:'bucket'},Ferreiros:{shirt:['#8a4a33','#6a3a2c'],style:'short',acc:'bucket'},
  Escribas:{shirt:['#3f6fa0','#35557f'],style:'hood',hood:'#2f4f78'},Mercadores:{shirt:['#8a4f9a','#a3584f'],style:'cap',cap:'#7a3f8a',acc:'sack'},
  Curandeiros:{shirt:['#e6e2d3','#5a8f6a'],style:'hood',hood:'#4f7f5f',acc:'staff'},Guardas:{shirt:['#9a3a32','#7a2f2a'],style:'cap',cap:'#9a3a32',acc:'staff'}};
var V25_CAMISAS=['#b84a3a','#3d6fb0','#5f8f4a','#c9a24a','#7a5b8a','#c7703c','#4a8a8a','#e0ddd0','#9a4f6a'],V25_ESTILOS=['short','short','long','straw','bald','cap','hood'];
function v25Hash(t){var h=2166136261;t=String(t);for(var i=0;i<t.length;i++){h^=t.charCodeAt(i);h=Math.imul(h,16777619)}return h>>>0}
function v25Visual(b,sal){
  var h=v25Hash((b&&b.id||'')+'#'+sal),g=null;
  if(typeof sim!=='undefined'&&sim&&sim.moradores){var m=sim.moradores[b.id];if(m&&m.guilda)g=V25_OFICIO[m.guilda]}
  var r=function(n){var v=h%n;h=Math.floor(h/n)^(h>>>7);return v},dress=r(3)===0;
  var look={skin:r(5),hair:r(6),pants:r(5),style:V25_ESTILOS[r(V25_ESTILOS.length)],shirt:V25_CAMISAS[r(V25_CAMISAS.length)],dress:dress?1:0};
  if(look.style==='long'&&!dress&&r(2))look.dress=1;
  if(look.style==='hood')look.hood=['#5b4a6a','#6a5a3a','#3f5a6a','#7a3f3a'][r(4)];
  if(look.style==='cap')look.cap=['#a33c32','#3f5f8a','#5a7a3a','#8a6a3a'][r(4)];
  var sorte=r(10);look.acc=sorte<2?'basket':sorte<4?'sack':sorte<5?'bucket':null;
  if(g&&r(4)){look.shirt=g.shirt[r(g.shirt.length)];if(r(3))look.style=g.style;if(g.cap)look.cap=g.cap;if(g.hood)look.hood=g.hood;if(g.acc&&r(2))look.acc=g.acc}
  if(look.style==='hood'&&look.acc==='sack')look.acc=null;
  return look;
}
function v25Porta(b){return {x:Math.round(b.x+(b.w-1)/2),y:b.y+b.h}}
function v25Nascer(c){
  var alvoId=c.viz[Math.floor(Math.random()*c.viz.length)],alvo=v25Predio(alvoId);
  if(!alvo)return null;
  var rota=v25Rota(c.b,alvo);
  if(!rota)return null;
  /* sai pela porta de casa e entra pela porta do vizinho */
  var r=[v25Porta(c.b)].concat(rota,[v25Porta(alvo)]),sal=Math.floor(Math.random()*3);
  return {tipo:'andarilho',de:c.b,para:alvo,rota:r,s:Math.random()*Math.max(1,r.length-2)+.5,sentido:1,i:0,f:0,volta:false,
    vel:V25_VEL*(.8+Math.random()*.45),pausa:0,passo:Math.random()*4,faixa:Math.random()<.5?-1:1,
    look:v25Visual(c.b,sal),cor:v25CorDaGuilda(c.b),x:0,y:0,dx:0,dy:1,conversa:0};
}
/* À toa em frente de casa: dá uns passos, para, olha em volta. */
function v25Ociosos(vivos){
  var f=faixaVisivel(),ja={},n=0;
  for(var i=0;i<v25Povo.length;i++){var a=v25Povo[i];if(a.tipo==='ocioso'&&a.casa.x+a.casa.w>=f.x0&&a.casa.x<=f.x1&&a.casa.y+a.casa.h>=f.y0&&a.casa.y<=f.y1&&world.buildings.indexOf(a.casa)>=0&&n<V25_OCIOSOS){vivos.push(a);ja[a.casa.id]=true;n++}}
  for(var j=0;j<world.buildings.length&&n<V25_OCIOSOS;j++){
    var b=world.buildings[j];if(b.tipo!=='nota'||ja[b.id])continue;
    if(b.x+b.w<f.x0||b.x>f.x1||b.y+b.h<f.y0||b.y>f.y1)continue;
    if(v25Hash(b.id+'ocioso')%3)continue;             /* nem toda casa tem alguém na porta */
    var p=v25Porta(b),lado=(v25Hash(b.id)&1)?1:-1,x=p.x+.5+lado*Math.max(1.3,b.w/2-.2),y=p.y+.55;   /* ao lado da porta, fora do letreiro */
    vivos.push({tipo:'ocioso',casa:b,de:b,x:x,y:y,ox:x,oy:y,alvo:null,vel:.55+Math.random()*.3,pausa:1+Math.random()*3,passo:0,
      look:v25Visual(b,'porta'),dx:0,dy:1,conversa:0});
    ja[b.id]=true;n++;
  }
}
function v25Livre(x,y){var t={x:Math.floor(x),y:Math.floor(y)};if(bAt(t))return false;if(MUNDO&&MUNDO.isWater&&MUNDO.isWater(t.x,t.y))return false;return true}
function v25Posicionar(a){
  var r=a.rota,n=r.length-1,s=Math.max(0,Math.min(n,a.s)),i=Math.min(n-1,Math.floor(s)),f=s-i,p0=r[i],p1=r[i+1];
  var dx=p1.x-p0.x,dy=p1.y-p0.y;if(a.sentido<0){dx=-dx;dy=-dy}
  if(a.pausa<=0&&(dx||dy)){a.dx=dx;a.dy=dy}
  /* mão dupla: cada um anda do seu lado da rua, menos na porta */
  var lado=(i===0||i===n-1)?0:.2*a.faixa*(a.sentido),ox=-dy*lado,oy=dx*lado;
  a.x=p0.x+(p1.x-p0.x)*f+.5+ox;a.y=p0.y+(p1.y-p0.y)*f+.62+oy;
  a.i=i;a.f=f;a.volta=a.sentido<0;
}
function v25Passo(dt){
  for(var i=0;i<v25Povo.length;i++){
    var a=v25Povo[i];
    if(a.conversa>0)a.conversa-=dt;
    if(a.pausa>0){a.pausa-=dt;if(a.tipo==='andarilho')v25Posicionar(a);continue}
    if(a.tipo==='ocioso'){
      if(!a.alvo){var tx=a.ox+(Math.random()*1.6-.8),ty=a.oy+(Math.random()*.6-.1);if(v25Livre(tx,ty))a.alvo={x:tx,y:ty};else{a.pausa=1;continue}}
      var ddx=a.alvo.x-a.x,ddy=a.alvo.y-a.y,d=Math.hypot(ddx,ddy),st=a.vel*dt*urbeVelPovo;
      if(d<=st){a.x=a.alvo.x;a.y=a.alvo.y;a.alvo=null;a.pausa=1.5+Math.random()*4;a.dx=0;a.dy=Math.random()<.6?1:0;if(!a.dy)a.dx=Math.random()<.5?-1:1}
      else{a.x+=ddx/d*st;a.y+=ddy/d*st;a.passo+=st*4;if(Math.abs(ddx)>Math.abs(ddy)){a.dx=ddx>0?1:-1;a.dy=0}else{a.dx=0;a.dy=ddy>0?1:-1}}
      continue;
    }
    var n=a.rota.length-1,av=a.vel*dt*urbeVelPovo;
    a.s+=av*a.sentido;a.passo+=av*4;
    if(a.s>=n){a.s=n;a.sentido=-1;a.pausa=2+Math.random()*4;a.dx=0;a.dy=1}      /* chegou: entra, visita e sai de novo */
    else if(a.s<=0){a.s=0;a.sentido=1;a.pausa=2+Math.random()*4;a.dx=0;a.dy=1}
    v25Posicionar(a);
  }
  /* cruzou com alguém na rua? às vezes param para um dedo de prosa */
  for(var p=0;p<v25Povo.length;p++){var A=v25Povo[p];if(A.tipo!=='andarilho'||A.pausa>0||A.conversa>-4)continue;
    for(var q=p+1;q<v25Povo.length;q++){var B=v25Povo[q];if(B.pausa>0||B.conversa>-4)continue;
      if(Math.abs(A.x-B.x)+Math.abs(A.y-B.y)<.75&&(A.dx*B.dx+A.dy*B.dy)<0){
        if(Math.random()<.45){var t=2.5+Math.random()*2.5;A.pausa=B.pausa=t;A.conversa=B.conversa=t;
          var ddx2=B.x-A.x,ddy2=B.y-A.y;if(Math.abs(ddx2)>=Math.abs(ddy2)){A.dx=ddx2>=0?1:-1;A.dy=0}else{A.dx=0;A.dy=ddy2>=0?1:-1}B.dx=-A.dx;B.dy=-A.dy}
        else{A.conversa=B.conversa=-.001}
        break}}}
  for(var k=0;k<v25Povo.length;k++)if(v25Povo[k].conversa<=0&&v25Povo[k].conversa>-10)v25Povo[k].conversa-=dt;
}
function v25Desenhar(){
  if(!v25Povo.length||!window.UrbeArt||!UrbeArt.villager)return;
  var z=camera.z;
  if(z<.38)return;                    /* de longe viram ruído; não desenha */
  var sp=TILE*z/20,f=faixaVisivel(),ord=v25Povo.slice().sort(function(a,b){return a.y-b.y}),agora=Date.now();
  ctx.save();ctx.imageSmoothingEnabled=false;
  for(var i=0;i<ord.length;i++){
    var a=ord[i];if(!a.x&&!a.y)continue;
    if(a.tipo==='andarilho'&&a.pausa>0&&(a.s<=0||a.s>=a.rota.length-1))continue;   /* entrou em casa: está de visita */
    if(a.x<f.x0-2||a.x>f.x1+2||a.y<f.y0-2||a.y>f.y1+3)continue;
    var parado=a.pausa>0,fr=parado?0:(Math.floor(a.passo)&3),dir=a.dx?'side':a.dy<0?'up':'down';
    var im=dir==='side'&&a.dx<0?UrbeArt.villagerFlip(a.look,fr):UrbeArt.villager(a.look,dir,fr);
    var q=w2s(a.x*TILE,a.y*TILE),w=im.width*sp,h=im.height*sp,bob=(!parado&&(fr&1))?sp:0;
    ctx.fillStyle='rgba(0,0,0,.22)';ctx.beginPath();ctx.ellipse(q.x,q.y,w*.34,sp*1.6,0,0,7);ctx.fill();
    ctx.drawImage(im,Math.round(q.x-w/2),Math.round(q.y-h+sp*1.2-bob),Math.ceil(w),Math.ceil(h));
    if(a.conversa>0&&z>=.55){ /* balão com reticências, piscando */
      var bx=q.x+w*.25,by=q.y-h-sp*2,bw=sp*9,bh=sp*6;
      ctx.fillStyle='rgba(255,252,240,.95)';ctx.strokeStyle='rgba(40,30,25,.8)';ctx.lineWidth=Math.max(1,sp*.6);
      ctx.beginPath();if(ctx.roundRect)ctx.roundRect(bx,by-bh,bw,bh,sp*2);else ctx.rect(bx,by-bh,bw,bh);ctx.fill();ctx.stroke();
      ctx.beginPath();ctx.moveTo(bx+sp*1.5,by);ctx.lineTo(bx,by+sp*2);ctx.lineTo(bx+sp*3.5,by);ctx.fill();
      var on=Math.floor(agora/350+(a.x*7|0))%4;ctx.fillStyle='#3a2e28';
      for(var d=0;d<3;d++)if(d<on||on===0)ctx.fillRect(Math.round(bx+sp*(1.8+d*2.2)),Math.round(by-bh/2-sp*.6),Math.ceil(sp*1.2),Math.ceil(sp*1.2));
    }
  }
  ctx.restore();
}

function v25Repovoar(){
  var elenco=v25Elenco(),vivos=[],usados={},novasRotas=0;
  /* mantém quem já está a caminho e ainda pertence ao elenco */
  for(var i=0;i<v25Povo.length;i++){
    var a=v25Povo[i];
    if(a.tipo!=='ocioso'&&elenco.some(function(c){return c.b===a.de})){vivos.push(a);usados[a.de.id]=true}
  }
  for(var j=0;j<elenco.length&&vivos.length<elenco.length;j++){
    var c=elenco[j];
    if(usados[c.b.id])continue;
    if(novasRotas>=4)break;          /* no máximo quatro rotas novas por ciclo */
    var novo=v25Nascer(c);
    novasRotas++;
    if(novo){vivos.push(novo);usados[c.b.id]=true}
  }
  v25Ociosos(vivos);
  v25Povo=vivos;
}
/* Entra no pipeline entre as árvores e os prédios, para que as casas
   ocultem quem passa atrás delas. */
var v25BaseDrawTrees=drawTrees;
drawTrees=function(){v25BaseDrawTrees();v25Desenhar()};

/* O laço de render só repinta sob demanda; aqui pedimos quadro apenas
   enquanto há alguém andando e o mapa está à vista. */
function v25MapaVisivel(){
  if(typeof v23Aberto!=='function')return true;
  if(v23Aberto(editorFull)||v23Aberto(fileSidebar)||v23Aberto(aiPanel)||v23Aberto(menuEl))return false;
  return document.visibilityState!=='hidden';
}
function v25Ciclo(){
  if(!v25Ligado||!v25MapaVisivel()){v25Povo.length&&(v25Povo=[]);return}
  var agora=Date.now();
  if(agora-v25UltimoElenco>2500){v25UltimoElenco=agora;v25Repovoar()}
  if(!v25Povo.length)return;
  v25Passo(V25_FPS/1000);
  pedirAnimacao();
}
(function(){var scheduler=window.UrbeCore&&window.UrbeCore.service('scheduler');if(scheduler)scheduler.add('aquarium.pedestrians',v25Ciclo,{interval:V25_FPS,whenVisible:true});else v25Relogio=setInterval(v25Ciclo,V25_FPS)})();

/* Gancho de inspeção: tudo do app vive dentro de uma IIFE, o que torna
   impossível testar de fora. Estes acessos são só de leitura e existem
   para o harness de verificação — nenhum deles altera estado sozinho. */
window.URBE=window.URBE||{};
Object.defineProperties(window.URBE,{
  povo:{get:function(){return v25Povo},configurable:true},
  rotas:{get:function(){return v25Rotas},configurable:true},
  mundo:{get:function(){return world},configurable:true},
  fauna:{get:function(){return urbeFauna},configurable:true}

});
Object.assign(window.URBE,{
  passo:function(dt){v25Passo(dt)},
  olhar:function(x,y,z){camera.x=(x+.5)*TILE;camera.y=(y+.5)*TILE;if(z)camera.z=z;counts();pedirDesenho()},
  camera:function(){return{x:camera.x/TILE,y:camera.y/TILE,z:camera.z}},
  quadro:function(){drawGround();drawRegions();drawRoads();drawTrees();drawBuildings()},
  naEstrada:function(t){return world.roads.has(K(t.x,t.y))},
  diagnostico:function(){return {mapaVisivel:v25MapaVisivel(),ligado:v25Ligado,
    editor:v23Aberto(editorFull),explorador:v23Aberto(fileSidebar),ia:v23Aberto(aiPanel),
    menu:v23Aberto(menuEl),visibilidade:document.visibilityState,povo:v25Povo.length}}
});

/* Mudou a geografia ou os links, as rotas guardadas não valem mais. */
var v25BaseRebuild=rebuildRoadNetwork;
rebuildRoadNetwork=function(){var r=v25BaseRebuild();v25Rotas.clear();v25Povo=[];return r};

/* ---------- v0.27: ponte para o Workspace Core ---------- */
(function registrarWorkspaceCore(){
  var core=window.UrbeCore;
  if(!core)return;

  core.provide('legacy.runtime',{
    openCity:function(nome){return abrirCidade(nome)},
    save:function(){return salvarCidade()},
    navigate:function(destino){return v23Navegar(destino)},
    createNote:function(regionId){return criarNotaNoDestino(regionId||null)},
    createFolder:function(parentId){return criarPastaNoDestino(parentId||null)},
    rebuildRoads:function(){return rebuildRoadNetwork()},
    openVaults:function(){return abrirMenu()},
    aiSettings:function(){return v21OpenGlobalSettings()},
    version:function(){return V21_VERSION},
    vaultName:function(){return Disco.cidade||'Urbe'},
    exportZip:function(){return exportarVault()},
    ensureFolders:function(qtd){Object.keys(qtd||{}).sort(function(a,b){return a.split('/').length-b.split('/').length}).forEach(function(p){urbeGarantirPasta(p,qtd)});pedirDesenho()},
    importFiles:function(folder){var cam=caminhosRegioes(),alvo=null;if(folder)world.regions.forEach(function(r){if(cam.get(r.id)===folder)alvo=r.id});return v21ImportPicker(alvo)},
    regions:function(){var cam=caminhosRegioes();return world.regions.map(function(r){return{id:r.id,name:r.name,path:cam.get(r.id)}})}
  });

  [
    ['workspace.navigate.world','Abrir cidade','Navegação',function(){return v23Navegar('mundo')}],
    ['workspace.navigate.files','Abrir notas','Navegação',function(){return v23Navegar('arquivos')}],
    ['workspace.navigate.ai','Abrir IA','Navegação',function(){return v23Navegar('ia')}],
    ['workspace.navigate.vaults','Pasta do Urbe','Workspace',function(){return v21PickRoot()}],
    ['document.create','Nova nota','Documento',function(ctx){return criarNotaNoDestino(ctx&&ctx.regionId||null)}],
    ['document.open','Abrir nota','Documento',function(ctx){var docs=core.service('documents'),doc=docs&&docs.get(ctx&&(ctx.id||ctx.path));if(!doc)return false;var rt=window.UrbeArtifacts.route(doc,ctx);if(rt.handled)return rt.result;var ws=core.service('editor.workspace');if(ws&&(!ctx||ctx.source!=='history'))ws.visit(doc.id);var b=world.buildings.find(function(x){return x.tipo==='nota'&&x.documentId===doc.id});if(!b){var path=dirDe(doc.path),paths=caminhosRegioes(),r=world.regions.find(x=>paths.get(x.id)===path),pos=r?vagaNaRegiao(r,semente(doc.path),new Set()):vagaAleatoria(semente(doc.path),3,3);b={id:id('b'),documentId:doc.id,kind:'building',tipo:'nota',regionId:r?r.id:null,x:pos?pos.x:0,y:pos?pos.y:0,w:3,h:3,name:doc.title,ext:(doc.path.match(/\.[^.]+$/)||['.md'])[0],content:doc.content,sprite:'house1',anexos:[],created:doc.created||nowDate(),modified:doc.modified||nowDate()};world.buildings.push(b);marcarIndice();indexar();buildTree();pedirDesenho()}openFullEditor(b);return true}],
    ['folder.create','Nova pasta','Documento',function(ctx){return criarPastaNoDestino(ctx&&ctx.parentId||null)}],
    ['workspace.save','Salvar workspace','Workspace',function(){return salvarCidade()}]
  ].forEach(function(item){
    if(!core.commands.has(item[0]))core.commands.register(item[0],{title:item[1],category:item[2],execute:item[3]});
  });

  core.events.on('editor:navigation',function(evt){if(evt&&evt.id)core.commands.execute('document.open',{id:evt.id,source:'history'});});
  /* Operações canônicas do Explorer são refletidas no vault legado durante a migração. */
  core.events.on('explorer:operation',function(evt){
    if(!evt)return;
    if(evt.type==='rename'||evt.type==='move'){
      var from=evt.from,to=evt.to,b=world.buildings.find(function(x){return x.tipo==='nota'&&x.documentId===from.id})||world.buildings.find(function(x){return x.tipo==='nota'&&x.name.toLowerCase()===from.title.toLowerCase()});
      if(b){b.name=urbeNomeDoc(to);b.content=to.content;b.modified=nowDate();buildTree();agendarSalvar();marcarSinc()}
    }else if(evt.type==='duplicate'&&evt.to){
      var source=world.buildings.find(function(b){return b.tipo==='nota'&&b.documentId===evt.from.id});
      if(source){var copy={...source,id:id('b'),documentId:evt.to.id,name:urbeNomeDoc(evt.to),ext:(evt.to.path.match(/\.[^.]+$/)||['.md'])[0],content:evt.to.content,x:source.x+4,y:source.y+4,created:nowDate(),modified:nowDate()};world.buildings.push(copy);marcarIndice();indexar();buildTree();pedirDesenho()}
      agendarSalvar();marcarSinc();
    }else if(evt.type==='delete'){
      var ids=new Set((evt.documents||[]).map(function(d){return d.id}));
      world.buildings=world.buildings.filter(function(b){return b.tipo!=='nota'||!ids.has(b.documentId)});
      buildTree();indexar();counts();scheduleRoadRebuild();agendarSalvar();marcarSinc();
    }
  });
  core.events.on('command:after',function(evt){
    if(evt.id.indexOf('workspace.navigate.')===0){
      core.state.patch({mode:evt.id.slice('workspace.navigate.'.length)},{source:evt.id});
    }
  });
  /* Mantém o modelo documental sincronizado enquanto a UI antiga ainda edita buildings. */
  var docs=core.service('documents');
  function caminhoDocumentoDoPredio(b){
    if(!b||b.tipo!=='nota')return null;
    var cam=caminhosRegioes(),dir=b.regionId?(cam.get(b.regionId)||''):'',base=nomeSeguro(b.name);
    return (dir?dir+'/':'')+base+'.md';
  }
  core.provide('legacy.documents',{
    syncBuilding:function(b,source){
      var path=caminhoDocumentoDoPredio(b);if(!path||!docs)return null;
      var existing=(b.documentId&&docs.get(b.documentId))||docs.get(path);
      // Documento excluído/movido para a lixeira: o editor não pode ressuscitá-lo.
      if(b.documentId&&!docs.get(b.documentId))return null;
      if(existing&&existing.content===(b.content||''))return existing;
      if(source==='editor.input'){
        var session=core.service('editor.session');
        if(existing&&session)return session.record(existing.id,b.content||'',{source:'editor.input',modified:b.modified||null});
      }
      var updated=docs.upsert({id:existing&&existing.id||b.documentId||undefined,path:path,title:b.name,content:b.content||'',tags:b.tags||[],created:b.created||null,modified:b.modified||null},{source:source||'legacy.building'});b.documentId=updated.id;return updated;
    },
    rebuild:function(){
      if(!docs)return[];
      var cam=caminhosRegioes(),used=new Set(),items=[];
      world.buildings.filter(function(b){return b.tipo==='nota'}).forEach(function(b){
        var dir=b.regionId?(cam.get(b.regionId)||''):'',base=nomeSeguro(b.name),path=(dir?dir+'/':'')+base+'.md',n=2;
        while(used.has(path.toLowerCase()))path=(dir?dir+'/':'')+base+' ('+(n++)+').md';
        used.add(path.toLowerCase());var existing=(b.documentId&&docs.get(b.documentId))||docs.get(path);items.push({id:existing&&existing.id||b.documentId||undefined,path:path,title:b.name,content:b.content||'',tags:b.tags||[],created:b.created||null,modified:b.modified||null});
      });
      return docs.replaceAll(items,{source:'legacy.rebuild'});
    }
  });
  var oldMarkChanged=markChanged;
  markChanged=function(){
    var result=oldMarkChanged.apply(this,arguments);
    if(currentFile&&currentFile.tipo==='nota')core.service('legacy.documents').syncBuilding(currentFile,'editor.input');
    return result;
  };
  /* O modo Visual grava direto em currentFile.content; sem isto o DocumentStore
     ficava desatualizado e a reconciliação do save descartava a edição. */
  var oldVisualSync=syncVisualToMarkdown;
  syncVisualToMarkdown=function(){
    var result=oldVisualSync.apply(this,arguments);
    if(currentFile&&currentFile.tipo==='nota')core.service('legacy.documents').syncBuilding(currentFile,'editor.input');
    return result;
  };
  core.start();
})();

/* The old world is a view of the vault. All textual content and identities
   come from DocumentStore, including when legacy controls create a building. */
var urbeCore=window.UrbeCore,urbeDocs=urbeCore.service('documents'),urbePersistence=urbeCore.service('persistence');
urbeCore.events.on('workspace:dirty',()=>document.getElementById('saveState').textContent='Alterado');
urbeCore.events.on('workspace:saving',()=>document.getElementById('saveState').textContent='Salvando...');
urbeCore.events.on('workspace:saved',()=>document.getElementById('saveState').textContent='Salvo');
urbeCore.events.on('workspace:saveError',()=>document.getElementById('saveState').textContent='Erro ao salvar');
/* nome da casa = título sem a extensão (senão “X.page.json” vira “X.page.json.json” no sync) */
function urbeNomeDoc(d){var ext=(String(d.path).match(/\.[^./]+$/)||['.md'])[0],t=String(d.title||'');return t.toLowerCase().endsWith(ext.toLowerCase())?t.slice(0,-ext.length):t}
function urbeBuildingPath(b,regions){
  var dir=b.regionId?(regions.get(b.regionId)||''):'';
  return(dir?dir+'/':'')+nomeSeguro(b.name)+extNota(b);
}
function urbeReconcileWorld(){
  var regions=caminhosRegioes();
  for(const b of world.buildings){
    if(b.tipo!=='nota')continue;
    var path=urbeBuildingPath(b,regions),doc=b.documentId&&urbeDocs.get(b.documentId);
    if(!doc)doc=urbeDocs.get(path);
    if(doc&&!b.regionId&&doc.path.includes('/'))path=doc.path;
    if(!doc){doc=urbeDocs.upsert({path:path,content:b.content||'',created:b.created,modified:b.modified},{source:'world.create'})}
    else if(b.documentId&&doc.path!==path){doc=urbeDocs.upsert({...doc,path:path,content:b.content||''},{source:'world.move'})}
    b.documentId=doc.id;b.content=doc.content;
  }
}
var urbeLegacyDesired=estadoDesejado;
estadoDesejado=function(){
  urbeReconcileWorld();
  var state=urbeLegacyDesired(),map=JSON.parse(state.arquivos.get('.urbe/mapa.json'));
  for(const [path] of state.arquivos)if(path!=='.urbe/mapa.json'&&window.UrbeArtifacts.RE.text.test(path))state.arquivos.delete(path);
  var byId=new Map(world.buildings.filter(b=>b.tipo==='nota'&&b.documentId).map(b=>[b.documentId,b]));
  var regions=caminhosRegioes();map.notas={};
  for(const doc of urbeDocs.list()){
    state.arquivos.set(doc.path,doc.content);
    var b=byId.get(doc.id),old=b&&b.aiLocal;
    var dir=doc.path.includes('/')?doc.path.slice(0,doc.path.lastIndexOf('/')):'';
    if(dir)state.pastas.add(dir);
    map.notas[doc.path]={id:doc.id,x:b?b.x:0,y:b?b.y:0,sprite:b?b.sprite:'house1',tags:doc.tags,anexos:b?(b.anexos||[]).map(metaAnexoNota):[],criado:doc.created||(b&&b.created)||nowDate(),modificado:doc.modified||(b&&b.modified)||nowDate(),aiLocal:old||null};
  }
  state.arquivos.set('.urbe/mapa.json',JSON.stringify(map,null,1));
  urbePersistence.meta=map;
  return state;
};
/* Fonte do mapa para o escritor único (REQ-040): só com a cidade inteira carregada; durante a abertura vale o mapa lido do disco. */
urbePersistence.metadataProvider=function(){
  if(sincSuspenso||Disco.cidade!==urbePersistence.vault)return null;
  return JSON.parse(estadoDesejado().arquivos.get('.urbe/mapa.json'));
};
urbeCore.events.on('document:updated',function(evt){
  var d=evt.document,b=world.buildings.find(x=>x.tipo==='nota'&&x.documentId===d.id);
  if(b&&evt.meta&&evt.meta.source!=='editor.input'&&evt.meta.source!=='world.move'){
    if(assinaturaLinks(b.content||'')!==assinaturaLinks(d.content))scheduleRoadRebuild();
    b.content=d.content;b.name=urbeNomeDoc(d);b.ext=(d.path.match(/\.[^.]+$/)||['.md'])[0];
    var folder=dirDe(d.path),regions=caminhosRegioes();
    var region=world.regions.find(r=>regions.get(r.id)===folder);
    if(region||!folder)b.regionId=region?region.id:null;
    if(currentFile===b){bodyEditor.value=d.content;renderCurrentPreview()}
  }
  marcarSinc();
});
urbeCore.events.on('document:removed',function(evt){
  var b=world.buildings.find(x=>x.tipo==='nota'&&x.documentId===evt.document.id);
  if(b){world.buildings=world.buildings.filter(x=>x!==b);if(currentFile===b)closeFullEditor();marcarIndice();indexar();buildTree();pedirDesenho()}
  marcarSinc();
});
/* Toda nota vira casa, venha de onde vier (Notas, busca, lixeira, IA). A checagem
   espera o ciclo atual: quando a própria cidade cria a casa, ela liga o documentId
   logo depois do upsert, e não pode ganhar uma casa duplicada. */
urbeCore.events.on('document:created',function(evt){
  var d0=evt.document;if(!d0)return;
  Promise.resolve().then(function(){
    var d=urbeDocs.get(d0.id);if(!d)return;
    var regioes=caminhosRegioes();
    if(world.buildings.some(x=>x.tipo==='nota'&&(x.documentId===d.id||(!x.documentId&&urbeBuildingPath(x,regioes)===d.path))))return;
    urbeCasaParaDocumento(d);
  });
});
/* Pasta sem bairro (nota criada pelo Assistente, por plugin, importação ou Tutorial):
   cria a cadeia de bairros; se o filho não couber, o bairro pai cresce e tenta de novo.
   qtd = {caminho: quantas casas o bairro deve comportar}, para já nascer do tamanho certo. */
function urbeGarantirPasta(folder,qtd){
  if(!folder)return null;var segs=folder.split('/').filter(Boolean),parent=null,path='';
  for(var i=0;i<segs.length;i++){
    path=path?path+'/'+segs[i]:segs[i];var r=regiaoPorCaminho(path);
    if(!r){var q=Math.max(1,(qtd&&qtd[path])||1);
      for(var t=0;t<4&&!r;t++){r=criarRegiaoOrganica(segs[i],q,semente('pasta:'+path)+t,parent?parent.id:null,null,'');if(!r&&parent)expandirRegiao(parent,Math.max(80,q*36))}
      if(!r)return parent;marcarIndice();indexar();buildTree();marcarSinc();}
    parent=r;
  }
  return parent;
}
function urbeCasaParaDocumento(d){
  var folder=dirDe(d.path),paths=caminhosRegioes(),region=world.regions.find(r=>paths.get(r.id)===folder);
  if(!region&&folder)region=urbeGarantirPasta(folder);
  var pos=region?vagaNaRegiao(region,semente(d.path),new Set()):vagaAleatoria(semente(d.path),3,3);
  world.buildings.push({id:id('b'),documentId:d.id,kind:'building',tipo:'nota',regionId:region?region.id:null,x:pos?pos.x:0,y:pos?pos.y:0,w:3,h:3,name:urbeNomeDoc(d),ext:(d.path.match(/\.[^.]+$/)||['.md'])[0],content:d.content,sprite:['house1','house2','house3'][semente(d.path)%3],anexos:[],created:d.created||nowDate(),modified:d.modified||nowDate()});
  marcarIndice();indexar();buildTree();if(assinaturaLinks(d.content))scheduleRoadRebuild();pedirDesenho();marcarSinc();
}
/* pasta excluída pelo explorador novo: a região (e subpastas) sai da cidade; o sync apaga o diretório vazio */
urbeCore.events.on('explorer:folderRemoved',function(evt){
  var paths=caminhosRegioes(),r=world.regions.find(x=>paths.get(x.id)===evt.path);
  if(r)apagarRegiao(r,true);
});
urbeCore.events.on('explorer:folderCreated',function(evt){
  var paths=caminhosRegioes(),existing=world.regions.find(r=>paths.get(r.id)===evt.path);
  if(existing)return;
  var parentPath=dirDe(evt.path),parent=world.regions.find(r=>paths.get(r.id)===parentPath);
  var region=criarRegiaoOrganica(evt.path.split('/').pop(),1,semente(evt.path),parent?parent.id:null,null,'');
  if(region){marcarIndice();indexar();buildTree();pedirDesenho();marcarSinc()}
});
var urbeOpenLegacy=abrirCidade;
abrirCidade=async function(name){
  if(Disco.cidade&&Disco.cidade!==name){
    while(sincRodando)await new Promise(r=>setTimeout(r,20));
    await rodarSinc(true);
    await urbePersistence.flush();while(urbePersistence.busy)await new Promise(r=>setTimeout(r,20));urbePersistence.suspend(true);
  }
  await v21StopSync();
  await urbePersistence.load(name);
  await urbeOpenLegacy(name);
  if(Disco.cidade!==name)throw Error('Falha ao abrir o vault '+name);
  var regions=caminhosRegioes();
  for(const b of world.buildings){
    if(b.tipo!=='nota')continue;
    var path=urbeBuildingPath(b,regions),doc=urbeDocs.get(path);
    if(doc){b.documentId=doc.id;b.content=doc.content}
  }
  /* as ruas derivam dos links: refeitas depois que o conteúdo canônico chegou às casas */
  rebuildRoadNetwork();
  var projection=urbeCore.service('world.projection');
  if(projection)projection.load(urbePersistence.meta);
  estadoDesejado();
  var editorWorkspace=urbeCore.service('editor.workspace');
  if(editorWorkspace)editorWorkspace.restore();
  marcarSinc();
};

/* An existing installation may have several independent cities. Copy each
   source into a named folder inside Urbe; leave every source intact. */
async function urbeEnsureSingleVault(){
  const target='Urbe',names=await FS.cidades();
  if(!names.includes(target))await FS.criarCidade(target);
  const markerPath='.urbe/merged-v1.json',done=JSON.parse(await FS.ler(target,markerPath)||'{}');
  const existing=new Set(await FS.listar(target));
  let targetMap;try{targetMap=JSON.parse(await FS.ler(target,'.urbe/mapa.json')||'null')}catch(_){}
  if(!targetMap)targetMap={v:4,regioes:[],notas:{},construcoes:[]};
  targetMap.regioes=targetMap.regioes||[];targetMap.notas=targetMap.notas||{};targetMap.construcoes=targetMap.construcoes||[];
  for(const source of names){
    if(source===target||done[source])continue;
    const folder='Cidades/'+nomeSeguro(source),paths=await FS.listar(source);
    for(const path of paths){
      if(path.startsWith('.urbe/'))continue;
      const dest=folder+'/'+path;
      if(existing.has(dest))continue;
      if(path==='.pasta'||path.endsWith('/.pasta')){await FS.criarPasta(target,dirDe(dest));existing.add(dest);continue}
      if(window.UrbeArtifacts.RE.text.test(path)){
        const content=await FS.ler(source,path);
        if(content==null)throw Error('Não consegui ler '+source+'/'+path);
        await FS.escrever(target,dest,content);
      }else{
        const blob=await FS.lerBlob(source,path);
        if(!blob)throw Error('Não consegui ler '+source+'/'+path);
        await FS.escreverBlob(target,dest,blob);
      }
      existing.add(dest);
    }
    const oldMap=await FS.ler(source,'.urbe/mapa.json');
    if(oldMap!=null){
      await FS.escrever(target,'.urbe/origens/'+nomeSeguro(source)+'.json',oldMap);
      const original=JSON.parse(oldMap),prefix=folder+'/',regions=original.regioes||[],notes=original.notas||{};
      const positions=[...targetMap.regioes.map(r=>({x:r.x||0,w:r.w||0})),...targetMap.construcoes.map(b=>({x:b.x||0,w:b.w||0})),...Object.values(targetMap.notas).map(n=>({x:n.x||0,w:3}))];
      const minX=Math.min(0,...regions.map(r=>r.x||0),...Object.values(notes).map(n=>n.x||0),...(original.construcoes||[]).map(b=>b.x||0));
      const dx=positions.length?Math.max(...positions.map(p=>p.x+p.w))+40-minX:0;
      const withPrefix=path=>path?prefix+path:folder;
      const asset=a=>({...a,relPath:a.relPath?withPrefix(a.relPath):a.relPath,folderPath:a.folderPath?withPrefix(a.folderPath):folder});
      const note=n=>({...n,x:(n.x||0)+dx,anexos:(n.anexos||[]).map(asset)});
      for(const r of regions){const caminho=withPrefix(r.caminho);if(!targetMap.regioes.some(x=>x.caminho===caminho))targetMap.regioes.push({...r,caminho,x:(r.x||0)+dx})}
      for(const [path,n] of Object.entries(notes))targetMap.notas[withPrefix(path)]=note(n);
      for(const b of original.construcoes||[]){const caminho=withPrefix(b.caminho),files=(b.files||b.anexos||[]).map(asset);if(!targetMap.construcoes.some(x=>x.caminho===caminho&&x.name===b.name&&x.x===(b.x||0)+dx))targetMap.construcoes.push({...b,caminho,x:(b.x||0)+dx,files,anexos:files})}
      await FS.escrever(target,'.urbe/mapa.json',JSON.stringify(targetMap));
    }
    done[source]={folder:folder,importedAt:Date.now()};
    await FS.escrever(target,markerPath,JSON.stringify(done));
  }
  return target;
}
urbeCore.provide('workspace.storage',{chooseRoot:v21PickRoot});
abrirMenu=function(){
  menuEl.querySelector('.caixa').innerHTML='<div class="v21MenuHead"><div class="v21MenuLogo">URBE</div><span class="v21Version">v'+V21_VERSION+'</span></div><div class="v21StorageRow"><span class="v21StorageName" id="v21StorageName"></span><button class="v21IconBtn" id="v21PickRoot" title="Escolher pasta do Urbe" aria-label="Escolher pasta do Urbe">▣</button></div><p style="padding:1rem">Um mundo para todas as suas cidades. Organize os arquivos em pastas no explorador.</p>';
  menuEl.classList.add('open');document.getElementById('v21PickRoot').onclick=v21PickRoot;v21UpdateStorageName();
};
var urbeConfirmOriginal=v21ConfirmPlacement,urbeConfirmBusy=false;
v21ConfirmPlacement=async function(){
  if(urbeConfirmBusy)return;
  urbeConfirmBusy=true;
  document.getElementById('v21PlacementOk').disabled=true;
  try{await urbeConfirmOriginal()}finally{urbeConfirmBusy=false;v21PlacementStatus()}
};
document.getElementById('v21PlacementOk').onclick=v21ConfirmPlacement;

/* ============================================================
   v0.40 — a cidade (fase 3 do redesenho)
   Chão sem a “grade”, rótulos e seleção no mesmo estilo da interface,
   e a câmera abre mostrando as notas.
   ============================================================ */
var URBE_FONTE='-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Inter",sans-serif';
/* Os tiles de grama e água trazem, a partir do pixel 38, uma faixa escura herdada
   da folha de sprites; em mosaico ela vira uma grade sobre a cidade inteira.
   Usamos só a área limpa (38×38), esticada para o tamanho do tile. */
(function urbeChaoLimpo(){
  /* suaviza o contraste puxando cada pixel para a cor média do tile:
     mantém a textura pixel art, mas as manchas deixam de formar um padrão */
  function suavizar(x,w,h,forca){
    var img=x.getImageData(0,0,w,h),d=img.data,r=0,g=0,b=0,n=d.length/4;
    for(var i=0;i<d.length;i+=4){r+=d[i];g+=d[i+1];b+=d[i+2]}r/=n;g/=n;b/=n;
    for(var j=0;j<d.length;j+=4){d[j]+=(r-d[j])*forca;d[j+1]+=(g-d[j+1])*forca;d[j+2]+=(b-d[j+2])*forca}
    x.putImageData(img,0,0);
  }
  function limpar(chave,area,forca){
    var orig=A[chave];if(!orig)return;
    function gerar(){
      if(!orig.naturalWidth||orig._urbeLimpo)return;
      var c=document.createElement('canvas');c.width=orig.naturalWidth;c.height=orig.naturalHeight;
      var x=c.getContext('2d');x.imageSmoothingEnabled=false;x.drawImage(orig,0,0,area,area,0,0,c.width,c.height);if(forca)suavizar(x,c.width,c.height,forca);
      var im=new Image();im._urbeLimpo=true;im.onload=function(){A[chave]=im;if(chave==='grass')padraoGrama=null;pedirDesenho()};im.src=c.toDataURL('image/png');
    }
    if(orig.complete&&orig.naturalWidth)gerar();else orig.addEventListener('load',gerar,{once:true});
  }
  limpar('grass',38,.45);limpar('water',38,.3);
})();

function urbeRetanguloArredondado(c,x,y,w,h,r){r=Math.min(r,h/2,w/2);c.beginPath();c.moveTo(x+r,y);c.arcTo(x+w,y,x+w,y+h,r);c.arcTo(x+w,y+h,x,y+h,r);c.arcTo(x,y+h,x,y,r);c.arcTo(x,y,x+w,y,r);c.closePath()}
/* rótulo em pílula: mesma fonte, cor e raio da interface */
var urbeLarguras=new Map();
function urbeRotulo(texto,cx,topo,opts){
  opts=opts||{};var z=camera.z,tam=Math.round(Math.max(11,Math.min(15,12*z)));
  ctx.save();ctx.font=(opts.peso||600)+' '+tam+'px '+URBE_FONTE;ctx.textAlign='center';ctx.textBaseline='middle';
  var mk=ctx.font+'|'+texto,larg=urbeLarguras.get(mk);if(larg==null){larg=ctx.measureText(texto).width;if(urbeLarguras.size>4000)urbeLarguras.clear();urbeLarguras.set(mk,larg)}var padX=Math.round(tam*.65),alt=Math.round(tam*1.75),x=cx-larg/2-padX,w=larg+padX*2;
  if(opts.esquerda){x=cx;ctx.textAlign='left'}
  if(opts.medir){ctx.restore();return{x:x,y:topo,w:w+(opts.ponto?padX*.7:0),h:alt}}
  urbeRetanguloArredondado(ctx,x,topo,w,alt,alt/2);ctx.fillStyle=opts.fundo||'rgba(14,15,17,.82)';ctx.fill();
  if(opts.borda){ctx.strokeStyle=opts.borda;ctx.lineWidth=1.5;ctx.stroke()}
  if(opts.ponto){ctx.fillStyle=opts.ponto;ctx.beginPath();ctx.arc(x+padX*.9,topo+alt/2,tam*.28,0,Math.PI*2);ctx.fill();ctx.fillStyle=opts.cor||'#ececf0';ctx.fillText(texto,opts.esquerda?x+padX*1.6:cx+padX*.35,topo+alt/2+.5)}
  else{ctx.fillStyle=opts.cor||'#ececf0';ctx.fillText(texto,opts.esquerda?x+padX:cx,topo+alt/2+.5)}
  ctx.restore();return{x:x,y:topo,w:w+(opts.ponto?padX*.7:0),h:alt};
}
function urbeNomeCasa(b){var raw=nomeExibidoArquivo(b);if(b.tipo==='nota')raw=raw.replace(window.UrbeArtifacts.RE.note,'');return raw.length>30?raw.slice(0,28)+'…':raw}

drawBuildings=function(){
  var f=faixaVisivel(),rotulos=[];
  for(var i=0;i<world.buildings.length;i++){
    var b=world.buildings[i];if(b.x>f.x1||b.x+b.w<f.x0||b.y>f.y1||b.y+b.h<f.y0)continue;
    if(selected===b){ /* seleção: halo arredondado na cor de destaque, desenhado sob a casa */
      var sp=w2s((b.x-.15)*TILE,(b.y-.45)*TILE),sw=(b.w+.3)*TILE*camera.z,sh=(b.h+.6)*TILE*camera.z;
      ctx.save();urbeRetanguloArredondado(ctx,sp.x,sp.y,sw,sh,10*camera.z);ctx.fillStyle='rgba(143,179,255,.16)';ctx.fill();ctx.strokeStyle='#8fb3ff';ctx.lineWidth=2;ctx.stroke();ctx.restore();
    }
    if(b.tipo==='nota')v21DrawEditableBuilding(b);else drawArquivoBuilding(b);
    if((camera.z>=.42&&urbeOpcoes.nomes)||selected===b)rotulos.push(b);
  }
  if(typeof urbeAntesDosRotulos==='function')urbeAntesDosRotulos();
  /* rótulos por último, para nunca ficarem sob outra casa. Os nomes dos bairros
     têm a vez; nome de casa que encostaria em outro nome fica escondido
     (a casa selecionada sempre mostra o dela) */
  var usados=typeof urbeLayoutNomesBairros==='function'?urbeLayoutNomesBairros():[],bairros=usados.slice();
  rotulos.sort(function(a,b){return(selected===b)-(selected===a)});
  for(var j=0;j<rotulos.length;j++){var r=rotulos[j],q=w2s((r.x+r.w/2)*TILE,(r.y+r.h)*TILE),op=selected===r?{fundo:'#8fb3ff',cor:'#0b1325'}:{},nome=urbeNomeCasa(r);
    var cx=urbeRotulo(nome,q.x,q.y+4*camera.z,Object.assign({medir:true},op));
    if(selected!==r&&usados.some(function(u){return cx.x<u.x+u.w+3&&cx.x+cx.w+3>u.x&&cx.y<u.y+u.h+2&&cx.y+cx.h+2>u.y}))continue;
    usados.push(cx);urbeRotulo(nome,q.x,q.y+4*camera.z,op)}
  if(typeof urbeDesenharNomesBairros==='function')urbeDesenharNomesBairros(bairros);
};

drawRegions=function(){
  var f=faixaVisivel(),vis=world.regions.filter(function(r){return r.x<f.x1&&r.x+r.w>f.x0&&r.y<f.y1&&r.y+r.h>f.y0}).sort(function(a,b){return b.w*b.h-a.w*a.h}),sz=TILE*camera.z;
  vis.forEach(function(r){
    var nivel=caminhoRegiao(r).length-1;ctx.save();ctx.fillStyle=r.color+(nivel?'14':'1c');ctx.strokeStyle=r.color+'cc';ctx.lineWidth=nivel?1.5:2;ctx.lineJoin='round';
    if(r.cells){
      if(!r._cellSet)r._cellSet=new Set(r.cells);
      ctx.beginPath();
      for(var k=0;k<r.cells.length;k++){var a=r.cells[k].split(','),x=+a[0],y=+a[1];if(x<f.x0-1||x>f.x1+1||y<f.y0-1||y>f.y1+1)continue;var p=w2s(x*TILE,y*TILE);ctx.fillRect(p.x,p.y,sz+.5,sz+.5);
        if(!r._cellSet.has(K(x+1,y))){ctx.moveTo(p.x+sz,p.y);ctx.lineTo(p.x+sz,p.y+sz)}
        if(!r._cellSet.has(K(x-1,y))){ctx.moveTo(p.x,p.y);ctx.lineTo(p.x,p.y+sz)}
        if(!r._cellSet.has(K(x,y+1))){ctx.moveTo(p.x,p.y+sz);ctx.lineTo(p.x+sz,p.y+sz)}
        if(!r._cellSet.has(K(x,y-1))){ctx.moveTo(p.x,p.y);ctx.lineTo(p.x+sz,p.y)}}
      ctx.stroke();
    }else{var p2=w2s(r.x*TILE,r.y*TILE);urbeRetanguloArredondado(ctx,p2.x,p2.y,r.w*sz,r.h*sz,8*camera.z);ctx.fill();ctx.setLineDash(nivel?[4,4]:[]);ctx.stroke();ctx.setLineDash([])}
    ctx.restore();
    if(camera.z>=.35&&urbeOpcoes.bairros){var t=w2s(r.x*TILE,r.y*TILE);urbeRotulo(r.name,t.x+8,t.y+8,{esquerda:true,ponto:r.color,peso:nivel?500:650})}
  });
};

/* ruas: caminho de pedra contínuo, conectado aos vizinhos. O sprite antigo era
   vertical e tinha borda escura, então ruas horizontais viravam blocos soltos. */
var urbeRuaViz=null,urbeRuaVizDe=null;
/* vizinhos de cada trecho (1 leste, 2 oeste, 4 sul, 8 norte), refeitos só quando a rede muda */
function urbeVizinhosRuas(a){
  if(urbeRuaVizDe===a&&urbeRuaViz)return urbeRuaViz;
  var m=new Uint8Array(a.length/2);for(var i=0,j=0;i<a.length;i+=2,j++){var x=a[i],y=a[i+1];
    m[j]=(world.roads.has(K(x+1,y))?1:0)|(world.roads.has(K(x-1,y))?2:0)|(world.roads.has(K(x,y+1))?4:0)|(world.roads.has(K(x,y-1))?8:0)}
  urbeRuaViz=m;urbeRuaVizDe=a;return m;
}
drawRoads=function(){
  var f=faixaVisivel(),a=roadsParsed(),viz=urbeVizinhosRuas(a),s=TILE*camera.z,lg=s*.56,mg=(s-lg)/2,borda=Math.max(1,s*.07);
  function trecho(x,y,v,extra){
    var p=w2s(x*TILE,y*TILE),cx=p.x+mg-extra,cy=p.y+mg-extra,l=lg+extra*2;
    ctx.fillRect(cx,cy,l,l);
    if(v&1)ctx.fillRect(cx+l-extra*2,cy,s-mg-l+extra*3+mg,l);
    if(v&2)ctx.fillRect(p.x-.5,cy,mg+extra+.5,l);
    if(v&4)ctx.fillRect(cx,cy+l-extra*2,l,s-mg-l+extra*3+mg);
    if(v&8)ctx.fillRect(cx,p.y-.5,l,mg+extra+.5);
  }
  ctx.save();
  for(var pass=0;pass<2;pass++){ctx.fillStyle=pass===0?'#6f6250':'#b8a684';
    for(var i=0,j=0;i<a.length;i+=2,j++){var x=a[i],y=a[i+1];if(x<f.x0-1||x>f.x1+1||y<f.y0-1||y>f.y1+1)continue;
      trecho(x,y,viz[j],pass===0?borda:0)}}
  if(camera.z>=.7){ctx.fillStyle='rgba(111,98,80,.35)';for(var j=0;j<a.length;j+=2){var x2=a[j],y2=a[j+1];if(x2<f.x0||x2>f.x1||y2<f.y0||y2>f.y1)continue;var q=w2s(x2*TILE,y2*TILE),u=s/8;
    ctx.fillRect(q.x+s*.38,q.y+s*.34,u,u);ctx.fillRect(q.x+s*.56,q.y+s*.58,u,u)}}
  ctx.restore();
};

/* abre mostrando as notas: se nenhuma casa está à vista, enquadra todas */
function urbeEnquadrarNotas(forcar){
  var bs=world.buildings;if(!bs.length)return false;
  var f=faixaVisivel(),visivel=function(b){return b.x>=f.x0&&b.x+b.w<=f.x1&&b.y>=f.y0&&b.y+b.h<=f.y1};
  var x0=Infinity,y0=Infinity,x1=-Infinity,y1=-Infinity;
  bs.forEach(function(b){x0=Math.min(x0,b.x);y0=Math.min(y0,b.y-1);x1=Math.max(x1,b.x+b.w);y1=Math.max(y1,b.y+b.h+1)});
  var margem=3,largura=(x1-x0+margem*2)*TILE,altura=(y1-y0+margem*2)*TILE,util=Math.max(200,cv.h-170),zIdeal=Math.min(cv.w/largura,util/altura);
  /* ao abrir: só mexe se há casas fora da tela e todas cabem num zoom legível,
     ou se nenhuma casa está à vista */
  if(!forcar){var todas=bs.every(visivel),alguma=bs.some(function(b){return b.x+b.w>f.x0&&b.x<f.x1&&b.y+b.h>f.y0&&b.y<f.y1});if(todas||(alguma&&zIdeal<.45))return false}
  camera.x=(x0+x1)/2*TILE;camera.y=(y0+y1)/2*TILE;
  camera.z=clamp(zIdeal,.45,1.3);
  counts();pedirDesenho();return true;
}
(function urbeEnquadrarAoAbrir(){
  var core=window.UrbeCore;if(!core)return;
  core.events.on('workspace:loaded',function(){requestAnimationFrame(function(){requestAnimationFrame(function(){urbeEnquadrarNotas(false)})})});
  core.commands.register('city.fit',{title:'Enquadrar todas as notas',category:'Cidade',execute:function(){return urbeEnquadrarNotas(true)}});
})();

/* ============================================================
   v0.41 — mundo vivo
   Terreno com geografia (src/world/terrain.js) e arte pixel medieval
   (src/world/pixel-art.js). Regras: não se constrói em água, pântano,
   montanha ou neve eterna; ruas cruzam rios e lagos por pontes, custam mais
   em colinas, matas e pântanos, e não atravessam mar nem montanha.
   ============================================================ */
var MUNDO=window.UrbeTerrain?UrbeTerrain.createWorld('urbe',{spawnX:Math.floor(BASE_W/2),spawnY:Math.floor(BASE_H/2)}):null;
var ARTE=window.UrbeArt||null;
/* tile já ocupado pela cidade: casas antigas continuam válidas mesmo onde o
   terreno novo é água ou montanha (a cidade “aterrou” o lugar) */
function urbeAssentado(x,y){return world.roads.has(K(x,y))||!!bAt({x:x,y:y})}
if(MUNDO){
  ehAgua=function(x,y){return !MUNDO.buildable(x,y)&&!urbeAssentado(x,y)};
  arvoreEm=function(x,y){var t=MUNDO.tree(x,y);return t?1:0};
}
function urbeBloqueiaRua(x,y){return MUNDO?(!MUNDO.roadable(x,y)&&!urbeAssentado(x,y)):ehAgua(x,y)}
function urbeCustoRua(x,y){return MUNDO&&!urbeAssentado(x,y)?MUNDO.roadCost(x,y):1}
function urbeMotivoTerreno(x,y,w,h){if(!MUNDO)return null;for(var yy=y;yy<y+(h||3);yy++)for(var xx=x;xx<x+(w||3);xx++){if(urbeAssentado(xx,yy))continue;var r=MUNDO.reason(xx,yy);if(r)return r}return null}

/* ---------- chão: um canvas por chunk, gerado aos poucos ---------- */
var urbeChunks=new Map(),URBE_CHUNKS_MAX=innerWidth<900?56:96,urbeFila=[];
function urbeChunkPronto(cx,cy){var k=cx+':'+cy,c=urbeChunks.get(k);if(c){urbeChunks.delete(k);urbeChunks.set(k,c)}return c||null}
/* os pixels do chão saem de um worker; a página só monta o canvas pronto */
var urbeWorker=null,urbePedidos=new Set(),urbeGeracaoChao=0;
(function(){
  if(!MUNDO||!ARTE||!window.Worker||location.protocol==='file:')return;
  try{
    urbeWorker=new Worker('./src/world/chunk-worker.js');
    urbeWorker.postMessage({type:'init',seed:MUNDO.seed,opts:{spawnX:Math.floor(BASE_W/2),spawnY:Math.floor(BASE_H/2)}});
    urbeWorker.onmessage=function(e){var m=e.data;if(!m||m.type!=='chunk')return;var k=m.cx+':'+m.cy;urbePedidos.delete(k);if((m.gen|0)!==urbeGeracaoChao){pedirDesenho();return}if(m.bio)MUNDO.inject(m.cx,m.cy,new Uint8Array(m.bio),new Float32Array(m.elev));
      var cvc=ARTE.canvasFromPixels(new Uint8ClampedArray(m.buf),m.n);if(m.far)cvc.far=ARTE.canvasFromPixels(new Uint8ClampedArray(m.far),m.n>>1);urbeChunks.set(k,cvc);
      if(urbeChunks.size>URBE_CHUNKS_MAX)urbeChunks.delete(urbeChunks.keys().next().value);pedirDesenho()};
    urbeWorker.onerror=function(){urbeWorker=null;urbePedidos.clear()};
  }catch(_){urbeWorker=null}
})();
function urbeGerarChunks(orcamentoMs){
  if(urbeWorker){ /* no máximo 6 pedidos em voo; o mais próximo do centro primeiro */
    for(var i=0;i<urbeFila.length&&urbePedidos.size<6;i++){var q=urbeFila[i],k=q[0]+':'+q[1];if(urbeChunks.has(k)||urbePedidos.has(k))continue;urbePedidos.add(k);urbeWorker.postMessage({type:'chunk',cx:q[0],cy:q[1],gen:urbeGeracaoChao})}
    return;
  }
  var t0=performance.now();
  while(urbeFila.length&&performance.now()-t0<orcamentoMs){
    var q=urbeFila.shift(),k=q[0]+':'+q[1];if(urbeChunks.has(k))continue;
    urbeChunks.set(k,ARTE.renderChunk(MUNDO,q[0],q[1]));
    if(urbeChunks.size>URBE_CHUNKS_MAX)urbeChunks.delete(urbeChunks.keys().next().value);
  }
  if(urbeFila.length)pedirDesenho();
}
var urbeCorBioma=null;
function urbeCores(){if(urbeCorBioma)return urbeCorBioma;urbeCorBioma=ARTE.IDS.map(function(id){return ARTE.PAL[id][0]});return urbeCorBioma}
drawGround=function(){
  if(!MUNDO||!ARTE){ctx.fillStyle='#4f7a3a';ctx.fillRect(0,0,cv.w,cv.h);return}
  var f=faixaVisivel(),CHs=MUNDO.CH,cx0=Math.floor(f.x0/CHs),cx1=Math.floor(f.x1/CHs),cy0=Math.floor(f.y0/CHs),cy1=Math.floor(f.y1/CHs),lado=CHs*TILE*camera.z;
  var faltam=[],cores=urbeCores();
  ctx.save();ctx.imageSmoothingEnabled=false;
  for(var cy=cy0;cy<=cy1;cy++)for(var cx=cx0;cx<=cx1;cx++){
    var p=w2s(cx*CHs*TILE,cy*CHs*TILE),img=urbeChunkPronto(cx,cy),x=Math.floor(p.x),y=Math.floor(p.y),l=Math.ceil(lado)+1;
    if(img){
      /* longe: versão com as copas das árvores pintadas; bairros ficam sem árvores, como de perto */
      if(img.far&&camera.z<URBE_LONGE){ctx.imageSmoothingEnabled=camera.z<.2;ctx.drawImage(img.far,x,y,l,l);ctx.imageSmoothingEnabled=false;
        var cx0t=cx*CHs,cy0t=cy*CHs,px2=img.width/CHs;
        for(var ri=0;ri<world.regions.length;ri++){var rg=world.regions[ri],ax=Math.max(rg.x,cx0t),ay=Math.max(rg.y,cy0t),bx=Math.min(rg.x+rg.w,cx0t+CHs),by=Math.min(rg.y+rg.h,cy0t+CHs);if(ax>=bx||ay>=by)continue;
          ctx.drawImage(img,(ax-cx0t)*px2,(ay-cy0t)*px2,(bx-ax)*px2,(by-ay)*px2,x+(ax-cx0t)*TILE*camera.z,y+(ay-cy0t)*TILE*camera.z,Math.ceil((bx-ax)*TILE*camera.z)+1,Math.ceil((by-ay)*TILE*camera.z)+1)}}
      else ctx.drawImage(img,x,y,l,l);
    }
    else{ /* enquanto o chunk não fica pronto: cor de cada tile se o terreno já é conhecido;
             senão um tom neutro — o ruído não é calculado aqui quando há worker */
      if(!urbeWorker||MUNDO.has(cx,cy)){var ch=MUNDO.chunk(cx,cy),s=TILE*camera.z;
        for(var j=0;j<CHs;j++)for(var i=0;i<CHs;i++){ctx.fillStyle=cores[ch.bio[j*CHs+i]];ctx.fillRect(Math.floor(p.x+i*s),Math.floor(p.y+j*s),Math.ceil(s)+1,Math.ceil(s)+1)}}
      else{ctx.fillStyle='#3d5a36';ctx.fillRect(x,y,l,l)}
      faltam.push([cx,cy]);
    }
  }
  /* aterro: casas antigas sobre terreno proibido ganham um lote de terra firme */
  var grama=urbeTexturaGrama(),st=TILE*camera.z;
  for(var bi=0;bi<world.buildings.length;bi++){var b=world.buildings[bi];if(b.x>f.x1||b.x+b.w<f.x0||b.y>f.y1||b.y+b.h<f.y0)continue;
    for(var yy=b.y-1;yy<b.y+b.h+1;yy++)for(var xx=b.x-1;xx<b.x+b.w+1;xx++)if(!MUNDO.buildable(xx,yy)){var q=w2s(xx*TILE,yy*TILE);ctx.drawImage(grama,Math.floor(q.x),Math.floor(q.y),Math.ceil(st)+1,Math.ceil(st)+1)}}
  ctx.restore();
  if(faltam.length){ /* o que está mais perto do centro da tela sai primeiro */
    var c0=s2w(cv.w/2,cv.h/2),ccx=c0.x/TILE/CHs,ccy=c0.y/TILE/CHs;
    faltam.sort(function(a,b){return Math.hypot(a[0]+.5-ccx,a[1]+.5-ccy)-Math.hypot(b[0]+.5-ccx,b[1]+.5-ccy)});
    urbeFila=faltam;urbeGerarChunks(8);
  }
  urbeAtualizarBioma();
};
var urbeGramaCv=null;
function urbeTexturaGrama(){if(urbeGramaCv)return urbeGramaCv;var c=document.createElement('canvas');c.width=c.height=ARTE.PX;var x=c.getContext('2d'),img=x.createImageData(ARTE.PX,ARTE.PX);img.data.set(ARTE.texture('grass',1));x.putImageData(img,0,0);urbeGramaCv=c;return c}

/* ---------- vegetação ---------- */
var urbeArvoresCache=new Map();
function urbeArvoresDoChunk(cx,cy){var k=cx+':'+cy,a=urbeArvoresCache.get(k);if(a)return a;a=[];var CHs=MUNDO.CH;
  for(var j=0;j<CHs;j++)for(var i=0;i<CHs;i++){var x=cx*CHs+i,y=cy*CHs+j,t=MUNDO.tree(x,y);if(t)a.push(x,y,t)}
  if(urbeArvoresCache.size>400)urbeArvoresCache.clear();urbeArvoresCache.set(k,a);return a}
var URBE_LONGE=.5;
drawTrees=function(){
  if(MUNDO&&ARTE&&camera.z>=(urbeWorker?URBE_LONGE:.3)){
    var f=faixaVisivel(),CHs=MUNDO.CH,s=TILE*camera.z/ARTE.PX*1.3,p={x:0,y:0},vis=[];
    for(var cy=Math.floor(f.y0/CHs);cy<=Math.floor(f.y1/CHs);cy++)for(var cx=Math.floor(f.x0/CHs);cx<=Math.floor(f.x1/CHs);cx++){
      if(urbeWorker&&!MUNDO.has(cx,cy))continue; /* árvores aparecem junto com o chão */
      var a=urbeArvoresDoChunk(cx,cy);
      for(var i=0;i<a.length;i+=3){var x=a[i],y=a[i+1];if(x<f.x0||x>f.x1||y<f.y0||y>f.y1+2)continue;
        /* ruas ganham acostamento livre; casas e bairros não têm árvores por cima */
        if(world.roads.has(K(x,y))||world.roads.has(K(x+1,y))||world.roads.has(K(x-1,y))||world.roads.has(K(x,y+1)))continue;
        p.x=x;p.y=y;if(bAt(p)||regAt(p))continue;p.y=y+1;if(bAt(p))continue;
        vis.push(x,y,a[i+2])}
    }
    /* de trás para a frente: quem está mais ao sul cobre quem está atrás */
    var ord=[];for(i=0;i<vis.length;i+=3)ord.push(i);ord.sort(function(m,n){return vis[m+1]-vis[n+1]||vis[m]-vis[n]});
    ctx.save();ctx.imageSmoothingEnabled=false;
    for(var o=0;o<ord.length;o++){var ix=ord[o],tx=vis[ix],ty=vis[ix+1],t=vis[ix+2],img=ARTE.tree(t.kind,t.v,t.snow),q=w2s((tx+.5)*TILE,(ty+1)*TILE),jx=(MUNDO.hash(tx,ty,31)-.5)*8*s,jy=(MUNDO.hash(tx,ty,32)-.5)*5*s;
      ctx.drawImage(img,Math.round(q.x-12*s+jx),Math.round(q.y-30*s+jy),Math.ceil(24*s),Math.ceil(32*s))}
    ctx.restore();
  }
  if(typeof v25Desenhar==='function')v25Desenhar();
};
/* ---------- fauna: ovelhas, vacas, cervos, patos e pássaros ----------
   Só existe em volta do que está na tela e só em chunks já prontos (nada de
   calcular terreno na thread principal). Evita ruas, casas e bairros.
   Mesmo relógio dos moradores: ~12 quadros por segundo, e só pede quadro
   quando há bicho visível. */
/* opções do mundo que a Personalização liga e desliga */
var urbeOpcoes={moradores:true,fauna:true,clima:true,eventos:true,nomes:true,bairros:true,ambiente:'ciclo',editor:'visual'};
var urbeFauna=[],urbeFaunaT=0,urbeFaunaUlt=0,urbeFaunaAves=0;
(function(){
  if(!MUNDO||!ARTE||!ARTE.animal)return;
  var B=MUNDO.B,VEL={sheep:.5,cow:.38,deer:.9,duck:.32},MAX=18;
  function pronto(x,y){return MUNDO.has(Math.floor(x/MUNDO.CH),Math.floor(y/MUNDO.CH))}
  function classe(x,y){if(!pronto(x,y))return null;var b=MUNDO.biome(x,y);
    if(b===B.LAKE||b===B.RIVER)return'agua';if(b===B.GRASS||b===B.MEADOW||b===B.STEPPE||b===B.SAVANNA||b===B.HILLS)return'campo';if(b===B.FOREST||b===B.DENSE||b===B.TAIGA)return'mata';return null}
  function livre(x,y){x=Math.floor(x);y=Math.floor(y);if(world.roads.has(K(x,y)))return false;var p={x:x,y:y};return !bAt(p)&&!regAt(p)}
  function nascer(f){
    var w=f.x1-f.x0,h=f.y1-f.y0;
    for(var tent=0;tent<14;tent++){var x=f.x0-3+Math.random()*(w+6),y=f.y0-3+Math.random()*(h+6),c=classe(Math.floor(x),Math.floor(y));if(!c||!livre(x,y))continue;
      var kind=c==='agua'?'duck':c==='mata'?(Math.random()<.45?'deer':null):(Math.random()<.72?'sheep':'cow');if(!kind)continue;
      var n=kind==='deer'?1+(Math.random()<.4?1:0):kind==='duck'?2+Math.floor(Math.random()*2):2+Math.floor(Math.random()*3);
      for(var i=0;i<n;i++){var ax=x+(Math.random()-.5)*2.4,ay=y+(Math.random()-.5)*1.8;if(classe(Math.floor(ax),Math.floor(ay))!==c||!livre(ax,ay))continue;
        urbeFauna.push({kind:kind,c:c,x:ax,y:ay,tx:ax,ty:ay,st:'idle',t:Math.random()*3,fr:0,ft:0,flip:Math.random()<.5})}
      return}
  }
  function aves(f){var n=3+Math.floor(Math.random()*3),fromLeft=Math.random()<.5,y=f.y0+Math.random()*(f.y1-f.y0),x=fromLeft?f.x0-4:f.x1+4,vx=(fromLeft?1:-1)*(3+Math.random()*1.5),vy=(Math.random()-.5)*1.2;
    for(var i=0;i<n;i++)urbeFauna.push({kind:'bird',x:x-(fromLeft?1:-1)*i*.9,y:y+(i%2?.6:-.6)*Math.ceil(i/2),vx:vx,vy:vy,fr:i%2,ft:0,flip:!fromLeft,alt:1.6+Math.random()*.8})}
  function passo(dt,f){
    var cx=(f.x0+f.x1)/2,cy=(f.y0+f.y1)/2,lim=Math.max(f.x1-f.x0,f.y1-f.y0)*1.2+8;
    urbeFauna=urbeFauna.filter(function(a){return Math.abs(a.x-cx)<lim&&Math.abs(a.y-cy)<lim});
    for(var i=0;i<urbeFauna.length;i++){var a=urbeFauna[i];a.ft+=dt;
      if(a.kind==='bird'){a.x+=a.vx*dt;a.y+=a.vy*dt;if(a.ft>.16){a.ft=0;a.fr^=1}continue}
      if(a.st==='walk'){var dx=a.tx-a.x,dy=a.ty-a.y,d=Math.hypot(dx,dy),v=VEL[a.kind]*dt;
        if(d<=v){a.x=a.tx;a.y=a.ty;a.st=Math.random()<.6&&a.kind!=='duck'?'graze':'idle';a.t=1.5+Math.random()*4}else{a.x+=dx/d*v;a.y+=dy/d*v;a.flip=dx<0}
        if(a.ft>.22){a.ft=0;a.fr=a.fr===0?1:0}}
      else{a.t-=dt;a.fr=a.st==='graze'?2:(a.kind==='duck'?(a.ft>.6?(a.ft=0,a.fr^1):a.fr):0);
        if(a.t<=0){for(var k=0;k<6;k++){var nx=a.x+(Math.random()-.5)*6,ny=a.y+(Math.random()-.5)*4;if(classe(Math.floor(nx),Math.floor(ny))===a.c&&livre(nx,ny)){a.tx=nx;a.ty=ny;a.st='walk';break}}if(a.st!=='walk')a.t=1+Math.random()*2}}
    }
  }
  function ciclo(){
    if(typeof v25MapaVisivel==='function'&&!v25MapaVisivel())return;
    var agora=performance.now(),dt=Math.min(.25,(agora-(urbeFaunaUlt||agora))/1000);urbeFaunaUlt=agora;
    if(camera.z<.3||!urbeOpcoes.fauna){if(urbeFauna.length){urbeFauna=[];pedirDesenho()}return}
    var f=faixaVisivel();
    if(agora-urbeFaunaT>900){urbeFaunaT=agora;var chao=0;for(var i=0;i<urbeFauna.length;i++)if(urbeFauna[i].kind!=='bird')chao++;if(chao<MAX)nascer(f);
      if(agora-urbeFaunaAves>14000&&Math.random()<.25){urbeFaunaAves=agora;aves(f)}}
    if(!urbeFauna.length)return;passo(dt,f);pedirAnimacao();
  }
  var sch=window.UrbeCore&&window.UrbeCore.service('scheduler');if(sch)sch.add('world.fauna',ciclo,{interval:40,whenVisible:true});else setInterval(ciclo,40);
})();
function urbeDesenharFauna(){
  if(!urbeFauna.length||camera.z<.3)return;var sp=TILE*camera.z/16,f=faixaVisivel(),ord=urbeFauna.slice().sort(function(a,b){return a.y-b.y});
  ctx.save();ctx.imageSmoothingEnabled=false;
  for(var i=0;i<ord.length;i++){var a=ord[i];if(a.x<f.x0-2||a.x>f.x1+2||a.y<f.y0-2||a.y>f.y1+3)continue;
    if(a.kind==='bird'){ /* sombra no chão e o bando no alto */
      var qs=w2s(a.x*TILE,a.y*TILE),img=ARTE.animal('bird',a.fr,a.flip);ctx.fillStyle='rgba(0,0,0,.16)';ctx.beginPath();ctx.ellipse(qs.x,qs.y,3*sp,1.2*sp,0,0,7);ctx.fill();
      ctx.drawImage(img,Math.round(qs.x-img.width*sp/2),Math.round(qs.y-a.alt*TILE*camera.z),Math.ceil(img.width*sp),Math.ceil(img.height*sp));continue}
    if(camera.z<.45)continue;
    var q=w2s(a.x*TILE,a.y*TILE),im=ARTE.animal(a.kind,a.fr,a.flip),w=im.width*sp,h=im.height*sp;
    if(a.kind!=='duck'){ctx.fillStyle='rgba(0,0,0,.2)';ctx.beginPath();ctx.ellipse(q.x,q.y,w*.38,sp*1.4,0,0,7);ctx.fill()}
    ctx.drawImage(im,Math.round(q.x-w/2),Math.round(q.y-h+(a.kind==='duck'?sp*2:0)),Math.ceil(w),Math.ceil(h))}
  ctx.restore();
}

/* fauna por cima das árvores e dos moradores, antes das casas */
var urbeFaunaBaseTrees=drawTrees;drawTrees=function(){urbeFaunaBaseTrees();urbeDesenharFauna()};

/* ---------- construções no mesmo estilo do mundo ---------- */
var URBE_OFICIO={md:'house',markdown:'house',txt:'house',html:'hall',htm:'hall',js:'workshop',mjs:'workshop',css:'dyer',json:'tower',yaml:'tower',yml:'tower',csv:'market'};
function urbeArteDaConstrucao(b){
  var kind=b.tipo==='nota'?(URBE_OFICIO[v21Kind(b)]||'house'):'store',cx=Math.floor(b.x+b.w/2),cy=Math.floor(b.y+b.h/2);
  if(urbeSpritesProprios[kind])return urbeSpritesProprios[kind];
  var bioma=MUNDO?ARTE.IDS[MUNDO.biome(cx,cy)]:'grass',variant=b.sprite==='house2'?1:b.sprite==='house3'?2:(semente(b.id||b.name)%3);
  return ARTE.building(kind,ARTE.styleFor(bioma),variant,(semente(b.name||'')%3)===0);
}
function urbeDesenharConstrucao(b){
  var img=urbeArteDaConstrucao(b),s=TILE*camera.z/ARTE.PX,p=w2s(b.x*TILE,b.y*TILE),w=48*s*(b.w/3),h=56*s*(b.h/3);
  ctx.save();ctx.imageSmoothingEnabled=false;ctx.drawImage(img,Math.round(p.x),Math.round(p.y+b.h*TILE*camera.z-h),Math.ceil(w),Math.ceil(h));ctx.restore();
}
if(ARTE){v21DrawEditableBuilding=urbeDesenharConstrucao;drawArquivoBuilding=urbeDesenharConstrucao}

/* ---------- ruas com pontes ---------- */
var urbeRuasBase=drawRoads;
drawRoads=function(){
  urbeRuasBase();
  if(!MUNDO)return;
  var f=faixaVisivel(),a=roadsParsed(),s=TILE*camera.z;
  ctx.save();
  for(var i=0;i<a.length;i+=2){var x=a[i],y=a[i+1];if(x<f.x0||x>f.x1||y<f.y0||y>f.y1||!MUNDO.isWater(x,y))continue;
    var p=w2s(x*TILE,y*TILE),hor=world.roads.has(K(x-1,y))||world.roads.has(K(x+1,y)),ver=world.roads.has(K(x,y-1))||world.roads.has(K(x,y+1));
    ctx.fillStyle='#8a6440';ctx.fillRect(p.x,p.y+s*.14,s,s*.72);if(ver&&!hor){ctx.fillRect(p.x+s*.14,p.y,s*.72,s)}
    ctx.fillStyle='#6b4a2e';
    if(ver&&!hor){for(var k=1;k<5;k++)ctx.fillRect(p.x+s*.14,p.y+k*s/5,s*.72,Math.max(1,s*.04));ctx.fillStyle='#4f3520';ctx.fillRect(p.x+s*.1,p.y,Math.max(1,s*.06),s);ctx.fillRect(p.x+s*.84,p.y,Math.max(1,s*.06),s)}
    else{for(k=1;k<5;k++)ctx.fillRect(p.x+k*s/5,p.y+s*.14,Math.max(1,s*.04),s*.72);ctx.fillStyle='#4f3520';ctx.fillRect(p.x,p.y+s*.1,s,Math.max(1,s*.06));ctx.fillRect(p.x,p.y+s*.84,s,Math.max(1,s*.06))}
  }
  ctx.restore();
};

/* ---------- mapa e minimapa com os biomas ---------- */
if(MUNDO&&ARTE)pintarMapa=function(c,L,W2,H2,detalhe){
  var esc=Math.min(W2/(L.x1-L.x0),H2/(L.y1-L.y0)),px=function(x){return(x-L.x0)*esc},py=function(y){return(y-L.y0)*esc},cores=urbeCores();
  c.fillStyle='#23466f';c.fillRect(0,0,W2,H2);
  var step=Math.max(1,Math.ceil(2/Math.max(.001,esc))),sz=Math.max(1,step*esc+.5);
  for(var y=Math.floor(L.y0);y<L.y1;y+=step)for(var x=Math.floor(L.x0);x<L.x1;x+=step){
    var e=MUNDO.elevation(x,y),e2=MUNDO.elevation(x+step,y-step),sh=Math.max(-.25,Math.min(.25,(e-e2)*6/step));
    c.fillStyle=cores[MUNDO.biome(x,y)];c.fillRect(px(x),py(y),sz,sz);
    if(sh){c.fillStyle=sh>0?'rgba(255,255,255,'+sh+')':'rgba(0,0,0,'+(-sh)+')';c.fillRect(px(x),py(y),sz,sz)}
  }
  var visM=world.regions.filter(function(r){return r.w});
  if(visM.length){c.save();c.translate(-L.x0*esc,-L.y0*esc);c.scale(esc,esc);urbeDesenharBairros(c,esc,visM);c.restore()}
  c.globalAlpha=1;c.fillStyle='#e8d7b0';for(var road of world.roads){var q=road.split(',');c.fillRect(px(+q[0]),py(+q[1]),Math.max(1,esc),Math.max(1,esc))}
  for(var bi=0;bi<world.buildings.length;bi++){var b=world.buildings[bi],z=Math.max(3,Math.min(6,3*esc));c.fillStyle='#fff';c.fillRect(px(b.x)-1,py(b.y)-1,z+2,z+2);c.fillStyle=b.tipo==='nota'?'#c9553d':'#8a6440';c.fillRect(px(b.x),py(b.y),z,z)}
  var a0=s2w(0,0),a1=s2w(cv.w,cv.h);c.strokeStyle='#fff';c.lineWidth=2;c.strokeRect(px(a0.x/TILE),py(a0.y/TILE),(a1.x-a0.x)/TILE*esc,(a1.y-a0.y)/TILE*esc);
  if(detalhe&&visM.length)urbeNomesBairros(c,visM,function(x,y){return{x:px(x),y:py(y)}},esc);
  return{esc:esc,px:px,py:py};
};

/* ---------- mapa explorável ----------
   Arrastar explora, pinça/roda dá zoom, toque duplo aproxima. Um toque marca
   o ponto e mostra o bioma com “Ir até lá” (arrastar nunca teleporta sem querer).
   O terreno é amostrado direto do gerador (sem cachear chunks inteiros) e
   desenhado em duas passadas: rápida e depois nítida. */
(function urbeMapa(){
  if(!MUNDO||!ARTE||!mapao)return;
  mapao.onclick=null;mapao.classList.add('um');
  mapao.innerHTML='<div class="um-top"><strong>Mapa</strong><span class="um-sub"></span><button type="button" class="um-b" data-a="close" aria-label="Fechar mapa">✕</button></div>'+
    '<div class="um-stage"><canvas class="um-cv" aria-label="Mapa do mundo"></canvas><div class="um-pin" hidden><span class="um-pin-dot"></span><button type="button" class="um-go" data-a="go"></button></div>'+
    '<div class="um-ctl"><button type="button" class="um-b" data-a="in" aria-label="Aproximar">+</button><button type="button" class="um-b" data-a="out" aria-label="Afastar">−</button><button type="button" class="um-b" data-a="me" aria-label="Voltar para onde estou">◎</button></div></div>'+
    '<div class="um-hint">Arraste para explorar · pinça ou roda para zoom · toque num lugar para ir até lá</div>';
  var cvm=mapao.querySelector('.um-cv'),g=cvm.getContext('2d'),pin=mapao.querySelector('.um-pin'),goBtn=mapao.querySelector('.um-go');
  var st={cx:0,cy:0,tpp:1,w:1,h:1,dpr:1,base:null,job:0,pin:null};
  var PAL=null;function pal(){if(PAL)return PAL;PAL=urbeCores().map(function(h){h=h.replace('#','');return[parseInt(h.slice(0,2),16),parseInt(h.slice(2,4),16),parseInt(h.slice(4,6),16)]});return PAL}
  function size(){var r=cvm.getBoundingClientRect();st.dpr=Math.min(2,window.devicePixelRatio||1);st.w=Math.max(1,Math.round(r.width));st.h=Math.max(1,Math.round(r.height));cvm.width=Math.round(st.w*st.dpr);cvm.height=Math.round(st.h*st.dpr)}
  function toWorld(sx,sy){return{x:st.cx+(sx-st.w/2)*st.tpp,y:st.cy+(sy-st.h/2)*st.tpp}}
  function toScreen(x,y){return{x:(x-st.cx)/st.tpp+st.w/2,y:(y-st.cy)/st.tpp+st.h/2}}
  /* terreno: amostras numa grade; a imagem fica guardada com a vista em que foi feita */
  function renderTerrain(step){var job=++st.job,gw=Math.ceil(st.w/step)+1,gh=Math.ceil(st.h/step)+1,view={cx:st.cx,cy:st.cy,tpp:st.tpp,step:step};
    var bio=new Uint8Array(gw*gh),elv=new Float32Array(gw*gh),row=0,P=pal();
    var off=document.createElement('canvas');off.width=gw;off.height=gh;var oc=off.getContext('2d'),img=oc.createImageData(gw,gh);
    function slice(){if(job!==st.job)return;var t0=performance.now();
      while(row<gh&&performance.now()-t0<12){for(var i=0;i<gw;i++){var w=toWorldV(view,i*step,row*step),smp=MUNDO.sample(w.x,w.y);bio[row*gw+i]=smp.b;elv[row*gw+i]=smp.e}row++}
      if(row<gh){requestAnimationFrame(slice);return}
      var k=6/Math.max(.35,view.tpp*step);
      for(var j=0;j<gh;j++)for(i=0;i<gw;i++){var n=j*gw+i,c=P[bio[n]],e=elv[n],e2=elv[Math.max(0,j-1)*gw+Math.max(0,i-1)],sh=bio[n]<=3?-Math.max(0,Math.min(1,(MUNDO.sea-e)/.12))*.3:Math.max(-.28,Math.min(.28,(e-e2)*k)),f=1+sh,o=n*4;
        img.data[o]=c[0]*f;img.data[o+1]=c[1]*f;img.data[o+2]=c[2]*f;img.data[o+3]=255}
      oc.putImageData(img,0,0);st.base={cv:off,view:view};draw();
      if(step>2)setTimeout(function(){if(job===st.job)renderTerrain(2)},30)}
    requestAnimationFrame(slice)}
  function toWorldV(v,sx,sy){return{x:v.cx+(sx-st.w/2)*v.tpp,y:v.cy+(sy-st.h/2)*v.tpp}}
  var redrawT=0;function changed(){draw();clearTimeout(redrawT);st.job++;redrawT=setTimeout(function(){renderTerrain(5)},140)}
  function draw(){var d=st.dpr;g.setTransform(d,0,0,d,0,0);g.fillStyle='#23466f';g.fillRect(0,0,st.w,st.h);
    if(st.base){var v=st.base.view,sc=v.tpp/st.tpp,ox=(v.cx-st.cx)/st.tpp+st.w/2-st.w/2*sc,oy=(v.cy-st.cy)/st.tpp+st.h/2-st.h/2*sc;
      g.imageSmoothingEnabled=v.tpp*v.step>1.2;g.drawImage(st.base.cv,ox,oy,st.base.cv.width*v.step*sc,st.base.cv.height*v.step*sc)}
    var px=1/st.tpp;
    /* pastas: a forma real de cada bairro, com a hierarquia do contorno */
    var visR=world.regions.filter(function(r){if(!r.w)return false;var a=toScreen(r.x,r.y);return !(a.x>st.w||a.y>st.h||a.x+r.w*px<0||a.y+r.h*px<0)});
    if(visR.length){g.save();g.translate(st.w/2-st.cx*px,st.h/2-st.cy*px);g.scale(px,px);urbeDesenharBairros(g,px,visR);g.restore()}
    /* ruas */
    g.fillStyle='#e8d7b0';var rs=Math.max(1,px);for(var road of world.roads){var q=road.split(','),rp=toScreen(+q[0],+q[1]);if(rp.x<-2||rp.y<-2||rp.x>st.w||rp.y>st.h)continue;g.fillRect(rp.x,rp.y,rs,rs)}
    /* casas */
    var z=Math.max(4,Math.min(10,3*px));for(var bi=0;bi<world.buildings.length;bi++){var b=world.buildings[bi],bp=toScreen(b.x+b.w/2,b.y+b.h/2);if(bp.x<-z||bp.y<-z||bp.x>st.w+z||bp.y>st.h+z)continue;
      g.fillStyle='#fff';g.fillRect(bp.x-z/2-1,bp.y-z/2-1,z+2,z+2);g.fillStyle=b.tipo==='nota'?'#c9553d':'#8a6440';g.fillRect(bp.x-z/2,bp.y-z/2,z,z)}
    /* nomes dos bairros por cima das casas */
    if(visR.length)urbeNomesBairros(g,visR,toScreen,px);
    /* onde estou */
    var a0=s2w(0,0),a1=s2w(cv.w,cv.h),s0=toScreen(a0.x/TILE,a0.y/TILE),s1=toScreen(a1.x/TILE,a1.y/TILE);
    g.strokeStyle='#fff';g.lineWidth=2;g.strokeRect(s0.x,s0.y,Math.max(6,s1.x-s0.x),Math.max(6,s1.y-s0.y));
    var me=toScreen(camera.x/TILE,camera.y/TILE);g.fillStyle='#8fb3ff';g.beginPath();g.arc(me.x,me.y,5,0,7);g.fill();g.strokeStyle='#fff';g.lineWidth=2;g.stroke();
    placePin();
    var sub=mapao.querySelector('.um-sub');if(sub)sub.textContent=Math.round(st.w*st.tpp)+' × '+Math.round(st.h*st.tpp)+' tiles'}
  function placePin(){if(!st.pin){pin.hidden=true;return}var p=toScreen(st.pin.x,st.pin.y);pin.hidden=false;pin.style.left=p.x+'px';pin.style.top=p.y+'px'}
  function setPin(sx,sy){var w=toWorld(sx,sy),inf=MUNDO.info(Math.floor(w.x),Math.floor(w.y));st.pin={x:w.x,y:w.y};goBtn.textContent=inf.nome+' · Ir até lá';placePin()}
  function zoomAt(f,sx,sy){var before=toWorld(sx,sy);st.tpp=Math.max(.06,Math.min(18,st.tpp*f));var after=toWorld(sx,sy);st.cx+=before.x-after.x;st.cy+=before.y-after.y;changed()}
  function travel(){if(!st.pin)return;camera.x=st.pin.x*TILE;camera.y=st.pin.y*TILE;close();counts();try{v23Atualizar()}catch(_){}}
  function close(){mapao.classList.remove('open');st.job++;st.pin=null}
  function open(){mapao.classList.add('open');st.pin=null;requestAnimationFrame(function(){size();st.cx=camera.x/TILE;st.cy=camera.y/TILE;st.tpp=Math.max(.3,360/Math.max(st.w,st.h));st.base=null;draw();renderTerrain(6)})}
  /* gestos */
  var ptrs=new Map(),gest=null,lastTap=null;
  cvm.addEventListener('pointerdown',function(e){cvm.setPointerCapture(e.pointerId);ptrs.set(e.pointerId,{x:e.offsetX,y:e.offsetY});
    if(ptrs.size===1)gest={mode:'pan',x:e.offsetX,y:e.offsetY,cx:st.cx,cy:st.cy,t:performance.now(),moved:0};
    else if(ptrs.size===2){var v=[...ptrs.values()],d=Math.hypot(v[0].x-v[1].x,v[0].y-v[1].y);gest={mode:'pinch',d:d,tpp:st.tpp,mx:(v[0].x+v[1].x)/2,my:(v[0].y+v[1].y)/2,anchor:toWorld((v[0].x+v[1].x)/2,(v[0].y+v[1].y)/2),moved:99}}});
  cvm.addEventListener('pointermove',function(e){if(!ptrs.has(e.pointerId)||!gest)return;ptrs.set(e.pointerId,{x:e.offsetX,y:e.offsetY});
    if(gest.mode==='pan'&&ptrs.size===1){var dx=e.offsetX-gest.x,dy=e.offsetY-gest.y;gest.moved=Math.max(gest.moved,Math.hypot(dx,dy));if(gest.moved<6)return;st.cx=gest.cx-dx*st.tpp;st.cy=gest.cy-dy*st.tpp;changed()}
    else if(gest.mode==='pinch'&&ptrs.size===2){var v=[...ptrs.values()],d=Math.max(10,Math.hypot(v[0].x-v[1].x,v[0].y-v[1].y)),mx=(v[0].x+v[1].x)/2,my=(v[0].y+v[1].y)/2;
      st.tpp=Math.max(.06,Math.min(18,gest.tpp*gest.d/d));st.cx=gest.anchor.x-(mx-st.w/2)*st.tpp;st.cy=gest.anchor.y-(my-st.h/2)*st.tpp;changed()}});
  function up(e){if(!ptrs.has(e.pointerId))return;ptrs.delete(e.pointerId);if(!gest)return;
    if(gest.mode==='pan'&&e.type==='pointerup'&&gest.moved<6&&performance.now()-gest.t<500){var now=performance.now();
      if(lastTap&&now-lastTap.t<320&&Math.hypot(lastTap.x-gest.x,lastTap.y-gest.y)<30){lastTap=null;zoomAt(.5,gest.x,gest.y)}else{lastTap={t:now,x:gest.x,y:gest.y};setPin(gest.x,gest.y)}}
    if(!ptrs.size)gest=null;else if(ptrs.size===1){var v=[...ptrs.values()][0];gest={mode:'pan',x:v.x,y:v.y,cx:st.cx,cy:st.cy,t:0,moved:99}}}
  cvm.addEventListener('pointerup',up);cvm.addEventListener('pointercancel',up);
  cvm.addEventListener('wheel',function(e){e.preventDefault();zoomAt(Math.exp(e.deltaY*.0015),e.offsetX,e.offsetY)},{passive:false});
  mapao.addEventListener('click',function(e){var b=e.target.closest('[data-a]');if(!b)return;var a=b.dataset.a;
    if(a==='close')close();else if(a==='go')travel();else if(a==='in')zoomAt(.6,st.w/2,st.h/2);else if(a==='out')zoomAt(1/.6,st.w/2,st.h/2);
    else if(a==='me'){st.cx=camera.x/TILE;st.cy=camera.y/TILE;changed()}});
  window.addEventListener('resize',function(){if(mapao.classList.contains('open')){size();changed()}});
  abrirMapao=function(){open()};pintarMapao=function(){if(mapao.classList.contains('open'))changed()};
  var d0=document.getElementById('mini');if(d0)d0.onclick=function(){open()};
})();

/* ---------- explorar e construir onde se está olhando ---------- */
centroBuscaRaiz=function(){return{x:Math.floor(camera.x/TILE)-1,y:Math.floor(camera.y/TILE)-1}};
var urbeBiomaAtual=null,urbeBiomaT=0;
function urbeAtualizarBioma(){
  var agora=performance.now();if(agora-urbeBiomaT<250||!MUNDO)return;urbeBiomaT=agora;
  var inf=MUNDO.info(Math.floor(camera.x/TILE),Math.floor(camera.y/TILE));if(inf===urbeBiomaAtual)return;urbeBiomaAtual=inf;
  var el=document.getElementById('urbeBiome');if(!el){var brand=document.querySelector('#v23Top .v23Brand');if(!brand)return;el=document.createElement('span');el.id='urbeBiome';brand.appendChild(el)}
  el.textContent=inf.nome;el.title=inf.build?'Dá para construir aqui':'Não dá para construir aqui';el.classList.toggle('bloqueado',!inf.build);
}
var urbeStatusBase=v21PlacementStatus;
v21PlacementStatus=function(){
  urbeStatusBase();var c=v21PlacementCandidate,txt=document.getElementById('v21PlacementText');if(!c||!txt||!v21Placement)return;
  var ok=!document.getElementById('v21PlacementOk').disabled,motivo=urbeMotivoTerreno(c.x,c.y,3,3);
  txt.textContent=ok?('Construir aqui · '+(MUNDO?MUNDO.info(c.x+1,c.y+1).nome:'')):(motivo?'Não dá: '+motivo:'Não dá: lote ocupado ou sem acesso para rua');
};

/* Abrir uma nota e sair dela não pode reescrever o arquivo. O modo Visual
   serializa o HTML de volta para Markdown; comparamos com a serialização do
   conteúdo recém-renderizado e só gravamos quando o usuário mudou algo. */
var urbeVisualBase=null,urbeRenderBase=renderCurrentPreview,urbeSyncBase=syncVisualToMarkdown;
renderCurrentPreview=function(){var r=urbeRenderBase.apply(this,arguments);try{urbeVisualBase=editorViewMode==='preview'?markdownFromVisual(renderedPreview):null}catch(_){urbeVisualBase=null}return r};
syncVisualToMarkdown=function(){
  if(visualSyncLock||editorViewMode!=='preview'||!currentFile)return;
  var next=null;try{next=markdownFromVisual(renderedPreview)}catch(_){}
  if(urbeVisualBase!=null&&next===urbeVisualBase)return;
  var r=urbeSyncBase.apply(this,arguments);urbeVisualBase=next;return r;
};

/* ============================================================
   v0.42 — Assistente agêntico (src/ai/*)
   O chat antigo dá lugar a agentes que usam ferramentas no vault,
   com qualquer provedor de modelo. Aqui só ligamos a interface ao app.
   ============================================================ */
(function urbeAssistente(){
  if(!window.UrbeAgentUI)return;
  var core=window.UrbeCore,docsA=core&&core.service('documents');
  UrbeAgentUI.configure({
    vaultName:function(){return Disco.cidade||'Urbe'},
    editor:function(){
      var ps=core.service('pages.studio'),pg=ps&&ps.current&&ps.current();if(pg)return{id:pg.id,path:pg.path};
      if(!v23Aberto(editorFull)||!currentFile)return null;
      var d=docsA&&currentFile.documentId&&docsA.get(currentFile.documentId);
      return{id:d?d.id:null,path:d?d.path:nomeCompletoNota(currentFile)};
    },
    openNote:function(id){core.commands.execute('document.open',{id:id,source:'ai'})}
  });
  /* o painel é todo do Assistente (src/ai): os assistentes antigos (v0.20/v0.21) saíram na 1.0 */
  if(!aiPanel.classList.contains('ag-host'))UrbeAgentUI.mount(aiPanel);
  document.getElementById('openAI').onclick=function(){aiPanel.classList.toggle('v23SobreEditor',v23Aberto(editorFull));aiPanel.classList.add('open');UrbeAgentUI.opened()};
  v21OpenGlobalSettings=function(){aiPanel.classList.toggle('v23SobreEditor',v23Aberto(editorFull));aiPanel.classList.add('open');UrbeAgentUI.showSettings()};
})();

/* ============================================================
   v1.0 — Personalização do mundo (serviço 'world.custom')
   Opções da cidade, luz do dia, paleta e texturas do chão, desenho
   próprio das construções e camadas que plugins desenham no mapa.
   ============================================================ */
var urbeCamadas=[],urbeSpritesProprios={};
function urbeAmbienteAgora(){var a=urbeOpcoes.ambiente;if(a!=='auto')return a;var h=new Date().getHours();return h>=6&&h<17?'dia':(h>=17&&h<19?'entardecer':'noite')}
function urbeVista(){
  var f=faixaVisivel();
  return{ctx:ctx,tile:TILE*camera.z,zoom:camera.z,centro:{x:camera.x/TILE,y:camera.y/TILE},visivel:{x0:f.x0,y0:f.y0,x1:f.x1,y1:f.y1},largura:cv.w,altura:cv.h,
    paraTela:function(x,y){return w2s(x*TILE,y*TILE)},
    casas:function(){return world.buildings.filter(function(b){return b.x<=f.x1&&b.x+b.w>=f.x0&&b.y<=f.y1&&b.y+b.h>=f.y0}).map(function(b){return{nome:urbeNomeCasa(b),tipo:b.tipo,x:b.x,y:b.y,w:b.w,h:b.h,id:b.documentId||b.id}})},
    ambiente:urbeAmbienteAgora()};
}
function urbeJanelasAcesas(k){
  ctx.save();ctx.globalCompositeOperation='lighter';var f=faixaVisivel(),z=camera.z;
  for(var j=0;j<world.buildings.length;j++){var b=world.buildings[j];if(b.x>f.x1||b.x+b.w<f.x0||b.y>f.y1||b.y+b.h<f.y0)continue;
    /* cada casa acende numa hora um pouco diferente e às vezes uma janela tremula */
    var sem=(b.x*73856093^b.y*19349663)>>>0,liga=((sem%100)/100)*.35;if(k<liga)continue;var kk=Math.min(1,(k-liga)/.3)*(.9+.1*Math.sin(performance.now()/700+sem));
    var p=w2s((b.x+b.w/2)*TILE,(b.y+b.h*.7)*TILE),r=TILE*z*1.7,g=ctx.createRadialGradient(p.x,p.y,0,p.x,p.y,r);
    g.addColorStop(0,'rgba(255,186,92,'+(.42*kk).toFixed(3)+')');g.addColorStop(1,'rgba(255,186,92,0)');ctx.fillStyle=g;ctx.fillRect(p.x-r,p.y-r,r*2,r*2)}
  ctx.restore();
}
function urbeAntesDosRotulos(){
  if(urbeCamadas.length){var vista=urbeVista();
    for(var i=urbeCamadas.length-1;i>=0;i--){var c=urbeCamadas[i];ctx.save();
      try{c.fn(ctx,vista)}catch(e){c.erros=(c.erros||0)+1;console.warn('camada '+c.id,e);if(c.erros>=3){urbeCamadas.splice(i,1);if(c.onError)c.onError(e)}}
      finally{ctx.restore()}}}
  if(urbeVida&&urbeVida.ceu){try{urbeVida.ceu()}catch(e){console.warn('vida (céu)',e)}}
  /* luz: a vida do mundo dá a cor contínua do dia (aurora, sol dourado, crepúsculo, noite) */
  var L=urbeVida&&urbeVida.luz?urbeVida.luz():null;
  if(L){var c=L.cor;if(c[0]+c[1]+c[2]<762){ctx.save();ctx.globalCompositeOperation='multiply';ctx.fillStyle='rgb('+c[0]+','+c[1]+','+c[2]+')';ctx.fillRect(0,0,cv.w,cv.h);ctx.restore()}
    if(L.escuro>.25&&camera.z>=.3)urbeJanelasAcesas(Math.min(1,(L.escuro-.25)/.6));
    if(urbeVida.brilho){try{urbeVida.brilho()}catch(e){console.warn('vida (brilho)',e)}}
    return}
  var amb=urbeAmbienteAgora();if(amb!=='entardecer'&&amb!=='noite')return;
  ctx.save();ctx.globalCompositeOperation='multiply';ctx.fillStyle=amb==='noite'?'rgb(86,102,168)':'rgb(255,200,158)';ctx.fillRect(0,0,cv.w,cv.h);
  if(amb==='noite'&&camera.z>=.3){ /* janelas acesas */
    ctx.globalCompositeOperation='lighter';var f=faixaVisivel(),z=camera.z;
    for(var j=0;j<world.buildings.length;j++){var b=world.buildings[j];if(b.x>f.x1||b.x+b.w<f.x0||b.y>f.y1||b.y+b.h<f.y0)continue;
      var p=w2s((b.x+b.w/2)*TILE,(b.y+b.h*.7)*TILE),r=TILE*z*1.7,g=ctx.createRadialGradient(p.x,p.y,0,p.x,p.y,r);
      g.addColorStop(0,'rgba(255,186,92,.42)');g.addColorStop(1,'rgba(255,186,92,0)');ctx.fillStyle=g;ctx.fillRect(p.x-r,p.y-r,r*2,r*2)}}
  ctx.restore();
}
(function urbeMundoPersonalizavel(){
  var core=window.UrbeCore;if(!core||core.service('world.custom'))return;
  var sch=core.service('scheduler'),ultimaHora=-1;
  if(sch)sch.add('world.ambiente',function(){if(urbeOpcoes.ambiente!=='auto')return;var h=new Date().getHours();if(h!==ultimaHora){ultimaHora=h;pedirDesenho()}},{interval:60000,whenVisible:true});
  core.provide('world.custom',{
    options:function(o){
      if(o&&typeof o==='object'){Object.keys(urbeOpcoes).forEach(function(k){if(o[k]!==undefined)urbeOpcoes[k]=o[k]});
        v25Ligado=urbeOpcoes.moradores!==false;if(!v25Ligado)v25Povo=[];pedirDesenho()}
      return Object.assign({},urbeOpcoes);
    },
    /* paleta {bioma:[4 cores]} e texturas {bioma:[RGBA 16×16, até 4 variações]} */
    terrain:function(cfg){
      if(!ARTE||!ARTE.configure)return false;cfg=cfg||{};ARTE.configure(cfg);
      if(urbeWorker)urbeWorker.postMessage({type:'config',cfg:cfg});
      urbeGeracaoChao++;urbeChunks.clear();urbePedidos.clear();urbeCorBioma=null;pedirDesenho();return true;
    },
    /* imagens próprias por tipo de construção (house, hall, workshop, dyer, tower, market, store) */
    sprites:function(map){urbeSpritesProprios={};Object.keys(map||{}).forEach(function(k){if(map[k])urbeSpritesProprios[k]=map[k]});pedirDesenho()},
    layer:function(id,fn,onError){var c={id:String(id||'camada'),fn:fn,onError:onError};urbeCamadas.push(c);pedirDesenho();
      return function(){var i=urbeCamadas.indexOf(c);if(i>=0)urbeCamadas.splice(i,1);pedirDesenho()}},
    view:urbeVista,
    redraw:function(){pedirDesenho()},
    goTo:function(x,y,z){camera.x=(x+.5)*TILE;camera.y=(y+.5)*TILE;if(z)camera.z=Math.max(.1,Math.min(3,z));counts();pedirDesenho()},
    biomes:function(){return ARTE?ARTE.IDS.slice():[]},
    buildingKinds:function(){return['house','hall','workshop','dyer','tower','market','store']}
  });
})();

/* ============================================================
   v1.2 — bairros organizados
   Cada pasta é um bairro compacto e arredondado. Subpastas ficam DENTRO do
   bairro pai, com 1 tile de margem até a borda dele; bairros irmãos ficam a
   1 tile um do outro e os bairros da raiz a 2 tiles. Assim nenhum contorno
   encosta em outro. As casas se agrupam perto do centro do bairro, num
   arranjo orgânico (sem grade), em vez de cair espalhadas.
   ============================================================ */
/* versão do gerador do mundo: uma cidade salva com outra versão foi montada noutro
   terreno (casas podem ter caído na água) e é reorganizada uma vez ao abrir */
var URBE_MUNDO='placas-1',urbeCidadeMigrada=false;
var URBE_CORES_BAIRRO=['#5bc2ff','#e38eff','#7ee3a0','#ffd567','#ff9a88','#8fa8ff','#5fd4c4','#f7a95c','#c9a0ff','#b5d96a'];
function urbeAreaBairro(q){return Math.round(26+Math.max(1,q)*36)}
function urbeRaizDe(r){var n=0;while(r&&r.parentId&&n++<64){var p=regPai(r);if(!p)break;r=p}return r}
function urbeNivel(r){return caminhoRegiao(r).length-1}
function urbeCelulas(r){if(!r.cells){r.cells=[];for(var y=r.y;y<r.y+r.h;y++)for(var x=r.x;x<r.x+r.w;x++)r.cells.push(K(x,y))}if(!r._cellSet||r._cellSet.size!==r.cells.length)r._cellSet=new Set(r.cells);return r.cells}
function urbeCentroide(r){var cs=urbeCelulas(r),sx=0,sy=0;if(!cs.length)return{x:r.x+r.w/2,y:r.y+r.h/2};for(var i=0;i<cs.length;i++){var j=cs[i].indexOf(',');sx+=+cs[i].slice(0,j);sy+=+cs[i].slice(j+1)}return{x:sx/cs.length+.5,y:sy/cs.length+.5}}
function urbeLimites(r){var cs=urbeCelulas(r);if(!cs.length){r.w=0;r.h=0;return}var x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;for(var i=0;i<cs.length;i++){var j=cs[i].indexOf(','),x=+cs[i].slice(0,j),y=+cs[i].slice(j+1);if(x<x0)x0=x;if(x>x1)x1=x;if(y<y0)y0=y;if(y>y1)y1=y}r.x=x0;r.y=y0;r.w=x1-x0+1;r.h=y1-y0+1}

/* grade local de bloqueio (1 = não pode virar parte do bairro) */
function urbeGradeBairro(parent,self,x0,y0,x1,y1){
  var W=x1-x0+1,H=y1-y0+1,bl=new Uint8Array(W*H),x,y,i;
  var g={x0:x0,y0:y0,W:W,H:H,bl:bl};
  function marca(cx,cy,raio){for(var yy=Math.max(y0,cy-raio);yy<=Math.min(y1,cy+raio);yy++)for(var xx=Math.max(x0,cx-raio);xx<=Math.min(x1,cx+raio);xx++)bl[(yy-y0)*W+(xx-x0)]=1}
  for(y=y0;y<=y1;y++)for(x=x0;x<=x1;x++)if(ehAgua(x,y))bl[(y-y0)*W+(x-x0)]=1;
  if(parent){var ps=(urbeCelulas(parent),parent._cellSet);
    for(y=y0;y<=y1;y++)for(x=x0;x<=x1;x++){i=(y-y0)*W+(x-x0);if(bl[i])continue;
      /* margem de 1 tile por dentro da borda do pai */
      for(var dy=-1;dy<=1&&!bl[i];dy++)for(var dx=-1;dx<=1;dx++)if(!ps.has(K(x+dx,y+dy))){bl[i]=1;break}}}
  var pid=parent?parent.id:null,raio=parent?1:2;
  world.regions.forEach(function(o){
    if(o===self||o===parent||(o.parentId||null)!==pid)return;
    if(o.x>x1+raio||o.y>y1+raio||o.x+o.w<x0-raio||o.y+o.h<y0-raio)return;
    urbeCelulas(o).forEach(function(c){var j=c.indexOf(',');marca(+c.slice(0,j),+c.slice(j+1),raio)});
  });
  /* casas soltas do mesmo nível (do pai, ou da raiz) são obstáculo: o bairro novo as contorna */
  world.buildings.forEach(function(b){if((b.regionId||null)!==pid)return;for(var yy=b.y-1;yy<b.y+b.h+1;yy++)for(var xx=b.x-1;xx<b.x+b.w+1;xx++)if(xx>=x0&&yy>=y0&&xx<=x1&&yy<=y1)bl[(yy-y0)*W+(xx-x0)]=1});
  return g;
}
/* distância até o obstáculo mais próximo (8 vizinhos) — o "miolo" livre */
function urbeFolga(g){
  var W=g.W,H=g.H,d=new Int16Array(W*H).fill(32767),q=new Int32Array(W*H),h=0,t=0,i;
  for(i=0;i<W*H;i++){var x=i%W,y=(i/W)|0;if(g.bl[i]){d[i]=0;q[t++]=i}else if(x===0||y===0||x===W-1||y===H-1){d[i]=1;q[t++]=i}}
  while(h<t){i=q[h++];var cx=i%W,cy=(i/W)|0;for(var dy=-1;dy<=1;dy++)for(var dx=-1;dx<=1;dx++){var xx=cx+dx,yy=cy+dy;if(xx<0||yy<0||xx>=W||yy>=H)continue;var j=yy*W+xx;if(d[j]>d[i]+1){d[j]=d[i]+1;q[t++]=j}}}
  return d;
}
/* ponto de partida: o lugar mais perto do centro que tenha folga suficiente */
function urbeMelhorPonto(g,cx,cy,raio){
  var d=urbeFolga(g),best=-1,bs=-1e18;
  for(var i=0;i<d.length;i++){if(g.bl[i])continue;var x=g.x0+i%g.W+.5,y=g.y0+((i/g.W)|0)+.5,s=Math.min(d[i],raio)*1000-Math.hypot(x-cx,y-cy);if(s>bs){bs=s;best=i}}
  return best<0?null:{x:g.x0+best%g.W,y:g.y0+((best/g.W)|0),folga:d[best]};
}
/* crescimento por prioridade (distância ao centro com um leve ondulado): sai um
   bairro arredondado, e não o losango da busca em largura */
function urbeCrescer(g,cx,cy,alvo,sem,ja){
  var W=g.W,H=g.H,N=W*H,rand=rng(sem),a1=rand()*6.3,a2=rand()*6.3,a3=rand()*6.3;
  var dentro=new Uint8Array(N),visto=new Uint8Array(N),hk=[],hv=[],total=0,novos=0;
  function custo(i){var x=g.x0+i%W+.5-cx,y=g.y0+((i/W)|0)+.5-cy,t=Math.atan2(y,x);return Math.hypot(x,y)*(1+.14*Math.sin(2*t+a1)+.09*Math.sin(3*t+a2)+.05*Math.sin(5*t+a3))}
  function push(i){var c=custo(i),n=hk.length;hk.push(c);hv.push(i);while(n>0){var p=(n-1)>>1;if(hk[p]<=hk[n])break;var tk=hk[p];hk[p]=hk[n];hk[n]=tk;var tv=hv[p];hv[p]=hv[n];hv[n]=tv;n=p}}
  function pop(){var v=hv[0],lk=hk.pop(),lv=hv.pop();if(hk.length){hk[0]=lk;hv[0]=lv;var n=0,L=hk.length;for(;;){var a=2*n+1,b=a+1,m=n;if(a<L&&hk[a]<hk[m])m=a;if(b<L&&hk[b]<hk[m])m=b;if(m===n)break;var tk=hk[m];hk[m]=hk[n];hk[n]=tk;var tv=hv[m];hv[m]=hv[n];hv[n]=tv;n=m}}return v}
  function viz(i,f){var x=i%W,y=(i/W)|0;if(x>0)f(i-1);if(x<W-1)f(i+1);if(y>0)f(i-W);if(y<H-1)f(i+W)}
  function em(x,y){return x>=g.x0&&y>=g.y0&&x<g.x0+W&&y<g.y0+H}
  if(ja){ja.forEach(function(k){var j=k.indexOf(','),x=+k.slice(0,j),y=+k.slice(j+1);if(!em(x,y))return;var i=(y-g.y0)*W+(x-g.x0);dentro[i]=2;visto[i]=1;total++});
    for(var i0=0;i0<N;i0++)if(dentro[i0]===2)viz(i0,function(j){if(!visto[j])push(j)});
    total=ja.size}
  else{var s=(cy|0)-g.y0,s2=(cx|0)-g.x0;if(s<0||s2<0||s>=H||s2>=W)return[];push(s*W+s2)}
  while(hk.length&&total<alvo){var i=pop();if(visto[i])continue;visto[i]=1;if(g.bl[i])continue;dentro[i]=1;total++;novos++;viz(i,function(j){if(!visto[j])push(j)})}
  /* acabamento: fecha buracos e tira pontas soltas */
  for(var pass=0;pass<3;pass++)for(var k=0;k<N;k++){var n=0;viz(k,function(j){if(dentro[j])n++});
    if(!dentro[k]&&!g.bl[k]&&n>=3)dentro[k]=1;else if(dentro[k]===1&&n<=1)dentro[k]=0}
  var out=[];for(var k2=0;k2<N;k2++)if(dentro[k2]===1)out.push(K(g.x0+k2%W,g.y0+((k2/W)|0)));
  return out;
}
/* centro da cidade: média ponderada dos bairros da raiz e das casas soltas */
function urbeCentroCidade(){
  var sx=0,sy=0,n=0;
  world.regions.forEach(function(r){if(r.parentId||!r.w)return;var c=urbeCentroide(r),p=Math.max(1,(r.cells||[]).length);sx+=c.x*p;sy+=c.y*p;n+=p});
  world.buildings.forEach(function(b){if(b.regionId)return;sx+=(b.x+1.5)*9;sy+=(b.y+1.5)*9;n+=9});
  return n?{x:sx/n,y:sy/n}:{x:Math.floor(camera.x/TILE),y:Math.floor(camera.y/TILE)};
}
/* ponto de partida do mundo: escolhido pelo gerador em terra boa, com rio perto */
function urbeInicioDoMundo(){return{x:Math.floor(BASE_W/2)+.5,y:Math.floor(BASE_H/2)+.5}}
/* se o centro pedido caiu no mar ou num lugar sem terra firme por perto, usa o ponto de partida */
function urbeCentroEmTerra(c){var ok=0,n=0;for(var dy=-12;dy<=12;dy+=4)for(var dx=-12;dx<=12;dx+=4){n++;if(!ehAgua(Math.round(c.x+dx),Math.round(c.y+dy)))ok++}return ok>=n*.4?c:urbeInicioDoMundo()}
function urbeMascara(sem,alvo,origem,parent){
  var raio=Math.max(3,Math.sqrt(alvo/Math.PI)*.8),p=null,g=null,cx,cy;
  if(parent){urbeCelulas(parent);var c=urbeCentroide(parent);cx=c.x;cy=c.y;
    g=urbeGradeBairro(parent,null,parent.x,parent.y,parent.x+parent.w-1,parent.y+parent.h-1);p=urbeMelhorPonto(g,cx,cy,raio)}
  else{var c2=urbeCentroEmTerra(origem||urbeCentroCidade());cx=c2.x;cy=c2.y;
    for(var m=Math.ceil(Math.sqrt(alvo))*2+14,t=0;t<4;t++,m*=2){
      g=urbeGradeBairro(null,null,Math.floor(cx-m),Math.floor(cy-m),Math.ceil(cx+m),Math.ceil(cy+m));
      p=urbeMelhorPonto(g,cx,cy,raio);if(p&&p.folga>=Math.min(raio,6))break}}
  if(!p)return null;
  var cells=urbeCrescer(g,p.x+.5,p.y+.5,alvo,sem,null);if(!cells.length)return null;
  var r={cells:cells};urbeLimites(r);return{cells:cells,x:r.x,y:r.y,w:r.w,h:r.h};
}
construirMascaraRegiao=function(sem,quantidade,origem,parent){
  var alvo=urbeAreaBairro(quantidade),res=null;
  for(var t=0;t<4;t++){
    res=urbeMascara(sem+t,alvo,t?null:origem,parent||null);
    if(res&&res.cells.length>=alvo*.85)return res;
    if(!parent)break;
    /* não coube: o pai cresce e tenta de novo */
    if(!expandirRegiao(parent,Math.round(alvo*1.3)+40)&&res)break;
  }
  return res&&res.cells.length>=12?res:null;
};
expandirRegiao=function(r,extra){
  urbeCelulas(r);var parent=r.parentId?regPai(r):null,alvo=r.cells.length+Math.max(1,Math.round(extra)),m=Math.ceil(Math.sqrt(alvo)*.7)+6;
  var g=urbeGradeBairro(parent,r,r.x-m,r.y-m,r.x+r.w-1+m,r.y+r.h-1+m),c=urbeCentroide(r);
  var novos=urbeCrescer(g,c.x,c.y,alvo,semente(r.id||r.name),r._cellSet);
  if(novos.length<extra*.5&&parent&&expandirRegiao(parent,Math.round(extra*1.4)+30)){
    g=urbeGradeBairro(parent,r,r.x-m,r.y-m,r.x+r.w-1+m,r.y+r.h-1+m);var mais=urbeCrescer(g,c.x,c.y,alvo,semente(r.id||r.name)+1,r._cellSet);if(mais.length>novos.length)novos=mais}
  if(!novos.length)return 0;
  novos.forEach(function(k){r.cells.push(k);r._cellSet.add(k)});urbeLimites(r);r._lotes=null;marcarIndice();indexar();return novos.length;
};
/* cor: bairros da raiz com a cor menos usada pelos vizinhos; subpastas no tom do bairro de cima */
function urbeMisturar(h,alvo,f){var a=parseInt(h.slice(1),16),b=parseInt(alvo.slice(1),16),o='#';for(var s=16;s>=0;s-=8){var c=Math.round(((a>>s)&255)*(1-f)+((b>>s)&255)*f);o+=(c<16?'0':'')+c.toString(16)}return o}
function urbeCorFilha(r){var raiz=urbeRaizDe(r),n=urbeNivel(r),base=/^#[0-9a-f]{6}$/i.test(raiz&&raiz.color||'')?raiz.color:'#5bc2ff';return n?urbeMisturar(base,'#ffffff',Math.min(.55,.22*n)):base}
function urbeCorRaiz(r){var c=urbeCentroide(r),uso={};URBE_CORES_BAIRRO.forEach(function(k){uso[k]=0});
  world.regions.forEach(function(o){if(o===r||o.parentId||!o.w||!(o.color in uso))return;var d=Math.hypot(urbeCentroide(o).x-c.x,urbeCentroide(o).y-c.y);uso[o.color]+=1+200/(20+d)});
  return URBE_CORES_BAIRRO.slice().sort(function(a,b){return uso[a]-uso[b]||URBE_CORES_BAIRRO.indexOf(a)-URBE_CORES_BAIRRO.indexOf(b)})[0]}
function urbeCorAutomatica(c){return colors.indexOf(c)>=0||URBE_CORES_BAIRRO.indexOf(c)>=0}
var urbeCriarRegiaoAntes=criarRegiaoOrganica;
criarRegiaoOrganica=function(nome,quantidade,sem,parentId,origem,descricao){
  var r=urbeCriarRegiaoAntes.apply(null,arguments);if(!r)return r;
  r.color=r.parentId?urbeCorFilha(r):urbeCorRaiz(r);marcarIndice();indexar();return r;
};

/* ---------- casas agrupadas, sem grade ----------
   As casas se juntam perto do centro do bairro, mas em arranjo orgânico: a ordem
   dos lotes é a distância ao centro com um ondulado e um sorteio fixo por lote
   (mesmo bairro, mesma ordem), sem alinhamento. O afastamento mínimo entre
   casas continua o da regra de lote. */
function urbeSorteioLote(x,y,s){var h=Math.imul(x^0x27d4eb2d,0x165667b1)^Math.imul(y^s,0x9e3779b1);h^=h>>>15;h=Math.imul(h,0x85ebca6b);h^=h>>>13;return(h>>>0)/4294967296}
function urbeOrdemOrganica(cx,cy,sem){var rand=rng(sem),a1=rand()*6.3,a2=rand()*6.3;
  return function(x,y){var dx=x-cx,dy=y-cy,t=Math.atan2(dy,dx);return Math.hypot(dx,dy)*(1+.18*Math.sin(2*t+a1)+.1*Math.sin(3*t+a2))+urbeSorteioLote(x,y,sem)*2.6}}
function urbeLotes(r){
  urbeCelulas(r);var key=r.cells.length+':'+r.x+':'+r.y+':'+r.w+':'+r.h;
  if(r._lotes&&r._lotes.key===key)return r._lotes.list;
  var c=urbeCentroide(r),ordem=urbeOrdemOrganica(c.x,c.y,semente('lotes:'+(r.id||r.name))),list=[];
  for(var y=r.y;y<=r.y+r.h-3;y++)for(var x=r.x;x<=r.x+r.w-3;x++){
    if(!r._cellSet.has(K(x,y))||!r._cellSet.has(K(x+2,y+2))||!r._cellSet.has(K(x+2,y))||!r._cellSet.has(K(x,y+2)))continue;
    list.push({x:x,y:y,s:ordem(x+1.5,y+1.5)});
  }
  list.sort(function(a,b){return a.s-b.s});r._lotes={key:key,list:list};return list;
}
posicaoAleatoriaNaRegiao=function(r,sem,ignorarIds){
  ignorarIds=ignorarIds||new Set();if(idxSujo)indexar();var L=urbeLotes(r);
  for(var i=0;i<L.length;i++)if(casaCabeNaRegiao(r,L[i].x,L[i].y,ignorarIds))return{x:L[i].x,y:L[i].y};
  return null;
};
/* notas soltas (raiz): agrupadas em volta do centro, do mesmo jeito orgânico, a 1 tile de qualquer bairro */
var urbeVagaAntes=vagaAleatoria;
vagaAleatoria=function(sem,w,h,area){
  w=w||3;h=h||3;var c=area||centroBuscaRaiz(),cx=Math.round(c.x),cy=Math.round(c.y),ordem=urbeOrdemOrganica(cx,cy,semente('raiz'));if(idxSujo)indexar();
  for(var R=12,feito=-1;R<=192;feito=R,R*=2){var cand=[];
    for(var dy=-R;dy<=R;dy++)for(var dx=-R;dx<=R;dx++){var d=Math.hypot(dx,dy);if(d>R||d<=feito)continue;cand.push({x:cx+dx,y:cy+dy,s:ordem(cx+dx+1.5,cy+dy+1.5)})}
    cand.sort(function(a,b){return a.s-b.s});
    for(var j=0;j<cand.length;j++)if(loteForaDeRegioes(cand[j].x-1,cand[j].y-1,w+2,h+2)&&loteValido(cand[j].x,cand[j].y))return{x:cand[j].x,y:cand[j].y}}
  return urbeVagaAntes(sem,w,h,area);
};

/* ---------- reorganizar uma cidade que já existe ----------
   Refaz todos os bairros (maiores primeiro, cada subpasta dentro da sua) e
   reagrupa as casas perto do centro de cada bairro. Notas, pastas e ligações não mudam. */
function urbeReorganizarCidade(opts){
  opts=opts||{};if(idxSujo)indexar();
  /* na migração a cidade vai para o ponto de partida do mundo novo (a antiga pode ter ficado no mar) */
  var regs=world.regions.slice(),casas=world.buildings.slice(),itens=new Map(),filhos=new Map(),centro=opts.centro||urbeCentroEmTerra(urbeCentroCidade());
  regs.forEach(function(r){itens.set(r.id,0);filhos.set(r.id,[])});
  regs.forEach(function(r){if(r.parentId&&filhos.has(r.parentId))filhos.get(r.parentId).push(r);else r.parentId=null});
  casas.forEach(function(b){var r=b.regionId?regs.find(function(x){return x.id===b.regionId}):null;if(b.tipo==='anexo')return;while(r){itens.set(r.id,itens.get(r.id)+1);r=r.parentId?regs.find(function(x){return x.id===r.parentId}):null}});
  function peso(r){return itens.get(r.id)+filhos.get(r.id).reduce(function(s,f){return s+2+peso(f)*.25},0)}
  regs.forEach(function(r){r.cells=[];r._cellSet=new Set();r.w=0;r.h=0;r._lotes=null});
  world.buildings.length=0;marcarIndice();indexar();
  var ordem=regs.filter(function(r){return!r.parentId}).sort(function(a,b){return peso(b)-peso(a)}),falhas=0;
  function montar(r,parent,primeiro){
    var m=urbeMascara(semente('bairro:'+r.id),urbeAreaBairro(peso(r)),primeiro?centro:null,parent);
    if(!m&&parent){expandirRegiao(parent,urbeAreaBairro(peso(r))+40);m=urbeMascara(semente('bairro:'+r.id)+1,urbeAreaBairro(peso(r)),null,parent)}
    if(!m){falhas++;return}
    r.cells=m.cells;r._cellSet=new Set(m.cells);urbeLimites(r);
    if(urbeCorAutomatica(r.color))r.color=parent?urbeCorFilha(r):urbeCorRaiz(r);
    marcarIndice();indexar();
    filhos.get(r.id).slice().sort(function(a,b){return peso(b)-peso(a)}).forEach(function(f){montar(f,r,false)});
  }
  ordem.forEach(function(r,i){montar(r,null,i===0)});
  /* casas: notas primeiro (em ordem de nome, para ficar estável), depois arquivos */
  var notas=casas.filter(function(b){return b.tipo!=='anexo'}).sort(function(a,b){return String(a.name).localeCompare(String(b.name))}),anexos=casas.filter(function(b){return b.tipo==='anexo'});
  function colocar(b,pos){if(pos){b.x=pos.x;b.y=pos.y}world.buildings.push(b);indexarUm(idxB,b)}
  notas.forEach(function(b){var r=b.regionId?regs.find(function(x){return x.id===b.regionId&&x.w}):null;if(b.regionId&&!r)b.regionId=null;
    colocar(b,r?vagaNaRegiao(r,semente(b.id),new Set()):vagaAleatoria(semente(b.id),b.w,b.h,centro))});
  anexos.forEach(function(b){var dono=world.buildings.find(function(x){return x.id===b.parentNoteId}),r=b.regionId?regs.find(function(x){return x.id===b.regionId&&x.w}):null;
    colocar(b,(dono&&posicaoAdjacenteArquivo(dono,r,b.id))||(r?vagaNaRegiao(r,semente(b.id),new Set()):vagaAleatoria(semente(b.id),b.w,b.h,centro)))});
  marcarIndice();indexar();
  if(!opts.carregando){rebuildRoadNetwork();counts();buildTree();agendarSalvar();marcarSinc();try{urbeEnquadrarNotas(true)}catch(_){}}
  pedirDesenho();
  return{bairros:regs.length-falhas,casas:world.buildings.length,falhas:falhas};
}
/* confere as regras dos bairros: subpasta dentro do pai (com margem), irmãos sem
   encostar, casas inteiras dentro do próprio bairro e sem sobreposição */
function urbeValidarBairros(){
  if(idxSujo)indexar();var erros=[],viz8=[[1,0],[-1,0],[0,1],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1]];
  world.regions.forEach(function(r){if(!r.w)return;urbeCelulas(r);var pai=r.parentId?regPai(r):null;
    if(pai){urbeCelulas(pai);var fora=0;r.cells.forEach(function(k){var j=k.indexOf(','),x=+k.slice(0,j),y=+k.slice(j+1);if(!pai._cellSet.has(k)||viz8.some(function(d){return!pai._cellSet.has(K(x+d[0],y+d[1]))}))fora++});if(fora)erros.push(r.name+': '+fora+' tile(s) fora da margem de '+pai.name)}
    world.regions.forEach(function(o){if(o===r||!o.w||(o.parentId||null)!==(r.parentId||null)||o.id<r.id)return;urbeCelulas(o);
      var toca=r.cells.some(function(k){var j=k.indexOf(','),x=+k.slice(0,j),y=+k.slice(j+1);return o._cellSet.has(k)||viz8.some(function(d){return o._cellSet.has(K(x+d[0],y+d[1]))})});if(toca)erros.push(r.name+' encosta em '+o.name)})});
  world.buildings.forEach(function(b,i){var r=b.regionId?world.regions.find(function(x){return x.id===b.regionId}):null;
    for(var y=b.y;y<b.y+b.h;y++)for(var x=b.x;x<b.x+b.w;x++){var a=regAt({x:x,y:y});if((a?a.id:null)!==(r?r.id:null)){erros.push('casa '+b.name+' fora do bairro '+(r?r.name:'raiz'));y=1e9;break}}
    for(var j=i+1;j<world.buildings.length;j++){var o=world.buildings[j];if(b.x<o.x+o.w&&b.x+b.w>o.x&&b.y<o.y+o.h&&b.y+b.h>o.y)erros.push('casas sobrepostas: '+b.name+' e '+o.name)}});
  return{bairros:world.regions.length,casas:world.buildings.length,erros:erros};
}
(function(){var core=window.UrbeCore;if(!core)return;
  core.provide('city.layout',{reorganize:urbeReorganizarCidade,validate:urbeValidarBairros});
  core.commands.register('city.reorganize',{title:'Organizar os bairros',category:'Cidade',execute:async function(){
    if(!world.regions.length&&!world.buildings.length){toast('A cidade ainda está vazia.');return}
    var ok=await UD.confirm({title:'Organizar os bairros?',message:'Os bairros são redesenhados (cada subpasta dentro da sua pasta) e as casas se agrupam perto do centro de cada bairro. Notas, pastas e ligações não mudam; só o lugar de cada coisa no mapa.',confirm:'Organizar'});
    if(!ok)return;var r=urbeReorganizarCidade();toast(r.falhas?'Cidade organizada ('+r.falhas+' bairro(s) sem espaço).':'Cidade organizada.');return r}});
})();

/* ---------- desenho: formas guardadas em Path2D (em tiles) ----------
   Um preenchimento por bairro (sem emendas) e o contorno em traços contínuos.
   Hierarquia: bairro da raiz com borda grossa e halo escuro; subpastas mais
   finas; do 3º nível para baixo, tracejadas. */
function urbeForma(r){
  urbeCelulas(r);var g=r._forma;if(g&&g.cells===r.cells&&g.n===r.cells.length)return g;
  var set=r._cellSet,linhas=new Map(),x0=1e9,y0=1e9,x1=-1e9,y1=-1e9;
  r.cells.forEach(function(k){var j=k.indexOf(','),x=+k.slice(0,j),y=+k.slice(j+1);var a=linhas.get(y);if(!a)linhas.set(y,a=[]);a.push(x);if(x<x0)x0=x;if(x>x1)x1=x;if(y<y0)y0=y;if(y>y1)y1=y});
  var topo=new Map();
  linhas.forEach(function(xs,y){xs.forEach(function(x){if(!topo.has(x)||topo.get(x)>y)topo.set(x,y)})});
  /* contorno: arestas da borda orientadas (bairro à direita), ligadas em laços,
     sem pontos colineares e arredondadas (Chaikin) — sai uma curva, não degraus */
  var sai=new Map(),n=0;
  function aresta(ax,ay,bx,by){var k=ax+','+ay,l=sai.get(k);if(!l)sai.set(k,l=[]);l.push([bx,by]);n++}
  r.cells.forEach(function(k){var j=k.indexOf(','),x=+k.slice(0,j),y=+k.slice(j+1);
    if(!set.has(K(x,y-1)))aresta(x,y,x+1,y);if(!set.has(K(x+1,y)))aresta(x+1,y,x+1,y+1);
    if(!set.has(K(x,y+1)))aresta(x+1,y+1,x,y+1);if(!set.has(K(x-1,y)))aresta(x,y+1,x,y)});
  var lacos=[];
  sai.forEach(function(l,k){while(l.length){var j=k.indexOf(','),sx=+k.slice(0,j),sy=+k.slice(j+1),px=sx,py=sy,dx=0,dy=0,pts=[[sx,sy]],guard=0;
    for(;;){var lst=sai.get(px+','+py);if(!lst||!lst.length)break;var idx=0;
      /* num ponto de encontro de dois cantos, vira para a direita (mantém os laços separados) */
      if(lst.length>1){for(var q=0;q<lst.length;q++){var ex=lst[q][0]-px,ey=lst[q][1]-py;if(ex===-dy&&ey===dx){idx=q;break}}}
      var nx=lst[idx][0],ny=lst[idx][1];lst.splice(idx,1);dx=nx-px;dy=ny-py;px=nx;py=ny;
      if(px===sx&&py===sy)break;pts.push([px,py]);if(++guard>1e6)break}
    lacos.push(pts)}});
  function simplificar(p){var o=[];for(var i=0;i<p.length;i++){var a=p[(i+p.length-1)%p.length],b=p[i],c=p[(i+1)%p.length];if((b[0]-a[0])*(c[1]-b[1])-(b[1]-a[1])*(c[0]-b[0])!==0)o.push(b)}return o.length>=3?o:p}
  function chaikin(p){var o=[];for(var i=0;i<p.length;i++){var a=p[i],b=p[(i+1)%p.length];o.push([a[0]*.75+b[0]*.25,a[1]*.75+b[1]*.25],[a[0]*.25+b[0]*.75,a[1]*.25+b[1]*.75])}return o}
  var fill=new Path2D();
  lacos.forEach(function(p){p=simplificar(p);for(var it=0;it<3;it++)p=chaikin(p);fill.moveTo(p[0][0],p[0][1]);for(var i=1;i<p.length;i++)fill.lineTo(p[i][0],p[i][1]);fill.closePath()});
  var edge=fill;
  /* rótulo: no alto do bairro, sobre a coluna do centro */
  var c=urbeCentroide(r),cx=Math.floor(c.x),best=null;
  for(var dx=0;dx<=Math.max(2,r.w/4)&&best===null;dx++)[cx-dx,cx+dx].forEach(function(x){if(best===null&&topo.has(x))best={x:x+.5,y:topo.get(x)}});
  r._forma=g={cells:r.cells,n:r.cells.length,fill:fill,edge:edge,rot:best||{x:c.x,y:r.y},c:c,x0:x0,y0:y0,x1:x1+1,y1:y1+1};
  return g;
}
function urbeCorRGBA(h,a){var n=parseInt(String(h||'#5bc2ff').slice(1),16);return'rgba('+(n>>16&255)+','+(n>>8&255)+','+(n&255)+','+a+')'}
/* desenha todos os bairros num contexto já transformado para tiles; px = pixels por tile */
function urbeDesenharBairros(c,px,vis){
  var ordem=vis.map(function(r){return{r:r,n:urbeNivel(r)}}).sort(function(a,b){return a.n-b.n});
  ordem.forEach(function(o){var f=urbeForma(o.r);c.fillStyle=urbeCorRGBA(o.r.color,o.n?.10:.13);c.fill(f.fill)});
  ordem.forEach(function(o){var f=urbeForma(o.r),n=o.n,w=n===0?3:n===1?2:1.5;
    c.lineCap='round';c.lineJoin='round';
    if(n===0){c.strokeStyle='rgba(8,10,14,.55)';c.lineWidth=(w+2.5)/px;c.setLineDash([]);c.stroke(f.edge)}
    c.strokeStyle=urbeCorRGBA(o.r.color,n?.85:.95);c.lineWidth=w/px;c.setLineDash(n>=2?[5/px,4/px]:[]);c.stroke(f.edge)});
  c.setLineDash([]);
}
drawRegions=function(){
  var f=faixaVisivel(),vis=world.regions.filter(function(r){return r.w&&r.x<f.x1+1&&r.x+r.w>f.x0-1&&r.y<f.y1+1&&r.y+r.h>f.y0-1}),s=TILE*camera.z,o=w2s(0,0);
  if(!vis.length)return;
  ctx.save();ctx.translate(o.x,o.y);ctx.scale(s,s);urbeDesenharBairros(ctx,s,vis);ctx.restore();
};
/* etiqueta de bairro: quadrado para bairro da raiz, ponto para subpasta */
function urbeCaixaBairro(c,r,n,x,y){
  var nome=r.name.length>26?r.name.slice(0,24)+'…':r.name,tam=n?11:13,fonte=(n?600:700)+' '+tam+'px '+URBE_FONTE;
  c.save();c.font=fonte;var tw=c.measureText(nome).width;c.restore();var alt=n?22:26,w=tw+(n?30:36);
  return{x:x-w/2,y:y-alt/2,w:w,h:alt,r:r,n:n,nome:nome,fonte:fonte,cy:y};
}
function urbePilulaBairro(c,box){
  var r=box.r,n=box.n,y=box.cy;c.save();
  urbeRetanguloArredondado(c,box.x,box.y,box.w,box.h,box.h/2);c.fillStyle=n?'rgba(20,22,27,.86)':'rgba(12,13,16,.92)';c.fill();
  c.strokeStyle=urbeCorRGBA(r.color,n?.7:.95);c.lineWidth=n?1.5:2;c.stroke();
  c.fillStyle=r.color;c.beginPath();if(n)c.arc(box.x+12,y,3.5,0,7);else c.rect(box.x+10,y-4.5,9,9);c.fill();
  c.font=box.fonte;c.textBaseline='middle';c.textAlign='left';c.fillStyle=n?'#dfe3ea':'#fff';c.fillText(box.nome,box.x+(n?21:25),y+.5);c.restore();
}
/* escolhe os nomes que cabem: os de cima primeiro, subpastas só com zoom suficiente, nada sobreposto */
function urbeLayoutNomes(c,vis,toS,px){
  var usados=[];
  vis.map(function(r){return{r:r,n:urbeNivel(r)}}).filter(function(o){return o.n===0||px>=(o.n===1?7:12)})
    .sort(function(a,b){return a.n-b.n||b.r.cells.length-a.r.cells.length}).forEach(function(o){
      var g=urbeForma(o.r);if((g.x1-g.x0)*px<(o.n?60:34))return;var p=toS(g.rot.x,g.rot.y),box=urbeCaixaBairro(c,o.r,o.n,p.x,p.y);
      var bate=function(b){return usados.some(function(u){return b.x<u.x+u.w+4&&b.x+b.w+4>u.x&&b.y<u.y+u.h+3&&b.y+b.h+3>u.y})};
      /* colidiu com o nome do bairro de cima: tenta logo abaixo e dos lados, sem sair da área do bairro */
      if(bate(box)){var a0=toS(g.x0,g.y0),a1=toS(g.x1,g.y1),passo=box.h+6,achou=null;
        [[0,1],[0,2],[-.5,1],[.5,1],[-.5,2],[.5,2],[0,3]].some(function(d){var bx=urbeCaixaBairro(c,o.r,o.n,p.x+d[0]*box.w,p.y+d[1]*passo);
          if(bx.x<a0.x-4||bx.x+bx.w>a1.x+4||bx.y+bx.h>a1.y)return false;if(bate(bx))return false;achou=bx;return true});
        if(!achou)return;box=achou}
      usados.push(box)});
  return usados;
}
function urbeNomesBairros(c,vis,toS,px){urbeLayoutNomes(c,vis,toS,px).forEach(function(b){urbePilulaBairro(c,b)})}
function urbeLayoutNomesBairros(){
  if(!urbeOpcoes.bairros||camera.z<.28)return[];
  var f=faixaVisivel(),vis=world.regions.filter(function(r){return r.w&&r.x<f.x1&&r.x+r.w>f.x0&&r.y<f.y1&&r.y+r.h>f.y0});
  return urbeLayoutNomes(ctx,vis,function(x,y){return w2s(x*TILE,y*TILE)},TILE*camera.z);
}
function urbeDesenharNomesBairros(boxes){boxes.forEach(function(b){urbePilulaBairro(ctx,b)})}

/* ao abrir a cidade, garante que cada casa está dentro do próprio bairro */
var urbeAbrirAntesDaRegra=abrirCidade;
abrirCidade=async function(){var r=await urbeAbrirAntesDaRegra.apply(this,arguments);try{var n=urbeCasasNosBairros();if(n)console.info('casas devolvidas aos bairros: '+n);var t=urbeTaparTodos();if(t){console.info('buracos tapados nos bairros: '+t);scheduleRoadRebuild();pedirDesenho();agendarSalvar();marcarSinc()}}catch(e){console.warn('casas nos bairros',e)}return r};

/* ---------- vida do mundo (src/world/life.js) ----------
   O aquário: clima, água, bichos, flores, luzes e eventos. Este bloco só entrega o que a vida
   precisa enxergar do mundo e abre três camadas de desenho:
   chão (antes das árvores e bichos), céu (por cima das casas) e brilho (depois do escurecer). */
var urbeVida=null;
(function(){
  var core=window.UrbeCore;if(!core)return;
  core.provide('world.life.host',{
    ctx:ctx,TILE:TILE,
    camera:function(){return camera},w2s:w2s,faixa:faixaVisivel,
    largura:function(){return cv.w},altura:function(){return cv.h},
    agua:function(x,y){return MUNDO?MUNDO.isWater(Math.floor(x),Math.floor(y)):ehAgua(Math.floor(x),Math.floor(y))},
    bioma:function(x,y){x=Math.floor(x);y=Math.floor(y);if(!MUNDO||!MUNDO.has(Math.floor(x/MUNDO.CH),Math.floor(y/MUNDO.CH)))return -1;return MUNDO.biome(x,y)},
    B:MUNDO?MUNDO.B:{},
    mundo:function(){return world},
    rua:function(x,y){return world.roads.has(K(Math.floor(x),Math.floor(y)))},
    casaEm:function(x,y){return bAt({x:Math.floor(x),y:Math.floor(y)})},
    povo:function(){return v25Povo},fauna:function(){return urbeFauna},
    opcoes:function(){return urbeOpcoes},
    animar:pedirAnimacao,redesenhar:pedirDesenho,
    visivel:function(){return v25MapaVisivel()},
    pressa:function(f){urbeVelPovo=f>0?f:1},
    nomeBairro:function(r){return r&&r.name||''},
    centroBairro:function(r){return urbeCentroide(r)},
    registrar:function(v){urbeVida=v;pedirDesenho()}
  });
})();
var urbeVidaArvores=drawTrees;
drawTrees=function(){if(urbeVida&&urbeVida.chao){try{urbeVida.chao()}catch(e){console.warn('vida (chão)',e)}}urbeVidaArvores()};

/* ---------- boot ----------
   Com uma pasta do aparelho escolhida, três casos pedem a pessoa em vez de seguir calado:
   a permissão expirou (comum no Android), a pasta foi apagada/movida, ou abrir falhou.
   Antes, o primeiro caia sem aviso no armazenamento interno (parecia que as notas
   tinham sumido) e os outros num menu sem explicação. */
async function urbePastaExiste(h){try{for await(const _ of h.values())break;return true}catch(e){return !(e&&(e.name==='NotFoundError'||e.name==='NotAllowedError'))}}
async function urbeUsarInterno(esquecer){Disco.raiz=null;Disco.modo='interno';if(esquecer){try{await DBK.del?DBK.del('pastaRaiz'):DBK.set('pastaRaiz',null)}catch(_){}}}
async function urbeResolverPasta(h,motivo){
  var nome=h&&h.name?'“'+h.name+'”':'escolhida';
  var msg=motivo==='permissao'?'O aparelho pediu de novo a permissão para o Urbe usar a pasta '+nome+'. Suas notas continuam lá.'
    :'A pasta '+nome+' não foi encontrada. Ela pode ter sido apagada, movida ou renomeada.';
  var ops=(motivo==='permissao'?[{value:'permitir',icon:'check',label:'Permitir acesso à pasta '+nome,detail:'Continua de onde parou'}]:[])
    .concat([{value:'outra',icon:'folder',label:'Escolher outra pasta',detail:motivo==='permissao'?'Outra pasta do aparelho':'Pode ser uma pasta nova e vazia: o Urbe começa do zero nela'},
      {value:'interno',icon:'storage',label:'Usar o armazenamento do aplicativo',detail:'Guarda as notas dentro do navegador, sem pasta'}]);
  var r=await UD.choose({title:motivo==='permissao'?'Acesso à pasta do Urbe':'Pasta do Urbe não encontrada',message:msg,options:ops});
  if(r==='permitir'){try{if(await h.requestPermission({mode:'readwrite'})==='granted'&&await urbePastaExiste(h)){Disco.raiz=h;Disco.modo='pasta';return true}}catch(e){console.warn('permissão',e)}
    toast('O acesso não foi liberado.');return urbeResolverPasta(h,motivo)}
  if(r==='outra'){try{var nh=await window.showDirectoryPicker({mode:'readwrite',id:'urbe-vaults',startIn:'documents'});if(nh.requestPermission&&await nh.requestPermission({mode:'readwrite'})!=='granted')return urbeResolverPasta(h,motivo);
      Disco.raiz=nh;Disco.modo='pasta';await DBK.set('pastaRaiz',nh);return true}catch(e){if(e&&e.name!=='AbortError')toast('Não consegui abrir a pasta: '+e.message);return urbeResolverPasta(h,motivo)}}
  /* armazenamento do aplicativo: se a pasta sumiu, esquece; se só faltou permissão, pergunta de novo na próxima vez */
  await urbeUsarInterno(motivo!=='permissao');return true;
}
/* App instalado (Windows/Android): a pasta do Urbe é uma pasta de verdade no aparelho
   (por padrão Documentos/Urbe). No Android 11+ pede o "acesso a todos os arquivos" para também
   enxergar o que for copiado para lá por outro app ou pelo computador. */
async function urbeNativoPreparar(){
  const N=window.UrbeNative;
  if(N.storage&&N.storage.status){
    try{
      const s=await N.storage.status();
      if(s&&s.legacy)await N.storage.requestLegacy();
      else if(s&&s.needsAllFiles&&!s.allFiles&&!(await DBK.get('nativoAcessoRecusado'))){
        const r=await UD.choose({title:'Pasta do Urbe no aparelho',message:'Suas notas ficam em Documentos/Urbe, como arquivos normais que você vê no gerenciador de arquivos e pode copiar para o computador.\n\nPara o Urbe também ler arquivos que você colocar lá por outro app (ou depois de reinstalar), o Android pede o acesso a todos os arquivos.',
          options:[{value:'sim',icon:'check',label:'Permitir acesso',detail:'Abre a tela do Android: ligue “Permitir acesso para gerenciar todos os arquivos” e volte'},{value:'nao',icon:'folder',label:'Agora não',detail:'O Urbe grava e lê normalmente o que ele mesmo criar'}]});
        if(r==='sim'){await N.storage.requestAllFiles();await new Promise(ok=>{const f=()=>{if(document.visibilityState==='visible'){document.removeEventListener('visibilitychange',f);setTimeout(ok,300)}};document.addEventListener('visibilitychange',f);setTimeout(()=>{document.removeEventListener('visibilitychange',f);ok()},120000)})}
        else await DBK.set('nativoAcessoRecusado',1);
      }
    }catch(e){console.warn('permissão de arquivos',e)}
  }
  Disco.raiz=await window.UrbeNativeFS.root();Disco.modo='pasta';_fsa.resetCache();
}
async function urbeIniciar(){
  Disco.modo="interno";Disco.raiz=null;
  if(window.UrbeNativeFS)await urbeNativoPreparar();
  else if(TEM_FSA){
    const h=await DBK.get("pastaRaiz");
    if(h&&h.queryPermission){
      const st=await h.queryPermission({mode:"readwrite"});
      if(st==="granted"&&await urbePastaExiste(h)){Disco.raiz=h;Disco.modo="pasta"}
      else await urbeResolverPasta(h,st==="granted"?'sumiu':'permissao');
    }
  }
  let migrada=null;
  try{migrada=await migrarAntiga()}catch(e){console.warn("migracao",e)}
  const alvo=await urbeEnsureSingleVault();
  await abrirCidade(alvo);
  if(migrada)toast('Arquivos anteriores reunidos em Cidades.');
}
(async()=>{
  for(let tentativa=0;;tentativa++){
    try{await urbeIniciar();return}
    catch(e){console.warn("boot",e);try{v21SetLoading(false)}catch(_){}
      const pasta=Disco.modo==='pasta';
      const r=await UD.choose({title:'Não consegui abrir o Urbe',message:'Erro: '+((e&&e.message)||e)+(pasta?'\n\nA pasta do Urbe pode estar indisponível.':''),
        options:[{value:'de-novo',icon:'restore',label:'Tentar de novo'}].concat(TEM_FSA?[{value:'outra',icon:'folder',label:'Escolher outra pasta'}]:[],pasta?[{value:'interno',icon:'storage',label:'Usar o armazenamento do aplicativo'}]:[])});
      if(r==='outra'){await v21PickRoot();return}
      if(r==='interno')await urbeUsarInterno(true);
      if(!r&&tentativa>2){abrirMenu();return}
    }
  }
})();



})();
