# Pre-plano: Credenciais e Ciclo de Segurança da Conta

## Status

Documento de levantamento para o futuro plano `.ai/plans/plan-users-security-lifecycle.md`.

Este documento depende do plano [plan-users-accounts-module-v2.md](../plans/plan-users-accounts-module-v2.md).
O plano formal deve ser escrito e refinado depois que o `UserAccounts v2` existir, pois parte do desenho pode mudar
durante a implementação.

## Objetivo do plano futuro

Evoluir o módulo `RoyalIdentity.UserAccounts` para cobrir o ciclo de segurança de conta:

- credenciais de conta, começando por senha local;
- troca de senha;
- recuperação de senha;
- histórico de senhas;
- expiração de senha;
- `MustChangePassword`;
- lockout por falha de autenticação;
- bloqueios/restrições administrativas;
- verificação de email e telefone;
- `SecurityStamp`;
- invalidação opcional de cookie, sessão e refresh tokens;
- auditoria, métricas, eventos e outbox quando necessário.

O plano não deve implementar MFA, passwordless ou federação. Esses temas aparecem em itens próprios do roadmap.
Este plano deve, no máximo, deixar o modelo preparado para tipos futuros de credencial.

## Entradas usadas

- [plans-roadmap-01.md](../plans/plans-roadmap-01.md), seção 3.
- [plan-users-accounts-module-v2.md](../plans/plan-users-accounts-module-v2.md).
- Respostas e decisões informadas pelo usuário nesta discussão.
- Código atual:
  - `RoyalIdentity/Options/AccountOptions.cs`
  - `RoyalIdentity/Options/PasswordOptions.cs`
  - `RoyalIdentity.Storage.InMemory/LockoutPolicy.cs`
  - `RoyalIdentity.Storage.InMemory/MemoryLocalUserAuthenticator.cs`
- Referências externas de comparação:
  - ASP.NET Core Identity: password options, lockout, cookie/security stamp e tokens.
  - Keycloak: credentials como conceito amplo, required actions, password history/expiration e brute-force lockout.

## O que já está decidido ou fortemente assumido

1. O plano será separado do `UserAccounts v2`.
2. O plano será implementado depois do `UserAccounts v2`.
3. Configurações de credencial e ciclo de conta devem ser por realm.
4. `AllowForgotPassword` e `AllowChangePassword` devem ter uma fonte única no módulo `UserAccounts`.
5. Senha expirada força troca antes de continuar.
6. Depois da troca forçada por expiração, o usuário deve fazer login de verdade com a nova senha.
7. `MustChangePassword` exige fluxo de troca antes de continuar o login.
8. Sessões ativas continuam podendo emitir tokens, salvo se uma política de invalidação por realm determinar o contrário.
9. Tokens de recuperação/verificação devem:
   - ser armazenados como hash;
   - ter TTL;
   - ser de uso único;
   - ser revogados/expirados por nova emissão;
   - preservar anti-enumeração.
10. Alteração de senha deve ter política opcional de invalidação por realm.
11. Eventos que servem para integração/replicação tendem a ir para outbox; eventos úteis só para logs, métricas e indicadores
    podem ficar em auditoria/log/telemetria sem outbox.

## O que o plano v2 já deixa preparado

O `UserAccounts v2` deixa uma credencial local mínima:

```text
UserAccountCredential
  UserAccountId
  RealmId
  PasswordHash
  FailedPasswordAttempts
  LastPasswordFailureAt
  LockoutEndAt
  PasswordChangedAt
  MustChangePassword
```

Ele também move `PasswordOptions` para o módulo e aplica apenas:

- complexidade em `SetPassword`/`ChangePassword`;
- lockout básico no autenticador;
- persistência de políticas de histórico/expiração, sem enforcement completo.

O ciclo completo fica para este plano.

## Como outros sistemas tratam credenciais

### ASP.NET Core Identity

O ASP.NET Core Identity não usa uma coleção genérica única para tudo. A senha local fica como hash associado ao usuário.
Outros mecanismos aparecem por stores/estruturas próprias: external logins, tokens, passkeys, recovery codes, telefone,
email confirmado, lockout e security stamp.

