# ADR 0011 — Primeiro corte de networking do Lunet

**Data:** 2026-10-29  
**Estado:** Experimental, condicionado a CI, auditoria e validação Android  
**Escopo:** Fase 7 / §8 do SPEC; não encerra o gate de multiplayer.

## Contexto

O Lunet dispõe de jogo C# e exportação standalone, mas nenhum runtime multiplayer utilizável. O proprietário pediu suporte progressivo a todas as modalidades e pesquisa anterior ao código. As soluções de Godot, Mirror, FishNet e Unity separam gameplay, autoridade e transporte. LiteNetLib oferece transporte UDP com confiabilidade seletiva, adequado à etapa realtime; não é preciso inventar reliability UDP agora. Gaffer demonstra por que snapshots realtime não devem depender de TCP.

## Decisão incremental

1. Primeiro demonstrar a fronteira de segurança (input do cliente / estado exclusivo do host) e o protocolo com testes de dois peers reais, rejeição de frames inválidos e limite de 8 KiB.
2. Criar o projeto independente `src/Lunet.Networking` com referências explícitas no Framework, solução, testes e compilador Roslyn, conforme o SPEC. O empacotador standalone deve receber a dependência transitivamente e será validado em CI.
3. `TcpGameHost` / `TcpGameClient` servem apenas a LAN, baixa frequência e testes. O contrato público não promete predição, UDP, criptografia, autenticação nem uso em servidor público. Não contam como gate realtime.
4. Não alterar saves, projeto, código dos jogos existentes, GameHost, assinatura ou formato do APK; declarar `INTERNET` no manifesto do jogo standalone, requisito Android para rede opt-in.
5. Em uma próxima etapa criar `INetworkTransport` estável, adaptar LiteNetLib 2.1.x sem expor tipos, e só então implementar netcode realtime, replicação e segurança.

## Alternativas descartadas

- **Inventar sistema UDP confiável próprio na primeira fatia:** duplicaria mecanismos difíceis de acertar sem teste de perda/jitter, com maior risco de corrupção de sessão e consumo.
- **TCP como transporte definitivo para todos os gêneros:** bloqueio entre pacotes, inadequado para ações e snapshots de alta frequência.
- **Afirmar MMO/online pronto com duas conexões:** falso; faltam operações, shard/servidor, segurança, consistência e load testing.
- **Auto-hospedar internet sem auth/TLS:** expõe jogadores a vazamento/tampering e abuso.
- **Tornar a DLL independente sem validar dependências transitivas:** risco de APK sem dependência; exigimos smoke e teste de build antes da integração.

## Critérios de reversão

Se CI demonstrar quebra de compilação dinâmica, build offline, empacotamento Android ou regressão de IDE, **não integrar**; corrigir antes de publicar. Tests + CI não substituem DEVICE. Fase 7 permanece aberta até dois jogos reais rodando com simulação de latência e testes físicos.

## Referências

- https://docs.godotengine.org/en/4.6/tutorials/networking/high_level_multiplayer.html
- https://mirror-networking.gitbook.io/docs/manual/general
- https://docs.unity.com/en-us/multiplayer/netcode/netcode
- https://github.com/RevenantX/LiteNetLib
- https://gafferongames.com/post/udp_vs_tcp/
- https://developer.android.com/develop/connectivity/network-ops/connecting
