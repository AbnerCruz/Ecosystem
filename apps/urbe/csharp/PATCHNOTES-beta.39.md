# Urbe 2.0.0-beta.39 — registro detalhado

**Versão canônica preparada:** 2.0.0-beta.39
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** workflow oficial, após CI e integração
**Identidade Android:** app.urbe.csharp, mesmo certificado e atualização por cima da beta.38

## Abas sob seu controle

- Abrir outra nota pelo Explorer, busca ou links substitui a aba atual. O histórico de navegação continua disponível em Voltar/Avançar, sem virar uma fila de abas.
- O botão **+** no topo abre a busca para escolher uma nota em uma aba adicional. Cancelar mantém tudo como estava; selecionar uma nota já aberta não duplica a aba.
- As abas extras continuam disponíveis. A navegação comum substitui apenas a aba ativa; escolher uma aba existente muda o foco para ela.
- Trocar ou fechar salva pelo mesmo caminho verificado do editor. Falhas de gravação mantêm o texto recuperável na sessão.
- A primeira restauração de uma sessão antiga abre somente a última nota ativa. As abas abertas explicitamente nesta versão são restauradas como foram escolhidas; abas fechadas não voltam sozinhas.

## Editor mais confiável

- Corrigida perda do rascunho mais novo após duas gravações sobrepostas, quando a primeira falhava.
- Frações e fórmulas altas usam a altura necessária, sem invadir o parágrafo seguinte. Equações largas podem rolar horizontalmente, inclusive no painel de fórmulas.
- Corrigido acesso a bitmap já descartado ao tirar e recolocar uma prévia de imagem na tela. A prévia mantém o zoom e reutiliza os bytes já lidos.

## Novo estúdio de Publicações

- Criação a partir de nota, pasta, Área de trabalho, configurações ou paleta. A pasta seleciona as notas existentes naquele momento, inclusive subpastas; novas notas não são adicionadas automaticamente à composição.
- Referências por identidade às notas reais: adicionar, reordenar e remover fontes; editar a nota no editor e voltar com o conteúdo atualizado. O projeto não guarda outra cópia autoral do texto.
- Composição com erro ao salvar pode ser recuperada ao fechar/reabrir o estúdio no mesmo vault carregado; há aviso e ação de tentar novamente. Conflitos bloqueiam sobrescrita e exportação silenciosas.
- HTML offline com matemática vetorial, autoria da IA preservada e comentários editoriais privados omitidos. Trechos de código mantêm o texto literal.
- **Imprimir ou salvar como PDF** usa o serviço de impressão da plataforma e as notas atuais. No Android, escolha “Salvar como PDF” e o destino no diálogo do sistema. No Windows, a composição abre no navegador: use Imprimir (Ctrl+P) e selecione o destino PDF. Preparar a impressão não significa que o arquivo já foi salvo.
- Os caminhos normais de criação passam a usar Publicações. Arquivos `.page.json` antigos continuam preservados, com visualização, exportação e acesso ao JSON.

## Limites desta beta

- Esta release não conclui o plano de consolidação inteiro. O estúdio ainda não oferece todos os formatos e recursos editoriais planejados.
- Recuperação durável após morte do processo e através de recarga/troca do vault permanece em desenvolvimento.
- A impressão desta entrega não equivale ao motor editorial profissional do CST-164; paginação final e resultado no aparelho precisam de verificação própria. EPUB, DOCX/PPTX e vídeo não são anunciados nesta versão.
- O pipeline de autoria e os módulos internos legados ainda têm etapas de retirada e adaptação no plano. Os dados existentes não são apagados.
- Builds e testes automatizados são registrados separadamente de toque, IME e ciclo de vida em Android real.