O ponto relevante para RoyalIdentity: senha local pode ser um submodelo forte e específico, sem impedir que outros tipos de
credencial existam depois.

### Keycloak

O Keycloak usa o termo `credentials` de forma ampla: senha, OTP, certificados digitais e outros fatores são todos formas de
verificar a identidade do usuário. Ele também possui required actions, como `Update Password` e `Verify Email`, que bloqueiam
a conclusão do login até a ação ser satisfeita.

O ponto relevante para RoyalIdentity: faz sentido pensar conceitualmente em coleção de credenciais, mas cada tipo precisa
de regras próprias.

## 1. Modelo de credenciais

### Pergunta

O usuário terá apenas uma senha local ou uma coleção de credenciais?

### Resposta parcial

Conceitualmente, credencial é uma coleção: senha, OTP, passkey, certificado, recovery code e login externo são tipos
diferentes de credenciais ou métodos de autenticação.

Mas o plano atual deve implementar somente a senha local. MFA, passwordless e federação continuam fora de escopo.

### Opções

#### Opção A - `PasswordCredential` singular

Manter um submodelo forte e singular para senha:

```text
UserAccountCredential
  PasswordHash
  FailedPasswordAttempts
  LockoutEndAt
  PasswordChangedAt
  MustChangePassword
```

Prós:

- simples;
- combina com o v2;
- menor risco de generalização prematura;
- suficiente para login local.

Contras:

- futura migração quando MFA/passkey/passwordless entrarem;
- não representa bem o conceito amplo de credencial.

#### Opção B - Coleção genérica de credenciais

Criar uma tabela `UserCredential` com `CredentialType`, payload e estado.

Prós:

- mais alinhado com Keycloak;
- prepara vários tipos;
- permite listar credenciais por conta.

Contras:

- risco de payload genérico demais;
- regras de senha, OTP, passkey e recovery code são muito diferentes;
- pode invadir o escopo do plano de MFA/passwordless.

#### Opção C - Híbrida recomendada

Modelar credencial como coleção no vocabulário do domínio, mas implementar apenas o tipo `Password` neste plano,
com regras específicas e unicidade por conta:

```text
UserCredential
  Id
  RealmId
  UserAccountId
  Type              -- Password neste plano
  Name              -- opcional, para tipos futuros
  State             -- Active/Disabled/Removed
  CreatedAt
  UpdatedAt
  LastUsedAt

PasswordCredential
  CredentialId
  PasswordHash
  PasswordChangedAt
  MustChangePassword
  FailedPasswordAttempts
  LastPasswordFailureAt
  LockoutEndAt
```

Restrição inicial:

```text
unique (RealmId, UserAccountId, Type) where Type = 'Password' and State = 'Active'
```

Recomendação para o plano formal: usar a opção C se o custo de implementação for aceitável. Se o v2 ficar mais simples,
manter a opção A e registrar a migração futura.

## 2. Histórico de senhas

### Pergunta

Como armazenar histórico de senhas?

### Resposta parcial

A proposta de tabela própria é correta. Ao trocar a senha, o hash anterior entra no histórico. Ao criar uma nova senha,
o sistema valida a senha candidata contra a senha atual e contra os últimos hashes do histórico.

Como os hashes têm salt, não se compara hash com hash. A senha candidata deve ser verificada com o `IPasswordProtector`
contra cada hash histórico relevante.

### Modelo sugerido

```text
PasswordHistory
  Id
  RealmId
  UserAccountId
  CredentialId
  PasswordHash
  CreatedAt
  Reason              -- Change/Reset/AdminSet/Import
  CreatedBySubjectId  -- null para self-service/sistema
  HashAlgorithm       -- se o hash atual não carregar metadados suficientes
```

### Opções

#### Opção A - Tabela própria

Prós:

- simples de consultar;
- simples de podar por quantidade;
- suporta auditoria e migração de hash;
- suporta política por quantidade e por idade.

Contras:

- mais uma tabela;
- exige cuidado para não vazar hashes em logs/exportações.

#### Opção B - JSON no registro da credencial

Prós:

- menos tabelas;
- simples para MVP.

Contras:

- pior para poda, auditoria, concorrência e migração;
- tende a virar campo opaco.

#### Opção C - Apenas eventos/audit log

Prós:

