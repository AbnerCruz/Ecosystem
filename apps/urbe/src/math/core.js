(function(global){
  'use strict';
  /* Matemática no Markdown do Urbe (sem formato próprio: é .md comum).
     - Delimitadores: $…$ e \(…\) (no texto), $$…$$ e \[…\] (em destaque).
     - Nada dentro de código (``` ou `…`) é tocado; \$ é um cifrão literal.
     - Cada fórmula guarda os delimitadores originais, então o Markdown volta
       exatamente como estava depois de passar pelo editor visual.
     Renderização com KaTeX (vendor/katex), que funciona offline. */
  var OPEN_I='\uE000',CLOSE_I='\uE001',OPEN_B='\uE002',CLOSE_B='\uE003';
  function esc(v){return String(v==null?'':v).replace(/[&<>"']/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]})}

  /* ---------------- varredura ---------------- */
  /* devolve a lista de fórmulas com posição, delimitadores e se ocupa a linha inteira */
  function scan(md){
    var s=String(md==null?'':md),out=[],i=0,n=s.length,lineStart=0,inFence=null;
    function atLineStart(k){var j=k-1;while(j>=0&&(s[j]===' '||s[j]==='\t'))j--;return j<0||s[j]==='\n'}
    function atLineEnd(k){var j=k;while(j<n&&(s[j]===' '||s[j]==='\t'))j++;return j>=n||s[j]==='\n'}
    while(i<n){
      /* cercas de código */
      if(i===lineStart){var m=/^ {0,3}(`{3,}|~{3,})/.exec(s.slice(i,i+40));
        if(m){var f=m[1][0];if(!inFence)inFence=f.repeat(m[1].length);else if(m[1].indexOf(inFence)===0)inFence=null;var nl=s.indexOf('\n',i);i=nl<0?n:nl+1;lineStart=i;continue}}
      if(inFence){var nl2=s.indexOf('\n',i);i=nl2<0?n:nl2+1;lineStart=i;continue}
      var c=s[i];
      if(c==='\n'){i++;lineStart=i;continue}
      if(c==='`'){var run=/^`+/.exec(s.slice(i))[0],close=s.indexOf(run,i+run.length);if(close<0){i+=run.length;continue}i=close+run.length;continue}
      if(c==='\\'){
        var nx=s[i+1];
        if(nx==='$'){i+=2;continue}
        if(nx==='('||nx==='['){var endTok=nx==='('?'\\)':'\\]',e=s.indexOf(endTok,i+2);if(e>i+2){var tex=s.slice(i+2,e);if(nx==='('&&tex.indexOf('\n')>=0){i+=2;continue}
          var item={start:i,end:e+2,tex:tex,open:'\\'+nx,close:endTok,display:nx==='['};item.block=item.display&&atLineStart(i)&&atLineEnd(e+2);out.push(item);i=e+2;continue}}
        i+=2;continue;
      }
      if(c==='$'){
        if(s[i+1]==='$'){var e2=s.indexOf('$$',i+2);while(e2>0&&s[e2-1]==='\\')e2=s.indexOf('$$',e2+2);
          if(e2>i+2){var body=s.slice(i+2,e2);if(!/\n[ \t]*\n/.test(body)){var it={start:i,end:e2+2,tex:body,open:'$$',close:'$$',display:true};
            var lead=body.match(/^[ \t]*\n/),trail=body.match(/\n[ \t]*$/);if(lead){it.open='$$'+lead[0];it.tex=body.slice(lead[0].length)}if(trail){it.close=trail[0]+'$$';it.tex=it.tex.slice(0,it.tex.length-trail[0].length)}
            it.block=atLineStart(i)&&atLineEnd(e2+2);out.push(it);i=e2+2;continue}}
          i+=2;continue}
        var nxc=s[i+1];if(!nxc||/\s/.test(nxc)){i++;continue}
        var j=i+1,found=-1;while(j<n&&s[j]!=='\n'){if(s[j]==='\\'){j+=2;continue}if(s[j]==='$'){if(!/\s/.test(s[j-1])&&!/\d/.test(s[j+1]||'')){found=j;break}}j++}
        if(found>i+1){out.push({start:i,end:found+1,tex:s.slice(i+1,found),open:'$',close:'$',display:false,block:false});i=found+1;continue}
        i++;continue;
      }
      i++;
    }
    return out;
  }
  /* troca as fórmulas por marcadores que sobrevivem ao renderizador de Markdown */
  function extract(md){var s=String(md==null?'':md),items=scan(s),out='',last=0;
    items.forEach(function(it,k){out+=s.slice(last,it.start)+(it.block?OPEN_B+k+CLOSE_B:OPEN_I+k+CLOSE_I);last=it.end});
    return{text:out+s.slice(last),items:items}}
  function restore(html,items,o){o=o||{};
    html=html.replace(new RegExp('<p>\\s*'+OPEN_B+'(\\d+)'+CLOSE_B+'\\s*</p>','g'),function(_,k){return atom(items[+k],true,o)});
    html=html.replace(new RegExp(OPEN_B+'(\\d+)'+CLOSE_B,'g'),function(_,k){return atom(items[+k],false,o)});
    return html.replace(new RegExp(OPEN_I+'(\\d+)'+CLOSE_I,'g'),function(_,k){return atom(items[+k],false,o)})}
  function atom(it,block,o){if(!it)return '';var attrs=' data-tex="'+esc(it.tex)+'" data-open="'+esc(it.open)+'" data-close="'+esc(it.close)+'"'+(o.editable?' contenteditable="false"':'');
    return block?'<div class="umath umath-block"'+attrs+'>'+render(it.tex,true)+'</div>':'<span class="umath'+(it.display?' umath-display':'')+'"'+attrs+'>'+render(it.tex,it.display)+'</span>'}
  /* renderizar um Markdown qualquer passando pela função de Markdown do chamador */
  function renderWith(mdRender,md,o){var x=extract(md);if(!x.items.length)return mdRender(md);return restore(mdRender(x.text),x.items,o)}
  function findAt(md,index){var list=scan(md);for(var k=0;k<list.length;k++){var it=list[k],a=it.start+it.open.length,b=it.end-it.close.length;if(index>=a&&index<=b)return it}return null}
  function rawOf(el){return(el.getAttribute('data-open')||'$')+(el.getAttribute('data-tex')||'')+(el.getAttribute('data-close')||'$')}

  /* ---------------- KaTeX ---------------- */
  var cache=new Map(),macros={};
  function opts(display,strict){return{displayMode:!!display,throwOnError:!!strict,errorColor:'#ff7b86',strict:'ignore',trust:false,output:'htmlAndMathml',macros:Object.assign({},macros)}}
  function render(tex,display){var k=(display?'D':'I')+tex;if(cache.has(k))return cache.get(k);var K=global.katex,h;
    if(!K)h='<code class="umath-raw">'+esc(tex)+'</code>';
    else{try{h=K.renderToString(String(tex),opts(display,false))}catch(e){h='<span class="umath-err" title="'+esc(e.message)+'">'+esc(tex)+'</span>'}}
    if(cache.size>600)cache.clear();cache.set(k,h);return h}
  function error(tex,display){var K=global.katex;if(!K)return null;try{K.renderToString(String(tex),opts(display,true));return null}catch(e){return String(e.message||e).replace(/^KaTeX parse error:\s*/,'')}}
  function setMacros(m){macros=m||{};cache.clear()}

  /* ---------------- catálogo: comandos (autocompletar), símbolos e modelos ----------------
     ● marca onde o cursor fica depois de inserir. */
  function C(cmd,snip,desc,prev){return{cmd:cmd,snip:snip||cmd,desc:desc||'',prev:prev||null}}
  var G=[['alpha','α'],['beta','β'],['gamma','γ'],['delta','δ'],['epsilon','ϵ'],['varepsilon','ε'],['zeta','ζ'],['eta','η'],['theta','θ'],['vartheta','ϑ'],['iota','ι'],['kappa','κ'],['lambda','λ'],['mu','μ'],['nu','ν'],['xi','ξ'],['pi','π'],['rho','ρ'],['sigma','σ'],['tau','τ'],['upsilon','υ'],['phi','ϕ'],['varphi','φ'],['chi','χ'],['psi','ψ'],['omega','ω'],['Gamma','Γ'],['Delta','Δ'],['Theta','Θ'],['Lambda','Λ'],['Xi','Ξ'],['Pi','Π'],['Sigma','Σ'],['Phi','Φ'],['Psi','Ψ'],['Omega','Ω']];
  var COMMANDS=G.map(function(g){return C('\\'+g[0],null,'letra grega '+g[1])}).concat([
    C('\\frac','\\frac{●}{}','fração'),C('\\dfrac','\\dfrac{●}{}','fração grande'),C('\\sqrt','\\sqrt{●}','raiz quadrada'),C('\\sqrt[n]','\\sqrt[●]{}','raiz n-ésima'),C('\\binom','\\binom{●}{}','binomial'),
    C('\\sum','\\sum_{●}^{}','somatório'),C('\\prod','\\prod_{●}^{}','produtório'),C('\\int','\\int_{●}^{}','integral'),C('\\iint','\\iint','integral dupla'),C('\\oint','\\oint','integral de linha'),C('\\lim','\\lim_{● \\to }','limite'),
    C('\\partial','\\partial','derivada parcial'),C('\\nabla','\\nabla','nabla'),C('\\infty','\\infty','infinito'),C('\\mathrm{d}','\\mathrm{d}●','d reto'),
    C('\\cdot','\\cdot','produto (ponto)'),C('\\times','\\times','vezes'),C('\\div','\\div','divisão'),C('\\pm','\\pm','mais ou menos'),C('\\mp','\\mp','menos ou mais'),C('\\circ','\\circ','composição'),
    C('\\leq','\\leq','menor ou igual'),C('\\geq','\\geq','maior ou igual'),C('\\neq','\\neq','diferente'),C('\\approx','\\approx','aproximadamente'),C('\\equiv','\\equiv','equivalente'),C('\\sim','\\sim','semelhante'),C('\\propto','\\propto','proporcional'),C('\\ll','\\ll','muito menor'),C('\\gg','\\gg','muito maior'),
    C('\\in','\\in','pertence'),C('\\notin','\\notin','não pertence'),C('\\subset','\\subset','subconjunto'),C('\\subseteq','\\subseteq','subconjunto ou igual'),C('\\supset','\\supset','contém'),C('\\cup','\\cup','união'),C('\\cap','\\cap','interseção'),C('\\setminus','\\setminus','diferença'),C('\\emptyset','\\emptyset','vazio'),C('\\varnothing','\\varnothing','vazio'),
    C('\\forall','\\forall','para todo'),C('\\exists','\\exists','existe'),C('\\nexists','\\nexists','não existe'),C('\\neg','\\neg','negação'),C('\\land','\\land','e'),C('\\lor','\\lor','ou'),
    C('\\to','\\to','seta'),C('\\rightarrow','\\rightarrow','seta'),C('\\leftarrow','\\leftarrow','seta à esquerda'),C('\\leftrightarrow','\\leftrightarrow','seta dupla'),C('\\mapsto','\\mapsto','leva em'),C('\\Rightarrow','\\Rightarrow','implica'),C('\\implies','\\implies','implica'),C('\\iff','\\iff','se e somente se'),C('\\Leftrightarrow','\\Leftrightarrow','equivalência'),
    C('\\mathbb','\\mathbb{●}','conjuntos (ℝ, ℕ…)'),C('\\mathbb{R}','\\mathbb{R}','reais ℝ'),C('\\mathbb{N}','\\mathbb{N}','naturais ℕ'),C('\\mathbb{Z}','\\mathbb{Z}','inteiros ℤ'),C('\\mathbb{Q}','\\mathbb{Q}','racionais ℚ'),C('\\mathbb{C}','\\mathbb{C}','complexos ℂ'),
    C('\\mathcal','\\mathcal{●}','caligráfico'),C('\\mathrm','\\mathrm{●}','texto reto'),C('\\mathbf','\\mathbf{●}','negrito'),C('\\boldsymbol','\\boldsymbol{●}','símbolo em negrito'),C('\\text','\\text{●}','texto normal'),C('\\operatorname','\\operatorname{●}','operador'),
    C('\\hat','\\hat{●}','chapéu'),C('\\bar','\\bar{●}','barra'),C('\\overline','\\overline{●}','barra longa'),C('\\vec','\\vec{●}','vetor'),C('\\dot','\\dot{●}','ponto'),C('\\ddot','\\ddot{●}','dois pontos'),C('\\tilde','\\tilde{●}','til'),C('\\underbrace','\\underbrace{●}_{}','chave embaixo'),
    C('\\left(','\\left( ● \\right)','parênteses que crescem'),C('\\left[','\\left[ ● \\right]','colchetes que crescem'),C('\\left\\{','\\left\\{ ● \\right\\}','chaves que crescem'),C('\\left|','\\left| ● \\right|','módulo que cresce'),C('\\langle','\\langle ● \\rangle','ângulos'),C('\\lVert','\\lVert ● \\rVert','norma'),
    C('\\ldots','\\ldots','reticências'),C('\\cdots','\\cdots','reticências centrais'),C('\\vdots','\\vdots','reticências verticais'),C('\\ddots','\\ddots','reticências diagonais'),C('\\quad','\\quad','espaço'),C('\\,','\\,','espaço fino'),
    C('\\sin','\\sin','seno'),C('\\cos','\\cos','cosseno'),C('\\tan','\\tan','tangente'),C('\\log','\\log','logaritmo'),C('\\ln','\\ln','logaritmo natural'),C('\\exp','\\exp','exponencial'),C('\\max','\\max','máximo'),C('\\min','\\min','mínimo'),C('\\det','\\det','determinante'),C('\\gcd','\\gcd','mdc'),
    C('\\begin{pmatrix}','\\begin{pmatrix} ● & \\\\  &  \\end{pmatrix}','matriz ( )'),C('\\begin{bmatrix}','\\begin{bmatrix} ● & \\\\  &  \\end{bmatrix}','matriz [ ]'),C('\\begin{vmatrix}','\\begin{vmatrix} ● & \\\\  &  \\end{vmatrix}','determinante'),
    C('\\begin{cases}','\\begin{cases} ● & \\text{se }  \\\\  & \\text{se }  \\end{cases}','função por partes'),C('\\begin{aligned}','\\begin{aligned} ● &=  \\\\ &=  \\end{aligned}','equações alinhadas'),
    C('\\overset','\\overset{●}{}','símbolo acima'),C('\\underset','\\underset{●}{}','símbolo abaixo'),C('\\boxed','\\boxed{●}','resposta em caixa'),C('\\cancel','\\cancel{●}','cancelar'),C('\\therefore','\\therefore','portanto'),C('\\because','\\because','porque'),C('\\angle','\\angle','ângulo'),C('\\perp','\\perp','perpendicular'),C('\\parallel','\\parallel','paralelo'),C('\\degree','^\\circ','grau')
  ]);
  function S(label,tex){return{label:label,tex:tex}}
  var SYMBOLS=[
    {name:'Básico',items:[S('x²','^{●}'),S('xₙ','_{●}'),S('a⁄b','\\frac{●}{}'),S('√','\\sqrt{●}'),S('( )','\\left( ● \\right)'),S('=','='),S('≠','\\neq '),S('≤','\\leq '),S('≥','\\geq '),S('≈','\\approx '),S('±','\\pm '),S('·','\\cdot '),S('×','\\times '),S('÷','\\div '),S('∞','\\infty '),S('|x|','\\left| ● \\right|'),S('{ }','\\{ ● \\}'),S('…','\\ldots ')]},
    {name:'Grego',items:[S('α','\\alpha '),S('β','\\beta '),S('γ','\\gamma '),S('δ','\\delta '),S('ε','\\varepsilon '),S('θ','\\theta '),S('λ','\\lambda '),S('μ','\\mu '),S('π','\\pi '),S('ρ','\\rho '),S('σ','\\sigma '),S('τ','\\tau '),S('φ','\\varphi '),S('ω','\\omega '),S('Δ','\\Delta '),S('Σ','\\Sigma '),S('Ω','\\Omega '),S('Φ','\\Phi ')]},
    {name:'Cálculo',items:[S('∫','\\int '),S('∫ₐᵇ','\\int_{●}^{} '),S('∑','\\sum_{●}^{} '),S('∏','\\prod_{●}^{} '),S('lim','\\lim_{● \\to } '),S('∂','\\partial '),S('∇','\\nabla '),S('d/dx','\\frac{d}{dx}'),S('dx','\\,dx'),S('→','\\to '),S('′','\'')]},
    {name:'Conjuntos',items:[S('∈','\\in '),S('∉','\\notin '),S('⊂','\\subset '),S('⊆','\\subseteq '),S('∪','\\cup '),S('∩','\\cap '),S('∅','\\emptyset '),S('ℕ','\\mathbb{N}'),S('ℤ','\\mathbb{Z}'),S('ℚ','\\mathbb{Q}'),S('ℝ','\\mathbb{R}'),S('ℂ','\\mathbb{C}'),S('∀','\\forall '),S('∃','\\exists '),S('¬','\\neg '),S('∧','\\land '),S('∨','\\lor ')]},
    {name:'Setas',items:[S('→','\\to '),S('←','\\leftarrow '),S('↔','\\leftrightarrow '),S('↦','\\mapsto '),S('⇒','\\Rightarrow '),S('⇐','\\Leftarrow '),S('⇔','\\iff '),S('↑','\\uparrow '),S('↓','\\downarrow ')]}
  ];
  var TEMPLATES=[
    {label:'Fração',tex:'\\frac{●}{}'},{label:'Raiz n-ésima',tex:'\\sqrt[●]{}'},{label:'Potência',tex:'{●}^{}'},{label:'Integral definida',tex:'\\int_{●}^{} f(x)\\,dx'},
    {label:'Somatório',tex:'\\sum_{i=●}^{n} '},{label:'Limite',tex:'\\lim_{x \\to ●} '},{label:'Derivada',tex:'\\frac{d●}{dx}'},{label:'Derivada parcial',tex:'\\frac{\\partial ●}{\\partial x}'},
    {label:'Matriz 2×2',tex:'\\begin{pmatrix} ● & b \\\\ c & d \\end{pmatrix}'},{label:'Matriz 3×3',tex:'\\begin{bmatrix} ● & & \\\\ & & \\\\ & & \\end{bmatrix}'},{label:'Determinante',tex:'\\begin{vmatrix} ● & b \\\\ c & d \\end{vmatrix}'},
    {label:'Sistema',tex:'\\begin{cases} ● \\\\ \\end{cases}'},{label:'Por partes',tex:'f(x)=\\begin{cases} ● & \\text{se } x<0 \\\\ & \\text{se } x\\ge 0 \\end{cases}'},{label:'Alinhado',tex:'\\begin{aligned} ● &= \\\\ &= \\end{aligned}'},
    {label:'Binomial',tex:'\\binom{●}{k}'},{label:'Vetor',tex:'\\vec{●}'},{label:'Norma',tex:'\\lVert ● \\rVert'},{label:'Resposta',tex:'\\boxed{●}'}
  ];
  /* ● → posição do cursor */
  function snippet(s){var t=String(s),k=t.indexOf('●');return{text:t.replace(/●/g,''),caret:k<0?t.length:k}}
  function complete(prefix,limit){var p=String(prefix||'');if(!/^\\[A-Za-z]*$/.test(p))return[];var lo=p.toLowerCase(),seen={},a=[],b=[];
    COMMANDS.forEach(function(c){if(seen[c.cmd])return;var cl=c.cmd.toLowerCase();if(cl.indexOf(lo)===0){seen[c.cmd]=1;(c.cmd.indexOf(p)===0?a:b).push(c)}});
    return a.concat(b).sort(function(x,y){return(x.cmd.indexOf(p)===0?0:1)-(y.cmd.indexOf(p)===0?0:1)||x.cmd.length-y.cmd.length}).slice(0,limit||8)}

  function atomHtml(tex,block,o){var it=block?{tex:tex,open:'$$\n',close:'\n$$',display:true}:{tex:tex,open:'$',close:'$',display:false};return atom(it,!!block,o||{editable:true})}
  global.UrbeMath={scan:scan,atomHtml:atomHtml,extract:extract,restore:restore,renderWith:renderWith,findAt:findAt,rawOf:rawOf,render:render,error:error,setMacros:setMacros,
    COMMANDS:COMMANDS,SYMBOLS:SYMBOLS,TEMPLATES:TEMPLATES,snippet:snippet,complete:complete,esc:esc,ready:function(){return !!global.katex}};
})(typeof window!=='undefined'?window:globalThis);
