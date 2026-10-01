(function(global){
'use strict';var core=global.UrbeCore,docs=core&&core.service('documents');if(!core||!docs)return;
class RevisionHistory{
 constructor(events){this.events=events;this.byDoc=new Map();this.last=new Map();this.limit=40;this.windowMs=5000;events.on('document:updated',e=>this.capture(e));events.on('document:removed',e=>this.captureRemoved(e))}
 capture(e){var d=e&&e.document,p=e&&e.previous;if(!d||!p||d.content===p.content)return;var now=Date.now(),arr=this.byDoc.get(d.id)||[],last=this.last.get(d.id);if(!(last&&now-last<this.windowMs&&arr.length))arr.push({timestamp:now,path:p.path,content:p.content});if(arr.length>this.limit)arr.splice(0,arr.length-this.limit);this.byDoc.set(d.id,arr);this.last.set(d.id,now);this.events.emit('history:changed',{documentId:d.id,count:arr.length})}
 captureRemoved(e){var d=e&&e.document;if(!d)return;var arr=this.byDoc.get(d.id)||[];arr.push({timestamp:Date.now(),path:d.path,content:d.content,removed:true});this.byDoc.set(d.id,arr.slice(-this.limit))}
 list(id){return(this.byDoc.get(String(id))||[]).map(x=>({...x})).reverse()}
 restore(id,index){var d=docs.get(id),arr=this.list(id),rev=arr[index||0];if(!d||!rev)return null;return docs.upsert({...d,content:rev.content,path:d.path},{source:'history.restore'})}
 forget(id){var k=String(id),ok=this.byDoc.delete(k);this.last.delete(k);if(ok)this.events.emit('history:changed',{type:'forget',id:k});return ok}
 export(){var o={};this.byDoc.forEach((v,k)=>o[k]=v);return{version:1,documents:o}}
 import(data){this.byDoc.clear();if(data&&data.version===1&&data.documents)Object.keys(data.documents).forEach(k=>this.byDoc.set(k,(data.documents[k]||[]).slice(-this.limit)));return this}
}
var history=new RevisionHistory(core.events);core.provide('history',history);core.commands.register('history.restore',{title:'Restaurar versão',category:'Histórico',execute:c=>history.restore(c&&c.id,c&&c.index)});global.UrbeRevisionHistory={RevisionHistory};
})(window);