- reaproveita eventos existentes.

Contras:

- ruim para validação transacional;
- histórico de senha vira dependente de retenção de eventos;
- não recomendado.

### Criptografar o histórico?

O hash de senha já é um verificador unidirecional, não a senha original. Criptografar o hash pode ser defesa em profundidade,
mas aumenta complexidade de chave, rotação e recuperação. A recomendação inicial é:

- armazenar hashes fortes e versionados;
- não logar nem expor hashes;
- considerar criptografia de coluna/TDE se o deployment exigir;
- considerar `pepper` apenas se o `IPasswordProtector` suportar isso de forma governada.

### Recomendação

Usar tabela própria. Começar com política por quantidade (`PasswordHistoryCount`) e deixar aberta uma política por idade,
similar ao "não usado nos últimos N dias".

## 3. Expiração de senha

### Decisão informada

Senha expirada força troca. O usuário não pode continuar o login sem trocar.
Depois da troca, deve entrar com a nova senha para logar de verdade.

### Fluxo esperado

```text
1. Usuário informa login/senha.
2. Senha é válida, mas está expirada.
3. Sistema não cria sessão SSO real e não emite authorization code/token.
4. Sistema cria um desafio curto para troca de senha.
5. Usuário troca a senha.
6. Desafio é consumido.
7. Usuário volta ao login e autentica com a nova senha.
```

### Opções para o desafio curto

#### Opção A - Token de ação transitório

Token curto, de uso único, com purpose `ChangeExpiredPassword`.

Prós:

- não cria sessão real;
- encaixa com recovery/verification tokens;
- claro para OIDC.

Contras:

- exige tela/fluxo próprio.

#### Opção B - Sessão parcial de required action

Criar uma sessão parcial sem permissão de emitir tokens até completar a ação.

Prós:

- aproxima de modelos como Keycloak required actions.

Contras:

- aumenta complexidade no IdP;
- precisa garantir que sessão parcial nunca emite token.

Recomendação inicial: opção A, salvo se o login flow ganhar uma abstração explícita de required actions.

## 4. `MustChangePassword` e OIDC

### Decisão informada

- Exige fluxo de troca antes de continuar.
- Sessões ativas continuam a emitir tokens.

### Semântica proposta

Para novo login:

```text
senha válida + MustChangePassword = true
  => AuthenticationResult.RequiredAction(UpdatePassword)
  => não cria sessão SSO real
  => não emite authorization code/token
  => usuário troca senha por token/challenge curto
  => usuário autentica novamente com a nova senha
```

Para sessão já ativa:

```text
MustChangePassword definido depois da sessão criada
  => sessão continua válida por default
  => nova autenticação por senha exige troca
  => invalidação da sessão depende da política de SecurityStamp/invalidation do realm
```

### Dúvidas abertas

- `MustChangePassword` deve incrementar `SecurityStamp`?
- Deve existir uma opção por realm para invalidar sessões quando `MustChangePassword` for setado por admin?
- O resultado de autenticação deve ganhar um reason novo, como `PasswordChangeRequired`, ou uma estrutura separada
  `RequiredAction`?

Recomendação: não tratar `MustChangePassword` como falha comum de autenticação. Ele precisa ser um estado intermediário
explícito do login flow.

## 5. Tokens de recuperação e verificação

### Decisões informadas

- Armazenar somente hash do token.
- TTL obrigatório.
- Uso único.
- Nova emissão expira/revoga emissões anteriores.
- Anti-enumeração obrigatório.

### Modelo sugerido

```text
UserAccountActionToken
  Id
  RealmId
  UserAccountId
  Purpose              -- PasswordRecovery/EmailVerification/PhoneVerification/ChangeExpiredPassword
  TokenHash
  TargetValue          -- email/phone alvo quando aplicável, normalizado ou protegido
  CreatedAt
  ExpiresAt
  ConsumedAt
  RevokedAt
  RevokedReason
  CreatedIpHash
  ConsumedIpHash
  UserAgentHash
```

### Regras

- Token bruto só aparece uma vez, no link/código entregue ao usuário.
- Banco guarda apenas hash do token.
- Consumo faz update condicional:

```sql
update user_account_action_tokens
set consumed_at = @now
where realm_id = @realmId
  and token_hash = @tokenHash
  and purpose = @purpose
  and consumed_at is null
  and revoked_at is null
  and expires_at > @now;
```

- Nova emissão revoga tokens ativos do mesmo `Purpose` + `UserAccountId` + `TargetValue`.
- Token de email/phone deve ser vinculado ao valor alvo, para não verificar um endereço substituído depois.

### Anti-enumeração

Para recuperação de senha:

- resposta pública sempre igual, exista ou não conta;
- não revelar se email/login existe;
- não revelar se conta está bloqueada/inativa;
- aplicar rate limit por IP, realm e identificador normalizado;
- se a conta existir e a política permitir, enviar mensagem;
- se não existir, registrar no máximo auditoria/telemetria sem criar token real.

## 6. Lockout temporário, bloqueio administrativo e restrições de acesso

### Ambiguidade de vocabulário

O termo "lockout administrativo" pode significar duas coisas diferentes:

1. Lockout automático por erro de senha que ficou indefinido e exige desbloqueio administrativo.
2. Bloqueio manual imposto por administrador por qualquer motivo.

Essas duas coisas não devem ser misturadas no mesmo campo.

### Taxonomia proposta

#### `PasswordLockout`

Estado derivado da credencial por falhas de autenticação:

```text
FailedPasswordAttempts
LastPasswordFailureAt
LockoutEndAt
PermanentPasswordLockoutAt
TemporaryLockoutCount
```

Pode ser:

- desativado por realm;
- temporário;
- indefinido até admin unlock;
- progressivo/misto no futuro.

#### `AccountStatus`

Estado geral da conta:

```text
Active
Inactive
Disabled
Deleted
```

Não deve ser usado para cada bloqueio temporário de senha.

#### `AccountAccessRestriction`

Restrição administrativa ou política futura:

```text
Id
RealmId
UserAccountId
Type              -- AdminBlock/GeoBlock/TimeWindow/ClientRestriction
Reason
StartsAt
EndsAt
CreatedBy
RevokedAt
```

Essa coleção permite:

- bloqueio manual com prazo;
- bloqueio manual sem prazo;
- futuras restrições por geolocalização;
- futuras restrições por horário;
- futuras restrições por client/app.

### Opções

#### Opção A - Campos simples no usuário

Prós:

- mais simples.

Contras:

- mistura status, lockout e regra administrativa;
- difícil evoluir para geo/hora/client.

#### Opção B - Separar `PasswordLockout` e `AccountAccessRestriction`

Prós:

- semântica clara;
- compatível com regras futuras;
- auditoria melhor.

Contras:

- mais tabelas e joins.

Recomendação: opção B, mas implementar primeiro apenas `PasswordLockout` e `AdminBlock`.

## 7. `SecurityStamp`

### Como costuma funcionar

No ASP.NET Core Identity, o security stamp é um valor associado ao usuário que pode ser incluído no principal/cookie e
validado periodicamente. Quando muda, credenciais antigas podem ser rejeitadas. O `UserManager` também expõe operações
para obter e regenerar o security stamp.

Para RoyalIdentity, o stamp deve ser um versionador de estado sensível de segurança, não um contador para qualquer mudança
de perfil.

### Alterações que devem incrementar

Recomendadas:

- senha alterada pelo usuário;
- senha resetada por recuperação;
- senha definida por admin;
- credencial local removida/desabilitada;
- `SecurityStamp` regenerado explicitamente por admin;
- conta desativada, deletada ou bloqueada quando a política do realm exigir derrubar sessões;
- MFA/passkey/recovery codes alterados futuramente, quando esses planos existirem;
- external login adicionado/removido futuramente, se ele for método de autenticação.

Possíveis, dependem de política:

- email alterado, se email for identificador de login ou se realm exigir email confirmado para login;
- telefone alterado, se telefone for fator de autenticação ou requisito de login;
- `MustChangePassword` setado por admin;
- roles/permissões alteradas, se o produto quiser revogação imediata de autorização.

Não recomendadas por default:

- falha de senha;
- incremento/reset de contador de lockout temporário;
- login bem-sucedido;
- atualização de display name;
- alteração comum de perfil;
- mudança de property dinâmica que só afeta claims de perfil.

