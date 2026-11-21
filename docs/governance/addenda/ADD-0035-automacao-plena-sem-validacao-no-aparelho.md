# ADD-0035 — Automação plena: sem etapa de validação no aparelho e sem aprovações, salvo o extremamente crítico

**Data:** 19/11/2026. **Autoridade:** ordem explícita do proprietário nesta conversa ("Quero que remova do Ecosystem as etapas de validação no aparelho e aprovações. Quero merge automático, e no máximo ter que responder decisões de nível extremamente crítico, fora isso quero automação plena."). **Registro:** DEC-0065. **Escopo:** todos os Products e agentes do Ecosystem. Substitui, onde divergem, NN-017 (como etapa obrigatória), ADD-0012 §validações, ADD-0034 (fronteira de governança e revisão do proprietário no head), `definition-of-done.md` §2/§4 e `communication.md` §9.

## Decisão do proprietário

- **Sem etapa de validação no aparelho.** Nenhuma tarefa, roadmap, handoff, release ou integração espera validação humana. Agentes não criam validações humanas pendentes; o portal não mantém fila de validações. Uma tarefa conclui com implementação, testes automáticos verdes e evidência.
- **A honestidade continua:** o que foi testado só em CI, headless, Xvfb ou emulador é dito assim; nada é apresentado como "validado no aparelho" sem ter sido. Um defeito que o proprietário vê no aparelho chega como relato (Issue) e vira trabalho, não como etapa de aprovação.
- **Integração automática para tudo**, inclusive governança, workflows, segurança, dados e distribuição, quando o estado combinado com a `main` passa em todos os checks.
- **Só o extremamente crítico chega ao proprietário:** (1) o próprio portão — política de integração, checker de consistência, integrador e os workflows que o executam (`integrate.yml`, `consistency.yml`); (2) chaves e credenciais de assinatura; (3) o estágio da versão (alpha, beta, dev, estável), já reservado ao proprietário. Nesses casos vale a review APPROVED do proprietário no head do PR.
- **Informar o que foi implementado:** a cada integração, o agente diz em linguagem simples o que mudou para quem usa o produto, sem jargão, separando o que foi feito, o que foi só testado em máquina e o que ficou de fora.

## Por que o portão continua protegido

O integrador julga cada PR com o checker e a política da `main`. Se o próprio portão integrasse sozinho, um único PR poderia desligar todos os controles. Mantê-lo como extremamente crítico garante que a automação plena continue sendo uma escolha do proprietário.
