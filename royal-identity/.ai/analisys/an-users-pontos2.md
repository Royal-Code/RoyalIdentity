# Pontos de revisão - an-users-final

Este documento registra comenta `an-users-final.md` para uma revisão da modelagem.

Neste documento, considere o `IdP` como o projeto `RoyalIdentity`, o qual implementa autenticação e tudo o resto do protocolo OIDC.

## 1. Borda vs Módulo

A motivação desta revisão é por conta da refatoração ser grande ao ponto de se tratar de um módulo de contas de usuário, algo que deveria ficar fora da biblioteca do IdP, projeto `RoyalIdentity`.

A nova modelagem é boa, bem discutida e trabalhada. Ela foi feita pensando em melhorar a parte confusa da modelagem atual, descrito em `an-users.m1.md` e `an-users.m2.md`.

No entanto como diz em `UserClaim` do `an-users-final.md`: "Conversão para `System.Security.Claims.Claim` **só na borda** (factory de principal / profile service)", a modelagem nova já é uma modelagem de um módulo de contas de usuário.

Isso não é ruim, nem está errado, no entanto, a modelagem dos usuários, `IdentityUser`, `IdentitySession`, `UserDetails` e outros, são orientados a borda, ao IdP, e não a um módulo para gerenciar contas de usuários e tudo mais.

Um ponto de confusão é tratar a implementação `In-Memory` dos storages como algo final, onde o aplicativo em produção vai na verdade usar algo assim. Isso não será assim, a parte em memória atual é para testar a funcionalidade do IdP sem precisar de um banco de dados ou coisas complexas, o código atual é uma simplificação para iniciar os testes rapidamente.

O que será feito no futuro, são módulos com uma modelagem de classes orientadas a algum banco de dados, e os storages são uma `facede` para os módulos que gerenciam os dados.

## 2. Módulos de Dados

A idéia inicial é ter uma outra modelagem com entidades e EFCore para armazenar dados em bancos SQL, sendo inicialmente pensando em Postgre para produção e Sqlite para testes (substituindo in-memory).

Os projetos para modelagem de dados são pensados assim:

- **RoyalIdentity.Entities**: domínio administrativo com entidades dos modelos do IdP, como Realms, Clients, Resources, Options, etc. Contém entidades, serviços, contratos, regras para cadastro e manutenção do domínio, DbContex e queries.
- **RoyalIdentity.Storage.EntityFramework**: implementação dos `storages` do IdP (`RoyalIdentity`), usando `RoyalIdentity.Entities`.
- **RoyalIdentity.Storage.Caching**: mecanismos de cache para serem usados pelas implementações dos `storages`.
- **RoyalIdentity.Entities.Postgre**: mapeamentos das entidades de `RoyalIdentity.Entities` para EFCore com Postgre.
- **RoyalIdentity.Entities.Sqlite**: mapeamentos das entidades de `RoyalIdentity.Entities` para EFCore com Sqlite.

Os nomes dos módulo ainda não são definitivos, eles podem ser modificados para represetar melhor o que são.

O `RoyalIdentity.Entities` também poderá não ser único, pode ser que seja necessário mais de um, para dividir a responsabilidade, como por exemplo, módulo de configuração administrativo (Realms, Clients, Resources, ...) e módulo operacional (seções, tokens, ...).

## 3. Outros módulos

Os módulos da seção 2 são focados no modelo do IdP, projeto `RoyalIdentity`.

Mas outros módulos com domínios diferente irão existir.

Um deles é o `RoyalIdentity.KMS`, um módulo para gerenciamento de chaves, segredos e certificados, o qual também terá uma API e telas administrativas. Será um verdadeiro key vault.

Um outro que é candidado potencial, e agora praticamente já decidido, é o módulo de contas de usuários, o `RoyalIdentity.UsersAccounts`.

Além dos módulos, haverão outros projetos e API e de telas administrativas, mas isso não será tratado agora.

## 4. Modulo de Contas de Usuário

Assim chegamos ao módulo `RoyalIdentity.UsersAccounts`.

A modelagem atual vai bem em direção a este módulo.

No entanto, os requisitos dessa modelagem ficou apenas orientado a remodelar a borda.

Para a modelagem deste módulo, será necessário pensar em alguns requisitos a mais. Na verdade é bom pensar em dividir as requisitos de contas de usuário de controle de seção. O controle de seção faz parte do IdP, é possível pensar em um módulo de dados, ao lado do `RoyalIdentity.Entities`.

Requisitos que penso para contas de usuários:

- ter dados obrigatórios do OIDC.
- usuários e configurações por realm.
- email é opcional.
- mais de um email.
- email fictício, gerado automaticamente, configurável por realm, opcional.
- ID externo, identificador de usuários em outra sistema (geralmente legado).
- Propriedades dinâmicas por escopo:
  - deve ter escopos de propriedades
  - cada escopo define N propridades
  - propriedade tem nome (claim type), valor (claim value), tipo (claim value type), display name, informações de ajuda, se é dado sensível, regras de validação, (outras coisas se necessário).
  - escopo de propriedade é vínculo direto com Identity Scopes do IdP.
- Deve ter as outras regras já vistas no `RoyalIdentity`.
- Eventos de domínio.
- Inbox e Outbox.
- Replicação entre instâncias.

Além destes requisitos a arquitetura do módulo deve atender casos de uso para administrar contas de usuários.

## 5. Borda, módulos e modelagem/remodelagem

A solução proposta em `an-users-final.md` não atenderá os objetivos, a modelagem nova ainda não é boa o suficiente, é um ótimo caminho, trabalha a resolução da complexidade da implentação atual no IdP, mas não tem "bordas" bem definidas.

Ela precisará revisar para 3 pontos:
- borda: o modelo usado no IdP.
- contas de usuário: modelo para administração e persistência.
- seção: modelo operacional das seções do IdP.

A partir desta nova visão, vejo que antes de um ADR das decisões do documento `an-users-final.md`, precisariam um mais ADR para documentar a arquitetura modular e os projetos de persistência. No entanto, ainda é necessário avaliar e decidir o que deve existir e como deve ser chamado.

Outro ponto é revisar a modelagem atual, separando o que é para um módulo de contas de usuário, o que é operação do IdP. Outra coisa são as classes e o modelo para a borda, ou seja, o que é usado dentro do `RoyalIdentity`.

Tudo isso precisa ser reavaliado.
