# Análise do plano UsersAccounts 02

Data: 2026-06-16

## Escopo desta análise

Esta análise avalia o plano `.ai/plans/plan-users-accounts-module.md`.

## Comentários do proprietário e arquiteto do projeto

O plano está em coêrente nas seções iniciais, `Contexto`, `Objetivo`, `Fora de Escopo` e `Decisões já fechadas`.

No entanto há uma afirmação/decisão/modelagem que me encomoda, o uso do `UserClaimDto[]` como objeto de comunicação de borda. Já tem o `Claim` nativo das bibliotecas de autenticação, isso se trata de autenticação, colocar mais um tipo no meio entre a entidade que vem do banco e o `Claim` que é utilizado na autenticação me parece inútil, não consigo enxergar ganho nenhum, e ainda vejo um termo não desejado: `Dto`.

Outro ponto problemático é que falta a definição do `PropertyScope` e `PropertyDefinition` como schema e como isso é "instanciado" por conta de usuário.
O modelo de como salvar no banco não foi discutido, e também a modelagem me parece que não tem schema, ou que cada conta de usuário tem seu schema e valores juntos. Parece um erro de modelagem. Isso é grave ao meu ver.

O problema mais grave de é de design, onde o UserAccounts depende de RoyalIdentity.
O RoyalIdentity servirá para IdP, para autenticação com OIDC. UserAccounts vai gerenciar usuários, servirá como fonte para o IdP, mas também será usada de WebApi para administração.
Colocar RoyalIdentity como UserAccounts faz com que a WebApi tenha que carregar o RoyalIdentity junto com ela, o que absurdo.
O inverso é o melhor caminho rápido, mas também não é 100% bom, pois caso alguém queira implementar outro gerencidor de usuários, terá que carregar o UserAccounts.
O melhor é ter um projeto de ligação entre o RoyalIdentity e o UserAccounts, assim como é pretendido ter entre os storages e os projetos Data.
Na minha visão deveria ter um projeto `RoyalIdentity.UserAccounts.Integrations` ou algo com outro nome.

Também há os pontos a considerar de `.ai\analisys\an-users-plan-01.md`.

## Respostas as questões

### Q-A1 — Onde fica o projeto fisicamente

Ficam no mesmo diretório dos outros projetos.
Na `sln` deve ficar na mesma folder (virtual) dos outros projetos com o `RoyalIdentity`.

### Q-A2 — As libs RoyalCode estão disponíveis

As libs estão disponíveis em `nuget.org`, como por exemplo: `https://www.nuget.org/packages/RoyalCode.SmartSearch.AspNetCore/#readme-body-tab`.

### Q-A3 — Target framework

net10.0

### Q-B1 — Provider da persistência própria

Aqui devemos tomar um caminho e divisão como feito em `Data.*`.

O melhor seria manter o uso do EFCore e DbContext no `RoyalIdentity.UserAccounts` e criar `RoyalIdentity.UserAccounts.Postgre` e `RoyalIdentity.UserAccounts.Sqlite` com mapeamentos e configurações.

### Q-B2 — Coexistência com o fake durante a migração

Manter os Fake inicialmente.

No final, os fakes devem ser movidos para sqlite com conexão em memória, ou usando EFCore com InMemory. No entanto prefiro sqlite.

Depois de ter criado os fakes para o módulo, usando uma das duas abordagens acima, então pode se refatorar as configurações dos testes para começar a usar UserAccount com fakes dele.

### Q-C1 — Perfil "scope-driven" vs. campos 1ª classe

A conta de usuário deve ter algumas propriedades fixas, como recomendado `SubjectId`, `Username`, `DisplayName` e `AccountStatus`, mas também `Roles`, `Emails`, `ExternalId?`, fora as credenciais e outras propriedades de controle de estado que devem existir.

Quanto aos `scope`, o `email` fica com o primary email ou fictício se habilitado.
`profile` tem propriedades adicionais e fica com `username`, `displayname`, `role` e `ExternalId`. Deve ser possível configurar como cada um destes campos entrar em `profile`, com opção de não entrar também.

### Q-C2 — Modelo de email

Deve ter um campo (coleção) dedicado a email.
Pode ter mais de um, múltiplos.
Único por Realm.
`Email { Address, IsPrimary, IsVerified, IsFictitious }`.
Regra de criação de fictício por Realm.
Pattern de geração fictícia por realm, por expressão como: `mycompany_{username}@mycompany.com`. Compo `IsVerified` do fictício deve ser configurado por Realm também.

### Q-C5 — ID externo (legado)

1 `ExternalId?` opcional, índice **não-único** por realm, **não** é credencial de login.

### Q-C6 — Roles: 1ª classe ou propriedade

1ª classe (autorização é transversal), projetadas como claim `role` via provider.

### Q-C7 — Tipo da chave do agregado

`SubjectId` deve ser `string`, deve aceitar valor no cadastro, mas gerar automáticamente, pode ser usado `CryptoRandom.CreateUniqueId()`.

Aceitar `SubjectId` no cadastro pode ser opcional, conforme Realm, se habilita ou não.

### Q-D1 — Quanto do ciclo de senha entra aqui

Só o necessário para `ILocalUserAuthenticator`.

### Q-D2 — Onde mora o lockout

Deve ser vinculado a conta de usuário, no domínio.

Tende a ser como a recomendação: `LockoutPolicy` vira regra de domínio do módulo.

O `ILocalUserAuthenticator` deverá aplicar as regras quando chamado pelo IdP.

### Q-E1 — Quais casos entram

Não incluir endpoints ainda.

Só criar casos necessário para implementação da integração com o IdP. Casos administrativos de cadastro e outros ainda não são necessário, postergar para plano administrativo do UserAccounts.

### Q-F1 — O que entra agora

Eventos podem ser gerados agora. Salvá-los, ter outbox, inbox, replicação, deve ter plano a parte.

Por hora, manter os eventos no agregado, não mapear coleção de eventos, não gravar em lugar nenhum. Deixar isso documentado em algum lugar para não causar confusão achando que é bug ou inútil.

Outbox, Inbox e gravação de eventos será integrado com EFCore e será feito depois.

### Q-G1 — Semântica exata da projeção

A questão é confusa.

Existem escopos de identidade. Um escopo de identidade no UserAccounts tem propriedades.
Propriedades geram `Claim`.
No RoyalIdentity, quando um scope de identity é solicitado, é verificado quais claims devem ser solicitados.

O óbvio é pegar os claims do usuário conforme identity scope solita.

No entanto pode ser revisado isso, os IdP solicita os `claims` dos usuarios por scope, o processo de retornar os claims válidos fica no UserAccount então.

Isso é uma mudança. Precisa ser avaliado qual é melhor forma, e o impacto da troca ou não troca.

### Q-G2 — Consistência `PropertyScope` ↔ `IdentityScope`

Validação não sei, no módulo não vejo um bom lugar, traria um acoplamento muito forte.
A um nível de BFF talvez poderia, mas ainda não vejo necessidade de algo assim.

Uma opção é uma integração automática, um escopo de propriedades é criado no UserAccounts como rascunho, depois de finalizado a configuração e validada, é ativada.
Quando é ativada poderia ter uma integração via eventos que cria o identity scope no IdP.

É de se avaliar algo assim futuramente.