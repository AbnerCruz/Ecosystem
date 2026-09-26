(function(global){
  'use strict';
  /* Conversas e configurações do Assistente, só neste aparelho (IndexedDB).
     Chaves de API nunca vão para o vault nem para o repositório das notas.
     Sem IndexedDB (testes, modo privado restrito) tudo funciona em memória. */
  var DB='urbe-ai',VER=1,dbp=null,mem={kv:new Map(),conversations:new Map()};
  function open(){
    if(dbp)return dbp;
    dbp=new Promise(function(ok){
      if(!global.indexedDB)return ok(null);
      try{var r=global.indexedDB.open(DB,VER);
        r.onupgradeneeded=function(){var d=r.result;if(!d.objectStoreNames.contains('kv'))d.createObjectStore('kv');if(!d.objectStoreNames.contains('conversations'))d.createObjectStore('conversations',{keyPath:'id'})};
        r.onsuccess=function(){ok(r.result)};r.onerror=function(){ok(null)};r.onblocked=function(){ok(null)};
      }catch(_){ok(null)}
    });
    return dbp;
  }
  function tx(store,mode,fn){return open().then(function(db){
    if(!db){return fn(null)}
    return new Promise(function(ok,no){var t=db.transaction(store,mode),q=fn(t.objectStore(store));t.oncomplete=function(){ok(q&&q.result)};t.onerror=function(){no(t.error)};t.onabort=function(){no(t.error)}})})}

  var DEFAULT_CONFIG={version:1,providers:[],providerId:null,model:'',policy:'ask',agentId:'geral',instructions:'',memory:{},customAgents:[],maxSteps:30,modelCache:{}};

  async function getConfig(){
    var c=await tx('kv','readonly',function(s){if(!s)return{result:mem.kv.get('config')};return s.get('config')});
    if(!c){c=JSON.parse(JSON.stringify(DEFAULT_CONFIG));await migrateLegacy(c);await setConfig(c)}
    return Object.assign(JSON.parse(JSON.stringify(DEFAULT_CONFIG)),c);
  }
  function setConfig(c){return tx('kv','readwrite',function(s){if(!s){mem.kv.set('config',c);return null}return s.put(c,'config')})}

  /* traz a chave do OpenRouter, instruções e memória da versão anterior */
  function legacyGet(key){return new Promise(function(ok){
    if(!global.indexedDB)return ok(null);
    try{var r=global.indexedDB.open('knowledge-city');r.onsuccess=function(){var d=r.result;if(!d.objectStoreNames.contains('kv')){d.close();return ok(null)}
      var q=d.transaction('kv','readonly').objectStore('kv').get(key);q.onsuccess=function(){ok(q.result||null);d.close()};q.onerror=function(){ok(null)}};r.onerror=function(){ok(null)};r.onupgradeneeded=function(){try{r.transaction.abort()}catch(_){}ok(null)}}catch(_){ok(null)}})}
  async function migrateLegacy(c){
    try{
      var old=await legacyGet('urbe.ai.config.v1'),glob=await legacyGet('urbe.ai.global.v21');
      if(old&&old.apiKey){c.providers.push({id:'openrouter',preset:'openrouter',name:'OpenRouter',apiKey:old.apiKey,baseUrl:''});c.providerId='openrouter';c.model=old.model||''}
      if(glob){if(glob.instructions)c.instructions=glob.instructions;if(glob.memory)c.memory={'*':String(glob.memory).split('\n').map(function(x){return x.trim()}).filter(Boolean)}}
    }catch(_){}
  }

  function listConversations(){return tx('conversations','readonly',function(s){if(!s)return{result:Array.from(mem.conversations.values())};return s.getAll()}).then(function(a){return(a||[]).sort(function(x,y){return(y.updated||0)-(x.updated||0)})})}
  function getConversation(id){return tx('conversations','readonly',function(s){if(!s)return{result:mem.conversations.get(id)};return s.get(id)})}
  function saveConversation(c){c.updated=Date.now();var copy=JSON.parse(JSON.stringify(c));return tx('conversations','readwrite',function(s){if(!s){mem.conversations.set(c.id,copy);return null}return s.put(copy)})}
  function deleteConversation(id){return tx('conversations','readwrite',function(s){if(!s){mem.conversations.delete(id);return null}return s.delete(id)})}

  global.UrbeAIStore={getConfig:getConfig,setConfig:setConfig,listConversations:listConversations,getConversation:getConversation,saveConversation:saveConversation,deleteConversation:deleteConversation,DEFAULT_CONFIG:DEFAULT_CONFIG};
})(typeof window!=='undefined'?window:globalThis);
