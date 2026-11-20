# Inventário de dados gravados pelo Numa — #1005, fatia 2

**Data:** 20/11/2026 · **Base:** branch `ccr-f022efc2-0vtff7` sobre a `main` beta.63 · **Método:** leitura do código (constantes de caminho, chamadas de gravação e pastas criadas por ações). É **auditoria estática**: não medi arquivos produzidos no aparelho nem em execuções reais, e o que está em "Proposta" não está decidido.

Taxonomia: a do documento 01 da #1005 (conteúdo humano, objeto de produto, asset de apoio, área transitória, estado privado por vault, estado privado global, cache).

## 1. Dentro do vault, ocultos do Explorer (`.urbe/`)

Tudo que começa com `.` é ocultado pelo `FolderVaultStorage`/`AndroidVaultStorage`: não aparece no Explorer, mas vai junto em cópias e backups da pasta.

| Caminho | Quem grava | Classe | Observação |
|---|---|---|---|
| `.urbe/notas.json` | `NoteLibrary` | estado privado por vault | Autoridade de ids, datas e pastas (ADD-0028). |
| `.urbe/mapa.json`, `.urbe/mundo.json` | 1.8.4 (somente leitura aqui) | legado congelado | Nunca apagar nem reescrever (AGENTS §1.2). |
| `.urbe/area.json` | `AreaLayout` | estado privado por vault | Aparência e ordem das caixas da Área de trabalho. |
| `.urbe/constelacao.json` | `Constellation` | cache reconstruível | Posições derivadas das notas. |
| `.urbe/estudo/flashcards.json` | `FlashcardLibrary` (#1005) | objeto de produto | Conjuntos de flashcards do app. |
| `.urbe/trash.v2.json` (+ `trash.json`) | `LegacyTrash` | estado privado por vault | Lixeira. |
| `.urbe/history.v2.json` (+ v1) | `LegacyHistory` | estado privado por vault | Versões anteriores das notas. |
| `.urbe/journal.v2.json` (+ v1) | journal de operações | estado privado por vault | Recuperação de operação interrompida. |
| `.urbe/vault.json`, `identity.json`, `compatibility.json`, `tutorial.json` | identidade e compatibilidade do vault | estado privado por vault | |
| `.urbe/compositions*.json`, `.urbe/{key}*.json` | estados por chave | estado privado por vault | Precisam de dono documentado um a um. |
| `.urbe/backup/`, `.urbe/backup/import-*`, `.urbe/origens/` | backup e importação | asset de apoio / recuperação | Retenção não documentada aqui. |

## 2. Fora do vault, privados do app

| Caminho | Quem grava | Classe |
|---|---|---|
| `<LocalStatePath>.study.v1.json` | `MainView.Flashcards` | estado privado por vault (por chave de vault): progresso SM-2 |
| `<LocalStatePath>.readers.v1.json` | leitores | estado privado por vault: posição de leitura |
| pasta do Assistente (`UrbeApp.AssistantFolder`, `AssistantStore`) | conversas, anexos, memória | estado privado global/por vault |

**Atenção, fato do Android:** os dados privados do app somem ao desinstalar. Hoje o progresso dos flashcards está aí, e os conjuntos estão no vault. Ver a proposta P3.

## 3. Dentro do vault e **visíveis** no Explorer, criados pelo próprio app

| Caminho | Quem cria | Classe | Avaliação |
|---|---|---|---|
| `Personalização/` (`tema.json`, `editor.json`, `atalhos.json`, `AGENTS.md`, `plugins/`, `Skills/`, `estilos/`, `temas/`) | Personalização, plugins, skills | configuração por vault | Visível de propósito (1.8.4: editável à mão). Mistura configuração com o espaço humano: candidato à "Área do app". |
| `Notas rápidas/aaaa-mm.md` | `QuickNotes` | conteúdo humano | Correto: a pessoa pediu a nota. |
| `Modelos/` | modelos | conteúdo humano | Correto. |
| `Tutorial/` | tutorial do app | documentação do app | Os artigos vêm do app (`AppDocs`); as cópias no vault são legado da 1.8.4. Candidato a sair do Explorer. |
| `Publicações/*.publication.json` | Estúdio, `write_publication`, `create_video` | objeto de produto | Correto como objeto; o Explorer mostra o JSON. |
| notas de cena criadas por `create_video` | `AssistantVideoDirectionTools` | conteúdo editorial | Correto pela Numa 2.6 (a fala é nota real). |
| `Narração/<projeto>/Cena N.wav` | `render_video`, narração manual | asset de apoio | **Polui o Explorer:** são derivados da nota + voz, regeneráveis. Proposta P1. |
| `Vídeos/` (MP4) | `render_video`, `AssistantVideoTools` | resultado pedido | Correto: o MP4 é o que a pessoa pediu. |
| `Imagens/` | `generate_image` | resultado pedido | Correto. |
| `Estudos/*` (sessões de estudo) | `MainView.StudySessions` | objeto de produto | Visível como arquivo técnico de sessão. Proposta P2. |
| `Anotações de estudo.md` | `StartStudyAsync` sem nota aberta | conteúdo humano criado sem pedido | **Criado sem intenção explícita** (o documento 00 da #1005 aponta isso). Proposta P4. |

## 4. Propostas (não decididas; cada uma é uma fatia própria com teste)

- **P1 — Narração fora do Explorer.** Os WAV de narração passam para `.urbe/derivados/narracao/` (cache reconstruível) e o MP4 continua em `Vídeos/`. Projetos antigos: ler os dois lugares, sem mover bytes sem migração (doc 01 §5). **Restrição encontrada:** `VideoScript.IsVaultPath` recusa caminhos em `.urbe` de propósito (o modelo não aponta arquivos internos) e as gravações recusam segmentos ocultos. Então a P1 precisa de um tipo de referência própria para áudio derivado (resolvido pelo app, não pelo caminho escrito no projeto), não de afrouxar essa regra. É mudança de fronteira: ADR antes de implementar.
- **P2 — Sessões de estudo como objeto.** Listar as sessões na biblioteca de Estudo, como os flashcards, e parar de criar arquivos novos em `Estudos/`. Exige decisão sobre o local físico (ver P3).
- **P3 — Progresso que sobrevive à desinstalação.** Levar o progresso SM-2 para junto dos conjuntos (`.urbe/estudo/`), mantendo a chave por cartão. Hoje ele fica na área privada do app (seção 2). Muda modelo de dados: requer ADR (NN-011).
- **P4 — Estudo sem nota automática.** Abrir Estudo sem nota aberta mostra a fonte e pergunta antes de criar `Anotações de estudo.md`. Mudança de comportamento do produto: precisa de validação da direção pelo proprietário.
- **P5 — Área do app.** Uma tela em Configurações que mostra o que está em `.urbe/`, `Personalização/` e no armazenamento privado, com tamanho, origem e ações (doc 01 §4). Sem mover nada na primeira versão.

## 5. O que este inventário não cobre

- Arquivos gerados no aparelho em uso real (contagens de 0, 10, 100 e 1000 operações, doc 01 §5.1).
- Plugins de terceiros: o que um plugin grava depende dele.
- Retenção de `.urbe/backup/` e do journal.
