# P5-4 — Validação Android do IPC Binder autenticado

Estado: **roteiro em preparação; NÃO executar ainda**.

Este documento é o objeto da validação humana crítica do P5-4. Ele só passa a ser executável quando o PR indicar os APKs candidatos exatos do Hub e do primeiro Product caller.

## O que este gate provará

1. Os dois APKs rodam em processos/aplicativos distintos e o caller continua funcional quando o provider não está instalado.
2. O primeiro pareamento mostra o mesmo código curto nos dois aplicativos e exige confirmação humana no provider.
3. A chave privada de cada instalação permanece no Android Keystore; reinstalação/perda da chave invalida o pareamento antigo em vez de restaurar confiança silenciosamente.
4. Depois do pareamento, o provider autentica o UID/pacote/signatário real da transação Binder e exige prova de posse da chave pareada.
5. O caller verifica também a identidade/chave do provider antes de enviar texto.
6. `text.inspect@1.0.0` retorna characters/words/lines por IPC sem o Product referenciar código do provider.
7. Fechar/desconectar/cancelar invalida a operação/sessão; resultado tardio não vira sucesso.
8. Ausência, morte ou rejeição do provider vira integração indisponível explícita, sem quebrar o domínio essencial do caller.

## Casos

A versão final deste roteiro mapeará IPC-01..IPC-18 de `docs/architecture/ipc-transport-readiness.md` para:
- CI/unitário quando a propriedade não depende do Android real;
- ensaio automatizado Android quando exige identidade/processos;
- passo humano somente quando exige comparação/consentimento visual ou lifecycle físico.

Nenhum caso será marcado como aprovado só por CI de código portátil.

## Candidatos

- Hub: **a definir após CI do PR P5-4**.
- Caller Android: **a definir após CI do PR P5-4**.
- Commit testado: **a definir**.

## Resultado

Pendente.
