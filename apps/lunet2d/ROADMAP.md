# Roadmap

Uma caixa só é marcada quando implementação, integração, documentação e teste do fluxo correspondente estiverem completos.

## Fase 0 — Fundação

- [x] Estrutura inicial, versão, documentação de decisão e CI de build.
- [x] CI produz APK compilado sem erros (run 36506821599; artifact `lunet-foundation-apk`).
- [ ] APK instala e abre em Android físico.
- [ ] Publicar primeiro APK testado em GitHub Releases.

## Fase 1 — Fatia vertical

- [ ] Criar projeto, persistir arquivos em pasta comum e exportar ZIP (implementação inicial, sem teste em Android).
- [ ] Editar C# com salvamento e recuperação (salvamento atômico e testado; recuperação após crash ainda não existe).
- [ ] Compilar C# com Roslyn no Android usando referências offline (compilação testada no CI em desktop; **não validada em aparelho**, ver ADR 0002).
- [ ] Carregar e executar o jogo compilado no Preview (carregamento e execução testados em desktop com backend em memória; Preview Android implementado, não validado em aparelho).
- [ ] Renderizar sprite com OpenGL ES 3.x e ler touch (backend GLES escrito; sem teste em aparelho).
- [ ] Fluxo completo criar → editar → Run → mover sprite no aparelho.

## Fases posteriores

- [ ] Framework Core (tempo, gráficos, matemática, input, conteúdo, áudio).
- [ ] IDE, Roslyn e documentação offline.
- [ ] Framework avançado e ferramentas Studio.
- [ ] Plugins em C#.
- [ ] Multiplayer e simulador de rede.
- [ ] Workspace agêntico opcional.
- [ ] Empacotamento local de APK assinado.
- [ ] Build avançado em nuvem, APK/AAB.
- [ ] Três jogos tutoriais completos.
- [ ] Atualizações e infraestrutura de assinatura.
- [ ] Hardening, beta e versão 1.0.

## Riscos a validar primeiro

1. Roslyn no Android e provisionamento offline de assemblies de referência.
2. Carregamento e isolamento do assembly gerado; comportamento de memória e ciclo de vida.
3. Renderer OpenGL ES 3.x e integração com Preview.
4. Empacotamento local de APK do jogo com recursos, alinhamento e assinatura.

Não trate o build do aplicativo Lunet como prova do build local dos jogos.
