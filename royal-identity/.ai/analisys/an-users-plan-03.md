# Decisões para fechamento do plano UserAccounts

Data: 2026-06-16

## Escopo

Este documento registra as respostas finais para os pontos ainda abertos após
`an-users-plan-01.md`, `an-users-plan-02.md` e `an-users-plan-avail-A.md`.

O objetivo é servir de entrada para corrigir e fechar:

- `.ai/plans/plan-users-accounts-module.md`;
- `.ai/foundation/architecture.md`;
- `adrs/ADR-014.md` quando a decisão alterar a borda já implementada;
- a futura ADR do módulo `UserAccounts` (`ADR-015`, se mantida esta numeração).

## 1. Nome do módulo

Decisão: usar **`UserAccounts`**, não `UsersAccounts`.

Motivo: `UserAccounts` é mais natural/correto em inglês para nomear o módulo de
contas de usuário.

Consequências:

- Renomear o plano e documentos para `RoyalIdentity.UserAccounts`.
- Atualizar referências em análises, ADRs, roadmap e arquitetura curta.
- Usar o mesmo nome em projetos, namespaces e future package names.

Nome base fechado:

```text
RoyalIdentity.UserAccounts
```

## 2. Seam de claims da borda

Decisão: o contrato de claims da borda deve retornar `IReadOnlyList<Claim>`, usando
`System.Security.Claims.Claim`.

Motivo: neste seam específico o consumidor imediato é o IdP, que já usa `Claim` para
emitir tokens/userinfo. Retornar um tipo intermediário (`UserClaimDto`,
`UserClaimValue`, etc.) apenas para converter logo em seguida para `Claim` cria um
passo sem ganho real.

Decisão adicional: renomear o serviço de `IUserPropertyProvider` para
**`IUserClaimsProvider`**.

Motivo: o serviço não é um provedor genérico de propriedades de usuário. Ele é a
porta de borda que entrega claims emitíveis para o IdP.

Contrato alvo:

```csharp
using System.Security.Claims;

public interface IUserClaimsProvider
{
    Task<IReadOnlyList<Claim>> GetClaimsAsync(
        string subjectId,
        IReadOnlyCollection<string> identityScopeNames,
        IReadOnlyCollection<string> claimTypes,
        CancellationToken ct = default);
}
```

Limite importante:

- `Claim` pode aparecer no contrato de borda do IdP e no projeto de integração.
- `Claim` não deve virar o modelo persistido ou o vocabulário interno principal do
  domínio `UserAccounts`.
- O domínio/persistência continuam falando em conta, email, role, propriedade,
  definição, valor e projeção; a integração projeta isso para `Claim`.

Consequências:

- Ajustar `ADR-014`.
- Remover/aposentar `UserClaimDto`.
- Atualizar `DefaultProfileService`.
- Atualizar fake in-memory atual.
- Atualizar testes da borda.
- Atualizar o plano para usar `IUserClaimsProvider`.

## 3. Projetos da família UserAccounts

Decisão: usar projetos separados para o módulo puro, integração com o IdP e
provedores de persistência.

Projetos fechados:

```text
RoyalIdentity.UserAccounts
RoyalIdentity.UserAccounts.Integration
RoyalIdentity.UserAccounts.PostgreSql
RoyalIdentity.UserAccounts.Sqlite
```

Motivo de `PostgreSql`: usar a grafia baseada no nome oficial do projeto/site
`postgresql.org`.

Responsabilidades:

- `RoyalIdentity.UserAccounts`: domínio, casos de uso, contratos próprios do módulo,
  DbContext base, modelo e persistência própria comum.
- `RoyalIdentity.UserAccounts.PostgreSql`: configurações/mapeamentos/provider de
  PostgreSQL específicos do módulo.
- `RoyalIdentity.UserAccounts.Sqlite`: configurações/mapeamentos/provider de SQLite
  específicos do módulo; usado também para testes com conexão in-memory.
- `RoyalIdentity.UserAccounts.Integration`: adaptação entre `RoyalIdentity` e
  `UserAccounts`; implementa as portas do IdP (`ISubjectStore`,
  `ILocalUserAuthenticator`, `IUserClaimsProvider`, `IUserDirectory`) delegando para
  o módulo.

O módulo puro não deve depender de `RoyalIdentity`.

