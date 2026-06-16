# Análise do plano UsersAccounts 01

Data: 2026-06-16

## Escopo desta análise

Esta análise avalia o plano `.ai/plans/plan-users-accounts-module.md` contra o que
já está decidido ou implementado hoje no RoyalIdentity.

Importante: as **questões em aberto do plano não foram tratadas como problema**. Eu
as considerei como pontos de decisão/governança que serão fechados depois, conforme
orientação.

## Referências usadas

- `.ai/plans/plans-roadmap-01.md`, seção 1.
- `.ai/plans/plan-users-accounts-module.md`.
- `.ai/analisys/an-users-arch.md`.
- `.ai/analisys/an-users-final.md`.
- `.ai/analisys/an-users-pontos2.md`.
- `.ai/foundation/architecture.md`.
- `.ai/foundation/product.md`.
- `.ai/foundation/tech.md`.
- `.ai/foundation/structure.md`.
- `.ai/rules/code-style.rules.md`.
- `adrs/ADR-013.md`.
- `adrs/ADR-014.md`.
- `.ai/plans/plan-resources-redesign.md`.
- Código atual da borda/fake:
  - `RoyalIdentity/Users/Contracts/ISubjectStore.cs`.
  - `RoyalIdentity/Users/Contracts/ILocalUserAuthenticator.cs`.
  - `RoyalIdentity/Users/Contracts/IUserPropertyProvider.cs`.
  - `RoyalIdentity/Users/Contracts/IUserDirectory.cs`.
  - `RoyalIdentity/Users/Subject.cs`.
  - `RoyalIdentity/Users/AuthenticationResult.cs`.
  - `RoyalIdentity/Contracts/Defaults/DefaultProfileService.cs`.
  - `RoyalIdentity.Storage.InMemory/MemoryUserAccount.cs`.
  - `RoyalIdentity.Storage.InMemory/MemorySubjectStore.cs`.
  - `RoyalIdentity.Storage.InMemory/MemoryLocalUserAuthenticator.cs`.
  - `RoyalIdentity.Storage.InMemory/MemoryUserPropertyProvider.cs`.
  - `RoyalIdentity.Storage.InMemory/MemoryUserDirectory.cs`.
  - `RoyalIdentity.Storage.InMemory/RealmMemoryStore.cs`.
  - `RoyalIdentity.Storage.InMemory/LockoutPolicy.cs`.
  - `RoyalIdentity/Options/AccountOptions.cs`.
  - `RoyalIdentity/Options/PasswordOptions.cs`.
  - `Directory.Build.props`.

## O que se sabe hoje

O módulo `RoyalIdentity.UsersAccounts` deve ser a camada B: domínio rico de contas,
persistência própria, propriedades por escopo, credenciais, lockout e casos de uso
administrativos. Ele não deve reescrever a implementação OIDC nem mover sessão,
tokens, consentimentos, authorize/token/userinfo ou cookies para dentro do módulo.

A borda do IdP já está preparada para isso. O core define contratos pequenos:
`IUserDirectory`, `ISubjectStore`, `ILocalUserAuthenticator` e
`IUserPropertyProvider`. O fake in-memory implementa esses contratos hoje e o módulo
deve entrar por troca de DI.

O seam já tem uma regra boa: o `IUserPropertyProvider` recebe apenas primitivos
(`subjectId`, nomes de identity scopes e claim types). O módulo não deve receber
`IdentityScope`, `RequestedResources`, `Client`, `HttpContext` ou modelos ricos do
IdP.

O comportamento atual que precisa virar contrato de paridade é bem específico:

- `SubjectId` é estável, opaco, separado de `Username`, e já existe seed
  determinístico para Alice e Bob.
- `ILocalUserAuthenticator` resolve login por chave/username, depois username
  case-insensitive, e só usa email quando `LoginWithEmail` ou `EmailAsUsername` está
  ativo.
- A ordem de falha atual é: não encontrado, inativo, bloqueado, sem hash, senha
  inválida, sucesso.
- Falha de senha incrementa contador e sucesso zera contador.
- `IUserPropertyProvider` retorna lista vazia para usuário inexistente/inativo.
- Claims são filtradas estritamente pelos claim types solicitados.
- O fake in-memory ainda ignora `identityScopeNames`; o módulo deve honrar esse
  argumento porque as propriedades passarão a ser particionadas por escopo.
- `DefaultProfileService` continua sendo o orquestrador de claims no IdP; o módulo
  só fornece os DTOs.

