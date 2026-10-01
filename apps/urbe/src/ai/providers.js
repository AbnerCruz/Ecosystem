(function(global){
  'use strict';
  /* Camada de provedores, independente de modelo.
     O agente só conhece um formato normalizado:
       mensagem  {role:'user'|'assistant', content:[bloco]}
       bloco     {type:'text',text} | {type:'tool_use',id,name,input} | {type:'tool_result',tool_use_id,content,is_error}
       ferramenta{name, description, parameters (JSON Schema)}
     Cada protocolo (OpenAI-compatível e Anthropic) converte para o formato do fio,
     faz streaming SSE e devolve {content, stopReason, usage, model}. */

  var PRESETS={
    openrouter:{kind:'openai',name:'OpenRouter',baseUrl:'https://openrouter.ai/api/v1',needsKey:true,streamUsage:true,extra:{usage:{include:true}},keyHint:'sk-or-…',site:'https://openrouter.ai/keys'},
    anthropic:{kind:'anthropic',name:'Anthropic',baseUrl:'https://api.anthropic.com/v1',needsKey:true,keyHint:'sk-ant-…',site:'https://console.anthropic.com/settings/keys',
      models:['claude-opus-5-5','claude-sonnet-5','claude-haiku-4-5-20251001']},
    openai:{kind:'openai',name:'OpenAI',baseUrl:'https://api.openai.com/v1',needsKey:true,streamUsage:true,keyHint:'sk-…',site:'https://platform.openai.com/api-keys'},
    gemini:{kind:'openai',name:'Google Gemini',baseUrl:'https://generativelanguage.googleapis.com/v1beta/openai',needsKey:true,keyHint:'AIza…',site:'https://aistudio.google.com/apikey'},
    ollama:{kind:'openai',name:'Ollama (local)',baseUrl:'http://localhost:11434/v1',needsKey:false,keyHint:'não precisa'},
    custom:{kind:'openai',name:'Compatível com OpenAI',baseUrl:'',needsKey:false,streamUsage:false,keyHint:'se o servidor exigir'}
  };

  /* ---------- erros com mensagem útil ---------- */
  function ProviderError(message,info){var e=new Error(message);e.name='ProviderError';Object.assign(e,info||{});return e}
  function friendly(status,body,provider){
    var raw=(body&&(body.error&&(body.error.message||body.error)||body.message))||'';raw=typeof raw==='string'?raw:JSON.stringify(raw);
    var low=raw.toLowerCase(),name=provider&&provider.name||'provedor';
    if(status===401||status===403)return 'A chave de '+name+' foi recusada. Confira a chave nas configurações.';
    if(status===402)return 'Sem créditos em '+name+'. Adicione créditos ou troque de provedor.';
    if(status===404&&/model/.test(low))return 'Modelo não encontrado em '+name+': '+raw;
    if(/tool/.test(low)&&/(support|endpoint)/.test(low))return 'Este modelo não aceita ferramentas. Escolha outro modelo (ex.: Claude, GPT, Gemini, Qwen).';
    if(status===413||/context|too long|maximum.*tokens/.test(low))return 'A conversa ficou grande demais para este modelo. Comece uma nova conversa ou use um modelo com mais contexto.';
    if(status===429)return name+' limitou as requisições. Tentando de novo em instantes…';
    if(status>=500)return name+' está instável ('+status+'). '+(raw||'');
    return raw||('Erro '+status+' em '+name);
  }
  var RETRY=[408,409,425,429,500,502,503,504,529];
  function sleep(ms,signal){return new Promise(function(ok,no){var t=setTimeout(ok,ms);if(signal)signal.addEventListener('abort',function(){clearTimeout(t);no(abortError())},{once:true})})}
  function abortError(){var e=new Error('Interrompido');e.name='AbortError';return e}

  async function request(provider,url,init,signal){
    var last=null;
    for(var attempt=0;attempt<4;attempt++){
      if(signal&&signal.aborted)throw abortError();
      var res;
      try{res=await fetch(url,{...init,signal:signal})}
      catch(e){if(e&&e.name==='AbortError')throw e;last=ProviderError('Não foi possível conectar a '+provider.name+'. Verifique a internet'+(provider.kind==='openai'&&/localhost|127\.0\.0\.1/.test(url)?' e se o servidor local está aberto (e aceita CORS)':'')+'.',{network:true});
        if(attempt<2){await sleep(800*(attempt+1),signal);continue}throw last}
      if(res.ok)return res;
      var txt='';try{txt=await res.text()}catch(_){}
      var body=null;try{body=JSON.parse(txt)}catch(_){body={message:txt.slice(0,300)}}
      last=ProviderError(friendly(res.status,body,provider),{status:res.status,body:body});
      if(RETRY.indexOf(res.status)>=0&&attempt<3){var ra=Number(res.headers.get('retry-after'));await sleep(ra>0?Math.min(ra*1000,20000):1000*Math.pow(2,attempt),signal);continue}
      throw last;
    }
    throw last;
  }

  /* ---------- leitor SSE tolerante (linhas partidas, \r\n, comentários) ---------- */
  async function readSSE(res,onEvent,signal){
    var ct=res.headers.get('content-type')||'';
    if(ct.indexOf('text/event-stream')<0&&ct.indexOf('application/json')>=0){onEvent({event:'json',data:await res.text()});return}
    var reader=res.body.getReader(),dec=new TextDecoder(),buf='',event='message',data=[];
    function flush(){if(data.length){onEvent({event:event,data:data.join('\n')})}event='message';data=[]}
    while(true){
      if(signal&&signal.aborted){try{reader.cancel()}catch(_){}throw abortError()}
      var r=await reader.read();if(r.done)break;
      buf+=dec.decode(r.value,{stream:true});
      var lines=buf.split(/\r?\n/);buf=lines.pop();
      for(var i=0;i<lines.length;i++){var l=lines[i];
        if(l==='')flush();else if(l[0]===':')continue;
        else if(l.indexOf('event:')===0)event=l.slice(6).trim();
        else if(l.indexOf('data:')===0)data.push(l.slice(5).replace(/^ /,''));}
    }
    if(buf)data.push(buf.replace(/^data: ?/,''));flush();
  }
  function parseJSONLoose(s){if(!s)return{};try{return JSON.parse(s)}catch(_){ /* argumentos truncados: tenta fechar */
    try{return JSON.parse(s+'"}')}catch(__){}try{return JSON.parse(s+'}')}catch(__){}return{__invalid:s}}}

  /* ================= OpenAI-compatível ================= */
  var openai={
    toWire:function(req,provider){
      var msgs=[];if(req.system)msgs.push({role:'system',content:req.system});
      req.messages.forEach(function(m){
        if(m.role==='assistant'){
          var text=m.content.filter(function(b){return b.type==='text'}).map(function(b){return b.text}).join(''),calls=m.content.filter(function(b){return b.type==='tool_use'});
          var o={role:'assistant',content:text||(calls.length?null:'')};
          if(calls.length)o.tool_calls=calls.map(function(c){return{id:c.id,type:'function',function:{name:c.name,arguments:JSON.stringify(c.input||{})}}});
          msgs.push(o);
        }else{
          m.content.filter(function(b){return b.type==='tool_result'}).forEach(function(b){msgs.push({role:'tool',tool_call_id:b.tool_use_id,content:(b.is_error?'ERRO: ':'')+String(b.content)})});
          var t=m.content.filter(function(b){return b.type==='text'}).map(function(b){return b.text}).join('\n\n');
          if(t)msgs.push({role:'user',content:t});
        }
      });
      var body={model:req.model,messages:msgs,stream:true};
      if(req.tools&&req.tools.length){body.tools=req.tools.map(function(t){return{type:'function',function:{name:t.name,description:t.description,parameters:t.parameters}}});body.tool_choice='auto'}
      if(req.maxTokens)body.max_tokens=req.maxTokens;
      if(req.temperature!=null)body.temperature=req.temperature;
      if(provider.streamUsage)body.stream_options={include_usage:true};
      if(provider.extra)Object.assign(body,provider.extra);
      return body;
    },
    headers:function(p){var h={'Content-Type':'application/json'};if(p.apiKey)h.Authorization='Bearer '+p.apiKey;if(p.preset==='openrouter'){h['X-Title']='Urbe';if(global.location&&/^https?:/.test(global.location.origin))h['HTTP-Referer']=global.location.origin}return h},
    stream:async function(p,req,opts){
      var body=openai.toWire(req,p),res=await request(p,p.baseUrl.replace(/\/$/,'')+'/chat/completions',{method:'POST',headers:openai.headers(p),body:JSON.stringify(body)},opts.signal);
      var text='',reasoning='',calls=[],finish=null,usage={input:0,output:0,cost:null},model=req.model,err=null;
      function chunk(j){
        if(j.error){err=ProviderError(friendly(j.error.code||500,j,p),{status:j.error.code});return}
        if(j.model)model=j.model;
        if(j.usage){usage.input=j.usage.prompt_tokens||usage.input;usage.output=j.usage.completion_tokens||usage.output;if(j.usage.cost!=null)usage.cost=Number(j.usage.cost)}
        var ch=j.choices&&j.choices[0];if(!ch)return;
        var d=ch.delta||ch.message||{};
        if(d.reasoning||d.reasoning_content){var r=d.reasoning||d.reasoning_content;reasoning+=r;opts.onEvent({type:'reasoning',delta:r})}
        if(typeof d.content==='string'&&d.content){text+=d.content;opts.onEvent({type:'text',delta:d.content})}
        (d.tool_calls||[]).forEach(function(tc,k){
          var i=tc.index!=null?tc.index:k,c=calls[i]||(calls[i]={id:'',name:'',args:''});
          if(tc.id)c.id=tc.id;
          if(tc.function){if(tc.function.name){c.name+=tc.function.name;opts.onEvent({type:'tool_start',index:i,name:c.name})}
            if(tc.function.arguments){c.args+=typeof tc.function.arguments==='string'?tc.function.arguments:JSON.stringify(tc.function.arguments);opts.onEvent({type:'tool_delta',index:i,partial:c.args})}}
        });
        if(ch.finish_reason)finish=ch.finish_reason;
      }
      await readSSE(res,function(ev){
        if(ev.event==='json'){try{chunk(JSON.parse(ev.data))}catch(_){}return}
        if(ev.data==='[DONE]')return;try{chunk(JSON.parse(ev.data))}catch(_){}
      },opts.signal);
      if(err)throw err;
      var content=[];if(text)content.push({type:'text',text:text});
      calls.filter(Boolean).forEach(function(c,i){if(!c.name)return;content.push({type:'tool_use',id:c.id||('call_'+Date.now().toString(36)+'_'+i),name:c.name,input:parseJSONLoose(c.args)})});
      var hasTools=content.some(function(b){return b.type==='tool_use'});
      return{content:content,reasoning:reasoning,stopReason:hasTools?'tool_use':finish==='length'?'max_tokens':'end',usage:usage,model:model};
    },
    models:async function(p){
      var res=await request(p,p.baseUrl.replace(/\/$/,'')+'/models',{headers:openai.headers(p)});var j=await res.json();
      return(j.data||j.models||[]).map(function(m){
        var price=m.pricing?{input:Number(m.pricing.prompt||0)*1e6,output:Number(m.pricing.completion||0)*1e6}:null;
        var tools=Array.isArray(m.supported_parameters)?m.supported_parameters.indexOf('tools')>=0:null;
        return{id:m.id||m.name,name:m.name||m.id,context:m.context_length||m.context_window||null,price:price,tools:tools}});
    }
  };

  /* ================= Anthropic (nativo) ================= */
  var anthropic={
    toWire:function(req){
      var msgs=[];
      req.messages.forEach(function(m){
        var blocks=m.content.filter(function(b){return !(b.type==='text'&&!b.text)}).map(function(b){
          if(b.type==='text')return{type:'text',text:b.text};
          if(b.type==='tool_use')return{type:'tool_use',id:b.id,name:b.name,input:b.input||{}};
          return{type:'tool_result',tool_use_id:b.tool_use_id,content:String(b.content),is_error:!!b.is_error};
        });
        if(!blocks.length)return;
        var last=msgs[msgs.length-1];if(last&&last.role===m.role)last.content=last.content.concat(blocks);else msgs.push({role:m.role,content:blocks});
      });
      var body={model:req.model,max_tokens:req.maxTokens||8192,messages:msgs,stream:true};
      if(req.system)body.system=req.system;
      if(req.tools&&req.tools.length)body.tools=req.tools.map(function(t){return{name:t.name,description:t.description,input_schema:t.parameters}});
      if(req.temperature!=null)body.temperature=req.temperature;
      return body;
    },
    headers:function(p){return{'Content-Type':'application/json','x-api-key':p.apiKey||'','anthropic-version':'2023-06-01','anthropic-dangerous-direct-browser-access':'true'}},
    stream:async function(p,req,opts){
      var res=await request(p,p.baseUrl.replace(/\/$/,'')+'/messages',{method:'POST',headers:anthropic.headers(p),body:JSON.stringify(anthropic.toWire(req))},opts.signal);
      var blocks=[],stop=null,usage={input:0,output:0,cost:null},model=req.model,err=null,reasoning='';
      await readSSE(res,function(ev){
        var j;try{j=JSON.parse(ev.data)}catch(_){return}
        var t=j.type||ev.event;
        if(t==='message_start'&&j.message){model=j.message.model||model;var u=j.message.usage||{};usage.input=(u.input_tokens||0)+(u.cache_read_input_tokens||0)+(u.cache_creation_input_tokens||0);usage.output=u.output_tokens||0}
        else if(t==='content_block_start'){var cb=j.content_block||{};blocks[j.index]=cb.type==='tool_use'?{type:'tool_use',id:cb.id,name:cb.name,args:''}:cb.type==='thinking'?{type:'thinking',text:''}:{type:'text',text:cb.text||''};
          if(cb.type==='tool_use')opts.onEvent({type:'tool_start',index:j.index,name:cb.name})}
        else if(t==='content_block_delta'){var b=blocks[j.index],d=j.delta||{};if(!b)return;
          if(d.type==='text_delta'){b.text+=d.text;opts.onEvent({type:'text',delta:d.text})}
          else if(d.type==='input_json_delta'){b.args+=d.partial_json||'';opts.onEvent({type:'tool_delta',index:j.index,partial:b.args})}
          else if(d.type==='thinking_delta'){reasoning+=d.thinking||'';opts.onEvent({type:'reasoning',delta:d.thinking||''})}}
        else if(t==='message_delta'){if(j.delta&&j.delta.stop_reason)stop=j.delta.stop_reason;if(j.usage&&j.usage.output_tokens!=null)usage.output=j.usage.output_tokens}
        else if(t==='error'){err=ProviderError(friendly(j.error&&j.error.type==='overloaded_error'?529:500,j,p))}
      },opts.signal);
      if(err)throw err;
      var content=[];blocks.forEach(function(b){if(!b)return;if(b.type==='text'&&b.text)content.push({type:'text',text:b.text});else if(b.type==='tool_use')content.push({type:'tool_use',id:b.id,name:b.name,input:parseJSONLoose(b.args)})});
      return{content:content,reasoning:reasoning,stopReason:stop==='tool_use'?'tool_use':stop==='max_tokens'?'max_tokens':'end',usage:usage,model:model};
    },
    models:async function(p){
      try{var res=await request(p,p.baseUrl.replace(/\/$/,'')+'/models?limit=100',{headers:anthropic.headers(p)});var j=await res.json();
        return(j.data||[]).map(function(m){return{id:m.id,name:m.display_name||m.id,context:200000,tools:true}})}
      catch(e){if(e.status===401||e.status===403)throw e;return(PRESETS.anthropic.models).map(function(id){return{id:id,name:id,context:200000,tools:true}})}
    }
  };

  var KINDS={openai:openai,anthropic:anthropic};
  function resolve(cfg){var pre=PRESETS[cfg.preset]||PRESETS.custom;return{id:cfg.id,preset:cfg.preset,kind:cfg.kind||pre.kind,name:cfg.name||pre.name,baseUrl:(cfg.baseUrl||pre.baseUrl||'').trim(),apiKey:(cfg.apiKey||'').trim(),streamUsage:cfg.streamUsage!=null?cfg.streamUsage:pre.streamUsage,extra:pre.extra}}
  function kind(p){var k=KINDS[p.kind];if(!k)throw ProviderError('Tipo de provedor desconhecido: '+p.kind);return k}

  /* API pública: stream(cfgProvedor, pedido, {signal,onEvent}) */
  async function stream(cfg,req,opts){
    opts=opts||{};opts.onEvent=opts.onEvent||function(){};
    var p=resolve(cfg);
    if(!p.baseUrl)throw ProviderError('Informe o endereço (URL base) do provedor '+p.name+'.');
    if(PRESETS[p.preset]&&PRESETS[p.preset].needsKey&&!p.apiKey)throw ProviderError('Configure a chave de '+p.name+' nas configurações do Assistente.');
    if(!req.model)throw ProviderError('Escolha um modelo.');
    return kind(p).stream(p,req,opts);
  }
  async function listModels(cfg){var p=resolve(cfg);return kind(p).models(p)}
  async function test(cfg,model){
    var out='';var r=await stream(cfg,{model:model,system:'Responda só com: ok',messages:[{role:'user',content:[{type:'text',text:'ping'}]}],maxTokens:16},{onEvent:function(e){if(e.type==='text')out+=e.delta}});
    return{ok:true,text:out||(r.content[0]&&r.content[0].text)||'',model:r.model};
  }
  function registerKind(name,impl){KINDS[name]=impl}

  global.UrbeAIProviders={PRESETS:PRESETS,stream:stream,listModels:listModels,test:test,registerKind:registerKind,resolve:resolve,_openai:openai,_anthropic:anthropic,_readSSE:readSSE,ProviderError:ProviderError};
})(typeof window!=='undefined'?window:globalThis);