O projeto de integração pode expor um extension method:

```csharp
services.AddUserAccountsForRoyalIdentity(...);
```

Esse método registra a integração entre `RoyalIdentity` e `UserAccounts`.

Consequências:

- Atualizar `architecture.md`: `Integration/` não deve ser pasta interna do módulo
  puro; deve ser projeto separado.
- Atualizar `plan-users-accounts-module.md`: fase de integração cria/usa
  `RoyalIdentity.UserAccounts.Integration`.
- Registrar na ADR do módulo que projetos de integração são a forma preferida para
  adaptar módulos ricos às portas do IdP.

## 4. Semântica de escopos e claims

Decisão: o IdP continua sendo a fonte autoritativa sobre os identity scopes
existentes e sobre os claim types de cada scope.

Motivo: o endpoint discovery/well-known do OIDC publica dados de identidade e pode
expor claims suportadas (`ShowClaims`). Portanto, o que existe como identity scope e
claim de scope deve estar configurado no IdP.

O IdP deve chamar `IUserClaimsProvider` passando:

- nomes dos identity scopes solicitados;
- claim types associados a esses scopes.

O `UserAccounts` deve retornar apenas a **interseção** entre:

- propriedades/projeções disponíveis no módulo para aqueles scope names;
- claim types solicitados/autorizados pelo IdP.

Regra:

```text
claim emitida =
    existe no UserAccounts para o scope solicitado
    AND claim type foi solicitado pelo IdP
```

Consequências:

- Manter os dois parâmetros no contrato: `identityScopeNames` e `claimTypes`.
- Não mover a autoridade de discovery/consent para o módulo de contas.
- Evitar que o módulo emita claim que o IdP não anunciou/autorizou.
- `PropertyScope.Name` deve casar com `IdentityScope.Name` por string, sem o módulo
  depender do tipo rico `IdentityScope`.

## 5. Projeção de campos fixos para claims

Decisão: campos fixos do usuário não devem ser forçados para dentro do sistema de
propriedades dinâmicas.

Campos como `Username`, `DisplayName`, `Emails`, `Roles` e `ExternalId` continuam
sendo parte de primeira classe do modelo de conta.

Para decidir como esses campos viram claims, usar opções por realm no
`UserAccounts`.

Modelo conceitual simples:

```text
{ ScopeName, ClaimType, Include }
```

Exemplos de configurações por propriedade fixa:

```text
Username      -> { ScopeName = "profile", ClaimType = "preferred_username", Include = true }
DisplayName   -> { ScopeName = "profile", ClaimType = "name", Include = true }
PrimaryEmail  -> { ScopeName = "email",   ClaimType = "email", Include = true }
EmailVerified -> { ScopeName = "email",   ClaimType = "email_verified", Include = true }
Roles         -> { ScopeName = "profile", ClaimType = "role", Include = true }
ExternalId    -> { ScopeName = "profile", ClaimType = "...", Include = false/true }
```

Motivo: é parecido com uma `ClaimProjectionDefinition`, mas mais simples para campos
fixos. A configuração fica em options do realm do `UserAccounts`, enquanto
propriedades dinâmicas continuam usando `PropertyScope`/`PropertyDefinition`.

Consequências:

- Modelar separadamente:
  - campos fixos da conta;
  - opções de projeção de campos fixos;
  - propriedades dinâmicas por scope.
- O provider de claims combina essas fontes e aplica a interseção com os scopes/claims
  enviados pelo IdP.

## 6. Revisão de `AccountOptions`

Decisão: revisar `AccountOptions` para separar o que pertence ao IdP do que pertence
ao `UserAccounts`.

Motivo: hoje `AccountOptions` mistura opções de fluxo/login/UI do IdP com opções que
parecem políticas de conta/usuário. Com o módulo `UserAccounts`, algumas dessas
configurações devem migrar para options por realm do módulo.

Direção inicial:

- Opções de fluxo/protocolo/UI do IdP permanecem no lado `RoyalIdentity`.
- Opções de regra de conta, identificador de login, email, username, credencial e
  política de usuário devem ser avaliadas para mover para `UserAccounts`.

Ponto fechado: `AllowDuplicateEmail` deve ser opção do `UserAccounts` por realm.

Consequências:

