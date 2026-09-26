(function(global){
'use strict';var core=global.UrbeCore;if(!core)return;
class TouchController{
 constructor(events){this.events=events;this.el=null;this.opt=null;this.points=new Map();this.gesture=null;this.handlers=[]}
 attach(el,opt){this.detach();this.el=el;this.opt=opt||{};var on=(n,f)=>{el.addEventListener(n,f,{passive:false});this.handlers.push([n,f])};on('pointerdown',e=>this.down(e));on('pointermove',e=>this.move(e));on('pointerup',e=>this.up(e));on('pointercancel',e=>this.cancel(e));return this}
 down(e){if(this.opt.capture!==false&&this.el.setPointerCapture)this.el.setPointerCapture(e.pointerId);this.points.set(e.pointerId,{x:e.clientX,y:e.clientY});if(this.points.size===1)this.gesture={sx:e.clientX,sy:e.clientY,moved:false,data:this.opt.start&&this.opt.start(e)};else if(this.points.size===2){var p=[...this.points.values()];this.gesture={pinch:true,dist:Math.hypot(p[0].x-p[1].x,p[0].y-p[1].y),data:this.opt.pinchStart&&this.opt.pinchStart(e)}}}
 move(e){var p=this.points.get(e.pointerId);if(!p||!this.gesture)return;p.x=e.clientX;p.y=e.clientY;if(this.points.size===2&&this.gesture.pinch){var a=[...this.points.values()],d=Math.hypot(a[0].x-a[1].x,a[0].y-a[1].y);if(this.opt.pinch)this.opt.pinch(e,d/Math.max(1,this.gesture.dist),this.gesture.data);return}var dx=e.clientX-this.gesture.sx,dy=e.clientY-this.gesture.sy;if(Math.abs(dx)+Math.abs(dy)>7)this.gesture.moved=true;if(this.opt.move)this.opt.move(e,{dx:dx,dy:dy,moved:this.gesture.moved,data:this.gesture.data})}
 up(e){this.points.delete(e.pointerId);var g=this.gesture;if(!g)return;if(!g.pinch&&this.opt.end)this.opt.end(e,{moved:g.moved,data:g.data});if(!this.points.size)this.gesture=null}
 cancel(e){this.points.delete(e.pointerId);if(!this.points.size)this.gesture=null;if(this.opt&&this.opt.cancel)this.opt.cancel(e)}
 detach(){if(this.el)this.handlers.forEach(h=>this.el.removeEventListener(h[0],h[1]));this.handlers=[];this.points.clear();this.gesture=null;this.el=null}
}
var touch=new TouchController(core.events);core.provide('aquarium.touch',touch);global.UrbeTouchController={TouchController:TouchController};
})(window);