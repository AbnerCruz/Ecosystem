# ADD-0017 — Corte direto do Urbe no Ecosystem, sem espelho

Decisão explícita do proprietário em 2026-10-05, nesta conversa.

Ao ser informado de que o corte seguro originalmente previa uma última versão-ponte no repositório antigo, o proprietário rejeitou essa exigência e determinou:

> “Não precisa não, que se foda. Bota tudo no ecosystem e foda-se a sincronização. Só eu fazer backup da minha cidade e o resto fds”

Interpretação operacional, restrita ao que é necessário para executar essa direção:

1. `AbnerCruz/Urbe` deixa de ser canal ativo de desenvolvimento, sincronização ou novas releases.
2. Não haverá versão-ponte no repositório legado.
3. O proprietário aceita perder compatibilidade de atualização in-place entre a instalação legada e o novo canal, desde que possa fazer backup/export da cidade e restaurá-la depois.
4. Web/PWA, APK e instalador Windows do Urbe passam a ser distribuídos diretamente pelo `AbnerCruz/Ecosystem`.
5. O repositório legado permanece apenas como histórico congelado; não é apagado.
6. O primeiro release direto pode reutilizar a versão `1.8.3-beta`, pois ela não foi publicada no canal legado.
7. Como a chave privada legada não é mais requisito de compatibilidade, o canal beta direto passa a usar uma chave de desenvolvimento pública, estável e específica do Urbe no Ecosystem. Essa chave preserva atualização entre builds **diretas** futuras, mas não prova autoria e não é uma identidade estável de produção.
8. A migração Android é deliberada: backup/export → desinstalar legado → instalar APK direto → restaurar/abrir a cidade. O pipeline e a documentação devem tornar essa quebra explícita.
9. O updater do app novo continua consultando exclusivamente releases `urbe-v*` do Ecosystem, nunca o `/releases/latest` global do monorepo.
10. A direção futura first-party de DEC-0021-C não muda; esta decisão apenas define o canal GitHub atual até essa plataforma existir.

Esta decisão **substitui**, somente para o Urbe, as partes de ADD-0015 / ADR-0019 / P4-9 que exigiam versão-ponte, mesma chave privada, sincronização e preservação do repositório antigo como canal alternativo operacional.

Rastreabilidade: DEC-0039, P4-9, Issue #166, ADR-0019.
