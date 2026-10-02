(function(global){
'use strict';
/* Editor Visual → Markdown (RM-F2-10, parte pura; REQ-027). Serializa o DOM do contenteditable (#renderedPreview) de volta para
   Markdown; é o inverso de src/editor/markdown.js. Sem estado: o texto do frontmatter (que o editor Visual não mostra) entra por
   `opts.frontmatter`, quem chama é que sabe de onde vem. O estado e os eventos do editor (setEditorViewMode, sincronização,
   seleção) continuam em src/app.js até o restante de RM-F2-10.
   Saiu do app.js sem mudar o resultado (golden em tests/fixtures/visual-golden.json, gerado em Chromium com o código antigo).
   Camadas, na mesma ordem que o app.js empilhava: núcleo → normalização (U+200B e "#" vazio) → matemática (UrbeMathEditor). */
function toMarkdown(root,opts){
  const out=[];

  function textInline(node){
    if(node.nodeType===Node.TEXT_NODE)return node.nodeValue;
    if(node.nodeType!==Node.ELEMENT_NODE)return "";
    const el=node, tag=el.tagName.toLowerCase();
    const inner=[...el.childNodes].map(textInline).join("");

    if(tag==="strong"||tag==="b")return `**${inner}**`;
    if(tag==="em"||tag==="i")return `_${inner}_`;
    if(tag==="del"||tag==="s")return `~~${inner}~~`;
    if(tag==="code" && el.parentElement?.tagName.toLowerCase()!=="pre")return `\`${inner}\``;
    if(tag==="a")return `[${inner}](${el.getAttribute("href")||""})`;
    if(tag==="img")return `![${el.getAttribute("alt")||""}](${el.getAttribute("src")||""})`;
    if(el.classList.contains("wikilink")){
      const alvo=el.dataset.noteName||inner,rotulo=inner.trim();
      return rotulo&&rotulo!==alvo?`[[${alvo}|${rotulo}]]`:`[[${alvo}]]`;
    }
    if(tag==="ul"||tag==="ol"||tag==="input")return "";
    if(tag==="br")return "\n";
    return inner;
  }

  function block(el){
    if(el.nodeType===Node.TEXT_NODE){
      const t=el.nodeValue.trim();
      if(t)out.push(t);
      return;
    }
    if(el.nodeType!==Node.ELEMENT_NODE)return;
    const tag=el.tagName.toLowerCase();
    if(el.hasAttribute("data-frontmatter-card"))return;
    if(el.dataset&&el.dataset.callout){
      const titulo=el.querySelector(".callout-title"),corpo=el.querySelector(".callout-body");
      const t=titulo?textInline(titulo).replace(/\n/g," ").trim():"",b=corpo?textInline(corpo).replace(/\n+$/,""):"";
      out.push(["> [!"+el.dataset.callout+"]"+(t?" "+t:"")].concat(b.trim()?b.split("\n").map(x=>x?"> "+x:">"):[]).join("\n"));
      return;
    }

    if(/^h[1-6]$/.test(tag)){
      out.push("#".repeat(Number(tag[1]))+" "+textInline(el).trim());
      return;
    }
    // O navegador cria <div> ao sair de uma lista; se ele contém blocos, serializa cada um.
    if(tag==="div"&&[...el.children].some(c=>/^(P|DIV|H[1-6]|UL|OL|BLOCKQUOTE|PRE|HR|TABLE)$/.test(c.tagName))){
      [...el.childNodes].forEach(block);
      return;
    }
    if(tag==="p"||tag==="div"){
      const t=textInline(el).trim();
      if(t)out.push(t);
      return;
    }
    if(tag==="blockquote"){
      const linhas=[...el.childNodes].map(n=>/^(P|DIV)$/.test(n.nodeName)?textInline(n)+"\n":textInline(n)).join("").replace(/\n+$/,"").split("\n");
      out.push(linhas.map(x=>x?"> "+x:">").join("\n"));
      return;
    }
    if(tag==="table"){
      const rows=[...el.querySelectorAll("tr")];if(!rows.length)return;
      const cel=c=>textInline(c).replace(/\n/g," ").replace(/\|/g,"\\|").trim();
      const head=[...rows[0].children].map(cel),al=(el.dataset.align||"").split(",");
      const sep=head.map((_,k)=>{const a=al[k]||(rows[0].children[k]&&rows[0].children[k].style.textAlign)||"";return a==="center"?":---:":a==="right"?"---:":a==="left"?":---":"---"});
      const linhas=["| "+head.join(" | ")+" |","| "+sep.join(" | ")+" |"];
      rows.slice(1).forEach(r=>{const cs=[...r.children].map(cel);while(cs.length<head.length)cs.push("");linhas.push("| "+cs.join(" | ")+" |")});
      out.push(linhas.join("\n"));
      return;
    }
    if(tag==="pre"){
      out.push("```"+(el.dataset.lang||"")+"\n"+el.innerText.replace(/\n+$/,"")+"\n```");
      return;
    }
    if(tag==="hr"){
      out.push("---");
      return;
    }
    if(tag==="ul"||tag==="ol"){
      /* itens de uma lista ficam em linhas seguidas, sem linha em branco entre eles;
         sublistas descem até o início do texto do item de cima */
      const itens=[];
      (function lista(ul,recuo){
        const ord=ul.tagName.toLowerCase()==="ol";let n=0;
        [...ul.children].forEach(li=>{
          if(li.tagName.toLowerCase()!=="li")return;n++;
          const checkbox=[...li.children].find(c=>c.tagName==="INPUT"&&c.type==="checkbox");
          const content=textInline(li).replace(/\n+/g," ").trim();
          const marca=checkbox?`- [${checkbox.checked?"x":" "}] `:ord?n+". ":"- ";
          itens.push(recuo+marca+content);
          [...li.children].filter(c=>/^(UL|OL)$/.test(c.tagName)).forEach(sub=>lista(sub,recuo+" ".repeat(ord?String(n).length+2:2)));
        });
      })(el,"");
      if(itens.length)out.push(itens.join("\n"));
      return;
    }

    [...el.childNodes].forEach(block);
  }

  [...root.childNodes].forEach(block);
  const body=out.join("\n\n").replace(/\n{3,}/g,"\n\n").trim();
  const fm=(opts&&opts.frontmatter)||"";
  return (fm?fm+(body?"\n":""):"")+(body?body+"\n":"");
}

/* normalização do que o contenteditable deixa para trás: espaço de largura zero e cabeçalho vazio ("# " preserva o espaço) */
function normalize(md){return md.replace(/\u200b/g,'').replace(/^(#{1,6})[ \t]*$/gm,'$1 ')}

/* matemática (src/math): cada fórmula volta com os delimitadores originais */
function markdownFromVisual(root,opts){
  var base=function(r){return normalize(toMarkdown(r,opts))},E=global.UrbeMathEditor;
  return E&&E.serialize?E.serialize(base,root):base(root);
}

global.UrbeVisual={markdownFromVisual:markdownFromVisual};
if(global.UrbeCore&&global.UrbeCore.provide)global.UrbeCore.provide('editor.visual',global.UrbeVisual);
})(window);
