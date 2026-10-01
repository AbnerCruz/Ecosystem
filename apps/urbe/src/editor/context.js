(function(global){
  'use strict';
  var core=global.UrbeCore,docs=core&&core.service('documents'),knowledge=core&&core.service('knowledge');if(!core||!docs||!knowledge)return;
  function outline(idOrPath){
    var d=docs.get(idOrPath);if(!d)return[];
    var out=[],lines=d.content.split('\n'),offset=0;
    lines.forEach(function(line,i){var m=/^(#{1,6})\s+(.+?)\s*#*\s*$/.exec(line);if(m)out.push({level:m[1].length,title:m[2].trim(),line:i+1,offset:offset});offset+=line.length+1});
    return out;
  }
  function context(idOrPath){
    var d=docs.get(idOrPath);if(!d)return null;
    return {document:d,outline:outline(d.id),links:knowledge.links(d.id),backlinks:knowledge.backlinks(d.id),related:Array.from(new Map(knowledge.links(d.id).concat(knowledge.backlinks(d.id)).map(x=>[x.id,x])).values())};
  }
  var service={outline:outline,context:context};
  core.provide('editor.context',service);
  core.commands.register('editor.context',{title:'Contexto da nota',category:'Editor',execute:function(ctx){return context(ctx&&ctx.id||core.service('editor.session')?.activeId)}});
})(window);
