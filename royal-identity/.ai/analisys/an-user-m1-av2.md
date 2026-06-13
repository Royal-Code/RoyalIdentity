# Avaliação Comparativa — `an-users-m1` × `an-users-m2` (av2)

> Avaliação independente das duas análises do redesign de Usuários/Sessão:
> [an-users-m1.md](an-users-m1.md) e [an-users-m2.md](an-users-m2.md).
>
> **Método:** leitura estática das duas análises e do código-fonte que ambas citam.
> Nenhum build/teste executado (entrega só de documentação). **Não** li o `an-user-m1-av1.md`,
> para manter esta avaliação independente.
>
> Para cada eixo de divergência: **opções**, **prós/contras**, **impacto** e **recomendação
> justificada**. Onde as duas concordam, registro o consenso (sinal forte) e só avalio nuances.

---

## 1. Caráter de cada documento

| | **m1** | **m2** |
|---|---|---|
| Postura | **Maximalista** — redesenha o domínio inteiro e modela o futuro (credenciais, identidade externa, security stamp, sessões por dispositivo, registro/recuperação) | **Minimalista/conservadora** — colapsa para um registro de dados + poucos serviços; adiciona complexidade só quando necessária |
| Entrega | Quase um **plano**: 7 fases, testes de caracterização, 14 critérios de aceite, seção de nomeação | **Análise pura** + recomendações decisórias (§10/§12) + sequência curta; deixa o plano para depois da ADR |
| Profundidade do "estado atual" | Muito alta (20 regras a preservar, 10 problemas) | Alta e enxuta (7 invariantes, 12 *smells*, tabelas de inventário) |
| Verificação de fatos | Por leitura; alguns pontos como suspeita ("parece desalinhado") | Verifica e confirma (ex.: revalidating provider **lança exceção**; account=SSR vs não-account=InteractiveServer) |
| Governança | Não pede ADR; vai direto ao plano de fases | Pede **ADR-013 supersedendo a ADR-005 antes** de implementar; expõe a tensão |
| Novos tipos propostos | ~10 (UserAccount, UserSecurityState, UserCredential, ExternalIdentity, UserSession, UserSessionClient, AuthenticatedSubject, UserClaim, +serviços) | ~4 (User, IUserAuthenticator, LockoutPolicy, IUserClaimsFactory) + fachadas existentes |

**Leitura de uma linha:** o m1 é um **blueprint de produto de identidade completo**; o m2 é um
**refactor cirúrgico do que dói hoje**, com disciplina de ADR. Não são contraditórios na raiz —
divergem em **quanto futuro construir agora** e em **quanta cerimônia de processo aplicar**.

---

## 2. Pontos em comum (consenso — sinal forte)

As duas análises foram feitas de forma independente e **convergiram** no diagnóstico central e em
boa parte da direção. Onde ambas concordam, a confiança é alta:

1. `IdentityUser`/`DefaultIdentityUser` carrega comportamento demais (dado + credencial + lockout + criação de sessão + montagem de principal). **Quebrar.**
2. `UserDetails` × `IdentityUser` é duplicação; `IUserStore`/`IUserDetailsStore` são a **mesma classe** → **unificar num store de usuário só** (dados).
3. `IdentitySession` guarda um `IdentityUser` vivo → deve guardar **`SubjectId`** e ser **serializável**.
4. `UserSessionStore.GetCurrentSessionAsync` depende de `HttpContext` → *smell* de storage; "sessão atual" vai para a camada de aplicação/web.
5. **Separar `SubjectId` de `Username`** — ambas elegem como a mudança de maior impacto, com a **mesma** justificativa (OIDC exige `sub` estável; `AccountOptions` já tem username/email mutáveis).
6. Extrair **credencial/lockout/autenticação** para serviços (m1: `IUserCredentialService`+`IUserAuthenticationService`; m2: `IUserAuthenticator`+`LockoutPolicy`).
7. Extrair **factory de principal/claims** (m1: `ISubjectPrincipalFactory`; m2: `IUserClaimsFactory`).
8. **Afinar as telas** / tirar a máquina de estados do login de dentro do `LoginPageService`.
9. Unificar a checagem de "ativo" e **preservar** a validação cookie-contra-sessão-ativa.
10. O `IdentityRevalidatingAuthenticationStateProvider` está **desalinhado** (resolve `IUserStore` por DI em vez de `storage.GetUserStore(realm)`).
11. Consolidar os **dois** structs de resultado (`CredentialsValidationResult` + `ValidateCredentialsResult`).
12. **Preservar** as regras de negócio: realm isolation, lockout, registro de client na sessão, front/back-channel logout.

