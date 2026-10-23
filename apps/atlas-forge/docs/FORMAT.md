# Formato persistido e ownership

`atlasforge.world` versão **2**, JSON UTF-8. Uma única autoridade do relevo: `state.elevation`, array de números finitos em metros. `state.overrides` registra decisões autorais absolutas que devem coincidir com o relevo canônico; não é uma segunda versão de terreno. Dados derivados nunca são persistidos no projeto editável, pois são recalculados. Publicação é uma projeção imutável com derivados incorporados e não um projeto aberto pelo editor.

- `definition`: seed, ID da versão do algoritmo, planeta (raio m, oceanos %, inclinação °, base térmica °C), grid (dimensões, topologia, datum e unidade).
- `state`: nome, altitudes, entidades e máscaras; `species` reservado vazio e recusado se houver dados que o aplicativo ainda não compreende.
- `entities`: ID, tipo, nome, lat/lon, descrição, nota opcional e mapa filho opcional.
- `note`: identidade, nome do arquivo, Markdown incorporado, hash SHA-256 e data da cópia. Hash pode estar vazio somente para exemplo/legado sem digest calculado; não é assinatura nem garante origem.
- `localMap`: identidade, versão, escala, salas e arestas indexadas, com validação das conexões.
- `journal`: até 30 comandos de elevação em `undo` e em `redo`; cada mudança guarda índice, antes/depois e máscara anterior. Alterações de metadados não têm undo neste corte.
- `timeline`: até 12 marcos completos, contendo configuração, estado e diário, ID e pai anterior; ordem causal e unicidade validadas. O marco ativo pode ser ancestral de outros marcos preservados.

O parse limita entrada a 24 MiB, dimensões, quantidades, comprimentos e valores finitos; copia somente propriedades compreendidas. Topologia, datum, algoritmo ou versão futuros são recusados. O estado em memória só é substituído depois de validar e recalcular com sucesso; cancelamento/import inválido não substitui o mundo anterior. Nunca regrava arquivo de entrada: salvar faz um novo download. IndexedDB verifica transação e releitura; indisponibilidade/quota gera mensagem de fallback.

## Migração v1

Baseada exclusivamente no contrato documentado no mandato; **não foi auditado o HTML real da Alpha**. Fixture `fixtures/alpha-v1.synthetic.json` é sintética, identificada como tal. Alturas U16 em base64 são lidas little-endian e convertidas por `((u16/65535)*2−1)*6.000 m`. Essa escala é uma convenção de migração aproximada, não recuperação de medidas físicas da Alpha. O corpus real pode exigir ajuste do decoder antes de garantir compatibilidade geral.

Preserva seed, parâmetros (raio km → m), entidades/Markdown, elevação e snapshots `eras[].height`. Cada pixel importado vira override para evitar substituição por gerador desconhecido. `era` da Alpha não escolhe snapshot ativo automaticamente: o relevo atual do arquivo é a fonte canônica; todos os snapshots são oferecidos como marcos nomeados pelo ano. Mais de 12 snapshots são recusados com explicação, sem truncar história. IDs ausentes recebem IDs de migração estáveis por posição. Projetos com IDs duplicados são recusados.

## Segurança e limites conhecidos

Markdown é subconjunto de cabeçalhos, parágrafos, listas simples, ênfase e código; HTML ativo e links não são ativados. Publicação escapa `<` no JSON, incorpora somente JS confiável do viewer e impõe CSP com `connect-src 'none'`, `base-uri 'none'`, sem formulários/iframes/recursos remotos. Notas não podem fornecer código, regras mágicas ou assets executáveis.

Sem ZIP ou paths de arquivo extraídos, não há extração com path traversal. Não existe acesso automático a pastas/vault, arquivos internos de outros aplicativos ou envio de dados pela rede. SHA-256 é verificado na abertura de projetos e na recuperação local, inclusive nos marcos; hash ausente em exemplo/legado é indicado. SHA-256 é metadado de snapshot, não prova de autoria; arquivos não são assinados e o JSON não possui manifest global de integridade. Hashes por campo e arquivo binário/tile são propostas AF-013, não contratos oficiais.

## Compatibilidade da ampliação cartográfica 0.3

A representação JSON v2 e o algoritmo `spherical-causal-1` permanecem iguais. O leitor 0.3 amplia a faixa de grades aceitas para larguras pares 12–1.440 (altura = largura/2). Todos os projetos aceitos pelo leitor anterior continuam aceitos; o leitor 0.2 recusa grades acima de 360, sem reinterpretá-las. Não há conversão automática nem redução de resolução ao salvar.

Cada marco é validado pelas dimensões da **sua própria definição**, inclusive quando difere da grade atual depois de um refinamento. Isso corrige o uso incorreto das dimensões atuais para validar snapshots. Um refinamento novo acrescenta um marco original e inicia diário vazio para os novos índices; o diário anterior está integral no marco. Dimensões e autoridade das altitudes não são duplicadas em caches. O limite de 24 MiB não mudou; resultado de Worker e criação de marco são verificados antes de substituir o estado atual.
