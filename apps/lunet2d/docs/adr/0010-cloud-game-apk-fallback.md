# ADR 0010 — Fallback GitHub Actions para APK de jogos Lunet

## Contexto

O Lunet possui compilador Roslyn e Preview executáveis no Android, mas não embarca o Android SDK completo com AAPT2, alinhamento, assinatura e .NET Android workload. O APK experimental do jogo (LUNET-445) comprovou que a toolchain oficial do GitHub Actions consegue criar um APK independente sem incluir a interface do editor. Isso não comprova impossibilidade de build local.

## Decisão provisória

Implementar geração de APK por workflow privado na conta do usuário, opt-in e com permissão explícita, sem alterar nem bloquear o Run/Preview offline. Usar o token já cadastrado no Git, exigir repositório privado, enviar snapshot ZIP da pasta do jogo, disparar build e retornar APK para exportação/instalação pelo Android. O token nunca é incluído no jogo ou no payload do workflow. Publicar código do usuário no Ecosystem não é permitido. O owner pode inspecionar/remover o repositório e os snapshots.

## Riscos e pendências

1. Ainda não existe packager local/offline; implementar como melhoria obrigatória da F9.
2. A assinatura de debug gerada no runner é temporária, não serve como identidade estável/produção. Criar chaves individuais persistentes, protegidas e com backup.
3. O token GitAccountStore fica em arquivo privado do aplicativo, sem integração Android Keystore; fortalecer antes da versão estável.
4. A API inicial usa uploads ZIP pequenos com teto operacional de 30 MiB; suportar streaming e projetos maiores.
5. GitHub Actions depende de rede, permissões, billing/limites e disponibilidade da conta do usuário.
6. Workflow aponta para main do Ecosystem; fixar versões oficiais do runtime por release/commit para builds reprodutíveis.
7. Testes automatizados e APK smoke não validam o fluxo real de autorização/instalação em dispositivo. O gate F9 permanece aberto.

## Compatibilidade

O formato do projeto e as APIs Game/Content/Assets não mudam. A geração de APK cloud é opcional; não envolve o Hub nem o Urbe e não incorpora a IDE no APK do jogo.