> Conclusão da §2: o **núcleo do redesign não está em disputa**. A discussão real é de escopo,
> faseamento e processo (§4–§5).

---

## 3. O que cada uma tem de exclusivo

### Só no m1 (riqueza que o m2 não tem)
- **20 regras a preservar** + **14 critérios de aceite** + lista extensa de testes (unidade/integração/UI) → excelente rede de segurança para refatorar.
- **Plano de 7 fases** com testes de caracterização **antes** de mexer no comportamento.
- **Modelagem de futuro**: `UserSecurityState` (com **SecurityStamp**, `LockoutEndAt`, `MustChangePassword`), `UserCredential` (senha como credencial tipada), `ExternalIdentity` (login externo), `UserSessionClient` com timestamps, campos de **sessão por dispositivo** (UserAgent/IP/Device), ganchos para registro/recuperação/verificação.
- **`UserClaim`** como modelo de persistência (em vez de `System.Security.Claims.Claim` cru).
- Fachada única `IAccountInteractionService` que poderia **absorver** `ISessionContextService`/`ISignInManager`/`LoginPageService`.
- Seção de **nomeação** (nomes a preferir e a evitar).

### Só no m2 (rigor que o m1 não tem)
- **Fatos verificados**, não suspeitados: o revalidating provider **está registrado** e **lança exceção** (o `IUserStore` não está em DI nenhum); account pages são **SSR**, não-account são **InteractiveServer** — o que muda a decisão sobre o provider.
- **Honestidade sobre a tensão com a ADR-005** (§9 do m2): colapsar `IdentityUser` contraria a *forma* (herança) da ADR vigente → recomenda **nova ADR-013** que supersede. O m1 propõe remover `IdentityUser` **sem** confrontar essa decisão aceita.
- **Inconsistência tripla do "ativo"** explicitada: `ProfileService` (lenient com sessão nula) × `ValidateUserSessionAsync` (estrito) × revalidating provider (só conta). O m1 manda "preservar `IsActiveAsync`" sem notar que os três caminhos **divergem hoje**.
- **Semântica de sessão nula** com raciocínio de cache evictável (TTL → `null` deve falhar).
- Postura **decisória** (§10/§12: veredito + sequência) — mais fácil de transformar em ação imediata.

---

## 4. Divergências — avaliação ponto a ponto

Formato: **Opções** → **Prós/Contras** → **Impacto** → **Recomendação**.

### E1. Ambição do redesign (maximalista × minimalista)
- **Opção A (m1):** introduzir já o modelo completo (credenciais, identidade externa, security stamp, device sessions, registro).
- **Opção B (m2):** colapsar para `User` + poucos serviços; adicionar o resto sob demanda.
- **Prós A:** prepara MFA/externo/admin de uma vez; menos retrabalho conceitual depois.
- **Contras A:** superfície grande e **especulativa (YAGNI)**; migração maior; mais a errar; contraria a orientação do `structure.md` ("Areas Under Active Redesign — não estabilizar/super-construir").
- **Prós B:** muda só o que dói; baixo risco; rápido de validar; aderente ao "não super-construir".
- **Contras B:** revisita o modelo quando externo/MFA chegarem de fato.
- **Impacto:** A = alto/estrutural; B = médio.
- **Recomendação:** **B como base, com as *costuras* de A.** Adotar o núcleo enxuto do m2, mas
  desenhar os pontos de extensão (autenticador plugável, claims/roles no `User`, sessão com campos
  opcionais) de modo que `ExternalIdentity`/`SecurityStamp`/device-sessions **caibam depois sem
  reescrever** — sem implementá-los agora. Justificativa: o produto está em redesign ativo de
  várias áreas (resources acabou de fechar); cravar um modelo de identidade completo agora é risco
  desproporcional ao valor imediato.

