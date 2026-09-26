(function(global){
'use strict';var core=global.UrbeCore,docs=core&&core.service('documents');if(!core||!docs)return;
class BurgoProjection{
 constructor(events,store){this.events=events;this.store=store;this.enabled=false;this.state={day:1,residents:new Map(),chronicle:[]};events.on('documents:reset',()=>this.sync());events.on('document:created',()=>this.sync());events.on('document:removed',()=>this.sync());this.sync()}
 sync(){var alive=new Set();this.store.list().forEach(d=>{alive.add(d.id);if(!this.state.residents.has(d.id))this.state.residents.set(d.id,{documentId:d.id,name:this.name(d),level:1,vigor:1})});for(const id of Array.from(this.state.residents.keys()))if(!alive.has(id))this.state.residents.delete(id);this.events.emit('burgo:sync',this.snapshot());return this.snapshot()}
 name(d){var names=['Ari','Bela','Cael','Dara','Eron','Fara','Galen','Hana'];var n=0;for(const c of d.id)n=(n*31+c.charCodeAt(0))>>>0;return names[n%names.length]}
 advance(){if(!this.enabled)return this.snapshot();this.state.day++;this.state.residents.forEach(r=>{r.vigor=Math.max(.2,Math.min(1,r.vigor+((r.level%3)-1)*.01));if(this.state.day%(8+r.level)===0)r.level++});this.events.emit('burgo:advanced',this.snapshot());return this.snapshot()}
 setEnabled(v){this.enabled=v!==false;this.events.emit('burgo:enabled',{enabled:this.enabled});return this.enabled}
 snapshot(){return{enabled:this.enabled,day:this.state.day,residents:Array.from(this.state.residents.values()).map(r=>({...r})),chronicle:this.state.chronicle.slice()}}
}
var burgo=new BurgoProjection(core.events,docs);core.provide('burgo.projection',burgo);
core.commands.register('burgo.enable',{title:'Ativar Burgo',category:'Burgo',execute:()=>burgo.setEnabled(true)});
core.commands.register('burgo.disable',{title:'Desativar Burgo',category:'Burgo',execute:()=>burgo.setEnabled(false)});
global.UrbeBurgoProjection={BurgoProjection:BurgoProjection};
})(window);