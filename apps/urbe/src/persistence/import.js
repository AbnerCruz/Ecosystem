(function(global){
  'use strict';
  /* Critical legacy compatibility path, REQ-110. The C# client owns the portable
     successor in Urbe.Core/ImportPipeline; this module retires at the client cutover. */
  var JOURNAL='.urbe/import.v1.json',MAX_TOTAL=256*1024*1024,MAX_FILE=64*1024*1024,MAX_COUNT=10000;
  function path(p){p=String(p||'');if(!p||p.startsWith('/')||/[\\:\u0000-\u001f\u007f]/.test(p)||p.split('/').some(function(s){return !s||s==='.'||s==='..'}))throw Error('Nome de arquivo inválido: '+p);return p}
  function text(bytes){try{return new TextDecoder('utf-8',{fatal:true}).decode(bytes)}catch(_){throw Error('Texto com codificação não reconhecida. Salve-o em UTF-8 e tente novamente.')}}
  function canceled(signal){if(signal&&signal.aborted)throw new DOMException('Importação cancelada.','AbortError')}
  async function hash(bytes){return global.UrbeExportManifest.sha256(bytes)}
  async function bytesOf(file,signal){
    if(file.size>MAX_FILE)throw Error('Um dos arquivos excede o limite seguro de 64 MB.');
    var chunks=[],length=0;
    if(file.stream){var reader=file.stream().getReader();try{while(true){canceled(signal);var next=await reader.read();if(next.done)break;length+=next.value.length;if(length>MAX_FILE)throw Error('Arquivo excede o limite seguro.');chunks.push(next.value)}}finally{await reader.cancel().catch(function(){});reader.releaseLock()}}
    else {canceled(signal);var b=new Uint8Array(await file.arrayBuffer());length=b.length;chunks.push(b)}
    if(length>MAX_FILE)throw Error('Arquivo excede o limite seguro.');var out=new Uint8Array(length),offset=0;chunks.forEach(function(c){out.set(c,offset);offset+=c.length});return out;
  }
  async function zipBytes(entry){
    return new Promise(function(resolve,reject){var chunks=[],size=0,crc=-1,stream=entry.internalStream('uint8array');
      stream.on('data',function(chunk){size+=chunk.length;if(size>MAX_FILE){stream.pause();reject(Error('Arquivo compactado excede o limite seguro.'));return}chunks.push(chunk);for(var b of chunk){crc^=b;for(var j=0;j<8;j++)crc=(crc>>>1)^((crc&1)?0xedb88320:0)}});
      stream.on('error',reject);stream.on('end',function(){if(entry._data&&((crc^-1)|0)!==(entry._data.crc32|0)){reject(Error('O arquivo compactado está corrompido.'));return}var out=new Uint8Array(size),offset=0;chunks.forEach(function(c){out.set(c,offset);offset+=c.length});resolve(out)});stream.resume();
    });
  }
  async function inspect(selection,opts){
    opts=opts||{};var files=new Map(),keys=new Map(),total=0,manifest=null,backup=false,absent=[];
    if(!selection.length)throw Error('Nenhum arquivo selecionado.');if(selection.length>MAX_COUNT)throw Error('Há arquivos demais nesta seleção.');
    async function add(p,bytes){canceled(opts.signal);p=path(p);if(p.toLowerCase()===JOURNAL)throw Error('O vault contém uma importação interrompida. Abra-o no aparelho de origem para recuperar antes de importar.');var key=p.toLowerCase(),old=keys.get(key);
      if(old){if(await hash(files.get(old))!==await hash(bytes))throw Error('Arquivos têm o mesmo nome e conteúdos diferentes: '+p);return}
      total+=bytes.length;if(bytes.length>MAX_FILE||total>MAX_TOTAL||files.size>=MAX_COUNT)throw Error('A seleção excede o limite seguro de importação.');keys.set(key,p);files.set(p,bytes)}
    for(var item of selection){
      var file=item.file||item,p=path(item.rel||file.webkitRelativePath||file.name),bytes=await bytesOf(file,opts.signal);
      var zip=bytes.length>=4&&bytes[0]===80&&bytes[1]===75&&((bytes[2]===3&&bytes[3]===4)||(bytes[2]===5&&bytes[3]===6));
      if(zip){
        if(selection.length!==1||opts.directory)throw Error('Selecione o arquivo compactado separadamente dos demais documentos.');
        if(!global.JSZip)throw Error('Não foi possível abrir o arquivo compactado offline.');
        var archive;try{archive=await global.JSZip.loadAsync(bytes)}catch(_){throw Error('O arquivo compactado está inválido ou incompleto.')}
        var entries=Object.values(archive.files).filter(function(e){return !e.dir&&!e.name.split('/').includes('__MACOSX')});
        if(entries.length>MAX_COUNT)throw Error('O arquivo compactado tem arquivos demais.');
        entries.forEach(function(e){path(e.unsafeOriginalName||e.name)});
        var names=entries.map(function(e){return e.name}),root=names.length&&names[0].split('/')[0],prefix=root&&!root.startsWith('.')&&names.every(function(n){return n.startsWith(root+'/')})?root+'/':'';
        for(var entry of entries){
          if(entry._data&&entry._data.uncompressedSize>MAX_FILE)throw Error('Um arquivo compactado excede o limite seguro.');
          await add(prefix?entry.name.slice(prefix.length):entry.name,await zipBytes(entry));
        }
      }else {if(/\.zip$/i.test(p))throw Error('O arquivo ZIP não foi reconhecido ou está inválido.');await add(p,bytes)}
    }
    var M=global.UrbeExportManifest;
    if(files.has(M.NAME)){
      var parsed=M.parse(text(files.get(M.NAME)));if(parsed.state!=='current'||parsed.manifest.formatVersion!==1)throw Error('Backup não reconhecido ou de versão mais recente. Nenhum dado foi alterado.');
      manifest=parsed.manifest;var listed=new Set();manifest.files.forEach(function(f){path(f.path);if(listed.has(f.path.toLowerCase())||!Number.isSafeInteger(f.size)||f.size<0||!(/^[a-f0-9]{64}$/i.test(f.sha256)))throw Error('Manifesto do backup inválido.');listed.add(f.path.toLowerCase())});files.delete(M.NAME);var check=await M.verify(manifest,files);
      if(!check.ok)throw Error('Os arquivos do backup não conferem. Obtenha uma cópia completa e tente novamente.');
    }
    if(files.has('manifest.json')){
      var candidate;try{candidate=JSON.parse(text(files.get('manifest.json')))}catch(_){}
      var legacyBackup=candidate&&Object.prototype.hasOwnProperty.call(candidate,'from')&&Object.prototype.hasOwnProperty.call(candidate,'to')&&Array.isArray(candidate.files)&&Array.isArray(candidate.absent);
      if(legacyBackup||candidate&&candidate.kind==='urbe-import-backup'){
        var restored=new Map();backup=true;
        if(legacyBackup){if(candidate.version!==1)throw Error('Backup de versão mais recente ou não reconhecida.');absent=candidate.absent.map(path);
          for(var f of candidate.files){var original=path(f.path),mapped=original.startsWith('.urbe/')?'urbe/'+original.slice(6):original,content=files.get('files/'+mapped);if(!content||text(content).length!==f.size||f.sha256&&await hash(content)!==f.sha256)throw Error('Backup incompleto ou corrompido.');if(restored.has(original))throw Error('Caminho duplicado no backup.');restored.set(original,content)}
        }else{
          if(candidate.version!==2||!candidate.journal||candidate.journal.version!==1||!Array.isArray(candidate.journal.entries))throw Error('Backup não reconhecido.');
          for(var e of candidate.journal.entries){var original=path(e.path);if(e.backup==null){absent.push(original);continue}var content=files.get('files/'+original);if(!content||content.length!==e.size||await hash(content)!==e.sha256)throw Error('Backup incompleto ou corrompido.');if(restored.has(original))throw Error('Caminho duplicado no backup.');restored.set(original,content)}
        }
        files=restored;keys=new Map();for(var p of files.keys())keys.set(p.toLowerCase(),p);
      }
    }
    if(!files.size&&!absent.length)throw Error('Nenhum documento encontrado.');
    var documents=0,assets=0,metadata=false;
    for(var pair of files){var p=pair[0],bytes=pair[1];if(global.UrbeArtifacts.RE.text.test(p))text(bytes);
      if(p.toLowerCase().startsWith('.urbe/')){metadata=true;validateMetadata(p,bytes)}else if(global.UrbeArtifacts.RE.text.test(p)){documents++;if(/\.(page|block|theme)\.json$/i.test(p)){var data;try{data=JSON.parse(text(bytes))}catch(_){throw Error('Documento estruturado inválido: '+p)}if(data.version>1)throw Error('Documento de versão mais recente: '+p)}}else assets++;
    }
    if(metadata&&!backup&&!keys.has('.urbe/vault.json')&&!keys.has('.urbe/mapa.json'))throw Error('Os metadados selecionados não identificam um vault completo. Selecione a pasta inteira ou seu backup.');
    var kind=backup?'backup':metadata?(keys.has('.urbe/vault.json')?'current-vault':'historical-vault'):manifest?'export':'documents';
    return{kind:kind,files:files,documents:documents,assets:assets,manifest:manifest,restore:kind!=='documents',partial:backup,absent:absent};
  }
  function validateMetadata(p,bytes){
    var max={'.urbe/vault.json':['formatVersion',2],'.urbe/mapa.json':['v',4],'.urbe/identity.json':['version',1],'.urbe/journal.json':['version',1],'.urbe/journal.v2.json':['version',2],'.urbe/history.json':['version',1],'.urbe/history.v2.json':['version',2],'.urbe/trash.json':['version',1],'.urbe/trash.v2.json':['version',2],'.urbe/compositions.json':['version',1],'.urbe/compositions.v2.json':['version',2]},contract=max[p.toLowerCase()];
    if(!contract)return;var value;try{value=JSON.parse(text(bytes))}catch(_){throw Error('Metadados inválidos no vault: '+p)}
    var version=value&&value[contract[0]];if(version==null&&(p==='.urbe/mapa.json'||p==='.urbe/trash.json'))return;
    if(!Number.isInteger(version)||version<1||version>contract[1])throw Error('Este vault contém dados mais recentes ou não reconhecidos. Ele foi preservado.');
  }
  async function current(adapter,vault){var files=new Map(),total=0;for(var p of await adapter.list(vault)){path(p);if(p===JOURNAL)throw Error('Recupere a importação interrompida antes de continuar.');var blob=await adapter.readBlob(vault,p);if(!blob)throw Error('Não foi possível ler '+p);total+=blob.size;if(blob.size>MAX_FILE||total>MAX_TOTAL)throw Error('O vault atual excede o limite seguro desta operação. Nenhum dado foi alterado.');files.set(p,new Uint8Array(await blob.arrayBuffer()))}return files}
  async function plan(inspection,existing,choices){
    var operations=[],conflicts=[],unchanged=0,casePaths=new Map();existing.forEach(function(_,p){if(casePaths.has(p.toLowerCase()))throw Error('O vault contém nomes conflitantes: '+p);casePaths.set(p.toLowerCase(),p)});
    for(var pair of inspection.files){var p=pair[0],bytes=pair[1],previous=casePaths.get(p.toLowerCase());
      if(previous){if(await hash(existing.get(previous))===await hash(bytes)){unchanged++;continue}if(!choices||!choices[p]){conflicts.push(p);continue}if(choices[p]==='keep'){if(inspection.restore)throw Error('A restauração foi cancelada para preservar o vault atual.');unchanged++;continue}if(choices[p]!=='replace')throw Error('Escolha de conflito inválida.');p=previous}
      operations.push({path:p,bytes:bytes});
    }
    if(inspection.restore&&!inspection.partial)for(var p of existing.keys())if(!inspection.files.has(p)&&!p.startsWith('.urbe/backup/')){if(!choices||choices[p]!=='replace')conflicts.push(p);else operations.push({path:p,bytes:null})}
    for(var missing of inspection.absent||[]){if(inspection.files.has(missing))throw Error('Backup contraditório.');if(existing.has(missing)){if(!choices||choices[missing]!=='replace')conflicts.push(missing);else operations.push({path:missing,bytes:null})}}
    return{inspection:inspection,operations:operations,conflicts:Array.from(new Set(conflicts)),unchanged:unchanged,revision:await revision(existing)};
  }
  async function revision(files){var rows=[];for(var p of Array.from(files.keys()).sort())rows.push(p+'\u0000'+await hash(files.get(p)));return hash(new TextEncoder().encode(rows.join('\n')))}
  async function verify(adapter,vault,p,bytes){var actual=await adapter.readBlob(vault,p);if(!actual||await hash(new Uint8Array(await actual.arrayBuffer()))!==await hash(bytes))throw Error('Não foi possível conferir a gravação de '+p)}
  async function backupEntries(adapter,vault,manifest){
    if(!manifest||manifest.version!==1||!Array.isArray(manifest.entries)||typeof manifest.root!=='string'||!/^\.urbe\/backup\/import-[a-z0-9-]+$/.test(manifest.root))throw Error('Recuperação não reconhecida. Nenhum arquivo foi alterado.');
    var seen=new Set(),entries=[];for(var e of manifest.entries){path(e.path);if(e.path.toLowerCase()===JOURNAL||e.path.toLowerCase().startsWith(manifest.root.toLowerCase()+'/')||seen.has(e.path.toLowerCase()))throw Error('Recuperação contém caminhos inválidos.');seen.add(e.path.toLowerCase());
      var bytes=null;if(e.backup!=null){if(e.backup!==manifest.root+'/files/'+e.path)throw Error('Caminho de backup inválido.');var blob=await adapter.readBlob(vault,e.backup);if(!blob)throw Error('Backup ausente; vault permanece protegido.');bytes=new Uint8Array(await blob.arrayBuffer());if(bytes.length!==e.size||await hash(bytes)!==e.sha256)throw Error('Backup incompleto; vault permanece protegido.')}
      entries.push({path:e.path,bytes:bytes});}return entries;
  }
  async function recover(adapter,vault){
    var paths=await adapter.list(vault);if(!paths.includes(JOURNAL))return false;
    var manifest;try{manifest=JSON.parse(await adapter.read(vault,JOURNAL))}catch(_){throw Error('Importação interrompida com registro ilegível. O vault está protegido; preserve uma cópia para recuperação.')}
    var entries=await backupEntries(adapter,vault,manifest);for(var e of entries){if(e.bytes===null)await adapter.remove(vault,e.path);else{await adapter.writeBlob(vault,e.path,new Blob([e.bytes]));await verify(adapter,vault,e.path,e.bytes)}}
    await adapter.remove(vault,JOURNAL);return true;
  }
  async function execute(adapter,vault,plan,opts){
    opts=opts||{};if(plan.conflicts.length)throw Error('Resolva os conflitos antes de importar.');canceled(opts.signal);
    var existing=await current(adapter,vault);if(await revision(existing)!==plan.revision)throw Error('O vault mudou durante a revisão. Confira a importação novamente.');
    if(!plan.operations.length)return{written:0,removed:0,unchanged:plan.unchanged};
    // Backups are deliberately excluded from removal; incoming backups do not overwrite them.
    var root='.urbe/backup/import-'+global.crypto.randomUUID(),journal={version:1,root:root,entries:[]};
    for(var op of plan.operations){canceled(opts.signal);if(op.path.startsWith('.urbe/backup/')&&existing.has(op.path))throw Error('Um backup existente não pode ser substituído.');var old=existing.get(op.path),e={path:op.path,backup:null};if(old){e.backup=root+'/files/'+op.path;e.size=old.length;e.sha256=await hash(old);await adapter.writeBlob(vault,e.backup,new Blob([old]));await verify(adapter,vault,e.backup,old)}journal.entries.push(e)}
    await adapter.write(vault,root+'/manifest.json',JSON.stringify({version:2,kind:'urbe-import-backup',journal:journal}));
    // This durable record is written and verified before any live vault mutation.
    var raw=JSON.stringify(journal);await adapter.write(vault,JOURNAL,raw);if(await adapter.read(vault,JOURNAL)!==raw)throw Error('Não foi possível preparar a recuperação. Nenhum dado foi aplicado.');
    try{var written=0,removed=0;for(var op of plan.operations){canceled(opts.signal);if(op.bytes===null){await adapter.remove(vault,op.path);removed++}else{await adapter.writeBlob(vault,op.path,new Blob([op.bytes]));await verify(adapter,vault,op.path,op.bytes);written++}if(opts.progress)opts.progress(written+removed,plan.operations.length)}await adapter.remove(vault,JOURNAL);return{written:written,removed:removed,unchanged:plan.unchanged,backup:root}}
    catch(error){try{await recover(adapter,vault)}catch(recovery){throw Error('A importação falhou e aguarda recuperação ao reabrir. O backup foi preservado. '+recovery.message)}throw error}
  }
  async function restoreBackup(adapter,vault,dir){
    var saved=JSON.parse(await adapter.read(vault,dir+'/manifest.json'));if(saved.version!==2||saved.kind!=='urbe-import-backup')throw Error('Backup não reconhecido.');
    var entries=await backupEntries(adapter,vault,saved.journal),existing=await current(adapter,vault),operations=entries.map(function(e){return{path:e.path,bytes:e.bytes}});
    await execute(adapter,vault,{inspection:{restore:true},operations:operations,conflicts:[],unchanged:0,revision:await revision(existing)});
    return{restored:entries.filter(function(e){return e.bytes!==null}).map(function(e){return e.path}),removed:entries.filter(function(e){return e.bytes===null}).map(function(e){return e.path})};
  }
  global.UrbeImport={inspect:inspect,plan:plan,current:current,execute:execute,recover:recover,JOURNAL:JOURNAL,revision:revision,restoreBackup:restoreBackup};
})(window);
