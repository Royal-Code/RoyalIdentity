# Avaliacao comparativa - an-users-m1 vs an-users-m2

## Escopo

Este documento compara:

- `an-users-m1.md`: primeira analise/proposta.
- `an-users-m2.md`: segunda analise/proposta.

O objetivo e identificar:

- o que as duas analises dizem igual;
- o que so a M1 traz;
- o que so a M2 traz;
- onde divergem;
- qual abordagem e melhor por ponto, com opcoes, pros, contras, impacto e recomendacao.

## Veredito geral

As duas analises convergem fortemente na direcao tecnica correta: sair de um modelo com `IdentityUser` rico, `UserDetails` POCO, stores duplicados e sessao acoplada ao HTTP, para um modelo com dados persistiveis simples, servicos focados e fachadas para a UI.

A M1 e melhor como visao de arquitetura futura e criterios de aceite: cobre mais entidades, mais funcionalidades futuras, fases e testes.

A M2 e melhor como diagnostico operacional imediato: inventaria com mais precisao os fluxos, aponta inconsistencias concretas, explicita a tensao com ADR-005 e transforma alguns pontos em decisoes antes do plano.

Recomendacao geral: usar a M2 como base de decisao e recorte inicial do redesign; incorporar da M1 o modelo mais completo de entidades, a ideia de `IAccountInteractionService`, os criterios de aceite, a matriz de testes e as preparacoes para futuro (`UserCredential`, `ExternalIdentity`, `SecurityStamp`, sessoes por dispositivo).

## Pontos iguais

### Diagnostico central

As duas concordam que o problema nao e uma regra de negocio isolada, mas a distribuicao ruim de responsabilidades.

Pontos iguais:

- `IdentityUser` e `DefaultIdentityUser` carregam comportamento demais.
- `UserDetails` e `IdentityUser` criam dois modelos paralelos de usuario.
- `IUserStore` e `IUserDetailsStore` duplicam a superficie de storage.
- `UserStore` implementa os dois contratos, o que confirma que a separacao atual nao compra isolamento real.
- `IdentitySession` guardar `IdentityUser` e ruim para persistencia, serializacao e clareza.
- `IUserSessionStore.GetCurrentSessionAsync` depender de `HttpContext` e acoplamento incorreto.
- `LoginPageService` ainda conhece demais da maquina de estados do login.
- `SubjectId` e `Username` estao colados, pois `sub` hoje sai de `Username`.
- `IdentityRevalidatingAuthenticationStateProvider` esta desalinhado com o padrao realm-scoped.
- E preciso preservar realm isolation, cookie por realm, `sid`, `auth_time`, `amr`, `idp`, lockout, active check, consentimento e logout SSO.

### Direcao de modelagem

As duas convergem em:

- Um unico registro persistido de usuario.
- Store de usuario devolvendo dados, nao objeto rico com servicos.
- Sessao como dado puro, sem `IdentityUser`.
- Storage sem dependencia de HTTP.
- Autenticacao/credencial/lockout fora da entidade de usuario.
- Criacao de `ClaimsPrincipal` em factory/servico proprio.
- Fachadas/servicos para esconder complexidade da UI.
- Resultado unico de autenticacao em vez de dois resultados quase iguais.
- `SubjectId` imutavel separado de `Username`.

## O que a M1 tem que a M2 nao tem, ou tem menos

### Modelo mais rico de futuro

A M1 detalha melhor uma modelagem mais completa:

- `UserAccount`
- `UserSecurityState`
- `UserCredential`
- `ExternalIdentity`
- `UserSession`
- `UserSessionClient`
- `AuthenticatedSubject`
- `UserClaim`

A M2 propoe um `User` unico mais simples, com senha/lockout ainda dentro do proprio registro.

Melhor ponto da M1: explicita extensoes que o produto provavelmente vai precisar, como login externo, troca/expiracao de senha, MFA, registro, recuperacao de senha, sessoes por dispositivo e claims persistiveis proprias.

### Security stamp

A M1 recomenda `SecurityStamp` em usuario/sessao.

A M2 nao desenvolve esse ponto.

Esse e um ponto importante para:

- invalidar cookies apos troca de senha;
- invalidar sessoes apos alteracao sensivel de credenciais;
- suportar administracao de usuario com revogacao de sessoes.

### Separacao de credencial local

A M1 separa `UserCredential` de `UserAccount`.

A M2 propoe inicialmente manter `PasswordHash`, contadores e lockout dentro de `User`.

