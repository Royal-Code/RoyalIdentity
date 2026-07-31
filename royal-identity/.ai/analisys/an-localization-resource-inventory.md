# Análise: inventário de recursos de Localization

## Objetivo

Registrar a quantidade de mensagens fixas da UI do OP que precisam ser localizadas, o agrupamento das chaves e
o número de arquivos `.resx` necessários para o primeiro corte de `plan-localization.md`.

Esta análise não redefine o design do plano. Ela fixa a baseline quantitativa para execução e testes de paridade
dos catálogos.

## Fontes verificadas

- `RoyalIdentity.Razor/Components/**/*.razor` — textos visíveis, títulos, labels, placeholders, atributos de
  acessibilidade e mensagens de estado.
- `RoyalIdentity.Razor/Services/*.cs` — mensagens produzidas por login, consentimento e logout.
- `RoyalIdentity.Razor/ViewModels/*.cs` — DataAnnotations e campos usados em mensagens de validação.
- `RoyalIdentity/Users/LoginFlowResult.cs` e `RoyalIdentity/Users/Defaults/LoginFlowService.cs` — mensagens que
  hoje atravessam a borda core/UI.
- `RoyalIdentity/Options/AccountOptions.cs` — três mensagens configuráveis marcadas com
  `[Redesign("Usar Resource")]`.
- `plan-localization.md` — seletor de idioma, códigos estáveis, catálogos `AccountResources` e
  `ValidationResources`, textos de acessibilidade e limites do escopo.
