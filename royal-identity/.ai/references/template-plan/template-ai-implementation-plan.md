# Template: Plano de implementação orientado a IA

## Comandos para a IA geradora

- Gere um plano em Markdown no arquivo `.ai/plans/plan-<slug>.md`.
- Use o português do repositório.
- Escreva o plano para outra IA executar e manter.
- Use comandos, decisões, critérios, tarefas e verificações.
- Não use linguagem aspiracional.
- Não inclua justificativas longas.
- Não inclua descrição narrativa do processo de criação do plano.
- Não invente decisões humanas.
- Marque decisões ausentes como pergunta aberta.
- Questione o humano antes de fechar decisão que altere arquitetura, contrato público, persistência, segurança, compatibilidade, CI/CD, UX pública, custo operacional ou escopo.
- Leia os artefatos de referência antes de preencher `Contexto`, `Estado atual`, `Decisões fechadas`, `Design alvo`, `Fases`, `Invariantes`, `Riscos` e `Referências`.
- Registre apenas fatos verificados em `Estado atual do código`.
- Registre incertezas em `Perguntas ao humano`, não em `Decisões fechadas`.
- Separe `Decisões fechadas` de `Histórico de decisões`.
- Use `DF<n>` para decisões fechadas.
- Use `Q<n>` para perguntas ao humano.
- Quando uma decisão substituir outra, mantenha o histórico com `SUPERSEDED`.
- Crie fases entregáveis, executáveis e verificáveis.
- Cada fase deve ter `Depende de`, `O que/como`, `Tarefas`, `Critérios de aceite`, `Testes` e `Resultado da Fase`.
- Cada tarefa deve começar com verbo de ação.
- Cada tarefa deve ser marcada com `- [ ]` ou `- [x]`.
- Cada critério de aceite deve ser falsificável.
- Cada comando de teste deve ser executável no repositório alvo.
- Inclua comandos de build/test padrão quando houver.
- Inclua invariantes que não podem ser quebrados durante a execução.
- Inclua riscos com gatilho, impacto e mitigação.
- Inclua rastreabilidade entre objetivos, fases, decisões e testes.
- Inclua diferidos/backlog para itens fora do escopo que foram encontrados durante o design.
- Atualize o status, a barra de progresso e a tabela de fases sempre que uma fase for concluída.
- Não marque uma fase como concluída se houver decisão aberta, critério não atendido ou teste obrigatório não executado.
- Ao concluir uma fase, preencha `Resultado da Fase` com entregáveis, arquivos alterados, desvios, verificação e pendências.
- Ao validar o plano, confira se toda decisão citada por uma fase existe em `Decisões fechadas` ou `Histórico de decisões`.

## Shape do documento gerado

````markdown
# Plan: <título do plano> (`<slug-do-plano>`)

## Status: <RASCUNHO|EM ANDAMENTO|CONCLUIDO|BLOQUEADO> - <estado curto e verificável>

## Progresso

`<barra>` **<percentual>%** - <fases concluídas> de <total de fases> fases<observação curta opcional>

| Fase | Estado |
|---|---|
| Fase 1 - <nome> | <Pendente|Em andamento|Concluida|Bloqueada> |
| Fase 2 - <nome> | <Pendente|Em andamento|Concluida|Bloqueada> |
| Fase N - <nome> | <Pendente|Em andamento|Concluida|Bloqueada> |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de N`). Exemplo de barra: `████░░░░░░░░`.
> Antes de fechar uma fase, confirme que decisões, critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- <arquivo/link/comando verificado> — <fato extraído>.
- <arquivo/link/comando verificado> — <fato extraído>.

### Estado atual do código (verificado em <AAAA-MM-DD>)

- **<fato relevante>:** <evidência local com arquivo, símbolo, teste ou comando>.
- **<fato relevante>:** <evidência local com arquivo, símbolo, teste ou comando>.

### Lacunas, conflitos e restrições

- **<lacuna/conflito/restrição>:** <impacto verificável>.
- **<lacuna/conflito/restrição>:** <impacto verificável>.

### Superfícies impactadas a mapear

- `<módulo/projeto/pacote/sistema>` — <uso/contrato/risco>.
- `<módulo/projeto/pacote/sistema>` — <uso/contrato/risco>.

---

## Objetivo

1. <resultado verificável>.
2. <resultado verificável>.
3. <resultado verificável>.

## Fora de escopo

- <item fora do escopo e destino quando aplicável>.
- <item fora do escopo e destino quando aplicável>.

---

## Perguntas ao humano

> Remova esta seção quando não houver perguntas abertas.

- **Q1 — <decisão necessária>:** <pergunta objetiva>.
  - **Opções:**
    - **A)** <opção e efeito direto>.
    - **B)** <opção e efeito direto>.
  - **Impacto se não decidir:** <bloqueio ou risco>.
  - **Status:** Aberta.

---

## Decisões fechadas

- **DF1 — <nome da decisão>:** <decisão aplicada>. Fonte: <Qn/ADR/plano/review/código>.
- **DF2 — <nome da decisão>:** <decisão aplicada>. Fonte: <Qn/ADR/plano/review/código>.

---

## Histórico de decisões

> Mantenha esta seção quando houver perguntas respondidas, alternativas descartadas ou decisões substituídas.

**Fase <n> (<tema>):**

- **Q1 — <pergunta>:** <opções consideradas>.
  - **Resposta Q1.1:** <resposta humana>.
  - **Considerações Q1.1:** <fatos verificados que afetam a decisão>.
  - **Conclusão Q1:** <decisão final ou pergunta refinada>.
  - **SUPERSEDED por Q1.2:** <decisão substituída>, quando aplicável.

---

## Design alvo

### Contratos e bordas

- `<contrato/interface/API/evento>`: <assinatura, semântica e dono>.
- `<contrato/interface/API/evento>`: <assinatura, semântica e dono>.

### Modelo, dados e persistência

```text
<entidade/tabela/agregado>
  <campo> <tipo> <restrição>
  <campo> <tipo> <restrição>
  index/unique <restrição>
