import fs from 'node:fs';import vm from 'node:vm';
const read=p=>fs.readFileSync(new URL('../'+p,import.meta.url),'utf8');
function test(name,fn){try{fn();console.log('OK  ',name)}catch(e){console.error('FAIL',name,e.message);process.exitCode=1}}
const c={};c.globalThis=c;vm.createContext(c);vm.runInContext(read('src/world/terrain.js'),c);
const T=c.UrbeTerrain,w=T.createWorld('urbe',{spawnX:36,spawnY:25});

test('determinístico: mesma semente, mesmo mundo',()=>{
  const w2=T.createWorld('urbe',{spawnX:36,spawnY:25});
  for(let i=0;i<400;i++){const x=(i*37)%900-450,y=(i*91)%700-350;if(w.biome(x,y)!==w2.biome(x,y))throw new Error('diferente em '+x+','+y)}
  const w3=T.createWorld('outra');let dif=0;for(let i=0;i<400;i++){const x=(i*37)%900-450,y=(i*91)%700-350;if(w.biome(x,y)!==w3.biome(x,y))dif++}
  if(dif<50)throw new Error('semente não muda o mundo');
});
test('o início é terra construível',()=>{
  let ok=0;for(let y=15;y<35;y++)for(let x=26;x<46;x++)if(w.buildable(x,y))ok++;
  if(ok<300)throw new Error('pouca terra construível no início: '+ok+'/400');
});
test('geografia variada: mar, rio, lago, montanha, floresta, deserto, neve',()=>{
  const B=w.bounds,seen=new Set();for(let y=B.y0;y<B.y1;y+=5)for(let x=B.x0;x<B.x1;x+=5)seen.add(w.info(x,y).id);
  for(const id of ['sea','deep','river','lake','mountain','forest','desert','snow','beach'])if(!seen.has(id))throw new Error('sem '+id);
});
/* ---------- mundo físico: placas, relevo, clima e água ---------- */
const g=w.grid,N=g.N,SEA=w.sea;
test('placas tectônicas: continentais e oceânicas, cordilheiras nas colisões',()=>{
  const cont=g.plates.filter(p=>p.cont).length;if(cont<3||cont===g.plates.length)throw new Error('placas: '+cont+' continentais de '+g.plates.length);
  /* as terras mais altas ficam perto de uma borda de placa */
  let alto=0,perto=0;for(let y=2;y<N-2;y++)for(let x=2;x<N-2;x++){const i=y*N+x;if(g.E[i]<.8)continue;alto++;
    let borda=false;for(let d=-14;d<=14&&!borda;d+=2)for(let e=-14;e<=14&&!borda;e+=2){const xx=x+d,yy=y+e;if(xx>=0&&yy>=0&&xx<N&&yy<N&&g.plate[yy*N+xx]!==g.plate[i])borda=true}if(borda)perto++}
  if(!alto||perto/alto<.8)throw new Error('montanhas longe das bordas de placa: '+perto+'/'+alto);
});
test('relevo real: muita planície, pouca serra; mar fundo longe da costa',()=>{
  let terra=0,baixa=0,serra=0;for(let i=0;i<N*N;i++)if(g.E[i]>=SEA){terra++;if(g.E[i]<SEA+.2)baixa++;if(g.E[i]>.78)serra++}
  if(terra/(N*N)<.3||terra/(N*N)>.5)throw new Error('fração de terra '+(terra/N/N).toFixed(2));
  if(baixa/terra<.5)throw new Error('pouca planície');if(serra/terra<.02||serra/terra>.2)throw new Error('serras: '+(serra/terra).toFixed(3));
  const B=w.bounds;if(w.biome(B.x0-50,B.y0-50)!==T.BIOMES.DEEP)throw new Error('fora do continente não é oceano');
});
test('rios descem: cada trecho corre para baixo e termina no mar ou num lago',()=>{
  let ruins=0,rios=0;
  for(let i=0;i<N*N;i++){if(g.E[i]<SEA||g.acc[i]<g.riverQ)continue;rios++;let c=i,passos=0,ok=false;
    while(passos++<N*4){const d=g.down[c];if(d<0){ok=g.E[c]<SEA;break}if(g.F[d]>g.F[c]+1e-9){break}if(g.E[d]<SEA||g.lake[d]){ok=true;break}c=d}
    if(!ok)ruins++}
  if(!rios||ruins)throw new Error(ruins+' de '+rios+' trechos de rio não chegam ao mar ou lago');
});
test('rios crescem com afluentes: vazão aumenta rio abaixo e há confluências',()=>{
  let conf=0,menor=0;const chegam=new Uint8Array(N*N);
  for(let i=0;i<N*N;i++){if(g.E[i]<SEA||g.acc[i]<g.riverQ)continue;const d=g.down[i];if(d>=0&&g.E[d]>=SEA){if(g.acc[d]<g.acc[i])menor++;if(chegam[d]++===1)conf++}}
  if(menor)throw new Error('vazão diminui rio abaixo em '+menor+' trechos');
  if(conf<30)throw new Error('poucas confluências: '+conf);
  const larg=g.rivers.map(s=>s.w);if(Math.max(...larg)<3*Math.min(...larg))throw new Error('rios todos da mesma largura');
});
test('clima: frio perto dos polos e no alto; desertos e florestas nas faixas certas',()=>{
  const media=(f,lat0,lat1)=>{let s=0,n=0;for(let y=0;y<N;y++){const l=Math.abs(y/(N-1)*2-1);if(l<lat0||l>=lat1)continue;for(let x=0;x<N;x++){const i=y*N+x;if(g.E[i]>=SEA&&g.E[i]<SEA+.15){s+=f[i];n++}}}return n?s/n:NaN};
  const polo=media(g.T,.72,1),eq=media(g.T,0,.25);if(!(eq>polo+.25))throw new Error('temperatura não cai para os polos: '+eq+' / '+polo);
  let alto=0,na=0,baixo=0,nb=0;for(let i=0;i<N*N;i++){if(g.E[i]>.8){alto+=g.T[i];na++}else if(g.E[i]>=SEA&&g.E[i]<SEA+.05){baixo+=g.T[i];nb++}}
  if(na&&alto/na>=baixo/nb)throw new Error('montanha não é mais fria');
});
test('outras sementes também formam continentes com rios, serras e cidade em terra',()=>{
  for(const s of ['abc','x7']){const v=T.createWorld(s,{spawnX:36,spawnY:25}),seen=new Set(),B=v.bounds;
    for(let y=B.y0;y<B.y1;y+=9)for(let x=B.x0;x<B.x1;x+=9)seen.add(v.info(x,y).id);
    for(const id of ['river','mountain','forest','sea'])if(!seen.has(id))throw new Error(s+': sem '+id);
    let ok=0;for(let y=15;y<35;y++)for(let x=26;x<46;x++)if(v.buildable(x,y))ok++;if(ok<300)throw new Error(s+': início pouco construível '+ok)}
});
test('regras: água e montanha bloqueiam; rio aceita ponte; mar não',()=>{
  const I=w.INFO;const by=id=>I.find(x=>x.id===id);
  if(by('sea').build||by('sea').road)throw new Error('mar');
  if(by('river').build||!by('river').road||!by('river').bridge)throw new Error('rio');
  if(by('mountain').build||by('mountain').road)throw new Error('montanha');
  if(!by('grass').build||by('dense').cost<=by('grass').cost)throw new Error('custos');
  let achou=false;for(let y=-300;y<300&&!achou;y+=3)for(let x=-300;x<300&&!achou;x+=3)if(w.biome(x,y)===T.BIOMES.SEA){achou=true;if(!w.reason(x,y))throw new Error('sem motivo para o mar')}
});
test('coordenadas fracionárias e negativas são seguras',()=>{
  if(w.biome(-10.7,3.2)!==w.biome(-11,3))throw new Error('floor');
});
test('app integra terreno com regras e pontes',()=>{
  const app=read('src/app.js'),index=read('index.html');
  if(index.indexOf('./src/world/terrain.js')<0||index.indexOf('./src/world/terrain.js')>index.indexOf('./src/app.js'))throw new Error('ordem');
  for(const k of ['urbeBloqueiaRua(x,y)','passo=reuso?.06:urbeCustoRua(x,y)','function urbeMotivoTerreno','chunk-worker.js'])if(!app.includes(k))throw new Error('falta '+k);
});
{ /* biomas grandes: ao atravessar o mundo em linha reta, cada trecho de bioma dura em média bem mais que antes (17,7 tiles na versão anterior) */
  const W2=c.UrbeTerrain.createWorld('urbe',{spawnX:36,spawnY:25});let runs=0,len=0,cur=-1,n=0;
  const Bd=W2.bounds;for(let y=Bd.y0;y<=Bd.y1;y+=150){cur=-1;for(let x=Bd.x0;x<=Bd.x1;x++){const b=W2.biome(x,y);if(b!==cur){runs++;cur=b}n++}}
  len=n/runs;if(len<25)throw new Error('biomas pequenos demais: trecho médio '+len.toFixed(1)+' tiles');
  /* amostra avulsa do mapa = bioma do chunk */
  for(let i=0;i<200;i++){const x=Math.floor(Math.sin(i)*900),y=Math.floor(Math.cos(i*1.3)*700),s=W2.sample(x,y);if(s.b!==W2.biome(x,y))throw new Error('sample difere em '+x+','+y)}
  console.log('OK   biome scale (trecho médio '+len.toFixed(1)+' tiles) and map sampling');
}
{ /* chão cheio: detalhes variados por bioma (até dois por tile) na terra firme */
  const W3=c.UrbeTerrain.createWorld('urbe',{spawnX:36,spawnY:25}),kinds=new Set();let tiles=0,withDecor=0;
  for(let y=-200;y<200;y+=3)for(let x=-250;x<250;x+=3){if(!W3.buildable(x,y))continue;const d=W3.decor(x,y);tiles++;if(d){withDecor++;(Array.isArray(d)?d:[d]).forEach(k=>kinds.add(k))}}
  if(kinds.size<14)throw new Error('pouca variedade de detalhes: '+[...kinds].join(','));
  if(withDecor/tiles<.25)throw new Error('chão vazio: só '+Math.round(withDecor/tiles*100)+'% dos tiles com detalhe');
  console.log('OK   ground detail ('+kinds.size+' tipos, '+Math.round(withDecor/tiles*100)+'% dos tiles)');
}