O modelo da M1 e mais preparado para:

- login externo;
- passwordless;
- MFA;
- multiplas credenciais;
- password history;
- credencial desabilitada sem desativar a conta.

### ExternalIdentity

A M1 ja inclui `ExternalIdentity`.

A M2 menciona autenticador plugavel e login externo, mas nao modela a identidade externa.

Como a UI ja lista providers externos e clients ja possuem restricoes de IdP, o modelo deveria ao menos reservar esse lugar.

### IAccountInteractionService

A M1 sugere explicitamente uma fachada de interacao:

- construir login;
- processar login local;
- construir consentimento;
- processar consentimento;
- iniciar/confirmar logout.

A M2 fica mais focada em `ISignInManager`/`ISignOutManager` como fachadas.

A ideia da M1 e mais forte para simplificar os servicos Razor.

### Fases, testes e criterios de aceite

A M1 traz:

- proposta de fases;
- testes de unidade/dominio;
- testes de integracao;
- testes de UI flow;
- criterios de aceite.

A M2 e melhor como analise, mas menos completa como trilha de execucao.

## O que a M2 tem que a M1 nao tem, ou tem melhor

### Inventario mais objetivo

A M2 organiza melhor o inventario em tabelas:

- tipos de dominio;
- contratos;
- consumidores de UI.

Isso ajuda a transformar a analise em plano.

### Fluxos atuais mais precisos

A M2 descreve melhor:

- login interativo;
- prompt/login no authorize;
- emissao de token;
- logout;
- tres caminhos atuais de "ativo".

O ponto dos tres caminhos de ativo e especialmente bom:

- `DefaultProfileService.IsActiveAsync` aceita sessao nula como ativo, se a conta esta ativa.
- `HttpContextExtensions.ValidateUserSessionAsync` exige sessao ativa.
- `IdentityRevalidatingAuthenticationStateProvider` verifica so usuario ativo e ignora sessao.

A M1 menciona active check, mas a M2 captura melhor a inconsistencia concreta.

### Lockout split-brain

A M2 nomeia melhor o problema:

- checagem de lockout antes da senha;
- incremento de falha dentro de `AuthenticateAndStartSessionAsync`;
- politica no `DefaultIdentityUser`;
- contador no `UserDetails`;
- orquestracao no `SignInManager`.

A M1 identifica lockout e recomenda `UserSecurityState`, mas a M2 explica melhor o problema no fluxo atual.

### Sessao criada dentro da verificacao de senha

A M2 destaca melhor que `AuthenticateAndStartSessionAsync` cria sessao como efeito colateral da verificacao de credencial.

Isso e relevante para 2FA/login externo: uma credencial primaria valida nao deveria necessariamente criar sessao final antes de completar o fluxo.

### Tensao com ADR-005

A M2 e mais forte e mais correta aqui.

Ela observa que ADR-005 prometia entidades/regras customizaveis por realm e que `IdentityUser` abstrato materializa isso por heranca. Ao propor remover `IdentityUser`, ha uma mudanca de mecanismo: extensibilidade por composicao em vez de heranca.

A M1 preserva a intencao da ADR, mas nao explicita suficientemente que isso deveria gerar nova ADR/superseder.

### Pontos de decisao

A M2 transforma a analise em decisoes:

- introduzir `SubjectId`;
- nova ADR;
- semantica de sessao nula;
- destino de `IdentityUser`;
- remover/rever revalidating provider.

Isso e melhor para governanca do redesign.

### Revalidating provider

A M2 e mais incisiva:

- afirma que `IUserStore` nao esta registrado direto em DI;
- aponta que o provider esta registrado como `AuthenticationStateProvider`;
- conclui que pode quebrar quando revalidar;
- recomenda remover agora e reintroduzir corretamente quando necessario.

A M1 foi mais cautelosa, dizendo que o caminho poderia ser pouco exercitado. A M2 provavelmente e melhor para tomada de acao.

## Divergencias reais

### Nome do modelo principal: UserAccount vs User

M1 recomenda `UserAccount`.

M2 recomenda `User`.

Opcoes:

1. `User`
2. `UserAccount`
3. `IdentityUser` revisado

Pros de `User`:

- Simples.
- Curto.
- Natural para dominio de usuarios.
- Aderente a "um unico registro".

Contras de `User`:

- Nome muito generico.
- Pode confundir com `ClaimsPrincipal`/usuario autenticado do ASP.NET.
- Pode ficar ambiguo entre conta persistida e sujeito autenticado.

