# Consenso das Avaliações — Redesign de Usuários/Sessão (con2)

> Consenso entre as **duas avaliações comparativas** das análises m1 e m2:
> [an-user-m1-av1.md](an-user-m1-av1.md) e [an-user-m1-av2.md](an-user-m1-av2.md)
> (que por sua vez comparam [an-users-m1.md](an-users-m1.md) e [an-users-m2.md](an-users-m2.md)).
>
> **Método:** leitura das duas avaliações e das análises/código que elas citam. **Não** li o
> `an-user-con1.md`, para manter este consenso independente. Nenhum build/teste executado (entrega documental).
>
> **Objetivo:** destilar uma direção única e acionável — onde av1 e av2 concordam (a maior parte) e
> como resolver os poucos pontos em que divergem.

---

## 1. Veredito de consenso

As duas avaliações chegaram, independentemente, à **mesma conclusão estrutural**: usar o **m2 como
espinha dorsal** (diagnóstico operacional preciso, governança por ADR, contenção de escopo) e
**enriquecer com os ativos duráveis do m1** (modelo mais completo, `UserClaim`, `SecurityStamp`,
preparação de futuro, e — sobretudo — a matriz de testes/critérios de aceite). Não há vencedor
absoluto entre m1 e m2; e as duas avaliações estão de acordo sobre isso. Restam **três divergências
reais entre av1 e av2** (nome da entidade, profundidade do modelo de credencial, fachada da UI),
resolvidas na §4.

---

## 2. Onde av1 e av2 concordam (consenso forte)

### 2.1 Diagnóstico
- m1 e m2 convergem no núcleo; a divergência entre elas é de **escopo e processo**, não de direção.
- `IdentityUser`/`DefaultIdentityUser` têm comportamento demais; `UserDetails`×`IdentityUser` são dois modelos paralelos; `IUserStore`/`IUserDetailsStore` são a mesma classe (separação não compra isolamento).
- `IdentitySession` guardar `IdentityUser` é ruim; `GetCurrentSessionAsync` no store acoplado a `HttpContext` é *smell*.
- `sub` colado a `Username`; `LoginPageService` conhece a máquina de estados do login; revalidating provider desalinhado.
- Preservar: realm isolation, cookie por realm, `sub/sid/auth_time/amr/idp/name`, lockout, active check, registro de client na sessão, logout SSO (front/back-channel).

### 2.2 Forças de cada análise (ambas as avaliações concordam)
- **m1 é melhor** em: modelo de futuro (`UserCredential`/`ExternalIdentity`/`SecurityStamp`/device sessions/`UserClaim`), ideia de fachada de interação, e a trilha de execução (fases, testes, critérios de aceite).
- **m2 é melhor** em: inventário/fluxos precisos, a **inconsistência tripla do "ativo"**, o **lockout split-brain**, a **sessão criada dentro da verificação de senha**, a **tensão com a ADR-005**, os **pontos de decisão**, e o **revalidating provider** (verificado como quebrado → remover).

### 2.3 Direção de modelagem (consenso)
- **Um único registro persistido de usuário** que devolve **dados**, não objeto rico com serviços.
- **Sessão como dado puro serializável** (sem `IdentityUser`), com clients **deduplicados**; store **sem HttpContext**; "sessão atual" num serviço de aplicação.
- **Autenticação/credencial/lockout fora da entidade**; **lockout como política única**; **não** criar sessão como efeito colateral da verificação de senha.
- **Factory própria** para o `ClaimsPrincipal`.
- **Resultado único** de autenticação (funde `CredentialsValidationResult` + `ValidateCredentialsResult`).
- **`SubjectId` imutável**, separado de `Username`.

