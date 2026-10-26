# Urbe — Notas da versão atual

**Versão publicada:** 2.0.0-beta.31  
**Canal:** Urbe 2.0 nativo, Android e Windows  
**Publicação:** 25/10/2026  
**Identidade Android:** app.urbe.csharp

> Este arquivo descreve a versão publicada. Alterações em desenvolvimento só passam para cá no mesmo commit que atualiza a versão canônica e prepara uma release nova.

## Estabilidade e assistência

- Corrigido o crash ao renderizar argumentos e planos malformados da atividade dos agentes.
- O segundo envio de uma conversa é preservado e processado em ordem, sem perder a mensagem enviada enquanto outra execução está ativa.
- As etapas da IA permanecem observáveis, incluindo interrupções por ausência de progresso.

## Formulários e templates

- Formulários nativos dentro dos chats e a partir de templates.
- Rascunhos persistentes, diferentes tipos de campos, validações, condicionais, soma, grupos repetíveis e modelos reutilizáveis.
- Proteção de notas associadas a formulários quando a versão do documento mudou, respeitando permissões e aprovações.

## Desempenho e ferramentas

- Evita escritas redundantes quando o conteúdo já é idêntico.
- Reaproveita leituras associadas à versão do arquivo.
- Lê somente os trechos solicitados, quando aplicável, e evita injetar evidências idênticas novamente.
- Métricas de eficiência por execução para examinar ciclos sem progresso.

## Distribuição e dados

- APK Android e ZIP Windows na release urbe-csharp-v2.0.0-beta.31.
- Mesmo ID de pacote e certificado do canal beta anterior; atualizações preservam o vault quando feitas por cima da instalação compatível.
- A pasta de notas permanece separada das conversas e configurações privadas do Assistente. Desinstalar o aplicativo pode apagar estes últimos dados.
- Instalação de versões é manual. Versões antigas podem não reconhecer formatos criados por versões novas.

## Verificação

A release publicada foi verificada no GitHub. Testes de interface física, teclado, ciclo de vida do Android e atualização in-place continuam exigindo validação em aparelho.

**Fonte detalhada:** PATCHNOTES-beta.31.md.

**Próxima release:** as funcionalidades novas em desenvolvimento só entram neste arquivo após seus testes, integração e publicação.
