# Patch Notes e histórico — P4-8

**Release é do Product; Patch Notes são da release; Hub apenas descobre e apresenta.** Não existe catálogo manual de notas no Hub nem escrita na fonte.

## Fonte e boundary

O perfil `current` e `ecosystem.json` continuam definindo os canais. SOURCE é `apps/<id>` no monorepo; os repositórios históricos continuam sendo canais de DISTRIBUTION. Nenhum canal, pipeline, chave, contrato compartilhado ou Product muda por esta feature.

Os pipelines atuais publicam suas notas como `body` de GitHub Releases, geradas de CHANGELOG/release-notes. Esse campo é suficiente para ambos os formatos existentes. `ReleaseInfo` foi estendido com `BodyMarkdown` opcional; tag/título/data/prerelease/artefatos/hash já existiam. Assets auxiliares como release manifest, notes JSON e SHA256SUMS continuam listados como arquivos da fonte; não viram segundo catálogo nem são consultados automaticamente quando o body já fornece as notas. SHA-256 exibido continua **informado**, sem inferir verificação dos bytes.

`IReleaseProvider` é um boundary **local do Hub**, usando o modelo de releases já existente. `GitHubReader` é o adapter atual; o loader admite provider injetado. Histórico, parser, comparação e UI não dependem de tipos de resposta, repositórios ou endpoints GitHub. Uma fonte futura implementa o provider e a resolução de canal correspondente; não exige reescrever a feature nem adotar um contrato transversal prematuro. O arquivo legado `GitHubModels.cs` continua contendo `ReleaseInfo` para compatibilidade, sem criar um modelo duplicado só por causa do nome do arquivo.

## Navegação e estado

Products → **Ver mudanças** → histórico vertical → versão → notas. Lista inicialmente dez entradas; **Mostrar mais 10 versões** só expande os dados já consultados. O provider consulta no máximo as 100 releases mais recentes, sem paginação de rede ilimitada, filtra drafts e conserva pré-lançamentos publicados pelo canal. Em repositório com múltiplos Products, o prefixo declarado é aplicado antes da apresentação.

Histórico é ordenado por data de publicação descendente. Datas iguais/ausentes usam SemVer descendente quando comparável, depois tag como desempate determinístico; tag desconhecida permanece visível, sem inventar versão. Prefixo declarado e `v` são removidos apenas para apresentação/comparação; tag original e link ficam nos detalhes da fonte.

A tela separa **versão no SOURCE**, **mais recente publicada** e **instalada**. Versão do arquivo no repositório nunca vira versão instalada. O boundary aceita `Datum<string>` de uma consulta real futura ao sistema; hoje a UI informa **não conhecida — ainda não consultada**. Não há detecção de update, instalação, alteração de visibilidade Android ou novo permission model.

Loading, loaded, empty, unavailable/error e cache stale são distintos. `[]` válido significa nenhuma release; HTTP/rede/JSON/estrutura inválidos significam indisponível, com motivo. Um Product sem canal não é tratado como se tivesse uma release fictícia.

## Markdown e camadas

Markdig 1.4.0 interpreta Markdown, sem template obrigatório. Texto livre/sem body continua válido. Cabeçalhos comuns Added/Changed/Fixed e Novidades/Melhorias/Correções são reconhecidos; Breaking changes fica visível e Technical/Detalhes técnicos/commits/testes/migrações têm seção expansível. Categorias repetidas e desconhecidas preservam o conteúdo; cabeçalho dentro de bloco de código não vira categoria.

Widgets nativos apresentam títulos, listas ordenadas/não ordenadas, parágrafos, ênfase/negrito, código, citações, entidades, links HTTPS e tabelas simples com linhas que quebram no celular. HTML é mostrado literalmente, sem execução; imagens mostram texto alternativo, sem buscar recurso remoto. Links relativos ou de protocolo não HTTPS não são executados; a release original continua acessível. O Markdown original é preservado byte a byte como texto no modelo/cache, independentemente da projeção visual.

Detalhes da fonte/integridade e notas técnicas ficam expansíveis. Artefatos abrem a URL publicada no aplicativo adequado por toque; o painel P4-2/P4-7 continua sendo o fluxo já existente de download conferido. Não fabrica PR → release, commits, testes, resumo por IA ou notas quando a fonte não as publica.

## O que mudou desde uma versão

`ReleaseHistoryBuilder.Since` recebe a versão instalada explicitamente. Para SemVer comparável, seleciona releases posteriores e ordena em ordem crescente, preservando cada body, data, versão e proveniência. Inclui pré-lançamentos publicados, compara identificadores numéricos (ex.: beta.10 depois de beta.2) e ignora build metadata na precedência conforme SemVer.

Tags não comparáveis continuam no histórico e geram aviso na agregação. Base ausente no intervalo de 100 gera aviso de incompletude. Resultado vazio não comprova que não existe update fora do intervalo/canal. Sem versão instalada conhecida/comparável, não agrega nem infere atualização. Nenhum resumo sofisticado ou contagem funcional é inventado.

## Cache

Reutiliza `SnapshotCache`, projeção descartável. Bodies entram no mesmo snapshot e caches antigos sem esse campo continuam aceitos. Leitura totalmente indisponível usa o snapshot anterior marcado stale. Falha parcial de releases usa apenas o histórico salvo do **mesmo canal resolvido**; preserva motivo da falha e origem/stale, inclusive ao persistir esse fallback.

Resposta vazia válida substitui histórico antigo. Troca/ausência de canal impede misturar fontes antigas; não usa o perfil `target` como canal corrente. Sem cache confiável, mostra indisponibilidade. O Product e o restante do Hub continuam independentes da consulta de notas.

## Conferência mobile

Use APK contendo P4-8 (candidato do CI ou release após integração).

1. Abra cada Product em **Ver mudanças**; confira nome, versão SOURCE, instalada desconhecida, mais recente, datas e pré-lançamentos.
2. Abra notas estruturadas e texto livre; confira títulos/listas/links/código, rolagem e legibilidade, sem tabela larga. Expanda detalhes técnicos e volte ao histórico.
3. Confira artefatos e link original; nenhum instalador/update automático deve iniciar.
4. Expanda mais dez versões quando houver; não deve carregar histórico infinito da rede.
5. Depois de carregar online, teste offline/consulta indisponível: último histórico e notas devem estar identificados como cache. Sem cache, mostrar indisponível; Product sem releases deve mostrar vazio real.
6. Confira retorno/dismiss/reabertura durante refresh e com download em segundo plano. Essa validação de toque/layout/lifecycle é humana (NN-017), separada do CI e do gate de instalação P4-4.
