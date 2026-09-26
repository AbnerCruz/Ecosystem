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
test('geografia variada: mar, rio, montanha, floresta, deserto, neve',()=>{
  const seen=new Set();for(let y=-600;y<600;y+=4)for(let x=-600;x<600;x+=4)seen.add(w.info(x,y).id);
  for(const id of ['sea','river','mountain','forest','desert','snow','beach'])if(!seen.has(id))throw new Error('sem '+id);
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
