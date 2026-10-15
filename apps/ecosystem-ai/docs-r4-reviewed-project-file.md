# R4 — salvar resposta revisada no projeto

Slice de P6-5, Issue #579. No Android, **Salvar no projeto** em uma resposta
abre o nome do novo `.md`/`.txt`, seguido do conteúdo integral e da confirmação
**Confirmar e salvar**. Cancelar qualquer diálogo não grava nada. O arquivo
aparece em **Arquivos**, pode ser lido nas próximas tarefas e permanece ao
reabrir o aplicativo. A exportação pelo seletor Android continua disponível.

`ReviewedProjectFileHost` usa `LocalProjectStore` para localizar a resposta
por projeto, conversa e índice de turno. A proposta é imutável, vinculada
à instância do Host e não pode ser publicada por outro Host. A origem e o
workspace são relidos antes de publicar. Acrescentar mensagens à conversa
não invalida uma resposta anterior intacta.

O Host sela um `IsolatedWorkspace` e passa o `ChangeSet` ao **mesmo
`ChangeIntegrator` do Core**. O operador é o revisor independente do produtor
`host-response-export`; revisão, verificação exata do texto e autorização
referenciam a proposta exibida. Pareceres de modelo não autorizam esta ação.
Os IDs de tarefa/run do ChangeSet identificam a operação local de salvar,
não são recibos de inferência. Nenhuma execução fictícia é anexada à conversa.

O snapshot é deliberadamente limitado ao arquivo novo. A verificação do
combinado confirma nome/conteúdo; não afirma qualidade, cumprimento de uma
tarefa maior ou testes de projeto. O restante do workspace não é carregado
nem reescrito. Não há alteração do Core, grant de `files.write` ao agente,
contrato público, extração ou dependência em outro Product.

## Publicação e limites

- Somente nome simples portátil, até 96 caracteres, começando com letra ou
  número, extensão `.md`/`.txt`. Sem diretórios, nomes reservados Windows,
  arquivos ocultos, caminhos absolutos ou travessia.
- Arquivo, diretório ou link existente com nome equivalente, inclusive em
  outra capitalização, é conflito. Nunca substituir/excluir o destino.
  Um lock privado no catálogo serializa publicações de Hosts cooperantes,
  inclusive entre processos; não fica acessível ao agente no workspace.
- Workspace existente sem links simbólicos nos ancestrais, separado em ambas
  as direções do catálogo e journal. O uso previsto é o armazenamento privado
  Android; isto não é sandbox contra um processo hostil com o mesmo UID/root
  trocando diretórios entre syscalls.
- Texto UTF-8 sem BOM é gravado em temporário no mesmo diretório, descarregado
  em disco e movido para o destino com `overwrite: false`. A publicação de um
  arquivo é atômica; um destino criado concorrentemente causa recusa.
- Cancelamento/falha antes do rename não publica o destino. Depois do rename,
  a operação já foi efetivada e não é reportada como cancelada. Crash pode
  deixar um `.save-*.tmp`; não há replay nem remoção automática após restart.
- O resultado de publicação, com SHA-256, só é entregue após o rename. O recibo
  interno do integrador em memória não é exposto como publicação se o IO falhar.
  Propostas/recibos são efêmeros; o arquivo salvo é a autoridade do conteúdo.
  Conflito após restart continua recusado pelo filesystem, mesmo sem a memória
  dos IDs já publicados.
- Sem rede, token, modelo, orçamento ou execução automática. O arquivo contém
  a resposta inteira, incluindo eventuais cercas Markdown; não há parser que
  execute código ou adivinhe quais trechos devem ser extraídos.

Este incremento não integra mudanças em vários arquivos, não sobrescreve
arquivos existentes, não registra aprovação técnica de tarefas nem libera
dependências de `TeamPlan`. Auditoria durável da publicação e integração de
ChangeSets arbitrários continuam pendências de P6-5.

## Evidência

`ReviewedProjectFileTests` usa filesystem real para conferir bytes/hash,
revisão recusada, cancelamento, repetição, concorrência, nomes inválidos,
isolamento entre Hosts/projetos, rebind e links simbólicos. O smoke Android
usa uma resposta fictícia offline, sem recibo de inferência: confirma conteúdo
completo, cancela sem gravar, salva por gesto e reabre o arquivo após matar o
processo. CI não substitui toque/layout/lifecycle no moto g32 (NN-017).