Pros de `UserAccount`:

- Mais explicito: conta persistida, nao principal atual.
- Evita colisao conceitual com `HttpContext.User`.
- Combina bem com `AuthenticatedSubject` e `UserSession`.

Contras de `UserAccount`:

- Um pouco mais verboso.
- Pode sugerir que todo usuario e uma "conta local", quando tambem pode haver usuario federado.

Pros de manter `IdentityUser`:

- Menor churn inicial.
- Mantem compatibilidade nominal.

Contras de manter `IdentityUser`:

- Reforca o padrao atual ruim.
- Confunde com ASP.NET Identity.
- Tende a manter comportamento na entidade.

Recomendacao: usar `UserAccount` para o registro persistido. Se o projeto preferir nomes curtos, `User` e aceitavel, mas `UserAccount` reduz ambiguidade em um authorization server onde `User`, `Subject`, `Principal` e `Session` aparecem o tempo todo.

### Profundidade do modelo de credenciais

M1 separa `UserCredential`.

M2 mantem `PasswordHash` e contadores dentro de `User` no primeiro desenho.

Opcoes:

1. Modelo simples: `PasswordHash` e lockout dentro de `UserAccount`.
2. Modelo intermediario: `LocalPasswordCredential` dentro de `UserAccount`.
3. Modelo completo: colecao de `UserCredential` separada.

Pros da opcao 1:

- Menor implementacao.
- Migra mais rapido.
- Bom para fase inicial.

Contras da opcao 1:

- Continua misturando perfil e credencial.
- Vai exigir nova refatoracao para externo/passwordless/MFA/history.

Pros da opcao 2:

- Separa conceitualmente sem criar store extra.
- Suporta bem o estado atual.
- Permite crescer depois para colecao.

Contras da opcao 2:

- Ainda nao resolve multiplas credenciais.
- Exige modelagem nova mesmo sem feature imediata.

Pros da opcao 3:

- Mais preparado para futuro.
- Melhor para login externo, multiplas senhas, MFA, passwordless.

Contras da opcao 3:

- Maior complexidade agora.
- Pode ser overdesign para a primeira fase.

Recomendacao: adotar opcao 2. Criar um submodelo `PasswordCredential` ou `LocalCredential` dentro de `UserAccount`, com hash, failed attempts, lockout, password timestamps e enabled. Nao criar store separado agora. Isso combina a prudencia da M2 com a preparacao da M1.

### Heranca vs composicao

M1 recomenda abandonar `IdentityUser` rico, mas nao trata como decisao ADR.

M2 recomenda nova ADR que supersede ADR-005.

Opcoes:

1. Manter `IdentityUser` abstrato e apenas emagrecer.
2. Remover `IdentityUser` e usar composicao de servicos.
3. Manter `IdentityUser` como adapter temporario obsoleto.

Pros da opcao 1:

- Menor ruptura inicial.
- Preserva leitura literal da ADR-005.

Contras da opcao 1:

- Mantem o ponto de confusao.
- Incentiva dominio por heranca.
- Pode preservar stores que devolvem objetos ricos.

Pros da opcao 2:

- Modelo mais limpo.
- Melhor para persistencia real.
- Aderente ao estilo do projeto: contratos + defaults substituiveis.

Contras da opcao 2:

- Exige decisao arquitetural explicita.
- Maior refatoracao.

Pros da opcao 3:

- Permite migracao incremental.
- Reduz risco de big bang.

Contras da opcao 3:

- Mantem duplicidade por algum tempo.
- Precisa disciplina para remover depois.

Recomendacao: decisao final deve ser opcao 2, registrada em nova ADR. Implementacao pode usar opcao 3 temporariamente como ponte, desde que marcada como obsoleta/redesign e com fase de remocao clara.

### Fachada para UI: ISignInManager ampliado vs IAccountInteractionService

M1 recomenda `IAccountInteractionService`.

M2 recomenda que `ISignInManager` vire orquestrador fino e devolva enum de desfecho.

Opcoes:

1. Engordar `ISignInManager` para cobrir a maquina de estados do login.
2. Criar `IAccountInteractionService` para login/consent/logout de telas.
3. Manter servicos Razor como orquestradores e apenas melhorar dominio.

Pros da opcao 1:

- Menos tipos.
- Mantem nome existente.
- Menor churn.

Contras da opcao 1:

