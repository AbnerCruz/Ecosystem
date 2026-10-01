(function(global){
'use strict';var core=global.UrbeCore;if(!core)return;
class Scheduler{
 constructor(events){this.events=events;this.jobs=new Map();this.running=false;this.raf=0;this.bound=t=>this.tick(t)}
 add(id,fn,opts){opts=opts||{};this.jobs.set(id,{id:id,fn:fn,interval:Math.max(0,opts.interval||0),whenVisible:opts.whenVisible!==false,last:-Infinity,enabled:opts.enabled!==false});this.start();return()=>this.remove(id)}
 remove(id){this.jobs.delete(id);if(!this.jobs.size)this.stop()}
 enable(id,v){var j=this.jobs.get(id);if(j)j.enabled=v!==false}
 tick(t){if(!this.running)return;var hidden=typeof document!=='undefined'&&document.hidden;this.jobs.forEach(j=>{if(!j.enabled||(hidden&&j.whenVisible)||t-j.last<j.interval)return;j.last=t;try{j.fn(t)}catch(error){this.events.emit('scheduler:error',{id:j.id,error:error})}});this.raf=requestAnimationFrame(this.bound)}
 start(){if(this.running||typeof requestAnimationFrame!=='function')return;this.running=true;this.raf=requestAnimationFrame(this.bound)}
 stop(){this.running=false;if(this.raf&&typeof cancelAnimationFrame==='function')cancelAnimationFrame(this.raf);this.raf=0}
 stats(){return{running:this.running,jobs:Array.from(this.jobs.values()).map(j=>({id:j.id,interval:j.interval,enabled:j.enabled}))}}
}
var scheduler=new Scheduler(core.events);core.provide('scheduler',scheduler);global.UrbeScheduler={Scheduler:Scheduler};
})(window);