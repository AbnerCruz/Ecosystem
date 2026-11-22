# Lunet Aprender — matriz de implementação

**Item principal:** LUNET-520 (Aprender). **Complemento:** LUNET-521 (IA da área de trabalho).  
**Estado deste documento:** plano de execução. **ATENÇÃO:** caixas técnicas marcadas apenas após CI e integração em main; criar código numa branch não equivale a concluir.  
[Manifesto](MANIFESTO.md) · [60 cursos/120 projetos](MATRIZ-CURRICULAR.md) · [Pesquisa](PESQUISA.md) · [ROADMAP canônico](../../ROADMAP.md).

## Fase A — primeira entrega utilizável

- [ ] **LUNET-520-A01:** exibir os 60 cursos sob 12 trilhas na aba Aprender; somente primeiro curso tem o primeiro projeto habilitado. Testes de quantidade e IDs.
- [ ] **A02:** os 59 cursos seguintes e o segundo projeto de LRN-001 mostram “Disponível em breve”; nenhum botão finge iniciar conteúdo inexistente.
- [ ] **A03:** primeiro desafio mostra objetivos claros: desenhar quadrado, movimentar, posicionar círculo, vencer ao encostar, reiniciar.
- [ ] **A04:** criar e associar projeto normal via ProjectStore, preservando GameId e código do usuário.
- [ ] **A05:** marcar/desmarcar etapas por declaração; persistir progresso e recuperar após reinício.
- [ ] **A06:** mostrar progresso (0–100%) de quatro objetivos declarados, não da plataforma inteira nem verificação automática.
- [ ] **A07:** preservar arquivo corrompido, informar erro e não apagar projetos ao abrir Aprender.
- [ ] **A08:** navegar Início / Projetos / Studios / Explorar / Aprender, inclusive restauração e Back.
- [ ] **A09:** link de ajuda contextual abre chat de projeto no editor, somente com solicitação de pista.
- [ ] **A10:** garantir texto acessível, alvos tocáveis, rolagem incremental dos 60 cursos, operação sem login e sem rede.
- [ ] **A11:** testes xUnit dos dados, catálogo, transições e regressões dos projetos.
- [ ] **A12:** CI C# e Android verde no estado combinado com main, documentação/guia e integração automática autorizada.

## Fase B — IA global e divisão de responsabilidades

- [ ] **LUNET-521-B01:** adicionar chat geral como ação dedicada da Home, sem exigir projeto.
- [ ] **B02:** configuração de provedor, chave, permissões, modo e parâmetros de execução acessível apenas na Home.
- [ ] **B03:** projeto mostra conversa e troca de modelo, sem abrir diálogo completo de configuração.
- [ ] **B04:** manter conversa global e conversas por GameId isoladas, sem memória cruzada.
- [ ] **B05:** chat global textual não tem ferramentas de projeto nem permite escrita/leitura indireta de arquivos; orientação explícita de escopo.
- [ ] **B06:** armazenar histórico global privado sem chaves em claro; persistir entre reinícios, limpar mediante confirmação.
- [ ] **B07:** usar configuração de provider existente sem introduzir segundo armazenamento de segredos, respeitando validadores de endpoint.
- [ ] **B08:** tratar erros de rede, cancelamento, payload excessivo e arquivo de histórico danificado.
- [ ] **B09:** testes de persistência, isolamento, requisição HTTP simulada e falha de provider.
- [ ] **B10:** CI completo, documentação e integração na main sem alegar teste no aparelho não executado.

## Fase C — evolução do sistema de ensino

- [ ] **C01:** catálogo declarativo versionado com esquema, APIs mínimas, pré-requisitos e IDs imutáveis, sem duplicação manual entre código e documentação.
- [ ] **C02:** migrar estado do aluno entre versões sem perder conclusões, criar export/backups.
- [ ] **C03:** introduzir verificação automática confiável de objetivos mensuráveis, separada de confirmação do usuário.
- [ ] **C04:** revisão opcional por IA e desafios de transferência sem executar o trabalho pelo aluno.
- [ ] **C05:** progressão por competências e sugestões personalizadas com proteção de privacidade.
- [ ] **C06:** implementar LRN-001-P2 e liberar integralmente LRN-001 só após evidências de execução de ambos.
- [ ] **C07:** liberar demais 59 cursos por matriz de projetos e requisitos, cada um mantendo “Disponível em breve” até sua própria conclusão.
- [ ] **C08:** testes gráficos/UX, acessibilidade, navegabilidade tablet/desktop quando suportado, profiler e integração real de Studio/IA.
- [ ] **C09:** auditoria final de abrangência: C#, gráficos, shaders, física, áudio, UI, simulação, dados, IA, redes, produção e publicação 2D.

## Critério de revisão da matriz

Ao integrar cada incremento, o agente deve registrar PR/commit/CI e qual caixa ficou pronta; se a implementação for parcial, manter [ ]. Os checkboxes das **120 entregas pedagógicas** estão exclusivamente na [MATRIZ-CURRICULAR.md](MATRIZ-CURRICULAR.md); um projeto planejado nunca é apresentado como concluído por publicar este Markdown. Uma nova release pode trazer somente LRN-001-P1 disponível, sem mudar os 119 demais.

## Pontos de atenção

- A navegação compacta não pode apresentar seis destinos. Chat global é entrada pela Home.
- O projeto de estudo criado pelo usuário deve permanecer código C# real, abrível fora do Aprender.
- Uma etapa declarada não prova sua execução, e uma implementação feita pela IA não comprova domínio independente.
- Os eventos de IA de um projeto não devem aparecer automaticamente no chat geral.
- Configuração de chave e permissões nunca vai para a pasta do jogo.
- O CI da main é autoridade para release; não criar tag e APK manual com código não validado.