- `ISignInManager` vira mistura de autenticacao e UI flow.
- Consent/logout podem ficar fora ou acoplados de forma estranha.

Pros da opcao 2:

- Melhor fronteira para UI.
- Centraliza fluxo de interacao.
- Deixa `IUserAuthenticationService`/`ISignInService` focados em autenticacao.
- Ajuda futuras telas, localizacao e admin.

Contras da opcao 2:

- Novo contrato.
- Pode parecer camada a mais se mal recortado.

Pros da opcao 3:

- Menor mudanca imediata.
- Ja funciona parcialmente.

Contras da opcao 3:

- Nao resolve a queixa central do usuario.
- Mantem regra de fluxo espalhada em servicos Razor.

Recomendacao: opcao 2. Criar `IAccountInteractionService` ou nome equivalente para fluxo de telas. Manter `IUserAuthenticationService` e `IUserSessionService` como dominio/aplicacao. `ISignInManager` atual pode ser decomposto ou mantido temporariamente como adapter.

### Semantica de sessao nula

M1 recomenda cookie/session validation rejeitando sessao inexistente.

M2 explicita separar "conta ativa" de "sessao valida" e recomenda sessao ausente como invalida quando ha `sid`.

Opcoes:

1. Sessao ausente conta como ativa no `ProfileService`.
2. Sessao ausente sempre invalida quando a checagem envolve principal com `sid`.
3. Separar APIs: `IsAccountActive` e `IsSessionValid`.

Pros da opcao 1:

- Mais compativel com comportamento atual em alguns caminhos.
- Evita falha se a sessao nao foi persistida por algum motivo.

Contras da opcao 1:

- Enfraquece logout.
- Fica ruim para storage com TTL/cache.
- Permite tokens com principal cujo `sid` nao existe mais.

Pros da opcao 2:

- Mais seguro.
- Mais previsivel.
- Alinha cookie, token e refresh.

Contras da opcao 2:

- Pode quebrar fluxos ou testes que hoje usam principals fabricados sem sessao real.

Pros da opcao 3:

- Expressa a regra corretamente.
- Evita ambiguidades entre "usuario existe/ativo" e "login/sessao ainda vale".

Contras da opcao 3:

- Exige ajustar consumidores.

Recomendacao: opcao 3 como desenho, com comportamento da opcao 2 para checagens de sessao. Em outras palavras: conta ativa e uma pergunta; sessao valida e outra. Quando ha `sid` e o fluxo depende de autenticacao de usuario, sessao ausente deve ser invalida.

### RevalidatingAuthenticationStateProvider

M1 identifica desalinhamento.

M2 recomenda remover agora.

Opcoes:

1. Remover agora.
2. Corrigir agora para usar realm + `IStorage`.
3. Manter ate redesign maior.

Pros da opcao 1:

- Remove codigo potencialmente quebrado.
- Evita DI incorreto.
- Simples.

Contras da opcao 1:

- Se alguma area interativa depender disso, perde revalidacao periodica.

Pros da opcao 2:

- Mantem recurso.
- Endurece seguranca em area interativa.

Contras da opcao 2:

- Pode exigir solucao cuidadosa para realm em circuito interactive server.
- Pode ser trabalho fora do recorte inicial.

Pros da opcao 3:

- Zero churn.

Contras da opcao 3:

- Mantem codigo potencialmente quebrado.
- Ignora realm.

Recomendacao: seguir M2, mas validar uso antes de remover. Se nao houver area interativa autenticada que dependa dele, remover. Se houver, substituir por versao correta apoiada em `IUserSessionService` e realm capturado adequadamente.

### Modelo de claims

M1 recomenda `UserClaim` proprio.

M2 fala em claims/roles no `User`, mas nao aprofunda.

Opcoes:

1. Manter `HashSet<Claim>`.
2. Criar `UserClaim` persistivel e converter na borda.
3. Criar estrutura de perfil tipada para claims OIDC padrao.

Pros da opcao 1:

- Menor mudanca.
- Integracao direta com token code.

Contras da opcao 1:

- Tipo de framework no dominio persistido.
- Igualdade/serializacao menos claras.

Pros da opcao 2:

- Melhor para storage.
- Mais claro para admin/API.
- Ainda flexivel.

Contras da opcao 2:

- Exige conversores.

Pros da opcao 3:

- Melhor UX/admin para dados padrao.
- Validacao mais forte.

Contras da opcao 3:

- Mais complexidade.
- Pode engessar realms customizados.

