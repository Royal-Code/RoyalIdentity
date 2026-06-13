# Pontos de ajuste - an-users-final

Este documento registra os sete pontos levantados na avaliacao do `an-users-final.md`.

## 1. Deduplicacao de clients em `UserSession`

O esboco usa `HashSet<UserSessionClient>` para deduplicar clients por `ClientId`, mas `UserSessionClient` esta modelado como `class`. Nesse formato, o `HashSet` deduplica por referencia, nao por `ClientId`.

Recomendacao:

- Usar `record` com igualdade por `ClientId`; ou
- usar `Dictionary<string, UserSessionClient>`; ou
- iniciar com `HashSet<string>` para client ids, se a primeira fase quiser reduzir escopo.

O importante e nao voltar para `IList<string>` mutavel sem deduplicacao.

## 2. `FindByLoginAsync` nao deve concentrar policy demais no store

O documento diz que `FindByLoginAsync` aplica `EmailAsUsername` e `LoginWithEmail`. Isso pode colocar regra de login dentro do store.

Recomendacao:

- O store deve buscar por indices claros: subject id, username normalizado, email normalizado, etc.
- A decisao de quais identificadores usar no login deve ficar em `IUserAuthenticator`, `UserLoginResolver` ou policy equivalente.

Assim o store continua sendo persistencia pura.

## 3. Campos de email/login identifier precisam estar claros

O documento menciona opcoes como `EmailAsUsername`, `LoginWithEmail` e `VerifyEmail`, mas o `UserAccount` proposto nao possui `Email`, `NormalizedEmail` ou `EmailVerified`.

Recomendacao:

- Definir campos proprios de email no `UserAccount`; ou
- criar um modelo de login identifiers; ou
- explicitar que email ficara fora desta fase e que essas opcoes nao serao implementadas ainda.

Buscar email dentro de `UserClaim` para login seria fragil.

## 4. Imutabilidade do `SubjectId` deve virar regra verificavel

O `SubjectId` aparece como `init`, mas a imutabilidade precisa ser garantida pela regra de storage/atualizacao e por testes.

Recomendacao:

- O store nao deve permitir salvar/update alterando `SubjectId` de uma conta existente.
- Criar teste especifico para garantir que trocar `Username` nao muda `SubjectId`.
- Criar teste ou regra para rejeitar tentativa de alterar `SubjectId`.

## 5. Fronteira da UI ainda precisa ser mais explicita

O documento diz que a UI nao deve ver `UserAccount`, `UserSession`, `ClaimsPrincipal`, stores ou cookie. Mas o fluxo descrito ainda coloca `LoginPageService` chamando authenticator, session service, principal factory e cookie sign-in.

Recomendacao:

- Criar um servico de aplicacao, como `LoginFlowService` ou `LoginInteractionService`, para orquestrar autenticacao, sessao, principal, cookie e resultado de fluxo.
- Manter `LoginPageService` como adaptador de tela: monta view model e traduz resultado para redirect/render.

Esse ponto e importante porque ataca a dor original: servicos de tela conhecendo comportamento demais.

## 6. Semantica de `ExpiresAt` precisa ser definida ou marcada como reservada

`UserSession.ExpiresAt` e `PasswordCredential.ExpiresAt` aparecem no modelo, mas a semantica ainda nao esta fechada.

Recomendacao:

- Ou marcar esses campos como reservados e sem comportamento nesta fase;
- ou definir no plano como eles interagem com cookie lifetime, `UserSsoLifetime`, password expiration e lockout.

Evitar campos que parecem ativos, mas nao tem regra aplicada.

## 7. Referenciar `an-user-con1.md` ou declarar o consenso escolhido

O `an-users-final.md` lista os documentos que consolida/supersede, mas nao menciona `an-user-con1.md`, embora o arquivo exista no diretorio.

Recomendacao:

- Adicionar `an-user-con1.md` a lista de documentos consolidados; ou
- declarar explicitamente que `an-user-con2.md` foi o consenso escolhido como base final.

E um ajuste documental simples, mas evita confusao historica.
