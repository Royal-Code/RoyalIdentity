# Roadmap de planos futuros

Este roadmap organiza os planejamentos que ficam depois do plano ativo
[plan-users-edge-session.md](plan-users-edge-session.md). O plano ativo ainda cobre a
borda de usuários + sessão dentro do IdP; os itens abaixo são os próximos planos
necessários para implementar a visão definida em `../analisys/`.

## 1. Módulo de Contas de Usuário + Propriedades Dinâmicas por Escopo

**Plano sugerido:** `plan-users-accounts-module.md`

Criação do módulo `RoyalIdentity.UsersAccounts`, fora da biblioteca principal do
IdP, contendo domínio rico de contas e persistência própria.

Escopo principal:

- Modelo de conta: `UserAccount`, `SubjectId`, username, status e dados OIDC
  mínimos.
- Emails opcionais, múltiplos e fictícios/configuráveis por realm.
- ID externo/legado.
- Propriedades dinâmicas por escopo, vinculadas a `IdentityScope.Name`.
- Projeção das propriedades para claims via `IUserPropertyProvider`.
- Integração com as facades da borda: `IUserDirectory`, `ISubjectStore`,
  `ILocalUserAuthenticator` e `IUserPropertyProvider`.
- Persistência própria do módulo.
- Casos administrativos básicos.
- Eventos de domínio, Inbox/Outbox e replicação como fases finais ou diferidas.

Este plano une o antigo plano de módulo de contas com o plano de propriedades
dinâmicas por escopo, pois essas propriedades são parte central do modelo de
contas e não uma feature separada.

## 2. Persistência de Dados do IdP e Caching

**Plano sugerido:** `plan-data-persistence.md`

Implementação da persistência real do IdP atrás das facades existentes.

Escopo principal:

- `RoyalIdentity.Data.Configuration` para realms, clients, resources/scopes,
  keys e options.
- `RoyalIdentity.Data.Operational` para sessions, tokens, codes e consents.
- `RoyalIdentity.Storage.EntityFramework` adaptando `Data.*` para os contratos
  do core.
- Provedores `Storage.EntityFramework.Postgre` e
  `Storage.EntityFramework.Sqlite`.
- `RoyalIdentity.Storage.Caching` sobre as implementações de storage.
- Migração gradual do uso in-memory para Sqlite/Postgre conforme ambiente.

Este plano é a base de produção para os dados do IdP. O módulo
`UsersAccounts` tem persistência própria e não deve ser adaptado por este
storage do IdP.

## 3. Credenciais e Ciclo de Segurança da Conta

**Plano sugerido:** `plan-users-security-lifecycle.md`

Evolução das regras de segurança do módulo de contas.

Escopo principal:

- Senha como credencial de conta.
- Lockout temporário, lockout administrativo e auditoria de falhas.
- Troca de senha, recuperação de senha e verificação de email/phone.
- Password history e expiração de senha.
- `SecurityStamp` e invalidação de sessão/cookie quando credenciais ou estado
  sensível mudarem.
- Relação com `UserSsoLifetime`, cookie lifetime e sessões ativas.

Este plano pode ser uma grande fase do `UsersAccounts`, mas merece separação se
o tamanho ou o risco ficarem altos.

## 4. Administração de Sessões por Dispositivo

**Plano sugerido:** `plan-session-administration.md`

Extensão do modelo operacional de sessão para administração pelo usuário ou por
admin.

Escopo principal:

- Metadados de sessão: user agent, IP, device name e `LastSeenAt`.
- Listar sessões ativas por usuário/realm.
- Encerrar sessão específica ou encerrar outras sessões.
- Preservar integração com logout SSO front-channel/back-channel.

A sessão básica já é coberta pelo plano ativo de borda + sessão. Este plano
trata a camada administrativa e operacional mais rica.

## 5. API e UI Administrativa

**Plano sugerido:** `plan-admin-api-ui.md`

Criação das APIs e telas administrativas, em projetos separados dos módulos de
domínio.

Escopo principal:

- Administração de contas de usuário.
- Administração de realms, clients, resources/scopes e options.
- Reset de senha, ativar/desativar usuário e revogar sessões.
- Administração de propriedades dinâmicas por escopo.
- Integração com o realm `admin`.

Este plano depende das decisões de API/UI administrativa e deve respeitar a regra
da ADR-013: módulos contêm domínio + persistência; API e UI ficam separados.

## 6. Federation / Identity Brokering

**Plano sugerido:** `plan-federation-identity-brokering.md`

Autenticação por provedores externos configuráveis por realm.

Escopo principal:

- Modelo `ExternalIdentityProvider` por realm.
- Providers OIDC, social/corporativo e possivelmente SAML.
- Callback handlers realm-aware.
- Vinculação de identidades externas a contas do `UsersAccounts`.
- Respeito às restrições de IdP por client.
- Emissão correta de `idp` e `amr`.

O plano ativo prepara a costura para métodos externos, mas não implementa
federação.

## 7. MFA e Passwordless

**Plano sugerido:** `plan-auth-methods-mfa-passwordless.md`

Novos métodos de autenticação além de senha local.

Escopo principal:

- MFA por realm e por usuário.
- Passwordless e desafios temporários.
- Políticas por realm/client.
- Registro dos métodos em `amr`.
- Integração com login flow sem criar sessão antes da autenticação final.

Este plano depende do módulo de contas e do ciclo de credenciais estar bem
definido.

## 8. Key Management Service

**Plano sugerido:** `plan-kms.md`

Criação do módulo `RoyalIdentity.KMS` para gerenciamento de chaves, segredos e
certificados.

Escopo principal:

- Domínio de chaves, certificados e segredos.
- Persistência própria.
- Rotação de chaves por realm.
- Integração com `IKeyStore`.
- API e UI administrativas em projetos separados.

Este plano é parte da arquitetura modular definida na ADR-013, mas pode ser
priorizado independentemente dos planos de usuários quando a operação de chaves
virar requisito.

## Ordem recomendada

1. Concluir `plan-users-edge-session.md`.
2. Planejar e implementar `plan-users-accounts-module.md`.
3. Planejar e implementar `plan-data-persistence.md`.
4. Evoluir segurança de contas e administração de sessões.
5. Criar API/UI administrativa.
6. Avançar federação, MFA/passwordless e KMS conforme prioridade de produto.
