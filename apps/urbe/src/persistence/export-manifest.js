(function(global){
  'use strict';
  /* Manifesto do ZIP exportado (REQ-044, RM-F1-18): `urbe-export.json` na raiz do ZIP.
       {format:'urbe-export', formatVersion:1, appVersion, exportedAt, vault:{name, formatVersion}, files:[{path,size,sha256}], state}
     - `files` cobre todos os arquivos do ZIP (menos o próprio manifesto); o import confere cada hash.
     - `state` é o estado local do aparelho que vale levar junto: aprovações de plugins deste vault e preferências de interface.
       Lista PERMITIDA de chaves; chaves de IA (que ficam no IndexedDB do Assistente) nunca entram, e qualquer chave com cara
       de segredo é recusada mesmo se alguém a colocar na lista. */
  var NAME='urbe-export.json',FORMAT='urbe-export',VERSION=1;
  var PLUGINS='urbe.plugins.v1',EXACT=['urbe.explorer.v2','urbe.editor.workspace.v1'],PREFIX=['urbe.tip.'];
  var SECRET=/(^|[._-])(ai|ia|key|keys|token|secret|senha|password|apikey|api-key|credential)s?([._-]|$)/i;

  function hex(buf){return Array.from(new Uint8Array(buf)).map(function(b){return b.toString(16).padStart(2,'0')}).join('')}
  async function sha256(bytes){var s=global.crypto&&global.crypto.subtle;if(!s)throw new Error('crypto.subtle indisponível');return hex(await s.digest('SHA-256',bytes))}
  function forbidden(key){return SECRET.test(String(key))}
  function allowed(key){if(forbidden(key))return false;return key===PLUGINS||EXACT.indexOf(key)>=0||PREFIX.some(function(p){return String(key).indexOf(p)===0})}

  /** Estado local deste aparelho que acompanha o export. storage: localStorage-like; vault: nome do vault atual. */
  function collectState(storage,vault){
    var out={localStorage:{}};if(!storage)return out;
    for(var i=0;i<storage.length;i++){var k=storage.key(i);if(!allowed(k))continue;var v=storage.getItem(k);if(v==null)continue;
      if(k===PLUGINS){var all={};try{all=JSON.parse(v)||{}}catch(_){all={}}var mine={},pre=vault+'::';Object.keys(all).forEach(function(p){if(p.indexOf(pre)===0)mine[p.slice(pre.length)]=all[p]});out.plugins=mine;continue}
      out.localStorage[k]=v}
    return out;
  }
  /** Aplica o estado de um export neste aparelho, para o vault `vault`. Nunca grava chave proibida. Devolve as chaves gravadas. */
  function applyState(storage,state,vault){
    var wrote=[];if(!storage||!state)return wrote;
    Object.keys(state.localStorage||{}).forEach(function(k){if(!allowed(k)||k===PLUGINS)return;storage.setItem(k,String(state.localStorage[k]));wrote.push(k)});
    if(state.plugins&&typeof state.plugins==='object'){var all={};try{all=JSON.parse(storage.getItem(PLUGINS)||'{}')||{}}catch(_){all={}}
      Object.keys(state.plugins).forEach(function(p){all[vault+'::'+p]=state.plugins[p]});storage.setItem(PLUGINS,JSON.stringify(all));wrote.push(PLUGINS)}
    return wrote;
  }
  /** entries: [{path, bytes:Uint8Array}] → manifesto. */
  async function build(entries,opts){
    opts=opts||{};var files=[];
    for(var i=0;i<entries.length;i++){var e=entries[i];if(e.path===NAME)continue;files.push({path:e.path,size:e.bytes.byteLength,sha256:await sha256(e.bytes)})}
    files.sort(function(a,b){return a.path<b.path?-1:a.path>b.path?1:0});
    return{format:FORMAT,formatVersion:VERSION,appVersion:opts.appVersion||null,exportedAt:new Date(opts.now||Date.now()).toISOString(),
      vault:{name:opts.vault||null,formatVersion:opts.vaultFormat==null?null:opts.vaultFormat},files:files,state:opts.state||{localStorage:{}}};
  }
  function parse(text){
    var m;try{m=JSON.parse(text)}catch(_){return{state:'corrupt',manifest:null}}
    if(!m||m.format!==FORMAT||typeof m.formatVersion!=='number'||!Array.isArray(m.files))return{state:'corrupt',manifest:null};
    if(m.formatVersion>VERSION)return{state:'future',manifest:m};
    return{state:'current',manifest:m};
  }
  /** Confere o ZIP contra o manifesto. entries: Map path → Uint8Array. */
  async function verify(manifest,entries){
    var listed=new Map(manifest.files.map(function(f){return[f.path,f]})),mismatched=[],missing=[],extra=[];
    for(const f of manifest.files){var b=entries.get(f.path);if(!b){missing.push(f.path);continue}if(b.byteLength!==f.size||await sha256(b)!==f.sha256)mismatched.push(f.path)}
    entries.forEach(function(_,p){if(p!==NAME&&!listed.has(p))extra.push(p)});
    return{ok:!mismatched.length&&!missing.length&&!extra.length,mismatched:mismatched,missing:missing,extra:extra};
  }
  global.UrbeExportManifest={NAME:NAME,FORMAT:FORMAT,VERSION:VERSION,sha256:sha256,build:build,parse:parse,verify:verify,collectState:collectState,applyState:applyState,allowed:allowed,forbidden:forbidden};
})(window);