### E2. Migração de `SubjectId` (sub=username × ids novos)
- **Opção A (m1):** `SubjectId = Username` inicialmente, por compatibilidade.
- **Opção B (m2):** gerar `SubjectId` novo (GUID/CryptoRandom), seed com ids **determinísticos** ≠ username.
- **Prós A:** zero mudança em testes que assertam `sub`; transição suave se houvesse dados reais.
- **Contras A:** **rebaixa o ganho** — mantém o acoplamento temporariamente e adia uma segunda migração (de `sub=username` para id real); e **impede testar de fato** a invariante "trocar username mantém `sub`" (que o próprio m1 lista como teste).
- **Prós B:** entrega o desacoplamento **já**; permite o teste de estabilidade de `sub`; evita migração dupla.
- **Contras B:** exige atualizar seed + asserts de `sub` nos testes (uma vez).
- **Impacto:** decisivo é o **estado atual**: o storage é **só in-memory/seed; não há dado persistido**. A "compatibilidade" que A protege **não existe** ainda.
- **Recomendação:** **B agora.** Como não há dado real, este é o momento mais barato de fazer o
  certo; A só faria sentido como estratégia de migração **quando o backend SQL existir** (aí o
  `sub=username` de transição volta a ser útil para dados legados). Guardar A para o futuro, aplicar B hoje.

### E3. Modelo de sessão serializável (consenso, com nuances)
- Ambas concordam: tirar `IdentityUser` da sessão, guardar `SubjectId`, dedup de clients.
- **Nuance m1:** `UserSessionClient` com `FirstSeenAt`/`LastSeenAt` + campos de device + `SecurityStamp` na sessão.
- **Recomendação:** adotar a **forma serializável** (consenso) com **dedup de clients** já. Campos de
  device/timestamps por client: **adiar** (E1) — deixar a coleção de clients extensível. `SecurityStamp` na sessão: ver E8.

### E4. Remover `GetCurrentSessionAsync` do store (consenso)
- Sem divergência real. m1 detalha `IUserSessionService.GetCurrentAsync(realm, principal)`; m2 fala de `ICurrentSession`/extensão web.
- **Recomendação:** adotar (consenso). Nome/forma: um serviço de aplicação que recebe `principal`/`sid`
  explicitamente e chama o store puro. **Impacto baixo**, alto ganho de pureza/teste.

### E5. Serviços de credencial/lockout/autenticação (consenso, naming)
- m1 separa `IUserCredentialService` (senha+lockout) de `IUserAuthenticationService` (caso de uso). m2 funde em `IUserAuthenticator` + `LockoutPolicy`.
- **Prós m1 (mais granular):** senha e orquestração separadas → encaixa externo/MFA melhor.
- **Prós m2 (menos peças):** menos indireção para o caso local atual.
- **Recomendação:** **`IUserAuthenticator` + `LockoutPolicy` (m2) como ponto de partida**, com a
  *interface* desenhada para que múltiplos autenticadores (local/externo/MFA) coexistam (a granularidade
  do m1 vira realidade quando o segundo método de auth existir). Lockout como **política única** (resolve o "split-brain" das 3 localizações).

### E6. Factory de principal/claims (consenso)
- m1: `ISubjectPrincipalFactory`. m2: `IUserClaimsFactory` reaproveitando regras do id_token.
- **Recomendação:** adotar (consenso). Preferir o ângulo do m2 de **reusar as regras de claims já
  existentes** para reduzir divergência entre principal de login e claims de token. Claims obrigatórias:
  `sub/name/auth_time/sid/idp/amr` (ambas concordam).

### E7. Persistência de claims: `UserClaim` × saco de `Claims` (divergência)
- **Opção A (m1):** modelo `UserClaim {Type,Value,ValueType}`, converte para `Claim` só na borda.
- **Opção B (m2):** manter `HashSet<Claim>`.
- **Prós A:** contrato de persistência limpo, igualdade controlada, pronto para SQL.
- **Contras A:** código de conversão na borda agora; mais churn.
- **Prós B:** menos mudança imediata.
- **Contras B:** persiste tipo de framework (contrato fraco p/ SQL), igualdade default de `Claim`.
- **Impacto:** médio; aparece de verdade no backend SQL.
- **Recomendação:** **A**, mas **junto da introdução do `User`** (não antes). Como o `User` é novo,
  já nasce com `UserClaim` — custo marginal baixo e evita reescrever o contrato quando o SQL chegar.
  Aqui o m1 está **mais certo** que o m2.

### E8. Security stamp (só m1)
- **Opção A (m1):** `SecurityStamp` em conta e sessão; invalida cookies/sessões após troca de senha, desativação, MFA.
- **Opção B (m2):** silente.
- **Prós A:** proteção padrão e valiosa; habilita "deslogar tudo após evento sensível".
- **Contras A:** mais um campo/regra de validação; só rende quando houver troca de senha/admin.
- **Impacto:** baixo para introduzir o **campo**; médio para a regra de validação.
- **Recomendação:** **reservar o campo agora, ativar a regra depois.** Incluir `SecurityStamp` no
  `User`/sessão (custo marginal nulo no modelo novo) e **só** ligar a validação quando existir troca de
  senha/desativação administrativa. Aqui o m1 **acrescenta valor real** que o m2 omitiu.

