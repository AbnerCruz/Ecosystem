'use strict';
// Read-only, explicitly selected sources. Renderer receives opaque tokens, never filesystem authority.
const fs=require('fs/promises'),path=require('path'),crypto=require('crypto'),{constants}=require('fs');
const MAX_FILE=64*1024*1024,MAX_TOTAL=256*1024*1024,MAX_COUNT=10000;
function createImportSources(cacheRoot){
  const sessions=new Map();
  // The desktop host holds its single-instance lock before constructing this provider.
  let ready;function initialize(){return ready??=fs.mkdir(cacheRoot,{recursive:true}).then(async()=>{for(const name of await fs.readdir(cacheRoot))if(/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/.test(name))await fs.rm(path.join(cacheRoot,name),{recursive:true,force:true})})}
  async function pick(paths,folder){
    await initialize();const token=crypto.randomUUID(),root=path.join(cacheRoot,token),files=[];let total=0;
    await fs.mkdir(root,{recursive:true});
    async function add(file,relative,depth){
      if(depth>64)throw Error('Pasta profunda demais.');
      const stat=await fs.lstat(file);if(stat.isSymbolicLink())throw Error('Atalhos não são importados. Selecione a pasta original.');
      if(stat.isDirectory()) {for(const name of await fs.readdir(file))await add(path.join(file,name),relative?relative+'/'+name:name,depth+1);return}
      if(!stat.isFile())throw Error('Tipo de arquivo não reconhecido.');
      if(stat.size>MAX_FILE||total+stat.size>MAX_TOTAL||files.length>=MAX_COUNT)throw Error('A seleção excede o limite seguro de importação.');
      const canonical=await fs.realpath(file),id=String(files.length+1),target=path.join(root,id),source=await fs.open(file,constants.O_RDONLY|(process.platform==='win32'?0:constants.O_NOFOLLOW));let destination=null,copied=0;
      try{destination=await fs.open(target,'wx');const opened=await source.stat();if(opened.ino!==stat.ino||opened.dev!==stat.dev)throw Error('A seleção mudou durante a leitura.');const chunk=Buffer.alloc(65536);while(true){const r=await source.read(chunk,0,chunk.length,null);if(!r.bytesRead)break;copied+=r.bytesRead;if(copied>MAX_FILE||total+copied>MAX_TOTAL)throw Error('A seleção excede o limite seguro.');let offset=0;while(offset<r.bytesRead){const w=await destination.write(chunk,offset,r.bytesRead-offset,null);if(!w.bytesWritten)throw Error('Não foi possível gravar os arquivos selecionados.');offset+=w.bytesWritten}}await destination.sync();if(await fs.realpath(file)!==canonical||copied!==stat.size)throw Error('O arquivo mudou durante a leitura. Selecione-o novamente.')}finally{await source.close();if(destination)await destination.close()}
      total+=copied;files.push({id,path:relative,size:copied});
    }
    try{for(const file of paths)await add(file,folder?'':path.basename(file),0);sessions.set(token,root);return{token,files,canceled:false}}
    catch(error){await fs.rm(root,{recursive:true,force:true});throw error}
  }
  async function read(token,id,offset){
    const root=sessions.get(token);if(!root||!/^[1-9][0-9]{0,4}$/.test(String(id))||!Number.isSafeInteger(offset)||offset<0)throw Error('Seleção inválida.');
    const file=await fs.open(path.join(root,String(id)),'r');try{const size=(await file.stat()).size;if(offset>size)throw Error('Leitura inválida.');const bytes=Buffer.alloc(Math.min(65536,size-offset));await file.read(bytes,0,bytes.length,offset);return{data:new Uint8Array(bytes),done:offset+bytes.length===size}}finally{await file.close()}
  }
  async function release(token){const root=sessions.get(token);if(!root)return;sessions.delete(token);await fs.rm(root,{recursive:true,force:true})}
  return{pick,read,release,initialize,close:async()=>{for(const token of sessions.keys())await release(token)}};
}
module.exports={createImportSources};
