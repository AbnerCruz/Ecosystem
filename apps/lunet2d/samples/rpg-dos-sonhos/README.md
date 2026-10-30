# RPG dos Sonhos — fatia 1

LUNET-461 / Issue #862. Jogo C# exclusivamente Lunet2D. Não é o produto Tabletop RPG, nem mudança da IDE. Fonte técnica auditada: Ecosystem `f2d67c4de77385e49c91cd73760ec468895e7799`, `apps/lunet2d/VERSION` = 0.0.1. Isso identifica o código consultado; não prova qual versão está instalada no telefone do autor.

## Auditoria e escolha da fatia

Seis notas anexadas foram lidas: RPG dos Sonhos; Pilares e Progressão; Mundo Procedural e Dungeons; NPCs Economia e Ofícios; Combate Saúde e Vida Cotidiana; Roadmap e Prompt Lunet. O anexo Protótipo HTML e Caderno de Teste estava indisponível. Cosmologia, Linha-do-Tempo-Universal, Droerni/Brainstorm/Observações e Lunet2D-prompt-raiz não foram fornecidos. Não inventamos seu conteúdo nem resultados de playtest.

| Classificação | Uso nesta fatia |
|---|---|
| Requisito confirmado pelo autor | Android, 2D top-down, combate realtime, offline, procedural; sete dungeons/portais são meta futura, não sete salas prontas |
| Resultado verificado | Evidências de execução serão CI e testes identificados no PR; ausência de .NET, adb e dispositivo local registrada |
| Proposta em teste | Coleta → produção por NPC → preparo → exploração por pista → combate anunciado |
| Pendente | Nome final, arte, campanha, política de seed, controles, morte e salvamento finais; nada de lore de Droerni fixado |
| Implementação provisória | Portrait 480×800, região 24×24, 4 arbustos finitos, 2 NPCs, 1 inimigo, uma ruína aberta; sem interiores separados |

**Escolha:** uma cadeia causal completa, em vez de sistemas econômicos numerosos sem consequências. Cozinheira tem objetivo observável (aguardar insumo → ir ao fogão → produzir → retornar); batedor é informante estático, não A-Life completo. O personagem precisa entregar frutos no ponto de recebimento da vila, marcado por círculo, mesmo quando a cozinheira está no fogão.

**Aceites:** iniciar pelo Lunet; mover e colidir; colher sem gerar recurso infinito; entregar e esperar preparo visível; receber refeição e curar; entender caminho por relato ou placa; atacar, sair do círculo anunciado e vencer; pausar sem avançar simulação; fechar/reabrir e recuperar estado; save inválido permanece preservado e bloqueia gravação nessa sessão.

## Iniciar e jogar

Importe um ZIP desta pasta com `lunet.json` na raiz na tela de projetos do Lunet; abra e toque Run. Framework, compilador e runtime são os reais do Lunet, sem engines externas. O projeto não exige assets externos, internet, IA ou conta. A importação não instala outra IDE. Para gerar o APK no Lunet: Arquivo → Gerar APK usando a distribuição com packager offline. Este caminho precisa ser retestado no telefone; não está automaticamente aprovado por testes headless.

1. Use o analógico inferior esquerdo. `USAR` interage perto de uma pessoa, placa ou arbusto. `ATACAR` golpeia num raio de 54 unidades; não exige mirar com mouse. `COMER` consome provisão quando há dano.
2. Saia da vila para o norte: os quatro arbustos estão próximos. Cada um dá um fruto, uma única vez.
3. Entregue um fruto no círculo da cozinheira. Ela se move até o fogão e leva quatro segundos para produzir. Volte ao círculo para receber a provisão.
4. Fale com o batedor (PISTA). Siga ao norte até a ponte, cruze para leste e acompanhe a trilha ao norte. A placa da ponte repete a orientação: não existe requisito de possuir um item único nem marcador automático de destino.
5. Na ruína, ataque de perto e recue do círculo vermelho antes do impacto. Golpes têm intervalo; tocar freneticamente não aumenta dano. Ao derrotar o guardião, a mensagem confirma o fim da fatia. A ruína NÃO representa um dos sete portais.
6. `II` pausa e grava. `>` retoma. Ao voltar do segundo plano, o jogo permanece pausado até tocar `>`. Autosave a cada dez segundos e no encerramento normal quando o host chama o lifecycle.

## Salvamento e recuperação

`DreamState` versão 1 guarda seed, posição, vida, recursos, arbustos, pistas, inimigo derrotado e produção. O mapa é regerado pela seed; inimigo vivo retorna ao ponto inicial, cooldowns e animações reiniciam. Não existe avanço offline nem relógio real.

Usa `Game.Save`/`SaveData`, cuja implementação `DirectorySaveStore` grava temporário + rename. O host standalone auditado fornece uma pasta privada persistente; um host que só forneça `MemorySaveStore` não preserva entre processos. A mensagem “Estado gravado” confirma leitura de volta nesse store, não garante sincronização física contra falta de energia.

JSON inválido, versão incompatível, posição inválida ou valores fora de faixa: original preservado; abre sessão temporária com aviso e gravação bloqueada. Recuperação automática/migração/exportação de save não implementadas. Não há botão para apagar saves. Derrota recua à vila, restaura vida e preserva recursos: escolha reversível de teste, não regra de morte final.

## Verificação reproduzível

No repositório Ecosystem, com .NET 10:

```sh
cd apps/lunet2d
dotnet test --project tests/Lunet.Tests -- --filter-class '*RpgSonhosSliceTests'
dotnet build runtime-template/Lunet.GameCode.csproj -c Debug -p:GameSourceRoot="$PWD/samples/rpg-dos-sonhos"
```

Para APK pelo template real do Lunet (workload Android e SDK instalados):

```sh
dotnet build runtime-template/Lunet.Game.Android.csproj -c Debug -p:GameSourceRoot="$PWD/samples/rpg-dos-sonhos" -p:ApplicationId=io.lunet.rpgsonhos.slice1 -p:ApplicationTitle=RpgSonhos
```

`RpgSonhosSliceTests` compila Game.cs/DreamWorld.cs pelo GameCompiler, carrega GameLoader e executa GameHost com RecordingBackend. Verifica 64 seeds e acessibilidade, produção/cura/escassez, pistas independentes, alcance/cooldown e esquiva, touch real do InputState, pausa, persistência em disco e corrupção. Backend registrador não é aparelho Android, não mede desempenho GPU nem aprova toque/layout. Resultados reais e commits no PR/handoff.

## Limites e próximo passo

Arte geométrica placeholder, sem áudio, anatomia, fome passiva, habilidades, comércio, preços, guildas, agricultura, genética, sete dungeons ou multiplayer. Layout e rotas principais fixos; seed varia árvores/obstáculos, não gera continentes nem campanhas. A rede de pistas nesta fatia é relato + placa redundantes até um único destino, não uma cadeia de várias dungeons. Batedor estático. Estoques não regeneram. Não há ajuste de dificuldade nem menu de nova campanha. O código das regras está separado da apresentação porque permite testar causalidade sem GPU; nenhuma abstração nova de framework foi criada.

Próximo marco: validar o loop no celular (legibilidade, alcance, posicionamento dos botões, ritmo de preparo, pausa/process death e retomada) e só então escolher a expansão. Não aumentar quantidade de sistemas antes de observar a experiência.
