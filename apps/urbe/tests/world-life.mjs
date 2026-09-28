// Vida do mundo (src/world/life.js): luz ao longo do dia, eventos, interações e desenho sem erro.
import fs from 'node:fs';import vm from 'node:vm';
const read=f=>fs.readFileSync(new URL('../'+f,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}

/* contexto 2D de mentira: aceita qualquer chamada e conta quantas houve */
function fakeCtx(){let n=0;const grad={addColorStop(){}};
  return new Proxy({},{get(t,k){if(k==='__n')return n;if(k in t)return t[k];if(k==='createRadialGradient'||k==='createLinearGradient')return()=>grad;if(k==='createImageData')return(w,h)=>({data:new Uint8ClampedArray(w*h*4)});if(k==='measureText')return()=>({width:10});return(...a)=>{n++}},set(t,k,v){t[k]=v;return true}})}
function load(opts){
  opts=opts||{};const ctx=fakeCtx(),B={DEEP:0,SEA:1,RIVER:2,LAKE:3,BEACH:4,GRASS:5,MEADOW:6,FOREST:7,DENSE:8,SWAMP:9,TAIGA:10,TUNDRA:11,SNOW:12,HILLS:13,MOUNTAIN:14,PEAK:15,DESERT:16,SAVANNA:17,STEPPE:18};
  const roads=new Set();for(let x=0;x<40;x++)roads.add(x+',20');for(let y=0;y<40;y++)roads.add('20,'+y);
  const world={roads,buildings:[{x:22,y:16,w:3,h:3,tipo:'nota'},{x:14,y:22,w:3,h:3,tipo:'nota'}],regions:[{id:'r1',name:'Centro',w:10,h:10,x:15,y:15,parentId:'r0'}]};
  const povo=opts.povo||[],fauna=opts.fauna||[];let pressa=1,registrado=null,anim=0;
  const cam={x:20*32,y:20*32,z:1};
  const opc=Object.assign({moradores:true,fauna:true,clima:true,eventos:true,ambiente:'dia'},opts.opcoes||{});
  const host={ctx,TILE:32,camera:()=>cam,w2s:(x,y)=>({x:(x-cam.x)*cam.z+200,y:(y-cam.y)*cam.z+400}),faixa:()=>({x0:6,y0:-5,x1:34,y1:45}),largura:()=>400,altura:()=>800,
    agua:(x,y)=>Math.floor(x)<6||(opts.lago&&Math.floor(x)>30&&Math.floor(y)>30),bioma:(x,y)=>opts.bioma!=null?opts.bioma:(Math.floor(y)%3===0?B.FOREST:B.MEADOW),B,
    mundo:()=>world,rua:(x,y)=>roads.has(Math.floor(x)+','+Math.floor(y)),casaEm:(x,y)=>world.buildings.find(b=>x>=b.x&&x<b.x+b.w&&y>=b.y&&y<b.y+b.h)||null,
    povo:()=>povo,fauna:()=>fauna,opcoes:()=>opc,animar:()=>{anim++},redesenhar(){},visivel:()=>true,pressa:f=>{pressa=f},nomeBairro:r=>r.name,centroBairro:r=>({x:r.x+5,y:r.y+5}),registrar:v=>{registrado=v}};
  const services={'world.life.host':host};const cmds={};
  const canvas=()=>({width:0,height:0,getContext:()=>fakeCtx()});
  const doc={createElement:t=>t==='canvas'?canvas():{style:{},setAttribute(){},appendChild(){}},body:{appendChild(){}}};
  const c={window:{UrbeCore:{service:k=>services[k],commands:{register:(id,d)=>{cmds[id]=d}}}},document:doc,console,performance:{now:()=>Date.now()},setInterval:()=>0,Date,Math,Map,Set,Object,Array,Symbol,String,Number,JSON,parseInt};
  vm.createContext(c);vm.runInContext(read('src/world/life.js'),c);
  return{V:c.window.UrbeVida,ctx,host,opc,povo,fauna,world,cmds,get pressa(){return pressa},get registrado(){return registrado},get anim(){return anim}};
}
const run=(L,seg)=>{for(let i=0;i<Math.round(seg*30);i++)L.V._passo(1/30)};

await test('luz: aurora, dia claro, sol dourado, crepúsculo e noite, sem saltos',()=>{
  const {V}=load(),L=V._luzDa;
  ok(L(12).escuro===0&&L(12).cor.join()==='255,255,255','meio-dia sem filtro');ok(L(23).escuro===1,'noite escura');
  ok(L(17.5).cor[2]<L(12).cor[2]&&L(17.5).escuro<.3,'fim de tarde dourado');ok(L(6).escuro>0&&L(6).escuro<1,'aurora');
  let pior=0,prev=L(0);for(let h=.01;h<=24;h+=.01){const c=L(h);pior=Math.max(pior,Math.abs(c.escuro-prev.escuro));prev=c}ok(pior<.02,'transição suave: '+pior);
});
await test('registra as camadas (chão, céu, brilho) e os comandos de evento; desenha sem erro de dia e de noite',()=>{
  const X=load();ok(X.registrado===X.V&&typeof X.V.chao==='function'&&typeof X.V.ceu==='function'&&typeof X.V.brilho==='function','camadas');
  ok(Object.keys(X.cmds).length>=8&&X.cmds['world.event.chuva'],'comandos');
  run(X,6);X.V.chao();X.V.ceu();X.V.brilho();ok(X.ctx.__n>0,'desenhou');
  X.opc.ambiente='noite';X.V.evento('festa');run(X,6);X.V.chao();X.V.ceu();X.V.brilho();
  const e=X.V.estado();ok(e.escuro===1&&e.vagalumes>0&&e.festa==='Centro','noite com vaga-lumes e festa: '+JSON.stringify(e));
});
await test('chuva: escurece, apressa os moradores, faz marolas; depois pode vir o arco-íris; no frio vira neve',()=>{
  const X=load();X.V.evento('chuva');run(X,10);let e=X.V.estado();
  ok(e.chuva>.3&&e.gotas>30&&X.pressa>1.2&&e.escuro>0,'chovendo: '+JSON.stringify(e));
  const Y=load({bioma:12});Y.V.evento('chuva');run(Y,5);ok(Y.V.estado().neve,'neve no frio');
});
await test('interações: pombos voam quando alguém chega perto; ovelhas fogem da raposa; cervo se afasta de quem passa',()=>{
  const X=load({opcoes:{ambiente:'dia'}});run(X,15);let e=X.V.estado();ok(e.pombos>0,'pombos pousaram');
  /* um morador chega perto dos pombos */
  const pombo=()=>X.V.estado().pombos;X.povo.push({tipo:'ocioso',x:20.5,y:20.5,pausa:0});let voaram=0;for(let y=-5;y<45;y+=.25)for(let x=6;x<34;x+=4){X.povo[0].x=x;X.povo[0].y=y;X.V._passo(1/120);voaram=Math.max(voaram,X.V.estado().pombosVoando)}
  ok(voaram>0,'pombos voaram quando o morador chegou perto');X.povo.length=0;
  const ovelha={kind:'sheep',x:14,y:28,st:'idle',tx:14,ty:28},cervo={kind:'deer',x:26,y:30,st:'idle',tx:26,ty:30};X.fauna.push(ovelha,cervo);
  X.opc.ambiente='noite';X.V._passo(1/30);X.V.evento('raposa');const rp=X.V.estado().raposaXY;ok(rp,'a raposa apareceu');
  ovelha.x=ovelha.tx=rp[0]+rp[2]*4;ovelha.y=ovelha.ty=rp[1]+.3;const ox=ovelha.x;
  run(X,1);const fugiu=ovelha.st==='walk'&&Math.sign(ovelha.tx-ox)===rp[2],ov1=JSON.stringify(ovelha);
  X.povo.push({tipo:'ocioso',x:26.5,y:30.4,pausa:0});run(X,4);
  ok(cervo.st==='walk'&&Math.hypot(cervo.tx-26,cervo.ty-30)>1.5,'cervo foge de quem passa');
  ok(fugiu,'ovelha foge para longe da raposa: '+ov1);
});
await test('sem clima e sem eventos: nada de chuva, balão ou festa sozinhos; limites de partículas respeitados',()=>{
  const X=load({opcoes:{clima:false,eventos:false}});run(X,200);const e=X.V.estado();
  ok(e.chuva===0&&!e.balao&&!e.festa&&e.nuvens===0,'nada acontece: '+JSON.stringify(e));
  const Y=load({lago:true});for(const ev of ['chuva','festa','balao','barco','revoada'])Y.V.evento(ev);Y.opc.ambiente='noite';Y.V.evento('festa');run(Y,40);const s=Y.V.estado();
  ok(s.fumaca<=110&&s.ondas<=140&&s.folhas<=80,'limites: '+JSON.stringify(s));
});
if(failed)console.error(failed+' falha(s)');