### Onde guardar e comparar

Opção recomendada:

- `UserAccount.SecurityStamp` ou `UserSecurityState.SecurityStamp` no módulo;
- `SecurityStamp` capturado em `UserSession` no momento da criação da sessão;
- cookie pode carregar o stamp como claim, mas a fonte de verdade deve ser a sessão persistida;
- `IUserSessionService.IsSessionValidAsync(...)` compara stamp da sessão com stamp atual quando a política do realm exigir.

### Dúvidas abertas

- Qual intervalo de validação do cookie?
- A comparação deve acontecer em toda emissão de token, em validação de cookie, ou em ambos?
- Roles/permissões devem usar o mesmo `SecurityStamp` ou um stamp separado de autorização?

## 8. Política de invalidação por alteração de senha

### Decisão informada

Deve ser opcional e configurável por realm.

### Onde deve ficar a configuração

A configuração pertence conceitualmente ao `UserAccounts`, pois o gatilho é alteração de credencial.
A execução pode depender do IdP, porque sessões, cookies e refresh tokens são dados operacionais do servidor OIDC.

Forma sugerida:

```text
UserAccountsRealmOptions.SecurityLifecycle.CredentialChangeInvalidation
```

O módulo decide a política e publica um comando/evento de invalidação. O IdP executa sobre sessão/cookie/refresh tokens.

### Opções de configuração

Como os efeitos podem se combinar, flags são mais flexíveis que enum simples:

```text
None
CurrentSession
OtherInteractiveSessions
AllInteractiveSessions
RefreshTokens
AllSessionsAndRefreshTokens
```

Se for necessário enum simples por UX/admin, mapear para presets:

```text
None
KeepCurrentSessionOnly
RevokeOtherSessions
RevokeAllSessions
RevokeAllSessionsAndRefreshTokens
```

### Dúvidas abertas

- A troca forçada por expiração deve sempre invalidar sessões antigas?
- Reset por recuperação deve ter política mais forte que troca voluntária?
- Admin set password deve invalidar tudo por default?
- O usuário deve poder escolher "sair de outros dispositivos" ao trocar senha?

Recomendação inicial:

- troca voluntária: manter sessão atual e revogar outras sessões se a policy pedir;
- reset por recuperação: revogar sessões e refresh tokens por default;
- admin reset: revogar sessões e refresh tokens por default;
- import/migração: não invalidar.

## 9. Eventos, auditoria e outbox

### Pergunta

Eventos de segurança devem ser apenas domínio/testes ou duráveis via outbox?

### Separação recomendada

Usar três categorias:

1. Evento de domínio: usado dentro do módulo para consistência, testes e reações internas.
2. Auditoria de segurança: registro durável, consultável, com retenção e filtros.
3. Outbox de integração: somente quando outro sistema precisa receber o evento com entrega confiável.

Nem todo evento de segurança precisa ir para outbox.

### Eventos candidatos

#### Autenticação e lockout

- `LocalLoginSucceeded`
- `LocalLoginFailed`
- `PasswordFailureRegistered`
- `PasswordLockoutStarted`
- `PasswordLockoutEnded`
- `PasswordLockoutReset`
- `PermanentPasswordLockoutStarted`
- `PermanentPasswordLockoutCleared`

Classificação inicial:

- login success/failure: auditoria/telemetria, não outbox por default;
- lockout started/cleared: auditoria e possível outbox se houver integração com SIEM/notificação.

#### Senha e credencial

- `PasswordSet`
- `PasswordChanged`
- `PasswordResetRequested`
- `PasswordResetTokenIssued`
- `PasswordResetTokenConsumed`
- `PasswordResetTokenExpired`
- `PasswordResetTokenRevoked`
- `PasswordRecoveryAttemptRejected`
- `CredentialAdded`
- `CredentialUpdated`
- `CredentialRemoved`

Classificação inicial:

- `PasswordChanged`, `PasswordResetTokenConsumed`, `CredentialRemoved`: auditoria obrigatória;
- outbox quando houver replicação, notificação externa ou revogação operacional.

#### Histórico e expiração

- `PasswordHistoryAppended`
- `PasswordExpired`
- `PasswordChangeRequired`
- `PasswordChangeRequirementCleared`

