(function(global){
  'use strict';
  /* IDs estáveis de regiões (`reg_…`) e construções/assets (`ast_…`) no `.urbe/mapa.json` (REQ-041, RM-F1-14, ADR-0006).
     - Mapas da 1.x não têm esses IDs: `assign` os gera de forma determinística (mesmo mapa ⇒ mesmos IDs em toda abertura)
       e eles passam a ser gravados no próximo save, junto da migração.
     - Regiões/assets criados no app recebem ID aleatório (`fresh`).
     - Vínculo asset → nota: `parentId` (ID do documento); `parentNoteName` continua sendo gravado para a 1.x ler. */
  function hash(txt){var h=2166136261;for(var i=0;i<txt.length;i++){h^=txt.charCodeAt(i);h=Math.imul(h,16777619)}return (h>>>0).toString(36)}
  function valid(prefix,v){return typeof v==='string'&&v.indexOf(prefix+'_')===0&&v.length>prefix.length+1}
  function unique(base,used){var id=base,n=2;while(used.has(id))id=base+'_'+(n++);used.add(id);return id}
  function fresh(prefix){
    var c=global.crypto;
    if(c&&typeof c.randomUUID==='function')return prefix+'_'+c.randomUUID().replace(/-/g,'').slice(0,16);
    return prefix+'_'+Date.now().toString(36)+Math.random().toString(36).slice(2,8);
  }
  /** Preenche `regioes[].id` e `construcoes[].id` que faltam (determinístico) e devolve {regions:Map caminho→id, assets:Array}. Não remove nada do mapa. */
  function assign(mapa){
    var out={regions:new Map(),assets:[]};if(!mapa||typeof mapa!=='object')return out;
    var used=new Set(),regioes=Array.isArray(mapa.regioes)?mapa.regioes:[],cons=Array.isArray(mapa.construcoes)?mapa.construcoes:[];
    regioes.forEach(function(r){if(r&&valid('reg',r.id)){if(used.has(r.id))r.id=null;else used.add(r.id)}});
    regioes.forEach(function(r){if(!r||typeof r!=='object')return;if(!valid('reg',r.id))r.id=unique('reg_'+hash('reg:'+(r.caminho||'')),used);out.regions.set(r.caminho,r.id)});
    cons.forEach(function(g){if(g&&valid('ast',g.id)){if(used.has(g.id))g.id=null;else used.add(g.id)}});
    cons.forEach(function(g){if(!g||typeof g!=='object')return;var f=(g.files||g.anexos||[])[0],key=(f&&f.relPath)||((g.caminho||'')+'/'+(g.fileName||g.name||''));if(!valid('ast',g.id))g.id=unique('ast_'+hash('ast:'+key),used);out.assets.push(g.id)});
    return out;
  }
  /** ID determinístico de região para uma pasta que ainda não está no mapa. */
  function regionFor(caminho,used){return used?unique('reg_'+hash('reg:'+(caminho||'')),used):'reg_'+hash('reg:'+(caminho||''))}
  /** Garante ID no objeto de mundo (região ou construção) e o devolve. */
  function ensure(obj,prefix){if(!valid(prefix,obj.uid))obj.uid=fresh(prefix);return obj.uid}
  global.UrbeStableIds={assign:assign,regionFor:regionFor,ensure:ensure,fresh:fresh,valid:valid};
})(window);