Recomendacao: opcao 2 agora. Futuramente adicionar perfil tipado se houver tela/admin que justifique.

### SecurityStamp

M1 propoe, M2 nao enfatiza.

Opcoes:

1. Nao incluir agora.
2. Incluir campo no modelo, sem aplicar em todos os fluxos inicialmente.
3. Incluir e aplicar imediatamente em sessao/cookie validation.

Pros da opcao 1:

- Menor escopo.

Contras da opcao 1:

- Perde oportunidade de preparar invalidacao de sessoes.
- Vai exigir nova mudanca em sessao depois.

Pros da opcao 2:

- Prepara modelo.
- Baixo impacto.
- Permite migrar aos poucos.

Contras da opcao 2:

- Campo pode ficar sem efeito se nao houver tarefa clara para usa-lo.

Pros da opcao 3:

- Seguranca mais forte desde ja.

Contras da opcao 3:

- Mais testes e risco.
- Pode ampliar demais a primeira fase.

Recomendacao: opcao 2 no modelo inicial, com tarefa explicita posterior para validar security stamp em cookie/session validation.

## Melhor abordagem por area

### Entidade de usuario

Melhor: combinar M1 e M2.

Recomendacao:

- Usar `UserAccount`.
- Ter `SubjectId` imutavel.
- Manter `Username`, `DisplayName`, `IsActive`, `Roles`, `Claims`.
- Separar estado de seguranca em `UserSecurityState`.
- Separar credencial local em `PasswordCredential` dentro de `UserAccount`.

Justificativa:

- M2 esta certa sobre um unico registro.
- M1 esta certa sobre evitar que esse registro vire novamente um saco misturado de perfil, credencial e seguranca.

### Store de usuario

Melhor: M2 para simplificacao, com nome da M1.

Recomendacao:

- Criar `IUserAccountStore`.
- Remover/fundir `IUserDetailsStore`.
- `IUserStore` atual deve virar adapter temporario ou ser substituido.
- Store devolve dados, nao `IdentityUser`.

Impacto:

- Medio.
- Afeta profile, login, revalidation, testes e in-memory storage.

### Autenticacao local

Melhor: M2 para ordem de responsabilidades, M1 para nomeacao mais clara.

Recomendacao:

- Criar `IUserAuthenticationService` ou `IUserAuthenticator`.
- Validar credencial sem criar sessao como efeito colateral.
- Criar sessao so quando o fluxo de sign-in realmente concluir.
- Lockout em policy unica.

Impacto:

- Medio.
- Melhora extensibilidade para MFA/externo.

### Sessao

Melhor: consenso M1/M2.

Recomendacao:

- `UserSession` persistivel com `SessionId`, `SubjectId`, `RealmId`, `IdentityProvider`, `AuthenticationMethods`, `AuthenticatedAt`, `IsActive`, `EndedAt`, `Clients`.
- `Clients` como conjunto/deduplicado, idealmente `UserSessionClient`.
- Store sem `HttpContext`.
- `IUserSessionService` para current session, validacao, start/end e record client.

Impacto:

- Medio/alto, pois toca cookie validation, code factory, profile service e logout.

### Claims principal

Melhor: consenso com reforco M1.

Recomendacao:

- Criar `ISubjectPrincipalFactory`.
- Cookie principal continua com claims obrigatorias.
- Claims persistidas usam `UserClaim` e convertem para `Claim` na borda.

Impacto:

- Medio.
- Reduz duplicidade e melhora storage futuro.

### UI services

Melhor: M1.

Recomendacao:

- Criar `IAccountInteractionService`.
- `LoginPageService`, `ConsentPageService`, `EndSessionPageService` ficam adaptadores finos ou delegam quase tudo.
- UI recebe view models e flow results, sem conhecer usuario/sessao/stores.

Impacto:

- Medio.
- Resolve diretamente a dor descrita na tarefa.

### ADR/governanca

Melhor: M2.

Recomendacao:

- Criar nova ADR, provavelmente ADR-013, supersedendo ou refinando ADR-005.
- Registrar que a customizacao por realm passa a ser por composicao/servicos substituiveis, nao por heranca de entidade rica.
- Registrar `SubjectId` imutavel separado de username.

Impacto:

- Baixo em codigo, alto em clareza.

## Decisoes recomendadas antes do plano

### D1 - O usuario principal sera `UserAccount`

Recomendacao: sim.

Justificativa: evita ambiguidade com `HttpContext.User` e com `ClaimsPrincipal`, alem de deixar claro que e dado persistido.