Classificação inicial:

- auditoria;
- outbox só se outro sistema depender.

#### Email e telefone

- `EmailVerificationRequested`
- `EmailVerificationTokenIssued`
- `EmailVerified`
- `EmailVerificationTokenConsumed`
- `EmailVerificationTokenRevoked`
- `PhoneVerificationRequested`
- `PhoneVerified`
- `PrimaryEmailChanged`
- `PhoneNumberChanged`

Classificação inicial:

- `EmailVerified`/`PhoneVerified`: bons candidatos a outbox se outros sistemas replicam perfil;
- token issued/consumed: auditoria/notificação.

#### Segurança da sessão

- `SecurityStampChanged`
- `SessionInvalidationRequested`
- `UserSessionsRevoked`
- `RefreshTokensRevoked`

Classificação inicial:

- bons candidatos a outbox/command se o armazenamento operacional do IdP estiver separado do módulo.

#### Administração

- `AccountDisabled`
- `AccountEnabled`
- `AccountAdminBlocked`
- `AccountAdminUnblocked`
- `AccessRestrictionAdded`
- `AccessRestrictionRevoked`

Classificação inicial:

- auditoria obrigatória;
- outbox se outros sistemas precisam bloquear acesso imediatamente.

### Opções de estratégia

#### Opção A - Sem outbox neste plano

Prós:

- menor escopo.

Contras:

- revogação cross-store e notificações confiáveis ficam frágeis;
- pode dificultar replicação.

#### Opção B - Auditoria durável + outbox só para comandos/eventos de integração

Prós:

- equilíbrio bom;
- evita outbox de alto volume;
- preserva confiabilidade para integrações importantes.

Contras:

- exige classificar eventos.

#### Opção C - Tudo vai para outbox

Prós:

- modelo uniforme.

Contras:

- muito volume;
- custo operacional;
- eventos de login/falha podem virar ruído.

Recomendação: opção B.

## 10. Concorrência

### Cenários relevantes

#### Cenário 1 - Duas falhas de senha simultâneas

Risco:

- contador perde incremento;
- lockout demora a ativar;
- eventos inconsistentes.

Opções:

- optimistic concurrency com retry;
- `select ... for update`;
- update atômico no banco:

```sql
update user_account_credentials
set failed_password_attempts = failed_password_attempts + 1,
    last_password_failure_at = @now,
    lockout_end_at = case when failed_password_attempts + 1 >= @max then @lockoutEnd else lockout_end_at end
where realm_id = @realmId
  and user_account_id = @userAccountId
returning failed_password_attempts, lockout_end_at;
```

Recomendação: operação atômica de repositório, com `returning` no PostgreSQL e alternativa transacional no SQLite.

#### Cenário 2 - Sucesso concorrendo com falha

Risco:

- sucesso zera contador enquanto falha simultânea incrementa depois;
- conta pode ficar bloqueada logo após login válido.

Opções:

- serializar por row lock;
- registrar tentativa com timestamp e versão;
- aceitar eventualidade pequena.

Recomendação: serializar mutações da credencial durante autenticação por transação/row lock ou update condicional.

#### Cenário 3 - Consumo duplo de token

Risco:

- recovery/verification usado duas vezes;
- dois fluxos completam simultaneamente.

Recomendação:

- update condicional `ConsumedAt is null and RevokedAt is null and ExpiresAt > now`;
- se zero linhas afetadas, token inválido/consumido/expirado;
- índice único ou constraint para token hash ativo quando aplicável.

#### Cenário 4 - Nova emissão enquanto token antigo é consumido

Risco:

- token antigo ainda funciona depois de nova emissão.

Recomendação:

- nova emissão revoga tokens ativos em uma transação;
- consumo exige `RevokedAt is null`;
- considerar `TokenFamilyId`/`Generation` para invalidar toda família anterior.

#### Cenário 5 - Troca de senha concorrendo com login/token issuance

Risco:

- login emite sessão com stamp antigo;
- refresh token emitido logo antes da revogação.

Recomendação:

- alteração de senha e `SecurityStampChanged` devem ocorrer na mesma transação;
- emissão de sessão/token deve validar stamp atual no momento de persistir sessão;
- revogação de sessões/tokens deve ser idempotente.

