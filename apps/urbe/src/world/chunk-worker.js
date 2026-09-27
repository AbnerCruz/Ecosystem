/* Gera os pixels do chão fora da thread principal: explorar o mapa não trava a tela. */
importScripts('./terrain.js','./pixel-art.js');
var world=null;
self.onmessage=function(e){
  var m=e.data;
  if(m.type==='init'){world=self.UrbeTerrain.createWorld(m.seed,m.opts);return}
  if(m.type==='chunk'&&world){
    var px=self.UrbeArt.chunkPixels(world,m.cx,m.cy),far=self.UrbeArt.chunkFarPixels(world,m.cx,m.cy,px),ch=world.chunk(m.cx,m.cy),bio=ch.bio.slice(),elev=ch.elev.slice();
    self.postMessage({type:'chunk',cx:m.cx,cy:m.cy,n:world.CH*self.UrbeArt.PX,buf:px.buffer,far:far.buffer,bio:bio.buffer,elev:elev.buffer},[px.buffer,far.buffer,bio.buffer,elev.buffer]);
  }
};
