
Decisões de Arquitetura

## 1. Introdução

Este documento visa apresentar as decisões de arquitetura tomadas durante o desenvolvimento do projeto.

## 2. Metodologia

As decisões de arquitetura foram documentadas utilizando a técnica de Architecture Decision Records (ADR). Cada decisão é registrada em um arquivo markdown, seguindo o seguinte template:

```md
# [Número e título da decisão]

## 1. Contexto

Descrição do contexto que motivou a tomada da decisão.

## 2. Decisão

Descrição da decisão tomada.

## 3. Consequências

Descrição das consequências da decisão tomada.
```

## 2.1. Revisões

Quando uma ADR precisa ser atualizada por decisões posteriores na hora da implementação, uma nova seção pode ser adicionada ao ADR.

```md
## 4. Revisão

Descrição das decisões levantadas e tomadas durante a implementação.
```

## 3. Decisões

- [ADR-001: Rearquitetura do IdentityServer4](./adrs/ADR-001.md)
- [ADR-002: Realms](./adrs/ADR-002.md)
- [ADR-003: Testes](./adrs/ADR-003.md)
- [ADR-004: Telas em Razor](./adrs/ADR-004.md)
- [ADR-005: Usuários](./adrs/ADR-005.md)
- [ADR-006: Convenção de constantes e uso de constantes externas](./adrs/ADR-006.md)
- [ADR-007: Account pages em SSR estático](./adrs/ADR-007.md)
- [ADR-008: Gerenciamento de realms via IRealmManager](./adrs/ADR-008.md)
- [ADR-009: Testes de isolamento multi-realm](./adrs/ADR-009.md)
