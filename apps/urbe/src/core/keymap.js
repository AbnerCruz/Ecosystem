(function(global){
  'use strict';
  var core=global.UrbeCore;
  if(!core)return;

  var bindings=new Map();
  function normalize(event){
    var parts=[];
    if(event.ctrlKey||event.metaKey)parts.push('Mod');
    if(event.altKey)parts.push('Alt');
    if(event.shiftKey)parts.push('Shift');
    var key=event.key.length===1?event.key.toUpperCase():event.key;
    parts.push(key);
    return parts.join('+');
  }
  function bind(shortcut,commandId,options){ bindings.set(shortcut,{commandId:commandId,options:options||{}}); }
  function editable(target){ return target&&((target.tagName==='INPUT')||(target.tagName==='TEXTAREA')||target.isContentEditable); }

  document.addEventListener('keydown',function(event){
    var shortcut=normalize(event),binding=bindings.get(shortcut);
    if(!binding)return;
    if(editable(event.target)&&!binding.options.allowInEditor)return;
    if(!core.commands.has(binding.commandId))return;
    event.preventDefault();
    core.commands.execute(binding.commandId,{source:'keyboard',event:event});
  });

  bind('Mod+K','ui.commandPalette.open',{allowInEditor:true});
  bind('Mod+P','ui.quickOpen.open',{allowInEditor:true});
  bind('Mod+S','workspace.save',{allowInEditor:true});
  bind('Mod+W','editor.closeTab',{allowInEditor:true});
  bind('Mod+F','ui.find.open',{allowInEditor:true});
  bind('Alt+ArrowLeft','editor.back',{allowInEditor:true});
  bind('Alt+ArrowRight','editor.forward',{allowInEditor:true});
  bind('Mod+\\\\','editor.split.open',{allowInEditor:true});
  bind('Mod+N','document.create');
  bind('Mod+Shift+M','workspace.navigate.world');
  bind('Mod+Shift+F','ui.explorer.open',{allowInEditor:true});

  core.provide('keymap',{
    bind:bind,
    unbind:function(shortcut){return bindings.delete(shortcut)},
    get:function(shortcut){return bindings.get(shortcut)||null},
    list:function(){return Array.from(bindings.entries()).map(function(x){return {shortcut:x[0],commandId:x[1].commandId}})}
  });
})(window);