### D2 - `SubjectId` sera imutavel e separado de `Username`

Recomendacao: sim, fazer no redesign.

Justificativa: e a correcao protocolar mais importante; ainda nao ha storage persistente real, entao o timing e bom.

### D3 - Extensibilidade por realm sera por composicao

Recomendacao: sim, registrar em ADR.

Justificativa: combina melhor com a arquitetura do projeto: contratos, defaults, pipeline e storage realm-scoped.

### D4 - `IdentityUser` sera removido como modelo final

Recomendacao: sim.

Justificativa: manter o tipo preserva a confusao. Pode existir adapter temporario, mas nao deve ser destino final.

### D5 - Store de sessao nao tera conceito de current session

Recomendacao: sim.

Justificativa: current session depende de principal/HTTP; store e persistencia.

### D6 - Sessao ausente sera invalida para fluxos autenticados

Recomendacao: sim.

Justificativa: logout, TTL e seguranca ficam mais consistentes.

### D7 - Revalidating provider sera removido ou refeito

Recomendacao: remover se nao houver uso interativo autenticado relevante; refazer se houver.

Justificativa: como esta, ignora realm e usa DI incorreto.

## Recomendacao consolidada

Usar a seguinte sintese como direcao do redesign:

1. Escrever nova ADR antes da implementacao, registrando composicao sobre heranca e `SubjectId` imutavel.
2. Criar `UserAccount` como modelo unico persistivel.
3. Dentro de `UserAccount`, separar perfil, claims/roles, `UserSecurityState`, `PasswordCredential` e identidades externas.
4. Criar `IUserAccountStore`, substituindo `IUserStore`/`IUserDetailsStore`.
5. Criar `UserSession` persistivel sem referencia a usuario rico.
6. Criar `IUserSessionStore` puro e `IUserSessionService` para regras/current session.
7. Criar `IUserAuthenticationService`, `LockoutPolicy` e `ISubjectPrincipalFactory`.
8. Criar `IAccountInteractionService` para isolar login/consent/logout da UI.
9. Remover ou adaptar temporariamente `IdentityUser`, `UserDetails`, `ValidateCredentialsResult` e `CredentialsValidationResult`.
10. Usar a matriz de testes/criterios da M1, ajustada com as decisoes objetivas da M2.

## Impacto esperado

### Alto

- Separacao `SubjectId`/`Username`.
- Remocao final de `IdentityUser`.
- Mudanca de `IUserStore`/`IUserDetailsStore` para `IUserAccountStore`.

### Medio

- Revisao de sessao.
- Extracao de autenticacao/lockout/principal factory.
- Mudanca de `ProfileService`, cookie validation, code factory e logout.
- Reducao dos servicos Razor.

### Baixo

- Nova ADR.
- Remocao/correcao do revalidating provider, desde que nao exista area interativa autenticada dependendo dele.
- Introducao inicial de campos como `SecurityStamp`, se ainda nao forem usados em comportamento critico.

## Pontos que merecem cuidado no plano

1. Criar testes de caracterizacao antes de mudar login/sessao.
2. Garantir que consentimentos continuem por `SubjectId`, nao username.
3. Atualizar seeds para IDs deterministicos em testes.
4. Evitar big bang: adapters temporarios sao aceitaveis, mas precisam de fase de remocao.
5. Decidir semantica de sessao nula antes de mexer em `ProfileService`.
6. Conferir se `IdentityRevalidatingAuthenticationStateProvider` e exercitado em alguma tela interactive server autenticada.
7. Manter mensagens genericas de erro de login para evitar enumeracao.
8. Manter `sid`, `auth_time`, `amr`, `idp`, `sub` e `name` no cookie/principal.
9. Manter registro de client na sessao durante emissao de code.
10. Manter front/back-channel logout baseado nos clients da sessao.

## Conclusao

A M2 deve guiar as decisoes imediatas porque e mais precisa no diagnostico dos fluxos atuais, na tensao com ADR-005 e nos pontos que precisam ser decididos antes do plano.

A M1 deve complementar o desenho final porque oferece uma modelagem mais completa e uma trilha melhor de execucao/testes.

O melhor resultado e uma sintese: arquitetura por composicao e dados puros da M2, com o modelo mais preparado e os criterios de aceite da M1.

## Validacao

Esta avaliacao foi feita por leitura de `an-users-m1.md` e `an-users-m2.md`. Nao foi executado build/teste, pois a entrega e documental.
