# Review 001 — Domínio de contas (Fase 5, `UserAccount`)

- **Data:** 2026-06-18
- **Escopo:** `RoyalIdentity.UserAccounts/Features/Accounts/Domain/*` (Fase 5 do `plan-users-accounts-module-v2`)
- **Veredito:** ⛔ **REPROVADO** — redesenhar e refazer agora (não deixar para depois).
- **Build/testes no momento da review:** compila; `Tests.UserAccounts` 17/17 verde. *Os testes passarem não salva o design:* eles validam comportamento em memória, não a modelagem para persistência.

> **Nota de honestidade:** a primeira avaliação (informal) classificou esta fase como "alta qualidade". Estava
> **errada** — foi feita pela lente de "DDD em memória" e **não** pela lente correta: *isto é uma entidade que
> precisa ser persistida no EF*. Sob essa lente, o design falha em itens estruturais e ainda carrega code smells
> que o projeto rejeita. Esta review corrige a avaliação.

---

## 1. Princípio de avaliação (a lente correta)

`UserAccount` e seus filhos **são entidades de persistência**. Logo:

1. **Regra 1 — modelar para o banco primeiro.** Antes de "comportamento rico", a entidade tem que ser uma entidade
   EF válida: chave, `RealmId` estrutural, FKs, índices únicos, token de concorrência. Value object é aceitável e
   mapeia bem no EF — desde que **projetado** como tal agora.
2. **Validação de entrada não fica no agregado.** Vem **antes**, nos objetos de contrato/feature (commands),
   validando com **SmartValidations + SmartProblems**. O agregado recebe valores **já válidos e não-nulos**.
3. **Sem utilitários `static`, sem `throw` em fluxo esperado, sem normalização espalhada.** São code smells
   explicitamente não aceitos no projeto.

O design atual viola as três. Por isso é reprovado **agora** — construir Fases 6/7 sobre esta base multiplica o
retrabalho (a Fase 7 mapearia um modelo que não fecha, forçando refazer domínio + mapeamento + testes).

---

## 2. Defeitos bloqueantes — modelagem para persistência

### B1. Entidades-filho sem identidade/chave
`UserAccountEmail` ([UserAccountEmail.cs](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountEmail.cs)),
`UserAccountRole` ([UserAccountRole.cs](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountRole.cs))
e `UserAccountCredential` ([UserAccountCredential.cs](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccountCredential.cs))
**não têm `Id` nem chave**. Uma coleção de entidades no EF Core exige chave (própria, owned com chave explícita,
ou composta). Do jeito atual o EF não consegue rastrear identidade → update/delete de um email/role individual é
frágil ou impossível. Este é o "objeto que não seria gravado no banco" — o defeito central.

### B2. Filhos sem `RealmId` estrutural
ADR-015 §2.3: **toda tabela realm-scoped carrega `RealmId`**, e o isolamento deve ser **verificável no schema**.
Os filhos não têm `RealmId`, então as tabelas de email/role/credencial não nascem realm-scoped, e índices
compostos por realm (abaixo) ficam impossíveis. Apoiar-se só na FK para a conta **não** satisfaz a invariante.

### B3. Unicidade e índices não projetados
O modelo carrega `NormalizedUsername`/`NormalizedAddress`/`NormalizedName`, mas **nada expressa as restrições que
o banco precisa impor**:
- `unique (RealmId, SubjectId)` na conta;
- `unique (RealmId, NormalizedUsername)`;
- unicidade de email por realm quando `AllowDuplicateEmail = false` — **exige** `RealmId` + endereço normalizado
  **na linha do email** (índice único, possivelmente filtrado/parcial).

Unicidade de email cross-account **não** é invariante de agregado (precisa olhar outras contas) → tem que ser
**índice no banco** + checagem no command. O modelo atual não foi desenhado para suportar esse índice.

### B4. Sem token de concorrência
`UserAccountCredential` muta contadores de lockout a cada falha de login. Logins concorrentes correm → contagem de
falha e troca de email primário sofrem *lost update*. Para entidade de autenticação isso é correção, não
preferência. Falta um **concurrency token** (`xmin` no PostgreSQL / `rowversion`).

### B5. Owned vs tabela própria não decidido; convenções do RoyalCode ignoradas
- Não há decisão explícita: `UserAccountCredential` é 1:1 *owned*? Emails/roles são *owned collection* (com chave) ou
  entidades próprias (`Entity<long>` + FK)? Isso muda o schema e precisa ser decidido **antes** de codar.