### E9. Identidade externa / device sessions / entidades de registro (só m1)
- **Opção A (m1):** modelar `ExternalIdentity`, device fields, credential/verification/password-history já.
- **Opção B (m2):** adiar; extensão por composição quando o fluxo existir.
- **Prós A:** a UI já lista providers externos; clients já têm `IdentityProviderRestrictions`; `idp`/`amr` já entram nos tokens.
- **Contras A:** modelar sem fluxo é **especulativo**; risco de modelar errado sem caso de uso real.
- **Recomendação:** **B (adiar), com costura (E1).** Não modelar `ExternalIdentity` agora, mas garantir
  que `User`/autenticador o acomodem. Exceção pontual: como `idp`/`amr` **já** circulam, vale documentar
  o ponto de extensão. O m1 está certo no *foresight*; o m2 está certo no *timing*.

### E10. Fachada da UI (serviço único × managers + enum de desfecho)
- **Opção A (m1):** `IAccountInteractionService` único absorvendo session-context/sign-in/login-page.
- **Opção B (m2):** manter `ISignInManager`/`ISignOutManager`; mover a máquina de estados para um método de manager que devolve **enum de desfecho**; telas viram cola.
- **Prós A:** uma fronteira só para a UI; telas quase desaparecem.
- **Contras A:** risco de **god-service**; refactor maior; mistura login+consent+logout num tipo.
- **Prós B:** mudança menor; reaproveita managers; responsabilidade por caso de uso.
- **Contras B:** continuam existindo duas fachadas + page services.
- **Impacto:** A = alto; B = médio.
- **Recomendação:** **B.** O objetivo declarado ("telas não devem conhecer `IdentityUser`/sessão/cookie")
  é atingido com B sem o risco de god-object. Manter login/consent/logout em fachadas separadas é mais
  coeso que uni-las. O ganho marginal de A não paga o risco.

### E11. Semântica de "ativo" / sessão nula (m2 mais profundo)
- **Opção A (m1):** preservar `IsActiveAsync` (conta+sessão) como está.
- **Opção B (m2):** separar "conta habilitada" (só `User.IsActive`) de "sessão válida" (existe **e** ativa; **ausente ⇒ inválida**), uma implementação cada.
- **Prós B:** corrige a inconsistência tripla atual; correto para cache evictável (TTL).
- **Contras B:** comportamento mais estrito (mudança sutil).
- **Recomendação:** **B.** O m1 "preserva" um comportamento que hoje é **inconsistente**; o m2 o
  conserta. Adotar a regra do m2 e documentar a semântica de sessão nula.

### E12. Revalidating provider (m2 verificou)
- **Opção A (m1):** "parece desalinhado"; pode ser pouco exercitado em SSR.
- **Opção B (m2):** **removê-lo agora** (está registrado e **lança exceção**; ignora realm); reintroduzir correto só quando houver área interativa autenticada.
- **Recomendação:** **B**, porque é **fato verificado**, não suspeita. Remover já (é código que quebra).
  Aqui o m2 é objetivamente mais preciso.

### E13. Governança de ADR (processo)
- **Opção A (m1):** não menciona ADR; segue para plano de 7 fases.
- **Opção B (m2):** **ADR-013 supersedendo a ADR-005 antes** de implementar.
- **Prós B:** a remoção do `IdentityUser` **contraria a forma** da ADR-005 (extensão por herança); decidir/registrar antes evita "implementar contra ADR vigente".
- **Contras B:** um passo de cerimônia a mais.
- **Recomendação:** **B.** É a postura correta de governança do repositório (ADRs são decisões aceitas).
  Sem isso, o plano do m1 começaria violando uma ADR sem registro.

### E14. Formato da entrega (análise pura × plano embutido)
- **Opção A (m1):** já traz fases/critérios/testes (quase um plano).
- **Opção B (m2):** análise + recomendações; plano fica para depois da ADR.
- **Prós A:** acionável de imediato; rede de testes pronta.
- **Contras A:** mistura análise com plano antes da decisão (E13); risco de comprometer rumo cedo demais.
- **Prós B:** separação limpa análise→ADR→plano.
- **Recomendação:** **B para a estrutura**, **reaproveitando os ativos do A** (lista de regras, testes
  de caracterização, critérios de aceite) **dentro do plano** que nascer depois da ADR. As fases e testes
  do m1 não se perdem — migram para o plano no momento certo.

