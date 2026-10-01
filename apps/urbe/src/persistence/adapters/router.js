(function(global){
  'use strict';
  /* Escolhe o adaptador ativo (pasta real × IndexedDB) e expõe o contrato completo. Mantém os nomes em português
     (cidades, listar, ler, escrever…) que o restante de app.js ainda usa, até a migração terminar (REQ-028/029). */
  var A=global.UrbeAdapters=global.UrbeAdapters||{};
  var METHODS=['cities','createCity','removeCity','list','read','readBlob','write','writeBlob','remove','createFolder','removeFolder'];
  var PT={cidades:'cities',criarCidade:'createCity',excluirCidade:'removeCity',listar:'list',ler:'read',lerBlob:'readBlob',escrever:'write',escreverBlob:'writeBlob',apagar:'remove',criarPasta:'createFolder',apagarPasta:'removeFolder'};
  /** Valida que um objeto cumpre o contrato do adaptador de persistência. */
  A.assertAdapter=function(a){METHODS.forEach(function(m){if(typeof a[m]!=='function')throw new TypeError('adaptador sem '+m)});return a};
  /** opts: {mode:()=>'pasta'|'interno'|null, idb:<adapter>, fsa:<adapter>} */
  A.router=function(opts){
    function active(){return opts.mode()==='pasta'?opts.fsa:opts.idb}
    var r={kind:'router',resetCache:function(){if(opts.fsa.resetCache)opts.fsa.resetCache()}};
    METHODS.forEach(function(m){r[m]=function(){var a=active();return a[m].apply(a,arguments)}});
    Object.keys(PT).forEach(function(pt){r[pt]=r[PT[pt]]});
    return r;
  };
})(window);
