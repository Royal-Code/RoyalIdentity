# Parecer sobre as análises do plano UsersAccounts (an-users-plan-01 / -02)

Data: 2026-06-16

## Escopo e método

Parecer **honesto** sobre os pontos levantados em [an-users-plan-01.md](an-users-plan-01.md) (revisor) e
[an-users-plan-02.md](an-users-plan-02.md) (proprietário/arquiteto), sobre o plano
[plan-users-accounts-module.md](../plans/plan-users-accounts-module.md). Não assumi que os pontos são válidos:
cada um foi avaliado e, onde havia afirmação factual, **verifiquei no código/docs**.

**Verificações factuais feitas para este parecer:**
- `Directory.Build.props` → `TargetFramework = net10.0`. **Confirmado.** (O plano usou `net9.0` por texto de doc desatualizado.)
- `plan-resources-redesign.md` → `## Status: COMPLETED`. **Confirmado.** Logo o redesign de resources está **concluído**;
  o plano UsersAccounts **e o próprio `CLAUDE.md`** ainda dizem "IN_PROGRESS" — ambos desatualizados.
- `architecture.md` §3 desenha `Integration/` **dentro** do módulo — relevante para o ponto de dependência (P-02.3).
- `IUserDirectory.GetSubjectStore(Realm realm)` recebe `Realm` (tipo **rico** do core), não primitivo — relevante para P-02.1/P-02.3.

**Formato por ponto:** *Ponto* → *Procede?* → *Opções (prós/contras)* → *Implicações (aplicar × não)* → *Recomendação + justificativa*.

**Veredito resumido (o que muda):**
1. **Aceitar** o projeto de integração separado (P-02.3) — é o ajuste mais importante; corrige a direção de dependência.
2. **Aceitar** a definição explícita de schema das propriedades por escopo (P-02.2 + R-01.5/10) — lacuna real do plano.
3. **Tratar** a crítica ao `UserClaimDto` (P-02.1) como **mudança de core/ADR-014**, fora deste plano, com recomendação própria.
4. **Corrigir fatos** (net10.0, resources COMPLETED) — barato e obrigatório (R-01.1/2).
5. **Incorporar** RealmId estrutural, contract tests de borda, coexistência opt-in, seed de propriedades (R-01.4/6/7/8).
6. **Direção geral do plano: mantida** — ambas as análises concordam que a sequência e o posicionamento estão certos.

---

# Parte 1 — Pontos de design do proprietário (an-users-plan-02)

## P-02.1 — `UserClaimDto[]` no seam é um tipo intermediário inútil (e "Dto" é indesejado)

**Procede? Parcialmente — e o alvo está fora deste plano.**

A justificativa oficial do `UserClaimDto` (ADR-014 §2.9) é "manter o `UsersAccounts` independente do core: só
primitivos cruzam". Avaliando honestamente: o que **realmente** garante o desacoplamento é **não passar**
`IdentityScope`/`RequestedResources` (tipos do core) — e isso vale para os *parâmetros* (`identityScopeNames`,
`claimTypes`, strings). Mas o **retorno** ser `Claim` **não** quebraria o desacoplamento, porque
`System.Security.Claims.Claim` é tipo da **BCL**, não do `RoyalIdentity`. Portanto a premissa "precisa do DTO para
desacoplar" **não se sustenta** — o proprietário está certo nesse núcleo. O termo `Dto` também é, de fato, um *code
smell* segundo o próprio estilo do repo.

Contra-argumentos honestos (a favor de manter um record): `Claim` é **mutável**, carrega bagagem (`Subject`
=ClaimsIdentity backref, `Issuer`, `OriginalIssuer`, `Properties`); um `record(Type, Value, ValueType?)` é imutável,
com igualdade por valor e mínimo. Mas como o seam é **in-process** (sem serialização) e a borda **já** converte
`UserClaimDto → Claim` hoje, o ganho do DTO é estético/marginal, não estrutural.

**Importante:** `UserClaimDto` é **tipo do core**, fechado na **ADR-014** e entregue no **plano de borda concluído**
(contrato de `IUserPropertyProvider`, `DefaultProfileService`, fake e testes). **Não é artefato deste módulo** — mudá-lo
reabre trabalho concluído da borda.

**Opções:**
- (a) **Manter `UserClaimDto`.** Prós: zero mexida no core/borda já estável. Contras: mantém o tipo que o dono
  considera inútil + sufixo `Dto`.
