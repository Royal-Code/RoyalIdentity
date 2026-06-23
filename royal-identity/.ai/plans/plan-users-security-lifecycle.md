# Plan: Credenciais e Ciclo de Segurança da Conta (`plan-users-security-lifecycle`)

## Status: PLANEJADO - 0 de 10 fases (aguardando resolução das Questões em aberto)

## Progresso

`----------` **0%** - 0 de 10 fases

| Fase | Estado |
|---|---|
| Fase 1 - ADR-017, emendas e options de ciclo de segurança por realm | Pendente |
| Fase 2 - Modelo de credencial, `SecurityStamp` e concorrência | Pendente |
| Fase 3 - Política de senha: histórico e expiração com enforcement | Pendente |
| Fase 4 - Troca de senha e required action (`MustChangePassword`/expirada) | Pendente |
| Fase 5 - Recuperação de senha e tokens de ação | Pendente |
| Fase 6 - Verificação de email e telefone | Pendente |
| Fase 7 - Lockout, bloqueio administrativo e restrições de acesso | Pendente |
| Fase 8 - Invalidação de sessões/cookies/refresh tokens | Pendente |
| Fase 9 - Auditoria, eventos e (seletivo) outbox | Pendente |
| Fase 10 - Concorrência, contract tests e regressão OIDC | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `#` por fase concluida, `%` e `X de 10`). Ex.: 3 fases => `###-------` **30%** - 3 de 10.

