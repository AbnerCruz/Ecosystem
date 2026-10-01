(function(global){
  'use strict';
  /* Modelo de artefatos do vault (REQ-014, REQ-039, ADR-0006): fonte única de tipos e extensões.
     "Tudo é arquivo", mas nem todo arquivo textual é uma nota: o tipo vem do caminho e da extensão.
     Substitui as listas de extensões que existiam duplicadas em persistence, app, ai, explorer, pages e native. */
  var TEXT_ALT='md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv';
  var RE={
    text:new RegExp('\\.('+TEXT_ALT+')$','i'),        // arquivos textuais editáveis (viram documentos)
    splitText:new RegExp('^(.*?)(\\.(?:'+TEXT_ALT+'))$','i'),
    note:/\.(md|markdown)$/i,                          // Markdown
    noteText:/\.(md|markdown|txt)$/i,                  // texto que se comporta como nota
    noteExt:/^\.(md|markdown|txt)$/i
  };
  var PERSONALIZATION='Personalização/';

  function norm(p){return String(p==null?'':p).replace(/\\/g,'/').replace(/^\/+/,'')}
  function isSystem(p){p=norm(p);return p.split('/').some(function(s){return s.charAt(0)==='.'&&s.length>0})}
  function ext(p){var m=norm(p).match(/(\.[^./]+)$/);return m?m[1].toLowerCase():''}

  /** classify(path) → {type, ext, textual, editable, note, system}
      Tipos: system · plugin · theme · style · texture · personalization · page · page-template · page-block · note · text · asset */
  function classify(path){
    var p=norm(path),e=ext(p),sys=isSystem(p),textual=RE.text.test(p);
    var type='asset';
    if(sys)type='system';
    else if(p.indexOf(PERSONALIZATION)===0&&textual){
      if(/^Personalização\/plugins\/.+\.js$/i.test(p))type='plugin';
      else if(/^Personalização\/tema\.json$/i.test(p)||/^Personalização\/temas\/.+\.json$/i.test(p))type='theme';
      else if(/^Personalização\/estilos\/.+\.css$/i.test(p))type='style';
      else if(/^Personalização\/texturas\/.+\.json$/i.test(p))type='texture';
      else type='personalization';
    }
    else if(/\.page\.json$/i.test(p))type='page';
    else if(/\.template\.json$/i.test(p))type='page-template';
    else if(/\.block\.json$/i.test(p))type='page-block';
    else if(RE.noteText.test(p))type='note';
    else if(textual)type='text';
    return{type:type,ext:e,textual:textual&&!sys,editable:textual&&!sys,note:type==='note',system:sys};
  }
  function isNote(p){return classify(p).note}
  function isText(p){return classify(p).textual}

  /* Não são alvo de [[link]] nem entram no índice de títulos: código/configuração do próprio Urbe. */
  var NOT_LINKABLE={system:1,plugin:1,theme:1,style:1,texture:1,personalization:1};
  function linkable(p){return !NOT_LINKABLE[classify(p).type]}

  /* Abertura por tipo (substitui o wrapper de `document.open` que o Studio de páginas instalava, L8):
     cada módulo registra como abrir o seu tipo; `route` decide se um opener trata o documento. */
  var openers={},before=[];
  function registerOpener(type,fn){openers[type]=fn}
  function onBeforeOpen(fn){before.push(fn)}
  /** → {handled, result}; `ctx.raw` força o editor de texto comum. */
  function route(doc,ctx){
    for(var i=0;i<before.length;i++)before[i](doc,ctx||{});
    var o=openers[classify(doc.path).type];
    if(o&&!(ctx&&ctx.raw))return{handled:true,result:o(doc,ctx)};
    return{handled:false};
  }

  /** Nome seguro para arquivo/pasta em qualquer plataforma (Windows, Android/SAF, FSA): sem \\ / : * ? " < > | nem controle,
      sem ponto no início (oculto) ou no fim (Windows), no máximo 90 caracteres. Fonte única (antes `nomeSeguro` em app.js). */
  function safeName(s){return String(s==null?'':s).replace(/[\\/:*?"<>|\u0000-\u001f]/g,'-').replace(/\s+/g,' ').trim().replace(/^\.+/,'').replace(/\.+$/,'').slice(0,90)||'sem-nome'}
  global.UrbeArtifacts={safeName:safeName,linkable:linkable,registerOpener:registerOpener,onBeforeOpen:onBeforeOpen,route:route,RE:RE,TEXT_ALT:TEXT_ALT,classify:classify,isNote:isNote,isText:isText,isSystem:isSystem,ext:ext,
    TYPES:['system','plugin','theme','style','texture','personalization','page','page-template','page-block','note','text','asset']};
})(window);