- Antes da implementação, revisar propriedade por propriedade de `AccountOptions`.
- Documentar o destino de cada opção.
- Ajustar integração para ler a política correta do realm/módulo.
- Evitar duplicar a mesma regra nos dois lados.

Observações para revisão:

- `LoginWithEmail`, `EmailAsUsername`, `AllowDuplicateEmail`, `VerifyEmail`,
  `AllowChangeEmail`, `AllowChangeUsername`, `PasswordOptions` e políticas de senha
  provavelmente pertencem ao `UserAccounts`.
- `AllowLocalLogin`, `AllowRememberLogin`, `RememberMeLoginDuration` e
  `AutomaticRedirectAfterSignOut` tendem a permanecer mais próximos do fluxo IdP/UI,
  mas devem ser revisados.
- MFA, social login e passwordless continuam fora deste plano inicial.

## 7. `SubjectId` informado no cadastro

Decisão: por default, `SubjectId` é gerado automaticamente.

Decisão adicional: se o realm permitir informar `SubjectId`, o cadastro deve aceitar
um valor externo.

Motivo: isso ajuda cenários de migração/legado, mas não deve ser o comportamento
normal.

Regras:

- Default: gerar automaticamente, por exemplo com `CryptoRandom.CreateUniqueId()`.
- Permitir informar `SubjectId` apenas quando a política do realm habilitar.
- `SubjectId` continua imutável depois da criação.
- Unicidade é por realm.

Motivo da unicidade por realm: se o valor pode ser informado por realm, o mesmo valor
pode aparecer em realms diferentes sem violar isolamento.

Consequências:

- Índice único composto por `(RealmId, SubjectId)`.
- Testes para:
  - geração automática;
  - aceitação quando realm permite;
  - rejeição quando realm não permite;
  - rejeição de duplicidade dentro do mesmo realm;
  - aceitação do mesmo valor em realms diferentes;
  - imutabilidade após criação.

## 8. Eventos de domínio

Decisão: eventos de domínio entram no agregado agora, mas não são persistidos,
despachados ou gravados em outbox neste plano.

Tipos de eventos esperados:

- operações de criação/alteração de usuário;
- usuário bloqueado;
- usuário desbloqueado;
- alteração de status;
- alteração de username;
- alteração de email;
- alteração de credencial;
- alteração de propriedades de perfil.

Os eventos devem ficar no local definido pela arquitetura do módulo para eventos de
domínio.

Documentação obrigatória:

- plano do módulo;
- roadmap;
- ADR do módulo;
- eventualmente backlog/outbox plan.

Motivo: sem documentação, eventos gerados mas não persistidos podem parecer bug ou
código inútil.

Testes:

- Por enquanto, não validar eventos nos testes.
- Validar eventos depois, quando houver outbox/persistência/despacho de eventos.

## Pontos já fechados por consequência

Além das decisões acima, ficam confirmados:

- target framework: `net10.0`;
- `plan-resources-redesign.md` está completo, então `IdentityScope.Name` é base
  estável para integração por string;
- manter fake inicialmente;
- migrar fake/testes do módulo para SQLite in-memory preferencialmente;
- cortar endpoints/admin API deste plano inicial;
- casos administrativos completos ficam para o plano administrativo do
  `UserAccounts`;
- adicionar pré-flight das libs `RoyalCode.*` antes do skeleton;
- adicionar contract tests para as portas de borda;
- registrar coexistência opt-in com o fake até a paridade estar provada.

## Próximos ajustes nos documentos

1. Atualizar `plan-users-accounts-module.md` com estas decisões.
2. Atualizar `.ai/foundation/architecture.md` para refletir:
   - `UserAccounts`, singular no primeiro termo;
   - projeto `.Integration` separado;
   - projects `.PostgreSql` e `.Sqlite`.
3. Emendar `ADR-014`:
   - `IUserPropertyProvider` -> `IUserClaimsProvider`;
   - retorno `IReadOnlyList<Claim>`;
   - remoção de `UserClaimDto`.
4. Criar/atualizar ADR do módulo (`ADR-015`) com:
   - projetos da família;
   - schema de propriedades por escopo;
   - projeção de campos fixos;
   - revisão de `AccountOptions`;
   - eventos não persistidos neste plano.
5. Atualizar roadmap/backlog para registrar outbox/eventos como plano futuro.