> **Aviso de prontidão:** este plano **não está pronto para implementação**. Várias fases estão **bloqueadas
> pelas [Questões em aberto](#questões-em-aberto)**. O autor deve decidir cada questão (registrando a decisão na
> ADR-017 e neste documento) **antes** de iniciar a fase que depende dela. Os modelos relacionais e contratos abaixo
> são **propostas iniciais**, não decisões fechadas, exceto onde marcado em [Decisões fechadas](#decisões-fechadas).

---

## Contexto

Este plano implementa a **seção 3 do roadmap** ([plans-roadmap-01.md](plans-roadmap-01.md) §3 — "Credenciais e
Ciclo de Segurança da Conta") e consome o levantamento já feito em
[an-users-sec-preplan.md](../analisys/an-users-sec-preplan.md), que reúne decisões informadas pelo usuário, opções
comparadas (ASP.NET Core Identity, Keycloak) e dúvidas em aberto.

O plano **depende** de [plan-users-accounts-module-v2.md](plan-users-accounts-module-v2.md) (**CONCLUÍDO**), que
entregou o módulo `RoyalIdentity.UserAccounts` com domínio rico, persistência própria EFCore (PostgreSql/Sqlite),
integração opt-in com a borda do IdP (`.Integration`) e uma **credencial local mínima**. O v2 deixou explicitamente
**fora de escopo** (e apontou para este plano): recuperação de senha, verificação de email/phone, histórico de senha,
expiração com enforcement completo, `SecurityStamp` + invalidação de sessão/cookie. A ADR-015 §2.8 confirma:
"Expiração, histórico, `SecurityStamp` + invalidação de sessão ficam para o plano de *security lifecycle*".

Este plano evolui as regras de segurança **dentro do módulo `UserAccounts`** (domínio + persistência próprios) e
adapta a borda do IdP via `.Integration` + pequenas emendas no core, seguindo a arquitetura Feature-Slice
([architecture.md](../foundation/architecture.md)), os contratos de borda da ADR-014 e o módulo da ADR-015.

### Estado atual do código (verificado) — o que o v2 já deixou pronto

Credencial local (`RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountCredential.cs`), 1:1 com a conta:

```text
UserAccountCredential
  UserAccountId, RealmId
  PasswordHash
  PasswordChangedAt
  MustChangePassword          (campo existe; nenhum fluxo o consome ainda)
  FailedPasswordAttempts
  LastPasswordFailureAt
  LockoutEndAt
  HasPassword (derivado)
  SetPassword / RegisterFailure / IsLockedOut / ClearExpiredLockout / ResetFailures
```

- `UserAccount` (agregado `AggregateRoot<long>`): `IsActive`, `IsBlocked`/`BlockedReason`/`BlockedAt` (owned
  `UserAccountBlockState`), `Version` (token de concorrência), `LocalCredential`, emails, roles, property values.
  Métodos `SetPassword`/`ChangePassword`/`AuthenticateLocal`/`Activate`/`Deactivate`/`Block`/`Unblock`/`UnlockLocalCredential`.
- `PasswordOptions` (no módulo) **já tem** `MaxFailedAccessAttempts`, `AccountLockoutDurationMinutes`,
  `EnablePasswordExpiration` (true), `PasswordExpirationDays` (360), `EnforcePasswordHistory` (true),
  `PasswordHistoryCount` (3) e regras de complexidade — porém **expiração e histórico não têm enforcement**
  (apenas persistidos). Complexidade tem enforcement em `PasswordPolicy`/`SetPassword`/`ChangePassword`.
- `UserAccountsRealmOptions` **já tem fonte única** de `AllowForgotPassword`/`AllowChangePassword`,
  `AllowChangeEmail`, `AllowChangeUsername`, `AllowChangePhoneNumber`, `VerifyEmail`, `PasswordOptions`.
- `UserAccountEmail` tem `IsVerified`/`IsPrimary`/`IsFictitious` — **não tem `VerifiedAt`**.
- Eventos de domínio existentes (no agregado, **não despachados/persistidos**): `UserAccountPasswordChanged`,
  `UserAccountLocalCredentialLocked`, `UserAccountBlocked`/`Unblocked`, `UserAccountActivated`/`Deactivated`,
  `UserAccountPrimaryEmailChanged`, etc.

### Lacunas confirmadas (greenfield deste plano)

- **Não existe** `SecurityStamp` (nem no `UserAccount`, nem em `UserSession`).
- **Não existe** tabela/coleção de histórico de senha.
- **Não existe** modelo de **token de ação** (recuperação/verificação/troca-de-senha-expirada).
- **Não existe** modelo de **telefone** no agregado (só a opção `AllowChangePhoneNumber`).
- **Não existe** enforcement de expiração de senha nem consumo de `MustChangePassword` no login flow.
- **Borda do core não tem** conceito de "required action": `AuthenticationResult`/`AuthenticationFailureReason`
  (`RoyalIdentity/Users/AuthenticationResult.cs`) só modelam sucesso/falha+motivo
  (`NotFound`/`Inactive`/`InvalidCredentials`/`Blocked`).
- **Store de sessão puro não revoga por subject**: `IUserSessionStore`
  (`RoyalIdentity/Users/Contracts/IUserSessionStore.cs`) só tem `Create`/`FindById`/`RecordClient`/`End` por `sid`.
  `UserSession` tem `ExpiresAt` **reservado sem comportamento** e nenhum `SecurityStamp`.
  `IUserSessionService.IsSessionValidAsync(principal)` é a costura por request (cookie `OnValidatePrincipal`).

---

## Objetivo

1. Cobrir o **ciclo de segurança da conta local** no módulo `RoyalIdentity.UserAccounts`, por realm:
   senha como credencial, troca, recuperação, histórico, expiração, `MustChangePassword`, lockout,
   bloqueio administrativo, verificação de email/telefone, `SecurityStamp` e invalidação de sessão/cookie/refresh.
2. Manter o **módulo puro independente do core** (sem `RoyalIdentity`, sem ASP.NET); toda adaptação ao IdP fica em
   `RoyalIdentity.UserAccounts.Integration`; emendas mínimas no core ficam atrás dos contratos de borda da ADR-014.
3. Preservar **anti-enumeração** em todos os fluxos públicos (login, recuperação, verificação).
4. Tornar **`SecurityStamp` o versionador de estado sensível de segurança**, com invalidação **opcional e por realm**
   de sessões/cookies/refresh tokens quando credenciais ou estado sensível mudarem.
5. Deixar o modelo **preparado** para tipos futuros de credencial (MFA/passwordless/federação), **sem implementá-los**.
6. Entregar tudo com **contract tests** (fake vs módulo) e **regressão OIDC** verde, mantendo a integração **opt-in**.

---

## Fora de escopo

- **MFA, passwordless, OTP, passkeys, recovery codes e federação/login externo.** Este plano, no máximo, deixa o
  modelo de credencial preparado para esses tipos (Questão Q1). Cada um tem item próprio no roadmap (§6/§7).
- **API/UI administrativa** completa de contas/segurança (reset por admin via UI, listagem de sessões por dispositivo
  na tela): fica para `plan-admin-api-ui` (§5) e `plan-session-administration` (§4). Este plano entrega os **casos de
  uso de domínio** e a **costura** que essas telas consumirão; os **endpoints/telas user-facing mínimos** dependem de
  Q12.
- **Persistência operacional do IdP** (sessions/tokens/consents em banco): é do `plan-data-persistence` (§2). Este
  plano usa as facades/stores existentes (in-memory hoje) e **define a costura de revogação** que a persistência real
  honrará.
- **Reescrita da borda da ADR-014.** Apenas **emendas aditivas** (required action no resultado de autenticação;
  revogação por subject no store de sessão; `SecurityStamp` em `UserSession`), registradas como emenda à ADR-014.
- **Outbox/inbox/replicação como infraestrutura nova**, salvo se Q8 decidir o contrário. O v2 deixou eventos de
  domínio **não despachados**; este plano não cria, por padrão, a máquina de outbox.
- **Rotação de chaves/segredos** (é do KMS, §8) e **rehash-on-login de senha** (sem legado a migrar — ver
  [plan-royalidentity-security.md](plan-royalidentity-security.md); reintroduzir só com upgrade de parâmetros PBKDF2).

---

## Decisões fechadas

> Estas decorrem de **decisões já informadas pelo usuário** no pré-plano
> ([an-users-sec-preplan.md](../analisys/an-users-sec-preplan.md), seções "O que já está decidido ou fortemente
> assumido" e os blocos "Decisão informada"), de **padrões documentados do projeto** (ADR-013/014/015,
> architecture.md) ou do **estado atual do código**. Tudo o que é apenas recomendação/opção do pré-plano foi movido
> para [Questões em aberto](#questões-em-aberto).

### Arquitetura e fronteiras

- O domínio de segurança da conta vive no **módulo puro** `RoyalIdentity.UserAccounts` (Feature-Slice: `Domain` +
  `Features` + `Infrastructure` + persistência própria). O módulo **não referencia o core** nem ASP.NET (ADR-013/015).
- A adaptação ao IdP fica **exclusivamente** em `RoyalIdentity.UserAccounts.Integration`, implementando as portas de
  borda do core. Emendas no core ficam **atrás dos contratos da ADR-014** e são aditivas.
- **Realm é ligado na construção** das portas realm-bound; `Realm` só é conhecido pela `.Integration` (ADR-014 §2.5).
  Configurações de credencial/ciclo são **por realm** (pré-plano "decidido" #3).
- **Escritas** = `RoyalCode.SmartCommands` (`[Command]` + `WithWorkContext`); **leituras** = read services /
  `RoyalCode.SmartSearch`; domínio retorna `Result`/`Problems`, **sem throw em fluxo esperado** (architecture.md §4/§8).
- **Erro de login/recuperação/verificação é genérico** por default (anti-enumeração); motivo interno preservado para
  auditoria (ADR-014 §2.10; pré-plano §5/§5-anti-enumeração).

### Options por realm

- `AllowForgotPassword`/`AllowChangePassword` têm **fonte única** em `UserAccountsRealmOptions` (já implementado no v2;
  pré-plano "decidido" #4). Toda nova política de ciclo de segurança entra como bloco próprio nas
  `UserAccountsRealmOptions` (copy-on-create por realm), **não** na `RealmOptions`/`AccountOptions` do IdP.
- A **política de invalidação por alteração de credencial** é **opcional e por realm** (pré-plano §8, "decidido" #10),
  conceitualmente do `UserAccounts` (o gatilho é a credencial); a **execução** sobre sessão/cookie/refresh é do IdP
  (pré-plano §8). O formato exato das flags/presets é Q7.

### Senha, expiração e `MustChangePassword`

- **Senha expirada força troca antes de continuar**; após a troca, o usuário faz **login real** com a nova senha — o
  sistema **não** cria sessão SSO real nem emite code/token durante o desafio de troca (pré-plano "decidido" #5/#6, §3).
- **`MustChangePassword` exige fluxo de troca antes de continuar o login** e **não** é tratado como falha comum de
  autenticação: é um **estado intermediário explícito** do login flow (pré-plano "decidido" #7, §4).
- **Sessões já ativas continuam emitindo tokens** salvo política de invalidação por realm em contrário
  (pré-plano "decidido" #8, §4).

### Tokens de ação (recuperação/verificação/troca-expirada)

- Tokens de recuperação/verificação são (pré-plano "decidido" #9, §5): **armazenados como hash**; com **TTL**
  obrigatório; de **uso único**; **revogados/expirados por nova emissão**; com **anti-enumeração** preservada.
- O **token bruto aparece uma única vez** (no link/código entregue); o banco guarda apenas o hash; **consumo é
  idempotente via update condicional** (`ConsumedAt is null AND RevokedAt is null AND ExpiresAt > now`); token de
  email/phone é **vinculado ao valor alvo** (`TargetValue`) para não verificar um endereço trocado depois (§5/§10).
- Hash do token e comparação usam **`RoyalIdentity.Security`** (`CryptoRandom`, `Hashing`/`FixedTimeComparer`) — a
  biblioteca técnica de folha já consumida pelo módulo (ver [plan-royalidentity-security.md](plan-royalidentity-security.md)).

### `SecurityStamp` (princípios; especificidades em Q6)

- `SecurityStamp` é um **versionador de estado sensível de segurança**, **não** um contador de qualquer mudança de
  perfil (pré-plano §7).
- **Devem incrementar** (lista recomendada e aceita, pré-plano §7 "Alterações que devem incrementar"): senha alterada
  pelo usuário; senha resetada por recuperação; senha definida por admin; credencial local removida/desabilitada;
  `SecurityStamp` regenerado por admin; conta desativada/deletada/bloqueada **quando a política do realm exigir derrubar
  sessões**.
- **Não devem incrementar** por default: falha de senha; incremento/reset de contador de lockout temporário; login
  bem-sucedido; alteração de display name/perfil comum; mudança de property dinâmica de perfil (pré-plano §7).
- A **fonte de verdade** do stamp é persistida na conta/credencial; o stamp é **capturado em `UserSession` na criação**;
  a comparação acontece em `IUserSessionService.IsSessionValidAsync` **quando a política do realm exigir** (pré-plano
  §7 "Onde guardar e comparar"). Como o core não pode referenciar o módulo `UserAccounts`, a leitura/validação do
  stamp atual precisa de um seam core-owned, realm-bound e implementado pelo fake e pela `.Integration` (Q15).
  Casos limítrofes de incremento, intervalo e ponto exato de comparação são **Q6**.

### Concorrência (princípios; detalhes em Q11)

- Estratégia geral aceita (pré-plano §10 "Estratégia geral recomendada"): transações curtas nos casos de credencial;
  **updates condicionais/atômicos** em vez de read-modify-write solto; `ConcurrencyStamp`/row version para o agregado
  (o `UserAccount.Version` já existe); **consumo idempotente** de tokens; **emitir eventos/outbox só após o commit**;
  **não** depender de lock em memória em produção.

### Eventos e auditoria (princípio de classificação; mecanismo em Q8)

- Três categorias (pré-plano §9, "decidido" #11): **(1) evento de domínio** (consistência/testes/reações internas),
  **(2) auditoria de segurança** (registro durável e consultável), **(3) outbox de integração** (só quando outro
  sistema precisa receber com entrega confiável). **Nem todo evento de segurança vai para outbox.** Eventos de
  login/falha são auditoria/telemetria por default, não outbox.

---

## Questões em aberto

> O usuário pediu explicitamente para **levantar os pontos a discutir como questões no plano** e **não assumir nada
> em dúvida**. Cada questão abaixo deve ser **decidida e registrada na ADR-017** antes da fase que a consome. A
> coluna "Bloqueia" indica as fases dependentes.

### Q1 — Modelo de credencial: singular vs coleção (pré-plano §1)
**Bloqueia:** Fase 2.
Hoje o v2 tem `UserAccountCredential` **singular** (Opção A). O pré-plano recomenda a **Opção C híbrida**
(`UserCredential` com `Type=Password` + `PasswordCredential` 1:1), no vocabulário de coleção, implementando só senha
agora, **se o custo for aceitável**. Decisão necessária:
- (a) **manter singular** `UserAccountCredential` e registrar migração futura para coleção quando MFA/passkey entrarem; ou
- (b) **evoluir agora** para `UserCredential`+`PasswordCredential` (coleção), com `unique (RealmId, UserAccountId, Type)`
  para `Type=Password, State=Active`.
> Risco de (b): refatora o que o v2 entregou e o `IUserAccountPasswordHasher`/autenticador já consomem. Risco de (a):
> migração maior quando os planos de MFA/passwordless chegarem. **Recomendação do pré-plano: (b) se o custo couber.**

### Q2 — Histórico de senha: armazenamento e política (pré-plano §2)
**Bloqueia:** Fase 3.
- (a) **Tabela própria** `PasswordHistory` (recomendado), (b) JSON na credencial, ou (c) só eventos (não recomendado)?
- Política só por **quantidade** (`PasswordHistoryCount`, já existe) ou também por **idade** ("não usado nos últimos N
  dias")?
- **Criptografar** o hash histórico (defesa em profundidade / coluna/TDE) ou apenas armazenar hash forte versionado e
  nunca logar/expor? (pré-plano "Criptografar o histórico?").
- Como a comparação é feita: a senha candidata é verificada com `IPasswordProtector`/`IUserAccountPasswordHasher`
  contra **cada hash histórico** (salt impede comparar hash com hash) — confirmar limite de N comparações por troca.

### Q3 — Desafio de troca forçada: token de ação vs sessão parcial (pré-plano §3/§4)
**Bloqueia:** Fases 4 e 5.
- (a) **Token de ação transitório** de uso único com `Purpose=ChangeExpiredPassword` (recomendado; reusa o modelo de
  tokens de ação; não cria sessão), ou (b) **sessão parcial de required-action** (estilo Keycloak) que **nunca** pode
  emitir token até a ação ser satisfeita?

### Q4 — Forma do "required action" na borda de autenticação (pré-plano §4)
**Bloqueia:** Fases 1 e 4 (emenda à ADR-014 + core).
Hoje `AuthenticationResult` (core) e `LocalAuthenticationResult` (módulo) só têm sucesso/falha+motivo. Decidir:
- (a) novo **reason** `PasswordChangeRequired`/`PasswordExpired`, ou (b) estrutura separada **`RequiredAction`**
  (mais extensível p/ verificações futuras)?
- `MustChangePassword` setado por admin deve **incrementar `SecurityStamp`**? (relaciona com Q6)
- Deve existir **opção por realm** para invalidar sessões quando admin seta `MustChangePassword`? (relaciona com Q7)

### Q5 — Lockout/bloqueio: campos simples vs taxonomia separada (pré-plano §6)
**Bloqueia:** Fase 7.
- (a) campos simples no usuário, ou (b) **separar** `PasswordLockout` (derivado da credencial) de
  `AccountAccessRestriction` (coleção administrativa: `AdminBlock`/`GeoBlock`/`TimeWindow`/`ClientRestriction`)?
  **Recomendação: (b), implementando primeiro só `PasswordLockout` + `AdminBlock`.**
- Hoje já existe `IsBlocked`/`BlockState` (admin block simples) **e** lockout na credencial. Decidir se `AdminBlock`
  permanece como o `BlockState` atual ou migra para a coleção `AccountAccessRestriction` (com prazo `StartsAt`/`EndsAt`).
- Lockout indefinido (`AccountLockoutDurationMinutes = 0`) exige **unlock administrativo**: confirmar o caso de uso
  `UnlockPasswordCredential` (já há `UserAccount.UnlockLocalCredential`).

### Q6 — `SecurityStamp`: local, gatilhos limítrofes e ponto de comparação (pré-plano §7)
**Bloqueia:** Fases 2 e 8.
- Campo em **`UserAccount.SecurityStamp`** ou em um **`UserSecurityState`** dedicado no módulo?
- Gatilhos **dependentes de política** (decidir cada um): email alterado (se email é login/`VerifyEmail`); telefone
  alterado; `MustChangePassword` por admin; roles/permissões alteradas (revogação imediata de autorização?).
- **Intervalo de revalidação** do cookie; comparação em **emissão de token**, **validação de cookie**, ou **ambos**?
- `SecurityStamp` único para autenticação **e** autorização, ou **stamp separado** de autorização para roles/permissões?

### Q7 — Política de invalidação: formato e defaults (pré-plano §8)
**Bloqueia:** Fases 1 e 8.
- **Flags** (`None`/`CurrentSession`/`OtherInteractiveSessions`/`AllInteractiveSessions`/`RefreshTokens`/
  `AllSessionsAndRefreshTokens`) ou **enum de presets** (`KeepCurrentSessionOnly`/`RevokeOtherSessions`/…)?
- Defaults por gatilho (recomendação do pré-plano, **a ratificar**): troca voluntária → manter atual + revogar outras
  se a policy pedir; **reset por recuperação** → revogar sessões + refresh por default; **admin reset** → revogar
  sessões + refresh por default; **import/migração** → não invalidar.
- Troca forçada por expiração deve **sempre** invalidar sessões antigas? O usuário pode escolher "sair de outros
  dispositivos" ao trocar a senha?

### Q8 — Eventos/auditoria/outbox: escopo neste plano (pré-plano §9)
**Bloqueia:** Fase 9.
- Confirmar **Opção B** (auditoria durável + outbox seletivo). Mas: **a auditoria durável e o outbox são construídos
  neste plano** ou apenas a **emissão/classificação** dos eventos (despacho via `IEventDispatcher` existente),
  **diferindo** a persistência durável/outbox para `plan-data-persistence` (§2)? O v2 deixou eventos de domínio
  **não despachados**; o core já tem `IEventDispatcher`/`IEventObserver`.
- Lista de eventos candidatos e classificação (auditoria vs outbox) no pré-plano §9 — ratificar.

### Q9 — Telefone: entra no agregado neste plano? (pré-plano "Email/telefone" + §6 das emendas)
**Bloqueia:** Fase 6.
Hoje **não há modelo de telefone** (só a opção `AllowChangePhoneNumber`). O roadmap §3 inclui "verificação de
email/**phone**". Decidir:
- (a) modelar **telefone** no agregado neste plano (entidade/coleção `UserAccountPhone` com
  `Number`/`IsPrimary`/`IsVerified`/`VerifiedAt`) + projeção `phone_number`/`phone_number_verified`; ou
- (b) **adiar telefone** inteiramente (somente email neste plano), removendo `phone` do escopo até um plano futuro.
> Se (a), há **emenda à ADR-015** (campos fixos/projeção) e novas `FixedFieldClaimProjection` para phone.

### Q10 — Email: `VerifiedAt` e semântica de `email_verified` (pré-plano "Emendas sugeridas" #3/#4)
**Bloqueia:** Fase 6.
- Adicionar **`VerifiedAt`** em `UserAccountEmail` (hoje só há `IsVerified`)?
- Confirmar/registrar que **`email_verified` deriva do email primário verificado** (a projeção fixa
  `EmailVerified → email_verified` já existe; falta amarrar à verificação real e garantir que **troca de email
  reseta `IsVerified`**).

### Q11 — Concorrência: estratégia concreta por provider (pré-plano §10)
**Bloqueia:** Fases 2, 7 e 10.
- Contador de falhas: **update atômico** (`... set failed = failed + 1 ... returning ...` no PostgreSql) vs
  **optimistic concurrency + retry** (usando `UserAccount.Version`) — e a alternativa transacional equivalente no
  Sqlite. Cobrir os 7 cenários do pré-plano §10 (falhas simultâneas; sucesso×falha; consumo duplo de token; nova
  emissão×consumo; troca de senha×emissão de token; admin unblock×falha; verificação de email×troca de email).

### Q12 — Endpoints/telas user-facing: neste plano ou no plano de UI? (roadmap §3 vs §5)
**Bloqueia:** Fases 4, 5 e 6 (parte HTTP/UI).
O roadmap §3 lista os **fluxos** (troca, recuperação, verificação) e a ADR-013 separa API/UI dos módulos. Decidir o
recorte:
- (a) este plano entrega **domínio + casos de uso + costura** e **endpoints/telas SSR mínimos** (`I*PageService` +
  Razor, padrão de [structure.md](../foundation/structure.md) §RoyalIdentity.Razor) para troca/recuperação/verificação;
  ou (b) este plano para em **domínio + casos de uso + costura**, e **todas as telas/endpoints** ficam para
  `plan-admin-api-ui`/um plano de UI de conta.
> Hoje há `Manage/ProfilePage.razor`, login/consent/logout; **não há** telas de troca/recuperação/verificação de senha.

### Q13 — Costura de execução da invalidação (módulo → IdP) (pré-plano §8)
**Bloqueia:** Fases 1, 5 e 8.
O módulo decide a política e **publica um comando/evento de invalidação**; o IdP executa sobre sessão/cookie/refresh.
Decidir a forma da costura no core (emenda à ADR-014): novos métodos no store de sessão (`EndAllForSubjectAsync`,
`EndOthersForSubjectAsync(exceptSid)`) + revogação de refresh tokens por subject/sessão (`IRefreshTokenStore`), e o
**porto** que a `.Integration` chama para disparar a execução. Confirmar se `IUserSessionStore`/`IUserSessionService`
ganham as APIs de revogação por subject (hoje só por `sid`).

### Q14 — `UserSession.ExpiresAt` × cookie lifetime × `UserSsoLifetime` × expiração de senha/lockout (roadmap §3; `UserSession.cs:38`)
**Bloqueia:** Fase 8.
Hoje `UserSession.ExpiresAt` é **reservado, sem comportamento** (o próprio XML doc diz "a future phase may define its
interaction with cookie lifetime / `UserSsoLifetime` / session expiry. Do not branch on it yet"). `UserSsoLifetime` é
**por client** e já avaliado no `PromptLoginDecorator` (força nova interação quando a duração da sessão excede). O
roadmap §3 pede explicitamente a "relação com `UserSsoLifetime`, cookie lifetime e sessões ativas". Decidir:
- `ExpiresAt` ganha comportamento (expira a sessão de verdade) ou continua reservado? Se ganha, qual fonte define o
  prazo (cookie lifetime do realm? um SSO lifetime de sessão por realm, distinto do `UserSsoLifetime` por client)?
- Como `IsSessionValidAsync` combina: sessão ativa **e** não expirada (`ExpiresAt`) **e** `SecurityStamp` válido — sem
  conflitar com a avaliação per-client de `UserSsoLifetime` no `PromptLoginDecorator` (que continua forçando interação,
  não invalidando a sessão).
- Expiração de senha/lockout **derrubam a sessão** ou apenas barram **nova** autenticação? (Decisão fechada §Senha diz
  que sessões ativas seguem válidas salvo política — confirmar que expiração de senha não é exceção implícita.)

### Q15 — Seam core-owned para capturar e validar `SecurityStamp` (módulo → IdP sem dependência inversa)
**Bloqueia:** Fases 2 e 8.
Situação: a **fonte de verdade** do `SecurityStamp` fica no módulo puro `UserAccounts`, mas `UserSession` e
`IUserSessionService.IsSessionValidAsync` pertencem ao core. O core **não pode** referenciar `UserAccounts`, e o módulo
puro **não pode** referenciar o core. Sem uma porta explícita, a implementação tenderia a uma de duas quebras:
dependência inversa do core para o módulo, ou validação no-op/duplicada que não garante a política.

Decidir a forma do seam core-owned, sempre realm-bound na construção e com primitivos/BCL na assinatura:
- (a) estender `IUserDirectory` com algo como `GetUserSecurityStateProvider(realm)`, retornando uma porta
  `IUserSecurityStateProvider` com `GetSecurityStampAsync(subjectId)` e/ou
  `IsSecurityStampValidAsync(subjectId, sessionStamp)`;
- (b) carregar o stamp no resultado de autenticação apenas para criação da sessão, e usar outro validator core-owned
  para revalidação posterior;
- (c) introduzir um `IUserSessionSecurityValidator` no core, implementado pelo fake e pela `.Integration`.

Consequências a registrar na ADR-017:
- a criação de sessão precisa saber qual stamp capturar, mas `IUserSessionService.StartAsync` hoje recebe só `Subject`;
- validação de cookie, `IProfileService.IsActiveAsync` e emissão/renovação de tokens devem passar pela mesma regra para
  evitar comportamentos divergentes;
- o fake in-memory e o módulo opt-in precisam implementar o mesmo contrato, preservando os contract tests da ADR-015;
- Q15 é diferente de Q13: Q15 lê/valida o estado atual de segurança; Q13 executa revogação ativa de sessões/refresh.

---

## Modelo relacional proposto (sujeito às Questões)

> **Proposta inicial**, não decisão fechada. A forma final depende de Q1 (credencial), Q2 (histórico), Q5 (lockout/
> restrições), Q6 (security stamp), Q9 (telefone). Tabelas realm-scoped (`RealmId` estrutural — ADR-015 §2.3); FKs por
> `UserAccount.Id` físico; eventos chaveiam por `(RealmId, SubjectId)`.

### `SecurityStamp` (Q6) — em `UserAccount` ou `UserSecurityState`

```text
-- Opção em UserAccount:
UserAccount.SecurityStamp      string not null   -- regenerado nos gatilhos da Decisão fechada §SecurityStamp
-- (UserSession ganha cópia capturada na criação; ver emenda ADR-014)
UserSession.SecurityStamp      string null       -- comparado em IsSessionValidAsync via seam Q15 quando a policy exigir
```

### `PasswordHistory` (Q2) — tabela própria proposta

```text
PasswordHistory
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null fk -> UserAccount.Id
  CredentialId          bigint null   -- se Q1 = coleção
  PasswordHash          string not null
  CreatedAt             timestamp not null
  Reason                string/int    -- Change/Reset/AdminSet/Import
  CreatedBySubjectId    string null   -- null p/ self-service/sistema

  index (RealmId, UserAccountId, CreatedAt)
```

### `UserAccountActionToken` (recuperação/verificação/troca-expirada)

```text
UserAccountActionToken
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null fk -> UserAccount.Id
  Purpose               string/int    -- PasswordRecovery/EmailVerification/PhoneVerification/ChangeExpiredPassword
  TokenHash             string not null
  TargetValue           string null   -- email/phone alvo normalizado (vincula o token ao valor)
  CreatedAt, ExpiresAt  timestamp
  ConsumedAt, RevokedAt timestamp null
  RevokedReason         string null
  CreatedIpHash, ConsumedIpHash, UserAgentHash  string null

  index  (RealmId, UserAccountId, Purpose)
  unique (RealmId, TokenHash)            -- backstop p/ consumo idempotente
```

### `AccountAccessRestriction` (Q5) — admin block / restrições futuras

```text
AccountAccessRestriction
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null fk -> UserAccount.Id
  Type                  string/int    -- AdminBlock (neste plano); GeoBlock/TimeWindow/ClientRestriction (futuro)
  Reason                string null
  StartsAt, EndsAt      timestamp null
  CreatedBySubjectId    string null
  RevokedAt             timestamp null

  index (RealmId, UserAccountId, Type)
```

### `UserAccountPhone` (Q9) — apenas se telefone entrar neste plano

```text
UserAccountPhone
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null fk -> UserAccount.Id
  Number, NormalizedNumber  string not null
  IsPrimary, IsVerified bool not null
  VerifiedAt            timestamp null

  unique (RealmId, UserAccountId, NormalizedNumber)
```

### Emenda em `UserAccountEmail` (Q10)

```text
UserAccountEmail.VerifiedAt   timestamp null   -- complementa o IsVerified existente
```

### Update condicional de consumo de token (pré-plano §5/§10)

```sql
update user_account_action_tokens
set consumed_at = @now
where realm_id = @realmId and token_hash = @tokenHash and purpose = @purpose
  and consumed_at is null and revoked_at is null and expires_at > @now;
-- 0 linhas afetadas => token inválido/consumido/expirado
```

---

## Contratos de borda alvo (emendas à ADR-014, aditivas)

> Dependem de Q4 (required action), Q6 (security stamp em sessão), Q13 (revogação por subject) e Q15 (seam de leitura/
> validação do stamp atual). Forma exata fechada na Fase 1.

| Porta / tipo | Hoje | Emenda proposta |
|---|---|---|
| `AuthenticationResult` / `AuthenticationFailureReason` (core) | sucesso/falha+motivo | required action (`PasswordChangeRequired`/`Expired`) — Q4 |
| `LocalAuthenticationResult` (módulo) | sucesso/falha+motivo+lockout | espelhar required action — Q4 |
| `UserSession` (core) | `SubjectId`,`sid`,`amr`,`idp`,`auth_time`,`clients`,`IsActive`,`ExpiresAt`(reservado) | + `SecurityStamp` capturado na criação — Q6/Q15 |
| `IUserSessionStore` (core, puro) | `Create`/`FindById`/`RecordClient`/`End` (por `sid`) | + revogação por subject (`EndAllForSubject`/`EndOthersForSubject`) — Q13 |
| `IUserSessionService` (core) | `GetCurrent`/`IsSessionValid`/`Start`/`End`/`RecordClient` | `IsSessionValid` compara `SecurityStamp` via seam core-owned quando policy exigir — Q6/Q15 |
| novo porto de estado de segurança (core-owned) | — | leitura/validação do stamp atual para captura e revalidação de sessão — Q15 |
| novo porto de revogação (core) | — | costura que a `.Integration` chama p/ executar invalidação (sessões+refresh) — Q13 |

`IUserAccountPasswordHasher` (módulo) e `IPasswordProtector` (core) **permanecem** como o seam de hash já estabelecido
no v2 (`PasswordProtectorAccountHasher`); o histórico (Q2) verifica candidatas reusando esse seam.

---

## Arquitetura alvo (Feature-Slice no módulo)

```text
RoyalIdentity.UserAccounts/                  (módulo puro — sem core, sem ASP.NET)
  Features/
    Accounts/
      Domain/
        UserAccount  UserAccountCredential  (existentes; estendidos: SecurityStamp, expiração)
        PasswordPolicy                       (existente)
        UserAccountPhone?                    (Q9)
        Events/  (novos eventos de segurança — Q8)
      Security/                              (NOVO slice de ciclo de segurança)
        Domain/
          PasswordHistory                    (Q2)
          UserAccountActionToken             (recuperação/verificação/troca-expirada)
          AccountAccessRestriction           (Q5)
          SecurityStamp / UserSecurityState? (Q6)
        UseCases/  (SmartCommands)
          ChangePassword (user-facing, gated)  ResetPasswordWithToken
          RequestPasswordRecovery              IssueActionToken / ConsumeActionToken
          VerifyEmail / VerifyPhone            ForceChangeExpiredPassword
          AdminSetPassword  AdminBlock/Unblock  RegenerateSecurityStamp
        Reads/  (read services / SmartSearch)
          IsPasswordExpired  GetActiveRestrictions  ...
  Options/
    UserAccountsRealmOptions  (+ bloco SecurityLifecycle — Q7)  PasswordOptions (existente)
  Infrastructure/
    Data/  (mapeamentos + migrations das novas tabelas; PostgreSql/Sqlite)
    Messaging/?  (só se Q8 trouxer outbox p/ este plano)

RoyalIdentity.UserAccounts.Integration/      (única ponte com o IdP)
  + adaptação do required action (Q4) p/ AuthenticationResult
  + implementação do seam core-owned de estado de segurança para capturar/validar SecurityStamp (Q15)
  + tradução da política de invalidação -> porto de revogação do core (Q13)
  + IUserAccountActionNotifier? (envio de email/SMS — gateway; ver Q12/Fora de escopo)

RoyalIdentity (core)                         (emendas aditivas atrás da ADR-014)
  AuthenticationResult/UserSession/IUserSessionStore/IUserSessionService + porto de estado de segurança + porto de revogação
RoyalIdentity.Razor                          (telas SSR mínimas — só se Q12 = (a))
```

> **Envio de email/SMS** (entrega do link/código de recuperação/verificação) é um **gateway externo**
> (`Infrastructure/Gateways/`, architecture.md §6). Definir se há um `INotificationGateway`/`IUserAccountActionNotifier`
> abstrato neste plano com implementação no-op/log, deixando o provedor real para o host. (Relaciona com Q12.)

---

## Ordem de execução

> **Protocolo aditivo:** estender o módulo e o core de forma aditiva, mantendo a integração **opt-in** e o **fake
> in-memory** como referência/contrato até a paridade estar provada (ADR-015 §2.11). Nenhuma fase deve quebrar a
> suíte OIDC existente.

1. **Fase 1 fecha a governança:** ADR-017 decide as Questões em aberto, emenda ADR-014/015 e cria o bloco de options.
   Nenhuma fase de código começa antes de a Questão que ela consome estar decidida.
2. Fase 2 estabelece o **modelo de credencial + `SecurityStamp` + concorrência** (base de tudo).
3. Fases 3-4 entregam **histórico/expiração** e **troca + required action**.
4. Fases 5-6 entregam **recuperação** e **verificação de email/telefone**.
5. Fase 7 entrega **lockout/bloqueio/restrições**.
6. Fase 8 entrega **invalidação** (consome `SecurityStamp` + costura de revogação).
7. Fase 9 entrega **auditoria/eventos/outbox** conforme Q8.
8. Fase 10 fecha **concorrência + contract tests + regressão OIDC**.

Build/test padrão (igual ao v2):

```powershell
dotnet build RoyalIdentity.sln
$env:Logging__EventLog__LogLevel__Default = "None"
dotnet test RoyalIdentity.sln --no-build --nologo
```

---

## Fase 1 - ADR-017, emendas e options de ciclo de segurança por realm

**Depende de:** decidir Q1–Q15 (esta fase é onde as decisões são tomadas e registradas).

**O que/como:** registrar as decisões antes de codar. Criar a **ADR-017** ("Ciclo de segurança da conta") como
documento autoritativo; emendar **ADR-014** (required action no resultado de autenticação; `SecurityStamp` em
`UserSession`; seam core-owned para validar stamp; revogação por subject) e **ADR-015** (evolução de credencial Q1;
telefone Q9; bloco de options); criar o bloco `SecurityLifecycle`/invalidação em `UserAccountsRealmOptions`.

**Tarefas:**

- [ ] Criar `adrs/ADR-017.md` decidindo Q1–Q15 (cada questão vira decisão registrada; seguir a regra de ADR do
      CLAUDE.md — decisão, não design).
- [ ] Emendar `adrs/ADR-014.md`: required action na borda de autenticação; `SecurityStamp` em `UserSession`;
      seam core-owned para leitura/validação do stamp atual; revogação por subject no store/serviço de sessão (aditivo).
- [ ] Emendar `adrs/ADR-015.md`: decisão de credencial (Q1); telefone no agregado (Q9); bloco de options de ciclo.
- [ ] Modelar o bloco de options por realm em `UserAccountsRealmOptions` (invalidação Q7; expiração/histórico já em
      `PasswordOptions`; consolidar sem duplicar dono).
- [ ] Atualizar `plans-roadmap-01.md` (§3 aponta para este plano) e `CLAUDE.md` (mover este plano para "ativo").
- [ ] Atualizar `.ai/foundation/*` se a emenda mudar contratos de borda documentados.

**Critérios de aceite:** ADR-017 existe e decide todas as questões; ADR-014/015 registram as emendas; documentação não
contradiz o plano; options têm dono único.

**Testes:** n/a (documentação) + testes de options (copy-on-create/validação) se o bloco entrar nesta fase.

---

## Fase 2 - Modelo de credencial, `SecurityStamp` e concorrência

**Depende de:** Q1 (credencial), Q6 (security stamp), Q11 (concorrência), Q15 (seam do stamp).

**O que/como:** materializar a decisão de credencial (manter singular ou evoluir para coleção) e introduzir o
`SecurityStamp` como versionador de estado sensível, com a base de concorrência.

**Tarefas:**

- [ ] Implementar a decisão Q1 (manter `UserAccountCredential` ou introduzir `UserCredential`+`PasswordCredential`),
      preservando o seam `IUserAccountPasswordHasher` e o autenticador atuais.
- [ ] Adicionar `SecurityStamp` (em `UserAccount` ou `UserSecurityState`, conforme Q6), gerado por
      `RoyalIdentity.Security.CryptoRandom`, com método de regeneração e os gatilhos da Decisão fechada §SecurityStamp.
- [ ] Garantir update atômico/optimistic dos contadores e do stamp (Q11), reusando `UserAccount.Version`.
- [ ] Mapear/migrar o schema (PostgreSql/Sqlite) sem quebrar round-trip do v2.
- [ ] Emenda no core: `UserSession` ganha `SecurityStamp` capturado na criação via seam decidido em Q15 (aditivo,
      atrás da ADR-014).

**Critérios de aceite:** stamp persistido e regenerável; gatilhos corretos incrementam, os de perfil não; round-trip
EF verde; `UserSession` carrega o stamp; mudança de credencial e stamp ocorrem na mesma transação.

**Testes:** unidade de domínio (gatilhos do stamp); persistência (round-trip/migrations); concorrência (Fase 10 amplia).

---

## Fase 3 - Política de senha: histórico e expiração com enforcement

**Depende de:** Q2 (histórico).

**O que/como:** dar enforcement às políticas que o v2 só persistiu — histórico e expiração — sem mover a fonte de
verdade das options para fora do módulo.

**Tarefas:**

- [ ] Implementar `PasswordHistory` conforme Q2; ao `SetPassword`/`ChangePassword`, gravar o hash anterior e podar por
      `PasswordHistoryCount` (e por idade, se Q2 incluir).
- [ ] Validar senha candidata contra **senha atual + N hashes históricos** via `IUserAccountPasswordHasher.Verify`.
- [ ] Implementar enforcement de **expiração** (`EnablePasswordExpiration`/`PasswordExpirationDays` × `PasswordChangedAt`):
      a verificação de expiração alimenta o required action da Fase 4 (não bloqueia silenciosamente).
- [ ] Garantir que `EnforcePasswordHistory`/`EnablePasswordExpiration = false` desligam o enforcement.

**Critérios de aceite:** reuso de senha recente é rejeitado quando habilitado; poda respeita a política; senha expirada
é detectada e roteada para troca (Fase 4); flags desligam o comportamento; hashes nunca aparecem em log/export.

**Testes:** unidade (histórico/poda/expiração); persistência de histórico; flags off.

---

## Fase 4 - Troca de senha e required action (`MustChangePassword`/expirada)

**Depende de:** Q3 (desafio), Q4 (forma do required action), Q12 (HTTP/UI).

**O que/como:** entregar a troca de senha **user-facing** (gated por `AllowChangePassword`) e o **estado intermediário**
de login para `MustChangePassword`/senha expirada, sem criar sessão real durante o desafio.

**Tarefas:**

- [ ] Caso de uso de troca user-facing (gated por `AllowChangePassword`), distinto do `ChangeUserAccountPassword` de
      seed/admin do v2 (que **não** é gated, por intenção).
- [ ] Introduzir o required action na borda (Q4): senha válida + (`MustChangePassword` **ou** expirada) ⇒ resultado de
      required action; **sem** sessão SSO/code/token; desafio curto (Q3) leva à troca; depois, **login real** com a nova
      senha (Decisão fechada §Senha).
- [ ] Integrar no `LoginFlowService`/`.Integration` o roteamento do required action (sem reescrever a borda).
- [ ] Endpoints/telas SSR de troca **apenas se Q12 = (a)** (padrão `I*PageService` + Razor).

**Critérios de aceite:** `MustChangePassword`/expirada **nunca** emitem token antes da troca; o desafio é uso único; pós
troca exige novo login; troca user-facing respeita `AllowChangePassword`; anti-enumeração preservada; regressão OIDC
verde.

**Testes:** unidade do caso de uso; integração do required action no login; (se Q12=(a)) testes de endpoint/UI.

---

## Fase 5 - Recuperação de senha e tokens de ação

**Depende de:** Q3 (modelo de token compartilhado), Q7 (invalidação no reset), Q12 (HTTP/UI),
Q13 (costura/registro da invalidação).

**O que/como:** implementar `UserAccountActionToken` e o fluxo de recuperação com anti-enumeração e uso único
idempotente.

**Tarefas:**

- [ ] Implementar `UserAccountActionToken` (hash, TTL, uso único, revogação por nova emissão, `TargetValue`).
- [ ] `RequestPasswordRecovery` (gated por `AllowForgotPassword`): resposta pública **sempre igual**; só emite token se a
      conta existir e a policy permitir; rate limit por IP/realm/identificador normalizado; sem revelar existência/estado.
- [ ] `ResetPasswordWithToken`: consumo idempotente (update condicional); aplica complexidade/histórico (Fase 3);
      registra/publica a solicitação de invalidação conforme Q7/Q13 (reset costuma revogar sessões/refresh por default
      — a ratificar). A execução efetiva sobre stores de sessão/refresh é concluída na Fase 8.
- [ ] Gateway de notificação (`INotificationGateway`/no-op) para entregar o link/código (ver Arquitetura alvo).
- [ ] Endpoints/telas SSR **apenas se Q12 = (a)**.

**Critérios de aceite:** token só verificável uma vez; nova emissão revoga anteriores; conta inexistente não cria token
nem revela; reset aplica políticas de senha; solicitação de invalidação segue a policy decidida; hashes de token nunca
expostos.

**Testes:** unidade (emissão/consumo/revogação/anti-enumeração); concorrência de consumo (Fase 10 amplia).

---

## Fase 6 - Verificação de email e telefone

**Depende de:** Q9 (telefone entra?), Q10 (`VerifiedAt`/semântica `email_verified`), Q12 (HTTP/UI).

**O que/como:** reusar o modelo de token de ação para verificar email e (se Q9=(a)) telefone, amarrando à projeção de
claims existente.

**Tarefas:**

- [ ] `EmailVerification` (token com `Purpose=EmailVerification`, vinculado ao `TargetValue`): ao consumir, marca
      `IsVerified`/`VerifiedAt` (Q10) do email **alvo**; troca de email reseta `IsVerified`.
- [ ] Confirmar/registrar que `email_verified` deriva do **email primário verificado** (projeção fixa já existe).
- [ ] **Se Q9 = (a):** modelar `UserAccountPhone` + `PhoneVerification` + projeções `phone_number`/
      `phone_number_verified` (emenda à ADR-015 §2.6); **se Q9 = (b):** remover telefone do escopo e registrar diferimento.
- [ ] Endpoints/telas SSR **apenas se Q12 = (a)**.

**Critérios de aceite:** verificação confirma só o valor alvo (não um valor trocado depois); `email_verified` coerente
com o primário; telefone conforme Q9; anti-enumeração preservada.

**Testes:** unidade (verificação/target/troca-reseta-verificação); projeção de claims (`email_verified`/`phone_*`).

---

## Fase 7 - Lockout, bloqueio administrativo e restrições de acesso

**Depende de:** Q5 (taxonomia), Q11 (concorrência de contadores).

**O que/como:** consolidar o lockout temporário (já no autenticador) e o bloqueio administrativo na taxonomia decidida,
implementando primeiro `PasswordLockout` + `AdminBlock`.

**Tarefas:**

- [ ] Implementar a decisão Q5: manter `IsBlocked`/`BlockState` como `AdminBlock` simples **ou** migrar para
      `AccountAccessRestriction` (com prazo). Lockout temporário permanece derivado da credencial.
- [ ] Caso de uso `UnlockPasswordCredential` (admin) que zera contador/`LockoutEndAt` e incrementa versão (Q11/§10);
      lockout indefinido (`AccountLockoutDurationMinutes = 0`) exige unlock administrativo.
- [ ] Auditoria de falhas (eventos da Fase 9): `PasswordFailureRegistered`/`PasswordLockoutStarted`/`...Ended`/`...Reset`.
- [ ] Mapear cada estado ao reason de borda existente (`Inactive`/`Blocked`) sem conflar lockout com admin block.

**Critérios de aceite:** lockout temporário e bloqueio administrativo são estados distintos; unlock administrativo
funciona; contadores corretos sob concorrência; reasons de borda corretos; restrições futuras (`Geo`/`Time`/`Client`)
modeladas mas não implementadas.

**Testes:** unidade (lockout/unlock/admin block); concorrência (Fase 10).

---

## Fase 8 - Invalidação de sessões/cookies/refresh tokens

**Depende de:** Q6 (security stamp/comparação), Q7 (política/defaults), Q13 (costura de execução),
Q14 (`ExpiresAt`/`UserSsoLifetime`/cookie lifetime), Q15 (seam do stamp).

**O que/como:** ligar o `SecurityStamp` à validação de sessão e executar a política de invalidação por realm sobre
sessões/cookies/refresh tokens, com a execução no IdP (a costura é a emenda da ADR-014).

**Tarefas:**

- [ ] `IUserSessionService.IsSessionValidAsync` compara o `SecurityStamp` da sessão com o atual por meio do seam
      decidido em Q15 **quando a policy do realm exigir** (intervalo/ponto conforme Q6); cookie `OnValidatePrincipal`
      continua sendo a costura por request.
- [ ] Emenda no core (Q13): revogação por subject no store/serviço de sessão (`EndAllForSubject`/`EndOthersForSubject`)
      e revogação de refresh tokens por subject/sessão (`IRefreshTokenStore`); implementar no fake in-memory.
- [ ] `.Integration` traduz a política do módulo (Q7) em chamadas ao porto de revogação do core (idempotentes, pós-commit).
- [ ] Aplicar os defaults por gatilho ratificados em Q7 (troca voluntária/reset/admin/import).
- [ ] Resolver Q14: definir (ou manter reservado) o comportamento de `UserSession.ExpiresAt` e como `IsSessionValidAsync`
      combina ativo + não-expirado + stamp, sem conflitar com a avaliação per-client de `UserSsoLifetime` no
      `PromptLoginDecorator`; confirmar que expiração de senha não derruba sessão ativa por implícito.

**Critérios de aceite:** sessão com stamp antigo é inválida quando a policy exige; revogar outras/todas as sessões e
refresh tokens funciona por subject; execução idempotente e pós-commit; sessões ativas seguem emitindo token quando a
policy não pede invalidação (Decisão fechada §Senha).

**Testes:** integração (stamp inválida cookie; revogação por subject; refresh revogado não renova); regressão OIDC.

---

## Fase 9 - Auditoria, eventos e (seletivo) outbox

**Depende de:** Q8 (escopo de auditoria/outbox).

**O que/como:** emitir e classificar os eventos de segurança nas três categorias (domínio/auditoria/outbox), sem criar
infraestrutura de outbox além do que Q8 autorizar.

**Tarefas:**

- [ ] Adicionar os eventos de domínio de segurança (lista do pré-plano §9), chaveados por `(RealmId, SubjectId)`.
- [ ] Classificar cada evento (auditoria obrigatória vs candidato a outbox) conforme Q8.
- [ ] **Se Q8 mandar persistir auditoria/outbox neste plano:** implementar o store de auditoria durável e/ou outbox;
      **senão:** despachar via `IEventDispatcher` do core e **diferir** persistência durável/outbox para
      `plan-data-persistence` (§2), registrando o diferimento.
- [ ] Garantir que eventos só são publicados **após o commit** (§10).

**Critérios de aceite:** eventos certos disparam nos pontos certos; classificação coerente; nenhuma máquina de outbox
introduzida sem decisão Q8; sem ruído de outbox para login/falha.

**Testes:** unidade (emissão por caso de uso); (se aplicável) durabilidade/idempotência de auditoria/outbox.

---

## Fase 10 - Concorrência, contract tests e regressão OIDC

**Depende de:** Q11 (concorrência) e o conjunto entregue nas fases anteriores.

**O que/como:** provar concorrência, paridade fake×módulo e que a suíte OIDC permanece verde com a integração opt-in.

**Tarefas:**

- [ ] Cobrir os 7 cenários de concorrência do pré-plano §10 (falhas simultâneas; sucesso×falha; consumo duplo de token;
      nova emissão×consumo; troca×emissão de token; admin unblock×falha; verificação×troca de email).
- [ ] Estender os **contract tests** de `IUserDirectory`/portas de borda para os novos comportamentos
      (required action, invalidação, verificação), executados contra **fake in-memory** e contra **módulo + Sqlite**.
- [ ] Atualizar a matriz de testes ([plan-users-accounts-test-matrix.md](plan-users-accounts-test-matrix.md)) com os
      casos deste plano.
- [ ] Rodar a suíte completa do IdP **contra o fake** e **contra o módulo opt-in**; manter o fake como default até verde.
- [ ] Atualizar `Tests.Architecture` se novas fronteiras/portos exigirem guardas.

**Critérios de aceite:** cenários de concorrência verdes; contract tests passam nas duas implementações; suíte OIDC
verde com integração opt-in; fake permanece disponível; fronteiras de arquitetura intactas.

**Testes:** `dotnet test RoyalIdentity.sln` + suites do módulo/providers.

---

## Invariantes a preservar

1. O **módulo puro** continua sem referência a `RoyalIdentity` (core) e sem ASP.NET; só a `.Integration` conhece o IdP.
2. **`sub` = `SubjectId`**, imutável; nenhuma operação de ciclo de segurança troca o `sub`.
3. **Anti-enumeração**: login, recuperação e verificação não revelam existência/estado da conta; motivo interno fica
   só em auditoria.
4. **`MustChangePassword`/senha expirada nunca emitem token** antes da troca; após a troca, exige-se novo login real.
5. **Sessões ativas seguem emitindo tokens** salvo política de invalidação por realm.
6. **Tokens de ação**: hash-only, TTL, uso único idempotente, revogados por nova emissão, vinculados ao `TargetValue`.
7. **`SecurityStamp` versiona estado sensível de segurança**, não mudanças de perfil; comparado só quando a policy do
   realm exigir.
8. **Hashes de senha/token nunca** são logados, exportados ou expostos.
9. Mutações de credencial/stamp e a invalidação correspondente são **transacionais** e a publicação de eventos é
   **pós-commit**; consumo de token e revogação são **idempotentes**.
10. Realm isolation: nada cruza realm; `RealmId` estrutural nas novas tabelas.
11. Políticas de credencial/ciclo têm **dono único** em `UserAccountsRealmOptions` (sem duplicar no IdP).
12. A escolha de algoritmo de hash e a comparação constante continuam em `RoyalIdentity.Security` (sem reimplementar).

---

## Critérios globais de aceite

1. ADR-017 criada decidindo Q1–Q15; ADR-014/015 emendadas; documentação alinhada.
2. Senha local cobre: troca (user-facing gated), recuperação, histórico, expiração com enforcement e `MustChangePassword`.
3. Lockout temporário e bloqueio administrativo são estados distintos; unlock administrativo funciona.
4. Verificação de email (e telefone, se Q9=(a)) por token de ação, com `email_verified` coerente.
5. `SecurityStamp` implementado, capturado na sessão e usado na validação por seam core-owned quando a policy exigir.
6. Invalidação por realm de sessões/cookies/refresh tokens funciona pela costura `.Integration` → core (Q13).
7. Eventos de segurança emitidos/classificados (auditoria/outbox conforme Q8).
8. Concorrência coberta pelos 7 cenários; consumo/revogação idempotentes.
9. Contract tests provam paridade fake×módulo; suíte OIDC verde com integração opt-in; fake permanece default.
10. Módulo puro independente do core; fronteiras protegidas por `Tests.Architecture`.

---

## Riscos

- **Escopo amplo:** este é o maior plano de conta. Risco de inchaço — manter cada slice pequeno e gated pelas Questões;
  diferir telas/outbox/telefone quando a decisão permitir.
- **Emenda da borda (ADR-014):** required action, `SecurityStamp` em sessão e revogação por subject tocam core + fake +
  testes. Fazer aditivo e cedo (Fase 1/2) evita acoplamento posterior.
- **Refator de credencial (Q1):** evoluir para coleção mexe no que o v2 entregou (autenticador, hasher, persistência).
  Avaliar custo×benefício antes; se incerto, manter singular e registrar migração.
- **`SecurityStamp` mal escopado:** se incrementar em mudanças de perfil, derruba sessões à toa; se de menos, deixa
  credencial comprometida válida. Q6 deve fixar gatilhos e ponto de comparação.
- **Seam do `SecurityStamp` mal definido:** se o core tentar ler diretamente o módulo, viola as fronteiras da
  ADR-013/014/015; se a validação ficar no-op ou duplicada, a política de invalidação por stamp não é confiável. Q15
  deve fechar a porta core-owned usada por fake e `.Integration`.
- **Anti-enumeração:** fácil de vazar por diferença de tempo/resposta/mensagem entre conta existente e inexistente.
  Cobrir com testes específicos.
- **Concorrência:** contadores de falha e consumo de token são pontos clássicos de corrida; preferir updates atômicos a
  read-modify-write; divergência PostgreSql×Sqlite deve ser coberta por testes.
- **Persistência operacional ainda in-memory:** a invalidação de sessões/refresh depende de stores hoje fake; a costura
  deve ser definida de modo que `plan-data-persistence` a honre sem reescrita.
- **Outbox prematuro (Q8):** criar máquina de outbox aqui pode duplicar o que o plano de persistência/mensageria fará.

---

## Referências

- Pré-plano (insumo principal): [an-users-sec-preplan.md](../analisys/an-users-sec-preplan.md).
- Roadmap: [plans-roadmap-01.md](plans-roadmap-01.md) §3.
- Plano base (CONCLUÍDO): [plan-users-accounts-module-v2.md](plan-users-accounts-module-v2.md).
- Plano de borda/sessão (CONCLUÍDO): [plan-users-edge-session.md](plan-users-edge-session.md).
- Biblioteca técnica (CONCLUÍDO): [plan-royalidentity-security.md](plan-royalidentity-security.md).
- Matriz de testes do módulo: [plan-users-accounts-test-matrix.md](plan-users-accounts-test-matrix.md).
- Arquitetura: [architecture.md](../foundation/architecture.md), [structure.md](../foundation/structure.md),
  [product.md](../foundation/product.md).
- ADRs: [ADR-013](../../adrs/ADR-013.md), [ADR-014](../../adrs/ADR-014.md), [ADR-015](../../adrs/ADR-015.md),
  [ADR-016](../../adrs/ADR-016.md). (Este plano cria a **ADR-017** e emenda ADR-014/015.)
- Código de referência: `RoyalIdentity.UserAccounts/Features/Accounts/Domain/{UserAccount,UserAccountCredential,
  UserAccountEmail,UserAccountEvents}.cs`, `RoyalIdentity.UserAccounts/Options/{UserAccountsRealmOptions,PasswordOptions}.cs`,
  `RoyalIdentity/Users/{AuthenticationResult,UserSession}.cs`,
  `RoyalIdentity/Users/Contracts/{IUserSessionStore,IUserSessionService,ILocalUserAuthenticator,IPasswordProtector}.cs`.
- Comparação externa (no pré-plano): ASP.NET Core Identity (password/lockout/security stamp/tokens), Keycloak
  (credentials/required actions/brute-force).
```
