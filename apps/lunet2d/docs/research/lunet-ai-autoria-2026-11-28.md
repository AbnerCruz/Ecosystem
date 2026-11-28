# Lunet — autoria e assistência contextual (28/11/2026)

## Diagnóstico
O caso de `Player.Move` mostrou uma resposta tecnicamente plausível, porém substituindo o cálculo de direção por outra representação. Com `moveTarget = Pos` inicialmente, normalizar um vetor nulo pode produzir NaN; sem limitar o passo, o movimento pode ultrapassar o alvo. São defeitos demonstráveis, distintos da escolha válida de usar Normalize. A tarefa de ensino exigia código a cada passo e oferta de delegação em toda resposta. A orientação arquitetural impunha separação por arquivo, mesmo em projetos pequenos. A edição cirúrgica já existia, mas não bastava para proteger a lógica nem orientava o ensino.

## Referências e confronto
- Harvard CS50 Duck: https://cs50.readthedocs.io/cs50.ai/ — orientação em vez de entregar respostas por padrão.
- Estudo sobre dicas próximas da estrutura da solução do aluno: https://pmc.ncbi.nlm.nih.gov/articles/PMC7334677/ — aproveitar o código existente.
- Pesquisa sobre feedback e aprendizagem independente: https://link.springer.com/article/10.1186/s40594-025-00592-w — distinguir conclusão imediata de aprendizagem transferível.
- Relatos comunitários (evidência anedótica): https://www.reddit.com/r/learnprogramming/comments/1qbyv1w/protip_dont_use_ai_when_you_are_learning/ e https://www.reddit.com/r/ClaudeCode/comments/1xgvlir/can_we_start_training_models_to_minimize_diff/ — preocupação com dependência e mudanças desnecessárias.
- Decisão: Lunet não é uma plataforma de avaliação fechada. Dicas progressivas são padrão, mas código completo explicitamente solicitado continua disponível. Não se introduz proibição de refatoração autorizada, classificador adicional, novo runtime ou dependência.

## Verificação
- [x] Política comum em `LunetAiIntent.Instructions`, aplicada a ambos os modos e aos caminhos de contexto completo/compacto.
- [x] Recomendações arquiteturais não impositivas e avisos consultivos.
- [x] Testes de contrato de prompt, envio real ao provedor simulado e permissões existentes.
- [ ] CI .NET e APK; não executados localmente nesta sessão (sem acesso de rede Git e sem confirmação do SDK).
- [ ] Avaliação qualitativa com modelos reais: pedir diagnóstico de Move em Estudo; verificar que identifica vetor nulo e ultrapassagem, preserva Normalize e só entrega código completo quando solicitado. Em Fazer, verificar diff localizado, compilação e ausência de refatorações não relacionadas. Testes de prompt não garantem obediência semântica do modelo.
