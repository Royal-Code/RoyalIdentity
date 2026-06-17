# Decisão curta: chave física do UserAccount

Data: 2026-06-17

## Escopo

Complementa as decisões de [an-users-plan-03.md](an-users-plan-03.md) e
[an-users-plan-04.md](an-users-plan-04.md) sobre o tipo do ID do `UserAccount`,
chaves e índices mínimos para a persistência PostgreSQL do módulo `UserAccounts`.

## Decisão

Usar **duas identidades distintas**:

- `Id`: chave física interna do banco, tipo `long` / PostgreSQL `bigint generated ... as identity`.
- `SubjectId`: identificador de protocolo/negócio, tipo `string`, usado como OIDC `sub`.

O `SubjectId` continua:

- gerado por padrão, por exemplo com `CryptoRandom.CreateUniqueId()`;
- opcionalmente informado no cadastro quando a política do realm permitir;
- imutável depois da criação;
- único por realm: unique `(RealmId, SubjectId)`;
- nunca derivado de username/email;
- nunca substituído pelo `Id` interno.

## Motivo

As consultas do módulo usam `SubjectId` para entrada pela borda do IdP, mas os
joins internos do modelo usam a chave da conta:

- autenticação por username/email;
- lookup por `(RealmId, SubjectId)`;
- projeção de claims;
- joins com emails, roles, credencial e propriedades dinâmicas;
- buscas administrativas paginadas por realm/status/data.

Para esses joins e FKs, `bigint` é menor e mais eficiente que `uuid`: 8 bytes contra
16 bytes, índices menores, melhor uso de cache e boa localidade de inserção quando
gerado por sequence/identity.

`Guid.CreateVersion7()` / UUIDv7 é melhor que UUIDv4 por ser ordenável por tempo,
mas ainda não traz ganho suficiente aqui para ser PK física. Ele só deve ser
reavaliado se houver necessidade real de geração distribuída de IDs físicos ou
exposição pública de IDs não enumeráveis.

## Modelo relacional mínimo

```text
UserAccount
  Id                    bigint identity primary key
  RealmId               string not null
  SubjectId             string not null
  NormalizedUsername    string not null
  ExternalId            string null

  unique (RealmId, SubjectId)
  unique/index (RealmId, NormalizedUsername)        -- conforme política de username
  index (RealmId, ExternalId) where ExternalId is not null
```

Tabelas filhas devem referenciar a conta por `UserAccountId` (`bigint`) e manter
`RealmId` estrutural para isolamento e índices por tenant.

Exemplos:

```text
UserAccountEmail
  Id                  bigint identity primary key
  RealmId             string not null
  UserAccountId       bigint not null foreign key -> UserAccount.Id
  NormalizedAddress   string not null

  index/unique (RealmId, NormalizedAddress)         -- respeitando AllowDuplicateEmail

UserAccountPropertyValue
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null foreign key -> UserAccount.Id
  PropertyDefinitionId  bigint not null foreign key -> PropertyDefinition.Id
  ClaimType             string not null
  Ordinal               int null

  index (UserAccountId, PropertyDefinitionId, Ordinal)
```

## Consequência para o plano

A nova versão do plano deve documentar explicitamente que:

- `UserAccount.Id` é a PK física;
- `(RealmId, SubjectId)` é alternate key natural/protocolar;
- todas as tabelas realm-scoped carregam `RealmId`;
- FKs internas usam preferencialmente `UserAccountId`;
- queries de borda entram por `(RealmId, SubjectId)` e depois usam `Id` para joins.