- (b) **Substituir o retorno do seam por `Claim`** (`IUserPropertyProvider.GetClaimsAsync → IReadOnlyList<Claim>`).
  Prós: remove um tipo e a conversão; alinha ao que a borda usa de fato. Contras: muda contrato do core (ADR-014),
  fake e testes; `Claim` mutável.
- (c) **Renomear** `UserClaimDto → UserClaim`/`ClaimValue` (record, sem "Dto"). Prós: mata o *smell* do nome, mantém
  imutabilidade. Contras: ainda é "tipo no meio"; `UserClaim` foi um nome **removido** na borda (risco de confusão histórica).

**Implicações de aplicar (b/c) × não:** aplicar toca o **core** (não o módulo) e exige **emenda à ADR-014** + ajuste de
`DefaultProfileService`/fake/testes da borda — pequeno, reversível, mas é reabrir um plano "COMPLETED". Não aplicar:
o módulo segue normalmente; fica um tipo que o dono não gosta no contrato.

**Recomendação:** **(b)** — substituir por `Claim` no seam, **mas como mudança de core via emenda curta à ADR-014,
fora do `plan-users-accounts-module`** (registrar como item de pré-requisito/limpeza). Justificativa: a premissa de
desacoplamento que motivou o DTO é incorreta (`Claim` é BCL), o seam é in-process, e a borda já materializa `Claim` —
o tipo intermediário é um *hop* sem retorno. Se o custo de reabrir a borda for indesejado agora, **(c)** (renomear) é o
meio-termo barato. Em ambos, o **módulo não muda** por causa disto.

> Observação de projeto: com o projeto de integração separado (P-02.3), quem produz o `Claim`/`UserClaimDto` é a
> camada de integração — o módulo puro pode expor sua própria projeção (ex.: `AccountClaim`) e a integração mapeia para
> o tipo do seam. Isso isola ainda mais a decisão.

---

## P-02.2 — Falta o schema de `PropertyScope`/`PropertyDefinition` e como é instanciado por conta (modelagem)

**Procede? Sim — lacuna real de detalhe do plano.**

O plano separou conceitualmente `PropertyScope`/`PropertyDefinition` (configuração por realm) de
`UserAccount.Properties` (valores), mas de forma **curta demais**, a ponto de ser lido como "cada conta carrega seu
próprio schema + valores juntos" — o que **seria** erro de modelagem. A leitura do dono é compreensível e o gap é
legítimo: faltou explicitar **dois níveis** (definição × instância) e o **modelo de persistência**.

**Modelo-alvo proposto (dois níveis, explícito):**
- **Definição (schema), por realm** — não por usuário:
  `PropertyScope { RealmId, Name(↔ IdentityScope.Name), Status(Draft/Active), … }`
  `PropertyDefinition { PropertyScopeId, ClaimType, ValueType, DisplayName, Help, IsSensitive, IsRequired, Multiplicity(Single/Multi), Validation[…], IsActive }`.
- **Instância (valor), por conta:**
  `UserAccountPropertyValue { SubjectId(+RealmId), PropertyDefinitionId, Value, (Ordinal p/ multi) }`.

Assim a conta **referencia** definições; **não** as duplica. A projeção do provider faz `join`
definição×valor filtrando por `identityScopeNames` (escopo) **e** `claimTypes` (tipo) — ver R-01.5 (vocabulário) e Q-G1
(semântica).

**Opções de persistência:**
- (a) **Schema relacional** (tabelas `PropertyScope`/`PropertyDefinition`/`UserAccountPropertyValue`). Prós: integridade,
  consulta/admin, índices por realm. Contras: mais tabelas/joins.
- (b) **Valores como JSON** na conta + definições relacionais. Prós: leitura simples por conta. Contras: pior p/ consulta
  administrativa, validação e índice por claim/email.
- (c) **Tudo embutido por conta** (o que o dono temia). Contra: sem schema compartilhado, duplicação, inconsistência — **descartado**.

**Implicações:** aplicar (a) deixa o subdomínio sólido para o futuro admin/API e para a projeção por escopo; não aplicar
(ficar no esboço atual) mantém ambiguidade e arrisca nascer como *bag* de claims (perde a inovação — R-01.10).

