(function(global){
  'use strict';
  /* Tela de Personalização: tudo o que o tema.json guarda, com controles visuais e
     prévia ao vivo; pacotes de texturas com editor de pixels; estilos CSS; plugins;
     e o próprio arquivo JSON para quem prefere escrever. Celular: tela cheia com abas
     roláveis no topo. Computador: janela larga com a lista de seções à esquerda. */
  var core=global.UrbeCore,doc=global.document,D=global.UrbeDialogs;if(!core||!doc)return;
  var C=function(){return core.service('customize')},PL=function(){return core.service('plugins')},docs=core.service('documents');
  function ic(n){return global.UrbeIcons?global.UrbeIcons.icon(n):''}
  function esc(v){return String(v==null?'':v).replace(/[&<>"']/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]})}
  function toast(m){var t=doc.getElementById('toast');if(!t)return;t.textContent=m;t.classList.add('show');clearTimeout(toast._t);toast._t=setTimeout(function(){t.classList.remove('show')},2000)}

  var SECOES=[
    {id:'aparencia',nome:'Aparência',icone:'palette'},
    {id:'texto',nome:'Texto',icone:'type'},
    {id:'editor',nome:'Editor',icone:'edit'},
    {id:'cidade',nome:'Cidade',icone:'city'},
    {id:'texturas',nome:'Texturas',icone:'image'},
    {id:'estilos',nome:'Estilos CSS',icone:'brush'},
    {id:'plugins',nome:'Plugins',icone:'puzzle'},
    {id:'arquivo',nome:'Arquivo',icone:'code'}
  ];
  var NOMES_COR={destaque:'Destaque',fundo:'Fundo',superficie:'Painéis',superficie2:'Painéis 2',superficie3:'Botões',linha:'Linhas',linha2:'Bordas',texto:'Texto',texto2:'Texto secundário',texto3:'Texto apagado',perigo:'Perigo',sucesso:'Sucesso',aviso:'Aviso',codigo:'Código'};
  var PRINCIPAIS=['destaque','fundo','superficie','texto','perigo','sucesso'];
  var ACENTOS=['#8fb3ff','#b39cff','#f08aa0','#ff9f6b','#f1c46e','#8fd18a','#5fd0e0','#3b6fe0','#a0582a','#ffd400'];

  var root=null,secao='aparencia',maisCores=false;
  function cfg(){return C().get()}

  /* ---------- peças de interface ---------- */
  function seg(nome,valor,opcoes){return'<div class="uc-seg" role="radiogroup" data-seg="'+nome+'">'+opcoes.map(function(o){return'<button type="button" role="radio" aria-checked="'+(o[0]===valor)+'" class="'+(o[0]===valor?'on':'')+'" data-v="'+esc(o[0])+'">'+esc(o[1])+'</button>'}).join('')+'</div>'}
  function chave(nome,valor,rotulo,detalhe){return'<label class="uc-row uc-toggle"><span class="uc-row-t"><strong>'+esc(rotulo)+'</strong>'+(detalhe?'<small>'+esc(detalhe)+'</small>':'')+'</span><input type="checkbox" role="switch" data-bool="'+nome+'"'+(valor?' checked':'')+'><i aria-hidden="true"></i></label>'}
  function faixa(nome,valor,min,max,passo,rotulo,unid){return'<label class="uc-row uc-range"><span class="uc-row-t"><strong>'+esc(rotulo)+'</strong><output>'+valor+(unid||'')+'</output></span><input type="range" min="'+min+'" max="'+max+'" step="'+passo+'" value="'+valor+'" data-range="'+nome+'" data-unid="'+(unid||'')+'"></label>'}
  function bloco(titulo,corpo,extra){return'<section class="uc-block"><header><h3>'+esc(titulo)+'</h3>'+(extra||'')+'</header>'+corpo+'</section>'}
  function setPath(o,path,v){var ks=path.split('.'),x=o;for(var i=0;i<ks.length-1;i++){x[ks[i]]=x[ks[i]]||{};x=x[ks[i]]}x[ks[ks.length-1]]=v;return o}
  function patchDe(path,v){return setPath({},path,v)}

  /* ---------- seções ---------- */
  function vAparencia(){
    var c=cfg(),pal=C().palette(),P=C().PRESETS,cards=Object.keys(P).map(function(k){return card(k,P[k].nome,P[k].claro,P[k].cores,c.tema===k)}).join('');
    var salvos=C().themes().filter(function(t){return t.valido}).map(function(t){var base=t.claro?P.claro:P.escuro;return card(t.id,t.nome,t.claro,Object.assign({},base.cores,t.cores),c.tema===t.id)}).join('');
    var cores=(maisCores?C().COLORS:PRINCIPAIS).map(function(k){var val=pal.cores[k]||'#000000',proprio=c.cores&&c.cores[k];
      return'<div class="uc-row uc-color"><span class="uc-row-t"><strong>'+NOMES_COR[k]+'</strong>'+(proprio?'<small>personalizada</small>':'<small>do tema</small>')+'</span><span class="uc-color-ctl">'+(proprio?'<button type="button" class="uc-mini" data-cor-reset="'+k+'" aria-label="Voltar à cor do tema">'+ic('restore')+'</button>':'')+'<input type="color" value="'+val+'" data-cor="'+k+'" aria-label="'+NOMES_COR[k]+'"></span></div>'}).join('');
    return bloco('Tema','<div class="uc-themes">'+cards+salvos+'</div>','<button type="button" class="uc-link" data-act="salvar-tema">'+ic('download')+'Salvar como tema</button>')+
      bloco('Cor de destaque','<div class="uc-swatches">'+ACENTOS.map(function(a){return'<button type="button" class="uc-sw'+(pal.cores.destaque===a?' on':'')+'" style="--c:'+a+'" data-acento="'+a+'" aria-label="Destaque '+a+'"></button>'}).join('')+'</div>')+
      bloco('Cores',cores+'<button type="button" class="uc-more" data-act="mais-cores">'+(maisCores?'Menos cores':'Todas as cores ('+C().COLORS.length+')')+'</button>',Object.keys(c.cores||{}).length?'<button type="button" class="uc-link" data-act="reset-cores">'+ic('restore')+'Restaurar</button>':'')+
      bloco('Forma',
        '<div class="uc-row"><span class="uc-row-t"><strong>Cantos</strong></span>'+seg('forma.cantos',c.forma.cantos,[['retos','Retos'],['suaves','Suaves'],['redondos','Redondos']])+'</div>'+
        '<div class="uc-row"><span class="uc-row-t"><strong>Densidade</strong><small>Espaço entre itens de listas e menus</small></span>'+seg('forma.densidade',c.forma.densidade,[['compacta','Compacta'],['normal','Normal'],['confortavel','Ampla']])+'</div>'+
        chave('forma.vidro',c.forma.vidro,'Efeito de vidro','Desfoque atrás de barras e painéis. Desligar deixa celulares simples mais leves.')+
        '<div class="uc-row"><span class="uc-row-t"><strong>Animações</strong></span>'+seg('animacoes',c.animacoes,[['sistema','Do sistema'],['ligadas','Ligadas'],['reduzidas','Reduzidas']])+'</div>');
  }
  function card(id,nome,claro,cores,on){
    return'<button type="button" class="uc-theme'+(on?' on':'')+'" data-theme-id="'+esc(id)+'" aria-pressed="'+on+'" style="--t-bg:'+cores.fundo+';--t-sf:'+cores.superficie+';--t-tx:'+cores.texto+';--t-t2:'+(cores.texto3||cores.texto)+';--t-ac:'+cores.destaque+';--t-ln:'+(cores.linha2||cores.linha)+'">'+
      '<span class="uc-theme-prev" aria-hidden="true"><i></i><b></b><em></em><u></u></span><span class="uc-theme-name">'+esc(nome)+(on?ic('check'):'')+'</span><small>'+(claro?'claro':'escuro')+'</small></button>';
  }
  function vTexto(){
    var c=cfg(),F=C().FONTS;
    function fontes(k,valor){var lista=Object.keys(F).map(function(f){return'<button type="button" class="uc-font'+(valor===f?' on':'')+'" data-fonte="'+k+'" data-v="'+f+'" style="font-family:'+esc(F[f].css)+'"><span>Aa</span><small>'+esc(F[f].nome)+'</small></button>'}).join('');
      var propria=!F[valor];return'<div class="uc-fonts">'+lista+'<button type="button" class="uc-font'+(propria?' on':'')+'" data-fonte-propria="'+k+'"><span>'+ic('plus')+'</span><small>'+(propria?esc(valor):'Outra…')+'</small></button></div>'}
    return bloco('Fonte da interface',fontes('texto.fonte',c.texto.fonte)+faixa('texto.tamanho',c.texto.tamanho,12,22,1,'Tamanho','px'))+
      bloco('Fonte das notas',fontes('texto.fonteEditor',c.texto.fonteEditor)+faixa('texto.tamanhoEditor',c.texto.tamanhoEditor,13,28,1,'Tamanho','px')+faixa('texto.alturaLinha',c.texto.alturaLinha,1.2,2.2,.05,'Altura da linha','')+
        '<div class="uc-sample"><h4>Exemplo de nota</h4><p>Uma cidade se lê como um livro: cada casa guarda uma ideia, e as ruas mostram como elas se ligam.</p><code>[[Outra nota]]</code></div>');
  }
  function vEditor(){
    var c=cfg();
    return bloco('Editor de notas',
      '<div class="uc-row"><span class="uc-row-t"><strong>Largura do texto</strong><small>Em telas grandes</small></span>'+seg('editor.largura',c.editor.largura,[['estreita','Estreita'],['media','Média'],['larga','Larga'],['total','Toda']])+'</div>'+
      '<div class="uc-row"><span class="uc-row-t"><strong>Abrir notas no modo</strong><small>Visual mostra formatado; Texto mostra o Markdown</small></span>'+seg('editor.modoInicial',c.editor.modoInicial,[['visual','Visual'],['texto','Texto']])+'</div>'+
      chave('editor.ortografia',c.editor.ortografia,'Corretor ortográfico','Sublinha palavras com erro enquanto você escreve'));
  }
  function vCidade(){
    var c=cfg(),A=global.UrbeArt,B=C().BIOMES,base=(A&&A.DEFAULT_PAL)||{},pal=c.cidade.paleta||{};
    var inv={};Object.keys(pal).forEach(function(k){(C().biomeId(k)||[]).forEach(function(id){inv[id]=pal[k][0]})});
    var biomas=Object.keys(B).map(function(id){var v=inv[id]||(base[id]?base[id][0]:'#777777'),own=!!inv[id];
      return'<label class="uc-biome'+(own?' own':'')+'"><input type="color" value="'+v+'" data-bioma="'+id+'" aria-label="'+esc(B[id])+'"><span>'+esc(B[id])+'</span>'+(own?'<button type="button" class="uc-mini" data-bioma-reset="'+id+'" aria-label="Cor original">'+ic('restore')+'</button>':'')+'</label>'}).join('');
    return bloco('Vida na cidade',
      chave('cidade.moradores',c.cidade.moradores,'Moradores','Pessoas andando entre notas ligadas')+
      chave('cidade.fauna',c.cidade.fauna,'Animais','Rebanhos, cervos, patos, pássaros, peixes, borboletas, pombos e vaga-lumes')+
      chave('cidade.clima',c.cidade.clima!==false,'Clima','Nuvens, vento, folhas, chuva, neve, neblina e arco-íris')+
      chave('cidade.eventos',c.cidade.eventos!==false,'Eventos','Balão, festa e fogos, barcos, raposa, revoadas e estrelas cadentes')+
      chave('cidade.nomes',c.cidade.nomes,'Nomes das casas','Letreiro com o nome da nota')+
      chave('cidade.bairros',c.cidade.bairros,'Nomes dos bairros','Letreiro com o nome da pasta'))+
      bloco('Luz','<div class="uc-row"><span class="uc-row-t"><strong>Hora do dia</strong><small>Ciclo: um dia inteiro a cada 24 minutos. Auto segue o relógio do aparelho</small></span>'+seg('cidade.ambiente',c.cidade.ambiente,[['ciclo','Ciclo'],['dia','Dia'],['entardecer','Tarde'],['noite','Noite'],['auto','Auto']])+'</div>')+
      bloco('Cores do chão','<p class="uc-help">Toque num bioma para trocar a cor. Para desenhar o chão pixel a pixel, use Texturas.</p><div class="uc-biomes">'+biomas+'</div>',Object.keys(pal).length?'<button type="button" class="uc-link" data-act="reset-paleta">'+ic('restore')+'Restaurar</button>':'');
  }

  /* ---------- texturas ---------- */
  function pacoteAtual(){var p=cfg().cidade.texturas;if(!p)return null;var d=docs.get(p);if(!d)return null;try{return{doc:d,dados:JSON.parse(d.content)||{}}}catch(_){return{doc:d,dados:null}}}
  function salvarPacote(pk,dados){docs.upsert(Object.assign({},pk.doc,{content:JSON.stringify(dados,null,2)+'\n'}),{source:'customize'})}
  function vTexturas(){
    var c=cfg(),packs=C().packs(),pk=pacoteAtual();
    var sel='<div class="uc-packs"><button type="button" class="uc-pack'+(!c.cidade.texturas?' on':'')+'" data-pack="">Nenhum<small>arte original</small></button>'+
      packs.map(function(p){return'<button type="button" class="uc-pack'+(p.ativo?' on':'')+'" data-pack="'+esc(p.path)+'">'+esc(p.nome)+'<small>'+esc(p.path.split('/').pop())+'</small></button>'}).join('')+
      '<button type="button" class="uc-pack uc-pack-new" data-act="novo-pacote">'+ic('plus')+'Novo pacote</button></div>';
    if(!pk)return bloco('Pacote de texturas',sel+'<p class="uc-help">Um pacote guarda desenhos próprios para o chão de cada bioma e para as construções. Crie um e desenhe pixel a pixel, ou use imagens do aparelho.</p>');
    if(!pk.dados)return bloco('Pacote de texturas',sel+'<p class="uc-error">O arquivo '+esc(pk.doc.path)+' não é um JSON válido. Abra-o para corrigir.</p><button type="button" class="ui-btn" data-abrir="'+esc(pk.doc.path)+'">Abrir arquivo</button>');
    var B=C().BIOMES,chao=pk.dados.chao||{},cons=pk.dados.construcoes||{};
    function temChao(id){return Object.keys(chao).some(function(k){return(C().biomeId(k)||[]).indexOf(id)>=0})}
    var ids=['grass','meadow','forest','dense','swamp','taiga','tundra','snow','hills','mountain','peak','desert','savanna','steppe','beach','river','lake','sea','deep'];
    var chaoHtml=ids.map(function(id){var own=temChao(id);return'<div class="uc-tex'+(own?' own':'')+'"><canvas width="16" height="16" data-tex="'+id+'"></canvas><span>'+esc(B[id])+'</span><div class="uc-tex-act">'+
      '<button type="button" data-desenhar="'+id+'" aria-label="Desenhar '+esc(B[id])+'">'+ic('pencil')+'</button><button type="button" data-imagem-chao="'+id+'" aria-label="Imagem para '+esc(B[id])+'">'+ic('image')+'</button>'+(own?'<button type="button" data-limpar-chao="'+id+'" aria-label="Voltar ao original">'+ic('restore')+'</button>':'')+'</div></div>'}).join('');
    var BN=C().BUILDINGS,consHtml=Object.keys(BN).map(function(id){var k=Object.keys(cons).find(function(x){return C().buildingId(x)===id});
      return'<div class="uc-tex uc-tex-b'+(k?' own':'')+'"><canvas width="48" height="56" data-bld="'+id+'"></canvas><span>'+esc(BN[id])+'</span><div class="uc-tex-act"><button type="button" data-desenhar-b="'+id+'" aria-label="Desenhar">'+ic('pencil')+'</button><button type="button" data-imagem-b="'+id+'" aria-label="Imagem">'+ic('image')+'</button>'+(k?'<button type="button" data-limpar-b="'+id+'" aria-label="Voltar ao original">'+ic('restore')+'</button>':'')+'</div></div>'}).join('');
    return bloco('Pacote de texturas',sel,'<button type="button" class="uc-link" data-abrir="'+esc(pk.doc.path)+'">'+ic('code')+'Ver JSON</button>')+
      bloco('Chão','<p class="uc-help">Cada bioma é um quadrado de 16×16 pixels que se repete pelo mapa.</p><div class="uc-texs">'+chaoHtml+'</div>')+
      bloco('Construções','<p class="uc-help">Desenho de 48×56 pixels, como as casas originais. Imagens de outro tamanho são ajustadas.</p><div class="uc-texs">'+consHtml+'</div>');
  }
  function desenharPrevias(){
    var A=global.UrbeArt;if(!A||!root)return;
    root.querySelectorAll('canvas[data-tex]').forEach(function(cv){var id=cv.getAttribute('data-tex'),x=cv.getContext('2d'),img=x.createImageData(16,16),t=A.texture(id,0);if(!t)return;img.data.set(t);x.putImageData(img,0,0)});
    root.querySelectorAll('canvas[data-bld]').forEach(function(cv){var id=cv.getAttribute('data-bld'),x=cv.getContext('2d');x.clearRect(0,0,cv.width,cv.height);C().loadTextures().then(function(t){var im=t.construcoes[id]||A.building(id,A.styleFor('grass'),0,false);x.imageSmoothingEnabled=false;x.drawImage(im,0,0,cv.width,cv.height)})});
  }
  function lerArquivo(accept){return new Promise(function(ok){var i=doc.createElement('input');i.type='file';i.accept=accept||'image/*';i.onchange=function(){ok(i.files&&i.files[0]||null)};i.click()})}
  function reduzir(file,maxW,maxH){return new Promise(function(ok,no){var r=new FileReader();r.onload=function(){var im=new Image();im.onload=function(){var s=Math.min(1,maxW/im.width,maxH/im.height),w=Math.max(1,Math.round(im.width*s)),h=Math.max(1,Math.round(im.height*s)),cv=doc.createElement('canvas');cv.width=w;cv.height=h;var x=cv.getContext('2d');x.imageSmoothingEnabled=s<1;x.drawImage(im,0,0,w,h);ok(cv.toDataURL('image/png'))};im.onerror=function(){no(new Error('Não consegui ler essa imagem.'))};im.src=r.result};r.onerror=function(){no(r.error)};r.readAsDataURL(file)})}
  function chaoKey(dados,id){var ch=dados.chao||{};return Object.keys(ch).find(function(k){return(C().biomeId(k)||[]).indexOf(id)>=0&&(C().biomeId(k)||[]).length===1})||null}
  function nomePt(id){var m={grass:'grama',meadow:'prado',forest:'floresta',dense:'mata',swamp:'pantano',taiga:'taiga',tundra:'tundra',snow:'neve',hills:'colinas',mountain:'montanha',peak:'pico',desert:'deserto',savanna:'savana',steppe:'estepe',beach:'praia',sea:'mar',deep:'profundo',river:'rio',lake:'lago'};return m[id]||id}
  function consPt(id){return{house:'casa',hall:'salao',workshop:'oficina',dyer:'tinturaria',tower:'torre',market:'mercado',store:'armazem'}[id]||id}
  async function imagemChao(id){var f=await lerArquivo();if(!f)return;var pk=pacoteAtual();if(!pk||!pk.dados)return;try{var url=await reduzir(f,16,16);var d=pk.dados;d.chao=d.chao||{};var k=chaoKey(d,id);if(k)delete d.chao[k];d.chao[nomePt(id)]={imagem:url};salvarPacote(pk,d);toast('Textura trocada')}catch(e){D.alert({title:'Imagem',message:e.message})}}
  async function imagemCons(id){var f=await lerArquivo();if(!f)return;var pk=pacoteAtual();if(!pk||!pk.dados)return;try{var url=await reduzir(f,96,112);var d=pk.dados;d.construcoes=d.construcoes||{};Object.keys(d.construcoes).forEach(function(k){if(C().buildingId(k)===id)delete d.construcoes[k]});d.construcoes[consPt(id)]={imagem:url};salvarPacote(pk,d);toast('Construção trocada')}catch(e){D.alert({title:'Imagem',message:e.message})}}
  function limparChao(id){var pk=pacoteAtual();if(!pk||!pk.dados||!pk.dados.chao)return;var k=chaoKey(pk.dados,id);if(k)delete pk.dados.chao[k];salvarPacote(pk,pk.dados)}
  function limparCons(id){var pk=pacoteAtual();if(!pk||!pk.dados||!pk.dados.construcoes)return;Object.keys(pk.dados.construcoes).forEach(function(k){if(C().buildingId(k)===id)delete pk.dados.construcoes[k]});salvarPacote(pk,pk.dados)}
  async function novoPacote(){
    var nome=await D.prompt({title:'Novo pacote de texturas',label:'Nome',value:'Meu pacote',confirm:'Criar'});if(!nome)return;
    var p=C().FOLDER+'/texturas/'+nome.trim().replace(/[\\/:*?"<>|]/g,'-')+'.json',i=2;while(docs.get(p)){p=p.replace(/( \d+)?\.json$/,' '+i+'.json');i++}
    docs.upsert({path:p,content:JSON.stringify({nome:nome.trim(),chao:{},construcoes:{}},null,2)+'\n'},{source:'customize'});C().set({cidade:{texturas:p}});toast('Pacote criado e ativado');
  }

  /* ---------- editor de pixels ---------- */
  function editorPixels(titulo,w,h,inicial,cores0){
    return new Promise(function(fim){
      var px=inicial.slice(),hist=[],cor=cores0[0]||'#000000',ferr='lapis',paleta=cores0.slice(0,10);
      var ov=doc.createElement('div');ov.className='uc-px-ov';ov.innerHTML='<div class="uc-px" role="dialog" aria-modal="true" aria-label="'+esc(titulo)+'"><header><strong>'+esc(titulo)+'</strong><button type="button" class="uc-x" data-px="sair" aria-label="Fechar">'+ic('close')+'</button></header>'+
        '<div class="uc-px-stage"><canvas class="uc-px-cv"></canvas></div>'+
        '<div class="uc-px-tools"><button type="button" data-f="lapis" class="on" aria-label="Lápis">'+ic('pencil')+'</button><button type="button" data-f="balde" aria-label="Balde">'+ic('bucket')+'</button><button type="button" data-f="conta" aria-label="Conta-gotas">'+ic('dropper')+'</button><button type="button" data-px="desfazer" aria-label="Desfazer">'+ic('restore')+'</button><span class="uc-px-grid-t"><input type="checkbox" checked data-px="grade"> Grade</span></div>'+
        '<div class="uc-px-pal"></div><footer><button type="button" class="ui-btn" data-px="sair">Cancelar</button><button type="button" class="ui-btn ui-btn-primary" data-px="ok">Salvar</button></footer></div>';
      doc.body.appendChild(ov);
      var cv=ov.querySelector('canvas'),x=cv.getContext('2d'),grade=true,esc2=1;
      function tam(){var st=ov.querySelector('.uc-px-stage'),bw=st.clientWidth-32,bh=st.clientHeight-32;esc2=Math.max(4,Math.floor(Math.min(bw/w,bh/h)));cv.width=w*esc2;cv.height=h*esc2;cv.style.width=cv.width+'px';cv.style.height=cv.height+'px';pintar()}
      function pintar(){x.clearRect(0,0,cv.width,cv.height);for(var j=0;j<h;j++)for(var i=0;i<w;i++){var c=px[j*w+i];if(c){x.fillStyle=c;x.fillRect(i*esc2,j*esc2,esc2,esc2)}else{x.fillStyle=((i+j)&1)?'#cfcfd4':'#ececf0';x.fillRect(i*esc2,j*esc2,esc2,esc2)}}
        if(grade&&esc2>=6){x.strokeStyle='rgba(0,0,0,.18)';x.lineWidth=1;x.beginPath();for(var a=0;a<=w;a++){x.moveTo(a*esc2+.5,0);x.lineTo(a*esc2+.5,h*esc2)}for(var b=0;b<=h;b++){x.moveTo(0,b*esc2+.5);x.lineTo(w*esc2,b*esc2+.5)}x.stroke()}}
      function pal(){var box=ov.querySelector('.uc-px-pal');box.innerHTML=paleta.map(function(c){return'<button type="button" class="uc-sw'+(c===cor?' on':'')+'" style="--c:'+c+'" data-c="'+c+'" aria-label="'+c+'"></button>'}).join('')+(inicial.some(function(v){return!v})?'<button type="button" class="uc-sw uc-sw-clear'+(cor===''?' on':'')+'" data-c="" aria-label="Transparente"></button>':'')+'<label class="uc-sw uc-sw-add" aria-label="Outra cor">'+ic('plus')+'<input type="color" value="'+(cor||'#888888')+'"></label>';
        box.querySelectorAll('[data-c]').forEach(function(b){b.onclick=function(){cor=b.getAttribute('data-c');pal()}});
        var inp=box.querySelector('input');inp.oninput=function(){cor=inp.value.toLowerCase()};inp.onchange=function(){cor=inp.value.toLowerCase();if(paleta.indexOf(cor)<0){paleta.unshift(cor);paleta=paleta.slice(0,12)}pal()}}
      function pos(e){var r=cv.getBoundingClientRect();return{i:Math.floor((e.clientX-r.left)/r.width*w),j:Math.floor((e.clientY-r.top)/r.height*h)}}
      function balde(i,j){var alvo=px[j*w+i];if(alvo===cor)return;var pilha=[[i,j]];while(pilha.length){var q=pilha.pop(),a=q[0],b=q[1];if(a<0||b<0||a>=w||b>=h||px[b*w+a]!==alvo)continue;px[b*w+a]=cor;pilha.push([a+1,b],[a-1,b],[a,b+1],[a,b-1])}}
      var arrastando=false;
      function aplicarEm(e){var p=pos(e);if(p.i<0||p.j<0||p.i>=w||p.j>=h)return;if(ferr==='conta'){cor=px[p.j*w+p.i]||'';if(cor&&paleta.indexOf(cor)<0)paleta.unshift(cor);pal();return}if(ferr==='balde'){balde(p.i,p.j)}else px[p.j*w+p.i]=cor;pintar()}
      cv.addEventListener('pointerdown',function(e){e.preventDefault();hist.push(px.slice());if(hist.length>60)hist.shift();arrastando=ferr==='lapis';try{cv.setPointerCapture(e.pointerId)}catch(_){}aplicarEm(e)});
      cv.addEventListener('pointermove',function(e){if(arrastando)aplicarEm(e)});
      cv.addEventListener('pointerup',function(){arrastando=false});cv.addEventListener('pointercancel',function(){arrastando=false});
      ov.querySelectorAll('[data-f]').forEach(function(b){b.onclick=function(){ferr=b.getAttribute('data-f');ov.querySelectorAll('[data-f]').forEach(function(o){o.classList.toggle('on',o===b)})}});
      ov.addEventListener('click',function(e){var a=e.target.closest('[data-px]');if(!a)return;var k=a.getAttribute('data-px');
        if(k==='sair'){ov.remove();fim(null)}else if(k==='ok'){ov.remove();fim(px)}else if(k==='desfazer'){if(hist.length){px=hist.pop();pintar()}}else if(k==='grade'){grade=a.checked;pintar()}});
      pal();requestAnimationFrame(tam);
    });
  }
  function toGrid(px,w,h){var letras='abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789',mapa={},cores={},n=0,rows=[];
    for(var j=0;j<h;j++){var r='';for(var i=0;i<w;i++){var c=px[j*w+i];if(!c){r+='.';continue}if(!mapa[c]){if(n>=letras.length){r+=r.slice(-1)||'.';continue}mapa[c]=letras[n++];cores[mapa[c]]=c}r+=mapa[c]}rows.push(r)}return{cores:cores,pixels:rows}}
  function fromData(data,w,h){var out=[];for(var k=0;k<w*h;k++){var o=k*4;out.push(data[o+3]<128?'':'#'+[data[o],data[o+1],data[o+2]].map(function(v){return('0'+v.toString(16)).slice(-2)}).join(''))}return out}
  function coresDe(px){var cont={};px.forEach(function(c){if(c)cont[c]=(cont[c]||0)+1});return Object.keys(cont).sort(function(a,b){return cont[b]-cont[a]}).slice(0,8)}
  async function desenharChao(id){
    var A=global.UrbeArt,pk=pacoteAtual();if(!A||!pk||!pk.dados)return;var px=fromData(A.texture(id,0),16,16),base=A.PAL[id]||[];
    var out=await editorPixels('Chão: '+C().BIOMES[id],16,16,px,base.concat(coresDe(px)).filter(function(c,i,a){return a.indexOf(c)===i}));if(!out)return;
    out=out.map(function(c){return c||base[0]});var d=pk.dados;d.chao=d.chao||{};var k=chaoKey(d,id);if(k)delete d.chao[k];d.chao[nomePt(id)]=toGrid(out,16,16);salvarPacote(pk,d);toast('Textura salva');
  }
  async function desenharCons(id){
    var A=global.UrbeArt,pk=pacoteAtual();if(!A||!pk||!pk.dados)return;var t=await C().loadTextures(),src=t.construcoes[id]||A.building(id,A.styleFor('grass'),0,false),cv=doc.createElement('canvas');cv.width=48;cv.height=56;var x=cv.getContext('2d');x.imageSmoothingEnabled=false;x.drawImage(src,0,0,48,56);
    var px=fromData(x.getImageData(0,0,48,56).data,48,56),out=await editorPixels(C().BUILDINGS[id],48,56,px,coresDe(px).concat(['#000000','#ffffff']));if(!out)return;
    var d=pk.dados;d.construcoes=d.construcoes||{};Object.keys(d.construcoes).forEach(function(k){if(C().buildingId(k)===id)delete d.construcoes[k]});d.construcoes[consPt(id)]=toGrid(out,48,56);salvarPacote(pk,d);toast('Construção salva');
  }

  /* ---------- estilos ---------- */
  function vEstilos(){
    var c=cfg(),lista=C().styles(),seguro=C().safeMode();
    var itens=lista.length?lista.map(function(s){return'<div class="uc-row uc-item"><span class="uc-row-t"><strong>'+esc(s.nome)+'</strong><small>'+esc(s.path)+'</small></span><span class="uc-item-act"><button type="button" class="uc-mini" data-abrir="'+esc(s.path)+'" aria-label="Editar">'+ic('edit')+'</button><label class="uc-toggle uc-toggle-sm"><input type="checkbox" role="switch" data-estilo="'+esc(s.path)+'"'+(s.ligado?' checked':'')+' aria-label="Ligar '+esc(s.nome)+'"><i aria-hidden="true"></i></label></span></div>'}).join(''):'<p class="uc-help">Nenhum estilo ainda. Estilos são arquivos .css na pasta Personalização/estilos que mudam qualquer detalhe visual.</p>';
    return(seguro?'<p class="uc-warn">'+ic('shield')+'Modo seguro ligado: estilos e plugins estão desativados.</p>':'')+
      bloco('Estilos',itens+'<button type="button" class="uc-more" data-act="novo-estilo">'+ic('plus')+'Novo estilo</button>')+
      bloco('CSS rápido','<p class="uc-help">Regras aplicadas por último, guardadas no tema.json. Ex.: <code>#renderedPreview h1 { color: var(--ui-accent) }</code></p><textarea class="uc-code" data-css spellcheck="false" rows="7" placeholder="/* suas regras */">'+esc(c.css)+'</textarea><div class="uc-actions"><button type="button" class="ui-btn ui-btn-primary" data-act="aplicar-css">Aplicar</button></div>');
  }
  async function novoEstilo(){
    var nome=await D.prompt({title:'Novo estilo',label:'Nome do arquivo',value:'meu-estilo',confirm:'Criar'});if(!nome)return;
    var p=C().FOLDER+'/estilos/'+nome.trim().replace(/\.css$/i,'').replace(/[\\/:*?"<>|]/g,'-')+'.css';
    if(!docs.get(p))docs.upsert({path:p,content:'/* '+nome.trim()+' — estilo do Urbe.\n   Variáveis: --ui-accent, --ui-bg, --ui-surface, --ui-text, --ui-font, --ui-editor-font, --ui-r-m...\n   Veja Tutorial/Personalização/Estilos CSS. */\n\n/* títulos das notas na cor de destaque */\n#renderedPreview h1 { color: var(--ui-accent); }\n'},{source:'customize'});
    var e={};e[p]=true;C().set({estilos:e});toast('Estilo criado e ligado');
  }

  /* ---------- plugins ---------- */
  var ESTADOS={ativo:['Ligado','ok'],desligado:['Desligado',''],erro:['Erro','err'],mudou:['Código mudou','warn'],seguro:['Modo seguro','warn']};
  function vPlugins(){
    var lista=PL()?PL().list():[],seguro=C().safeMode();
    var itens=lista.length?lista.map(function(p){var st=ESTADOS[p.status]||ESTADOS.desligado,ligado=p.status==='ativo'||p.status==='erro'||p.status==='seguro';
      return'<div class="uc-plugin"><div class="uc-plugin-h"><span class="uc-row-t"><strong>'+esc(p.nome)+(p.versao?' <small class="uc-ver">v'+esc(p.versao)+'</small>':'')+'</strong><small>'+esc(p.descricao||p.path)+'</small></span><span class="uc-pill '+st[1]+'">'+st[0]+'</span></div>'+
        (p.erro?'<p class="uc-error">'+esc(p.erro)+'</p>':'')+
        '<div class="uc-plugin-act"><button type="button" class="uc-link" data-abrir="'+esc(p.path)+'">'+ic('code')+'Ver código</button>'+
        (p.status==='mudou'?'<button type="button" class="ui-btn ui-btn-primary uc-sm" data-plugin-on="'+esc(p.path)+'">Revisei, ligar</button><button type="button" class="ui-btn uc-sm" data-plugin-off="'+esc(p.path)+'">Desligar</button>':
          (ligado?'<button type="button" class="ui-btn uc-sm" data-plugin-off="'+esc(p.path)+'">Desligar</button>':'<button type="button" class="ui-btn ui-btn-primary uc-sm" data-plugin-on="'+esc(p.path)+'">Ligar</button>'))+'</div></div>'}).join(''):'<p class="uc-help">Nenhum plugin nesta cidade. Crie um a partir de um modelo e veja o código funcionando.</p>';
    return'<p class="uc-warn">'+ic('shield')+'<span>Plugins são programas: podem ler e mudar suas notas. Só ligue código que você entende ou que veio de alguém de confiança. Se o arquivo mudar, o Urbe desliga o plugin até você revisar.</span></p>'+
      bloco('Plugins desta cidade',itens+'<button type="button" class="uc-more" data-act="novo-plugin">'+ic('plus')+'Novo plugin</button>')+
      bloco('Segurança',chave('seguro',seguro,'Modo seguro','Desliga todos os plugins e estilos, sem apagar nada. Útil se algo quebrar a tela.'));
  }
  async function novoPlugin(){
    var P=PL();if(!P)return;var m=await D.choose({title:'Novo plugin',message:'Escolha um modelo para começar. O código fica em Personalização/plugins e você pode editar à vontade.',options:P.TEMPLATES.map(function(t){return{value:t.id,label:t.nome,detail:t.descricao,icon:'puzzle'}})});if(!m)return;
    var nome=await D.prompt({title:'Nome do plugin',value:(P.TEMPLATES.find(function(t){return t.id===m})||{}).nome||'Meu plugin',confirm:'Criar'});if(!nome)return;
    var d=P.create(nome,m);toast('Plugin criado: '+d.path.split('/').pop()+' (desligado)');render();
  }

  /* ---------- arquivo ---------- */
  function vArquivo(){
    var d=docs.get(C().FILE),txt=d?d.content:JSON.stringify({'$schema':'urbe-tema-1'},null,2)+'\n',st=C().state();
    return bloco(C().FILE,'<p class="uc-help">Tudo desta tela fica neste arquivo. Você pode editar aqui, abrir como nota, copiar para outra cidade ou pedir ao Assistente que crie um tema para você.</p>'+
      '<textarea class="uc-code uc-json" data-json spellcheck="false" rows="14">'+esc(txt)+'</textarea><div class="uc-json-state" data-json-state></div>'+
      '<div class="uc-actions"><button type="button" class="ui-btn" data-act="restaurar-tudo">Restaurar padrão</button><button type="button" class="ui-btn" data-act="abrir-json">Abrir como nota</button><button type="button" class="ui-btn ui-btn-primary" data-act="aplicar-json">Aplicar</button></div>')+
      (st.warnings&&st.warnings.length?bloco('Avisos','<ul class="uc-list">'+st.warnings.map(function(w){return'<li><code>'+esc(w.path)+'</code> '+esc(w.message)+'</li>'}).join('')+'</ul>'):'')+
      bloco('Pedir ao Assistente','<p class="uc-help">Exemplos: “crie um tema sépia com fonte serifada e cantos retos”, “deixe o mapa sempre à noite e esconda os animais”, “faça um plugin que conta as palavras de hoje”. O Assistente conhece o formato e mostra a alteração antes de gravar.</p>');
  }
  function validarJson(){var ta=root&&root.querySelector('[data-json]'),out=root&&root.querySelector('[data-json-state]');if(!ta||!out)return null;var n=C().normalize(ta.value);
    out.className='uc-json-state '+(n.errors.length?'err':'ok');out.innerHTML=n.errors.length?n.errors.map(function(e){return'<div><code>'+esc(e.path||'(arquivo)')+'</code> '+esc(e.message)+'</div>'}).join(''):'JSON válido'+(n.warnings.length?' · '+n.warnings.length+' aviso(s)':'');return n}

  /* ---------- montagem ---------- */
  var VIEWS={aparencia:vAparencia,texto:vTexto,editor:vEditor,cidade:vCidade,texturas:vTexturas,estilos:vEstilos,plugins:vPlugins,arquivo:vArquivo};
  function render(){
    if(!root)return;var body=root.querySelector('.uc-body'),y=body.scrollTop;
    root.querySelectorAll('[data-sec]').forEach(function(b){var on=b.getAttribute('data-sec')===secao;b.classList.toggle('on',on);b.setAttribute('aria-selected',on)});
    body.innerHTML=VIEWS[secao]();body.scrollTop=y;
    if(secao==='texturas')desenharPrevias();if(secao==='arquivo')validarJson();
  }
  function abrir(qual){
    if(qual&&VIEWS[qual])secao=qual;
    if(!root){
      root=doc.createElement('div');root.id='urbeCustomize';root.className='uc';root.innerHTML='<div class="uc-backdrop" data-close></div><div class="uc-card" role="dialog" aria-modal="true" aria-labelledby="ucTitle">'+
        '<header class="uc-head"><h2 id="ucTitle">'+ic('brush')+'Personalização</h2><button type="button" class="uc-x" data-close aria-label="Fechar">'+ic('close')+'</button></header>'+
        '<nav class="uc-nav" role="tablist">'+SECOES.map(function(s){return'<button type="button" role="tab" data-sec="'+s.id+'">'+ic(s.icone)+'<span>'+s.nome+'</span></button>'}).join('')+'</nav>'+
        '<div class="uc-body" role="tabpanel"></div></div>';
      doc.body.appendChild(root);ligar();
    }
    root.hidden=false;render();requestAnimationFrame(function(){root.classList.add('open');var on=root.querySelector('.uc-nav .on');if(on&&on.scrollIntoView)on.scrollIntoView({block:'nearest',inline:'center'})});
    doc.addEventListener('keydown',tecla,true);
  }
  function fechar(){if(!root)return;root.classList.remove('open');root.hidden=true;doc.removeEventListener('keydown',tecla,true)}
  function tecla(e){if(e.key==='Escape'&&root&&!root.hidden&&!doc.querySelector('.udlg.open,.uc-px-ov')){e.preventDefault();e.stopPropagation();fechar()}}
  function abrirNota(p){fechar();if(!docs.get(p))return;core.commands.execute('document.open',{path:p,source:'customize'})}

  function ligar(){
    root.addEventListener('click',function(e){
      var t=e.target,a;
      /* só elementos da própria tela: o <html> também tem atributos data-* (data-tema...) */
      function q(sel){var el=t.closest(sel);return el&&root.contains(el)?el:null}
      if(q('[data-close]')){fechar();return}
      if((a=q('[data-sec]'))){secao=a.getAttribute('data-sec');root.querySelector('.uc-body').scrollTop=0;render();return}
      if((a=q('[data-theme-id]'))){C().set({tema:a.getAttribute('data-theme-id')});render();return}
      if((a=q('[data-acento]'))){C().set({cores:{destaque:a.getAttribute('data-acento')}});render();return}
      if((a=q('[data-cor-reset]'))){var c=cfg();delete c.cores[a.getAttribute('data-cor-reset')];C().replace(c);render();return}
      if((a=q('.uc-seg [data-v]'))){var s=a.closest('[data-seg]').getAttribute('data-seg');C().set(patchDe(s,a.getAttribute('data-v')));render();return}
      if((a=q('[data-fonte]'))){C().set(patchDe(a.getAttribute('data-fonte'),a.getAttribute('data-v')));render();return}
      if((a=q('[data-fonte-propria]'))){var k=a.getAttribute('data-fonte-propria');D.prompt({title:'Outra fonte',label:'Nome da fonte instalada',hint:'Ex.: Inter, Georgia, "Fira Sans". Ela precisa existir no aparelho ou num estilo CSS com @font-face.',value:'',confirm:'Usar'}).then(function(v){if(v){C().set(patchDe(k,v.trim()));render()}});return}
      if((a=q('[data-bioma-reset]'))){var id=a.getAttribute('data-bioma-reset'),c2=cfg(),p=c2.cidade.paleta||{};Object.keys(p).forEach(function(k2){if((C().biomeId(k2)||[]).indexOf(id)>=0)delete p[k2]});c2.cidade.paleta=p;C().replace(c2);render();return}
      if((a=q('[data-pack]'))){C().set({cidade:{texturas:a.getAttribute('data-pack')}});render();return}
      if((a=q('[data-desenhar]'))){desenharChao(a.getAttribute('data-desenhar'));return}
      if((a=q('[data-imagem-chao]'))){imagemChao(a.getAttribute('data-imagem-chao'));return}
      if((a=q('[data-limpar-chao]'))){limparChao(a.getAttribute('data-limpar-chao'));return}
      if((a=q('[data-desenhar-b]'))){desenharCons(a.getAttribute('data-desenhar-b'));return}
      if((a=q('[data-imagem-b]'))){imagemCons(a.getAttribute('data-imagem-b'));return}
      if((a=q('[data-limpar-b]'))){limparCons(a.getAttribute('data-limpar-b'));return}
      if((a=q('[data-abrir]'))){abrirNota(a.getAttribute('data-abrir'));return}
      if((a=q('[data-plugin-on]'))){var pp=a.getAttribute('data-plugin-on');D.confirm({title:'Ligar plugin?',message:'O código de '+pp.split('/').pop()+' vai rodar com acesso às suas notas neste aparelho. Ligue só se confia nele.',confirm:'Ligar'}).then(function(ok){if(ok)PL().enable(pp).then(function(st){render();toast(st&&st.status==='ativo'?'Plugin ligado':'O plugin não iniciou — veja o erro')})});return}
      if((a=q('[data-plugin-off]'))){PL().disable(a.getAttribute('data-plugin-off')).then(render);return}
      if((a=q('[data-act]'))){acao(a.getAttribute('data-act'));return}
    });
    root.addEventListener('input',function(e){
      var t=e.target;
      if(t.matches('[data-cor]')){C().preview({cores:patchDe(t.getAttribute('data-cor'),t.value)})}
      else if(t.matches('[data-range]')){var u=t.getAttribute('data-unid');t.parentNode.querySelector('output').textContent=t.value+u;C().preview(patchDe(t.getAttribute('data-range'),+t.value))}
      else if(t.matches('[data-json]')){validarJson()}
    });
    root.addEventListener('change',function(e){
      var t=e.target;
      if(t.matches('[data-cor]')){C().set({cores:patchDe(t.getAttribute('data-cor'),t.value)});render()}
      else if(t.matches('[data-range]')){C().set(patchDe(t.getAttribute('data-range'),+t.value))}
      else if(t.matches('[data-bool]')){var k=t.getAttribute('data-bool');if(k==='seguro'){C().safeMode(t.checked);render()}else C().set(patchDe(k,t.checked))}
      else if(t.matches('[data-bioma]')){var id=t.getAttribute('data-bioma'),p={};p[nomePt(id)]=[t.value];C().set({cidade:{paleta:p}});render()}
      else if(t.matches('[data-estilo]')){var e2={};e2[t.getAttribute('data-estilo')]=t.checked;C().set({estilos:e2})}
    });
  }
  async function acao(k){
    if(k==='mais-cores'){maisCores=!maisCores;render()}
    else if(k==='reset-cores'){C().reset('cores');render()}
    else if(k==='reset-paleta'){var o=cfg();o.cidade.paleta={};C().replace(o);render()}
    else if(k==='salvar-tema'){var n=await D.prompt({title:'Salvar tema',label:'Nome',message:'Guarda as cores atuais em Personalização/temas para reaplicar depois ou compartilhar.',value:'Meu tema',confirm:'Salvar'});if(!n)return;try{var p=C().saveTheme(n),c3=cfg();c3.tema=p;c3.cores={};C().replace(c3);toast('Tema salvo');render()}catch(e){D.alert({title:'Tema',message:e.message})}}
    else if(k==='novo-pacote'){await novoPacote();render()}
    else if(k==='novo-estilo'){await novoEstilo();render()}
    else if(k==='aplicar-css'){var ta=root.querySelector('[data-css]');C().set({css:ta.value});toast('CSS aplicado')}
    else if(k==='novo-plugin'){await novoPlugin()}
    else if(k==='aplicar-json'){var n2=validarJson();if(!n2||n2.errors.length){toast('Corrija os erros antes de aplicar');return}var d=docs.get(C().FILE),txt=root.querySelector('[data-json]').value;docs.upsert(Object.assign({},d||{},{id:d?d.id:undefined,path:C().FILE,content:txt}),{source:'customize.file'});C().reload();toast('Aplicado');render()}
    else if(k==='restaurar-tudo'){if(await D.confirm({title:'Restaurar padrão?',message:'Volta cores, texto, editor e cidade ao padrão. Temas salvos, estilos, texturas e plugins continuam na pasta.',confirm:'Restaurar',danger:true})){C().reset();render()}}
    else if(k==='abrir-json'){if(!docs.get(C().FILE))C().set({});abrirNota(C().FILE)}
  }

  /* reage a mudanças feitas fora da tela (outro aparelho, Assistente, arquivo) */
  ['customize:applied','plugins:changed'].forEach(function(n){core.events.on(n,function(){if(root&&!root.hidden&&!root.contains(doc.activeElement&&doc.activeElement.closest&&doc.activeElement.closest('textarea,input[type=range],input[type=color]')))render()})});
  core.events.on('document:updated',function(e){if(root&&!root.hidden&&secao==='texturas'&&e.document&&e.document.path.indexOf(C().FOLDER+'/texturas/')===0)setTimeout(render,200)});

  core.commands.register('ui.customize',{title:'Personalização',category:'Aplicativo',execute:function(ctx){abrir(ctx&&ctx.section)}});
  core.commands.register('ui.plugins',{title:'Plugins',category:'Personalização',execute:function(){abrir('plugins')}});
  global.UrbeCustomizePanel={open:abrir,close:fechar};
})(window);
