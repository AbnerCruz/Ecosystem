# LUNET-512 — Abertura segura de links externos (16/11/2026)

## Escopo
Correção isolada em `MainActivity.Inspiration.cs`: ao tocar em **Abrir página da referência**, o Android pode não ter navegador/handler instalado, ou pode bloquear o lançamento da Activity. Anteriormente a exceção escapava do callback e podia encerrar a IDE. Agora `ActivityNotFoundException` e `Java.Lang.SecurityException` são tratadas e o usuário recebe uma mensagem, sem modificar o catálogo, notas ou projetos.

## Verificação
- Inspeção estática do callback e dos tipos de exceção: realizada.
- Teste automatizado .NET/CI: **pendente**; este ambiente não possui SDK .NET nem emulador Android. Não declarar CI verde.
- DEVICE Android 13: **pendente**. Com navegador instalado, abrir um link HTTP(S) salvo; sem handler compatível (ou com restrição do Android), tocar no mesmo botão e confirmar mensagem sem crash; retornar à lista e verificar notas/favoritos preservados.
- Não modifica versionamento, stages, pipelines ou release.

## Handoff
- Repositório canônico: `AbnerCruz/Ecosystem`, `apps/lunet2d`.
- Item: LUNET-512 (gate DEVICE permanece aberto).
- Alterações: `src/Lunet.Android/MainActivity.Inspiration.cs`.
- Risco residual: Android UI precisa ser exercitada no aparelho; não há teste automatizado de Activity launch no conjunto atual.
- Próximo passo: acompanhar CI deste PR e corrigir qualquer falha; validar DEVICE; prosseguir LUNET-510/511 em PRs independentes.
- Normas consultadas: `MANIFEST.md`, `AGENTS.md`, `ecosystem.json`, `apps/lunet2d/AGENTS.md`, `ROADMAP.md`, `docs/DEVELOPMENT.md`, auditorias de 15/11; invariantes NN-001, NN-008, NN-017.