- `RoyalCode.Entities` já oferece `IActiveState` (`IsActive`), `ISoftDeletable` (`IsDeleted`), `IHasGuid`,
  `IHasCode` — o design rolou um enum `AccountStatus` próprio e ignorou essas convenções. `AccountStatus`
  (Active/Inactive/Blocked) pode ser legítimo (mais rico que `IsActive`), mas a decisão tem que ser **consciente**
  frente às convenções, e a persistência do enum (string vs int) precisa ser definida (int é frágil a reordenação).

### B6. Coleções encapsuladas sem configuração de persistência
`List<>` privado exposto como `IReadOnlyCollection<>` exige config de backing-field no EF; combinado com a falta de
chave (B1), o mapeamento da coleção é inviável como está.

---

## 3. Defeitos — code smells explicitamente rejeitados

### S1. Utilitários `static` no agregado — remover **todos**
[UserAccount.cs:599-614](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs#L599):
`Normalize`, `NormalizeRequired`, `NormalizeOptional`. Além de smell, `NormalizeRequired` e `NormalizeOptional`
são **idênticos** (duplicação pura). Normalização não é trabalho do agregado.

### S2. Fábricas de `Problem` `static` — pedágio inútil
[UserAccount.cs:616-629](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs#L616):
`InvalidParameter`/`InvalidState`/`NotAllowed` apenas reencaminham para `Problems.*`. Indireção sem valor; os
problemas devem nascer na camada de validação (feature/command), não montados à mão dentro da entidade.

### S3. `throw` em fluxo esperado
[UserAccount.cs:127](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs#L127) e
[:371-372](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs#L371)
(`ArgumentNullException.ThrowIfNull`). Na filosofia SmartProblems o fluxo esperado retorna `Result`/`Problems`. E
se a entrada já vem validada do command, os null-guards são desnecessários. Remover.

### S4. Validação de entrada dentro do agregado
[UserAccount.cs:117-168](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs#L117)
(`Create` checando "RealmId is required", "Username is required", etc.) e
[:513-572](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs#L513) (`ValidatePassword`
inline, ~60 linhas). Validação de entrada/complexidade é da camada de feature (SmartValidations). O agregado guarda
**invariantes**, não valida string de entrada.

---

## 4. Defeitos — fronteira de domínio / DDD

### D1. `AddEmail` faz coisa demais
[UserAccount.cs:235-271](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/UserAccount.cs#L235): normaliza,
valida, decide primário e constrói. Deveria existir **`UserAccountEmail.Create(...)` (factory)** que produz um VO de
email **já válido**, e `AddEmail(email)` apenas aplica **invariantes locais** (primário único, dedup intra-conta) e
adiciona à coleção.

### D2. Email fictício no lugar errado
Política de fictício é do **realm** (`UserAccountsRealmOptions`). Se o realm usa fictício, o email fictício deve ser
criado **no `Create` da conta** (a conta nasce com primário fictício quando a política manda), não como flag crua em
`AddEmail`. Hoje `AddEmail(isFictitious: …)` aceita o flag sem consultar `AllowFictitiousEmail`.

### D3. Enforcement de política desigual
`Create` aplica a política de `SubjectId`, mas email/duplicidade/fictício são adiados/implícitos. Com o redesenho, a
**validação de entrada e política vai para a feature/command** (SmartValidations + options); o agregado fica só com
invariantes verdadeiras (imutabilidade de `SubjectId`, primário único, dedup intra-conta).

### D4. `PasswordPolicy` precisa ser extraído
A complexidade de senha (S4) deve virar um **`PasswordPolicy` reusável** (stateless) no módulo, usado tanto pela
feature de criação/troca quanto pela UI (que precisa validar **antes** de submeter). Hoje está presa no agregado.

### D5. Ruídos menores
- `LocalAuthenticationResult.DisplayName`
  ([LocalAuthenticationResult.cs:35](../../../RoyalIdentity.UserAccounts/Features/Accounts/Domain/LocalAuthenticationResult.cs#L35)):
  resultado de autenticação não precisa de display name (claims saem pelo claims provider). Sobre-modelagem.
- `UserAccountEmail.MarkVerified` é `internal` e **morto** nesta fase (sem fluxo de verificação). Código reservado
  sem uso — remover ou adiar com o fluxo que o usa.

---

## 5. O que está bom (não compensa os bloqueantes)
- Encapsulamento de coleções e mutação só pelo agregado.
- Tempo injetado (`DateTimeOffset` por parâmetro) — bom para teste.
- Eventos por `(RealmId, SubjectId)` via `AddEvent`, sem persistir (ADR-015 §2.9).
- Seam de hashing `IUserAccountPasswordHasher` (domínio livre de cripto).

Esses acertos devem ser **preservados** no redesenho.

---

## 6. Direção do redesenho (alvo)

**Modelar para EF primeiro:**
- `UserAccount : AggregateRoot<long>` — `Id` físico identity; `RealmId`, `SubjectId` (imutável), `NormalizedUsername`,
  `AccountStatus` (decidir persistência string), **concurrency token**.
- Filhos como **entidades EF de verdade**, cada um com `Id`, `RealmId` e FK para a conta:
  - `UserAccountEmail : Entity<long>` — `RealmId`, `NormalizedAddress`, flags; suporta `unique (RealmId, NormalizedAddress)`
    filtrado quando `!AllowDuplicateEmail`.
  - `UserAccountRole : Entity<long>` (ou owned com chave composta `(UserAccountId, NormalizedName)`).
  - `UserAccountCredential` — owned 1:1 (chave do dono) **ou** `Entity<long>`; decisão explícita.
- Índices/uniques projetados: `(RealmId, SubjectId)`, `(RealmId, NormalizedUsername)`, email por realm.
- Avaliar `IActiveState`/`ISoftDeletable`/`IHasGuid` do `RoyalCode.Entities` em vez de reinventar.

**Validação na borda da feature, não no agregado:**
- Objetos de command (`[Command]` partial class) com `HasProblems(out Problems?)` via `Rules.Set<>()`
  (SmartValidations) fazem a validação de entrada/normalização e checagens que precisam de repositório (ex.: email
  único por realm, username único).
- `PasswordPolicy` stateless reusável para complexidade.
- Factories de VO/entidade (`UserAccountEmail.Create(...) → Result`) validam a construção; o agregado recebe valores
  válidos.

**Remover:** todos os `static` utilitários, as fábricas de `Problem`, os `throw`, a normalização e a validação de
entrada dentro do agregado. Email fictício passa a ser decidido no `Create` conforme `UserAccountsRealmOptions`.

**Preservar:** encapsulamento, tempo injetado, eventos por `SubjectId`, seam de hashing.

---

## 7. Itens de ação (checklist)

Persistência (bloqueante):
- [ ] Dar `Id` + `RealmId` + FK aos filhos (email/role/credencial) e decidir owned vs tabela própria.
- [ ] Projetar uniques/índices: `(RealmId, SubjectId)`, `(RealmId, NormalizedUsername)`, email por realm.
- [ ] Adicionar token de concorrência ao agregado/credencial.
- [ ] Definir persistência de `AccountStatus` (string) e avaliar `IActiveState`/`ISoftDeletable`/`IHasGuid`.

Code smells (bloqueante):
- [ ] Remover `Normalize`/`NormalizeRequired`/`NormalizeOptional` `static`.
- [ ] Remover fábricas `static` de `Problem` (`InvalidParameter`/`InvalidState`/`NotAllowed`).
- [ ] Remover `throw`/`ThrowIfNull` de fluxo esperado.
- [ ] Tirar a validação de entrada e complexidade de senha do agregado.

DDD / features:
- [ ] `UserAccountEmail.Create(...)` factory; `AddEmail` só aplica invariantes locais.
- [ ] Email fictício criado no `Create` conforme política do realm.
- [ ] Extrair `PasswordPolicy` reusável.
- [ ] Mover validação de entrada para os commands com SmartValidations + SmartProblems.
- [ ] Remover `LocalAuthenticationResult.DisplayName` e `MarkVerified` morto (ou adiar com o fluxo).

Backlog (anotado, não agora):
- [ ] Projeto `RoyalIdentity.Security` (cripto/hash/`SubjectId`) compartilhável — ver `backlog-001.md`.

---

## 8. Próximos passos
1. Reabrir a Fase 5 (status do plano revertido para **Reprovada/redesign**).
2. Confirmar a direção do §6 (principalmente: filhos como entidades próprias vs owned; `AccountStatus` vs `IActiveState`).
3. Refazer o domínio modelado para EF + mover validação para as features; manter os acertos do §5.
4. Só então seguir para a Fase 6 (propriedades por escopo) e Fase 7 (persistência).

---

## 9. Adendo — decisões de redesenho validadas (2026-06-18)

Reconciliação com as decisões já fechadas no plano e na ADR-015.

### 9.1 A implementação ignorou o "Modelo relacional mínimo" do próprio plano
**Agravante decisivo.** O plano (§"Modelo relacional mínimo", linhas ~305-438) **já especificava** o modelo EF
correto e a Fase 5 não o seguiu:
- `UserAccount`: `Id bigint identity`, `RealmId`, `unique (RealmId, SubjectId)`, `unique (RealmId, NormalizedUsername)`.
- `UserAccountEmail`: **tabela própria** com `Id bigint identity`, `RealmId`, `UserAccountId` FK,
  `unique (RealmId, UserAccountId, NormalizedAddress)`, `unique (RealmId, UserAccountId) where IsPrimary`.
- `UserAccountRole`: **tabela própria** com `Id`, `RealmId`, `UserAccountId` FK, `unique (RealmId, UserAccountId, NormalizedName)`.
- `UserAccountCredential`: PK = `UserAccountId` (1:1), `RealmId` — owned/mesma-tabela aceito.

Ou seja, os defeitos B1/B2/B3 não são "o reviewer querendo a mais" — são **requisitos já escritos** que o código
não cumpriu. **Q1 fica resolvida pelo plano:** emails/roles = entidades próprias (`Entity<long>` + `RealmId` + FK);
credencial = owned 1:1 (chave = `UserAccountId`). O redesenho é **implementar o modelo relacional do plano**.

### 9.2 Unicidade de email — alinhar com o plano
Correção ao §B3 desta review: o plano **não** cria unique global `(RealmId, NormalizedAddress)` (porque
`AllowDuplicateEmail` é por realm). A unicidade cross-account quando `AllowDuplicateEmail = false` é garantida pelo
**caso de uso/repositório em transação**, com reforço físico opcional (tabela de guard ou índice parcial) — decisão
**ainda aberta na ADR-015**. As uniques **por conta** (`(RealmId, UserAccountId, NormalizedAddress)` e primário
único) continuam valendo.

### 9.3 `AccountStatus` — separar eixos (decisão do usuário, reabre decisão fechada)
O plano (§linha 268) e a **ADR-015 §2.7** fecharam `AccountStatus { Active, Inactive, Blocked }` num campo único.
O usuário aponta — com razão — que **ativo/inativo (ciclo de vida) e bloqueado (segurança/admin) são eixos
diferentes**; juntar num campo confunde. Isso é coerente com a própria lista de eventos do plano, que já separa
`AccountActivated`/`AccountDeactivated` de `AccountBlocked`/`AccountUnblocked`.
- **Decisão:** separar — eixo de habilitação (`Active`/`Inactive`, ou `IsActive`) **+** estado de bloqueio
  separado (`IsBlocked` + motivo + timestamp), ortogonais.
- **Impacto:** **emendar ADR-015 §2.7** e o plano (§linha 268 + `UserAccount` no modelo relacional). O autenticador
  passa a checar os dois eixos (inativo → `Inactive`; bloqueado **ou** lockout → `Blocked`).

### 9.4 Token de concorrência — adicionar (lacuna do plano também)
Nem o plano nem a implementação previram **concurrency token**. Contadores de lockout e troca de primário correm
sob concorrência. Adicionar ao `UserAccount`/credencial (ex.: `xmin` no PostgreSQL / `rowversion`) e registrar no
modelo relacional do plano.

### 9.5 Resumo das decisões para o redo
1. Implementar **exatamente** o modelo relacional do plano (entidades com `Id`/`RealmId`/FK/uniques).
2. Credencial owned 1:1 (PK = `UserAccountId`).
3. **Separar** habilitação vs bloqueio (emendar ADR-015 §2.7 + plano).
4. Adicionar **token de concorrência** (emendar o modelo relacional do plano).
5. Validação/normalização nas **features/commands** (SmartValidations + SmartProblems); agregado recebe válido.
6. Factories de VO/entidade; `PasswordPolicy` reusável; fictício no `Create` por política; remover smells do §3.
7. Preservar os acertos do §5.
