(function(global){
'use strict';
var core=global.UrbeCore,docs=core&&core.service('documents'),knowledge=core&&core.service('knowledge'),projection=core&&core.service('world.projection');if(!core||!docs||!projection)return;
class AquariumWorld{
 constructor(events){this.events=events;this.entities=new Map();this.regions=new Map();this.links=[];this.enabled=true;this.revision=0;this.rebuild();events.on('documents:reset',()=>this.rebuild());events.on('document:created',()=>this.rebuild());events.on('document:removed',()=>this.rebuild());events.on('document:updated',e=>this.update(e&&e.document));events.on('world:projection',()=>this.rebuild());events.on('world:spatialChanged',e=>this.update(e&&e.document));events.on('knowledge:indexed',()=>this.rebuildLinks())}
 rebuild(){this.entities.clear();projection.documents().forEach(d=>this.entities.set(d.id,this.entity(d)));this.regions=new Map((projection.snapshot().regions||[]).map(r=>[r.caminho,r]));this.rebuildLinks();this.bump('rebuild');return this.snapshot()}
 entity(p){return{id:p.id,documentId:p.documentId,path:p.path,title:p.title,folder:p.folder,x:p.x,y:p.y,w:3,h:3,sprite:p.sprite||'house1'}}
 update(doc){if(!doc)return;var p=projection.projectDocument(doc.id);if(p)this.entities.set(doc.id,this.entity(p));this.rebuildLinks();this.bump('update')}
 rebuildLinks(){var out=[],seen=new Set();if(knowledge)docs.list().forEach(d=>knowledge.links(d.id).forEach(t=>{var key=[d.id,t.id].sort().join('|');if(!seen.has(key)){seen.add(key);out.push({from:d.id,to:t.id})}}));this.links=out;this.events.emit('aquarium:links',{links:this.links.slice()});return this.links}
 move(id,x,y){var d=docs.get(id),target=d?d.id:id,p=projection.setSpatial(target,{x:x,y:y});if(p)this.entities.set(target,this.entity(p));this.bump('move');return this.entities.get(target)||null}\n get(id){var d=docs.get(id),target=d?d.id:id;return this.entities.get(target)||null}list(){return Array.from(this.entities.values()).map(x=>({...x}))}
 setEnabled(v){this.enabled=v!==false;projection.setEnabled(this.enabled);this.bump('enabled');return this.enabled}
 bump(type){this.revision++;this.events.emit('aquarium:changed',{type:type,revision:this.revision})}
 snapshot(){return{enabled:this.enabled,revision:this.revision,entities:this.list(),regions:Array.from(this.regions.values()).map(x=>({...x})),links:this.links.slice()}}
}
var service=new AquariumWorld(core.events);core.provide('aquarium.world',service);
core.commands.register('aquarium.move',{title:'Mover no aquário',category:'Aquário',execute:c=>service.move(c&&c.id,c&&c.x,c&&c.y)});
core.commands.register('aquarium.enable',{title:'Ativar aquário',category:'Aquário',execute:()=>service.setEnabled(true)});
core.commands.register('aquarium.disable',{title:'Desativar aquário',category:'Aquário',execute:()=>service.setEnabled(false)});
global.UrbeAquariumWorld={AquariumWorld:AquariumWorld};
})(window);