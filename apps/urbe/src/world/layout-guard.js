(function(global){
  'use strict';
  /* Proteção de layout (REQ-043, RM-F1-17): nenhuma reorganização da cidade sem backup e sem desfazer.
     `before(reason)` é chamado ANTES de mexer nas posições: guarda uma foto do layout em memória (desfazer imediato)
     e grava um backup do mapa atual em `.urbe/backup/<data>-layout-layout/` (restaurável por `workspace.restoreBackup`).
     O registro vai para `vault.json.maintenance` ({kind:'layout',reason,from,to,backup}). */
  var MAPA='.urbe/mapa.json';
  function clone(x){return x==null?x:JSON.parse(JSON.stringify(x))}
  function create(opts){
    var world=opts.world,persistence=opts.persistence,backup=opts.backup||global.UrbeBackup,last=null;
    function snapshot(){
      return{
        regions:world.regions.map(function(r){return{id:r.id,x:r.x,y:r.y,w:r.w,h:r.h,cells:clone(r.cells),color:r.color,parentId:r.parentId||null}}),
        buildings:world.buildings.map(function(b){return{id:b.id,x:b.x,y:b.y,regionId:b.regionId||null}})
      };
    }
    /* texto do mapa como está agora: o do mundo (se já carregou) ou o lido do disco */
    function currentMap(){
      var P=persistence();if(!P)return null;
      var m=null;try{m=typeof P.metadataProvider==='function'?P.metadataProvider():null}catch(_){}
      if(m)return JSON.stringify(m,null,1);
      return (P.snapshot&&P.snapshot.get(MAPA))||(P.originals&&P.originals.get(MAPA))||null;
    }
    async function writeBackup(text,info){
      var P=persistence();if(!P||!P.adapter||!P.vault||P.readOnly||P.mapaReadonly||text==null)return null;
      var b=await backup.create(P.adapter,P.vault,[MAPA],{from:'layout',to:'layout',reason:info.reason,contents:new Map([[MAPA,text]])});
      if(typeof P.recordMaintenance==='function')await P.recordMaintenance({kind:'layout',reason:info.reason,from:info.from||null,to:info.to||null,backup:b.dir});
      return b.dir;
    }
    return{
      /** Foto + backup antes de reorganizar. info: {reason, from, to}. Síncrono na foto; o backup termina em `last.backup` (promessa). */
      before:function(info){
        info=info||{};var text=currentMap();
        last={at:Date.now(),reason:info.reason||'reorganizar',from:info.from||null,to:info.to||null,snap:snapshot(),backup:null};
        last.backup=writeBackup(text,last).catch(function(e){console.warn('layout: backup falhou',e);return null});
        return last;
      },
      last:function(){return last},
      /** Desfaz a última reorganização (foto em memória). Devolve false se não há o que desfazer. */
      undo:function(){
        if(!last)return false;var s=last.snap,byR=new Map(s.regions.map(function(r){return[r.id,r]})),byB=new Map(s.buildings.map(function(b){return[b.id,b]}));
        world.regions.forEach(function(r){var o=byR.get(r.id);if(!o)return;r.x=o.x;r.y=o.y;r.w=o.w;r.h=o.h;r.cells=clone(o.cells);r._cellSet=r.cells?new Set(r.cells):null;r._lotes=null;r.color=o.color;r.parentId=o.parentId});
        world.buildings.forEach(function(b){var o=byB.get(b.id);if(!o)return;b.x=o.x;b.y=o.y;b.regionId=o.regionId});
        var done=last;last=null;if(opts.onRestored)opts.onRestored(done);return true;
      }
    };
  }
  global.UrbeLayoutGuard={create:create};
})(window);