Há dois fatos práticos relevantes:

- O `Directory.Build.props` já está em `net10.0`. Então qualquer dúvida de target
  framework deve ser resolvida em favor de herdar `net10.0`, salvo decisão explícita
  contrária.
- Os projetos atuais não referenciam pacotes `RoyalCode.*`. A adoção de
  `SmartCommands`, `SmartSearch`, `WorkContext`, `Aggregates`, `Entities`,
  `Problems` etc. é uma dependência nova real, não algo já testado na solução.

## Planejamento alvo que eu montaria

Eu manteria uma sequência muito parecida com a do plano atual, com alguns gates mais
explícitos.

### 0. Pré-flight e decisão arquitetural

Antes de codar o skeleton, eu fecharia a ADR do módulo e verificaria os pacotes
RoyalCode em condições reais: feeds, versões compatíveis com `net10.0`, licenças,
API atual e exemplos mínimos compilando. Isso evita começar a arquitetura em cima
de uma biblioteca que ainda não está disponível para a solução.

Também fecharia, nesse momento, a decisão física do projeto: raiz da solução ou
`Modules/UsersAccounts/`. Minha preferência atual é aceitar a pasta `Modules/` para
os módulos novos, porque deixa claro que `UsersAccounts` e `KMS` são outra família
arquitetural. Mas isso deve ser uma decisão explícita porque afeta `.sln`, paths,
docs e futuras APIs/UIs.

### 1. Skeleton do módulo

Criar `RoyalIdentity.UsersAccounts` com a feature-slice curta de
`.ai/foundation/architecture.md`: `Features/`, `Infrastructure/`, `Integration/`
e testes dedicados.

Nesta fase eu colocaria testes de arquitetura mínimos: sem Web/API no módulo,
sem dependência de `RoyalIdentity.Server`, sem uso de `HttpContext`, sem `IdentityScope`
ou `RequestedResources` atravessando o seam de claims, e sem tipos do módulo vazando
para o core.

### 2. Domínio de conta

Modelar o agregado `UserAccount` com `SubjectId`, username, display name, status,
emails, identificadores externos e credencial local mínima. O `SubjectId` deve ser
imutável por regra de domínio e por persistência.

Eu daria atenção especial ao `RealmId` nesta fase. Mesmo com portas realm-bound,
a persistência própria precisa ter o realm como parte estrutural do modelo,
índices e unicidade. A arquitetura não pode depender apenas do fato de alguém ter
criado o repositório com o realm certo.

### 3. Propriedades dinâmicas por escopo

Eu trataria isso como um subdomínio de primeira classe, não como um detalhe de
claims. O módulo precisa modelar ao menos:

- definição de propriedade;
- claim type emitido;
- identity scope name;
- valor por usuário;
- multiplicidade;
- sensibilidade/visibilidade;
- regras de validação;
- estado ativo/inativo da definição.

Não precisa virar outro bounded context agora, mas precisa ter vocabulário próprio,
porque é uma das ideias mais fortes do módulo.

### 4. Persistência própria

Criar `UsersAccountsDbContext`, mappings, índices e repositórios. Aqui eu exigiria
testes reais com Sqlite ou equivalente, incluindo unicidade por realm para
`SubjectId`, username normalizado, email quando aplicável e external id quando
modelado.

Mesmo que a escolha final de Postgres/Sqlite venha depois, esta fase precisa provar
round-trip do agregado, VOs, propriedades por escopo e lockout.

### 5. Casos de uso administrativos internos

Adicionar features administrativas do módulo sem expor HTTP ainda: criar conta,
alterar status, alterar username, gerenciar emails, gerenciar credencial local,
definir propriedades por escopo e consultar conta.

Esses casos de uso devem retornar resultados/erros de domínio, não exceptions para
fluxo esperado. A API administrativa futura só adapta HTTP para esses casos de uso.

### 6. Integração com a borda do IdP

Implementar `ISubjectStore`, `ILocalUserAuthenticator`, `IUserPropertyProvider` e
`IUserDirectory` no `Integration/`.

Eu criaria uma suíte de **contract tests de borda** que roda tanto contra o fake
in-memory quanto contra o módulo. Isso é mais forte do que "paridade por intenção":
a mesma bateria comprova que as duas implementações respeitam os contratos.

### 7. DI opt-in e paridade de seeds

