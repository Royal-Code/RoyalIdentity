# Plan: Credenciais e Ciclo de Segurança da Conta (`plan-users-security-lifecycle`)

## Status: EM ANDAMENTO - 15 questões decididas (Q1–Q15); Fases 1–6 concluídas

## Progresso

`#########-` **90%** - 9 de 10 fases

| Fase | Estado |
|---|---|
| Fase 1 - ADR-017, emendas e options de ciclo de segurança por realm | Concluida |
| Fase 2 - Modelo de credencial, `SecurityStamp` e concorrência | Concluida |
| Fase 3 - Política de senha: histórico e expiração com enforcement | Concluida |
| Fase 4 - Troca de senha e required action (`MustChangePassword`/expirada) | Concluida |
| Fase 5 - Recuperação de senha e tokens de ação | Concluida |
| Fase 6 - Verificação de email e telefone | Concluida |
| Fase 7 - Lockout, bloqueio administrativo e restrições de acesso | Concluida |
| Fase 8 - Invalidação de sessões/cookies/refresh tokens | Concluida |
| Fase 9 - Auditoria, eventos e (seletivo) outbox | Concluida |
| Fase 10 - Concorrência, contract tests e regressão OIDC | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `#` por fase concluida, `%` e `X de 10`). Ex.: 3 fases => `###-------` **30%** - 3 de 10.

> **Aviso de prontidão:** as **15 questões estão decididas** (ver `#### QN - Conclusão`). Próximo passo: **Fase 1** —
> registrar as decisões na **ADR-017** e emendar ADR-014/015. Os modelos relacionais e contratos abaixo já refletem as
> decisões; ajustes finos de assinatura fecham na ADR-017.

