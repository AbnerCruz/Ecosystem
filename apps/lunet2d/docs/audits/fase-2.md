# Auditoria da Fase 2 — Framework Core

- Data: 2026-09-29
- Versão testada em aparelho: v0.0.1-dev.53 (Laboratório e Coletor de moedas), com correções na versão seguinte
- Aprovação: usuário ("pode considerar aprovada"), após o teste no aparelho

## 1. Gate (§28)

Critério: pequeno jogo 2D completo somente com código. **Aprovado.** O Coletor de moedas (fonte, gestos, som, salvamento, aleatório) roda no aparelho, e o Laboratório exercita música, sensores, controle, vibração, gestos de dois dedos, controles virtuais e a página de gráficos (mistura, shader, amostragem, recorte, alvo de desenho, pixel perfect, área segura).

## 2. Itens do ROADMAP

Todos os itens da fase estão `[x]` (34/34). Os que dependem de Android foram validados no aparelho pelo usuário; os de lógica portátil (JSON, atlas, localização, geometria, timers, pools) têm testes automáticos (185 no total). Itens movidos com motivo e destino:

| Item | Destino | Motivo |
| --- | --- | --- |
| Baixa latência com Oboe/AAudio | Fase 14 | só vale se a revisão de desempenho medir latência ruim |
| Contador de audio underruns | Fase 4 (Profiler) | é métrica do Profiler |
| Recriar texturas/alvos/shaders ao perder o contexto GL | Fase 13 | hoje o jogo reinicia; `PreserveEGLContextOnPause` cobre o caso comum |
| Idioma do aparelho na `Localization` | Fase 13 | precisa de aparelhos com idiomas diferentes |
| Pipeline de conteúdo com cache em disco | Fase 5 (Atlas Studio) | só faz sentido com etapa de processamento real |

## 3. Requisitos do spec ainda não cobertos (§7)

`Font` além da fonte embutida, `Camera2D`, animação, tweening, partículas, tilemaps, UI, pathfinding, física: todos previstos na Fase 4. Nenhuma lacuna nova encontrada para a Fase 2.

## 4. Princípios (§2)

- Offline first: sim (nada da fase usa rede).
- Sem conta/telemetria: sim.
- Projetos como arquivos normais: sim (`Content/`, JSON, PNG, WAV).
- APIs públicas sem detalhes internos: `IGraphicsBackend`/`IAudioBackend` são o contrato; tipos de OpenGL, SoundPool e MediaPlayer não vazam.
- Ferramentas oficiais e plugins: não se aplica ainda (Fase 6).

## 5. Qualidade (§30)

- Sem TODO crítico escondido: verificado por busca no código.
- API pública com XML doc: sim (CS1591 silenciado apenas para membros triviais).
- Sem alocação por quadro no caminho quente: teste automático.
- **Bug encontrado no teste de aparelho:** perda de código ao sair do Preview (causa: salvar o texto de um editor recriado vazio). Corrigido com `EditorSession` + testes de regressão. Lição registrada: qualquer operação de escrita em disco precisa saber qual arquivo o editor realmente carregou.
- Bug de interface: opções de "Novo projeto" não desmarcavam (RadioButton sem id). Corrigido.

## 6. Arquitetura

`ArchitectureTests` verdes; ADRs 0001–0004 em dia.

## 7. Regressão

Suíte completa verde no CI; APK instalado e atualizado pelo usuário. Regressões da Fase 1 (Explorer, recuperação após crash, exportar ZIP) **ainda sem validação em aparelho**: continuam pendentes para fechar a Fase 1.

## 8. Documentação

README, CHANGELOG, ROADMAP e referência rápida do app atualizados.

## 9. Riscos (§32)

Roslyn no Android, Preview e renderer: validados. Risco 4 (geração de APK do jogo): **não validado**, entra na Fase 9; considerar antecipar um experimento de viabilidade.

## 10. Pendências herdadas e próxima fase

Validar a Fase 1 (Explorer, recuperação, ZIP) e seguir para a Fase 3 (IDE).

## Decisão

Fase 2 **concluída**.