#### Cenário 6 - Admin unblock concorrendo com falha de senha

Risco:

- admin desbloqueia e uma falha antiga bloqueia novamente.

Opções:

- admin unblock zera contador e incrementa versão;
- falha usa versão conhecida;
- bloqueio usa timestamp posterior ao desbloqueio.

Recomendação:

- `UnlockPasswordCredential` deve limpar contador, `LockoutEndAt` e incrementar uma versão de credencial;
- `RegisterFailure` deve operar sobre versão atual.

#### Cenário 7 - Verificação de email concorrendo com troca de email

Risco:

- token verifica email antigo como se fosse novo.

Recomendação:

- token deve carregar `TargetValue`;
- consumo só confirma se o email/phone atual ou pendente ainda corresponde ao alvo.

### Estratégia geral recomendada

- Usar transações curtas nos casos de credencial.
- Preferir updates condicionais e atômicos a lógica read-modify-write solta.
- Usar `ConcurrencyStamp`/row version para agregados.
- Usar row locks ou updates atômicos para contadores de falha.
- Usar consumo idempotente para tokens.
- Emitir eventos/outbox somente depois do commit.
- Não depender de lock em memória para produção.

## Ambiguidades tratadas

### Roadmap e fase grande

Resolvido como decisão de planejamento: será novo plano, depois do `UserAccounts v2`, e poderá ser refinado após a
implementação real do v2.

### Configuração por realm

Assumido: políticas de credencial, expiração, histórico, recuperação, verificação e invalidação são por realm.

### `AllowForgotPassword` e `AllowChangePassword`

Assumido: a fonte única fica em `UserAccountsRealmOptions`, provavelmente em um bloco `SecurityLifecycle` ou `Password`.

### Email/telefone, claims e plano v2

Este ponto pode exigir emenda pequena no v2.

Preparação recomendada no v2:

- garantir `IsVerified` e, se possível, `VerifiedAt` para email;
- definir claramente que `email_verified` deriva do email primário verificado;
- reservar projeção futura de `phone_number` e `phone_number_verified`;
- decidir se telefone entra no v2 ou apenas no lifecycle;
- garantir que troca de email/telefone não seja confundida com verificação.

Se telefone não entrar no v2, o plano de lifecycle deve incluir o modelo de telefone antes da verificação.

### SecurityStamp

Permanece decisão arquitetural central deste plano. Já há direção recomendada, mas o plano formal deve decidir:

- campo exato;
- evento de incremento;
- intervalo e ponto de validação;
- relação com sessão, cookie e refresh token;
- configuração por realm.

## Emendas sugeridas ao `UserAccounts v2`

Antes de implementar o v2, avaliar se vale emendar:

1. Reservar `SecurityStamp` em `UserAccount` ou `UserSecurityState`, sem ligar comportamento.
2. Reservar `ConcurrencyStamp`/versão para mutações concorrentes da conta/credencial.
3. Acrescentar `VerifiedAt` no email.
4. Especificar que `email_verified` vem do email primário verificado.
5. Decidir se telefone faz parte do agregado v2 ou fica totalmente para este plano.
6. Evitar consolidar `UserAccountCredential` de modo que bloqueie uma evolução para coleção de credenciais.

## Próximo plano esperado

O arquivo futuro `plan-users-security-lifecycle.md` deve conter fases semelhantes a:

1. ADR/emendas de arquitetura e options por realm.
2. Modelo de credenciais e versão/security stamp.
3. Política de senha, histórico e expiração.
4. Troca de senha e required action para `MustChangePassword`.
5. Recuperação de senha e tokens de ação.
6. Verificação de email e telefone.
7. Lockout, bloqueios administrativos e restrições de acesso.
8. Invalidação de sessões/cookies/refresh tokens.
9. Auditoria, eventos e outbox.
10. Concorrência, contract tests e regressão OIDC.

## Referências externas

- ASP.NET Core Identity configuration:
  <https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0>
- ASP.NET Core `UserManager<TUser>`:
  <https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1?view=aspnetcore-10.0>
- Keycloak Server Administration Guide:
  <https://www.keycloak.org/docs/latest/server_admin/index.html>
