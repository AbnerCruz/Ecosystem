(function(global){
'use strict';
/* Markdown → HTML do editor Visual (RM-F2-09, REQ-027). Pipeline puro: texto entra, HTML sai; sem DOM e sem estado.
   Saiu do `src/app.js` sem mudar o resultado (golden em tests/fixtures/markdown-golden.json, gerado do código antigo).
   Ordem do pipeline (a mesma das três camadas que o app.js empilhava): matemática → cabeçalho vazio ("# ") → blocos.
   Tudo o que é desenhado aqui volta igual em markdownFromVisual (ainda em app.js até RM-F2-10). */
function escapeHTML(s){
  return String(s).replace(/[&<>"']/g,ch=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[ch]));
}
/* Sanitização: a chave da API fica no IndexedDB desta origem; um .md importado não pode ganhar execução de script por um
   link javascript: nem por HTML injetado no nome de uma nota. Os trechos já convertidos (links, imagens, código, wikilinks)
   ficam guardados como marcadores até o fim, porque com dois links no mesmo parágrafo o regex de "_" casava de um _blank
   ao outro e destruía o HTML; e as URLs passam por uma checagem de esquema. */
function inlineEmphasis(t){
  return t.replace(/\*\*([^*\n]+)\*\*/g,'<strong>$1</strong>')
          .replace(/__([^_\n]+)__/g,'<strong>$1</strong>')
          .replace(/(?<!\*)\*([^*\n]+)\*(?!\*)/g,'<em>$1</em>')
          .replace(/(?<!_)_([^_\n]+)_(?!_)/g,'<em>$1</em>')
          .replace(/~~([^~\n]+)~~/g,'<del>$1</del>');
}
function safeUrl(u){
  var t=String(u).replace(/&(?:#\d+|#x[0-9a-fA-F]+|\w+);/g,'').replace(/[\s\u0000-\u001f]/g,'').toLowerCase();
  if(/^(javascript|vbscript|file):/.test(t))return '#';
  if(/^data:/.test(t)&&!/^data:image\//.test(t))return '#';
  return u;
}
function inlineMarkdown(s){
  var x=escapeHTML(s),guarda=[],codigos={};
  var guardar=function(html){guarda.push(html);return '\u0002'+(guarda.length-1)+'\u0002'};
  /* O código sai primeiro: dentro dele [[...]], links, ** e _ são texto. (Antes os wikilinks eram trocados antes do código e o
     marcador do wikilink vazava como U+0002 dentro do <code>.) */
  x=x.replace(/`([^`\n]+)`/g,function(m,c){var k=guarda.length;codigos[k]=c;return guardar('<code>'+c+'</code>')});
  x=x.replace(/\[\[([^\]|#]+)(?:\|([^\]]+))?\]\]/g,function(m,alvo,rotulo){
    var nome=(rotulo||alvo).trim();
    return guardar('<span class="wikilink" data-note-name="'+escapeHTML(alvo.trim())+'">'+escapeHTML(nome)+'</span>');
  });
  x=x.replace(/!\[([^\]]*)\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g,function(m,alt,src){
    /* no atributo alt o código volta como texto com crases, nunca como tag */
    alt=alt.replace(/\u0002(\d+)\u0002/g,function(mm,i){return codigos[i]!=null?'`'+codigos[i]+'`':mm});
    return guardar('<img src="'+safeUrl(src)+'" alt="'+alt+'">');
  });
  x=x.replace(/\[([^\]]+)\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g,function(m,txt,href){
    return guardar('<a href="'+safeUrl(href)+'" target="_blank" rel="noopener">'+inlineEmphasis(txt)+'</a>');
  });
  x=inlineEmphasis(x);
  /* marcadores aninhados (código dentro do rótulo de um link) voltam em mais de uma passada */
  for(var n=0;n<4&&/\u0002\d+\u0002/.test(x);n++)x=x.replace(/\u0002(\d+)\u0002/g,function(m,i){return guarda[+i]});
  return x;
}
function splitFrontmatter(md){
  const src=String(md||"").replace(/\r\n?/g,"\n");
  if(!src.startsWith("---\n"))return {raw:"",body:src,fields:[]};
  const end=src.indexOf("\n---",4);
  if(end<0)return {raw:"",body:src,fields:[]};
  const raw=src.slice(0,end+4);
  const body=src.slice(end+4).replace(/^\n+/,"");
  const lines=raw.split("\n").slice(1,-1);
  const fields=[];let current=null;
  for(const line of lines){
    const m=line.match(/^([A-Za-z0-9_-]+):\s*(.*)$/);
    if(m){current={key:m[1],value:m[2]||""};fields.push(current);continue}
    const li=line.match(/^\s*-\s*(.+)$/);
    if(li&&current)current.value+=(current.value?", ":"")+li[1];
  }
  return {raw,body,fields};
}
function renderFrontmatter(fields){
  if(!fields.length)return "";
  const chipKeys=new Set(["tags","tag","aliases","alias"]);
  const valueHTML=f=>{
    const raw=String(f.value||"").trim();
    if(!chipKeys.has(String(f.key).toLowerCase()))return inlineMarkdown(raw||"—");
    const clean=raw.replace(/^\[|\]$/g,"");
    const values=clean.split(",").map(x=>x.trim().replace(/^['"]|['"]$/g,"")).filter(Boolean);
    if(!values.length)return "—";
    return values.map(v=>`<span class="frontmatterChip">${escapeHTML(v)}</span>`).join("");
  };
  return `<section class="frontmatterCard" contenteditable="false" data-frontmatter-card="1">${fields.map(f=>`<div class="frontmatterRow"><span class="frontmatterKey">${escapeHTML(f.key)}</span><span class="frontmatterValue">${valueHTML(f)}</span></div>`).join("")}</section>`;
}
/* Markdown → HTML do editor Visual. Tudo o que é desenhado aqui volta igual em
   markdownFromVisual: tabelas (com alinhamento), citações de várias linhas, callouts
   (> [!tipo] Título), listas aninhadas, tarefas, código com linguagem e links com rótulo. */
const MD_CALLOUTS={note:"Nota",info:"Informação",tip:"Dica",success:"Pronto",question:"Pergunta",warning:"Atenção",danger:"Perigo",bug:"Erro",example:"Exemplo",quote:"Citação",abstract:"Resumo",todo:"A fazer"};
function mdTableCells(line){
  let t=line.trim();if(t.startsWith("|"))t=t.slice(1);if(t.endsWith("|")&&!t.endsWith("\\|"))t=t.slice(0,-1);
  const cells=[];let cur="";for(let i=0;i<t.length;i++){if(t[i]==="\\"&&t[i+1]==="|"){cur+="|";i++;continue}if(t[i]==="|"){cells.push(cur.trim());cur="";continue}cur+=t[i]}cells.push(cur.trim());return cells;
}
function mdIsTableSep(line){return /^\s*\|?\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)*\|?\s*$/.test(line)&&line.includes("-")}
function renderBlocks(md){
  const fm=splitFrontmatter(md);
  const lines=fm.body.replace(/\r\n?/g,"\n").split("\n");
  let out=renderFrontmatter(fm.fields), inCode=false, code=[], lang="", stack=[];

  function closeLevel(){const t=stack.pop();out+=`</li></${t.type}>`}
  function closeList(){while(stack.length)closeLevel()}
  function item(type,indent,html){
    while(stack.length&&indent<stack[stack.length-1].indent)closeLevel();
    if(stack.length&&indent===stack[stack.length-1].indent&&stack[stack.length-1].type!==type)closeLevel();
    if(!stack.length||indent>stack[stack.length-1].indent){out+=`<${type}>`;stack.push({type,indent})}
    else out+="</li>";
    out+=html;
  }
  function flushCode(){
    if(inCode){
      out+=`<pre${lang?` data-lang="${escapeHTML(lang)}"`:""}><code>${escapeHTML(code.join("\n"))}</code></pre>`;
      code=[];inCode=false;lang="";
    }
  }
  function indentOf(l){return l.match(/^[ \t]*/)[0].replace(/\t/g,"    ").length}

  for(let i=0;i<lines.length;i++){
    const line=lines[i];
    let m;

    if((m=line.match(/^```\s*([\w+#.-]*)\s*$/))||/^```/.test(line)){
      if(inCode) flushCode();
      else {closeList();inCode=true;code=[];lang=m?m[1]:""}
      continue;
    }
    if(inCode){code.push(line);continue}

    if(!line.trim()){closeList();continue}

    if((m=line.match(/^(#{1,6})\s+(.+)$/))){
      closeList();
      const n=m[1].length;
      out+=`<h${n}>${inlineMarkdown(m[2])}</h${n}>`;
      continue;
    }
    if(/^ {0,3}([-*_])(?:\s*\1){2,}\s*$/.test(line)){
      closeList();out+="<hr>";continue;
    }
    /* tabela: linha com | seguida da linha separadora */
    if(line.includes("|")&&i+1<lines.length&&mdIsTableSep(lines[i+1])){
      closeList();
      const head=mdTableCells(line),aligns=mdTableCells(lines[i+1]).map(c=>/^:-+:$/.test(c)?"center":/-:$/.test(c)?"right":/^:-/.test(c)?"left":"");
      const td=(tag,c,k)=>`<${tag}${aligns[k]?` style="text-align:${aligns[k]}"`:""}>${inlineMarkdown(c)||"<br>"}</${tag}>`;
      let html=`<table data-md-table="1" data-align="${aligns.join(",")}"><thead><tr>${head.map((c,k)=>td("th",c,k)).join("")}</tr></thead><tbody>`;
      i+=2;
      while(i<lines.length&&lines[i].trim()&&lines[i].includes("|")){const cells=mdTableCells(lines[i]);html+=`<tr>${head.map((_,k)=>td("td",cells[k]||"",k)).join("")}</tr>`;i++}
      i--;out+=html+"</tbody></table>";
      continue;
    }
    /* citação: linhas seguidas com > formam um bloco só; > [!tipo] Título vira um callout */
    if(/^>\s?/.test(line)){
      closeList();
      const grupo=[];while(i<lines.length&&/^>\s?/.test(lines[i])){grupo.push(lines[i].replace(/^>\s?/,""));i++}i--;
      const c=grupo[0].match(/^\[!(\w+)\][+-]?\s*(.*)$/);
      if(c){
        const tipo=c[1].toLowerCase(),titulo=c[2]||"";
        out+=`<div class="callout callout-${escapeHTML(tipo)}" data-callout="${escapeHTML(tipo)}"><div class="callout-title" data-default="${escapeHTML(MD_CALLOUTS[tipo]||tipo)}">${inlineMarkdown(titulo)||"<br>"}</div><div class="callout-body">${grupo.slice(1).map(inlineMarkdown).join("<br>")||"<br>"}</div></div>`;
      }else out+=`<blockquote>${grupo.map(inlineMarkdown).join("<br>")}</blockquote>`;
      continue;
    }
    if((m=line.match(/^([ \t]*)[-*+]\s+\[([ xX])\]\s+(.*)$/))){
      const checked=m[2].toLowerCase()==="x"?" checked":"";
      item("ul",indentOf(line),`<li class="task"><input type="checkbox"${checked}>${inlineMarkdown(m[3])}`);
      continue;
    }
    if((m=line.match(/^([ \t]*)[-*+]\s+(.+)$/))){
      item("ul",indentOf(line),`<li>${inlineMarkdown(m[2])}`);
      continue;
    }
    if((m=line.match(/^([ \t]*)(\d+)[.)]\s+(.+)$/))){
      item("ol",indentOf(line),`<li>${inlineMarkdown(m[3])}`);
      continue;
    }

    closeList();
    out+=`<p>${inlineMarkdown(line)}</p>`;
  }
  closeList();
  flushCode();
  return out||'<p><br></p>';
}

/* ---------- cabeçalho vazio ("# ") ----------
   O render de blocos exigia conteúdo depois do #, então o template padrão virava <p># </p> e o primeiro texto digitado
   saía como "#Texto". Agora "# " vira um <h1> editável e o caminho de volta preserva o "# ". */
function renderHeadings(md){
  var linhas=String(md==null?'':md).replace(/\r\n?/g,'\n').split('\n'),emCodigo=false;
  for(var i=0;i<linhas.length;i++){
    if(/^```/.test(linhas[i])){emCodigo=!emCodigo;continue}
    if(emCodigo)continue;
    var m=linhas[i].match(/^(#{1,6})[ \t]*$/);
    if(m)linhas[i]=m[1]+' \u0001';
  }
  return renderBlocks(linhas.join('\n')).replace(/\u0001/g,'<br>');
}

/* ---------- matemática (src/math) ----------
   As fórmulas saem do Markdown antes da renderização e voltam como blocos atômicos. */
function render(md){var M=global.UrbeMath;return M?M.renderWith(renderHeadings,md,{editable:true}):renderHeadings(md)}

global.UrbeMarkdown={render:render,escapeHTML:escapeHTML,inlineMarkdown:inlineMarkdown,splitFrontmatter:splitFrontmatter,renderFrontmatter:renderFrontmatter,safeUrl:safeUrl,callouts:MD_CALLOUTS};
if(global.UrbeCore&&global.UrbeCore.provide)global.UrbeCore.provide('editor.markdown',global.UrbeMarkdown);
})(window);
