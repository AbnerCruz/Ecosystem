# ADD-0015 — Builds e releases diretamente no Ecosystem

Decisão explícita do proprietário em 2026-10-03, nesta sessão com Codex.

Após explicar o fluxo direto por produto no Ecosystem, preservando versões,
assinaturas e requisitos de build, o proprietário respondeu: **“Ok quero fazer isso”**.
O contexto era eliminar a sincronização intermediária para todos os seus apps.

Direção autorizada: produzir os artefatos diretamente de apps/<id> no Ecosystem,
com pipelines próprios por produto. Não criar dependência do Hub, não apagar
histórico/repos antigos e não substituir chaves de assinatura.

A implementação prepara os pipelines existentes de Lunet2D e Urbe; Hub já publica
no Ecosystem; ecosystem-ai ainda não possui aplicativo distribuível. Novos apps
precisam de pipeline próprio, não recebem uma release por existir no monorepo.
Urbe mantém REQ-006/066 (release por tag), a URL/PWA antiga e os atualizadores
antigos até uma versão-ponte em item próprio. A chave privada não pode ser
exportada pela API do GitHub; sua configuração no Ecosystem é pré-requisito.

Esta direção atualiza o arranjo transitório de DEC-0008/DEC-0014/DEC-0017 e
DEC-0021-C para builds; não cancela a direção futura da plataforma first-party.