```

### Arquitetura alvo

```text
<projeto/módulo>/
  <responsabilidade>

<projeto/módulo>/
  <responsabilidade>
```

### Segurança, concorrência e confiabilidade

- <regra de segurança/concorrência/confiabilidade>.
- <regra de segurança/concorrência/confiabilidade>.

### Compatibilidade, migração e rollout

- <regra de compatibilidade/migração/rollout>.
- <regra de compatibilidade/migração/rollout>.

---

## Ordem de execução

1. **Fase 1 (<nome>)** — <dependência/razão operacional curta>.
2. **Fase 2 (<nome>)** — <dependência/razão operacional curta>.
3. **Fase N (<nome>)** — <dependência/razão operacional curta>.

Build/test padrão:

```powershell
<comando de build>
<comando de teste>
```

---

## Fase 1 - <nome da fase>

**Depende de:** <DFn/Qn/fase/artefato>.

**Escopo:** <módulos/projetos/arquivos/contratos tocados>.

**O que/como:** <comandos de design e implementação para a IA executar nesta fase>.

**Tarefas:**

- [ ] <ação verificável>.
- [ ] <ação verificável>.
- [ ] <ação verificável>.

**Critérios de aceite:** <condições falsificáveis para aceitar a fase>.

**Testes:** <comandos/testes/cenários obrigatórios>.

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - <nome da fase>

**Depende de:** <DFn/Qn/fase/artefato>.

**Escopo:** <módulos/projetos/arquivos/contratos tocados>.

**O que/como:** <comandos de design e implementação para a IA executar nesta fase>.

**Tarefas:**

- [ ] <ação verificável>.
- [ ] <ação verificável>.
- [ ] <ação verificável>.

**Critérios de aceite:** <condições falsificáveis para aceitar a fase>.

**Testes:** <comandos/testes/cenários obrigatórios>.

### Resultado da Fase 2

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Objetivo 1 | Fase <n> | DF<n> | <critério> | <teste/comando> |
| Objetivo 2 | Fase <n> | DF<n> | <critério> | <teste/comando> |

---

## Invariantes a preservar

1. <invariante arquitetural/de domínio/de segurança>.
2. <invariante arquitetural/de domínio/de segurança>.
3. <invariante arquitetural/de domínio/de segurança>.

---

## Critérios globais de conclusão

- <critério global verificável>.
- <critério global verificável>.
- <comando final obrigatório verde>.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| <risco> | <sinal verificável> | <efeito> | <ação> | <Aberto|Mitigado|Aceito> |
| <risco> | <sinal verificável> | <efeito> | <ação> | <Aberto|Mitigado|Aceito> |

---

## Diferidos e backlog

- <item diferido> — destino: <plano/backlog/ADR/fase futura>.
- <item diferido> — destino: <plano/backlog/ADR/fase futura>.

---

## Referências

- <arquivo/link/ADR/plano/review/teste>.
- <arquivo/link/ADR/plano/review/teste>.
````

## Comandos de manutenção para a IA executora

- Antes de iniciar uma fase, leia `Depende de`, `Decisões fechadas`, `Histórico de decisões`, `Invariantes a preservar`, `Critérios globais de conclusão` e `Riscos`.
- Antes do primeiro edit de uma fase, verifique as fontes citadas e atualize `Estado atual do código` se estiver divergente.
- Ao encontrar decisão ausente, pare a fase, registre `Q<n>` em `Perguntas ao humano` e marque a fase como `Bloqueada`.
- Ao implementar tarefa, marque `- [x]` apenas depois de validar o comportamento ou registrar a impossibilidade de validação.
- Ao alterar escopo, registre o desvio em `Resultado da Fase` e, se necessário, em `Diferidos e backlog`.
- Ao concluir fase, atualize `Resultado da Fase` com:
  - entregáveis;
  - arquivos/projetos alterados;
  - decisões aplicadas;
  - testes executados;
  - desvios;
  - pendências.
- Ao concluir fase, atualize `Status`, `Progresso`, tabela de fases e `Matriz de rastreabilidade`.
- Ao concluir o plano, garanta que `Critérios globais de conclusão` estejam atendidos e que `Perguntas ao humano` esteja vazia ou explicitamente diferida.