---

## 5. Placar por eixo

| Eixo | Melhor | Observação |
|---|---|---|
| E1 Ambição | **m2** (base) + costuras do m1 | evitar YAGNI, manter extensível |
| E2 Migração SubjectId | **m2** | não há dado a migrar hoje |
| E3 Sessão serializável | empate (consenso) | adiar device/timestamps |
| E4 Remover current do store | empate (consenso) | — |
| E5 Serviços de auth/lockout | **m2** (forma) + granularidade do m1 depois | — |
| E6 Factory de claims | empate; ângulo do m2 (reuso) | — |
| E7 `UserClaim` persistido | **m1** | fazer junto do `User` |
| E8 Security stamp | **m1** (campo agora, regra depois) | — |
| E9 Externo/device/registro | **m2** (timing) + foresight do m1 | só costurar |
| E10 Fachada UI | **m2** | evita god-service |
| E11 Semântica de "ativo" | **m2** | corrige inconsistência |
| E12 Revalidating provider | **m2** | fato verificado |
| E13 Governança ADR | **m2** | ADR-013 antes |
| E14 Formato | **m2** (estrutura) + ativos do m1 | — |

**Resumo do placar:** o m2 vence em **timing, verificação, governança e contenção de escopo**; o m1
vence em **completude de modelo (UserClaim, security stamp) e rede de segurança (regras/testes/critérios)**.
Não há vencedor absoluto — são forças complementares.

---

## 6. Recomendação final (síntese best-of-both)

**Seguir o m2 como espinha dorsal, enxertando os melhores ativos do m1.** Concretamente:

1. **Governança primeiro (E13):** escrever **ADR-013** supersedendo a ADR-005 — extensão de usuário por
   **composição**, não herança. Sem isso, não implementar.
2. **Modelo de dados enxuto (E1/E2/E7):** um `User` concreto e serializável com **`SubjectId` imutável
   (ids novos, não `sub=username`)**, `Username` mutável, e **`UserClaim`** (do m1) no lugar do saco de
   `Claim`. Incluir **`SecurityStamp`** como campo desde já (E8), sem ligar a regra ainda.
3. **Serviços focados (E5/E6):** `IUserAuthenticator` + `LockoutPolicy` (política única) e
   `IUserClaimsFactory` reusando as regras de claims do id_token. Interfaces desenhadas para múltiplos
   métodos de auth (costura para externo/MFA — E9), sem implementá-los.
4. **Sessão (E3/E4/E11):** registro serializável com `SubjectId` + clients deduplicados; remover
   `GetCurrentSessionAsync` do store; "sessão atual" num serviço de aplicação; **unificar "ativo"** com a
   semântica do m2 (conta vs sessão; sessão ausente ⇒ inválida).
5. **UI (E10):** manter `ISignInManager`/`ISignOutManager`; mover a máquina de estados do login para um
   método que devolve **enum de desfecho**; telas como cola. **Não** criar o god-service `IAccountInteractionService`.
6. **Limpeza (E12):** **remover** o `IdentityRevalidatingAuthenticationStateProvider` (quebra hoje);
   reintroduzir correto só quando houver área interativa autenticada.
7. **Rede de segurança (E14):** ao escrever o **plano** (depois da ADR), **importar do m1** a lista de
   regras a preservar, os testes de caracterização e os critérios de aceite.

**Sequência:** ADR-013 → `User`+`UserClaim`+`SubjectId` (remove `IdentityUser`) → serviços
(auth/lockout/claims) → sessão+"ativo" unificado → afinar telas → limpeza do provider. Os 14 critérios
de aceite e a bateria de testes do m1 governam o "pronto" de cada fase.

---

## 7. Conclusão

As duas análises concordam no essencial (§2) — o que dá alta confiança ao diagnóstico. As diferenças são
de **escopo** (m1 constrói o futuro; m2 corrige o presente), de **rigor de verificação** (m2 confirmou o
que o m1 supôs) e de **processo** (m2 exige ADR antes; o m1 já planeja). A melhor rota não é escolher um
e descartar o outro: é o **núcleo conservador e verificado do m2**, com **disciplina de ADR**, enriquecido
pelos **ativos duráveis do m1** (`UserClaim`, security stamp, e sobretudo a rede de regras/testes/critérios
que deve alimentar o plano). O único ponto em que recomendo explicitamente o m1 sobre o m2 é a persistência
de claims (`UserClaim`) e a reserva do `SecurityStamp`; em escopo, timing, governança e fachada de UI, o m2
é a escolha mais segura.