- [Unicode CLDR — Spanish locales](https://unicode.org/cldr/charts/41/summary/es.html) — reconhece `es_419`
  como Latin American Spanish / español latinoamericano.
- [IANA Language Subtag Registry](https://www.iana.org/assignments/language-subtags-tags-extensions/language-subtags-tags-extensions.xhtml)
  — tags de idioma seguem BCP 47/RFC 5646; `419` é o código regional usado para a América Latina.

Verificação local de runtime:

```powershell
[System.Globalization.CultureInfo]::GetCultureInfo("es-419")
```

Resultado observado em 2026-07-30:

```text
Name        es-419
EnglishName Spanish (Latin America)
NativeName  español (Latinoamérica)
IsNeutral   False
Parent      es
```

## Método de contagem

Uma mensagem corresponde a uma chave semântica entregue ao tradutor, não a cada ocorrência no markup.

Exemplos:

- `Login_Title` atende `PageTitle`, `<h1>` e botão quando todos significam “Log in”.
- `Error_Title` atende título, heading e fallback “Error”.
- Strings com significado diferente não são consolidadas apenas por terem palavras parecidas.
- Mensagens fragmentadas como `Click` + `here` + `to return...` viram textos completos, para permitir ordem
  gramatical correta em outros idiomas.
- Placeholders, `title`, `alt`, `aria-label` e nomes de campos usados em validação contam como recursos.
- Argumentos dinâmicos usam placeholders como `{0}`; o dado dinâmico não vira tradução.

O inventário bruto do código atual contém aproximadamente 61 frases/templates fixos distintos. O catálogo alvo
tem 62 chaves porque:

1. ocorrências repetidas são consolidadas;
2. fragmentos de frases são substituídos por mensagens completas;
3. validação separa o template `Required` dos display names dos campos;
4. o plano adiciona label/ação do seletor de idioma.

## Exclusões

Não entram nos catálogos:

- `Client.Name`, nomes de provedores externos e display names/descrições de scopes/resources;
- URLs, resource indicators, nomes de claims e valores fornecidos pelo tenant;
- códigos OAuth/OIDC como `invalid_request`, nomes de parâmetros e constantes de protocolo;
- logs, exceptions internas, mensagens de diagnóstico e comentários;
- valores de claims e conteúdo localizado cadastrado pelo tenant;
- textos da RP legada `Tests.WebApp`;
- mensagens do futuro Admin.

Esses itens são conteúdo dinâmico, protocolo invariável, diagnóstico técnico ou escopo de outro plano.

## Resultado

| Catálogo/área | Chaves |
|---|---:|
| Comum, layout e erro | 8 |
| Seleção de domínio | 4 |
| Login | 9 |
| Login concluído | 4 |
| Consentimento | 20 |
| Logout | 9 |
| Perfil | 3 |
| Validações | 5 |
| **Total por idioma** | **62** |

Distribuição lógica:

| Catálogo | Chaves por idioma |
|---|---:|
| `AccountResources` | 57 |
| `ValidationResources` | 5 |
| **Total** | **62** |

## Inventário proposto de chaves

### Comum, layout e erro — 8

```text
Common_Continue
Common_Loading
Common_Language
Common_ApplyLanguage
Branding_DefaultLogoAlt
Error_Title
Error_Generic
Error_RequestIdLabel
```

### Seleção de domínio — 4

```text
Domain_Title
Domain_Label
Domain_Placeholder
Domain_NotFound
```

`Domain_Required` não é duplicada aqui; a obrigatoriedade usa `Validation_Required` +
`Field_Domain`.

### Login — 9

```text
Login_Title
Login_Username
Login_Password
Login_RememberMe
Login_ForgotPassword
Login_ExternalHeading
Login_ExternalProviderTitle
Login_InvalidCredentials
Login_InvalidReturnUrl
```

`Login_ExternalProviderTitle` recebe o display name do provider como argumento. `Login_InvalidCredentials` é a
única apresentação para senha inválida, conta inexistente, inativa ou bloqueada.

### Login concluído — 4

```text
SignedIn_Title
SignedIn_WelcomeBack
SignedIn_Success
SignedIn_ReturnToApplication
```

### Consentimento — 20

```text
Consent_Title
Consent_ClientLogoAlt
Consent_RequestingPermission
Consent_UncheckPermissions
Consent_PersonalInformation
Consent_Required
Consent_ProtectedResourcesTitle
Consent_OfflineAccessTitle
Consent_OfflineAccessDescription
Consent_DescriptionLabel
Consent_DescriptionPlaceholder
Consent_RememberDecision
Consent_Allow
Consent_Deny
Consent_GrantedTitle
Consent_PermissionsGranted
Consent_Redirecting
Consent_RequestNotFound
Consent_RememberNotAllowed
Consent_RequiredScopeNotGranted
```

Nomes/descrições de client, identity scopes, resource servers, scopes e protected resources permanecem dados do
tenant e não entram neste catálogo.

### Logout — 9

```text
Logout_Title
Logout_LoggingOutTitle
Logout_Processing
Logout_ReturnToClient
Logout_ReturnToApplication
Logout_LoggedOutTitle
Logout_LoggedOutMessage
Logout_IdRequired
Logout_IdNotFound
```

`Logout_ReturnToClient` recebe o nome do client como argumento. A implementação não deve remontar uma frase
traduzida a partir de fragmentos `Click`/`here`/`to return`.

### Perfil — 3

```text
Profile_Title
Profile_Greeting
Profile_UserInformation
```

`Profile_Greeting` recebe o display name do usuário como argumento.

### Validações — 5

```text
Validation_Required
Field_Username
Field_Password
Field_ReturnUrl
Field_Domain
```

`Validation_Required` contém o placeholder do display name. Campos adicionados posteriormente exigem atualizar
esta contagem e os testes de paridade.

## Culturas do primeiro corte

Recomendação:

```text
en       catálogo neutro/autoral em inglês
pt-BR    português do Brasil
es-419   espanhol da América Latina
```

`es-419` é preferível a escolher um país específico:

- representa explicitamente a América Latina;
- evita tornar `es-MX`, `es-AR`, `es-CO` ou outro país o dialeto padrão do produto;
- é reconhecido por `CultureInfo` no ambiente local e por CLDR;
- permite adicionar variantes nacionais posteriormente sem renomear a baseline regional.

Como `es-MX`, `es-AR` e outras culturas têm parent `es`, e não `es-419`, o runtime não usará automaticamente
`es-419` como fallback dessas tags. O resolver realm-aware deve aplicar esta regra:

1. preferir match exato configurado;
2. tentar parents configurados;
3. quando não houver match e existir exatamente uma variante suportada do mesmo idioma, usá-la como fallback;
4. continuar a precedência normal quando houver ambiguidade.

Assim, `es-MX` pode selecionar `es-419` enquanto ele for a única variante espanhola oferecida. Se um futuro
catálogo `es-ES` for adicionado, match exato continua vencendo e casos ambíguos deixam de ser inferidos.

## Arquivos RESX

Com inglês neutro, `pt-BR` e `es-419`, o primeiro corte usa seis arquivos físicos:

```text
RoyalIdentity.Razor/Resources/
  AccountResources.resx
  AccountResources.pt-BR.resx
  AccountResources.es-419.resx
  ValidationResources.resx
  ValidationResources.pt-BR.resx
  ValidationResources.es-419.resx
```

Contagem física:

| Arquivo | Entradas |
|---|---:|
| `AccountResources.resx` | 57 |
| `AccountResources.pt-BR.resx` | 57 |
| `AccountResources.es-419.resx` | 57 |
| `ValidationResources.resx` | 5 |
| `ValidationResources.pt-BR.resx` | 5 |
| `ValidationResources.es-419.resx` | 5 |
| **Total** | **186** |

Portanto:

- 2 catálogos lógicos;
- 3 culturas;
- 6 arquivos `.resx`;
- 62 chaves por idioma;
- 186 entradas físicas.

Cada idioma adicional acrescenta dois arquivos e 62 entradas.

## Critério de manutenção

A contagem só permanece válida enquanto o escopo da UI não mudar. A execução do plano deve:

1. atualizar este inventário quando adicionar/remover uma chave;
2. exigir as mesmas chaves nos três arquivos de cada catálogo;
3. validar placeholders idênticos entre culturas;
4. falhar quando um código de apresentação não possuir recurso;
5. manter uma allowlist pequena para strings técnicas que não são apresentação.

Variações editoriais que não criam novo significado alteram valores, não a quantidade de chaves.
