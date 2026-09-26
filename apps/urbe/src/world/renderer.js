(function(global){
'use strict';var core=global.UrbeCore;if(!core)return;
class AquariumRenderer{
 constructor(events,scheduler){this.events=events;this.scheduler=scheduler;this.adapter=null;this.dirty=true;this.last=0;this.miniLast=0;events.on('aquarium:changed',()=>this.request());events.on('roads:changed',()=>this.request());events.on('world:spatialChanged',()=>this.request())}
 configure(adapter){this.adapter=adapter;this.request();if(this.scheduler)this.scheduler.add('aquarium.render',t=>this.frame(t),{interval:0,whenVisible:true});return this}
 request(){this.dirty=true}
 frame(t){if(!this.adapter||!this.adapter.visible||!this.adapter.visible())return;if(!this.dirty&&t-this.last<500)return;this.dirty=false;this.last=t;this.adapter.render();if(this.adapter.minimap&&t-this.miniLast>300){this.miniLast=t;this.adapter.minimap()}}
}
var renderer=new AquariumRenderer(core.events,core.service('scheduler'));core.provide('aquarium.renderer',renderer);global.UrbeAquariumRenderer={AquariumRenderer:AquariumRenderer};
})(window);