### 2.4 Decisões já alinhadas nas duas avaliações
- **ADR nova (ADR-013)** supersedendo/refinando a ADR-005: extensibilidade por **composição**, não herança; registrar `SubjectId` imutável. **Antes** de implementar.
- **`SubjectId` agora** (não há dado persistido a migrar → timing ótimo; seeds com IDs determinísticos).
- **Remover `IdentityUser`** como modelo final (adapter temporário aceitável, com fase de remoção).
- **Store único** de usuário (`IUserAccountStore`) substituindo `IUserStore`+`IUserDetailsStore`.
- **`UserClaim`** persistível, convertendo para `Claim` na borda (não persistir `System.Security.Claims.Claim` cru).
- **`SecurityStamp`**: incluir o **campo** já, **ligar a regra** (invalidação de cookie/sessão) depois.
- **Sessão ausente ⇒ inválida** para fluxos autenticados; separar "conta ativa" de "sessão válida" (APIs distintas).
- **Revalidating provider:** remover (confirmando antes que nenhuma área interativa autenticada dependa dele); refazer correto se houver.
- **Importar a matriz de testes/critérios de aceite do m1** para o plano que nascer depois da ADR.

> Tudo em §2.4 está acordado pelas duas avaliações — entra como decisão consolidada (§5) sem ressalva.

---

## 3. O que cada avaliação acrescentou

- **av1** acrescentou: debate explícito de **nomeação** (`UserAccount` vs `User`) com prós/contras; o **modelo intermediário de credencial** (`PasswordCredential` dentro de `UserAccount`, sem store extra); defesa forte do **`IAccountInteractionService`**; conjunto D1–D7 de decisões; matriz de impacto (alto/médio/baixo) e 10 "pontos de cuidado".
- **av2** acrescentou: avaliação ponto-a-ponto em 14 eixos com placar; verificação dos fatos do revalidating provider (registrado e **lança exceção**; account=SSR × não-account=InteractiveServer); raciocínio de **cache evictável (TTL)** para a semântica de sessão nula; ênfase em **anti-YAGNI** (construir costuras, não o futuro inteiro).

As duas se complementam: av1 puxa para completude e nomeação; av2 puxa para verificação e contenção.

---

## 4. Divergências entre av1 e av2 — resolução

### D-A. Nome da entidade principal: `UserAccount` (av1) × `User` (av2)
- **av1:** `UserAccount` — evita colisão com `HttpContext.User`/`ClaimsPrincipal`/`Subject`/`Principal`, que aparecem o tempo todo num authorization server; `User` aceitável se o time preferir nomes curtos.
- **av2:** usou `User` (herdado do m2), sem debater a fundo.
- **Resolução: `UserAccount`.** A argumentação da av1 é mais forte e a av2 não a contesta — apenas
  adotou o default do m2. Num servidor OIDC, `UserAccount` desfaz ambiguidade entre **conta persistida**
  e **sujeito autenticado**. (`User` fica como alternativa aceitável se o time priorizar concisão.)
  **Aqui a av1 está mais certa.**

### D-B. Profundidade do modelo de credencial
- **av1:** opção intermediária — submodelo `PasswordCredential`/`LocalCredential` **dentro** de `UserAccount`, **sem** store separado.
- **av2:** manter `PasswordHash`/contadores/lockout dentro de `User` por ora, com "costuras" para o futuro.
- **Resolução: adotar o submodelo `PasswordCredential` dentro de `UserAccount` (av1).** É um meio-termo
  superior: separa credencial de perfil a custo quase nulo (sem store extra) e prepara
  externo/MFA/passwordless/history sem over-design. A "costura" que a av2 pedia **é exatamente** esse
  submodelo. **Aqui a av1 refina melhor; convergem na prática.**

