# Formato persistido e ownership

`atlasforge.world` versão **3**, JSON UTF-8. Uma única autoridade do relevo: `state.elevation`, array de números finitos em metros. `state.overrides` registra decisões autorais absolutas que devem coincidir com o relevo canônico; não é uma segunda versão de terreno. Dados derivados nunca são persistidos no projeto editável, pois são recalculados. Publicação é uma projeção imutável com derivados incorporados e não um projeto aberto pelo editor.

- `definition`: seed, ID da versão do algoritmo, planeta (raio m, oceanos %, inclinação °, base térmica °C), grid (dimensões, topologia, datum e unidade).
- `state`: nome, altitudes, entidades e máscaras; `species` contém até 24 espécies; `ecosystem` guarda os estoques e o ano simulado.
- `entities`: ID, tipo, nome, lat/lon, descrição, nota opcional e mapa filho opcional.
- `note`: identidade, nome do arquivo, Markdown incorporado, hash SHA-256 e data da cópia. Hash pode estar vazio somente para exemplo/legado sem digest calculado; não é assinatura nem garante origem.
- `localMap`: identidade, versão, escala, salas e arestas indexadas, com validação das conexões.
- `journal`: até 30 comandos de elevação em `undo` e em `redo`; cada mudança guarda índice, antes/depois e máscara anterior. Alterações de metadados não têm undo neste corte.
- `timeline`: até 12 marcos completos, contendo configuração, estado e diário, ID e pai anterior; ordem causal e unicidade validadas. O marco ativo pode ser ancestral de outros marcos preservados.

O parse limita entrada a 24 MiB, dimensões, quantidades, comprimentos e valores finitos; copia somente propriedades compreendidas. Topologia, datum, algoritmo ou versão futuros são recusados. O estado em memória só é substituído depois de validar e recalcular com sucesso; cancelamento/import inválido não substitui o mundo anterior. Nunca regrava arquivo de entrada: salvar faz um novo download. IndexedDB verifica transação e releitura; indisponibilidade/quota gera mensagem de fallback.

## Ruptura intencional na versão 0.6

Por determinação expressa do proprietário, o Atlas Forge 0.6 **não importa nem converte projetos v1/v2 ou mundos associados ao motor `spherical-causal-1`**. A única definição de gerador aceita é `spherical-tectonic-2`, mantido o envelope JSON de projeto versão 3 para novos mundos. Importadores rejeitam versões futuras e algoritmos desconhecidos com mensagens diferentes de versões antigas. Arquivos incompatíveis não são abertos, sobrescritos ou excluídos automaticamente. Nenhuma migração é executada.


## Segurança e limites conhecidos

Markdown é subconjunto de cabeçalhos, parágrafos, listas simples, ênfase e código; HTML ativo e links não são ativados. Publicação escapa `<` no JSON, incorpora somente JS confiável do viewer e impõe CSP com `connect-src 'none'`, `base-uri 'none'`, sem formulários/iframes/recursos remotos. Notas não podem fornecer código, regras mágicas ou assets executáveis.

Sem ZIP ou paths de arquivo extraídos, não há extração com path traversal. Não existe acesso automático a pastas/vault, arquivos internos de outros aplicativos ou envio de dados pela rede. SHA-256 é verificado na abertura de projetos e na recuperação local, inclusive nos marcos; hash ausente em exemplo/legado é indicado. SHA-256 é metadado de snapshot, não prova de autoria; arquivos não são assinados e o JSON não possui manifest global de integridade. Hashes por campo e arquivo binário/tile são propostas AF-013, não contratos oficiais.

## Refinamento autoral

Cada marco é validado pelas dimensões da sua própria definição, inclusive quando diverge da grade atual após refinamento. A operação explícita de refinar acrescenta marco original com o relevo, as edições e o diário, gerando nova grade com a mesma seed e o mesmo algoritmo tectônico. Não ocorre migração entre geradores. A autoridade de altura continua em `state.elevation`, com `state.overrides` como restrição autoral absoluta. O limite de arquivo é 24 MiB.


## Terreno contínuo e publicação 0.4

O formato de projeto atual é v3 e não armazena bitmaps do cache. A superfície detalhada de novos mundos usa o campo local versionado `spherical-detail-1`; o gerador planetário é `spherical-tectonic-2`. Uma futura troca de algoritmo exigirá seu próprio identificador, sem compromisso de migração dos mundos descartados. O campo local é derivado de seed, grade canônica e algoritmo; os nós regionais são âncoras exatas, incluindo overrides. Não existe nesta versão override métrico persistente. O cache não é uma segunda autoridade de elevação regional.

Publicações incorporam `surfaceModel` e o avaliador correspondente; o viewer recusa um identificador desconhecido. O viewer inclui o avaliador local para reconstruir blocos sob demanda, sem solvers planetários/editor/storage. O Worker é código confiável empacotado, permitido por `worker-src blob:`; dados/notas nunca são código do Worker. Campos solares derivados usam um valor por **linha de latitude**, não por célula: `solarAnnual`, `solarJune`, `solarDecember`; `solarModel` declara modelo/unidade. Não crescem com número de longitudes e são recalculados ao abrir o projeto.

## Biosfera e eras — formato v3

Projetos novos são salvos e lidos exclusivamente no formato v3 com motor tectônico. `species: []` e `ecosystem: null` identificam um mundo atual sem biosfera iniciada; a inicialização ecológica permanece explícita no editor. Nenhum leitor grava novamente o arquivo importado, e formatos anteriores são recusados.

Catálogo: ID ASCII estável (reservados `detritus`, `__proto__`, `constructor`, `prototype`), nome, papel trófico, habitat, dieta por IDs, massa individual kg, limites térmicos °C, chuva mínima mm/ano, produtividade kg/m²/ano, mortalidade /ano, consumo kg/kg/ano, assimilação 0–1, dispersão km/ano e manejo. Referências ausentes, autoconsumo, IDs duplicados e intervalos inválidos são recusados.

`ecosystem.model = resources-trophic-1`: grade independente de largura par até 36 e metade em latitude; arrays `nutrients` (kg N/m²), `detritus` (kg/m²), `biomass[id]` (kg/m²), ano inteiro e último balanço `lastEra`. Estoques têm comprimento exato, são finitos e não negativos. A grade ecológica não é a resolução do terreno nem de animais individuais. Refinamento do relevo preserva os estoques em sua própria grade.

Cada era cria primeiro um marco completo do estado anterior e limpa o diário atual de relevo; o diário anterior continua no marco. Resultado completo é validado antes do commit, inclusive limite de 24 MiB e 12 marcos. Cancelar termina o Worker. Introdução de espécies é aporte externo, não nascimento simulado; invalida o relatório da última era. Projetos e publicações preservam catálogo/estoques; viewer só consulta os dados, sem solver.
