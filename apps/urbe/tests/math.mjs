import fs from 'node:fs';import vm from 'node:vm';
const read=f=>fs.readFileSync(new URL('../'+f,import.meta.url),'utf8');
let failed=0;async function test(name,fn){try{await fn();console.log('OK  ',name)}catch(e){failed++;console.error('FAIL',name,e&&e.stack||e);process.exitCode=1}}
function ok(v,m){if(!v)throw new Error(m||'falhou')}
function eq(a,b,m){if(JSON.stringify(a)!==JSON.stringify(b))throw new Error((m||'')+'\n esperado '+JSON.stringify(b)+'\n obtido  '+JSON.stringify(a))}
const c={window:{},console};c.globalThis=c.window;vm.createContext(c);
vm.runInContext(read('vendor/katex/katex.min.js'),c);c.window.katex=c.katex||c.window.katex;
vm.runInContext(read('src/math/core.js'),c);
const M=c.window.UrbeMath;

await test('varredura: delimitadores, código ignorado, cifrão de preço e escapado',()=>{
  const md='Seja $x^2$ e \\(y\\) mas R$ 10 e R$ 20, `$a$` e \\$5.\n\n```\n$$nao$$\n```\n\n$$\n\\int_0^1 f\n$$\n\nTexto \\[a=b\\] fim. E $5 e $6.';
  const s=M.scan(md).map(x=>[x.open,x.tex,x.close,!!x.display,!!x.block]);
  eq(s,[['$','x^2','$',false,false],['\\(','y','\\)',false,false],['$$\n','\\int_0^1 f','\n$$',true,true],['\\[','a=b','\\]',true,false]]);
});
await test('extrair e restaurar: blocos viram div, no texto viram span, com delimitadores originais',()=>{
  const md='# Área $A=\\pi r^2$\n\n$$\nE=mc^2\n$$';const x=M.extract(md);
  ok(!x.text.includes('\\pi')&&x.items.length===2,'marcadores');
  const html=M.restore('<h1>'+x.text.split('\n')[0].slice(2)+'</h1><p>'+x.text.split('\n\n')[1]+'</p>',x.items,{editable:true});
  ok(/<div class="umath umath-block" data-tex="E=mc\^2" data-open="\$\$\n" data-close="\n\$\$" contenteditable="false">/.test(html),html.slice(0,300));
  ok(/<span class="umath" data-tex="A=\\pi r\^2"/.test(html)&&html.includes('class="katex"'),'inline com KaTeX');
});
await test('renderWith com um Markdown simples preserva o resto e não deixa marcadores',()=>{
  const md2html=s=>s.split('\n\n').map(p=>'<p>'+p.replace(/\*\*(.+?)\*\*/g,'<strong>$1</strong>')+'</p>').join('');
  const h=M.renderWith(md2html,'**negrito** $a*b*c$ fim');ok(h.includes('<strong>negrito</strong>')&&!/[\uE000-\uE003]/.test(h)&&!h.includes('<em>'),h);
});
await test('findAt acha a fórmula sob o cursor',()=>{const md='a $x+1$ b $$y$$';ok(M.findAt(md,4).tex==='x+1');ok(M.findAt(md,1)===null);ok(M.findAt(md,12).tex==='y')});
await test('erros de LaTeX: mensagem legível e render não quebra',()=>{ok(/Expected|Undefined|\\frac/.test(M.error('\\frac{1}{',false)||''),M.error('\\frac{1}{',false));ok(M.error('x^2',false)===null);ok(M.render('\\frac{1}{',false).length>0)});
await test('autocompletar e trechos',()=>{const r=M.complete('\\fr');ok(r[0].cmd==='\\frac',JSON.stringify(r.map(x=>x.cmd)));ok(M.complete('\\Del')[0].cmd==='\\Delta');const s=M.snippet('\\frac{●}{}');eq([s.text,s.caret],['\\frac{}{}',6])});
await test('catálogo: todos os símbolos, comandos e modelos são LaTeX válido',()=>{
  const bad=[];const fill=t=>M.snippet(t).text.replace(/\{\}/g,'{x}').replace(/_\{\s*\}/g,'_{x}').replace(/\^\{\s*\}/g,'^{x}');
  M.SYMBOLS.forEach(g=>g.items.forEach(s=>{const t=/^[\^_]/.test(s.tex)?'a'+fill(s.tex):fill(s.tex);if(M.error(t,false))bad.push(s.label+': '+M.error(t,false))}));
  M.TEMPLATES.forEach(s=>{if(M.error(fill(s.tex),true))bad.push(s.label+': '+M.error(fill(s.tex),true))});
  M.COMMANDS.forEach(s=>{const t=fill(s.snip).replace(/\\to \}/,'\\to 0}');if(M.error(t,true))bad.push(s.cmd+': '+M.error(t,true))});
  eq(bad,[]);
});
if(failed)console.error(failed+' falha(s)');