### D-C. Fachada da UI: `IAccountInteractionService` (av1) × managers + enum de desfecho (av2)
- **av1:** criar `IAccountInteractionService` (construir/processar login, construir/processar consent, iniciar/confirmar logout) — melhor fronteira para a UI; resolve diretamente a queixa.
- **av2:** manter `ISignInManager`/`ISignOutManager` e mover a máquina de estados para um método que devolve **enum de desfecho**; receio de **god-service**.
- **Resolução: adotar a fachada de interação (av1), com a salvaguarda da av2.** A queixa central da
  tarefa ("as telas conhecem demais") é melhor resolvida por uma camada de **interação por fluxo** que a
  UI consome — devolvendo view models e **flow-results (enum)**. O risco de god-object levantado pela av2
  é mitigado por uma regra de desenho: **a fachada é só orquestração**, delegando a serviços de domínio
  focados (`IUserAuthenticationService`/`IUserSessionService`/`IConsentService`) — que é o que a própria
  av1 também prevê. Fica **em aberto** (§9) se será **uma** `IAccountInteractionService` ou **uma por
  fluxo** (login/consent/logout); decidir no plano conforme o tamanho. **Síntese das duas posições.**

---

## 5. Decisões consolidadas

| # | Decisão | Origem | Status |
|---|---|---|---|
| C1 | **ADR-013** supersede/refina a ADR-005: composição sobre herança; `SubjectId` imutável. Escrever **antes** de codar. | consenso | firme |
| C2 | Entidade persistida única **`UserAccount`** (`User` aceitável). | av1 (resolvido §4) | firme |
| C3 | **`SubjectId` imutável** ≠ `Username`, **agora**; seeds com IDs determinísticos. | consenso | firme |
| C4 | Dentro de `UserAccount`: perfil, `Roles`, **`UserClaim`**, **`UserSecurityState`** (com **`SecurityStamp`**), **`PasswordCredential`** (submodelo, sem store extra). | consenso + §4 | firme |
| C5 | **`IUserAccountStore`** único substitui `IUserStore`+`IUserDetailsStore`; devolve dados. | consenso | firme |
| C6 | **`UserSession`** persistível (sem `IdentityUser`), clients deduplicados; **store puro** (sem HttpContext). | consenso | firme |
| C7 | **`IUserSessionService`** (aplicação) para current session, validação, start/end, record client. | consenso | firme |
| C8 | **`IUserAuthenticationService`/`IUserAuthenticator`** + **`LockoutPolicy`** única; **não** criar sessão na verificação de senha. | consenso | firme |
| C9 | **`ISubjectPrincipalFactory`** monta o principal (claims obrigatórias); reusar regras de claims do id_token. | consenso | firme |
| C10 | **Camada de interação para a UI** (fachada por fluxo), só orquestração, devolvendo view model + flow-result. | §4 D-C | firme (forma em aberto §9) |
| C11 | Separar **"conta ativa"** de **"sessão válida"**; **sessão ausente ⇒ inválida** quando há `sid`. | consenso | firme |
| C12 | **Remover** `IdentityRevalidatingAuthenticationStateProvider` (confirmando uso); refazer correto se necessário. | consenso | firme |
| C13 | **`SecurityStamp`**: campo agora; validação (invalidar cookie/sessão) em tarefa posterior. | consenso | firme |
| C14 | Funde os dois structs de resultado num **resultado único** de autenticação. | consenso | firme |
| C15 | Remover/adaptar com obsolescência: `IdentityUser`, `UserDetails`, `ValidateCredentialsResult`, `CredentialsValidationResult`. | consenso | firme |
| C16 | **Importar a matriz de testes + critérios de aceite do m1** para o plano (pós-ADR). | consenso | firme |

---

## 6. Direção arquitetural consolidada

```
Telas (Razor)
   └─ Camada de interação (C10): view models + flow-results (enum); só orquestração
        ├─ IUserAuthenticationService (C8) ──► LockoutPolicy (C8)
        ├─ IUserSessionService (C7) ──► IUserSessionStore puro (C6)
        ├─ ISubjectPrincipalFactory (C9)
        └─ IConsentService (existente)
   Storage (dados puros, realm-scoped)
        ├─ IUserAccountStore (C5) → UserAccount { perfil, Roles, UserClaim, UserSecurityState{SecurityStamp}, PasswordCredential } (C2/C4)
        └─ IUserSessionStore → UserSession { SubjectId, RealmId, idp, amr, clients[], IsActive } (C6)
```

