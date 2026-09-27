import fs from 'node:fs';import vm from 'node:vm';
const read=p=>fs.readFileSync(new URL('../'+p,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function eq(a,b,m){if(JSON.stringify(a)!==JSON.stringify(b))throw new Error((m||'')+'\n esperado '+JSON.stringify(b)+'\n obtido  '+JSON.stringify(a))}
function ok(v,m){if(!v)throw new Error(m||'falhou')}

/* ---------- ambiente ---------- */
let fetchImpl=null;
const c={window:{},console,setTimeout,clearTimeout,TextDecoder,TextEncoder,Response,ReadableStream,AbortController,Headers,fetch:(...a)=>fetchImpl(...a)};
c.window.fetch=c.fetch;vm.createContext(c);
for(const f of ['src/core/core.js','src/core/documents.js','src/core/trash.js','src/core/knowledge-index.js','src/ui/quick-open.js','src/ai/providers.js','src/ai/tools.js','src/ai/agent.js'])vm.runInContext(read(f),c);
const W=c.window,core=W.UrbeCore,docs=core.service('documents'),P=W.UrbeAIProviders,A=W.UrbeAgent,T=W.UrbeAITools;
const J=x=>JSON.parse(JSON.stringify(x));

/* provedor roteirizado: cada passo recebe o pedido e devolve blocos */
let script=[],seen=[];
P.registerKind('mock',{stream:async(p,req,opts)=>{seen.push(J(req));const s=script.shift();if(!s)return{content:[{type:'text',text:'fim'}],stopReason:'end',usage:{input:1,output:1}};
  if(s.throw)throw s.throw;if(s.wait){await new Promise((r,no)=>{const t=setTimeout(r,s.wait);opts.signal&&opts.signal.addEventListener('abort',()=>{clearTimeout(t);const e=new Error('x');e.name='AbortError';no(e)})})}
  const content=typeof s==='function'?s(req):s;(content.filter?content:[]).forEach(b=>{if(b.type==='text')opts.onEvent({type:'text',delta:b.text})});
  return{content,stopReason:content.some(b=>b.type==='tool_use')?'tool_use':'end',usage:{input:10,output:5,cost:.001}}}});
const provider={id:'m',preset:'custom',kind:'mock',baseUrl:'mock://'};
const tu=(name,input,id)=>({type:'tool_use',id:id||('t'+Math.random().toString(36).slice(2,7)),name,input});
const say=t=>[{type:'text',text:t}];
function userMsg(t){return{role:'user',content:[{type:'text',text:t}]}}
async function go(o){return A.run({core,provider,model:'m1',messages:o.messages,agent:o.agent&&A.agentById(o.agent),policy:o.policy,approve:o.approve,signal:o.signal,maxSteps:o.maxSteps,contextTokens:o.contextTokens,editor:()=>({path:'Diário.md',selection:'um trecho'}),remember:o.remember,onUpdate:o.onUpdate})}

docs.upsert({path:'Diário.md',content:'# Diário\n\nHoje choveu.\n'});
docs.upsert({path:'Projetos/Urbe.md',content:'# Urbe\n\nVeja [[Diário]].\n\n## Tarefas\n- revisar\n'});

await test('loop completo: buscar → ler → editar (com aprovação) → responder; e desfazer',async()=>{
  script=[[tu('search_notes',{query:'diario'})],[tu('read_note',{path:'Diário.md'})],[tu('edit_note',{path:'Diário.md',old_text:'Hoje choveu.',new_text:'Hoje choveu forte.'})],say('Pronto: editei [[Diário]].')];
  const asked=[],msgs=[userMsg('melhore o diário')];
  const r=await go({messages:msgs,approve:async a=>{asked.push(a);return'approve'}});
  eq(r.stopped,'end');eq(r.steps,4);eq(asked.length,1,'só a edição pede aprovação');
  eq(asked[0].preview.after,'# Diário\n\nHoje choveu forte.\n');
  eq(docs.get('Diário.md').content,'# Diário\n\nHoje choveu forte.\n');
  eq(r.changes.length,1);ok(r.usage.input===40&&Math.abs(r.usage.cost-.004)<1e-9,'uso somado');
  ok(msgs[2].content[0].content.includes('Diário.md'),'resultado da busca volta ao modelo');
  ok(seen[1].messages.some(m=>m.content.some(b=>b.type==='tool_result')),'tool_result enviado');
  const rep=T.revert({core},r.changes);eq(rep.restored,1);eq(docs.get('Diário.md').content,'# Diário\n\nHoje choveu.\n');
});

await test('recusa vira erro para o modelo e o loop continua',async()=>{
  script=[[tu('write_note',{path:'Diário.md',content:'apagado'})],say('Ok, não mexo.')];
  const msgs=[userMsg('reescreva')];const r=await go({messages:msgs,approve:async()=>({reject:true,reason:'não'})});
  eq(docs.get('Diário.md').content,'# Diário\n\nHoje choveu.\n');const res=msgs[2].content[0];ok(res.is_error&&/recusou/.test(res.content)&&res._status==='rejected');eq(r.stopped,'end');
});

await test('ferramenta desconhecida, argumentos inválidos e erro da ferramenta viram tool_result de erro',async()=>{
  script=[[tu('voar',{}),tu('read_note',{}),tu('read_note',{path:'Nao existe'})],say('ok')];
  const msgs=[userMsg('x')];await go({messages:msgs});const rs=msgs[2].content;
  ok(rs.every(b=>b.is_error));ok(/desconhecida/.test(rs[0].content));ok(/falta "path"/.test(rs[1].content));ok(/não encontrada/.test(rs[2].content));
});

await test('edit_note exige trecho exato e único; append em seção',async()=>{
  let e='';try{T._applyEdit('a b a','a','x')}catch(err){e=err.message}ok(/2 vezes/.test(e));
  try{T._applyEdit('linha  um','linha um','x')}catch(err){e=err.message}ok(/espaços/.test(e));
  eq(T._appendText('# A\n\n## Tarefas\n- um\n\n## Outra\nx','- dois','Tarefas'),'# A\n\n## Tarefas\n- um\n- dois\n\n## Outra\nx');
});

await test('política: somente leitura esconde ferramentas de escrita; auto não pede aprovação; excluir sempre pede',async()=>{
  script=[say('ok')];await go({messages:[userMsg('x')],policy:'readonly'});
  const names=seen[seen.length-1].tools.map(t=>t.name);ok(names.includes('read_note')&&!names.includes('edit_note')&&!names.includes('delete_note'));
  let asked=0;script=[[tu('append_to_note',{path:'Diário.md',content:'mais'}),tu('delete_note',{path:'Projetos/Urbe.md'})],say('ok')];
  const r=await go({messages:[userMsg('x')],policy:'auto',approve:async()=>{asked++;return{reject:true}}});
  eq(asked,1,'só a exclusão pediu');ok(docs.get('Projetos/Urbe.md'),'exclusão recusada');T.revert({core},r.changes);
});

await test('aprovar tudo vale para o resto da execução',async()=>{
  let asked=0;script=[[tu('append_to_note',{path:'Diário.md',content:'1'}),tu('append_to_note',{path:'Diário.md',content:'2'})],say('ok')];
  const r=await go({messages:[userMsg('x')],approve:async()=>{asked++;return'approve_all'}});eq(asked,1);ok(/1\n\n2\n$/.test(docs.get('Diário.md').content));T.revert({core},r.changes);eq(docs.get('Diário.md').content,'# Diário\n\nHoje choveu.\n');
});

await test('renomear atualiza links e o desfazer volta tudo',async()=>{
  script=[[tu('rename_note',{path:'Diário.md',new_path:'Registros/Diário de bordo.md'})],say('ok')];
  const r=await go({messages:[userMsg('x')],policy:'auto'});
  ok(docs.get('Registros/Diário de bordo.md'));ok(docs.get('Projetos/Urbe.md').content.includes('[[Diário de bordo]]'));eq(r.changes.length,2);
  T.revert({core},r.changes);ok(docs.get('Diário.md'));ok(docs.get('Projetos/Urbe.md').content.includes('[[Diário]]'));
});

await test('desfazer não atropela edição feita depois pelo usuário',async()=>{
  script=[[tu('append_to_note',{path:'Diário.md',content:'IA'})],say('ok')];const r=await go({messages:[userMsg('x')],policy:'auto'});
  const d=docs.get('Diário.md');docs.upsert({...d,content:d.content+'\neu mexi'});const rep=T.revert({core},r.changes);eq(rep.restored,0);eq(rep.skipped,['Diário.md']);
  docs.upsert({...docs.get('Diário.md'),content:'# Diário\n\nHoje choveu.\n'});
});

await test('limite de passos e interrupção',async()=>{
  script=Array.from({length:5},()=>[tu('workspace_overview',{})]);const r=await go({messages:[userMsg('x')],maxSteps:3});eq(r.stopped,'max_steps');eq(r.steps,3);script=[];
  const ac=new AbortController();script=[{wait:2000}];setTimeout(()=>ac.abort(),30);const r2=await go({messages:[userMsg('x')],signal:ac.signal});eq(r2.stopped,'aborted');
});

await test('caminho com emoji estragado pelo modelo ainda acha a nota',async()=>{
  docs.upsert({path:'Burgo/Teste/🇬🇧 Gue.md',content:'# Gue\n\nIdioma gaélico.\n'});
  script=[[tu('read_note',{path:'Burgo/Teste/\uFFFD Gue.md'})],say('ok')];const msgs=[userMsg('fale sobre o gue')];await go({messages:msgs});
  const r=msgs[2].content[0];ok(!r.is_error&&/gaélico/.test(r.content),'leu a nota: '+r.content);
  script=[[tu('read_note',{path:'Gue'})],say('ok')];const m2=[userMsg('x')];await go({messages:m2});ok(!m2[2].content[0].is_error,'pelo título sem emoji');
  docs.remove?docs.remove('Burgo/Teste/🇬🇧 Gue.md'):null;
});

await test('mesma chamada que falhou não roda de novo e a repetição encerra o loop',async()=>{
  const bad=()=>[tu('read_note',{path:'Nada.md'})];script=[bad(),bad(),bad(),bad(),bad()];
  const msgs=[userMsg('x')];const r=await go({messages:msgs});
  eq(r.stopped,'loop');ok(/já falhou/.test(msgs[4].content[0].content),'aviso de repetição');ok(r.steps<=4,'parou cedo: '+r.steps);script=[];
});

await test('modelo que termina só com raciocínio depois das ferramentas é lembrado de responder',async()=>{
  script=[[tu('search_notes',{query:'diario'})],[],say('Achei o [[Diário]].')];
  const msgs=[userMsg('ache')];const r=await go({messages:msgs});
  eq(r.stopped,'end');const last=msgs[msgs.length-1];ok(last.role==='assistant'&&/Achei/.test(last.content[0].text),'resposta final escrita');
  ok(seen[seen.length-1].messages.some(m=>m.content.some(b=>b.type==='text'&&/não escreveu a resposta/.test(b.text))),'lembrete enviado');
});

await test('read_note entrega a nota inteira mesmo se o modelo pedir só 200 linhas',async()=>{
  docs.upsert({path:'Longa.md',content:Array.from({length:500},(_,i)=>'linha '+(i+1)).join('\n')});
  script=[[tu('read_note',{path:'Longa.md',max_lines:200})],say('ok')];const msgs=[userMsg('x')];await go({messages:msgs});
  const r=msgs[2].content[0].content;ok(/linha 500/.test(r)&&/completa/.test(r)&&!/PARCIAL/.test(r),r.slice(-200));
});

await test('leitura parcial: o modelo que responde sem terminar é lembrado; a resposta vira preliminar',async()=>{
  docs.upsert({path:'Enorme.md',content:Array.from({length:4000},(_,i)=>'linha '+(i+1)+' '+'x'.repeat(40)).join('\n')});
  script=[[tu('read_note',{path:'Enorme.md'})],say('Resposta apressada.'),
    req=>{const last=req.messages[req.messages.length-1];const t=last.content.map(b=>b.text||'').join('');ok(/sem terminar de ler/.test(t)&&/Enorme\.md/.test(t),'lembrete com a nota: '+t);
      const m=t.match(/start_line=(\d+)/);return[tu('read_note',{path:'Enorme.md',start_line:+m[1]})]},
    say('Resposta completa.')];
  const msgs=[userMsg('resuma')];const r=await go({messages:msgs,contextTokens:20000});
  eq(r.stopped,'end');const ans=msgs.filter(m=>m.role==='assistant'&&m.content.some(b=>b.type==='text'));
  ok(ans[0].meta.draft&&/apressada/.test(ans[0].content[0].text),'primeira resposta marcada como preliminar');ok(/completa/.test(ans[ans.length-1].content[0].text));
  ok(/PARCIAL/.test(msgs[2].content[0].content),'primeira leitura foi parcial');
});

await test('grep_notes: linhas exatas, sem acento/caixa, regex e contexto',async()=>{
  const g=T.get('grep_notes');let r=await g.run({pattern:'DIARIO'},{core});ok(/Projetos\/Urbe\.md\n\s+3: Veja \[\[Diário\]\]/.test(r.content),r.content);
  r=await g.run({pattern:'^## ',regex:true},{core});ok(/Tarefas/.test(r.content));
  r=await g.run({pattern:'revisar',context:1},{core});ok(/5- ## Tarefas/.test(r.content)&&/6: - revisar/.test(r.content),r.content);
  r=await g.run({pattern:'inexistente-xyz'},{core});ok(/Nenhuma/.test(r.content));
});

await test('read_notes lê várias notas numa chamada (aceita lista em texto)',async()=>{
  const t=T.get('read_notes'),input={paths:'["Diário.md","Projetos/Urbe.md","Nada.md"]'};eq(T.validate(t,input),[]);
  const r=await t.run(input,{core});ok(/Hoje choveu/.test(r.content)&&/## Tarefas/.test(r.content)&&/erro: Nota não encontrada/.test(r.content));eq(r.reads.length,2);
});

await test('update_plan mostra o plano e fica no resultado da execução',async()=>{
  const plans=[];script=[[tu('update_plan',{steps:[{step:'Buscar',status:'done'},{step:'Escrever',status:'in_progress'}]})],say('ok')];
  const r=await go({messages:[userMsg('x')],onUpdate:e=>{if(e.type==='plan')plans.push(e.steps)}});
  eq(r.plan.length,2);eq(plans.length,1);ok(A.needsApproval(T.get('update_plan'),'ask',false)===false,'plano não pede aprovação');
});

await test('leituras independentes do mesmo passo rodam em paralelo',async()=>{
  T.register({name:'lenta',access:'read',description:'x',parameters:{type:'object',properties:{},additionalProperties:false},run:()=>new Promise(r=>setTimeout(()=>r({content:'ok'}),150))});
  script=[[tu('lenta',{},'a1'),tu('lenta',{},'a2'),tu('lenta',{},'a3')],say('fim')];const t0=Date.now();const msgs=[userMsg('x')];await go({messages:msgs});
  const dt=Date.now()-t0;ok(dt<400,'demorou '+dt+'ms');eq(msgs[2].content.length,3);
});

await test('erro do provedor encerra com mensagem',async()=>{
  script=[{throw:new Error('Sem créditos')}];const msgs=[userMsg('x')];const r=await go({messages:msgs});eq(r.stopped,'error');eq(r.error,'Sem créditos');eq(msgs.length,1,'mensagem vazia removida');
});

await test('subagente pesquisador trabalha em contexto próprio e devolve o resumo',async()=>{
  script=[[tu('delegate_research',{task:'o que há sobre chuva?'})],[tu('search_notes',{query:'chuva'})],say('Choveu segundo [[Diário]].'),say('Resumo entregue.')];
  const msgs=[userMsg('pesquise')];const progress=[];const r=await go({messages:msgs,onUpdate:e=>{if(e.type==='tool_progress')progress.push(e.text)}});
  eq(msgs[2].content[0].content,'Choveu segundo [[Diário]].');ok(progress.length>=1);eq(r.usage.input,40);
  const subTools=seen[seen.length-3].tools.map(t=>t.name);ok(!subTools.includes('edit_note')&&!subTools.includes('delegate_research'),'subagente só lê');
});

await test('memória e contexto do editor',async()=>{
  const mem=[];script=[[tu('remember',{fact:'prefere listas curtas'}),tu('get_editor_context',{})],say('ok')];const msgs=[userMsg('x')];
  await go({messages:msgs,remember:f=>mem.push(f)});eq(mem,['prefere listas curtas']);ok(/Diário.md/.test(msgs[2].content[1].content)&&/um trecho/.test(msgs[2].content[1].content));
});

await test('preparo do contexto: pares tool_use/tool_result sempre íntegros e compactação',async()=>{
  const m=[userMsg('a'),{role:'assistant',content:[{type:'text',text:''},tu('read_note',{path:'x'},'A')]},userMsg('b'),{role:'user',content:[{type:'tool_result',tool_use_id:'ZZ',content:'órfão'}]},{role:'assistant',content:[tu('read_note',{path:'y'},'B')]}];
  const p=A.prepare(m,1e9);
  eq(p.map(x=>x.role),['user','assistant','user','assistant','user']);
  ok(p[2].content[0].type==='tool_result'&&p[2].content[0].tool_use_id==='A','resultado sintético para A');
  ok(!JSON.stringify(p).includes('ZZ'),'órfão removido');ok(p[4].content[0].tool_use_id==='B');
  const big=[userMsg('início')];for(let i=0;i<30;i++){big.push({role:'assistant',content:[tu('read_note',{path:'x'},'id'+i)]});big.push({role:'user',content:[{type:'tool_result',tool_use_id:'id'+i,content:'x'.repeat(5000)}]});big.push({role:'assistant',content:[{type:'text',text:'r'}]});big.push(userMsg('mais '+i))}
  const q=A.prepare(big,30000);let n=0;q.forEach(x=>x.content.forEach(b=>{n+=(b.text||b.content||'').length}));ok(n<32000,'coube no orçamento: '+n);eq(q[0].role,'user');
  const ids=new Set();q.forEach((x,i)=>{if(x.role==='assistant')x.content.filter(b=>b.type==='tool_use').forEach(b=>{ok(q[i+1].content.some(r=>r.tool_use_id===b.id),'par íntegro');ids.add(b.id)})});
});

/* ---------- protocolo OpenAI-compatível (SSE real, fragmentado) ---------- */
function sse(chunks,ct){const enc=new TextEncoder();return new Response(new ReadableStream({start(ctl){chunks.forEach(s=>ctl.enqueue(enc.encode(s)));ctl.close()}}),{status:200,headers:{'content-type':ct||'text/event-stream'}})}
await test('OpenAI: conversão, tool calls em pedaços, uso e custo',async()=>{
  let body=null;fetchImpl=async(url,init)=>{body=JSON.parse(init.body);ok(url==='https://openrouter.ai/api/v1/chat/completions');ok(init.headers.Authorization==='Bearer k');
    const ch=o=>'data: '+JSON.stringify(o)+'\n\n';const full=ch({choices:[{delta:{content:'Vou '}}]})+ch({choices:[{delta:{content:'ler.',tool_calls:[{index:0,id:'c1',function:{name:'read_note',arguments:'{"pa'}}]}}]})+ch({choices:[{delta:{tool_calls:[{index:0,function:{arguments:'th":"A.md"}'}}]}}]})+ch({choices:[{delta:{},finish_reason:'tool_calls'}]})+ch({choices:[],usage:{prompt_tokens:12,completion_tokens:7,cost:0.0021}})+'data: [DONE]\n\n';
    return sse([full.slice(0,37),full.slice(37,120),full.slice(120)])};
  const ev=[];const r=await P.stream({preset:'openrouter',apiKey:'k'},{model:'x/y',system:'S',messages:[userMsg('oi'),{role:'assistant',content:[tu('read_note',{path:'B'},'c0')]},{role:'user',content:[{type:'tool_result',tool_use_id:'c0',content:'conteúdo'}]}],tools:[{name:'read_note',description:'d',parameters:{type:'object'}}]},{onEvent:e=>ev.push(e.type)});
  eq(body.messages.map(m=>m.role),['system','user','assistant','tool']);eq(body.messages[2].tool_calls[0].function.arguments,'{"path":"B"}');eq(body.messages[3].tool_call_id,'c0');
  ok(body.stream_options&&body.usage,'uso pedido');eq(body.tools[0].type,'function');
  eq(r.stopReason,'tool_use');eq(r.content,[{type:'text',text:'Vou ler.'},{type:'tool_use',id:'c1',name:'read_note',input:{path:'A.md'}}]);eq(r.usage,{input:12,output:7,cost:0.0021});ok(ev.includes('tool_delta'));
});
await test('OpenAI: servidor que ignora stream e devolve JSON',async()=>{
  fetchImpl=async()=>new Response(JSON.stringify({choices:[{message:{content:'olá'},finish_reason:'stop'}],usage:{prompt_tokens:1,completion_tokens:1}}),{status:200,headers:{'content-type':'application/json'}});
  const r=await P.stream({preset:'custom',baseUrl:'http://x/v1'},{model:'m',messages:[userMsg('oi')]},{});eq(r.content,[{type:'text',text:'olá'}]);
});
await test('Anthropic: conversão (mesclagem de papéis) e stream com tool_use',async()=>{
  let body=null,headers=null;fetchImpl=async(url,init)=>{body=JSON.parse(init.body);headers=init.headers;ok(url==='https://api.anthropic.com/v1/messages');
    const e=(t,o)=>'event: '+t+'\ndata: '+JSON.stringify({type:t,...o})+'\n\n';
    return sse([e('message_start',{message:{model:'claude-sonnet-5',usage:{input_tokens:30,output_tokens:1}}})+e('content_block_start',{index:0,content_block:{type:'text',text:''}})+e('content_block_delta',{index:0,delta:{type:'text_delta',text:'Certo.'}}),
      e('content_block_stop',{index:0})+e('content_block_start',{index:1,content_block:{type:'tool_use',id:'tu1',name:'search_notes'}})+e('content_block_delta',{index:1,delta:{type:'input_json_delta',partial_json:'{"query":'}}),
      e('content_block_delta',{index:1,delta:{type:'input_json_delta',partial_json:'"chuva"}'}})+e('content_block_stop',{index:1})+e('message_delta',{delta:{stop_reason:'tool_use'},usage:{output_tokens:22}})+e('message_stop',{})])};
  const r=await P.stream({preset:'anthropic',apiKey:'ak'},{model:'claude-sonnet-5',system:'S',messages:[userMsg('a'),userMsg('b'),{role:'assistant',content:[{type:'text',text:''},tu('x',{},'q')]},{role:'user',content:[{type:'tool_result',tool_use_id:'q',content:'r',is_error:true}]}],tools:[{name:'x',description:'d',parameters:{type:'object'}}]},{});
  eq(headers['x-api-key'],'ak');eq(headers['anthropic-version'],'2023-06-01');eq(headers['anthropic-dangerous-direct-browser-access'],'true');
  eq(body.messages.length,3,'usuários seguidos mesclados');eq(body.messages[1].content.length,1,'texto vazio removido');eq(body.messages[2].content[0].is_error,true);eq(body.system,'S');eq(body.tools[0].input_schema.type,'object');ok(body.max_tokens>0);
  eq(r.stopReason,'tool_use');eq(r.content,[{type:'text',text:'Certo.'},{type:'tool_use',id:'tu1',name:'search_notes',input:{query:'chuva'}}]);eq(r.usage.input,30);eq(r.usage.output,22);eq(r.model,'claude-sonnet-5');
});
await test('erros: repete em 429 e explica 401/402 e modelo sem ferramentas',async()=>{
  let n=0;fetchImpl=async()=>{n++;if(n===1)return new Response('{}',{status:429,headers:{'retry-after':'0'}});return new Response(JSON.stringify({choices:[{message:{content:'ok'}}]}),{status:200,headers:{'content-type':'application/json'}})};
  const r=await P.stream({preset:'custom',baseUrl:'http://x'},{model:'m',messages:[userMsg('a')]},{});eq(n,2);eq(r.content[0].text,'ok');
  for(const [st,body,re] of [[401,{error:{message:'bad key'}},/chave/],[402,{error:{message:'x'}},/créditos/],[404,{error:{message:'No endpoints found that support tool use'}},/não aceita ferramentas/]]){
    fetchImpl=async()=>new Response(JSON.stringify(body),{status:st});let msg='';try{await P.stream({preset:'openrouter',apiKey:'k'},{model:'m',messages:[userMsg('a')]},{})}catch(e){msg=e.message}ok(re.test(msg),st+': '+msg)}
  let m2='';try{await P.stream({preset:'anthropic'},{model:'m',messages:[]},{})}catch(e){m2=e.message}ok(/Configure a chave/.test(m2));
});

await test('interface: markdown do modelo é escapado (sem HTML/JS injetado) e diff por linhas',async()=>{
  W.document={addEventListener(){},getElementById(){return null},querySelector(){return null}};c.document=W.document;
  vm.runInContext(read('src/ai/store.js'),c);vm.runInContext(read('src/ai/ui.js'),c);const md=W.UrbeAgentUI._md;
  const h=md('Olá <img src=x onerror=alert(1)> [x](javascript:alert(1)) [[Nota|apelido]] **b** `c<d`\n\n- item\n\n| a | b |\n|---|---|\n| 1 | 2 |');
  ok(!/<img/.test(h)&&h.includes('&lt;img'),'HTML escapado');ok(!/href="javascript/.test(h),'sem link javascript');
  ok(/<a class="ag-wiki" data-note="Nota">apelido<\/a>/.test(h),'wiki-link');ok(h.includes('<strong>b</strong>')&&h.includes('<code>c&lt;d</code>'));ok(h.includes('<table>'));
  const d=W.UrbeAgentUI._diff(['a','b','c'],['a','x','c']);eq(d.map(o=>o[0]).join(''),' -+ ');
});

await test('editor: abrir e sair de uma nota não reescreve o arquivo; listas sem linha em branco',async()=>{
  const app=read('src/app.js');ok(app.includes('if(urbeVisualBase!=null&&next===urbeVisualBase)return;'),'sync só quando há mudança');
  ok(app.includes('if(itens.length)out.push(itens.join("\\n"));'),'itens de lista juntos');
});

if(!failed)console.log('OK   todos os testes do agente');