Registrar o módulo de forma opt-in no host/testes. Eu evitaria uma troca global
imediata até os contract tests e seeds estarem maduros. A paridade de Alice/Bob é
crítica porque vários testes do IdP dependem desses fluxos.

### 8. Diferidos explícitos

Eventos de domínio, outbox/inbox, replicação, MFA/passwordless, federação e security
lifecycle completo podem ficar diferidos. Mas vale deixar os nomes dos eventos e os
pontos de emissão previstos, para não desenhar um domínio que depois não tem onde
encaixar auditoria e replicação.

## Comparação com o plano existente

O plano existente está bem alinhado com esse planejamento alvo. A sequência ADR ->
skeleton -> domínio -> propriedades por escopo -> persistência -> features ->
integração -> DI/paridade -> eventos diferidos é a ordem certa.

A diferença principal é que eu tornaria alguns gates mais explícitos antes da Fase 2:
verificação real das libs RoyalCode, target framework, status do redesign de
resources e decisão física do projeto. O plano já levanta boa parte disso como
questões em aberto; eu só puxaria essas respostas para critérios de entrada.

Também reforçaria a fase de integração com uma suíte nomeada de contract tests para
as portas de borda. O plano fala em paridade, mas "contract tests executáveis contra
as duas implementações" deixa a proteção mais concreta.

## O que está bom

O escopo está correto. O plano não tenta aplicar a arquitetura nova em toda a
solução e não tenta reabrir a implementação OIDC. Ele posiciona `UsersAccounts`
como módulo de domínio separado, exatamente onde faz sentido.

A fronteira com o core está bem desenhada. `UsersAccounts` implementa as portas de
borda; o core não referencia o módulo; API/UI ficam fora; o seam de claims usa
primitivos. Isso preserva a arquitetura já decidida em ADR-013/014.

O plano aproveita bem o trabalho já feito na borda. Ele cita os contratos atuais,
reproduz as assinaturas certas e descreve comportamentos importantes do fake
in-memory. Esse é um bom sinal: o módulo novo não está sendo planejado em abstrato,
mas como substituição compatível de uma implementação existente.

A decisão de juntar contas ricas com propriedades por escopo é boa. Ela evita criar
um `UserAccount` rígido demais e aproxima o modelo do que um authorization server
real precisa: claims e perfis variam por realm, por escopo e por política.

As fases têm boa granularidade. O plano separa domínio, persistência, features e
integração, e deixa API/UI para outro plano. Isso reduz bastante o risco de o módulo
nascer como uma mistura de controller, EF e regra de negócio.

A lista de out-of-scope é saudável. Segurança avançada, federação, MFA/passwordless,
admin UI/API, persistência geral do IdP e KMS são trabalhos relacionados, mas não
devem ser acoplados a este primeiro módulo.

A seção de riscos está honesta, especialmente ao chamar atenção para as libs
RoyalCode e para a paridade de seeds.

## O que pode melhorar

O plano deveria atualizar alguns fatos de base antes de virar execução. O target
framework real da solução é `net10.0`, então Q-A3 pode ser fechado ou reescrito como
"herdar `Directory.Build.props`". O plano de resources também aparece como algo em
andamento em alguns pontos, mas `plan-resources-redesign.md` está marcado como
completo.

Eu adicionaria uma fase ou checklist de pré-flight antes do skeleton. O ponto mais
importante é validar as libs RoyalCode na prática. Hoje a solução não usa esses
pacotes, então essa decisão tem risco de build, API e estilo. Não precisa abandonar
a ideia; só precisa provar cedo.

O `RealmId` deve aparecer de forma mais explícita no modelo alvo. O plano fala em
realm-scoped e índices por realm, mas o sketch do agregado não deixa esse campo tão
visível. Para um produto multi-tenant, eu colocaria `RealmId` como parte inegociável
da persistência e das chaves/índices.

A fase de propriedades por escopo merece mais vocabulário. Ela é central para o
módulo, mas ainda está um pouco "propriedade dinâmica genérica". Eu detalharia
definição, valor, claim type, multiplicidade, sensibilidade e validação, mesmo que
a implementação inicial seja mínima.

Eu deixaria "contract tests de borda" como item explícito. A paridade com
`MemoryUserDirectory` não deve depender apenas dos testes do IdP passarem; deve
haver uma suíte dedicada que valide `ISubjectStore`, `ILocalUserAuthenticator` e
`IUserPropertyProvider` contra qualquer implementação.

