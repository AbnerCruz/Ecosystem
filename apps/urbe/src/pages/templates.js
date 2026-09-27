(function(global){
  'use strict';
  /* Modelos embutidos de página. Cada um devolve um JSON de página completo;
     alguns partem de uma nota ou de uma pasta do vault. Modelos do usuário são
     arquivos .template.json no vault e aparecem junto destes no estúdio. */
  var P=global.UrbePages;if(!P)return;
  function sec(type,props,style){var s={type:type,props:props||{}};if(style)s.style=style;return s}
  function btn(label,url,variant){return{label:label,url:url||'#',variant:variant||'primary'}}
  function page(meta,theme,layout,sections){return{version:1,kind:'urbe-page',meta:meta,theme:theme,layout:layout||{},sections:sections}}
  var year=new Date().getFullYear();
  /* abertura e fechamento de um livro: capa, rosto, créditos, dedicatória, sumário … sobre o autor, colofão */
  function bookSections(t,miolo){return [
    sec('bookcover',{title:t,subtitle:'Um subtítulo que convida à leitura',author:'Nome do autor',publisher:'Editora',style:'classic'}),
    sec('titlepage',{title:t,subtitle:'Um subtítulo que convida à leitura',author:'Nome do autor',publisher:'Editora',place:'Cidade',year:String(year)}),
    sec('copyright',{}),
    sec('dedication',{kind:'dedication',markdown:'Para quem lê.'}),
    sec('booktoc',{title:'Sumário'})].concat(miolo,[
    sec('about',{}),
    sec('colophon',{})])}

  var LIST=[
    {id:'empty',name:'Vazio',icon:'○',description:'O mínimo do mínimo: nenhuma seção, sem barra, sem rodapé, sem animações. Você monta tudo.',
      build:function(c){return page({title:c.title||'Nova página',icon:'✦'},{preset:'grafite',animations:false,shadow:'none'},{nav:false,footer:'',themeToggle:false,backToTop:false,progress:false},[])}},

    {id:'canvas',name:'Tela livre',icon:'⊞',description:'Tudo em layout livre: capa, colunas e cartões montados peça por peça, cada um com estilo próprio para computador, tablet e celular.',
      build:function(c){var t=c.title||'Minha página',X=P.free,m=X.make;
        var capa=X.PRESETS.capa.build();capa.children[0].content.text=t;
        var cols=X.PRESETS['imagem-texto'].build();var cards=X.PRESETS['grade-cartoes'].build();
        var root=m('box',{},{gap:'64px',padding:'0 0 48px'},[capa,cols,m('box',{},{gap:'18px'},[m('heading',{text:'Destaques',level:2},{textAlign:'center'}),cards])]);
        return page({title:t,icon:'⊞'},{preset:'aurora'},{nav:false,footer:'',themeToggle:false},[sec('free',{root:root},{padding:'s'})])}},

    {id:'blank',name:'Simples',icon:'＋',description:'O básico, genérico e resumido: um título, um texto e um rodapé.',
      build:function(c){var t=c.title||'Nova página';return page({title:t,icon:'✦'},{preset:'papel',animations:false},{nav:false,footer:'© '+year+' '+t,themeToggle:false},[
        sec('hero',{title:t,subtitle:'Uma frase curta sobre o que é esta página.',buttons:[],height:'auto',layout:'left'},{padding:'l'}),
        sec('text',{markdown:'Escreva aqui. Toque em qualquer parte para editar e use **+** para adicionar blocos.'},{width:'narrow'})])}},

    {id:'landing',name:'Landing de produto',icon:'🚀',description:'Capa, números, recursos, depoimentos, planos, perguntas e chamada final.',
      build:function(c){var t=c.title||'Nome do produto';return page({title:t,description:'Uma frase que vende o produto.',icon:'🚀'},{preset:'aurora',background:'mesh'},{brand:t,navCta:'Começar',navCtaUrl:'#planos',footer:'© '+year+' '+t+' · Feito com Urbe'},[
        sec('hero',{eyebrow:'Novo · versão 1.0',title:'O jeito mais simples de fazer **o que importa**',subtitle:'Explique em uma frase o que o produto resolve e para quem ele é.',buttons:[btn('Começar grátis','#planos'),btn('Ver como funciona','#recursos','secondary')],layout:'center',height:'tall'},{padding:'l'}),
        sec('stats',{items:[{value:'10k+',label:'pessoas usando'},{value:'4,9★',label:'avaliação média'},{value:'2 min',label:'para começar'},{value:'24/7',label:'suporte'}]},{align:'center',padding:'s'}),
        sec('features',{title:'Tudo o que você precisa',subtitle:'Recursos pensados para o dia a dia.',columns:3,items:[{icon:'⚡',title:'Rápido',text:'Resposta instantânea em qualquer tela.'},{icon:'🔒',title:'Privado',text:'Seus dados ficam com você.'},{icon:'🧠',title:'Inteligente',text:'Automatiza o repetitivo.'},{icon:'📱',title:'Em todo lugar',text:'Celular, tablet e computador.'},{icon:'🎨',title:'Bonito',text:'Visual moderno e personalizável.'},{icon:'🤝',title:'Colaborativo',text:'Compartilhe com quem quiser.'}]},{anchor:'Recursos',menu:true}),
        sec('testimonials',{title:'Quem usa, recomenda',columns:3,items:[{text:'Economizei horas toda semana.',author:'Marina Alves',role:'Designer'},{text:'Finalmente algo simples que funciona.',author:'João Pedro',role:'Desenvolvedor'},{text:'Minha equipe adotou em um dia.',author:'Carla Dias',role:'Gerente de produto'}]},{background:'surface',anchor:'Depoimentos',menu:true}),
        sec('pricing',{title:'Planos simples',subtitle:'Comece grátis e mude quando quiser.',items:[{name:'Grátis',price:'R$ 0',period:'/mês',description:'Para experimentar.',features:'Recursos essenciais\n1 projeto\nSuporte da comunidade',buttonLabel:'Começar',url:'#',highlight:false},{name:'Pro',price:'R$ 29',period:'/mês',description:'Para quem leva a sério.',features:'Tudo do Grátis\nProjetos ilimitados\nSuporte prioritário',buttonLabel:'Assinar Pro',url:'#',highlight:true},{name:'Equipe',price:'R$ 79',period:'/mês',description:'Para times.',features:'Tudo do Pro\nAté 10 pessoas\nAdministração',buttonLabel:'Falar com vendas',url:'#',highlight:false}]},{anchor:'Planos',menu:true,align:'center'}),
        sec('faq',{title:'Perguntas frequentes',items:[{q:'Preciso de cartão para começar?',a:'Não. O plano grátis não pede cartão.'},{q:'Posso cancelar quando quiser?',a:'Sim, em um clique, sem multa.'},{q:'Meus dados ficam seguros?',a:'Sim. Tudo é criptografado e você pode exportar a qualquer momento.'}]},{anchor:'Dúvidas',menu:true,width:'narrow'}),
        sec('cta',{title:'Pronto para começar?',text:'Leva menos de um minuto.',buttons:[btn('Criar conta grátis','#'),btn('Falar com a gente','#','secondary')]})])}},

    {id:'portfolio',name:'Portfólio',icon:'🎨',description:'Apresentação pessoal, projetos em cartões, trajetória e contato.',
      build:function(c){var t=c.title||'Seu Nome';return page({title:t+' — Portfólio',icon:'◆'},{preset:'grafite',background:'grid'},{brand:t,footer:'© '+year+' '+t},[
        sec('hero',{eyebrow:'Disponível para projetos',title:'Olá, eu sou '+t+'. Crio experiências **simples e bonitas**.',subtitle:'Designer e desenvolvedor. Transformo ideias em produtos que as pessoas gostam de usar.',buttons:[btn('Ver projetos','#projetos'),btn('Contato','#contato','secondary')],layout:'left',height:'tall'},{padding:'l'}),
        sec('cards',{title:'Projetos selecionados',columns:3,items:[{title:'Aplicativo de finanças',text:'Redesenho completo com foco em clareza.',tag:'Produto'},{title:'Identidade visual',text:'Marca, tipografia e sistema de cores.',tag:'Marca'},{title:'Site institucional',text:'Rápido, acessível e fácil de editar.',tag:'Web'}]},{anchor:'Projetos',menu:true}),
        sec('timeline',{title:'Trajetória',items:[{date:String(year),title:'Estúdio próprio',text:'Projetos para empresas e pessoas.'},{date:String(year-3),title:'Designer sênior',text:'Liderança de design em produto digital.'},{date:String(year-6),title:'Começo',text:'Primeiros projetos como freelancer.'}]},{anchor:'Trajetória',menu:true,width:'narrow'}),
        sec('contact',{title:'Vamos criar algo juntos?',text:'Conte sua ideia; respondo em até um dia.',email:'voce@exemplo.com',links:[{label:'GitHub',url:'https://github.com'},{label:'LinkedIn',url:'https://linkedin.com'}]},{anchor:'Contato',menu:true,background:'surface'})])}},

    {id:'article',name:'Artigo de uma nota',icon:'📄',needs:'note',description:'Publica uma nota como artigo elegante, com barra de progresso de leitura.',
      build:function(c){var n=c.note||{title:'Artigo',path:''};return page({title:n.title,icon:'✎',description:c.excerpt||''},{preset:'papel',width:1000},{nav:true,brand:n.title,progress:true,footer:'Publicado com Urbe · '+new Date().toLocaleDateString('pt-BR')},[
        sec('note',{path:n.path,showTitle:true,showMeta:true},{width:'narrow',padding:'l'})])}},

    {id:'vault-site',name:'Site de uma pasta',icon:'🗂️',needs:'folder',description:'Transforma uma pasta em site: capa, índice em cartões e o texto de cada nota.',
      build:function(c){var f=c.folder||'',t=c.title||(f?f.split('/').pop():'Minhas notas');return page({title:t,icon:'🗂️'},{preset:'lavanda'},{brand:t,progress:true,footer:'Gerado a partir de “'+(f||'vault')+'” com Urbe'},[
        sec('hero',{eyebrow:'Coleção',title:t,subtitle:'Todas as notas de **'+(f||'o vault')+'** reunidas num só lugar.',buttons:[btn('Começar a ler','#indice')],layout:'center',height:'auto'},{padding:'l'}),
        sec('notes',{title:'Índice',source:'folder',folder:f,layout:'cards',columns:3,limit:60,sort:'title',excerpt:true,expand:true},{anchor:'Índice',menu:true})])}},

    {id:'resume',name:'Currículo',icon:'💼',description:'Resumo, experiência em linha do tempo, habilidades e contato.',
      build:function(c){var t=c.title||'Seu Nome';return page({title:t+' — Currículo',icon:'💼'},{preset:'papel',mode:'light',headingFont:'fraunces',bodyFont:'dmsans'},{nav:false,themeToggle:false,footer:''},[
        sec('hero',{eyebrow:'Profissão · Cidade',title:t,subtitle:'Duas ou três frases sobre quem você é, o que faz de melhor e o que procura.',buttons:[btn('E-mail','mailto:voce@exemplo.com'),btn('Baixar PDF','#','secondary')],layout:'left',height:'auto'},{padding:'l',width:'narrow'}),
        sec('timeline',{title:'Experiência',items:[{date:(year-2)+' – hoje',title:'Cargo · Empresa',text:'- Resultado mensurável\n- Outro resultado'},{date:(year-5)+' – '+(year-2),title:'Cargo · Empresa',text:'- O que você conquistou'}]},{width:'narrow'}),
        sec('features',{title:'Habilidades',columns:3,items:[{icon:'🧭',title:'Liderança',text:'Times pequenos e autônomos.'},{icon:'🛠️',title:'Técnica',text:'Ferramentas que domina.'},{icon:'🗣️',title:'Idiomas',text:'Português, inglês.'}]},{width:'narrow'}),
        sec('timeline',{title:'Formação',items:[{date:String(year-8),title:'Curso · Instituição',text:''}]},{width:'narrow'}),
        sec('contact',{title:'Contato',text:'',email:'voce@exemplo.com',links:[{label:'LinkedIn',url:'https://linkedin.com'}]},{width:'narrow',padding:'s'})])}},

    {id:'event',name:'Evento',icon:'🎟️',description:'Capa com data, contagem regressiva, programação, palestrantes e inscrição.',
      build:function(c){var t=c.title||'Nome do Evento',d=new Date(Date.now()+30*864e5).toISOString().slice(0,10);return page({title:t,icon:'🎟️'},{preset:'entardecer'},{brand:t,navCta:'Inscrever-se',navCtaUrl:'#inscricao',footer:t+' · '+d},[
        sec('hero',{eyebrow:d.split('-').reverse().join('/')+' · Local do evento',title:t,subtitle:'Um dia inteiro de ideias, conversas e gente boa.',buttons:[btn('Garantir minha vaga','#inscricao'),btn('Programação','#programacao','secondary')],layout:'center',height:'screen'},{background:'gradient'}),
        sec('countdown',{title:'Faltam',date:d+' 09:00',done:'O evento começou!'},{padding:'s'}),
        sec('timeline',{title:'Programação',items:[{date:'09:00',title:'Abertura',text:'Boas-vindas e café.'},{date:'10:00',title:'Palestra principal',text:'Tema do evento.'},{date:'14:00',title:'Oficinas',text:'Mãos na massa em grupos.'},{date:'18:00',title:'Encerramento',text:'Networking.'}]},{anchor:'Programação',menu:true,width:'narrow'}),
        sec('testimonials',{title:'Palestrantes',columns:3,items:[{text:'Tema da fala.',author:'Pessoa Um',role:'Empresa'},{text:'Tema da fala.',author:'Pessoa Dois',role:'Empresa'},{text:'Tema da fala.',author:'Pessoa Três',role:'Empresa'}]},{anchor:'Palestrantes',menu:true,background:'surface'}),
        sec('faq',{title:'Dúvidas',items:[{q:'Onde será?',a:'Endereço completo e como chegar.'},{q:'Tem certificado?',a:'Sim, para quem participar do dia todo.'}]},{width:'narrow'}),
        sec('cta',{title:'Vagas limitadas',text:'Garanta a sua agora.',buttons:[btn('Inscrever-se','#')]},{anchor:'Inscrição'})])}},

    {id:'docs',name:'Documentação',icon:'📚',needs:'folder',description:'Sumário e as notas de uma pasta em sequência, com código e busca pelo menu.',
      build:function(c){var f=c.folder||'',t=c.title||(f?f.split('/').pop():'Documentação');return page({title:t,icon:'📚'},{preset:'oceano',width:1040},{brand:t,progress:true,footer:'Documentação gerada com Urbe'},[
        sec('hero',{title:t,subtitle:'Guia completo, organizado a partir das notas.',buttons:[btn('Começar','#conteudo')],layout:'left',height:'auto'},{padding:'m'}),
        sec('notes',{title:'Conteúdo',source:'folder',folder:f,layout:'list',limit:100,sort:'path',excerpt:true,expand:true},{anchor:'Conteúdo',menu:true,width:'narrow'})])}},

    {id:'book',name:'Livro',icon:'📖',description:'Livro pronto para imprimir ou gerar PDF: capa, folha de rosto, créditos, dedicatória, sumário, partes e capítulos.',
      build:function(c){var t=c.title||'O título do livro';return page({title:t,icon:'📖',description:'Um livro feito com o Urbe.'},{preset:'livro'},
        {format:'book',pageSize:'a5',margins:'normal',pageNumbers:true,runningHead:t,chapterStyle:'word',nav:false,footer:''},bookSections(t,[
        sec('part',{title:'O começo',markdown:'Onde tudo começa.'}),
        sec('chapter',{title:'A cidade de papel',epigraph:'Toda cidade é um livro que se lê andando.',epigraphAuthor:'Anônimo',markdown:'Escreva aqui o primeiro capítulo. O primeiro parágrafo ganha uma **letra capitular** e a primeira linha em versalete.\n\nOs parágrafos seguintes têm recuo na primeira linha, texto justificado e hifenização, como num livro impresso.\n\n***\n\nTrês asteriscos sozinhos numa linha viram um ornamento de troca de cena.'}),
        sec('chapter',{title:'Ruas e pontes',markdown:'Cada capítulo começa numa página nova. Para puxar o texto de uma nota, troque **Texto de onde** para **De uma nota**.'}),
        sec('free',{sheet:'page',root:P.free.make('box',{},{gap:'14px'},[P.free.make('heading',{text:'Uma página montada peça por peça',level:2},{textAlign:'center'}),P.free.make('text',{text:'Esta página é um **layout livre**: cada título, parágrafo, imagem ou caixa é uma peça que você move, aninha e estiliza.'}),P.free.PRESETS.destaque.build(),P.free.make('text',{text:'Qualquer bloco do livro também pode virar layout livre: menu ⋯ da seção → **Converter em layout livre**.'})])}),
        sec('part',{title:'O caminho'}),
        sec('chapter',{title:'Onde tudo se liga',markdown:'Para montar o livro inteiro a partir de uma pasta, use o bloco **Capítulos de uma pasta** ou o modelo **Livro de uma pasta**.'})]))}},

    {id:'book-folder',name:'Livro de uma pasta',icon:'📚',needs:'folder',description:'Cada nota da pasta vira um capítulo do livro, com capa, folha de rosto, sumário e créditos.',
      build:function(c){var f=c.folder||'',t=c.title||(f?f.split('/').pop():'Meu livro');return page({title:t,icon:'📖'},{preset:'livro'},
        {format:'book',pageSize:'a5',margins:'normal',pageNumbers:true,runningHead:t,chapterStyle:'word',nav:false,footer:''},bookSections(t,[
        sec('chapters',{folder:f,sort:'path',dropCap:true})]))}},

    {id:'links',name:'Links (bio)',icon:'🔗',description:'Página única e vertical com seus links principais — ótima para celular.',
      build:function(c){var t=c.title||'@seunome';return page({title:t,icon:'🔗'},{preset:'neon',width:560},{nav:false,backToTop:false,footer:''},[
        sec('hero',{title:t,subtitle:'Criador de conteúdo · links abaixo 👇',buttons:[btn('Meu site','https://exemplo.com'),btn('Instagram','https://instagram.com','secondary'),btn('YouTube','https://youtube.com','secondary'),btn('Newsletter','#','secondary')],layout:'center',height:'screen',stack:true},{padding:'m'})])}}
  ];

  function get(id){return LIST.find(function(t){return t.id===id})||null}

  /* variações de qualquer modelo (inclusive os da pessoa):
     simple   = só as partes principais, listas curtas, um botão por grupo;
     skeleton = os mesmos blocos e o mesmo visual, sem os textos de exemplo. */
  var OPCIONAIS_LIVRO=['copyright','dedication','about','colophon','part'];
  function limparCampos(fields,props){var o={};fields.forEach(function(f){var v=props[f.key];
    if(f.type==='list'){var first=(v||[])[0];o[f.key]=first?[limparCampos(f.fields,first)]:[]}
    else if(f.type==='text')o[f.key]=f.key==='title'?f.label:(f.key==='label'||f.key==='value'||f.key==='icon'?v:'');
    else if(f.type==='markdown'||f.type==='textarea')o[f.key]=/^(markdown|subtitle|text)$/.test(f.key)?'Escreva aqui.':'';
    else if(f.type==='image'||f.type==='url')o[f.key]='';
    else o[f.key]=v});return o}
  function encurtar(fields,props){var o={};fields.forEach(function(f){var v=props[f.key];
    if(f.type==='list'&&Array.isArray(v))o[f.key]=v.slice(0,f.key==='buttons'?1:2).map(function(it){return encurtar(f.fields,it)});else o[f.key]=v});return o}
  function variant(spec,mode){if(!mode||mode==='full')return spec;var s=JSON.parse(JSON.stringify(spec));
    if(mode==='simple'){var livro=s.layout&&s.layout.format==='book';
      if(livro)s.sections=s.sections.filter(function(x){return OPCIONAIS_LIVRO.indexOf(x.type)<0}).slice(0,6);
      else s.sections=s.sections.slice(0,4);
      s.sections.forEach(function(x){var b=P.BLOCKS[x.type];if(b)x.props=encurtar(b.fields,x.props)});
      if(s.layout){s.layout.navCta='';s.layout.navCtaUrl='';s.layout.progress=false}}
    else if(mode==='skeleton')s.sections.forEach(function(x){var b=P.BLOCKS[x.type];if(b)x.props=limparCampos(b.fields,x.props)});
    return P.normalize(s).spec}
  function build(id,ctx){var t=get(id);if(!t)throw new Error('Modelo desconhecido: '+id);var spec=t.build(ctx||{});return P.normalize(spec).spec}
  global.UrbePageTemplates={list:function(){return LIST.slice()},get:get,build:build,variant:variant};
})(typeof window!=='undefined'?window:globalThis);
