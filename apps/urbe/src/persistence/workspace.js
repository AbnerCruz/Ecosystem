(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents'),trash=core&&core.service('trash'),history=core&&core.service('history'),compositions=core&&core.service('compositions');if(!core||!docs)return;
  var Meta=global.UrbeVaultMeta,Backup=global.UrbeBackup,Identity=global.UrbeIdentity;
  if(!Meta||!Backup||!Identity)throw new Error('persistence: src/persistence/vault-meta.js, backup.js e identity.js precisam carregar antes de workspace.js (src/modules.json)');

  /* Arquivos do vault que esta camada possui (contrato em docs/v2/discovery/DATA-CATALOG.md §9).
     2.x só ESCREVE os arquivos v2; os v1 da 1.x são lidos uma vez (migração) e nunca mais tocados (REQ-035, ADR-0004). */
  var MAPA='.urbe/mapa.json',JOURNAL_V1='.urbe/journal.json',JOURNAL_V2='.urbe/journal.v2.json';
  var SIDES=[];
  if(trash)SIDES.push({key:'trash',service:trash,v1:'.urbe/trash.json',v2:'.urbe/trash.v2.json',indent:1});
  if(history)SIDES.push({key:'history',service:history,v1:'.urbe/history.json',v2:'.urbe/history.v2.json',indent:0});
  if(compositions)SIDES.push({key:'compositions',service:compositions,v1:'.urbe/compositions.json',v2:'.urbe/compositions.v2.json',indent:0});
  var V1_FILES=[MAPA,JOURNAL_V1,'.urbe/trash.json','.urbe/history.json','.urbe/compositions.json','.urbe/tutorial.json','.urbe/merged-v1.json'];
  var TEXT_RE=window.UrbeArtifacts.RE.text;
  function editable(p){return TEXT_RE.test(p)&&!p.split('/').some(function(part){return part.startsWith('.')})}
  function parseJSON(raw){try{return JSON.parse(raw)}catch(_){return undefined}}

  class WorkspacePersistence{
    constructor(events,store){
      this.events=events;this.store=store;this.adapter=null;this.vault=null;this.meta=null;this.snapshot=new Map();this.busy=false;this.pending=false;this.timer=null;this.delay=900;this.suspended=true;this.state='idle';this.lastSavedAt=null;
      this.paths=[];this.originals=new Map();this.identity=null;this.fpCache=new Map();this.lastReconcile={renames:[],ambiguous:[]};this.vaultInfo={state:'absent',data:null};this.readOnly=false;this.mapaReadonly=false;this.sideReadonly={};this.foreign=[];
    }
    configure(adapter){this.adapter=adapter;return this}
    /* Situação de formato do vault (para a UI de recuperação/diagnóstico). */
    info(){return{vault:this.vault,format:this.vaultInfo.state,readOnly:this.readOnly,mapaReadonly:this.mapaReadonly,foreign:this.foreign.slice(),sideReadonly:Object.assign({},this.sideReadonly)}}

    /* Lê uma "side file" (lixeira, histórico, composições). v2 tem precedência; v1 é lido só para migrar.
       Versão desconhecida/ilegível no arquivo v2 ⇒ preserva o arquivo e desliga a escrita daquele artefato (REQ-035). */
    async readSide(vault,side,paths){
      var svc=side.service,data;
      if(paths.includes(side.v2)){
        data=parseJSON(await this.adapter.read(vault,side.v2));
        if(data&&data.version===2){svc.import(Object.assign({},data,{version:1}));return 'v2'}
        svc.import(null);this.sideReadonly[side.key]=true;
        this.foreign.push({path:side.v2,version:(data&&data.version)||null,reason:data===undefined?'corrupt':'future'});return 'foreign';
      }
      if(paths.includes(side.v1)){
        data=parseJSON(await this.adapter.read(vault,side.v1));
        if(data&&(data.version===1||(side.key==='trash'&&data.version==null))){svc.import(data);return 'v1'}
        svc.import(null);return 'v1-ignored'; // versão maior/ilegível no nome v1: nunca lido nem alterado
      }
      svc.import(null);return 'absent';
    }

    async load(vault){
      if(!this.adapter)throw new Error('Persistence adapter not configured');this.suspended=true;this.vault=vault;
      this.foreign=[];this.sideReadonly={};this.readOnly=false;this.mapaReadonly=false;
      var paths=await this.adapter.list(vault);this.paths=paths;
      var md=paths.filter(editable);
      // formato do vault (REQ-036): lido antes de qualquer escrita
      var vraw=paths.includes(Meta.PATH)?await this.adapter.read(vault,Meta.PATH):null;
      this.vaultInfo=Meta.parse(vraw);
      if(this.vaultInfo.state==='future'){this.readOnly=true;this.foreign.push({path:Meta.PATH,version:this.vaultInfo.data.formatVersion,reason:'future'})}
      else if(this.vaultInfo.state==='current')this.vaultInfo={state:'current',data:Meta.touch(this.vaultInfo.data,core.version)};
      /* estado 1.x original, capturado ANTES de qualquer escrita (a camada legada de app.js pode reescrever o mapa antes do primeiro flush): base do backup de migração */
      this.originals=new Map();
      for(const p of V1_FILES.concat(paths.filter(function(x){return x.indexOf('.urbe/origens/')===0})))if(paths.includes(p))this.originals.set(p,await this.adapter.read(vault,p));
      if(this.vaultInfo.state==='corrupt'&&vraw!=null)this.originals.set(Meta.PATH,vraw);
      var meta=null;try{meta=JSON.parse(this.originals.get(MAPA)||'null')}catch(_){}
      /* mapa.v é lido e validado (REQ-040): ausente = mapa legado (tratado como v1); 1..4 = formatos da 1.x; maior = futuro;
         qualquer outro valor = desconhecido. Futuro e desconhecido são preservados (a persistência não reescreve o mapa). */
      if(meta&&typeof meta==='object'){var mv=meta.v;
        if(typeof mv==='number'&&mv>4){this.mapaReadonly=true;this.foreign.push({path:MAPA,version:mv,reason:'future'})}
        else if(mv!==undefined&&!(Number.isInteger(mv)&&mv>=1)){this.mapaReadonly=true;this.foreign.push({path:MAPA,version:mv,reason:'unknown'})}}
      // journal de uma operação interrompida: v2 (2.x) ou v1 (1.x); qualquer outra versão é preservada
      var journal=null,journalPath=null;
      for(const cand of [[JOURNAL_V2,2],[JOURNAL_V1,1]]){
        if(!paths.includes(cand[0]))continue;
        var jd=parseJSON(await this.adapter.read(vault,cand[0]));
        if(jd&&jd.version===cand[1]&&Array.isArray(jd.documents)){journal=jd;journalPath=cand[0];break}
        this.foreign.push({path:cand[0],version:(jd&&jd.version)||null,reason:jd===undefined?'corrupt':'future'});
        if(cand[0]===JOURNAL_V2)this.sideReadonly.journal=true; // journal v2 de versão maior: preservado, sem diário nesta sessão
      }
      for(const side of SIDES)await this.readSide(vault,side,paths);
      // sidecar de identidade (REQ-042): versão maior é preservada; ilegível é recriado (é derivado das notas)
      var idInfo=Identity.parse(paths.includes(Identity.PATH)?await this.adapter.read(vault,Identity.PATH):null);
      if(idInfo.state==='future'){this.sideReadonly.identity=true;this.foreign.push({path:Identity.PATH,version:idInfo.data.version,reason:'future'})}
      this.identity=idInfo.state==='current'?idInfo.data:null;this.fpCache=new Map();
      var notesMeta=(meta&&meta.notas)||{},items=[];
      for(var i=0;i<md.length;i++){var path=md[i],m=notesMeta[path]||{};items.push({id:m.id||null,path:path,content:(await this.adapter.read(vault,path))||'',tags:m.tags||null,created:m.criado||null,modified:m.modificado||null})}
      /* rename/move feito fora do app enquanto ele estava fechado: a nota mantém ID, casa, região e assets */
      this.lastReconcile=journal?{renames:[],ambiguous:[]}:Identity.reconcile(items,meta,this.identity,new Set(paths));
      var physical=new Map(items.map(function(d){return[d.path,d.content]}));
      var owned=[Meta.PATH,Identity.PATH].filter(function(p){return p!==Identity.PATH||!this.sideReadonly.identity},this).concat(this.mapaReadonly?[]:[MAPA]).concat(SIDES.filter(function(s){return !this.sideReadonly[s.key]},this).map(function(s){return s.v2}));
      for(const path of owned)if(paths.includes(path))physical.set(path,await this.adapter.read(vault,path));
      if(journal){
        items=journal.documents.filter(function(d){return d&&d.path});
        if(compositions&&journal.compositions)compositions.import(journal.compositions);
        if(journal.metadata)meta=journal.metadata;
        if(trash&&journal.trash)trash.import(journal.trash);
        if(history&&journal.history)history.import(journal.history);
        this.events.emit('workspace:recovered',{vault:vault,count:items.length,timestamp:journal.timestamp||null});
      }
      this.store.replaceAll(items,{source:'persistence.load',vault:vault});this.meta=meta||{};this.snapshot=physical;this.suspended=false;
      if(this.lastReconcile.renames.length||this.lastReconcile.ambiguous.length)this.events.emit('workspace:reconciled',{vault:vault,renames:this.lastReconcile.renames.slice(),ambiguous:this.lastReconcile.ambiguous.slice()});
      if(this.foreign.length)this.events.emit('workspace:foreign',{vault:vault,items:this.foreign.slice()});
      if(this.readOnly)this.events.emit('workspace:readonly',{vault:vault,reason:'vault-format-future',formatVersion:this.vaultInfo.data.formatVersion});
      if(journal){await this.flush(this.meta);try{await this.adapter.remove(vault,journalPath)}catch(_){}}
      this.events.emit('workspace:loaded',{vault:vault,documents:this.store.list(),metadata:meta,paths:paths});return{vault:vault,documents:this.store.list(),metadata:meta,paths:paths}
    }

    desired(metadata){
      var files=new Map();this.store.list().forEach(function(d){files.set(d.path,d.content)});
      if(metadata!==undefined&&!this.mapaReadonly)files.set(MAPA,JSON.stringify(metadata||{},null,1));
      for(const side of SIDES){if(this.sideReadonly[side.key])continue;var exp=Object.assign({},side.service.export(),{version:2});files.set(side.v2,side.indent?JSON.stringify(exp,null,side.indent):JSON.stringify(exp))}
      if(this.vaultInfo.state==='current'&&this.vaultInfo.data)files.set(Meta.PATH,Meta.serialize(this.vaultInfo.data));
      if(!this.sideReadonly.identity){this.identity=Identity.build(this.store.list(),this.identity,new Date().toISOString(),this.fpCache);files.set(Identity.PATH,Identity.serialize(this.identity))}
      return files
    }
    /* O mapa vem de quem desenha o mundo (metadataProvider); null = mundo ainda carregando, fica o último mapa conhecido. */
    refreshMeta(){if(typeof this.metadataProvider!=='function')return;var m=null;try{m=this.metadataProvider()}catch(e){console.warn('persistence: mapa indisponível',e)}if(m&&typeof m==='object')this.meta=m}
    schedule(){if(this.suspended||!this.vault||!this.adapter)return;this.pending=true;this.state='dirty';this.events.emit('workspace:dirty',{vault:this.vault});if(this.timer)return;var self=this;this.timer=setTimeout(function(){self.timer=null;self.flush()},this.delay)}

    /* Primeira gravação de um vault sem `vault.json`: backup restaurável dos arquivos 1.x existentes (REQ-038) e registro da migração. */
    async ensureVaultFormat(){
      if(this.vaultInfo.state==='current'||this.vaultInfo.state==='future')return;
      var v=this.vault,have=Array.from(this.originals.keys()),migration=null;
      if(have.length&&Backup){var b=await Backup.create(this.adapter,v,have,{from:1,to:2,contents:this.originals});migration=Meta.migrationEntry({from:1,to:2,backup:b.dir});this.events.emit('workspace:migrated',{vault:v,from:1,to:2,backup:b.dir,files:have})}
      this.vaultInfo={state:'current',data:Meta.create({appVersion:core.version,migration:migration})};
    }

    /** Registro de manutenção em vault.json (`maintenance`, últimos 20): GC, reorganização de layout. Vault 1.x passa pela migração com backup antes. */
    async recordMaintenance(entry){
      if(this.readOnly||!this.adapter||!this.vault)return false;
      await this.ensureVaultFormat();var data=this.vaultInfo&&this.vaultInfo.data;if(!data)return false;
      var log=Array.isArray(data.maintenance)?data.maintenance:[];log.push(Object.assign({at:new Date().toISOString()},entry));data.maintenance=log.slice(-20);
      this.schedule();return true;
    }
    async flush(metadata){
      if(this.suspended||!this.vault||!this.adapter)return false;
      if(this.readOnly){this.pending=false;this.state='readonly';return false}
      if(this.busy){this.pending=true;return false}this.busy=true;this.pending=true;
      try{
        await this.ensureVaultFormat();
        while(this.pending){this.pending=false;if(metadata===undefined)this.refreshMeta();var desired=this.desired(metadata===undefined?this.meta:metadata);this.state='saving';this.events.emit('workspace:saving',{vault:this.vault});
          var changed=[];for(const pair of desired)if(this.snapshot.get(pair[0])!==pair[1]&&pair[0]!==JOURNAL_V2)changed.push(pair[0]);
          var removed=Array.from(this.snapshot.keys()).filter(path=>!desired.has(path)&&!path.startsWith('.urbe/'));
          /* o diário (cópia de tudo) só protege operações que mexem em vários arquivos de uma vez;
             editar uma nota grava só ela: antes cada pausa na digitação copiava o vault inteiro */
          var multi=!this.sideReadonly.journal&&changed.filter(function(p){return !p.startsWith('.urbe/')}).length+removed.length>1;
          if(multi){var journal={version:2,timestamp:Date.now(),documents:this.store.list().map(function(d){return{id:d.id,path:d.path,content:d.content,tags:d.tags,created:d.created,modified:d.modified}}),metadata:metadata===undefined?this.meta:metadata,trash:trash?trash.export():null,history:history?history.export():null,compositions:compositions?compositions.export():null};await this.adapter.write(this.vault,JOURNAL_V2,JSON.stringify(journal))}
          for(const pair of desired){if(this.snapshot.get(pair[0])!==pair[1]){await this.adapter.write(this.vault,pair[0],pair[1]);this.snapshot.set(pair[0],pair[1])}}
          for(const path of Array.from(this.snapshot.keys()))if(!desired.has(path)&&!path.startsWith('.urbe/')){await this.adapter.remove(this.vault,path);this.snapshot.delete(path)}
          if(multi)await this.adapter.remove(this.vault,JOURNAL_V2);
        }
        this.state='saved';this.lastSavedAt=Date.now();this.events.emit('workspace:saved',{vault:this.vault,savedAt:this.lastSavedAt});return true
      }catch(error){this.state='error';this.events.emit('workspace:saveError',{vault:this.vault,error:error});throw error}finally{this.busy=false}
    }
    suspend(value){this.suspended=value!==false}

    /* Relê o que mudou na pasta por fora do app (Explorer, Obsidian, OneDrive, outro aparelho).
       Uma mudança de fora só entra se a nota não tem edição local ainda não gravada: nada se perde.
       paths: só esses caminhos (o app de computador avisa quais mudaram); sem paths: a pasta toda. */
    async syncFromDisk(paths){
      if(this.suspended||!this.vault||!this.adapter)return 0;
      if(this.busy||this.timer){var self=this;return new Promise(function(ok){setTimeout(function(){self.syncFromDisk(paths).then(ok,function(){ok(0)})},1200)})}
      var all=!paths,onDisk;
      if(all){onDisk=new Set((await this.adapter.list(this.vault)).filter(editable));paths=Array.from(onDisk)}
      else paths=paths.map(function(p){return String(p).replace(/\\/g,'/')}).filter(editable);
      var byPath=new Map(this.store.list().map(function(d){return[d.path,d]})),changed=0,appeared=[],vanished=[],self=this;
      for(var i=0;i<paths.length;i++){var path=paths[i],disk=await this.adapter.read(this.vault,path),snap=this.snapshot.get(path),d=byPath.get(path);
        if(disk==null){if(!all&&d&&d.content===snap)vanished.push(d);continue}
        if(disk===snap)continue;
        if(d&&snap!==undefined&&d.content!==snap)continue;
        if(d){this.snapshot.set(path,disk);if(d.content!==disk){this.store.upsert(Object.assign({},d,{content:disk}),{source:'disk'});changed++}}
        else appeared.push({path:path,content:disk})}
      if(all)for(const pair of Array.from(this.snapshot)){var pth=pair[0];if(!editable(pth)||onDisk.has(pth))continue;var doc=byPath.get(pth);if(doc&&doc.content===pair[1])vanished.push(doc)}
      /* sumiu de um caminho + apareceu em outro com o mesmo conteúdo = rename/move externo: mesmo documento (REQ-042) */
      var r=Identity.pair(vanished.map(function(d){return{id:d.id,path:d.path,fingerprint:Identity.fingerprint(d.content)}}),appeared),moved=new Set(),arrived=new Set();
      r.pairs.forEach(function(p){var d=self.store.get(p.id),a=appeared.find(function(x){return x.path===p.to});if(!d||!a)return;
        self.snapshot.delete(p.from);self.snapshot.set(p.to,a.content);self.store.upsert(Object.assign({},d,{path:p.to,title:null,content:a.content}),{source:'disk.rename',from:p.from});moved.add(p.id);arrived.add(p.to);changed++});
      appeared.forEach(function(a){if(arrived.has(a.path))return;self.snapshot.set(a.path,a.content);self.store.upsert({path:a.path,content:a.content},{source:'disk'});changed++});
      vanished.forEach(function(d){if(moved.has(d.id))return;self.snapshot.delete(d.path);self.store.remove(d.id,{source:'disk'});changed++});
      if(r.pairs.length||r.ambiguous.length)this.events.emit('workspace:reconciled',{vault:this.vault,renames:r.pairs,ambiguous:r.ambiguous});
      if(changed)this.events.emit('workspace:external',{vault:this.vault,changed:changed});
      return changed;
    }
  }
  var service=new WorkspacePersistence(core.events,docs);core.provide('persistence',service);
  if(trash)core.events.on('trash:changed',function(){service.schedule()});if(history)core.events.on('history:changed',function(){service.schedule()});if(compositions){core.events.on('composition:created',function(){service.schedule()});core.events.on('composition:updated',function(){service.schedule()});core.events.on('composition:removed',function(){service.schedule()})}
  core.events.on('document:created',function(){service.schedule()});core.events.on('document:updated',function(){service.schedule()});core.events.on('document:removed',function(){service.schedule()});
  /* O debounce não pode perder a última operação quando o app é fechado ou vai para segundo plano. */
  function flushPending(){if(!service.timer&&!service.pending)return;if(service.timer){clearTimeout(service.timer);service.timer=null}service.flush().catch(function(){})}
  if(global.addEventListener)global.addEventListener('pagehide',flushPending);
  if(global.document&&global.document.addEventListener)global.document.addEventListener('visibilitychange',function(){if(global.document.hidden)flushPending()});
  /* Backups de migração (REQ-038): listar e restaurar. Restaurar grava os arquivos de volta e recarrega o app (o mundo é reconstruído do disco). */
  core.commands.register('workspace.backups',{title:'Backups do vault',category:'Workspace',execute:function(){return Backup.list(service.adapter,service.vault)}});
  core.commands.register('workspace.restoreBackup',{title:'Restaurar backup do vault',category:'Workspace',execute:async function(ctx){
    if(!ctx||!ctx.dir)throw new TypeError('workspace.restoreBackup requires dir');
    if(service.timer){clearTimeout(service.timer);service.timer=null}
    while(service.busy)await new Promise(function(r){setTimeout(r,20)});
    service.suspend(true);var r=await Backup.restore(service.adapter,service.vault,ctx.dir);
    core.events.emit('workspace:restored',{vault:service.vault,dir:ctx.dir,restored:r.restored,removed:r.removed});
    if(!(ctx&&ctx.reload===false)&&global.location&&global.location.reload)global.location.reload();return r}});
  core.commands.register('workspace.flush',{title:'Salvar workspace',category:'Workspace',execute:function(){return service.flush()}});
  global.UrbePersistence={WorkspacePersistence:WorkspacePersistence};
})(window);