A coexistência com o fake poderia ser mais operacional. O plano fala da troca via
DI, mas eu preferiria registrar explicitamente um modo opt-in para o módulo durante
a migração, mantendo o fake como default em alguns testes até a paridade estar
provada.

A migração/seeding de propriedades padrão merece uma subtarefa própria. Alice/Bob
hoje têm email e role em claims planas; o módulo precisará decidir como isso vira
propriedade do escopo `email`, propriedade do escopo `profile`, papel administrativo,
ou uma combinação.

## Problemas e riscos reais

O maior risco prático é a dependência de bibliotecas RoyalCode ainda não usadas pela
solução. Se elas não estiverem disponíveis, não compilarem em `net10.0`, ou tiverem
APIs diferentes das documentadas, as primeiras fases travam. Isso não invalida a
arquitetura, mas pede uma validação curta antes de comprometer o plano.

Há um pequeno desalinhamento factual no plano em torno do target framework e do
status de resources. São ajustes simples, mas importantes para a IA não planejar
código em cima de informação antiga.

Como o módulo provavelmente precisará referenciar `RoyalIdentity` para implementar
os contratos, existe um risco de acoplamento acidental. Em C#, a referência de
projeto é grossa: mesmo que a regra diga "usar só contratos e DTOs primitivos", o
compilador permitirá usar mais coisas. Testes de arquitetura ou uma futura extração
para `RoyalIdentity.Abstractions` podem ser necessários se o limite começar a
escapar.

O modelo de propriedades por escopo pode virar complexo rápido. Se for tratado só
como "bag de claims", perde a inovação. Se for modelado grande demais de início,
atrasa o módulo. O plano precisa manter a implementação inicial pequena, mas com os
nomes certos para evoluir.

A troca global do `IUserDirectory` no host pode quebrar fluxos OIDC por detalhes de
seed, claim type, lockout ou login por email. A fase de DI deve ser conservadora:
opt-in, contract tests, depois suíte ampla.

## Oportunidades de inovação

A ideia mais forte é o perfil orientado por escopos. Em vez de o usuário ter uma
lista fixa de claims, cada realm pode definir quais propriedades existem, quais
claim types elas emitem e sob quais identity scopes aparecem. Isso combina muito
bem com um authorization server multi-tenant.

As definições de propriedade podem virar metadados reutilizáveis pela futura API/UI:
tipo, obrigatoriedade, multiplicidade, máscara/sensibilidade, validação, texto de
exibição, localização e política de edição. Assim a UI administrativa pode ser mais
data-driven sem perder controle de domínio.

Emails opcionais, múltiplos e fictícios são uma diferenciação real. Muitos sistemas
legados têm usuários sem email confiável, usuários compartilhados, emails duplicados
ou identificadores externos. O módulo pode suportar esses cenários sem contaminar
o OIDC core.

`ExternalId` pode ser mais do que um campo. No futuro, pode virar uma coleção de
identificadores externos por sistema/origem, facilitando migração, reconciliação e
federação.

Eventos de domínio podem ser pensados desde já para auditoria e replicação:
`UserAccountCreated`, `UsernameChanged`, `EmailAdded`, `EmailVerified`,
`CredentialChanged`, `AccountBlocked`, `AccountActivated`, `PropertyValueChanged`,
`PropertyDefinitionChanged`. Mesmo que outbox fique diferido, os pontos de emissão
podem ser preservados.

Uma suíte compartilhada de contract tests para `IUserDirectory` seria uma boa
inovação interna. Ela permitiria testar fake in-memory, módulo real, e eventualmente
implementações customizadas de clientes sem reescrever cenários.

O módulo também pode preparar um read model de administração desde cedo: busca por
username/email/external id/status, filtros por realm, e projeções sem expor o
agregado inteiro. Isso conversa bem com `SmartSearch`, se a biblioteca entrar.

## Veredito

O plano está bom e, principalmente, está no lugar arquitetural certo. Ele respeita a
borda já implementada, não tenta reabrir o OIDC, não mistura API/UI dentro do módulo
e trata `UsersAccounts` como domínio próprio.

Eu não mudaria a direção geral. O que eu faria antes de executar é endurecer os
gates de entrada, atualizar os fatos observáveis (`net10.0`, resources completo),
explicitar `RealmId` no modelo persistido e transformar "paridade com o fake" em
contract tests de borda.

Se esses ajustes entrarem, o plano vira um bom primeiro módulo para provar a
arquitetura feature-slice sem forçar essa arquitetura no restante da solução.