**Recomendação:** **(a)**, com os valores multi modelados como linhas (`Ordinal`). Justificativa: é a parte mais forte do
módulo (perfil dirigido por escopo); precisa de schema de verdade (definição × instância) e de índice por realm/claim
para a futura administração. Detalhar isto na **Fase 3/4** do plano e na **ADR-015**.

---

## P-02.3 — Direção de dependência: `UsersAccounts` depender de `RoyalIdentity` é problema de design (headline)

**Procede? Sim — é o ponto mais importante das duas análises, e está correto.**

Verificação: para implementar `IUserDirectory`, o módulo usa `IUserDirectory.GetSubjectStore(Realm realm)` — e `Realm`
é tipo **rico do core** `RoyalIdentity`. Ou seja, o módulo, do jeito planejado, referencia o **assembly inteiro do IdP**
(pipelines, endpoints, handlers, options…). Consequência exatamente como o dono descreve: a **WebApi administrativa**, que
só deveria gerenciar contas, ao referenciar `UsersAccounts` arrasta **todo o `RoyalIdentity`**. É acoplamento errado de
camada. (Nota: isso também mostra que o seam **não** é "só primitivos" no nível do `IUserDirectory` — `Realm` é rico.)

O dono propõe um **projeto de integração** entre IdP e módulo, espelhando o que `Storage.EntityFramework` faz entre
`Data.*` e as facades do core (ADR-013 §2.3). E a ADR-013 §2.4 já **insinua** isso ("expõe sua própria
**implementação/integração** para as portas de borda"). O plano e o `architecture.md` §3, porém, colocam `Integration/`
como **pasta dentro** do módulo — é justamente o que cria o acoplamento.

**Opções:**
- (a) **Projeto de integração separado** (proposta do dono): três peças —
  `RoyalIdentity.UsersAccounts` (domínio + persistência + casos de uso; **não** referencia o IdP; só RoyalCode/EFCore);
  `RoyalIdentity.UsersAccounts.Integration` (referencia **o IdP e o módulo**; implementa
  `ISubjectStore`/`ILocalUserAuthenticator`/`IUserPropertyProvider`/`IUserDirectory` delegando aos casos de uso do módulo);
  o host do IdP referencia a integração e registra no DI.
  Prós: WebApi admin carrega **só** o módulo (sem IdP); espelha `Storage.EntityFramework`; permite **outro** gerenciador de
  usuários só trocando o projeto de integração (plugabilidade real); resolve estruturalmente o risco de acoplamento "grosso"
  de project reference (R-01.9); a integração só enxerga a **API pública** do módulo (fronteira limpa). Contras: +1 projeto;
  a impl das portas não acessa internals do módulo (na prática, desejável).
- (b) **Inverter** (core → módulo). Contra: **proibido pela ADR-013** (core não depende de módulo) e, como o próprio dono
  nota, força quem quer outro gerenciador a carregar o `UsersAccounts`. **Descartado.**
- (c) **Extrair `RoyalIdentity.Abstractions`** (portas + `Subject`/`AuthenticationResult` + ?`Realm`). Contra: `Realm` é
  rico e teria de migrar (mudança grande no core); ADR-013 §2.6 **adiou** Abstractions; ainda assim a admin API arrastaria a
  abstração. Inferior a (a) aqui.
- (d) **Manter `Integration/` dentro do módulo** (status quo do plano). Contra: o problema do dono permanece. **Descartado.**

**Implicações de aplicar (a) × não:** aplicar **muda o plano** (Fase 7 vira "criar o projeto de integração", não "pasta")
e **contradiz o `architecture.md` §3** e o layout do plano — ambos precisam ser atualizados, e a **ADR-015** (ou emenda à
ADR-013) deve **fixar** o projeto de integração como padrão dos módulos (igual a `Storage.EntityFramework`). Não aplicar:
mantém o acoplamento que torna a WebApi administrativa "absurda" (palavra do dono) e o módulo não-reutilizável.

**Recomendação:** **(a) — aceitar o projeto de integração separado.** Justificativa: é coerente com o padrão **já adotado**
no repo (`Storage.EntityFramework` adapta `Data.*`), com a intenção da ADR-013, e entrega o objetivo central do módulo
(ser fonte de contas reutilizável, administrável por uma API própria **sem** o IdP). É o ajuste de maior valor do parecer.
Ação: atualizar `plan-users-accounts-module.md` (separar `*.Integration`), atualizar `architecture.md` §3, e registrar a
regra na ADR-015. Combina com **Q-B1** (provedores `*.Postgre`/`*.Sqlite`): a família passa a ser
`UsersAccounts` (+`.Postgre`/`.Sqlite`) e `UsersAccounts.Integration`.

---

# Parte 2 — Respostas do proprietário às questões (decisões adotadas) + parecer crítico

As respostas (Q-A1…Q-G2) **fecham** as questões em aberto do plano e devem ser promovidas a "Decisões fechadas" +
ADR-015. A maioria é direta e adoto sem ressalva; comento abaixo só as que têm trade-off ou exigem cuidado.

| Q | Decisão do dono | Parecer |
|---|---|---|
| A1 | Mesma pasta dos outros projetos; mesma folder (virtual) da `sln`. | **OK.** Sobrepõe a preferência do revisor por `Modules/`. Coerente com o repo (flat). Combinar com P-02.3 (mais projetos na mesma família). |
| A2 | Libs em `nuget.org` (ex.: `RoyalCode.SmartSearch.AspNetCore`). | **OK, mas manter o pré-flight (R-01.3):** confirmar **versões compatíveis com net10.0** e um exemplo mínimo compilando antes da Fase 2. Disponível ≠ validado. |
| A3 | `net10.0`. | **OK — confirmado em `Directory.Build.props`.** Fechar Q-A3 como "herda `Directory.Build.props`". |
| B1 | EFCore/DbContext no módulo + `*.Postgre`/`*.Sqlite`. | **OK**, com nota de YAGNI: ver P-Extra-1 abaixo. |
| B2 | Manter fakes; ao final, mover para **sqlite (conexão in-memory)**; depois migrar testes. | **OK e saudável** (= coexistência opt-in, R-01.7). Sqlite-in-memory dá round-trip real sem DB externo. |
| C1 | Fixos: `SubjectId/Username/DisplayName/AccountStatus/Roles/Emails/ExternalId?` + credenciais/estado; `email`→primário/fictício; `profile`→props + `username/displayname/role/ExternalId`, cada um **configurável** (inclusive "não entra"). | **OK e bom.** O "configurável por campo" é forte. Implica modelar **mapeamento campo→propriedade/escopo por realm** (entra no schema de P-02.2). |
| C2 | Coleção de email; múltiplos; **único por realm**; `Email{Address,IsPrimary,IsVerified,IsFictitious}`; fictício por realm com **pattern** (`mycompany_{username}@…`) e `IsVerified` configurável. | **OK.** "Único por realm" ⇒ índice único composto `(RealmId, NormalizedAddress)` — reforça RealmId estrutural (R-01.4). Definir precedência de `IsPrimary` e colisão com `AllowDuplicateEmail` (hoje `false`). |
| C5 | 1 `ExternalId?`, índice **não-único** por realm, não é credencial. | **OK.** (Oportunidade futura: coleção de identificadores externos — R-01/oportunidades — fica para depois.) |
| C6 | Roles 1ª classe, projetadas como claim `role` via provider. | **OK** (alinha ADR-014 §2.8: role fora do cookie, sai pelo provider). |
| C7 | `SubjectId` **string**; aceita valor no cadastro (opcional por realm) mas **gera** (`CryptoRandom.CreateUniqueId()`). | **OK.** Cuidado: "aceitar `SubjectId` externo" exige validar **unicidade por realm** e **imutabilidade após criado**. |
| D1 | Só o necessário para `ILocalUserAuthenticator`. | **OK** (corte de credencial-lifecycle → plano #3 mantido). |
| D2 | Lockout vinculado à conta, no domínio; `ILocalUserAuthenticator` aplica. | **OK** (= recomendação do plano). |
| E1 | **Sem endpoints**; só os casos necessários à integração com o IdP; admin posterga. | **OK e mais enxuto** que o plano (que listava CRUD admin). Reduz escopo da Fase 6 ao mínimo p/ a integração. Ver P-Extra-2. |
| F1 | Eventos **no agregado agora**, **sem** mapear coleção/persistir; documentar para não parecer bug; outbox/inbox depois. | **OK e correto** documentar o "ainda não persiste". Evita confusão futura. |
| G1 | "Questão confusa"; revela **decisão de design** (quem resolve claims por escopo: IdP passando claim types × módulo dono do mapeamento). | **Precisa decisão** — ver P-Extra-3. |
| G2 | Sem validação `PropertyScope↔IdentityScope` no módulo (acoplamento forte); futuro: draft→ativar→evento cria identity scope no IdP. | **OK** desacoplar. **Corrigir o fato**: resources está **COMPLETED**, então os `Name`s de identity scope estão **estáveis** — o acoplamento por `Name` (string) fica seguro. |

### P-Extra-1 — `*.Postgre`/`*.Sqlite` desde já (Q-B1): risco de YAGNI

**Procede como observação.** Criar dois projetos de provedor logo no 1º módulo **antecipa** estrutura antes de haver um
2º provedor em uso real. **Opções:** (a) seguir o dono (2 projetos já) — prós: consistência com `Data.*`, simetria;
contras: +2 projetos cedo. (b) DbContext+mappings no módulo e **extrair** `*.Postgre`/`*.Sqlite` quando o 2º provedor
entrar — prós: menos peças agora; contras: refactor depois. **Recomendação:** seguir **(a)** por consistência **se** os
testes já usarão Sqlite (B2) e produção Postgre — nesse caso os dois provedores **têm uso imediato**, então **não é YAGNI**.
Justificativa: como o próprio B2 adota Sqlite para teste, ambos os provedores são exercitados desde o início.

### P-Extra-2 — Cortar Fase 6 para o mínimo de integração (Q-E1)

**Procede.** O plano listava CRUD administrativo; o dono restringe ao **necessário para a integração** (criar/semear conta,
verificar credencial+lockout, definir/projetar propriedades, lookup). **Recomendação:** reescrever a Fase 6 como "casos de
uso mínimos para a borda" e mover cadastro/manutenção administrativa para o **plano admin (#5)**. Justificativa: reduz
escopo e risco; a API admin é outro plano.

### P-Extra-3 — Q-G1: quem é dono da resolução "scope → claims" (mudança de contrato)

**Procede e é decisão real, não confusão.** Hoje: `IdentityScope.UserClaims` (core) declara os claim types do escopo; a
borda calcula `claimTypes` e os passa ao provider junto com `identityScopeNames`; o fake filtra **só** por `claimTypes`. Se
o módulo passa a **ser dono** das propriedades por escopo, há **duas fontes** do mapeamento escopo→claims (o
`IdentityScope.UserClaims` do IdP e as `PropertyDefinition` do módulo).

**Opções:**
- (a) **Manter os dois parâmetros** (escopo **e** claim types); o módulo **interseciona**. Prós: o IdP continua autoritativo
  sobre "quais claims um escopo rende" (consistente com consent/MDC); menor disrupção. Contras: duplicação; exige que
  `PropertyDefinition.ClaimType` **concorde** com `IdentityScope.UserClaims`.
- (b) **Módulo dono**: a borda passa **só** `identityScopeNames`; o módulo devolve os claims que **suas** definições mapeiam
  para aqueles escopos. Prós: fonte única (módulo); casa com "propriedades particionadas por escopo". Contras: muda o
  contrato de `IUserPropertyProvider` (core/ADR-014); `IdentityScope.UserClaims` vira **advisory**; consent/telas podem
  divergir do que o módulo realmente emite.

**Implicações:** (b) é **mudança de core/ADR-014** (mexe na borda concluída) e interage com o resources (agora COMPLETED);
(a) não muda contrato. **Recomendação:** **(a) agora** (interseção escopo∧tipo — ver Q-G1/R-01.5), e **registrar (b) como
avaliação futura** alinhada ao resources, **não** silenciosa. Justificativa: preserva o IdP autoritativo para consent e não
reabre a borda; o ganho de (b) é organizacional e pode vir depois com emenda explícita.

---

# Parte 3 — Pontos do revisor (an-users-plan-01)

## R-01.1 — Target framework real é `net10.0` (fechar/reescrever Q-A3)

**Procede? Sim — verificado** (`Directory.Build.props`). **Ação:** trocar no plano `net9.0`→`net10.0` e fechar Q-A3 como
"herda `Directory.Build.props`". **Implicação:** baixíssima; evita a IA planejar contra framework errado. **Recomendação:**
aplicar já (corrigir também a frase do `CLAUDE.md`/foundation se citar net9).

## R-01.2 — `plan-resources-redesign.md` está COMPLETED, não IN_PROGRESS

**Procede? Sim — verificado** (`## Status: COMPLETED`). O plano UsersAccounts **e o `CLAUDE.md`** dizem IN_PROGRESS —
**ambos desatualizados**. **Implicação:** Q-G2 deve parar de tratar `IdentityScope` como "em redesenho"; os nomes estão
**estáveis**, o que **fortalece** o acoplamento por `Name`. **Recomendação:** corrigir o plano e **também o `CLAUDE.md`**
(linha da seção "Active plans" que lista resources como IN_PROGRESS) — senão a desinformação se propaga.

## R-01.3 — Pré-flight das libs RoyalCode antes do skeleton

**Procede? Sim.** Mesmo com Q-A2 (libs no nuget.org), "disponível" ≠ "valida em net10.0 com a API documentada".
**Opções:** (a) Fase 0 de pré-flight (resolver pacotes, versão net10.0, 1 exemplo `[Command]`+`ICriteria` compilando);
(b) seguir direto e descobrir na Fase 2. **Implicações:** (a) gasta pouco e remove o maior risco de build/estilo; (b)
arrisca travar as primeiras fases. **Recomendação:** **(a)** — adicionar gate de pré-flight. Justificativa: é o risco nº 1 do
próprio plano; provar cedo é barato.

## R-01.4 — `RealmId` explícito no modelo persistido

**Procede? Sim — e importante.** Portas realm-bound resolvem o **runtime**, mas a **persistência** multi-tenant precisa de
`RealmId` **estrutural** (chaves, índices, unicidade), como defesa contra vazamento cross-realm. As respostas C2 ("único por
realm") e C5 ("índice por realm") **exigem** isso. **Opções:** (a) `RealmId` em todas as raízes/índices compostos; (b) confiar
no escopo do repositório. **Implicações:** (a) torna o isolamento verificável e seguro; (b) deixa a invariante implícita
(frágil). **Recomendação:** **(a)** — `RealmId` inegociável no schema (chave composta/índices `(RealmId, …)`). Justificativa:
isolamento por realm é invariante de produto (ADR-013/AGENTS); não pode depender de quem construiu o repositório.

## R-01.5 — Vocabulário das propriedades por escopo

**Procede? Sim** — consolidado em **P-02.2** (definição/valor/claimType/multiplicidade/sensibilidade/validação/estado). Sem
repetição: aplicar junto de P-02.2.

## R-01.6 — Contract tests de borda (rodando contra fake **e** módulo)

**Procede? Sim — e é uma boa engenharia.** É mais forte que "paridade por intenção": uma bateria única que valida
`ISubjectStore`/`ILocalUserAuthenticator`/`IUserPropertyProvider` contra **qualquer** implementação. **Opções:** (a) suíte de
contrato compartilhada; (b) confiar só nos 206 testes do IdP. **Implicações:** (a) pega divergência (ordem de falha, login por
email, filtro de claim) na fonte, e protege futuras implementações; (b) detecta tarde, via efeito colateral. **Recomendação:**
**(a)** — item explícito na Fase 7/8. Justificativa: o fake é a **especificação executável** da borda; vale fixá-lo como tal.

## R-01.7 — Coexistência opt-in (fake default até paridade provada)

**Procede? Sim** — e **coincide com Q-B2** do dono. **Recomendação:** registrar a troca de DID como **opt-in por host/ambiente**,
fake como default nos testes até os contract tests (R-01.6) + seeds (R-01.8) passarem; então virar. Implicação: migração sem
*big bang*. Justificativa: protege os 206 testes.

## R-01.8 — Seed/migração das propriedades padrão como subtarefa própria

**Procede? Sim.** Alice/Bob têm hoje `email`/`role` como **claims planas**; com o modelo por escopo, é preciso decidir como isso
vira propriedade do escopo `email`/`profile` e role 1ª classe (respostas C1/C6 já orientam). **Recomendação:** subtarefa explícita
na Fase 8 (mapa claim-plana→propriedade-por-escopo + seeds determinísticos). Justificativa: é exatamente onde a paridade
silenciosamente quebra.

## R-01.9 — Acoplamento "grosso" de project reference ao core

**Procede? Sim — e é resolvido por P-02.3.** Em C#, referência de projeto expõe **todo** o público; "use só contratos/DTOs" não
é forçado pelo compilador. **Com o projeto de integração separado (P-02.3), o módulo puro nem referencia o core**, e os testes de
arquitetura guardam a integração. **Recomendação:** adotar P-02.3 + testes de arquitetura (Fase 2). Justificativa: transforma uma
regra "por disciplina" em **garantia estrutural**.

## R-01.10 — Modelo de propriedades pode inflar (ou virar "bag")

**Procede? Sim — risco real de dois lados.** **Recomendação:** implementação **inicial pequena** com **nomes certos** (o schema de
P-02.2), sem virar outro bounded context agora. Justificativa: preserva a inovação sem atrasar o módulo.

## R-01.11 — Risco da troca global do `IUserDirectory`

**Procede? Sim** — mitigado por R-01.6 (contract tests) + R-01.7 (opt-in) + R-01.8 (seeds). **Recomendação:** Fase 8 conservadora
nessa ordem. Sem ação adicional além das já listadas.

## Oportunidades (an-users-plan-01) — parecer breve

Não são "problemas"; são adições. **Válidas e que valem registrar (sem construir agora):** perfil dirigido por escopo como
**metadados reutilizáveis** pela futura UI admin (data-driven); `ExternalId` evoluindo para **coleção** de identificadores;
**catálogo de eventos** de domínio nomeados desde já (com pontos de emissão, mesmo sem outbox — casa com F1); **suíte de
contract tests** como ativo reutilizável (= R-01.6); **read model administrativo** desde cedo (casa com SmartSearch). **Não**
incorporar como escopo do plano — registrar como notas/oportunidades na ADR-015 e nos planos #5/#3/#6 correspondentes.

---

# Parte 4 — Síntese: ajustes a aplicar e o que não muda

**Aplicar (ordenado por valor):**
1. **Projeto de integração separado** (P-02.3): `UsersAccounts` (puro) + `UsersAccounts.Integration` (implementa as portas) +
   `*.Postgre`/`*.Sqlite`. Atualizar o plano, o `architecture.md` §3 e fixar na ADR-015. **(corrige R-01.9)**
2. **Schema explícito de propriedades por escopo** (P-02.2 + R-01.5/10): definição (por realm) × valor (por conta); persistência relacional.
3. **`RealmId` estrutural** no schema/índices/unicidade (R-01.4; exigido por C2/C5).
4. **Correções factuais** (R-01.1/2): `net10.0`; resources **COMPLETED** (corrigir também o `CLAUDE.md`).
5. **Contract tests de borda** + **coexistência opt-in** + **seed de propriedades** (R-01.6/7/8; casa com Q-B2).
6. **Pré-flight das libs RoyalCode** como gate (R-01.3), apesar de Q-A2.
7. **Fechar as questões** com as respostas do dono (Parte 2) na ADR-015; reescrever a Fase 6 ao mínimo de integração (Q-E1/P-Extra-2).

**Tratar como mudança de core (fora deste plano), com decisão explícita:**
- `UserClaimDto` → `Claim` (ou renome) via **emenda à ADR-014** (P-02.1).
- "Dono da resolução scope→claims" (Q-G1/P-Extra-3): manter (a) agora; (b) avaliação futura registrada.

**O que NÃO muda (ambas as análises concordam):** o **posicionamento** (`UsersAccounts` como módulo de domínio separado, sem
reabrir OIDC, sem API/UI dentro), a **fronteira** com o core (módulo implementa as portas; core não referencia o módulo), e a
**sequência** ADR → skeleton → domínio → propriedades → persistência → features → integração → DI/paridade → diferidos. A direção
geral do plano está correta; os ajustes são de **rigor e de fronteira de projeto**, não de rumo.

---

## Referências

- Análises avaliadas: [an-users-plan-01.md](an-users-plan-01.md), [an-users-plan-02.md](an-users-plan-02.md).
- Plano: [plan-users-accounts-module.md](../plans/plan-users-accounts-module.md).
- Base: [an-users-arch.md](an-users-arch.md), [ADR-013](../../adrs/ADR-013.md), [ADR-014](../../adrs/ADR-014.md),
  [architecture.md](../foundation/architecture.md).
- Verificações: `Directory.Build.props` (net10.0); [plan-resources-redesign.md](../plans/plan-resources-redesign.md) (COMPLETED);
  `RoyalIdentity/Users/Contracts/IUserDirectory.cs` (`Realm` no getter); `architecture.md` §3 (`Integration/` dentro do módulo).
