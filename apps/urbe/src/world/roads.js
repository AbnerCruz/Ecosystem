(function(global){
'use strict';
var core=global.UrbeCore,world=core&&core.service('aquarium.world');if(!core||!world)return;
class RoadGraph{
 constructor(events){this.events=events;this.edges=new Map();this.dirty=new Set();events.on('aquarium:links',e=>this.sync(e&&e.links||[]));events.on('world:spatialChanged',e=>{if(e&&e.document)this.invalidate(e.document.id)});this.sync(world.links)}
 key(a,b){return[a,b].sort().join('|')}
 sync(links){var next=new Map();links.forEach(l=>{var k=this.key(l.from,l.to),old=this.edges.get(k);next.set(k,old||{id:k,from:l.from,to:l.to,path:null,dirty:true});if(!old)this.dirty.add(k)});this.edges=next;for(const k of Array.from(this.dirty))if(!next.has(k))this.dirty.delete(k);this.events.emit('roads:changed',{edges:this.list(),dirty:this.dirty.size})}
 invalidate(id){this.edges.forEach((e,k)=>{if(e.from===id||e.to===id){e.dirty=true;this.dirty.add(k)}});this.events.emit('roads:invalidated',{documentId:id,count:this.dirty.size})}
 setPath(a,b,path){var k=this.key(a,b),e=this.edges.get(k);if(!e)return null;e.path=Array.isArray(path)?path.slice():[];e.dirty=false;this.dirty.delete(k);this.events.emit('roads:path',{edge:{...e}});return e}
 pending(limit){return Array.from(this.dirty).slice(0,limit||20).map(k=>this.edges.get(k)).filter(Boolean)}
 list(){return Array.from(this.edges.values()).map(e=>({...e,path:e.path&&e.path.slice()}))}
}
var roads=new RoadGraph(core.events);core.provide('aquarium.roads',roads);global.UrbeRoadGraph={RoadGraph:RoadGraph};
})(window);