> **Rodada 2 (analisada):** **todas as 15 questões decididas** (ver `#### QN - Conclusão`). A reconciliação central
> **stamp × invalidação** foi fechada na Q7 com **`SecurityStamp` (versão geral) + `SessionsValidAfter` (marcador de
> invalidação)**.

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
  uso de domínio** e a **costura** que essas telas consumirão; Q12 decidiu que os **endpoints/telas user-facing
  mínimos** ficam fora deste plano.
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
> para [Histórico de decisões](#histórico-de-decisões).

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

### `SecurityStamp` e invalidação (Q6/Q7/Q15)

- `SecurityStamp` é um **versionador de estado sensível de segurança**, **não** um contador de qualquer mudança de
  perfil.
- `SecurityStamp` muda em todos os gatilhos sensíveis decididos em Q6/Q7; falha de login, login bem-sucedido e mudanças
  comuns de perfil **não** mudam o stamp.
- `SessionsValidAfter` é o marcador de invalidação: move só quando o gatilho deve derrubar sessões/tokens.
- `UserSession.SecurityStamp` é capturado no sign-in para revalidação de cookie/claims e rastreabilidade; a validade forte
  da sessão usa `session.StartedAt >= conta.SessionsValidAfter`, lido por `IUserSecurityStateProvider` quando a policy do
  realm exigir (Q15).

### Concorrência (princípios; detalhes em Q11)

- Estratégia geral aceita (pré-plano §10 "Estratégia geral recomendada"): transações curtas nos casos de credencial;
  **updates condicionais/atômicos** em vez de read-modify-write solto; `ConcurrencyStamp`/row version para o agregado
  (o `UserAccount.Version` já existe; **optimistic concurrency + retry** decidida na Q11 para credencial/contadores/stamp);
  **consumo idempotente** de tokens (UPDATE condicional); **emitir eventos/outbox só após o commit**;
  **não** depender de lock em memória em produção.

### Eventos e auditoria (princípio de classificação; mecanismo em Q8)

- Três categorias (pré-plano §9, "decidido" #11): **(1) evento de domínio** (consistência/testes/reações internas),
  **(2) auditoria de segurança** (registro durável e consultável), **(3) outbox de integração** (só quando outro
  sistema precisa receber com entrega confiável). **Nem todo evento de segurança vai para outbox.** Eventos de
  login/falha são auditoria/telemetria por default, não outbox.

---

## Histórico de decisões

> As questões abaixo registram o raciocínio, as respostas e a conclusão que a ADR-017 deve consolidar. Q1–Q15 estão
> decididas; a coluna "Bloqueia" indica as fases que consomem cada decisão.

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

#### Q1 - Resposta 1

A classe `UserAccountCredential` tem muita peculiariedades para autenticação por password. A variável no `UserAccount` se chama `LocalCredential`. É muito específico, e acredito que tentar transformar em algo genérico pode gerar gambiarras no futuro. Então prefiro manter assim.

#### Q1 - Conclusão

A resposta **atende**. Decisão: **manter `UserAccountCredential` singular** (`LocalCredential` no `UserAccount`); não genericizar. Tipos futuros (MFA/passkey/OTP/passwordless) serão modelados como **credenciais/entidades próprias** nos respectivos planos, sem forçar esta classe a virar payload genérico (evita a gambiarra prevista).

Aplicar ao plano:
- Fase 2: a tarefa passa a "preservar `UserAccountCredential` singular"; a opção (b) coleção sai.
- `PasswordHistory` referencia `UserAccount.Id` diretamente (sem `CredentialId`).
- Remover o item "Refator de credencial (Q1)" do bloco **Riscos** (a opção descartada).

### Q2 — Histórico de senha: armazenamento e política (pré-plano §2)
**Bloqueia:** Fase 3.
- (a) **Tabela própria** `PasswordHistory` (recomendado), (b) JSON na credencial, ou (c) só eventos (não recomendado)?
- Política só por **quantidade** (`PasswordHistoryCount`, já existe) ou também por **idade** ("não usado nos últimos N
  dias")?
- **Criptografar** o hash histórico (defesa em profundidade / coluna/TDE) ou apenas armazenar hash forte versionado e
  nunca logar/expor? (pré-plano "Criptografar o histórico?").
- Como a comparação é feita: a senha candidata é verificada com `IPasswordProtector`/`IUserAccountPasswordHasher`
  contra **cada hash histórico** (salt impede comparar hash com hash) — confirmar limite de N comparações por troca.

#### Q2 - Resposta 1

- Usar **Tabela própria**. 
- Políticas por **quantidade** e **idade**.
- apenas armazenar hash forte versionado.
- tem que ter a nova senha plana, aplicar o hash conforme o hash do histórico e comparar.

#### Q2 - Conclusão

A resposta **atende**. Decisão: **tabela própria `PasswordHistory`**; política por **quantidade** (`PasswordHistoryCount`, já existe) **e por idade**; **só hash forte versionado** (sem criptografia adicional; nunca logar/expor). Comparação: a **nova senha plana** é verificada contra cada hash histórico relevante via `IUserAccountPasswordHasher.Verify` — o formato `$RIPWD$` é autocontido (salt + iterações embutidos), então "aplicar o hash conforme o histórico e comparar" = `Verify(novaSenhaPlana, hashHistórico)` (não se compara hash com hash).

Aplicar ao plano:
- Adicionar a `PasswordOptions` um campo de **idade** (ex.: `PasswordHistoryMaxAgeDays` ou `PasswordReuseWindowDays`). O conjunto a comparar = (últimos `PasswordHistoryCount` itens) ∪ (itens dentro da janela de idade).
- Fase 3: confirmar o limite de N verificações por troca (N = tamanho desse conjunto).
- Guardas de configuração ficam em `UserAccountsRealmOptions.Validate()`: valores negativos são inválidos; histórico
  ligado exige quantidade ou idade; se idade estiver ativa, `MaxPasswordHistoryComparisons` deve permitir ao menos uma
  comparação. Os casos de uso podem assumir options coerentes.

### Q3 — Desafio de troca forçada: token de ação vs sessão parcial (pré-plano §3/§4)
**Bloqueia:** Fases 4 e 5.
- (a) **Token de ação transitório** de uso único com `Purpose=ChangeExpiredPassword` (recomendado; reusa o modelo de
  tokens de ação; não cria sessão), ou (b) **sessão parcial de required-action** (estilo Keycloak) que **nunca** pode
  emitir token até a ação ser satisfeita?

#### Q3 - Resposta 1

- Preciso de mais detalhes de cada uma das opções. Minha visão é em direção a opção `a`, mas eu precisaria entender o estilo KeyCloak e quais as consequencias e implicações.

#### Q3 - Comentários e Questões 2

A resposta **não decide** — pede detalhamento das opções, do estilo Keycloak e das implicações. Segue a comparação.

**Opção A — Token de ação transitório.** Conferida a senha, se há ação obrigatória (senha expirada / `MustChangePassword`), o login **não conclui**: emite-se um token curto, de uso único, `Purpose=ChangeExpiredPassword` (o mesmo `UserAccountActionToken` da Q5/Fase 5) e redireciona-se para a tela de troca, que exige esse token. Concluída a troca, o token é consumido e o usuário **refaz o login** com a nova senha.
- Implicações: simples; reusa o modelo de tokens de ação; **nenhuma** máquina de estado nova no IdP; nenhuma sessão é criada antes da ação. Custo de UX: **dois logins** (antes e depois). É exatamente o que a Decisão fechada §Senha já presume ("após a troca, login real").

**Opção B — Sessão parcial de required-action (estilo Keycloak).** No Keycloak há a *authentication session* (cookie `AUTH_SESSION_ID`), **distinta** da sessão SSO (`KEYCLOAK_IDENTITY`). Quando o usuário autentica mas tem *required actions* pendentes (`UPDATE_PASSWORD`, `VERIFY_EMAIL`, `CONFIGURE_TOTP`…), o fluxo de autenticação **não termina**: o usuário fica nessa sessão parcial, as ações são apresentadas **em sequência** e **nenhum** code/token é emitido até todas serem satisfeitas. Ao concluir, a sessão parcial é **promovida** a sessão SSO e o `authorize` original **continua sem novo login**.
- Implicações: melhor UX (fluxo contínuo, sem segundo login, encadeia múltiplas ações); porém exige um **estado de required-action explícito** no `LoginFlowService` e a **garantia forte** de que a sessão parcial nunca emite token. Mais complexidade no IdP. Contraria a decisão atual de "refazer login".

**Recomendação:** **Opção A** para este plano — menor risco no IdP, reusa o modelo de token de ação e é coerente com a decisão já tomada de re-login. O `RequiredAction` da Q4 fica como **ponto de evolução**: permite migrar para B (resume-in-flow) depois, sem retrabalho, se a UX de duplo login incomodar.

**Questão 2 (Q3.2):** Confirmar **Opção A** (token de ação + re-login), ou adotar **B** (required-action resume-in-flow), o que **reabriria** a decisão fechada "após a troca, login real"?

#### Q3 - Resposta 2

Ficamos com opção 1.

#### Q3 - Conclusão

A resposta **atende**. Decisão: **Opção A** — token de ação transitório (`Purpose=ChangeExpiredPassword`, de uso único) + **re-login** após a troca. Sem sessão parcial/resume-in-flow neste plano; o `RequiredAction` (Q4) fica como **ponto de evolução** para B no futuro, sem retrabalho.

Aplicar ao plano: Fases 4/5 já refletem (token de ação + re-login); marcar Q3 como decidido (Opção A).

### Q4 — Forma do "required action" na borda de autenticação (pré-plano §4)
**Bloqueia:** Fases 1 e 4 (emenda à ADR-014 + core).
Hoje `AuthenticationResult` (core) e `LocalAuthenticationResult` (módulo) só têm sucesso/falha+motivo. Decidir:
- (a) novo **reason** `PasswordChangeRequired`/`PasswordExpired`, ou (b) estrutura separada **`RequiredAction`**
  (mais extensível p/ verificações futuras)?
- `MustChangePassword` setado por admin deve **incrementar `SecurityStamp`**? (relaciona com Q6)
- Deve existir **opção por realm** para invalidar sessões quando admin seta `MustChangePassword`? (relaciona com Q7)

#### Q4 - Resposta 1

- Acho que retornar junto um `RequiredAction` seria interessante.
- Acho que `MustChangePassword` só deve invalidar seções se configurado no Realm, por padrão acho que não deveria.
- `MustChangePassword` setado por admin deve **incrementar `SecurityStamp`**: se existe uma opção de um administrador marcar uma senha ou conta como necessária para trocar a senha, ou seja, existir um campo `MustChangePassword`, então deve incrementar. Se for algo como senha expirada, aí não.
- Sim, deve ter opção.

#### Q4 - Conclusão

A resposta **atende** (com uma reconciliação a fechar em Q7-2). Decisão:
- O resultado de autenticação carrega uma estrutura **`RequiredAction`** (não um novo `reason`) — mais extensível para verificações futuras.
- `MustChangePassword` setado por **admin** → **incrementa `SecurityStamp`**; **expiração de senha → não incrementa** (não é sinal de comprometimento).
- Há **opção por realm** para invalidar sessões quando admin seta `MustChangePassword`; **default = não invalidar**.

⚠ **Reconciliação (ver Q7-2):** "incrementar o stamp" ≠ "derrubar sessão". Com um **único** stamp comparado **sempre** na emissão de token (Q6), incrementar em admin-`MustChangePassword` **invalidaria** a emissão para sessões antigas — o que **conflita** com "por default não invalida". Para honrar ambos, o enforcement do stamp precisa ser **gated por política**, e/ou `MustChangePassword` ser enforçado **na autenticação** (via `RequiredAction`), não via stamp na emissão. A forma final está em **Q7-2**.

Aplicar ao plano: tabela de contratos → "required action via estrutura `RequiredAction` (decidido)"; a opção por realm entra no bloco de options (Fase 1).

### Q5 — Lockout/bloqueio: campos simples vs taxonomia separada (pré-plano §6)
**Bloqueia:** Fase 7.
- (a) campos simples no usuário, ou (b) **separar** `PasswordLockout` (derivado da credencial) de
  `AccountAccessRestriction` (coleção administrativa: `AdminBlock`/`GeoBlock`/`TimeWindow`/`ClientRestriction`)?
  **Recomendação: (b), implementando primeiro só `PasswordLockout` + `AdminBlock`.**
- Hoje já existe `IsBlocked`/`BlockState` (admin block simples) **e** lockout na credencial. Decidir se `AdminBlock`
  permanece como o `BlockState` atual ou migra para a coleção `AccountAccessRestriction` (com prazo `StartsAt`/`EndsAt`).
- Lockout indefinido (`AccountLockoutDurationMinutes = 0`) exige **unlock administrativo**: confirmar o caso de uso
  `UnlockPasswordCredential` (já há `UserAccount.UnlockLocalCredential`).

#### Q5 - Resposta 1

- **separar** `PasswordLockout` de `AccountAccessRestriction`.
- `UserAccountBlockState` já é um bloqueio administrativo. Este é mais pessoal. Para bloqueios como `GeoBlock`/`TimeWindow`/`ClientRestriction` é necessário outras abordagens, para ser aplicada para todos, ou para determinados grupos de usuários. Precisa refinar isso e elaborar um design multifuncional.
- Deve existir um **unlock administrativo**. O desbloqueio pode ocorrer por troca de senha ou manualmente por um administrador. No futuro deve existir um motor de permissão, essas funções administrativas poderão requerer permissões específicas.

#### Q5 - Comentários e Questões 2

A resposta **decide a separação** mas **pede refino** do design de restrições de grupo/realm e **muda** o modelo que propus.

Decidido:
- **Separar** `PasswordLockout` (derivado da credencial — já existe) do bloqueio administrativo.
- `UserAccountBlockState` permanece como o **AdminBlock pessoal** (por conta) — não migra para uma coleção.
- **Unlock administrativo** existe (por troca de senha **ou** manualmente por admin); funções administrativas serão futuramente gated por um **motor de permissões** (fora deste plano).

A revisar (pedido do autor): `GeoBlock`/`TimeWindow`/`ClientRestriction` **não** são bloqueios por conta — são políticas aplicáveis a **todos os usuários ou a grupos**, exigindo um **design multifuncional próprio** (por realm/grupo). Logo, a tabela `AccountAccessRestriction` per-account que propus **não** é o modelo certo para esses casos.

Proposta para este plano:
- Escopo = `PasswordLockout` + `AdminBlock` pessoal (o `UserAccountBlockState` atual), opcionalmente com **janela temporal** (`StartsAt`/`EndsAt`) se quisermos bloqueio com prazo.
- **Aposentar** a tabela `AccountAccessRestriction` per-account do modelo proposto.
- Registrar `Geo`/`Time`/`Client` como **restrições por realm/grupo** — design futuro próprio (relaciona com o motor de permissões), fora deste plano.

**Questão 2 (Q5.2):** Confirmar: (a) manter `UserAccountBlockState` como AdminBlock pessoal — **com** ou **sem** janela `StartsAt`/`EndsAt`? (b) aposentar a `AccountAccessRestriction` per-account; (c) `Geo/Time/Client` viram design futuro por realm/grupo. Algum desses (ex.: bloqueio com prazo) é requisito **deste** plano ou tudo que passa de `PasswordLockout` + `AdminBlock` simples fica para depois?

#### Q5 - Resposta 2

`UserAccountBlockState` poderia ter janela `StartsAt`/`EndsAt`, isso daria para bloquear usuários se estiverem de férias, por exemplo, ou outras causas.
aposentar a `AccountAccessRestriction`.
Podemos deixar o bloqueio `Geo/Time/Client` para depois, mas isso deve ir para um plano futuro ou backlog para não se perder.


#### Q5 - Conclusão

A resposta **atende**. Decisão:
- `UserAccountBlockState` ganha janela **`StartsAt`/`EndsAt`** (ex.: bloquear conta em férias) — AdminBlock pessoal com **prazo opcional** (null = indefinido).
- **Aposentar** a tabela `AccountAccessRestriction` per-account (sai do modelo).
- `Geo`/`Time`/`Client` ficam para **plano futuro** e são **registrados no backlog** ("para não se perder").
- **Unlock administrativo** por troca de senha **ou** por admin; futuras funções admin gated por motor de permissões (fora de escopo).

Aplicar ao plano: modelo → remover `AccountAccessRestriction`, adicionar `StartsAt`/`EndsAt` ao BlockState; Fase 7 → AdminBlock com janela + unlock; Arquitetura → remover o tipo do slice; **adicionar item no backlog** (Geo/Time/Client como restrições por realm/grupo + motor de permissões).

### Q6 — `SecurityStamp`: local, gatilhos limítrofes e ponto de comparação (pré-plano §7)
**Bloqueia:** Fases 2 e 8.
- Campo em **`UserAccount.SecurityStamp`** ou em um **`UserSecurityState`** dedicado no módulo?
- Gatilhos **dependentes de política** (decidir cada um): email alterado (se email é login/`VerifyEmail`); telefone
  alterado; `MustChangePassword` por admin; roles/permissões alteradas (revogação imediata de autorização?).
- **Intervalo de revalidação** do cookie; comparação em **emissão de token**, **validação de cookie**, ou **ambos**?
- `SecurityStamp` único para autenticação **e** autorização, ou **stamp separado** de autorização para roles/permissões?

#### Q6 - Resposta 1

- `SecurityStamp` me parece um campo, talvez poderia ser um value object.
- Cada um destes "Gatilhos **dependentes de política**" altera `SecurityStamp`.
-  **Intervalo de revalidação** em **emissão de token**.
- `SecurityStamp` único.

#### Q6 - Conclusão

A resposta **atende** o que foi perguntado (com uma nuance que se consolida em Q7-2). Decisão:
- `SecurityStamp` modelado como **value object** (encapsula o valor), persistido como **coluna no `UserAccount`** (sem `UserSecurityState` dedicado por ora).
- **Stamp único** (serve autenticação **e** autorização).
- **Comparação na emissão de token**.
- Os gatilhos dependentes de política (email, telefone, admin-`MustChangePassword`, roles/permissões) **incrementam** o stamp.

⚠ **Nuance/consequência (consolidar em Q7-2):**
- Comparar **só na emissão de token** ⇒ a **validação de cookie** (UI interativa, `OnValidatePrincipal`) **não** checa o stamp; uma sessão de cookie segue navegável até tocar o token endpoint. Avaliar em Q14/Q7 se a comparação também ocorre na validação de cookie.
- "Todos os gatilhos incrementam" + "comparar sempre na emissão" **invalida** a emissão para sessões antigas — o que **conflita** com "admin-`MustChangePassword` por default não invalida" (Q4) e com "sessões ativas seguem válidas salvo política". Com **um único** stamp não dá para "enforçar uns gatilhos e outros não"; a reconciliação (quais gatilhos enforçam por default e como o enforcement é gated por realm) está em **Q7-2**.

Aplicar ao plano: modelo §`SecurityStamp` → "VO, coluna em `UserAccount`"; Fase 2 cria o VO; contratos mantêm Q6/Q15.

### Q7 — Política de invalidação: formato e defaults (pré-plano §8)
**Bloqueia:** Fases 1 e 8.
- **Flags** (`None`/`CurrentSession`/`OtherInteractiveSessions`/`AllInteractiveSessions`/`RefreshTokens`/
  `AllSessionsAndRefreshTokens`) ou **enum de presets** (`KeepCurrentSessionOnly`/`RevokeOtherSessions`/…)?
- Defaults por gatilho (proposta original; decisão final na conclusão da Q7): troca voluntária → manter atual + revogar outras só se a policy pedir
  se a policy pedir; **reset por recuperação** → revogar sessões + refresh por default; **admin reset** → revogar
  sessões + refresh por default; **import/migração** → não invalidar.
- Troca forçada por expiração deve **sempre** invalidar sessões antigas? O usuário pode escolher "sair de outros
  dispositivos" ao trocar a senha?

#### Q7 - Resposta 1

- São muitas opções vagas, me traga opções, recomendação, consequencias e implicações. Justifique o recomendado.
- Ok.
- Senha expirada não força fim da seção. Apenas quando trocar a senha, conforme políticas já definidas.

#### Q7 - Comentários e Questões 2

A resposta **pede** opções, recomendação, consequências e implicações justificadas. Esta é a questão central — consolido aqui também a reconciliação stamp×invalidação (Q4/Q6).

**1. Formato da configuração.**
- **(A) `[Flags]` componível** — ex.: `SessionInvalidation { None, CurrentSession, OtherSessions, AllSessions, RefreshTokens }`. Expressa a matriz "sessões × refresh × atual/outras" sem explosão.
- **(B) Enum de presets** — ex.: `KeepCurrentSessionOnly`, `RevokeOtherSessions`, `RevokeAllSessions`, `RevokeAllSessionsAndRefreshTokens`. Simples para admin, mas não compõe (cada combinação nova vira um preset).

**Recomendação: (A) flags internamente + um pequeno conjunto de presets nomeados expostos na configuração/UI.** Justificativa: os efeitos **combinam** (encerrar outras sessões mantendo a atual; revogar refresh sem derrubar cookie etc.); flags modelam isso de forma fechada e testável, e os presets dão ao admin escolhas simples mapeadas para flags. Consequências: revogar refresh força re-auth no próximo refresh; revogar "outras sessões" desloga em outros dispositivos; "sessão atual" precisa do `sid` corrente (disponível no principal). Execução **idempotente** e **pós-commit** (Decisão fechada §Concorrência); como os stores operacionais ainda são in-memory, a execução distribuída real é honrada pelo `plan-data-persistence`.

**2. Reconciliação stamp×invalidação (resolve a tensão Q4/Q6).** Com **um único** `SecurityStamp` comparado **sempre** na emissão de token, qualquer incremento invalida a emissão para sessões antigas — o que briga com "sessões ativas seguem válidas salvo política" e com "admin-`MustChangePassword` por default não invalida". Proponho **separar dois mecanismos**:
  - **(i) Incremento do stamp** (bookkeeping barato): ocorre **sempre** nos gatilhos de **comprometimento de credencial** — senha trocada/resetada, credencial removida/desabilitada, `SecurityStamp` regenerado por admin. Esses **sempre** enforçam (sessão antiga não emite token): é a garantia de segurança "credencial comprometida não mina tokens".
  - **(ii) Invalidação ativa por política** (Q13): para gatilhos **não-comprometimento** — admin-`MustChangePassword`, email/telefone alterados, roles/permissões — a derrubada de sessões/refresh é **gated por opção de realm** (default **off**, honrando "sessões seguem válidas"). Quando ligada, dispara revogação ativa (Q13) e/ou passa a enforçar o stamp para aquele realm.
  - Com isso: `MustChangePassword` é enforçado **na autenticação** (via `RequiredAction`, Q4) — não precisa derrubar a sessão ativa; e roles→revogação imediata de autorização vira **opt-in por realm** (quando ligado, o incremento do stamp passa a ser enforçado e a próxima emissão de token força re-auth). Isso **revisa** parcialmente Q4/Q6 ("admin-`MustChangePassword` sempre incrementa" e "todos os gatilhos incrementam + sempre compara") para um modelo coerente: incrementar pode ser sempre, **enforçar é que é gated**.

**3. Defaults por gatilho** (autor respondeu "Ok" à proposta; e "senha expirada não derruba sessão"):

| Gatilho | Stamp incrementa? | Invalidação default |
|---|---|---|
| Troca **voluntária** de senha | sim (enforça) | manter sessão atual; revogar outras **se a policy pedir** |
| **Reset** por recuperação | sim (enforça) | revogar sessões + refresh |
| **Admin** set/reset de senha | sim (enforça) | revogar sessões + refresh |
| Admin `MustChangePassword` | sim (bookkeeping) | **não** invalida (opt-in por realm) |
| **Expiração** de senha | não | **não** derruba sessão; troca aplica a política da troca |
| Import/migração | não | não invalida |
| Email/telefone/roles alterados | sim (bookkeeping) | **não** invalida (opt-in por realm) |

**Questão 2 (Q7.2):** Confirmar: (a) flags + presets; (b) o modelo **incrementar-sempre / enforçar-gated** com a divisão "comprometimento (sempre enforça)" vs "não-comprometimento (opt-in por realm)" — isto **ajusta** as respostas de Q4/Q6, ok?; (c) a tabela de defaults acima.

#### Q7 - Resposta 2

1. opção `a`.
2. Gostei mais da opção enforçar-gated.
3. Parace bom.

Olhar com carinho a reconciliação SecurityStamp × “enforce gated”.
Tem uma sutileza importante: se um único SecurityStamp sempre muda, uma sessão antiga só sabe “meu stamp é diferente do atual”; ela não sabe por que mudou.
Então, se MustChangePassword incrementa o stamp mas não deve invalidar sessões por default, uma comparação simples sessionStamp != currentStamp quebraria a decisão.

Talvez isso pode ser resolvido com um marcador separado de invalidação, por exemplo:
- SecurityStamp: versão geral do estado sensível, pode mudar em todos os gatilhos.
- SessionsValidAfter ou SecurityEpoch: muda só quando aquele gatilho deve invalidar sessões/tokens.

Assim, admin MustChangePassword pode incrementar SecurityStamp sem derrubar sessão; reset de senha incrementa SecurityStamp e também move SessionsValidAfter. Essa pequena peça evita uma gambiarra futura.

#### Q7 - Conclusão

A resposta **atende** e **refina** com uma peça-chave (que adoto). Decisões:

1. **Formato: flags + presets** (opção `a`).
2. **Modelo incrementar-sempre / enforçar-gated**, operacionalizado pelo refinamento do autor com **dois campos** (resolve a tensão Q4/Q6 sem "o stamp não sabe por quê mudou"):
   - **`SecurityStamp`** (VO): versão geral do estado sensível; muda em **todos** os gatilhos (Q6). Usado para revalidação de cookie/claims e bookkeeping.
   - **`SessionsValidAfter`** (timestamp) **ou** `SecurityEpoch` (contador): **marcador de invalidação**; move **só** nos gatilhos que devem derrubar sessões/tokens. **É este** que a emissão de token/`IsSessionValidAsync` comparam (sessão válida sse `session.StartedAt >= conta.SessionsValidAfter`, ou `session.Epoch == conta.Epoch`).
   - Efeito: admin-`MustChangePassword` incrementa `SecurityStamp` **sem** derrubar sessão (não move `SessionsValidAfter`); troca/reset/admin-set-senha movem **ambos**; email/telefone/roles movem `SecurityStamp` e movem `SessionsValidAfter` **só se a policy do realm exigir** (opt-in). Isto **substitui** o "enforçar o próprio stamp" do Q7-2 — o enforcement passa a ser via `SessionsValidAfter`.
3. **Tabela de defaults por gatilho: aprovada.**

Recomendação de representação: **`SessionsValidAfter` (timestamp)** (simples; alinha com o "notBefore"/revocation-por-usuário do Keycloak); `SecurityEpoch` é equivalente — confirmável na implementação.

Aplicar ao plano: §`SecurityStamp` do modelo (+ `SessionsValidAfter`); Fase 2 cria o VO **e** `SessionsValidAfter`; Fase 8 compara `SessionsValidAfter`; bloco de options de invalidação (flags+presets + classificação por gatilho).

### Q8 — Eventos/auditoria/outbox: escopo neste plano (pré-plano §9)
**Bloqueia:** Fase 9.
- Confirmar **Opção B** (auditoria durável + outbox seletivo). Mas: **a auditoria durável e o outbox são construídos
  neste plano** ou apenas a **emissão/classificação** dos eventos (despacho via `IEventDispatcher` existente),
  **diferindo** a persistência durável/outbox para `plan-data-persistence` (§2)? O v2 deixou eventos de domínio
  **não despachados**; o core já tem `IEventDispatcher`/`IEventObserver`.
- Lista de eventos candidatos e classificação (auditoria vs outbox) no pré-plano §9 — ratificar.

#### Q8 - Resposta 1

- No outbox serão armazeados eventos de domínio que outros sistemas precisam consumir, geralmente para replicação de dados ou por alguma necessidade de interação. Os eventos deve ser despachados de qualquer forma. Não será armazenado nada em outbox ainda, pois não há design, requisitos, planos de como funcionará o outbox. Também não há nada planejado de auditoria, como opções do que auditar por Realm. Mas isso pode ser feito um design agora, pois é mais simples, e na teoria todos eventos de segurança podem ser auditados. O ponto é como auditar isso.
- Tudo deve ser eventos de domínio, podem deixar estes como possíbilidade, e conforme existirem operações no domínio, podem ser criados eventos. One Big Shot na frente é ruim como decisão, pode falsificar as reais necessidades, é melhor a regra de intenção de negócio gera evento de domínio.

#### Q8 - Comentários e Questões 2

A resposta **decide** o outbox/eventos mas **pede** um design de auditoria.

Decidido:
- **Sem outbox neste plano** (não há design/requisitos/plano de outbox) — diferido inteiramente.
- Eventos são **eventos de domínio**, **despachados** (via `IEventDispatcher`/`IEventObserver` do core, já existentes).
- **Sem catálogo grande antecipado** ("One Big Shot é ruim"): cada evento nasce de uma **operação de intenção de negócio**, à medida que os casos de uso surgem.

Pedido extra — **design de auditoria** ("o ponto é como auditar"). Proposta:
- Auditoria é **consumidora** de eventos (não um novo tipo de evento). Um `ISecurityAuditSink` (ou um `IEventObserver` dedicado) observa os eventos de segurança e registra entradas auditáveis.
- **Por realm**: `SecurityAuditOptions` define **o que** auditar (ex.: falhas de login, trocas/resets de senha, ações administrativas, verificações), com defaults sensatos. Em tese todo evento de segurança é auditável; a opção controla volume/retenção.
- **Forma da entrada**: `RealmId`, `SubjectId`, `EventType`, `OccurredAt`, `Outcome`, `ActorSubjectId` (admin, quando aplicável), `IpHash`/`UserAgentHash`, metadados mínimos — **nunca** senhas/hashes/tokens.
- **Onde**: a **abstração** (`ISecurityAuditSink` + `SecurityAuditOptions`) é definida agora; o **store durável** fica para o `plan-data-persistence` (§2). Por ora, um sink de log/in-memory satisfaz e mantém testável.

Aplicar ao plano: revisar Fase 9 → "sem catálogo; cada caso de uso emite seu evento; o sink de auditoria observa o subconjunto configurado; outbox totalmente diferido".

**Questão 2 (Q8.2):** Confirmar o design de auditoria (sink + `SecurityAuditOptions` por realm; store durável diferido ao §2; sink de log por ora). Auditar em tese **todos** os eventos de segurança com **opt-out** por categoria, ou **opt-in** explícito por categoria?

#### Q8 - Resposta 2

O Design é bom, podemos prossegir.
Quanto aos eventos a auditar, por padrão nas opções devem vir marcados os eventos de segurança, outros não são padrão.

Poderia ser por categorias de auditoria, não lista fixa de eventos: Credential, Recovery, Verification, Lockout, AdminSecurity, SessionRevocation, AuthenticationFailure. Defaults on para segurança, off para o resto.

Para histórico por idade + quantidade, eu colocaria um limite máximo de comparações/retenção para evitar custo exagerado em contas que trocam senha muitas vezes.

Para recuperação/verificação, vale explicitar que o token bruto só sai como “delivery payload” uma vez e nunca entra em evento/auditoria/log.

#### Q8 - Conclusão

A resposta **atende**. Decisões:
- **Design de auditoria aprovado**: `ISecurityAuditSink` + `SecurityAuditOptions` por realm; **store durável diferido** ao `plan-data-persistence` (§2); sink de log por ora.
- Auditoria **por categorias** (não lista fixa de eventos): `Credential`, `Recovery`, `Verification`, `Lockout`, `AdminSecurity`, `SessionRevocation`, `AuthenticationFailure`. **Defaults: on** para as categorias de segurança; **off** para o resto.
- **Sem outbox** (diferido inteiramente); eventos por **caso de uso** (sem catálogo grande antecipado).
- Refinamentos absorvidos: (i) **limite máximo de comparações/retenção** no histórico de senha (Q2/Fase 3) — evita custo em contas que trocam senha muitas vezes; (ii) **token bruto** só sai como *delivery payload* **uma vez** e **nunca** entra em evento/auditoria/log (vira invariante).

Aplicar ao plano: Fase 9 → categorias + defaults + sem catálogo; Fase 3/`PasswordOptions` → cap de comparações/retenção; **Invariantes** → token bruto nunca em evento/auditoria/log.

### Q9 — Telefone: entra no agregado neste plano? (pré-plano "Email/telefone" + §6 das emendas)
**Bloqueia:** Fase 6.
Hoje **não há modelo de telefone** (só a opção `AllowChangePhoneNumber`). O roadmap §3 inclui "verificação de
email/**phone**". Decidir:
- (a) modelar **telefone** no agregado neste plano (entidade/coleção `UserAccountPhone` com
  `Number`/`IsPrimary`/`IsVerified`/`VerifiedAt`) + projeção `phone_number`/`phone_number_verified`; ou
- (b) **adiar telefone** inteiramente (somente email neste plano), removendo `phone` do escopo até um plano futuro.
> Se (a), há **emenda à ADR-015** (campos fixos/projeção) e novas `FixedFieldClaimProjection` para phone.

#### Q9 - Resposta 1

- Pode ser modelado o telefone da mesma forma que o email. Mas acho que é algo muito mais opcional que email.

#### Q9 - Conclusão

A resposta **atende**. Decisão: telefone **entra** neste plano, **modelado como o email** (`UserAccountPhone`: `Number`/`NormalizedNumber`/`IsPrimary`/`IsVerified`), com verificação por token de ação e projeção `phone_number`/`phone_number_verified` (emenda à ADR-015 §2.6). Porém é **mais opcional** que email: gated por realm (`AllowChangePhoneNumber` já existe + uma flag de habilitação) e, por ser secundário, pode ser a **última entrega** da Fase 6. Coerente com Q10 (sem `VerifiedAt`; trocar gera novo objeto).

Aplicar ao plano: `UserAccountPhone` deixa de ser condicional ("apenas se telefone entrar"); remover `VerifiedAt`; Fase 6 trata phone como entrega opcional/secundária.

### Q10 — Email: `VerifiedAt` e semântica de `email_verified` (pré-plano "Emendas sugeridas" #3/#4)
**Bloqueia:** Fase 6.
- Adicionar **`VerifiedAt`** em `UserAccountEmail` (hoje só há `IsVerified`)?
- Confirmar/registrar que **`email_verified` deriva do email primário verificado** (a projeção fixa
  `EmailVerified → email_verified` já existe; falta amarrar à verificação real e garantir que **troca de email
  reseta `IsVerified`**).

#### Q10 - Resposta 1

- Não precisa de `VerifiedAt`.
- troca de email gera novo objeto, não é um reset do campo.

#### Q10 - Conclusão

A resposta **atende**. Decisão: **sem `VerifiedAt`** (email e phone). Trocar email/telefone **gera um novo objeto** (`UserAccountEmail`/`UserAccountPhone` não-verificado) — **não** se reseta o `IsVerified` do existente. `email_verified` continua derivando do **email primário verificado**.

Aplicar ao plano: remover `VerifiedAt` do modelo (emenda `UserAccountEmail` e `UserAccountPhone`); Fase 6 → "ao consumir o token, marca `IsVerified` do email/phone **alvo**; trocar gera novo objeto não-verificado (não reset)".

### Q11 — Concorrência: estratégia concreta por provider (pré-plano §10)
**Bloqueia:** Fases 2, 7 e 10.
- Contador de falhas: **update atômico** (`... set failed = failed + 1 ... returning ...` no PostgreSql) vs
  **optimistic concurrency + retry** (usando `UserAccount.Version`) — e a alternativa transacional equivalente no
  Sqlite. Cobrir os 7 cenários do pré-plano §10 (falhas simultâneas; sucesso×falha; consumo duplo de token; nova
  emissão×consumo; troca de senha×emissão de token; admin unblock×falha; verificação de email×troca de email).

#### Q11 - Resposta 1

- Usar `optimistic concurrency + retry` parece uma alternativa melhor, porém exige um design bem elaborado do lado da aplicação para que isso seja garantido que funcione.

#### Q11 - Conclusão

A resposta **atende**. Decisão: **optimistic concurrency + retry** (via `UserAccount.Version`) para mutações de credencial/contadores/stamp, com um design de aplicação cuidadoso (política/limite de retry, escopo de transação, tratamento de `DbUpdateConcurrencyException`).

Esclarecimento: **consumo de token** **não** usa optimistic-retry — usa **UPDATE condicional idempotente** (`ConsumedAt is null AND …`), já na Decisão fechada §Tokens de ação. Optimistic+retry vale para o agregado/credencial; o detalhamento (cenários) fecha nas Fases 2/10.

Aplicar ao plano: Decisões fechadas §Concorrência → fixar optimistic+retry; Fase 2 estabelece a base, Fase 10 prova os 7 cenários.

### Q12 — Endpoints/telas user-facing: neste plano ou no plano de UI? (roadmap §3 vs §5)
**Bloqueia:** Fases 4, 5 e 6 (parte HTTP/UI).
O roadmap §3 lista os **fluxos** (troca, recuperação, verificação) e a ADR-013 separa API/UI dos módulos. Decidir o
recorte:
- (a) este plano entrega **domínio + casos de uso + costura** e **endpoints/telas SSR mínimos** (`I*PageService` +
  Razor, padrão de [structure.md](../foundation/structure.md) §RoyalIdentity.Razor) para troca/recuperação/verificação;
  ou (b) este plano para em **domínio + casos de uso + costura**, e **todas as telas/endpoints** ficam para
  `plan-admin-api-ui`/um plano de UI de conta.
> Hoje há `Manage/ProfilePage.razor`, login/consent/logout; **não há** telas de troca/recuperação/verificação de senha.

#### Q12 - Resposta 1

- Este plano entrega apenas **domínio + casos de uso + costura**. As telas e endpoints ficam para parte de admin.

#### Q12 - Conclusão

A resposta **atende**. Decisão: este plano entrega **apenas domínio + casos de uso + costura**. Telas/endpoints user-facing (troca/recuperação/verificação) ficam para a parte administrativa/UI (`plan-admin-api-ui` §5 e/ou um plano de UI de conta).

Aplicar ao plano: remover das Fases 4/5/6 as tarefas "Endpoints/telas SSR … se Q12=(a)"; remover a linha `RoyalIdentity.Razor` da Arquitetura alvo; em **Fora de escopo**, marcar UI **sempre** fora. Atenção: a **costura** no `LoginFlowService`/`.Integration` (roteamento do required action; emissão/consumo de tokens de recuperação/verificação) e os **casos de uso** permanecem neste plano — só não há render/endpoint; a UI os consumirá depois.

### Q13 — Costura de execução da invalidação (módulo → IdP) (pré-plano §8)
**Bloqueia:** Fases 1, 5 e 8.
O módulo decide a política e **publica um comando/evento de invalidação**; o IdP executa sobre sessão/cookie/refresh.
Decidir a forma da costura no core (emenda à ADR-014): novos métodos no store de sessão (`EndAllForSubjectAsync`,
`EndOthersForSubjectAsync(exceptSid)`) + revogação de refresh tokens por subject/sessão (`IRefreshTokenStore`), e o
**porto** que a `.Integration` chama para disparar a execução. Confirmar se `IUserSessionStore`/`IUserSessionService`
ganham as APIs de revogação por subject (hoje só por `sid`).

#### Q13 - Resposta 1

- Pode ser criada novas API/métodos nos serviços atuais do IdP, conforme necessidade. O importante é garantir que estamos fazendo o melhor design, não simplesmente enfiando mais métodos de forma procedural. Cabe uma avaliação de opções, mas é possível criar novos métodos.

#### Q13 - Comentários e Questões 2

A resposta **aprova** criar novos métodos/APIs, mas **pede** uma avaliação de opções para o melhor design (não "enfiar métodos de forma procedural"). Opções para a costura de revogação módulo → IdP:

- **(1) Estender os contratos atuais** — `IUserSessionStore`/`IUserSessionService` ganham `EndAllForSubject`/`EndOthersForSubject(exceptSid)` e `IRefreshTokenStore` ganha revogação por subject/sid. *Prós:* mínimo de tipos novos. *Contras:* incha interfaces e mistura "sessão corrente" com revogação em massa.
- **(2) Porta core-owned dedicada** — `ISessionRevocationService` (ou `IUserSecurityInvalidator`) que encapsula "revogar sessões/refresh do subject conforme política", implementada sobre os stores. *Prós:* responsabilidade única, seam limpo que a `.Integration` chama, testável. *Contras:* uma abstração a mais.
- **(3) Orientada a evento** — o módulo publica `SecurityInvalidationRequested`; um handler do IdP executa. *Prós:* desacoplado, casa com outbox futuro. *Contras:* exige infra de evento/handler (e Q8 difere o outbox; o despacho in-process via `IEventDispatcher` já existe).

**Recomendação: (2) + (3) híbrido** — uma porta core-owned `ISessionRevocationService` (seam limpo, usado **síncrono** agora pela `.Integration`), deixando aberto **disparar a partir de um evento** `SecurityInvalidationRequested` quando o outbox/assíncrono chegar. Justificativa: é o "melhor design" pedido (SRP, sem method-stuffing), honra a Q8 (sem outbox agora) e a Q15 (pluggabilidade: o IdP **possui** a execução; o módulo só **sinaliza** a política).

**Questão 2 (Q13.2):** Confirmar a porta `ISessionRevocationService` core-owned (síncrona agora, evento depois), em vez de adicionar métodos soltos aos contratos existentes?

#### Q13 - Resposta 2

Confirmado.

#### Q13 - Conclusão

A resposta **atende**. Decisão: porta core-owned **`ISessionRevocationService`** (síncrona agora pela `.Integration`; aberta a disparo por evento `SecurityInvalidationRequested` no futuro), em vez de espalhar métodos pelos contratos existentes.

Aplicar ao plano: contratos de borda / Fase 8 / Arquitetura referenciam `ISessionRevocationService`.

### Q14 — `UserSession.ExpiresAt` × cookie lifetime × `UserSsoLifetime` × expiração de senha/lockout (roadmap §3; `UserSession.cs:38`)
**Bloqueia:** Fase 8.
Hoje `UserSession.ExpiresAt` é **reservado, sem comportamento** (o próprio XML doc diz "a future phase may define its
interaction with cookie lifetime / `UserSsoLifetime` / session expiry. Do not branch on it yet"). `UserSsoLifetime` é
**por client** e já avaliado no `PromptLoginDecorator` (força nova interação quando a duração da sessão excede). O
roadmap §3 pede explicitamente a "relação com `UserSsoLifetime`, cookie lifetime e sessões ativas". Decidir:
- `ExpiresAt` ganha comportamento (expira a sessão de verdade) ou continua reservado? Se ganha, qual fonte define o
  prazo (cookie lifetime do realm? um SSO lifetime de sessão por realm, distinto do `UserSsoLifetime` por client)?
- Como `IsSessionValidAsync` combina: sessão ativa **e** não expirada (`ExpiresAt`) **e** `SessionsValidAfter` válido — sem
  conflitar com a avaliação per-client de `UserSsoLifetime` no `PromptLoginDecorator` (que continua forçando interação,
  não invalidando a sessão).
- Expiração de senha/lockout **derrubam a sessão** ou apenas barram **nova** autenticação? (Decisão fechada §Senha diz
  que sessões ativas seguem válidas salvo política — confirmar que expiração de senha não é exceção implícita.)

#### Q14 - Resposta 1

- Temos duas opções quanto ao `ExpiresAt. Ter por Realm, ou ter ambas, por Realm e por Client como opcional.
  Se apenas por Realm, é mais simples controlar. Se por Client também, será necessário um tratamento especial no cookie.
  A opção de ter também por client precisa de um design mais avançado.
  Avalia as duas opções, qual implicação vs benefícios, como é em outros IdP, como implementar, qual impacto no que tem hoje.
- O `IsSessionValidAsync` entra no problema de design acima.
- Expiração de senha não derruba seção. Bloqueio me parece opcional/configurável.

#### Q14 - Comentários e Questões 2

A resposta **pede** a avaliação das duas opções (Realm-only vs Realm+Client), com implicações/benefícios, comparação com outros IdP, implementação e impacto.

**Hoje:** `UserSsoLifetime` é **por client** e avaliado no `PromptLoginDecorator` — **força nova interação** (re-login) quando a idade da sessão excede o limite do client; **não** encerra/invalida a sessão. A sessão em si não tem prazo real (`ExpiresAt` reservado); o cookie tem sua própria expiração.

**Outros IdP:**
- **Keycloak:** lifetimes de sessão **por realm** (*SSO Session Idle* + *SSO Session Max*), com **override opcional por client** (*Client Session Idle/Max*). O cookie de identidade vive atado ao *SSO Session Max*.
- **IdentityServer:** o cookie (auth ticket) **é** a sessão; `UserSsoLifetime` por client limita a idade aceitável da sessão para aquele client (≈ o comportamento atual do RoyalIdentity: força re-login, não encerra a sessão compartilhada).
- **Entra ID:** *sign-in frequency*/*session lifetime* por tenant/conditional-access.

**Opção Realm-only** (um *SSO session max*/*idle* por realm → dá comportamento ao `UserSession.ExpiresAt`): mais simples; uma fonte; `IsSessionValidAsync = ativo && não-expirado && (policy ? SessionsValidAfter-ok : true)`; o `UserSsoLifetime` por client **permanece** como o knob de "forçar interação" (ortogonal — força prompt, não expira a sessão). **Baixo impacto** no código atual.

**Opção Realm + Client:** o realm define o baseline; o client pode **encurtar**. Como o **cookie é compartilhado** entre clients do realm, um limite por client **não** pode encolher o cookie — teria de ser enforçado no `authorize`/token por client (semelhante à lógica atual de `UserSsoLifetime`). **Design mais avançado** e risco de confundir dois knobs por client (`UserSsoLifetime` força-prompt × session-max-expira).

**Recomendação: Realm-only para o `ExpiresAt`** neste plano (um *SSO session max* e, opcionalmente, *idle* por realm), **mantendo `UserSsoLifetime` por client como está**. Diferir expiração de sessão por client para um plano futuro (sobrepõe ao `UserSsoLifetime` e adiciona complexidade de cookie para pouco ganho). Confirmado pelo autor: **expiração de senha não derruba a sessão**; bloqueio é opcional/configurável.

**Idle touch recomendado:** usar atualização com **throttle** por janela mínima. A validação pode acontecer em cada request/ponto de protocolo, mas o store só atualiza `LastSeenAt`/`ExpiresAt` se `LastSeenAt <= now - IdleTouchInterval`. Isso evita escrita por request e mantém o idle timeout suficientemente fiel. Quando idle estiver ligado, `IdleTouchInterval` deve ser **maior que zero** e **menor que** `SsoSessionIdle`.

**Questão 2 (Q14.2):** Confirmar **Realm-only** para `ExpiresAt` (session max + idle por realm), `UserSsoLifetime` por client inalterado, e a combinação de `IsSessionValidAsync` acima? Diferir a opção por client?

#### Q14 - Resposta 2

Confirma.

#### Q14 - Conclusão

A resposta **atende**. Decisão: **Realm-only** para `UserSession.ExpiresAt` (SSO session max + idle por realm — `ExpiresAt` ganha comportamento); **`UserSsoLifetime` por client inalterado** (segue forçando interação no `PromptLoginDecorator`, ortogonal); expiração por client **diferida**. `IsSessionValidAsync = ativo && não-expirado(ExpiresAt) && (policy ? SessionsValidAfter-ok : true)`. **Expiração de senha não derruba sessão**; bloqueio é opcional/configurável.

Idle timeout usa **opção 3**: `LastSeenAt` + `IdleTouchInterval` por realm, com update condicional. Touch aceito atualiza `LastSeenAt = now` e `ExpiresAt = min(StartedAt + SsoSessionMax, now + SsoSessionIdle)`. Sem escrita em toda validação; o store só grava quando a janela mínima passou. Guardas de options impedem `IdleTouchInterval <= 0` ou `IdleTouchInterval >= SsoSessionIdle` quando o idle timeout estiver ativo.

Aplicar ao plano: bloco de options por realm (SSO session max/idle + `IdleTouchInterval`); Fase 8; contratos (`UserSession.ExpiresAt` ganha comportamento, Realm-only; `LastSeenAt` alimenta idle).

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

#### Q15 - Resposta 1

Um dos motivos de separar a conta de usuários do IdP é para poder usar múltiplos módulos como fornecedores de usuários.
Não podemos restringir demais as funcionalidades e o funcionamento do módulo de usuários pelo IdP.
Pode ser que em uma compania, já exista um sistema que controle usuários e tenha funcionalidades diferentes, requisitos diferentes.
Então devemos pensar em montar essa integração como peças que se encaixam e podem ser trocadas por requisitos e limitações.
A partir disso, e do que já foi respondido também na Q14, podes avaliar uma solução para esta questão.

#### Q15 - Comentários e Questões 2

A resposta dá o **princípio** (provedores de usuário plugáveis e intercambiáveis; o IdP não deve restringir o módulo) e **pede** que eu avalie uma solução. Proposta alinhada a esse princípio (e à Q13/Q14):

- **Porta de capacidade opcional, core-owned:** `IUserSecurityStateProvider`, realm-bound via `IUserDirectory.GetSecurityStateProvider(realm)`, com `GetSecurityStateAsync(subjectId)` retornando `SecurityStamp` e `SessionsValidAfter` — **primitivos/BCL apenas**. É **opcional**: um provedor que não tenha esse estado simplesmente **não** a implementa, e o IdP **degrada graciosamente** quando a policy não exige invalidação por estado de segurança.
- **Captura no sign-in:** o `LoginFlowService`/`.Integration` lê o `SecurityStamp` por essa porta e o grava em `UserSession.SecurityStamp`. `IsSessionValidAsync`/emissão de token leem `SessionsValidAfter` pela mesma porta, **quando a política exigir** (Q7).
- **Camadas que não dependem do módulo:** o **tempo de vida/expiração da sessão** (Q14, por realm) e o **"ativo"** são dados puros do IdP — funcionam **sem** qualquer capacidade do módulo. `SecurityStamp` é aditivo para cookie/claims; `SessionsValidAfter` é aditivo para invalidação.
- **Relação com Q13:** Q15 **lê/valida** o estado atual de segurança (capacidade do provedor); Q13 **executa** a revogação ativa (responsabilidade do IdP). São portas distintas e complementares — "peças que se encaixam".

**Recomendação:** porta de capacidade `IUserSecurityStateProvider` **opcional**, realm-bound, primitivos-only, com **degradação graciosa**. O IdP sempre valida ativo/expiração; provedores com estado de segurança também suportam captura do stamp e invalidação por `SessionsValidAfter`.

**Questão 2 (Q15.2):** Confirmar a porta de capacidade **opcional** `IUserSecurityStateProvider` (com degradação graciosa quando ausente) em vez de um contrato obrigatório? E o ponto de captura (sign-in via `LoginFlowService`) está ok?

**Questão 3 (Q15.3):** Quando a política do realm exigir enforcement por `SessionsValidAfter`, mas o provider de usuários
não expuser `IUserSecurityStateProvider`, o comportamento deve ser: (a) **erro de configuração** (recomendado — evita
uma policy de segurança silenciosamente inoperante), ou (b) degradação com warning e enforcement ignorado? Proposta:
degradação graciosa só quando a policy de invalidação por estado estiver desligada; se estiver ligada e a capacidade faltar, o realm é
inválido na validação de options/composição.

#### Q15 - Resposta 2

Confirmo `Q15.2`.

Para o Q15.3, se o Realm exige a política, deve existir `IUserSecurityStateProvider`, então o melhor é erro de configuração.

#### Q15 - Conclusão

A resposta **atende**. Decisões:
- Porta de capacidade **opcional** `IUserSecurityStateProvider` (core-owned, realm-bound via `IUserDirectory`, primitivos/BCL apenas); **captura do stamp no sign-in** via `LoginFlowService`.
- `UserSession.SecurityStamp` serve para revalidação de cookie/claims e rastreabilidade; invalidação forte usa `SessionsValidAfter`.
- **Degradação graciosa** quando a capacidade está ausente — **exceto** que, se a policy do realm **exigir** enforcement por `SessionsValidAfter` e o provider **não** expuser a porta, é **erro de configuração** (Q15.3 = opção a; evita policy de segurança silenciosamente inoperante).
- Múltiplos provedores de usuário podem coexistir, cada um expondo só as capacidades que tem (pluggabilidade).

Aplicar ao plano: contratos de borda (porta opcional `IUserSecurityStateProvider`); Fase 1 → **validação de options/composição**: realm com policy de invalidação por estado **exige** a capacidade (senão, erro de configuração); Fases 2/8 e Arquitetura.

---

## Modelo relacional proposto

> Q1–Q15 estão **decididas** (ver `#### QN - Conclusão`); os blocos abaixo refletem as decisões. Tabelas realm-scoped
> (`RealmId` estrutural — ADR-015 §2.3); FKs por `UserAccount.Id` físico;
> eventos chaveiam por `(RealmId, SubjectId)`.

### `SecurityStamp` + `SessionsValidAfter` (Q6/Q7 — decidido) — VO + marcador de invalidação em `UserAccount`

```text
UserAccount.SecurityStamp       string not null    -- VO; versão geral do estado sensível; muda em TODOS os gatilhos (Q6)
UserAccount.SessionsValidAfter  timestamp not null -- marcador de invalidação; move só em gatilhos que devem derrubar sessões/tokens (Q7)
-- UserSession captura o stamp no sign-in via seam Q15 (ver emenda ADR-014):
UserSession.SecurityStamp       string null        -- usado p/ revalidação de cookie/claims
UserSession.LastSeenAt          timestamp not null -- idle touch com throttle (Q14)
-- Enforcement (emissão de token / IsSessionValid): sessão válida sse session.StartedAt >= conta.SessionsValidAfter
--   (via seam Q15, quando a policy do realm exigir). SecurityEpoch (contador) é representação equivalente a SessionsValidAfter.
-- ExpiresAt efetivo: min(StartedAt + SsoSessionMax, LastSeenAt + SsoSessionIdle); touch não passa do max.
```

### `PasswordHistory` (Q2) — tabela própria proposta

```text
PasswordHistory
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null fk -> UserAccount.Id
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

### Bloqueio administrativo pessoal (Q5 — decidido) — `UserAccountBlockState` com janela

```text
UserAccountBlockState (owned em UserAccount; já existe IsBlocked/BlockedReason/BlockedAt)
  + StartsAt, EndsAt    timestamp null   -- bloqueio com prazo (ex.: férias); null = indefinido
```

O bloqueio administrativo pessoal permanece no `UserAccountBlockState`, separado do `PasswordLockout` da credencial, e
**ganha janela temporal** `StartsAt`/`EndsAt`. A `AccountAccessRestriction` per-account foi **aposentada**.
`GeoBlock`/`TimeWindow`/`ClientRestriction` viram políticas futuras por realm/grupo (registradas no backlog), não
restrições por conta neste plano.

### `UserAccountPhone` (Q9 — decidido) — modelado como o email, opcional por realm

```text
UserAccountPhone
  Id                    bigint identity primary key
  RealmId               string not null
  UserAccountId         bigint not null fk -> UserAccount.Id
  Number, NormalizedNumber  string not null
  IsPrimary, IsVerified bool not null

  unique (RealmId, UserAccountId, NormalizedNumber)
```

### `UserAccountEmail` / `UserAccountPhone` (Q10 — decidido) — sem `VerifiedAt`

Sem `VerifiedAt`. Ao consumir o token de verificação, marca-se apenas `IsVerified` do email/telefone **alvo**. **Trocar**
o email/telefone **gera um novo objeto** não-verificado (não há reset do campo). `email_verified` deriva do email
primário verificado.

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

> **Decididos** (Q4/Q6/Q7/Q13/Q14/Q15); forma exata fechada na Fase 1 (ADR-017).

| Porta / tipo | Hoje | Emenda |
|---|---|---|
| `AuthenticationResult` / `AuthenticationFailureReason` (core) | sucesso/falha+motivo | + estrutura `RequiredAction` (Q4) |
| `LocalAuthenticationResult` (módulo) | sucesso/falha+motivo+lockout | espelhar `RequiredAction` (Q4) |
| `UserSession` (core) | `…`,`IsActive`,`ExpiresAt`(reservado) | `ExpiresAt` ganha comportamento (Realm-only — Q14) + `LastSeenAt` para idle touch + `SecurityStamp` capturado no sign-in (Q6/Q15) |
| `IUserSessionService` (core) | `GetCurrent`/`IsSessionValid`/`Start`/`End`/`RecordClient` | `IsSessionValid = ativo && não-expirado && (policy ? StartedAt>=SessionsValidAfter : true)` + idle touch condicional (Q6/Q7/Q14/Q15) |
| `ISessionRevocationService` (core-owned, **novo**) | — | revogação por subject de sessões+refresh; síncrona agora, evento depois (Q13); pode exigir consultas por subject no `IUserSessionStore`/`IRefreshTokenStore` |
| `IUserSecurityStateProvider` (core-owned, **novo, opcional**) | — | lê `SecurityStamp`/`SessionsValidAfter` atuais p/ captura e validação; degradação graciosa; **obrigatória** se a policy de invalidação por estado do realm estiver ligada (Q15) |

`IUserAccountPasswordHasher` (módulo) e `IPasswordProtector` (core) **permanecem** como o seam de hash já estabelecido
no v2 (`PasswordProtectorAccountHasher`); o histórico (Q2) verifica candidatas reusando esse seam.

---

## Arquitetura alvo (Feature-Slice no módulo)

```text
RoyalIdentity.UserAccounts/                  (módulo puro — sem core, sem ASP.NET)
  Features/
    Accounts/
      Domain/
        UserAccount  UserAccountCredential  (existentes; UserAccountCredential singular — Q1; SecurityStamp/expiração)
        PasswordPolicy                       (existente)
        UserAccountPhone                     (Q9 — opcional por realm)
        Events/  (novos eventos de segurança — Q8)
      Security/                              (NOVO slice de ciclo de segurança)
        Domain/
          PasswordHistory                    (Q2 — quantidade + idade, com cap de comparações/retenção)
          UserAccountActionToken             (recuperação/verificação/troca-expirada)
          AdminBlock = UserAccountBlockState + janela StartsAt/EndsAt  (Q5 — decidido)
          SecurityStamp (VO) + SessionsValidAfter em UserAccount  (Q6/Q7 — decidido)
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
  + adaptação do RequiredAction (Q4) p/ AuthenticationResult
  + implementação de IUserSecurityStateProvider (opcional) p/ capturar SecurityStamp e validar SessionsValidAfter (Q15)
  + tradução da política de invalidação -> ISessionRevocationService do core (Q13)
  + IUserAccountActionNotifier? (envio de email/SMS — gateway; ver Q12/Fora de escopo)

RoyalIdentity (core)                         (emendas aditivas atrás da ADR-014)
  AuthenticationResult/UserSession/IUserSessionService + ISessionRevocationService + IUserSecurityStateProvider (opcional)
(telas user-facing de troca/recuperação/verificação ficam para o plano de admin/UI — Q12 — fora deste plano)
```

> **Envio de email/SMS** (entrega do link/código de recuperação/verificação) é um **gateway externo**
> (`Infrastructure/Gateways/`, architecture.md §6). Definir se há um `INotificationGateway`/`IUserAccountActionNotifier`
> abstrato neste plano com implementação no-op/log, deixando o provedor real para o host. (Relaciona com Q12.)

---

## Ordem de execução

> **Protocolo aditivo:** estender o módulo e o core de forma aditiva, mantendo a integração **opt-in** e o **fake
> in-memory** como referência/contrato até a paridade estar provada (ADR-015 §2.11). Nenhuma fase deve quebrar a
> suíte OIDC existente.

1. **Fase 1 fecha a governança:** ADR-017 consolida o Histórico de decisões, emenda ADR-014/015 e cria o bloco de options.
   Nenhuma fase de código começa antes de sua decisão correspondente estar registrada.
2. Fase 2 estabelece o **modelo de credencial + `SecurityStamp` + concorrência** (base de tudo).
3. Fases 3-4 entregam **histórico/expiração** e **troca + required action**.
4. Fases 5-6 entregam **recuperação** e **verificação de email/telefone**.
5. Fase 7 entrega **lockout/bloqueio/restrições**.
6. Fase 8 entrega **invalidação** (consome `SessionsValidAfter` + costura de revogação).
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

**Depende de:** Q1–Q15 **decididas** (rodadas 1–2); a ADR-017 registra as decisões.

**O que/como:** registrar as decisões antes de codar. Criar a **ADR-017** ("Ciclo de segurança da conta") como
documento autoritativo; emendar **ADR-014** (required action no resultado de autenticação; `SecurityStamp` em
`UserSession`; seam core-owned para ler `SecurityStamp`/`SessionsValidAfter`; revogação por subject) e **ADR-015** (evolução de credencial Q1;
telefone Q9; bloco de options); criar o bloco `SecurityLifecycle`/invalidação em `UserAccountsRealmOptions`.

**Tarefas:**

- [x] Criar `adrs/ADR-017.md` decidindo Q1–Q15 (cada questão vira decisão registrada; seguir a regra de ADR do
      CLAUDE.md — decisão, não design).
- [x] Emendar `adrs/ADR-014.md`: required action na borda de autenticação; `SecurityStamp` em `UserSession`;
      seam core-owned para leitura de `SecurityStamp`/`SessionsValidAfter`; revogação por subject no store/serviço de sessão (aditivo).
- [x] Emendar `adrs/ADR-015.md`: decisão de credencial (Q1); telefone no agregado (Q9); bloco de options de ciclo.
- [x] Modelar o bloco de options por realm em `UserAccountsRealmOptions`: invalidação **flags + presets** (Q7), **SSO
      session max/idle + `IdleTouchInterval`** por realm (Q14), **categorias de auditoria** com defaults segurança-on (Q8); expiração/histórico
      (+ cap de comparações/retenção) já em `PasswordOptions`; consolidar sem duplicar dono.
      → `SecurityLifecycleOptions` (novo) + `PasswordOptions.PasswordReuseWindowDays`/`MaxPasswordHistoryComparisons`.
- [x] **Validação de composição (Q15):** realm com policy de invalidação por estado ligada **exige** `IUserSecurityStateProvider`;
      ausência ⇒ **erro de configuração** (não degrada silenciosamente).
      → modelado o gate `SecurityLifecycleOptions.EnableSessionInvalidationByState` ⇒ `RequiresSecurityStateProvider`;
      a checagem da capacidade contra o porto core-owned fica na `.Integration` (Fase 8, onde o porto existe).
      Nota de revisão: esta tarefa está concluída na Fase 1 como **modelagem do gate**; a validação real contra o provider
      está listada explicitamente na Fase 8.
- [x] Atualizar `plans-roadmap-01.md` (§3 aponta para este plano) e `CLAUDE.md` (mover este plano para "ativo").
- [~] Atualizar `.ai/foundation/*` se a emenda mudar contratos de borda documentados.
      → **diferido**: os novos portos (`RequiredAction`, `IUserSecurityStateProvider`, `ISessionRevocationService`)
      ainda não existem no código; os foundation docs descrevem o estado implementado. Atualizar nas Fases 2/8
      (mesmo precedente da nota `IUserClaimsProvider` "until Fase 2" em `product.md`).

**Critérios de aceite:** ADR-017 existe e decide todas as questões; ADR-014/015 registram as emendas; documentação não
contradiz o plano; options têm dono único. ✔

**Testes:** n/a (documentação) + testes de options (copy-on-create/validação) — **incluídos** em
`Tests.UserAccounts/UserAccountsRealmOptionsTests.cs` (defaults decididos, copy-on-create independente, validação de
SSO idle×max/touch interval e do cap de histórico). 101/101 verdes.

### Resultado da Fase 1

**Concluída (2026-06-23).** Governança registrada antes de codar: ADR-017 criada, ADR-014/015 emendadas, bloco de
options de ciclo de segurança modelado em `UserAccountsRealmOptions` (dono único), roadmap/CLAUDE.md atualizados.

**Arquivos novos:**
- `adrs/ADR-017.md` — decide Q1–Q15 (estilo decisão, não design; Contexto/Decisão/Consequências): credencial singular
  não-genérica, histórico+expiração com enforcement, `RequiredAction` + troca forçada por token de ação, tokens de
  ação, lockout×bloqueio com janela, `SecurityStamp` + `SessionsValidAfter` (dois campos), invalidação flags+presets,
  verificação email/telefone, concorrência optimistic+retry, seams de borda, auditoria por categorias, expiração de
  sessão Realm-only, fronteira de escopo/options.
- `RoyalIdentity.UserAccounts/Options/SecurityLifecycleOptions.cs` — `[Flags] SessionInvalidation` +
  `SessionInvalidationPresets`, `[Flags] SecurityAuditCategories`, defaults por gatilho (Q7), SSO
  max/idle/`IdleTouchInterval` Realm-only (Q14, off por default), gate
  `EnableSessionInvalidationByState ⇒ RequiresSecurityStateProvider` (Q15) e `Validate()`.

**Arquivos alterados:**
- `adrs/ADR-014.md` — banner + §5 Emenda: `RequiredAction`, `SecurityStamp` em `UserSession` (desfaz a reserva do §3
  via `SessionsValidAfter`), `IUserSecurityStateProvider` (opcional) e `ISessionRevocationService` (revogação por subject).
- `adrs/ADR-015.md` — banner + §4 Emenda: credencial não genérica (evolui §2.8), histórico/expiração, telefone como
  campo fixo (emenda §2.6), bloqueio com janela, bloco de options, eventos despachados (evolui §2.9).
- `RoyalIdentity.UserAccounts/Options/PasswordOptions.cs` — `PasswordReuseWindowDays` (idade, Q2) e
  `MaxPasswordHistoryComparisons` (cap, Q8) + copy-ctor.
- `RoyalIdentity.UserAccounts/Options/UserAccountsRealmOptions.cs` — propriedade `SecurityLifecycle` (dono único),
  copy-on-create e agregação na `Validate()` (idade ≥ 0; cap ≥ count quando histórico ligado; SSO idle×max).
- `Tests.UserAccounts/UserAccountsRealmOptionsTests.cs` — copy-on-create independente, defaults decididos, validação.
- `CLAUDE.md` — plano em "Active plans"; ADRs `..017`. `.ai/plans/plans-roadmap-01.md` — §3 aponta para a ADR-017.

**Verificação:** `dotnet build RoyalIdentity.sln` — sucesso; `Tests.UserAccounts` **101/101** verdes.

**Notas de ordenação:**
- A checagem de composição Q15 (exigir `IUserSecurityStateProvider` quando a policy de invalidação por estado está
  ligada) fica na `.Integration` na **Fase 8** — o porto core-owned ainda não existe; no módulo puro está modelado só
  o gate `RequiresSecurityStateProvider`.
- `.ai/foundation/*` **não** foi alterado: os novos portos (`RequiredAction`, `IUserSecurityStateProvider`,
  `ISessionRevocationService`) ainda não existem no código; a atualização acompanha as Fases 2/8 (mesmo precedente da
  nota `IUserClaimsProvider` "until Fase 2" em `product.md`).

**Critérios de aceite:** atendidos — ADR-017 existe e decide Q1–Q15; ADR-014/015 registram as emendas; documentação
não contradiz o plano; options têm dono único.

---

## Fase 2 - Modelo de credencial, `SecurityStamp` e concorrência

**Depende de:** Q1 (credencial), Q6 (security stamp), Q11 (concorrência), Q15 (seam do stamp).

**O que/como:** materializar a decisão de credencial (manter singular ou evoluir para coleção) e introduzir o
`SecurityStamp` como versionador de estado sensível, com a base de concorrência.

**Tarefas:**

- [x] Preservar `UserAccountCredential` singular (Q1 — decidido: manter), com o seam `IUserAccountPasswordHasher` e o
      autenticador atuais; não genericizar.
- [x] Adicionar `SecurityStamp` (value object) **e** `SessionsValidAfter` em `UserAccount` (Q6/Q7 — decidido);
      `SecurityStamp` gerado por `RoyalIdentity.Security.CryptoRandom`. `SecurityStamp` muda em **todos** os gatilhos;
      `SessionsValidAfter` move **só** nos gatilhos que devem derrubar sessões/tokens (enforcement via `SessionsValidAfter`).
- [x] Garantir update atômico/optimistic dos contadores e do stamp (Q11), reusando `UserAccount.Version`.
- [x] Mapear/migrar o schema (PostgreSql/Sqlite) sem quebrar round-trip do v2.
- [x] Emenda no core: `UserSession` ganha `SecurityStamp` capturado na criação via seam decidido em Q15 (aditivo,
      atrás da ADR-014).

**Critérios de aceite:** stamp persistido e regenerável; `SessionsValidAfter` persistido e atualizado só nos gatilhos de
invalidação; mudanças comuns de perfil não alteram stamp; round-trip EF verde; `UserSession` carrega o stamp; mudança de
credencial, stamp e invalidação ocorrem na mesma transação quando aplicável.

**Testes:** unidade de domínio (gatilhos do stamp); persistência (round-trip/migrations); concorrência (Fase 10 amplia).

### Resultado da Fase 2

Concluída.

- `UserAccountCredential` foi preservado como credencial local singular.
- `UserAccount` ganhou `SecurityStamp` (VO gerado por `CryptoRandom`) e `SessionsValidAfter`.
- Senha e regeneração com invalidação movem stamp + `SessionsValidAfter`; email/primário/roles regeneram só o stamp; perfil/login não mexem no stamp.
- `UserAccount.Version` agora avança nas mutações do aggregate, deixando a concorrência otimista efetiva em SQLite; PostgreSQL segue usando `xmin`.
- EF persiste `SecurityStamp` por conversão string e `SessionsValidAfter`; round-trip v2 permanece verde.
- `UserSession` ganhou `SecurityStamp` nullable; `IUserSessionService` tem overload aditivo para capturar o stamp quando a seam Q15 estiver disponível.
- A leitura real pelo `IUserSecurityStateProvider` e o enforcement por `SessionsValidAfter` continuam na Fase 8, conforme Q15.
- Verificação: `dotnet test Tests.UserAccounts --nologo`; `dotnet test Tests.Integration --nologo --filter "FullyQualifiedName~DefaultUserSessionServiceTests|FullyQualifiedName~SubjectPrincipalFactoryTests"`; `dotnet build RoyalIdentity.sln --nologo`.

---

## Fase 3 - Política de senha: histórico e expiração com enforcement

**Depende de:** Q2 (histórico).

**O que/como:** dar enforcement às políticas que o v2 só persistiu — histórico e expiração — sem mover a fonte de
verdade das options para fora do módulo.

**Tarefas:**

- [x] Implementar `PasswordHistory` (tabela própria — Q2); ao `SetPassword`/`ChangePassword`, gravar o hash anterior,
      reter o conjunto necessário para **quantidade ∪ idade** e podar só o que estiver fora dos dois critérios
      (novo campo em `PasswordOptions`, ex.: `PasswordReuseWindowDays`), com um **limite máximo** de comparações/retenção
      (Q8) para conter custo em contas que trocam senha muitas vezes.
- [x] Consumir options já validadas: `UserAccountsRealmOptions.Validate()` impede idade sem comparação possível
      (`PasswordReuseWindowDays > 0` com cap zero), valores negativos e histórico ligado sem quantidade nem idade.
- [x] Validar senha candidata contra **senha atual + N hashes históricos** (N limitado pelo cap acima) via
      `IUserAccountPasswordHasher.Verify`.
- [x] Implementar enforcement de **expiração** (`EnablePasswordExpiration`/`PasswordExpirationDays` × `PasswordChangedAt`):
      a verificação de expiração alimenta o required action da Fase 4 (não bloqueia silenciosamente).
- [x] Garantir que `EnforcePasswordHistory`/`EnablePasswordExpiration = false` desligam o enforcement.

**Critérios de aceite:** reuso de senha recente é rejeitado quando habilitado; poda respeita a política; senha expirada
é detectada e roteada para troca (Fase 4); flags desligam o comportamento; hashes nunca aparecem em log/export.

**Testes:** unidade (histórico/poda/expiração); persistência de histórico; flags off.

### Resultado da Fase 3

**Concluída (2026-06-24).** Histórico e expiração de senha ganharam enforcement no módulo, sem mover a fonte de
verdade das options para fora dele. Recording+poda ficam no agregado (mutação); a validação de reuso é um serviço de
feature (espelha `PasswordPolicy`); a detecção de expiração é uma query pura na credencial (ao lado de `IsLockedOut`).

**Arquivos novos:**
- `Features/Accounts/Domain/PasswordChangeReason.cs` — enum `Create/Change/Reset/AdminSet/Import` gravado na entrada arquivada.
- `Features/Accounts/Domain/PasswordHistoryEntry.cs` — entidade filha (`Entity<long>`), só hash forte versionado; sem senha plana.
- `Features/Accounts/Domain/PasswordHistoryPolicy.cs` — serviço de feature que rejeita reuso (senha atual + N históricos,
  por **quantidade ∪ idade**, limitado pelo cap), comparando candidata via `IUserAccountPasswordHasher.Verify`; gated por `EnforcePasswordHistory`.
- `Infrastructure/Data/Mappings/PasswordHistoryEntryMap.cs` — tabela `UserAccountPasswordHistory` + índice `(RealmId, UserAccountId, CreatedAt)`.

**Arquivos alterados:**
- `Features/Accounts/Domain/UserAccount.cs` — coleção `PasswordHistory` (+ nav protegida `PasswordHistoryItems`); `SetPassword`
  agora recebe `PasswordOptions`/`PasswordChangeReason`/`changedBySubjectId`, **arquiva o hash anterior** e **poda** (quantidade ∪
  idade, bounded pelo cap; só quando `EnforcePasswordHistory`); `ChangePassword` recebe `options`. `CreatedAt` do histórico = momento do arquivamento.
- `Features/Accounts/Domain/UserAccountCredential.cs` — `IsPasswordExpired(options, now)` (detecção; alimenta o required action da Fase 4).
- `Features/Accounts/Commons/UserAccountReader.cs` — `AccountGraph` inclui `PasswordHistoryItems`.
- `Features/Accounts/Commons/UserAccountsServiceCollectionExtensions.cs` — registra `PasswordHistoryPolicy`.
- `Features/Accounts/UseCases/ChangeUserAccountPassword.cs` — injeta `PasswordHistoryPolicy`, roda o check de reuso e passa `options`.
- `Features/Accounts/UseCases/CreateUserAccount.cs` — `SetPassword(..., Options.PasswordOptions, PasswordChangeReason.Create)`.
- `Infrastructure/Data/Mappings/UserAccountMap.cs` — `HasMany(PasswordHistoryItems)` + `Ignore(PasswordHistory)`.
- `Options/UserAccountsRealmOptions.cs` (Validate) — rejeita negativos, histórico ligado sem quantidade nem idade, e idade com cap zero.

**Decisões de design:**
- Validação em **feature** (reuso) vs mutação no **agregado** (arquivar+podar), espelhando o split já usado por `PasswordPolicy`.
- Comparação por **hash da candidata** contra cada hash armazenado (hashes `$RIPWD$` são salgados — não se compara hash com hash).
- Schema por `EnsureCreated`/model (sem migrations, como o resto do módulo); tabela nomeada `UserAccountPasswordHistory` (convenção do módulo) — o modelo do plano cita `PasswordHistory`.
- Expiração é só **detecção** nesta fase; o roteamento para o desafio de troca (required action) é a Fase 4.

**Verificação:** `dotnet build RoyalIdentity.sln` — sucesso. Testes: **Tests.UserAccounts 119/119**, Tests.Integration 203/203,
Tests.Security 116/116, Tests.Identity 13/13, Tests.Pipelines 3/3, Tests.Architecture 15/15 — todos verdes. Novos testes:
`PasswordLifecycleTests` (arquivamento/poda por quantidade e idade/cap, reuso atual+histórico+pruned, flags off, expiração),
`UserAccountsPersistenceTests.PasswordHistory_RoundTrips_AndIsPrunedToRetainedQuantity`,
`UserAccountUseCasesTests.ChangePassword_RejectsReuse_OfCurrentAndRecentPasswords_WhenHistoryEnforced`, e os de `Validate`.

---

## Fase 4 - Troca de senha e required action (`MustChangePassword`/expirada)

**Depende de:** Q3 (desafio), Q4 (forma do required action), Q12 (HTTP/UI).

**O que/como:** entregar a troca de senha **user-facing** (gated por `AllowChangePassword`) e o **estado intermediário**
de login para `MustChangePassword`/senha expirada, sem criar sessão real durante o desafio.

**Tarefas:**

- [x] Caso de uso de troca user-facing (gated por `AllowChangePassword`), distinto do `ChangeUserAccountPassword` de
      seed/admin do v2 (que **não** é gated, por intenção).
- [x] Introduzir o required action na borda (Q4): senha válida + (`MustChangePassword` **ou** expirada) ⇒ resultado de
      required action; **sem** sessão SSO/code/token; desafio curto (Q3) leva à troca; depois, **login real** com a nova
      senha (Decisão fechada §Senha). (O **token transitório** `Purpose=ChangeExpiredPassword` da Q3/Q5 é entregue na
      Fase 5; a Fase 4 estabelece o **sinal** e o **roteamento** que levam à troca.)
- [x] Integrar no `LoginFlowService`/`.Integration` o roteamento do required action (sem reescrever a borda).
- [x] Sem telas/endpoints user-facing (Q12 — decidido: ficam para o plano de admin/UI); entregar só a costura + casos de uso.

**Critérios de aceite:** `MustChangePassword`/expirada **nunca** emitem token antes da troca; o desafio é uso único; pós
troca exige novo login; troca user-facing respeita `AllowChangePassword`; anti-enumeração preservada; regressão OIDC
verde.

**Testes:** unidade do caso de uso; integração do required action no login.

### Resultado da Fase 4

**Concluída (2026-06-24).** A borda de autenticação ganhou o conceito de **required action** (Q4) de forma aditiva, e a
troca de senha **user-facing** (gated por `AllowChangePassword`) virou um caso de uso próprio, distinto do de seed/admin.
O modelo segue o split do módulo: o **domínio** decide quando uma credencial válida é gated; a **`.Integration`** mapeia
para a borda; o **`LoginFlowService`** roteia sem criar sessão/cookie/token; a **validação** de troca fica em features
(`PasswordPolicy` + `PasswordHistoryPolicy`). Nenhuma tela/endpoint (Q12).

**Arquivos novos:**
- `RoyalIdentity/Users/RequiredAction.cs` (core) — `enum RequiredActionType { ChangePassword }` + record `RequiredAction`
  (estrutura tipada, não um novo *reason*; extensível para verificações futuras — Q4/ADR-017 §2.3).
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/LocalRequiredAction.cs` (módulo) — enum espelho
  (`ChangePasswordMustChange`/`ChangePasswordExpired`); mantém o motivo (admin vs expirada) para eventos/diagnóstico do
  módulo, mesmo que a borda colapse ambos em "trocar senha".
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/ChangeOwnPassword.cs` (módulo) — caso de uso user-facing gated
  por `AllowChangePassword`; aplica complexidade + histórico (Fase 3) antes de `UserAccount.ChangePassword`.

**Arquivos alterados:**
- `RoyalIdentity/Users/AuthenticationResult.cs` — terceiro estado `RequiresAction(subject, requiredAction)`: credencial
  válida mas gated (`Success=false`, `Reason=null`, `RequiredAction` setado, `Subject` carregado para o desafio).
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/LocalAuthenticationResult.cs` — espelha `RequiredAction` +
  factory `RequiresAction(account, action)`.
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs` — `AuthenticateLocal`, após verificar a senha,
  retorna required action quando `MustChangePassword` (precedência) **ou** `IsPasswordExpired` (Fase 3); o reset de
  falhas/`Touch` continua persistido.
- `RoyalIdentity.UserAccounts.Integration/LocalUserAuthenticator.cs` — mapeia `LocalRequiredAction` →
  `AuthenticationResult.RequiresAction(subject, RequiredAction.ChangePassword)` (ambos os motivos colapsam).
- `RoyalIdentity/Users/LoginFlowResult.cs` — novo outcome `LoginFlowOutcome.RequiresPasswordChange`.
- `RoyalIdentity/Users/Defaults/LoginFlowService.cs` — roteia o required action **antes** do ramo de falha/sucesso, sem
  criar sessão/cookie/token e sem despachar evento de login.

**Decisões de design:**
- `RequiredAction` é **estrutura tipada** (não um `reason` novo) — ADR-017 §2.3; o motivo (must-change vs expirada) é
  preservado no enum do módulo e **colapsado** na borda (ambos ⇒ "trocar senha"), porque o roteamento é idêntico.
- O required action é um **terceiro estado** do `AuthenticationResult` (`Success=false` **sem** `Reason`); o
  `LoginFlowService` o trata **antes** de `!Success`, então uma conta gated nunca cai no ramo de falha (anti-enumeração
  preservada: a UI recebe um outcome genérico, sem motivo).
- **`MustChangePassword` tem precedência** sobre expiração na detecção (sinal administrativo > política).
- **Senha errada não vira required action**: o gate só se aplica a uma credencial **válida** (a falha continua
  `InvalidCredentials`/lockout).
- O caso de uso user-facing (`ChangeOwnPassword`) **respeita `AllowChangePassword`** (gate em `Execute`, como o
  `AllowProvidedSubjectId` do `CreateUserAccount`); o de seed/admin (`ChangeUserAccountPassword`) permanece **não** gated.
- A UI e o **token transitório** `Purpose=ChangeExpiredPassword` (Q3/Q5) ficam para fora desta fase: a Fase 5 entrega o
  `UserAccountActionToken`; a Fase 4 entrega o **sinal** (required action) e o **roteamento** que levam à troca + re-login.

**Verificação:** `dotnet build RoyalIdentity.sln` — sucesso. Testes: **Tests.UserAccounts 127/127** (8 novos),
Tests.Integration 203/203, Tests.Security 116/116, Tests.Identity 13/13, Tests.Pipelines 3/3, Tests.Architecture 15/15 —
todos verdes. Novos testes: domínio (`AuthenticateLocal` required action por must-change/expirada, precedência, senha
errada não gera ação, reset de falhas no caminho gated), use case (`ChangeOwnPassword` honra `AllowChangePassword`;
`AuthenticateLocalCredential` expõe o required action), integração (`LocalUserAuthenticator` mapeia para
`RequiresAction` com subject; `LoginFlowService` roteia `RequiresPasswordChange` **sem** iniciar sessão — provado por
`ThrowingUserSessionService`/`ThrowingSubjectPrincipalFactory` — e sem despachar evento).

---

## Fase 5 - Recuperação de senha e tokens de ação

**Depende de:** Q3 (modelo de token compartilhado), Q7 (invalidação no reset), Q12 (HTTP/UI),
Q13 (costura/registro da invalidação).

**O que/como:** implementar `UserAccountActionToken` e o fluxo de recuperação com anti-enumeração e uso único
idempotente.

**Tarefas:**

- [x] Implementar `UserAccountActionToken` (hash, TTL, uso único, revogação por nova emissão, `TargetValue`). O enum
      `ActionTokenPurpose` inclui `PasswordRecovery`/`ChangeExpiredPassword`/`EmailVerification`/`PhoneVerification`;
      a Fase 5 exercita `PasswordRecovery` e `ChangeExpiredPassword`.
- [x] `RequestPasswordRecovery` (gated por `AllowForgotPassword`): resposta pública **sempre igual**; só emite token se a
      conta existir e a policy permitir; sem revelar existência/estado. **Rate limit:** entregue como **cooldown por
      realm/conta** (`PasswordRecoveryResendCooldownSeconds`, default 0 = off); o limite por **IP/identificador** é
      concern de borda (camada HTTP, Q12) — registrado como nota.
- [x] `ResetPasswordWithToken`: consumo idempotente (update condicional via `ExecuteUpdate`); aplica complexidade/histórico
      (Fase 3) **antes** de consumir (senha rejeitada não queima o token); move `SessionsValidAfter` (marcador de
      invalidação) via `UserAccount.ResetPassword`. A execução ativa sobre stores de sessão/refresh (Q7/Q13) é concluída
      na Fase 8.
- [x] `IssueChangeExpiredPasswordToken` + `ChangeExpiredPasswordWithToken`: token curto
      `Purpose=ChangeExpiredPassword`, vinculado ao `SecurityStamp`, uso único, validação de complexidade/histórico antes
      de consumir e re-login com a nova senha.
- [x] Gateway de notificação (`INotificationGateway`/no-op) para entregar o link/código (ver Arquitetura alvo); o token
      bruto trafega **uma vez** no `PasswordRecoveryNotification` e nunca é persistido/logado/auditado.
- [x] Sem telas/endpoints user-facing (Q12 — decidido: ficam para o plano de admin/UI); entregue só a costura + casos de uso.

**Critérios de aceite:** token só verificável uma vez; nova emissão revoga anteriores; conta inexistente não cria token
nem revela; reset aplica políticas de senha; solicitação de invalidação segue a policy decidida; hashes de token nunca
expostos.

**Testes:** unidade (emissão/consumo/revogação/anti-enumeração); concorrência de consumo (Fase 10 amplia).

### Resultado da Fase 5

**Concluída (2026-06-24).** O modelo de **token de ação** (`UserAccountActionToken`) e o fluxo de **recuperação de
senha** com anti-enumeração e uso único idempotente entraram no módulo puro, seguindo o split de sempre: domínio (token
+ `ResetPassword`), persistência própria (mapeamento + DbSet), serviço de feature (emissão/consumo) e casos de uso
SmartCommands; a entrega externa fica atrás de um **gateway abstrato** com no-op default. Nenhuma tela/endpoint (Q12);
nenhuma referência do módulo ao core/ASP.NET.

**Arquivos novos:**
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/ActionTokenPurpose.cs` — `PasswordRecovery`/`EmailVerification`/
  `PhoneVerification`/`ChangeExpiredPassword` (modelo único; `PasswordRecovery` e `ChangeExpiredPassword` são exercitados aqui).
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/ActionTokenRevocationReason.cs` — `Superseded` (revogação por nova emissão).
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountActionToken.cs` — entidade EF-first: só o **hash** do
  token é guardado; TTL obrigatório; `TargetValue` (email/phone normalizado); `IsConsumable(now)`; `Revoke(reason, now)`
  idempotente; colunas de auditoria `CreatedIpHash`/`ConsumedIpHash`/`UserAgentHash` (preenchidas pela borda depois).
- `RoyalIdentity.UserAccounts/Infrastructure/Data/Mappings/UserAccountActionTokenMap.cs` — tabela
  `UserAccountActionTokens`; índices `(RealmId, UserAccountId, Purpose)` e único `(RealmId, TokenHash)`; FK para a conta
  **sem navegação** (ciclo independente do grafo do agregado); `CreatedAt`/`ExpiresAt` convertidos para **ticks UTC**
  (a comparação de TTL/throttle não traduz para SQL com `DateTimeOffset` no provider SQLite).
- `RoyalIdentity.UserAccounts/Features/Accounts/Commons/UserAccountActionTokenService.cs` — gera o token bruto
  (`CryptoRandom`) + hash (`Hashing.Sha256Base64Url` do `RoyalIdentity.Security`); `IssueAsync` revoga os ativos
  anteriores do mesmo `Purpose` e insere o novo; `FindConsumableAsync`/`TryConsumeAsync` fazem o **consumo idempotente
  via `ExecuteUpdate` condicional** (`ConsumedAt is null AND RevokedAt is null AND ExpiresAt > now`).
- `RoyalIdentity.UserAccounts/Infrastructure/Gateways/INotificationGateway.cs` +
  `PasswordRecoveryNotification.cs` + `NoopNotificationGateway.cs` — seam de entrega (email/SMS) definido no módulo puro,
  com no-op default (`TryAdd`); o token bruto trafega **uma única vez** no payload e nunca é persistido/logado/auditado.
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/RequestPasswordRecovery.cs` — gated por `AllowForgotPassword`;
  resposta pública **idêntica** para conta inexistente/inelegível; emite token só para conta ativa, com senha e email
  primário; commita explicitamente (`SaveAsync`) e despacha a notificação pós-commit (best-effort) via
  `INotificationGateway`; cooldown por realm opcional.
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/ResetPasswordWithToken.cs` — valida complexidade/histórico
  **antes** de consumir; consome (uso único); `account.ResetPassword(...)`.
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/IssueChangeExpiredPasswordToken.cs` e
  `ChangeExpiredPasswordWithToken.cs` — emitem/consomem o token transitório de troca forçada/expirada
  (`Purpose=ChangeExpiredPassword`) e exigem re-login com a nova senha.

**Arquivos alterados:**
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs` — novo `ResetPassword(newHash, options, changedAt)`
  (não verifica senha atual — o token já provou controle; reason=`Reset`; limpa `MustChangePassword`/lockout, arquiva
  histórico e move `SecurityStamp` **e** `SessionsValidAfter`).
- `RoyalIdentity.UserAccounts/Features/Accounts/Commons/UserAccountReader.cs` — `FindByIdAsync(realmId, accountId)`
  (carrega a conta a partir do FK físico resolvido pelo token).
- `RoyalIdentity.UserAccounts/Infrastructure/Data/UserAccountsDbContext.cs` — `DbSet<UserAccountActionToken>`.
- `RoyalIdentity.UserAccounts/Options/SecurityLifecycleOptions.cs` — `PasswordRecoveryTokenLifetimeMinutes` (default 60),
  `ChangeExpiredPasswordTokenLifetimeMinutes` (default 10) e `PasswordRecoveryResendCooldownSeconds` (throttle por realm,
  default 0).
- `RoyalIdentity.UserAccounts/Features/Accounts/Commons/UserAccountsServiceCollectionExtensions.cs` — registra
  `UserAccountActionTokenService` (scoped) e `INotificationGateway` → `NoopNotificationGateway` (`TryAdd`).

**Decisões de design:**
- **Consumo idempotente é a fonte única de uso-único**: `TryConsumeAsync` é um `ExecuteUpdate` condicional; tentativas
  concorrentes veem no máximo 1 linha afetada (ADR-017 §2.9 — token usa update condicional, **não** optimistic-retry).
- **Validar-antes-de-consumir**: uma senha rejeitada (complexidade/histórico) **não** queima o token; o consumo só
  acontece após a validação passar, deixando o token reutilizável para nova tentativa.
- **Anti-enumeração**: `RequestPasswordRecovery` devolve o mesmo sucesso genérico para conta inexistente/inativa/
  bloqueada/sem-senha/sem-email (sem emitir/entregar); só emite e despacha no caminho elegível. O reset usa erro
  genérico para token inválido/expirado/consumido (o token é o segredo, não revela estado da conta).
- **Token = hash + ticks UTC**: handles opacos de alta entropia (`CryptoRandom`) guardados como SHA-256; timestamps de
  comparação em ticks para traduzir o predicado em SQL em todos os providers (limitação do SQLite com `DateTimeOffset`).
- **Invalidação (Q7/Q13) no reset**: o módulo move `SessionsValidAfter` (marcador passivo) via `ResetPassword`; a
  **execução ativa** de revogação de sessões/refresh (`ISessionRevocationService`, flags `OnPasswordRecoveryReset`) é da
  borda e fecha na Fase 8.
- **Rate limit**: o módulo puro entrega o **cooldown por realm/conta**; o limite por **IP/identificador** depende do
  contexto HTTP e fica na borda (Q12). Os campos `*IpHash`/`UserAgentHash` do token são colunas prontas para a borda preencher.
- **Entrega pós-commit (revisado 2026-06-25)**: o use case injeta `IWorkContext`, **dispensa** `[WithWorkContext]`,
  commita com `SaveAsync` e só então despacha a notificação via `INotificationGateway` (best-effort: falha de transporte
  não falha o request — o token persistido permite reenvio). Uma única abstração de entrega (o gateway) e raio do token
  bruto mínimo (nunca volta no `Result`). O token de `ChangeExpiredPassword` **não** usa `TargetValue` (validade vem de
  uso único + TTL + `StillRequiresPasswordChange`); a amarração ao `SecurityStamp` foi descartada por over-restringir.

**Verificação:** `dotnet build RoyalIdentity.sln` — sucesso, 0 erros. Testes: **Tests.UserAccounts 136/136** (9 novos),
Tests.Integration 203/203, Tests.Security 116/116, Tests.Identity 13/13, Tests.Pipelines 3/3, Tests.Architecture 15/15 —
todos verdes. Novos testes: domínio (`ResetPassword` limpa forced-change/lockout e move `SessionsValidAfter`; token
`IsConsumable`/`Revoke` idempotente), use case (emissão→reset→login com a nova senha; anti-enumeração; uso único; nova
emissão revoga a anterior; cooldown de reenvio; senha fraca não consome o token; gate `AllowForgotPassword`).

---

## Fase 6 - Verificação de email e telefone

**Depende de:** Q9 (telefone entra?), Q10 (`VerifiedAt`/semântica `email_verified`), Q12 (HTTP/UI).

**O que/como:** reusar o modelo de token de ação para verificar email e (se Q9=(a)) telefone, amarrando à projeção de
claims existente.

**Tarefas:**

- [x] `EmailVerification` (token com `Purpose=EmailVerification`, vinculado ao `TargetValue`): ao consumir, marca
      `IsVerified` do email **alvo** (Q10 — sem `VerifiedAt`); **trocar** o email gera novo objeto não-verificado (não reset).
      Entregue como `RequestEmailVerification` (emite, commita e despacha via gateway pós-commit) +
      `ConfirmEmailVerification` (consome + `UserAccount.VerifyEmail`).
- [x] Confirmar/registrar que `email_verified` deriva do **email primário verificado** (projeção fixa já existe). Coberto por
      teste de projeção (antes da verificação = `false`; depois = `true`).
- [x] Modelar `UserAccountPhone` + verificação + projeções `phone_number`/`phone_number_verified`
      (Q9 — decidido: telefone entra, como o email, **opcional por realm**; realiza a emenda da ADR-017 §2.8 à ADR-015 §2.6).
      Gated por `EnablePhoneNumber` (default off); projeções fixas default `Include=false` e inertes quando telefone está off.
      Entregue como `RequestPhoneVerification` (emite, commita e despacha via gateway pós-commit) /
      `ConfirmPhoneVerification` + `UserAccount.AddPhone`/`VerifyPhone` + normalização de telefone.
- [x] Sem telas/endpoints user-facing (Q12 — decidido: ficam para o plano de admin/UI); entregue só a costura + casos de uso.

**Critérios de aceite:** verificação confirma só o valor alvo (não um valor trocado depois); `email_verified` coerente
com o primário; telefone conforme Q9; anti-enumeração preservada.

**Testes:** unidade (verificação/target/troca gera novo objeto não-verificado); projeção de claims
(`email_verified`/`phone_*`).

### Resultado da Fase 6

**Concluída (2026-06-24).** A verificação de **email** e **telefone** reusou o modelo de token de ação da Fase 5
(`UserAccountActionToken` com `Purpose=EmailVerification`/`PhoneVerification`, vinculado ao `TargetValue`), amarrada à
projeção de claims fixos. O telefone entrou como o email, porém **opcional por realm** (`EnablePhoneNumber`, default off;
projeções `phone_*` default `Include=false`), realizando a emenda da ADR-017 §2.8 à ADR-015 §2.6 — sem editar a ADR-015
(o amendment vive na ADR-017). Nenhuma tela/endpoint (Q12); módulo puro sem core/ASP.NET.

**Arquivos novos:**
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountPhone.cs` — entidade espelho do email
  (`Number`/`NormalizedNumber`/`IsPrimary`/`IsVerified`; **sem** `VerifiedAt`, Q10).
- `RoyalIdentity.UserAccounts/Infrastructure/Data/Mappings/UserAccountPhoneMap.cs` — tabela `UserAccountPhones`; único
  `(RealmId, UserAccountId, NormalizedNumber)`.
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/RequestEmailVerification.cs` +
  `ConfirmEmailVerification.cs` — emite token vinculado ao email normalizado, commita (`SaveAsync`) e despacha via
  `INotificationGateway` (pós-commit, best-effort) / consome e marca `IsVerified` do **alvo**.
- `RoyalIdentity.UserAccounts/Features/Accounts/UseCases/RequestPhoneVerification.cs` +
  `ConfirmPhoneVerification.cs` — idem para telefone, gated por `EnablePhoneNumber`.
- `RoyalIdentity.UserAccounts/Infrastructure/Gateways/EmailVerificationNotification.cs` +
  `PhoneVerificationNotification.cs` — payloads de entrega (token bruto trafega uma vez, nunca persistido/logado).

**Arquivos alterados:**
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs` — coleção `Phones`/`PhoneItems`/`PrimaryPhone`;
  `VerifyEmail(normalizedAddress, changedAt)` (idempotente; alvo específico); `AddPhone`/`VerifyPhone` (espelham
  email; movem `SecurityStamp` sem invalidar sessões — gatilho sensível não-comprometimento, Q7).
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountEmail.cs` — `MarkVerified()` interno.
- `RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountEvents.cs` — `UserAccountEmailVerified`,
  `UserAccountPhoneAdded`, `UserAccountPhoneVerified`.
- `RoyalIdentity.UserAccounts/Features/Accounts/Commons/IUserAccountNormalizer.cs` +
  `DefaultUserAccountNormalizer.cs` — `NormalizePhoneNumber` (mantém dígitos + `+` líder).
- `RoyalIdentity.UserAccounts/Features/Accounts/Commons/UserAccountReader.cs` — grafo inclui `PhoneItems`.
- `RoyalIdentity.UserAccounts/Infrastructure/Data/Mappings/UserAccountMap.cs` — `HasMany PhoneItems` + `Ignore` de
  `Phones`/`PrimaryPhone`.
- `RoyalIdentity.UserAccounts.Sqlite/UserAccountsSqliteModelBuilderExtensions.cs` +
  `RoyalIdentity.UserAccounts.PostgreSql/UserAccountsPostgreSqlModelBuilderExtensions.cs` — índices parciais para
  garantir no banco no máximo um telefone primário por conta.
- `RoyalIdentity.UserAccounts/Options/FixedFieldClaimProjection.cs` — campos `PrimaryPhone`/`PhoneVerified`.
- `RoyalIdentity.UserAccounts/Features/ScopeProperties/Commons/UserAccountClaimProjector.cs` — projeta `phone_number`/
  `phone_number_verified` do **telefone primário** apenas quando `EnablePhoneNumber=true`.
- `RoyalIdentity.UserAccounts/Options/UserAccountsRealmOptions.cs` — `EnablePhoneNumber` (copy-on-create) + projeções
  default de telefone (`Include=false`) e projeções fixas de telefone inertes na validação quando `EnablePhoneNumber=false`.
- `RoyalIdentity.UserAccounts/Options/SecurityLifecycleOptions.cs` — TTLs
  `EmailVerificationTokenLifetimeMinutes` (1440) e `PhoneVerificationTokenLifetimeMinutes` (15) + validação.
- `RoyalIdentity.UserAccounts/Infrastructure/Gateways/INotificationGateway.cs` + `NoopNotificationGateway.cs` —
  `SendEmailVerificationAsync`/`SendPhoneVerificationAsync`.

**Decisões de design:**
- **Verificação amarrada ao alvo (`TargetValue`)**: `Confirm*` verifica exatamente o valor para o qual o token foi
  emitido; um valor trocado/adicionado depois **não** é verificado pelo token antigo (atende o critério de aceite).
- **Trocar gera novo objeto, sem `VerifiedAt`** (Q10): `email_verified`/`phone_number_verified` derivam do primário
  verificado; um novo email/telefone entra não-verificado, sem reset do existente.
- **Telefone é opt-in** (Q9): `EnablePhoneNumber` gateia os casos de uso de telefone; as projeções `phone_*` são fixas
  mas default desligadas (`Include=false`) e inertes quando o recurso está off, então realms que não usam telefone não
  emitem essas claims e a validação de duplicidade não é afetada.
- **Entrega pós-commit (revisado 2026-06-25)**: `RequestEmailVerification`/`RequestPhoneVerification` injetam
  `IWorkContext`, commitam com `SaveAsync` e despacham via `INotificationGateway` dentro do `Execute`, após a persistência
  (best-effort). Sem `[WithWorkContext]` e sem tipo de payload no `Result`.
- **Primário único no banco**: email e telefone primários são protegidos por índice parcial provider-specific; o agregado
  continua limpando o primário anterior, mas import/SQL direto não conseguem criar dois primários.
- **Verificação move só o `SecurityStamp`** (não `SessionsValidAfter`): é gatilho sensível **não-comprometimento**
  (Q6/Q7) — bookkeeping de claims, não derruba sessão.
- **Consumo idempotente reusado**: `Confirm*` usa o mesmo `FindConsumableAsync`/`TryConsumeAsync` (update condicional)
  da Fase 5; falha genérica para token inválido/expirado/consumido (anti-enumeração; o token é o segredo).
- **Eventos por caso de uso** (Q8): a verificação emite `UserAccountEmailVerified`/`UserAccountPhoneVerified`
  (despacho/auditoria fecham na Fase 9, como os demais eventos do agregado).

**Verificação:** `dotnet build RoyalIdentity.sln` — sucesso, 0 erros. Testes: **Tests.UserAccounts 144/144** (8 novos),
Tests.Integration 203/203, Tests.Security 116/116, Tests.Identity 13/13, Tests.Pipelines 3/3, Tests.Architecture 15/15 —
todos verdes. Novos testes: domínio (`VerifyEmail` alvo/idempotência/ausente; `AddPhone` primário único/duplicado/realm;
`VerifyPhone` move stamp sem invalidar), use case (email request→confirm→`email_verified=true`; token verifica só o alvo;
anti-enumeração + uso único; phone request→confirm→projeções `phone_number`/`phone_number_verified`; phone gated por
`EnablePhoneNumber`).

**Correção pós-review da Fase 6 (2026-06-25):** projeções `phone_*` ficaram inertes quando `EnablePhoneNumber=false`,
inclusive na validação de claim types; índice parcial de telefone primário foi adicionado nos providers SQLite/PostgreSQL.

**Refinamento de entrega (2026-06-25):** o padrão de notificação foi consolidado — os três requests (recuperação,
verificação de email e de telefone) deixaram de retornar payload (`*RequestResult` removidos) e passaram a **commitar com
`SaveAsync` e despachar via `INotificationGateway` no próprio `Execute`** (sem `[WithWorkContext]`, dispatch best-effort);
o raw token nunca volta no `Result`. Também foi removida a amarração do token `ChangeExpiredPassword` ao `SecurityStamp`
(validade por uso único + TTL + `StillRequiresPasswordChange`, evitando over-restrição por mudanças sensíveis não
relacionadas). Suite completa verde: Tests.UserAccounts 158/158, Tests.Integration 203/203, Tests.Security 116/116,
Tests.Identity 13/13, Tests.Pipelines 3/3, Tests.Architecture 15/15.

---

## Fase 7 - Lockout, bloqueio administrativo e restrições de acesso

**Depende de:** Q5 (taxonomia), Q11 (concorrência de contadores).

**O que/como:** consolidar o lockout temporário (já no autenticador) e o bloqueio administrativo na taxonomia decidida,
implementando primeiro `PasswordLockout` + `AdminBlock`.

**Tarefas:**

- [x] Implementar Q5 (decidido): `UserAccountBlockState` como AdminBlock pessoal **com janela** `StartsAt`/`EndsAt`;
      `AccountAccessRestriction` aposentada; `Geo/Time/Client` registrados no backlog. Lockout temporário permanece
      derivado da credencial.
- [x] Caso de uso `UnlockPasswordCredential` (admin) que zera contador/`LockoutEndAt` e incrementa versão (Q11/§10);
      lockout indefinido (`AccountLockoutDurationMinutes = 0`) exige unlock administrativo.
- [~] Auditoria de falhas (eventos da Fase 9): lock já emite `UserAccountLocalCredentialLocked`; adicionado o simétrico
      `UserAccountLocalCredentialUnlocked` (emitido por intenção de negócio, condicional). O catálogo restante de
      falhas/lockout e o **sink de auditoria/classificação** ficam na Fase 9 (Q8: evento por caso de uso, sem catálogo
      antecipado).
- [x] Mapear cada estado ao reason de borda existente (`Inactive`/`Blocked`) sem conflar lockout com admin block
      (lockout e admin block → `Blocked`; o bloqueio agora é avaliado pela **janela** em `AuthenticateLocal`).

**Critérios de aceite:** lockout temporário e bloqueio administrativo são estados distintos; unlock administrativo
funciona; contadores corretos sob concorrência; reasons de borda corretos; restrições futuras (`Geo`/`Time`/`Client`)
registradas como design futuro por realm/grupo, sem tabela per-account neste plano.

**Testes:** unidade (lockout/unlock/admin block); concorrência (Fase 10).

### Resultado da Fase 7

**Concluida (2026-06-25).** Lockout temporário (derivado da credencial) e bloqueio administrativo pessoal são estados
distintos, sem conflação.

- **Janela de bloqueio (Q5):** `UserAccountBlockState` ganhou `StartsAt`/`EndsAt` (ambos `null` = imediato e
  indefinido). `IsBlocked` permanece como o *flag configurado*; a efetividade é avaliada por `IsActiveAt(now)` /
  `UserAccount.IsBlockedAt(now)` no intervalo `[StartsAt, EndsAt)`. `UserAccount.Block` recebe a janela opcional;
  `AuthenticateLocal` passou a usar `IsBlockedAt(attemptedAt)` (um bloqueio agendado/expirado não rejeita login fora da
  janela). As comparações de janela rodam **em memória** (grafo já carregado), sem tradução LINQ→SQL — colunas
  `BlockStartsAt`/`BlockEndsAt` mapeadas como `DateTimeOffset?` simples (sem value converter). `RequestPasswordRecovery`
  passou a consultar `IsBlockedAt(now)` em vez do flag bruto.
- **Casos de uso (admin):** `BlockUserAccount` (valida a janela **na feature**, não no agregado: fim posterior ao
  início efetivo, senão `user_account.block_window_invalid`), `UnblockUserAccount`, e `UnlockPasswordCredential` (zera
  contador/`LockoutEndAt` + incrementa `Version`; cobre o lockout **indefinido** que só o admin/troca-de-senha limpa).
  Todos `[Command, WithValidateModel, WithWorkContext]`, retornam `Result`, `NotFound` para subject inexistente.
- **Evento:** `UserAccountLocalCredentialUnlocked` (simétrico ao `...Locked`), emitido por `UnlockLocalCredential`
  apenas quando havia lockout a limpar. Catálogo de falhas restante + sink de auditoria ficam na Fase 9.
- **Restrições `Geo/Time/Client`:** permanecem registradas no backlog (`backlog-001.md`), sem tabela per-account.
- **Testes:** domínio (janela efetiva só no intervalo; `AuthenticateLocal` honra a janela; unlock de lockout indefinido
  + evento condicional) e casos de uso (block imediato + unblock; janela futura bloqueando só no intervalo; janela
  inválida e subject inexistente; unlock de lockout indefinido só por ação admin). Concorrência dos contadores fica na
  Fase 10.

---

## Fase 8 - Invalidação de sessões/cookies/refresh tokens

**Depende de:** Q6 (security stamp/comparação), Q7 (política/defaults), Q13 (costura de execução),
Q14 (`ExpiresAt`/`UserSsoLifetime`/cookie lifetime), Q15 (seam do stamp).

**O que/como:** validar sessões por `SessionsValidAfter` e executar a política de invalidação por realm sobre
sessões/cookies/refresh tokens, mantendo `SecurityStamp` capturado para cookie/claims e rastreabilidade.

> **Refinamento de fronteira (decisão do autor, 2026-06-25):** o **ciclo de sessão é responsabilidade do IdP**, não do
> módulo de usuários (a sessão é um conceito do core — `UserSession`). Por isso as options de sessão
> (`EnableSsoSessionExpiration`, `SsoSessionMax/Idle`, `IdleTouchInterval` e o gate passivo
> `EnableSessionInvalidationByState`) **saíram do módulo** (`SecurityLifecycleOptions`) e foram para o **core**
> (`RealmOptions.Session` — novo `SessionOptions`). Isto **emenda a Fase 1 e a ADR-017 §2.12/§2.13**. Permanecem no
> módulo: os flags `On*` de revogação **ativa** por gatilho de credencial (Q7), os token lifetimes e o audit.

**Tarefas:**

- [x] `IUserSessionService.IsSessionValidAsync = ativo && não-expirado(ExpiresAt, Realm-only — Q14) && (policy ?
      session.StartedAt >= conta.SessionsValidAfter : true)`, lendo o estado atual via `IUserSecurityStateProvider`
      (Q15) quando a policy exigir; cookie `OnValidatePrincipal` segue a costura por request.
- [x] Validação de composição Q15: se `Session.RequiresSecurityStateProvider` (core) estiver `true` e o provider de
      usuários **não** expuser `IUserSecurityStateProvider`, é **erro de configuração** — aplicado por *fail-fast* em
      `IsSessionValidAsync` (evita policy silenciosamente inoperante). O fake in-memory **não** expõe a capacidade
      (degrada); o módulo **sempre** expõe.
- [x] Caso de fronteira `StartedAt == SessionsValidAfter` coberto (sessão **válida**: `StartedAt >= marker`).
      Precisão/clock entre providers fica para a persistência real (in-memory usa `TimeProvider`).
- [x] Emenda no core (Q13 — decidido): `ISessionRevocationService` dedicado (`DefaultSessionRevocationService`), sobre
      `IUserSessionStore` (+ `EndSessionsForSubjectAsync`) e `IRefreshTokenStore` (+ `RemoveBySubjectAsync`), revogação
      por subject de sessões e refresh tokens; implementado no fake in-memory.
- [x] `.Integration` traduz a política do módulo (Q7) em chamadas a `ISessionRevocationService` via
      `SessionInvalidationExecutor` (idempotente, pós-commit). **O gatilho** que chama o executor (use case de
      credencial / evento `SecurityInvalidationRequested`) é cabeado com os eventos de conta (**Fase 9**) / endpoints
      (Q12) — não há orquestrador hoje, então a Fase 8 entrega o **mecanismo** pronto.
- [x] Defaults por gatilho ratificados em Q7 permanecem no módulo (`On*`); enforcement passivo via `SessionsValidAfter`
      no core; **expiração de senha não derruba sessão ativa** (apenas barra nova autenticação).
- [x] `UserSession.ExpiresAt` ganhou comportamento **Realm-only** (definido no sign-in a partir de `RealmOptions.Session`);
      `UserSsoLifetime` por client inalterado no `PromptLoginDecorator` (ortogonal); `UserSession.LastSeenAt` adicionado.
- [x] Idle touch com throttle (Q14): a validação só persiste (`IUserSessionStore.TouchAsync`) quando
      `now - LastSeenAt >= IdleTouchInterval`; ao tocar, `ExpiresAt = min(StartedAt + SsoSessionMax, now + SsoSessionIdle)`
      (nunca passa do max). Guardas em `SessionOptions.Validate()` (`IdleTouchInterval > 0` e `< SsoSessionIdle`).

**Critérios de aceite:** sessão iniciada antes de `SessionsValidAfter` é inválida quando a policy exige; idle touch não
gera escrita por request e nunca estende além do SSO session max; revogar outras/todas as sessões e refresh tokens
funciona por subject; execução idempotente e pós-commit; sessões ativas seguem emitindo token quando a policy não pede
invalidação (Decisão fechada §Senha).

**Testes:** integração (`SessionsValidAfter` invalida sessão; idle touch com throttle e limite max; revogação por
subject; refresh revogado por subject; composição = erro); regressão OIDC.

### Resultado da Fase 8

**Concluida (2026-06-25).** Validação de sessão e invalidação por estado/expiração viraram comportamento do core,
mantendo o módulo independente.

- **Fronteira (decisão do autor):** options de **ciclo de sessão** movidas do módulo para o core
  (`RoyalIdentity/Options/SessionOptions.cs` em `RealmOptions.Session`, com copy-on-create e `Validate()`). O módulo
  `SecurityLifecycleOptions` ficou só com `On*` (revogação ativa por gatilho — Q7), token lifetimes e audit. Emenda a
  Fase 1 / ADR-017 §2.12/§2.13.
- **`UserSession`:** `LastSeenAt` adicionado; `ExpiresAt` ganhou comportamento (definido no sign-in a partir de
  `RealmOptions.Session`, idle puxa para `min(StartedAt+Max, ref+Idle)`).
- **`IsSessionValidAsync`** (`DefaultUserSessionService`): `ativo && não-expirado(ExpiresAt) && (policy ? StartedAt >=
  SessionsValidAfter : true)` + idle touch com throttle via `IUserSessionStore.TouchAsync`. A capacidade
  `IUserSecurityStateProvider` (core, opcional, realm-bound via `IUserDirectory.GetSecurityStateProvider`) lê
  stamp/`SessionsValidAfter`; o módulo **gateia** `SessionsValidAfter` pela policy do realm (null = não enforça);
  policy on + capacidade ausente = **erro de configuração** (fail-fast — Q15.3).
- **Captura do stamp:** `LoginFlowService` lê `IUserSecurityStateProvider` no sign-in e grava `UserSession.SecurityStamp`.
- **Revogação ativa (Q13):** `ISessionRevocationService` + `DefaultSessionRevocationService` sobre os stores (novos
  `IUserSessionStore.EndSessionsForSubjectAsync` e `IRefreshTokenStore.RemoveBySubjectAsync`, implementados no fake).
  A `.Integration` traduz `SessionInvalidation` (módulo) → `SessionRevocation` (core) via `SessionInvalidationExecutor`.
  O **gatilho** (chamar o executor quando uma credencial muda) é cabeado na **Fase 9** (eventos) / endpoints (Q12).
- **Suite completa verde:** Tests.Integration 221/221, Tests.UserAccounts 168/168, Tests.Security 116/116,
  Tests.Architecture 15/15, Tests.Identity 13/13, Tests.Pipelines 3/3.

---

## Fase 9 - Auditoria, eventos e (seletivo) outbox

**Depende de:** Q8 (escopo de auditoria/outbox).

**O que/como:** emitir e classificar os eventos de segurança nas três categorias (domínio/auditoria/outbox), sem criar
infraestrutura de outbox além do que Q8 autorizar.

> **Achado de mecânica (2026-06-25):** a stack RoyalCode em uso (WorkContext 0.8.9 / DomainEvents 0.8.1) **não
> despacha** os eventos de domínio automaticamente (o v2 os deixou só acumulados no agregado), e os eventos do módulo
> (`DomainEventBase`) são **incompatíveis** com o `Event` do `IEventDispatcher` do core. Decisão do autor: **despacho
> próprio do módulo via override do `DbContext.SaveChangesAsync`** (coleta + dispatch pós-commit), mantendo o módulo
> puro; a auditoria é um **observer** do módulo, com a policy de categorias por realm vinda da `.Integration`.

**Tarefas:**

- [x] Emitir eventos de domínio **por caso de uso** (sem catálogo antecipado — Q8), chaveados por `(RealmId, SubjectId)`,
      **despachados pós-commit** pelo override de `UserAccountsDbContext.SaveChangesAsync` → `IDomainEventDispatcher`
      (módulo) → `IDomainEventObserver`s. (O bridge para o `IEventDispatcher` do core, se necessário p/ eventos de
      integração, fica disponível pela mesma costura — não havia consumidor de core a alimentar nesta fase.)
- [x] `ISecurityAuditSink` + auditoria **por categorias** (`Credential`, `Recovery`, `Verification`, `Lockout`,
      `AdminSecurity`, `SessionRevocation`, `AuthenticationFailure`) com **defaults segurança-on / resto-off** (Q8). As
      categorias por realm vivem em `SecurityLifecycleOptions.AuditCategories`; o `SecurityAuditObserver` mapeia evento →
      entrada e gateia por `ISecurityAuditPolicyProvider` (default all-on; a `.Integration` resolve por realm via
      `RealmSecurityAuditPolicyProvider`).
- [x] **Sem outbox** neste plano; **store durável de auditoria diferido** ao `plan-data-persistence` (§2) — por ora,
      `NoopSecurityAuditSink` (default) e um sink de gravação nos testes.
- [x] Eventos publicados **após o commit** (coleta antes, dispatch depois de `base.SaveChangesAsync`); **token bruto
      nunca** entra em evento/auditoria (a entrada carrega só `RealmId`/`SubjectId`/categoria/tipo/`OccurredAt`).

**Critérios de aceite:** eventos certos disparam nos pontos certos; classificação coerente; nenhuma máquina de outbox
introduzida sem decisão Q8; sem ruído de outbox para login/falha.

**Testes:** unidade (emissão por caso de uso); (se aplicável) durabilidade/idempotência de auditoria/outbox.

### Resultado da Fase 9

**Concluida (2026-06-25).** Eventos de domínio passam a ser **despachados** e os de segurança **auditados por
categoria**, sem outbox e sem quebrar a pureza do módulo.

- **Despacho (decisão do autor):** override de `UserAccountsDbContext.SaveChangesAsync` coleta os `DomainEvents` dos
  agregados rastreados, limpa-os, commita e **despacha pós-commit** via `IDomainEventDispatcher` (módulo) →
  `IDomainEventObserver`s. O dispatcher é injetado por DI no ctor do DbContext (validado: o WorkContext constrói o
  context pelo SP do escopo). Resolve o achado de que a stack RoyalCode não auto-despacha.
- **Auditoria (Q8):** `ISecurityAuditSink` + `SecurityAuditEntry` (sem segredos) + `NoopSecurityAuditSink` (default);
  `SecurityAuditObserver` mapeia os eventos existentes para categorias (`UserAccountPasswordChanged`→Credential;
  `...Locked`/`...Unlocked`→Lockout; `Blocked`/`Unblocked`→AdminSecurity; `EmailVerified`/`PhoneVerified`→Verification)
  e gateia por `ISecurityAuditPolicyProvider`. A `.Integration` registra `RealmSecurityAuditPolicyProvider` (lê
  `SecurityLifecycle.AuditCategories` do realm via resolver), substituindo o default all-on.
- **Sem catálogo antecipado (Q8):** só os eventos que já existem são mapeados; novos nascem com novos casos de uso.
  Categorias `Recovery`/`AuthenticationFailure` ainda não têm evento de agregado correspondente (a emissão de token de
  recuperação e a falha genérica de senha não geram evento) — ficam para quando um evento de intenção surgir.
- **Gatilho de revogação ativa (pendência da Fase 8):** continua diferido — depende de contexto de borda (o `sid`
  atual a preservar) que só os endpoints (Q12) têm; a garantia de segurança já é dada **passivamente** por
  `SessionsValidAfter` (Fase 8). O mecanismo (`SessionInvalidationExecutor`) está pronto para esse gatilho.
- **Suite completa verde:** Tests.UserAccounts 173/173, Tests.Integration 222/222, Tests.Security 116/116,
  Tests.Architecture 15/15, Tests.Identity 13/13, Tests.Pipelines 3/3.

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

### Resultado da Fase 10

*a preencher*

---

## Invariantes a preservar

1. O **módulo puro** continua sem referência a `RoyalIdentity` (core) e sem ASP.NET; só a `.Integration` conhece o IdP.
2. **`sub` = `SubjectId`**, imutável; nenhuma operação de ciclo de segurança troca o `sub`.
3. **Anti-enumeração**: login, recuperação e verificação não revelam existência/estado da conta; motivo interno fica
   só em auditoria.
4. **`MustChangePassword`/senha expirada nunca emitem token** antes da troca; após a troca, exige-se novo login real.
5. **Sessões ativas seguem emitindo tokens** salvo política de invalidação por realm.
6. **Tokens de ação**: hash-only, TTL, uso único idempotente, revogados por nova emissão, vinculados ao `TargetValue`;
   o token bruto só sai como *delivery payload* uma vez e **nunca** entra em evento/auditoria/log.
7. **`SecurityStamp`** versiona estado sensível (muda em todos os gatilhos); a **invalidação** de sessões usa
   **`SessionsValidAfter`** (marcador que move só nos gatilhos que devem derrubar sessão), enforçado quando a policy do
   realm exigir — não mudanças de perfil.
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
5. `SecurityStamp` implementado e capturado na sessão; invalidação validada por `SessionsValidAfter` via seam core-owned.
6. Invalidação por realm de sessões/cookies/refresh tokens funciona pela costura `.Integration` → core (Q13).
7. Eventos de segurança emitidos/classificados (auditoria/outbox conforme Q8).
8. Concorrência coberta pelos 7 cenários; consumo/revogação idempotentes.
9. Contract tests provam paridade fake×módulo; suíte OIDC verde com integração opt-in; fake permanece default.
10. Módulo puro independente do core; fronteiras protegidas por `Tests.Architecture`.

---

## Riscos

- **Escopo amplo:** este é o maior plano de conta. Risco de inchaço — manter cada slice pequeno e guiado pelo Histórico de decisões;
  diferir telas/outbox/telefone quando a decisão permitir.
- **Emenda da borda (ADR-014):** required action, `SecurityStamp` em sessão e revogação por subject tocam core + fake +
  testes. Fazer aditivo e cedo (Fase 1/2) evita acoplamento posterior.
- **Tipos futuros de credencial (Q1 — decidido: manter singular):** MFA/passkey/passwordless serão credenciais próprias
  nos respectivos planos; **não** genericizar `UserAccountCredential` (evita payload genérico/gambiarra).
- **`SecurityStamp`/`SessionsValidAfter` mal escopados:** stamp em mudança comum gera ruído; `SessionsValidAfter`
  excessivo derruba sessões à toa; de menos deixa credencial comprometida válida. Q6/Q7 fixam os gatilhos.
- **Seam de estado de segurança mal definido:** se o core tentar ler diretamente o módulo, viola as fronteiras da
  ADR-013/014/015; se a validação ficar no-op ou duplicada, a política de invalidação não é confiável. Q15 fecha a porta
  core-owned usada por fake e `.Integration`.
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