Regras de fronteira: storage devolve dado; comportamento vive em serviços; a UI só vê a camada de
interação. `IProfileService` permanece como contrato OIDC, consumindo `IUserAccountStore`/`IUserSessionService`.

---

## 7. Impacto

- **Alto:** separação `SubjectId`/`Username`; remoção final de `IdentityUser`; troca de `IUserStore`/`IUserDetailsStore` por `IUserAccountStore`.
- **Médio:** revisão de sessão; extração de auth/lockout/principal-factory; mudanças em `ProfileService`, cookie validation, `DefaultCodeFactory` e logout; redução das telas; introdução de `UserClaim`/`PasswordCredential`.
- **Baixo:** ADR-013; introdução do campo `SecurityStamp` (sem regra ainda); remoção/correção do revalidating provider.

---

## 8. Sequência recomendada

1. **ADR-013** (C1) — fixa composição-sobre-herança e `SubjectId` imutável.
2. **Modelo de dados** (C2/C3/C4) — `UserAccount` + `SubjectId` + `UserClaim` + `UserSecurityState{SecurityStamp}` + `PasswordCredential`.
3. **Store** (C5) — `IUserAccountStore` (in-memory primeiro), com adapters temporários se preciso.
4. **Serviços** (C8/C9) — autenticação + `LockoutPolicy` + `ISubjectPrincipalFactory`; remover criação de sessão da verificação de senha.
5. **Sessão + "ativo"** (C6/C7/C11) — `UserSession` puro, `IUserSessionService`, semântica unificada.
6. **UI** (C10) — camada de interação; telas viram cola.
7. **Limpeza** (C12/C15) — remover revalidating provider e tipos antigos.

Os **critérios de aceite e testes de caracterização do m1 (C16)** governam o "pronto" de cada etapa,
e devem ser escritos **antes** de mexer em login/sessão.

---

## 9. Sub-decisões deixadas para o plano

1. **C10 — uma `IAccountInteractionService` única ou uma por fluxo** (login/consent/logout)? Decidir pelo tamanho real, mantendo a regra "só orquestração".
2. **C12 — confirmar** se alguma página `InteractiveServer` **autenticada** depende do revalidating provider antes de remover (se sim, refazer com realm capturado + `IUserSessionService`).
3. **Nome final da entidade** (C2): `UserAccount` recomendado; `User` aceitável — bater o martelo na ADR-013.
4. **Escopo de unicidade do `SubjectId`** (por realm vs global/GUID) — GUID resolve ambos; confirmar.

---

## 10. Pontos de cuidado (herdados das avaliações)

1. Testes de caracterização **antes** de mudar login/sessão.
2. Consentimentos passam a ser por **`SubjectId`**, não username.
3. Seeds com **IDs determinísticos** (testes estáveis).
4. **Sem big bang** — adapters temporários são aceitáveis, mas com fase de remoção marcada.
5. Decidir a **semântica de sessão nula** antes de tocar o `ProfileService`.
6. Manter **mensagens genéricas** de erro de login (anti-enumeração).
7. Preservar `sub/sid/auth_time/amr/idp/name` no cookie/principal.
8. Manter **registro de client na sessão** na emissão do code e o **front/back-channel logout** por clients da sessão.

---

## 11. Conclusão

As duas avaliações concordam no essencial e divergem em apenas três pontos — todos resolvidos por
síntese (§4): **`UserAccount`** como nome, **`PasswordCredential` como submodelo** dentro dele, e uma
**camada de interação para a UI** que é só orquestração sobre serviços de domínio focados. O consenso é
claro: **m2 guia as decisões imediatas e a governança (ADR-013, `SubjectId`, semântica de sessão,
remoção do provider quebrado); o m1 fornece o modelo mais preparado (`UserClaim`, `SecurityStamp`,
credencial/sessão extensíveis) e a rede de testes/critérios.** A direção arquitetural (§6) e a sequência
(§8) estão prontas para virar uma ADR seguida de plano; nenhuma decisão firme (§5) está em disputa entre
as avaliações.
