'use strict';
/* Acesso ao disco do app de computador. Todo caminho vem relativo à pasta do vault e é
   conferido aqui: nada sai dela (nem com "..", caminho absoluto ou atalho para fora),
   mesmo que um plugin ou página tente. */
const fs=require('fs/promises');
const path=require('path');

function createVaultFS(getRoot){
  function resolve(rel){
    rel=String(rel==null?'':rel);
    if(/\u0000/.test(rel))throw new Error('Caminho inválido');
    const parts=rel.split(/[\\/]+/).filter(Boolean);
    if(parts.some(p=>p==='..'||p==='.'))throw new Error('Caminho fora da pasta do Urbe: '+rel);
    const root=path.resolve(getRoot()),abs=path.resolve(root,...parts),r=path.relative(root,abs);
    if(r.startsWith('..')||path.isAbsolute(r))throw new Error('Caminho fora da pasta do Urbe: '+rel);
    return abs;
  }
  async function inside(abs){
    /* segue atalhos (symlinks/junções) e confere de novo */
    const root=await fs.realpath(path.resolve(getRoot())).catch(()=>path.resolve(getRoot()));
    let real;try{real=await fs.realpath(abs)}catch(_){return abs}
    const r=path.relative(root,real);
    if(r.startsWith('..')||path.isAbsolute(r))throw new Error('Caminho fora da pasta do Urbe');
    return real;
  }
  return{
    resolve,
    async stat(rel){try{const s=await fs.stat(await inside(resolve(rel)));return{kind:s.isDirectory()?'directory':'file',size:s.size,mtime:s.mtimeMs}}catch(e){if(e.code==='ENOENT'||e.code==='ENOTDIR')return null;throw e}},
    async list(rel){const d=await inside(resolve(rel));const es=await fs.readdir(d,{withFileTypes:true});
      return es.filter(e=>e.isDirectory()||e.isFile()).map(e=>({name:e.name,kind:e.isDirectory()?'directory':'file'}))},
    async readBytes(rel){return new Uint8Array(await fs.readFile(await inside(resolve(rel))))},
    async writeBytes(rel,bytes){const abs=resolve(rel);await fs.mkdir(path.dirname(abs),{recursive:true});await inside(path.dirname(abs));
      /* grava num temporário e troca: um corte de energia não deixa a nota pela metade */
      const tmp=abs+'.urbe-tmp-'+process.pid+'-'+Date.now();
      await fs.writeFile(tmp,Buffer.from(bytes&&bytes.buffer?new Uint8Array(bytes.buffer,bytes.byteOffset,bytes.byteLength):bytes||[]));
      try{await fs.rename(tmp,abs)}
      catch(e){await fs.rm(tmp,{force:true});
        /* no Windows a troca falha se outro programa (antivírus, sincronizador) está com o arquivo aberto */
        if(e.code==='EPERM'||e.code==='EACCES'||e.code==='EBUSY')await fs.writeFile(abs,Buffer.from(bytes||[]));else throw e}},
    async mkdir(rel){await fs.mkdir(resolve(rel),{recursive:true})},
    async remove(rel,recursive){const abs=await inside(resolve(rel));if(abs===path.resolve(getRoot()))throw new Error('A pasta do Urbe não é apagada pelo app.');
      const s=await fs.stat(abs).catch(()=>null);if(!s)return;if(s.isDirectory()){if(recursive)await fs.rm(abs,{recursive:true});else await fs.rmdir(abs)}else await fs.unlink(abs)}
  };
}
module.exports={createVaultFS};
