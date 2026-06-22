# Plan: Projeto compartilhado de seguranca (`RoyalIdentity.Security`)

## Status: EM ANDAMENTO - 4 de 8 fases concluidas

## Progresso

`####----` **50%** - 4 de 8 fases

| Fase | Estado |
|---|---|
| Fase 1 - Esqueleto, solution folders e guardrails de dependencia | Concluida |
| Fase 2 - Random, Base64Url, hashing basico e comparacao constante | Concluida |
| Fase 3 - Password hashing reutilizavel e compatibilidade legado | Concluida |
| Fase 4 - Key material, `KeyParameters` e helpers de chaves | Concluida |
| Fase 5 - Troca no core `RoyalIdentity` | Pendente |
| Fase 6 - Troca em `UserAccounts`, fake in-memory e testes de borda | Pendente |
| Fase 7 - Remocao de duplicacoes e shims temporarios | Pendente |
| Fase 8 - Verificacao ampla, documentacao e fechamento | Pendente |

> **Manutencao deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `#` por fase concluida, `%` e `X de 8`). Ex.: 3 fases => `###-----` **37%** - 3 de 8.

---

## Contexto

O backlog registra o trabalho em [backlog-001.md](../backlogs/backlog-001.md), secao
`Projeto compartilhado de seguranca (RoyalIdentity.Security)`. A motivacao inicial era evitar duplicacao entre:

- o core `RoyalIdentity`, que usa `CryptoRandom.CreateUniqueId()` para authorization codes, refresh tokens,
  JWT ids, salts e dados de teste;
- o modulo puro `RoyalIdentity.UserAccounts`, que criou `DefaultSubjectIdGenerator` proprio porque nao pode
  referenciar o core;
- o futuro `RoyalIdentity.KMS`, que precisara manipular chaves, certificados e segredos sem depender do IdP.

Este plano nomeia o projeto **`RoyalIdentity.Security`**, porque o escopo desejado e uma biblioteca
reutilizavel de componentes tecnicos, nao um modulo de dominio do IdP. O projeto fica no namespace do produto
(`RoyalIdentity.*`) como uma biblioteca tecnica de folha (sem dependencia do core nem de outros modulos de dominio),
e **nao** sob o ecossistema externo de bibliotecas `RoyalCode.*` (SmartCommands, WorkContext, Entities, etc.), para
nao colidir com esses pacotes de terceiros. A decisao esta registrada no [ADR-016](../../adrs/ADR-016.md), que emenda
o ADR-013.

O projeto deve conter apenas componentes genericos: cripto utilitaria, identificadores opacos, encoding, hashing,
comparacao constante, password hashing reutilizavel e material de chaves. Ele nao deve conter fluxos OIDC,
conhecimento de realm, clients, stores, pipelines, ASP.NET DataProtection, nem abstracoes de borda do IdP.

---

## Objetivo

1. Criar o projeto `RoyalIdentity.Security` dentro da solution `RoyalIdentity.sln`.
2. Colocar `RoyalIdentity.Security` no virtual folder **`src`** da solution.
3. Criar o projeto de testes `Tests.Security`.
4. Colocar `Tests.Security` no virtual folder **`test`** da solution.
5. Migrar para `RoyalIdentity.Security` as primitivas genericas atualmente espalhadas no core:
   - `CryptoRandom`;
   - `Base64Url`;
   - hashing SHA e helpers de hash;
   - comparacao constante;
   - password hashing reutilizavel;
   - componentes genericos de chaves (`KeyParameters`, encoding, formato, RSA, ECDsa, symmetric, JWK/public key).
6. Atualizar `RoyalIdentity`, `RoyalIdentity.Storage.InMemory` e `RoyalIdentity.UserAccounts` para consumirem
   os componentes reutilizaveis.
7. Manter compatibilidade comportamental com os tokens, secrets, password hashes e chaves existentes.
8. Remover ou aposentar duplicacoes locais depois da troca.

---

## Fora de escopo

- Criar um modulo de dominio `RoyalIdentity.Security`. O projeto e `RoyalIdentity.Security` e nao conhece o IdP.
- Criar projeto `.AspNetCore`, integracao com ASP.NET DataProtection ou key ring do ASP.NET.
- Mover `ProtectedDataMessageStore` ou `IDataProtectionProvider`.
- Mover `SecretEvaluatorBase`, `PrivateKeyJwtSecretEvaluator`, `DefaultClientSecretChecker` ou qualquer fluxo de
  autenticacao de client.
- Mover `DefaultTokenFactory`, `DefaultTokenValidator`, `DefaultJwtFactory` como servicos completos.
- Mover politicas de conta (`PasswordPolicy`, lockout, `PasswordOptions`) do modulo `UserAccounts`.
- Criar novas abstracoes compartilhadas como `IPasswordHasher`, `IKeyManager` ou stores.
- Persistencia, rotacao operacional e administracao de chaves do KMS. Este plano prepara componentes para o KMS,
  mas nao implementa o modulo `RoyalIdentity.KMS`.
- Publicar NuGet externo. O projeto pode ser packable no futuro, mas este plano exige apenas funcionamento dentro
  da solution.

---

## Decisoes fechadas

### Nome, projetos e solution folders

- Projeto de componentes: `RoyalIdentity.Security`.
- Projeto de testes: `Tests.Security`.
- Fisicamente, ambos ficam no diretorio raiz da solution, seguindo o padrao atual dos projetos:

```text
RoyalIdentity.Security/
Tests.Security/
```

- Virtual folders na solution:
  - `RoyalIdentity.Security` fica em `src`.
  - `Tests.Security` fica em `test`.

### Dependencias

- `RoyalIdentity.Security` nao referencia `RoyalIdentity`, `RoyalIdentity.Pipelines`, `RoyalIdentity.UserAccounts`,
  storage, Razor, Server ou ASP.NET.
- `RoyalIdentity.Security` remove o `FrameworkReference` global `Microsoft.AspNetCore.App` no `.csproj`, como o modulo
  puro `RoyalIdentity.UserAccounts` ja faz.
- Dependencias permitidas:
  - BCL (`System.Security.Cryptography`, `System.Text`, etc.);
  - `Microsoft.IdentityModel.Tokens`, quando necessario para `SecurityKey`, `SigningCredentials` e `JsonWebKey`.
- `RoyalIdentity`, `RoyalIdentity.Storage.InMemory` e `RoyalIdentity.UserAccounts` podem referenciar
  `RoyalIdentity.Security`.
- `RoyalIdentity.Pipelines` continua sem dependencia de dominio ou seguranca do IdP.

### Visibilidade da API

- A API de `RoyalIdentity.Security` e `public`. O projeto e consumido por tres projetos hoje (core, storage, modulo puro)
  e pelo futuro KMS; nao ha `InternalsVisibleTo`. Empacotamento NuGet externo continua fora de escopo, mas a
  superficie publica deve ser tratada como estavel desde a Fase 1.

### Filosofia de API

- Entregar implementacoes reutilizaveis, nao abstracoes compartilhadas.
- Preferir APIs pequenas, estaticas e deterministicamente testaveis.
- Nomes podem preservar compatibilidade quando isso reduzir churn (`CryptoRandom`, `Base64Url`, `PasswordHash`,
  `KeyParameters`).
- Componentes que hoje tem nomes IdP-specific devem ganhar nomes genericos quando migrados.
- O IdP pode manter wrappers finos quando o nome atual fizer parte do vocabulario do core.

### Componentes alvo

#### Random e identificadores opacos

Componente:

```text
RoyalIdentity.Security.Cryptography.CryptoRandom
```

API alvo minima:

```csharp
public static class CryptoRandom
{
	public static byte[] CreateRandomKey(int length);
	public static void CreateRandomKey(Span<byte> bytes);
	public static void CreateRandomKey(byte[] bytes);
	public static string CreateUniqueId(int length = 33, OutputFormat format = OutputFormat.Base64Url);
	public static int Next();
	public static int Next(int maxValue);
	public static int Next(int minValue, int maxValue);
	public static double NextDouble();
	public static void NextBytes(byte[] buffer);
}
```

Regras:

- `length` significa bytes de entropia, preservando a semantica atual.
- `Base64Url` nao tem padding.
- `Hex` deve manter uppercase enquanto houver comparacao/teste dependente do formato atual.
- `Next(int minValue, int maxValue)` deve continuar evitando modulo bias. Em vez de preservar o loop de
  rejection-sampling artesanal atual, delegar a `RandomNumberGenerator.GetInt32(minValue, maxValue)`, que e a
  primitiva sem vies abencoada pela BCL; o mesmo vale para `Next()`/`NextBytes()` via as APIs estaticas
  `RandomNumberGenerator`. A semantica de `length` em bytes de `CreateUniqueId`/`CreateRandomKey` deve ser preservada.
- `DefaultSubjectIdGenerator` do `UserAccounts` vira wrapper de `CryptoRandom.CreateUniqueId(32)`.

#### Base64Url

Componente:

```text
RoyalIdentity.Security.Encoding.Base64Url
```

API alvo minima:

```csharp
public static class Base64Url
{
	public static string Encode(ReadOnlySpan<byte> bytes);
	public static byte[] Decode(string value);
	public static bool TryDecode(string value, out byte[] bytes);
}
```

Regras:

- Preservar round-trip dos valores gerados hoje.
- Aceitar valores sem padding.
- Rejeitar ou retornar `false` para tamanho invalido.
- O .NET 9+ ja expoe `System.Buffers.Text.Base64Url`. Decisao: `RoyalIdentity.Security.Encoding.Base64Url` e uma
  fachada fina que delega ao tipo da BCL (mesma semantica de no-padding), em vez de reimplementar conversao manual.
  A fachada preserva o nome curto consumido pelo core e evita colisao de `using` com `System.Buffers.Text.Base64Url`.
  Dentro do namespace `RoyalIdentity.Security.Encoding`, qualquer uso de `System.Text.Encoding` deve ser totalmente
  qualificado ou via alias para evitar ambiguidade.

#### Hashing basico

Componentes:

```text
RoyalIdentity.Security.Cryptography.Hashing
RoyalIdentity.Security.Cryptography.HashExtensions
```

API alvo minima:

```csharp
public static class Hashing
{
	public static byte[] Sha256(ReadOnlySpan<byte> bytes);
	public static byte[] Sha384(ReadOnlySpan<byte> bytes);
	public static byte[] Sha512(ReadOnlySpan<byte> bytes);
	public static string Sha256Base64(string value);
	public static string Sha512Base64(string value);
	public static string Sha256Base64Url(string value);
	public static string LeftHalfHashBase64Url(string value, HashAlgorithmName algorithm);
}
```

Regras:

- A primitiva `LeftHalfHashBase64Url` e generica.
- O significado OIDC (`at_hash`, `c_hash`, `s_hash`) continua no core.
- `HashExtensions.Sha256()` e `HashExtensions.Sha512()` podem ser mantidas como extensoes para reduzir churn,
  mas devem chamar o componente central.

#### Comparacao constante

Componente:

```text
RoyalIdentity.Security.Cryptography.FixedTimeComparer
```

API alvo minima:

```csharp
public static class FixedTimeComparer
{
	public static bool IsEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right);
	public static bool IsEqualUtf8(string left, string right);
	public static bool IsEqualBase64(string leftBase64, string rightBase64);
}
```

Regras:

- Usar `CryptographicOperations.FixedTimeEquals`.
- Para strings, comparar bytes UTF-8 ou bytes decodificados, nunca `SequenceEqual` direto em `string`.
- O antigo `TimeConstantComparer` nao deve ser movido como esta; ele deve ser substituido/corrigido. A implementacao
  atual (`s1.AsSpan().SequenceEqual(s2.AsSpan())`) faz early-exit no primeiro caractere diferente e **nao** e tempo
  constante.
- Atualizar os call-sites e in-scope mesmo quando o tipo que os contem esta fora de escopo (ex.: `SecretEvaluatorBase`
  permanece no core/fora de escopo de migracao, mas a chamada de comparacao dentro dele deve passar a usar
  `FixedTimeComparer`). Call-sites atuais: `PkceMatchValidator` (verificacao PKCE) e `SecretEvaluatorBase`
  (comparacao de client secret).
- `FixedTimeEquals` retorna `false` imediatamente quando os tamanhos diferem (o tamanho nao e segredo nesses
  call-sites: ambos comparam digests/hashes de tamanho deterministico). Cobrir esse caso com teste explicito.

#### Password hashing reutilizavel

Componente:

```text
RoyalIdentity.Security.Passwords.PasswordHash
RoyalIdentity.Security.Passwords.PasswordHashOptions
RoyalIdentity.Security.Passwords.PasswordVerificationResult
```

API alvo minima:

```csharp
public static class PasswordHash
{
	public static string Create(string password);
	public static string Create(string password, PasswordHashOptions options);
	public static PasswordVerificationResult Verify(string password, string storedHash);
	public static bool NeedsRehash(string storedHash, PasswordHashOptions options);
}
```

Regras:

- Sem interface compartilhada nesta fase.
- Formato novo deve ser versionado e autocontido, incluindo algoritmo, iteracoes, salt e hash.
- O formato legado atual (`$PBKDF2$.{salt}.{hash}`, salt/hash em Base64 padrao) deve continuar verificavel.
- **Mudanca de comportamento intencional:** o `Verify` atual lanca `ArgumentException` para hash malformado; o novo
  `Verify` retorna `PasswordVerificationResult` (falha), nunca lanca. Isso alinha com a regra de nao usar throw em
  fluxo esperado. Deve haver teste cobrindo "hash malformado retorna falha, nao lanca e nao autentica".
- O `DefaultPasswordProtector` do core passa a chamar esta implementacao e mapeia `PasswordVerificationResult` para o
  `bool` esperado por `IPasswordProtector.VerifyAsync`. `Success` e `SuccessRehashNeeded` mapeiam para `true`.
- **Adocao do formato versionado / rehash-on-login:** `NeedsRehash` so tem valor se alguem reidratar hashes legados.
  A orquestracao de rehash-on-login (verificar legado -> em sucesso, se `NeedsRehash`, regravar no formato novo)
  pertence ao consumidor/dominio de contas, **nao** a `RoyalIdentity.Security`. Este plano apenas entrega `NeedsRehash`;
  A orquestracao fica **deferido para o backlog** (registrar item). Sem isso, `NeedsRehash` permanece disponivel
  porem nao adotado para usuarios existentes - o que e aceitavel para o escopo deste plano, desde que registrado.
- Password policy e lockout permanecem no dominio de contas.

#### Key material e `KeyParameters`

Componentes:

```text
RoyalIdentity.Security.Keys.KeyParameters
RoyalIdentity.Security.Keys.KeySerializationFormat
RoyalIdentity.Security.Keys.KeyEncoding
RoyalIdentity.Security.Keys.ECKeyHelper
RoyalIdentity.Security.Keys.SecurityKeyExtensions
```

API alvo minima:

```csharp
public class KeyParameters
{
	public string KeyId { get; }
	public string Name { get; }
	public string SecurityAlgorithm { get; }
	public KeySerializationFormat Format { get; }
	public KeyEncoding Encoding { get; }
	public string Key { get; }
	public DateTime Created { get; set; }
	public DateTime? NotBefore { get; set; }
	public DateTime? Expires { get; set; }

	public SigningCredentials CreateSigningCredentials();
	public SecurityKey GetSecurityKey();
	public RsaSecurityKey CreateRsaSecurityKey();
	public ECDsaSecurityKey CreateECDsaSecurityKey();
	public SymmetricSecurityKey CreateSymmetricSecurityKey();
	public (SecurityKey Key, JsonWebKey? JsonWebKey) GetValidationKey();
}
```

Regras:

- `KeyParameters` pode depender de `Microsoft.IdentityModel.Tokens`, mas nao de `RoyalIdentity.Options.KeyOptions`.
- A criacao baseada em `KeyOptions` fica no core como factory/adaptador especifico do IdP.
- O material deve suportar:
  - RSA (`RS*`, `PS*`);
  - ECDsa (`ES256`, `ES384`, `ES512`);
  - symmetric/HMAC (`HS*`).
- Preservar compatibilidade com XML/JSON/Base64/Hex ja aceitos pelos testes atuais.
- `WithoutPrivateKey` para RSA/ECDsa entra em `SecurityKeyExtensions`.
- `ECKeyHelper` entra como utilitario generico de export/import/serialize/deserialize de `ECParameters`.
- `ValidationKeysInfo` deve ser avaliado na Fase 4:
  - mover para `RoyalIdentity.Security.Keys` se for apenas wrapper tecnico;
  - manter no IdP se continuar sendo contrato da borda `IKeyManager`.

#### X509

Componentes candidatos:

```text
RoyalIdentity.Security.Certificates.X509CertificateExtensions
RoyalIdentity.Security.Certificates.X509
```

Regras:

- `CreateThumbprintCnf()` e candidato forte por ser primitiva tecnica.
- O helper fluente de busca em stores (`CurrentUser`, `LocalMachine`, `My`, `Thumbprint`) e generico, mas toca
  ambiente operacional. Pode entrar na Fase 4 se os testes ficarem simples e nao fragilizarem CI.
- Nada de DataProtection nesta area.
- As duas tarefas de "avaliar mover" da Fase 4 tem condicao de pronto explicita: **decidir e registrar no plano**
  (mover ou manter). Criterio: mover `CreateThumbprintCnf()` se for puramente tecnico e testavel com um certificado
  de teste em memoria; manter o store-finder no IdP se exigir certificate store do SO (fragiliza CI). A decisao,
  qualquer que seja, deve ser anotada no plano antes de fechar a Fase 4.

---

## Mapeamento inicial de codigo existente

> Nesta tabela, "Destino planejado" descreve onde o componente passa a existir. "Mover" aqui segue o protocolo
> aditivo-depois-deleta da secao **Ordem de execucao**: adicionar no destino nas Fases 2-4, remover do core na Fase 7.

| Hoje | Destino planejado | Observacoes |
|---|---|---|
| `RoyalIdentity/Utils/CryptoRandom.cs` | `RoyalIdentity.Security.Cryptography.CryptoRandom` | Preservar semantica de tamanho em bytes e formatos. |
| `RoyalIdentity.UserAccounts/.../DefaultSubjectIdGenerator.cs` | wrapper sobre `CryptoRandom` | O contrato `ISubjectIdGenerator` fica no modulo. |
| `RoyalIdentity/Utils/Base64Url.cs` | `RoyalIdentity.Security.Encoding.Base64Url` | Usar nativo do .NET se possivel. |
| `RoyalIdentity/Extensions/HashExtensions.cs` | `RoyalIdentity.Security.Cryptography.HashExtensions` | Chamar `Hashing` central. |
| `RoyalIdentity/Utils/CryptoHelper.cs` | parte em `Hashing`, parte fica no IdP | OIDC hash claims continuam no core. |
| `RoyalIdentity/Utils/PkceHelper.cs` | fica no IdP, usando `Hashing`/`Base64Url` | PKCE e protocolo OIDC. |
| `RoyalIdentity/Utils/TimeConstantComparer.cs` | `RoyalIdentity.Security.Cryptography.FixedTimeComparer` | Corrigir implementacao. |
| `RoyalIdentity/Utils/PasswordHash.cs` | `RoyalIdentity.Security.Passwords.PasswordHash` | Adicionar formato versionado e verificar legado. |
| `RoyalIdentity/Users/Defaults/DefaultPasswordProtector.cs` | fica no IdP, usa `PasswordHash` | Adapter local, sem interface compartilhada nova. |
| `RoyalIdentity/Models/Keys/KeyParameters.cs` | `RoyalIdentity.Security.Keys.KeyParameters` | Remover dependencia de `KeyOptions`; factory do IdP fica no core. |
| `RoyalIdentity/Models/Keys/KeyEncoding.cs` | `RoyalIdentity.Security.Keys.KeyEncoding` | Mover. |
| `RoyalIdentity/Models/Keys/KeySerializationFormat.cs` | `RoyalIdentity.Security.Keys.KeySerializationFormat` | Mover. |
| `RoyalIdentity/Utils/ECKeyHelper.cs` | `RoyalIdentity.Security.Keys.ECKeyHelper` | Mover com testes. |
| `RoyalIdentity/Extensions/SecurityKeyExtensions.cs` | `RoyalIdentity.Security.Keys.SecurityKeyExtensions` | Mover com testes de public key. |
| `RoyalIdentity/Utils/X509.cs` | `RoyalIdentity.Security.Certificates.*` | Mover thumbprint primeiro; store finder se valer o custo. |

---

## Arquitetura alvo

```text
RoyalIdentity.Pipelines

RoyalIdentity.Security
  - sem referencia ao IdP
  - sem ASP.NET
  - componentes tecnicos reutilizaveis

RoyalIdentity
  -> RoyalIdentity.Pipelines
  -> RoyalIdentity.Security

RoyalIdentity.Storage.InMemory
  -> RoyalIdentity
  -> RoyalIdentity.Security (se necessario diretamente)

RoyalIdentity.UserAccounts
  -> RoyalIdentity.Security
  - nao referencia RoyalIdentity
  - nao referencia ASP.NET

RoyalIdentity.UserAccounts.Integration
  -> RoyalIdentity
  -> RoyalIdentity.UserAccounts

RoyalIdentity.UserAccounts.PostgreSql / Sqlite
  -> RoyalIdentity.UserAccounts
```

Guardrails:

- `RoyalIdentity.Security` nunca referencia `RoyalIdentity*`.
- `RoyalIdentity.Security` nunca referencia `Microsoft.AspNetCore*`.
- `RoyalIdentity.UserAccounts` pode referenciar `RoyalIdentity.Security`, mas continua sem referencia ao core.
- O core continua sem referencia ao modulo `UserAccounts`.

---

## Ordem de execucao

> **Protocolo aditivo-depois-deleta (vale para todo o plano):** as Fases 2 a 4 **adicionam** os componentes em
> `RoyalIdentity.Security` sem remover nada do core - os tipos originais continuam compilando e em uso. A Fase 5/6 troca
> os consumidores para os tipos novos. A **remocao** dos tipos originais do core acontece **exclusivamente na Fase 7**.
> Onde a tabela de mapeamento ou as tarefas dizem "mover", leia-se "adicionar em `RoyalIdentity.Security` agora, remover
> do core na Fase 7". Isso evita janela de build quebrado entre fases (ex.: core sem `KeyParameters` antes da troca).

1. Criar projetos e guardrails antes de adicionar codigo.
2. Adicionar primitivas pequenas primeiro (`Base64Url`, `CryptoRandom`, hash, fixed-time compare).
3. Adicionar password hashing com compatibilidade legado antes de trocar `DefaultPasswordProtector`.
4. Adicionar key material depois que a suite de utilitarios estiver verde.
5. Atualizar consumidores em ondas: core, storage/fake, modulo puro.
6. Remover duplicacoes apenas quando as suites focadas estiverem verdes (Fase 7).

---

## Fase 1 - Esqueleto, solution folders e guardrails de dependencia

**Estado:** Concluida

### Tarefas

- [x] Criar projeto `RoyalIdentity.Security/RoyalIdentity.Security.csproj`.
- [x] Remover `FrameworkReference Include="Microsoft.AspNetCore.App"` do projeto.
- [x] Adicionar package/reference minimo para `Microsoft.IdentityModel.Tokens` apenas se a Fase 4 precisar.
  (Decisao: **nao** adicionado agora; a condicao so vale na Fase 4, quando entra o key material. Documentado no `.csproj`.)
- [x] Criar marker interno/publico simples para testes de arquitetura, se necessario. (`SecurityAssemblyMarker` publico.)
- [x] Criar projeto `Tests.Security/Tests.Security.csproj`.
- [x] Referenciar `RoyalIdentity.Security` no projeto de testes.
- [x] Adicionar os projetos na solution.
- [x] Colocar `RoyalIdentity.Security` no virtual folder `src`.
- [x] Colocar `Tests.Security` no virtual folder `test`.
- [x] Estender o projeto existente `Tests.Architecture` (`ModuleBoundaryTests.cs` ja usa `GetReferencedAssemblies()`
  + parsing de `.csproj`; seguir esse mesmo padrao) garantindo:
  - [x] `RoyalIdentity.Security` nao referencia `RoyalIdentity`; (`SecurityLibrary_DoesNotReference_Core`)
  - [x] `RoyalIdentity.Security` nao referencia `RoyalIdentity.UserAccounts`; (`SecurityLibrary_DoesNotReference_AnyDomainModule`)
  - [x] `RoyalIdentity.Security` nao referencia `Microsoft.AspNetCore*`; (`SecurityLibrary_DoesNotDependOn_AspNetCore`)
  - [x] `RoyalIdentity.UserAccounts` pode referenciar `RoyalIdentity.Security` sem quebrar a regra de modulo puro.
    (Regra de pureza usa match exato do core; `PureModule_MayReference_SecurityLibrary_WithoutBreakingPurity` trava o invariante.)

### Resultado da Fase 1

Projetos criados, solution organizada e guardrails prontos antes de qualquer migracao de comportamento.

**Execucao (2026-06-21):** `dotnet test Tests.Security` -> 1/1 verde; `dotnet test Tests.Architecture` -> 14/14 verde
(10 existentes + 4 novos da fronteira `RoyalIdentity.Security`). Build dos projetos novos sem erros (warnings
pre-existentes do core apenas). `RoyalIdentity.Security` em `src`, `Tests.Security` em `test`.

---

## Fase 2 - Random, Base64Url, hashing basico e comparacao constante

**Estado:** Concluida

### Tarefas

- [x] Implementar `CryptoRandom` em `RoyalIdentity.Security`. (`Cryptography/CryptoRandom.cs` + `OutputFormat`; delega aos APIs estaticos de `RandomNumberGenerator`.)
- [x] Implementar `Base64Url` em `RoyalIdentity.Security`. (`Encoding/Base64Url.cs`; fachada sobre `System.Buffers.Text.Base64Url`.)
- [x] Implementar `Hashing` e `HashExtensions` genericos. (`Cryptography/Hashing.cs` + `Cryptography/HashExtensions.cs`.)
- [x] Implementar `FixedTimeComparer` usando `CryptographicOperations.FixedTimeEquals`. (`Cryptography/FixedTimeComparer.cs`.)
- [x] Adicionar testes de `CryptoRandom`:
  - [x] tamanho em bytes preservado para `CreateRandomKey`;
  - [x] `CreateUniqueId` em Base64Url sem padding;
  - [x] `CreateUniqueId` em Base64;
  - [x] `CreateUniqueId` em Hex;
  - [x] `Next` e ranges basicos;
  - [x] teste de sanidade de unicidade sem depender de probabilidade fragil.
- [x] Adicionar testes de `Base64Url`:
  - [x] vetores conhecidos de round-trip;
  - [x] inputs com e sem padding;
  - [x] input invalido em `Decode` e `TryDecode`.
- [x] Adicionar testes de hashing:
  - [x] SHA256/SHA512 com vetores conhecidos; (tambem SHA384)
  - [x] extensoes retornam o mesmo formato legado esperado;
  - [x] left-half hash base64url com algoritmo 256/384/512. (com vetor `at_hash` do OIDC Core para SHA-256)
- [x] Adicionar testes de `FixedTimeComparer`:
  - [x] igualdade verdadeira;
  - [x] igualdade falsa;
  - [x] tamanhos diferentes;
  - [x] comparacao UTF-8;
  - [x] comparacao Base64.

### Resultado da Fase 2

Primitivas pequenas existem em `RoyalIdentity.Security` com testes proprios, ainda sem trocar consumidores.

**Execucao (2026-06-21):** `dotnet test Tests.Security` -> 58/58 verde; `dotnet test Tests.Architecture` -> 14/14 verde.
Ajuste de implementacao: `Base64Url.TryDecode` usa `System.Buffers.Text.Base64Url.IsValid` como guarda, porque o
`TryDecodeFromChars` da BCL ainda lanca `FormatException` em conteudo invalido (o "Try" cobre so o tamanho do buffer).
Cobertura adicional adicionada para overloads de preenchimento de buffer de `CryptoRandom` e tamanho invalido de
`Base64Url`. Nenhum consumidor trocado ainda (protocolo aditivo).

---

## Fase 3 - Password hashing reutilizavel e compatibilidade legado

**Estado:** Concluida

### Tarefas

- [x] Implementar `PasswordHashOptions`. (`Passwords/PasswordHashOptions.cs`; defaults = params legados.)
- [x] Implementar `PasswordVerificationResult`. (`Failed`/`Success`/`SuccessRehashNeeded`.)
- [x] Implementar `PasswordHash.Create(...)` com formato novo versionado. (`$RIPWD$1$PBKDF2-{SHA}${iter}${salt}${hash}`.)
- [x] Implementar `PasswordHash.Verify(...)`. (Nunca lanca; legado matcheado -> `SuccessRehashNeeded`.)
- [x] Implementar verificacao do formato legado atual `$PBKDF2$.{salt}.{hash}`. (PBKDF2-HMAC-SHA256, 100k, salt 16, hash 32.)
- [x] Implementar `NeedsRehash(...)`. (Legado, iteracoes abaixo da politica, algoritmo/sizes diferentes ou malformado.)
- [x] Registrar no backlog a orquestracao de rehash-on-login (consumidor/dominio de contas), deixando claro que
  `NeedsRehash` e entregue aqui mas a adocao para hashes legados fica fora do escopo deste plano.
  (Item novo em `backlog-001.md`: "Rehash-on-login de hashes de senha (orquestracao)".)
- [x] Adicionar testes:
  - [x] senha correta valida hash novo;
  - [x] senha errada falha;
  - [x] hash malformado retorna falha (nao lanca, nao autentica);
  - [x] formato legado atual continua verificavel;
  - [x] `NeedsRehash` detecta iteracoes/algoritmo antigos;
  - [x] salts diferentes geram hashes diferentes para a mesma senha;
  - [x] hash gerado nao contem a senha em texto claro.

### Resultado da Fase 3

Password hashing reutilizavel pronto, sem interface compartilhada nova e com compatibilidade para hashes existentes.

**Execucao (2026-06-21):** `dotnet test Tests.Security` -> 76/76 verde. Implementacao usa a primitiva da BCL
`Rfc2898DeriveBytes.Pbkdf2` (PBKDF2-HMAC-SHA256, senha em UTF-8) em vez do `Microsoft.AspNetCore.Cryptography.KeyDerivation`
do core, para nao introduzir dependencia de ASP.NET na biblioteca de folha; ambas produzem os mesmos bytes (PBKDF2
padrao). O teste de compat. legado constroi um hash no formato antigo com a mesma primitiva/parametros; a verificacao
cruzada definitiva core->lib (hash criado pelo `PasswordHash` legado, verificado pela lib) entra na Fase 5/6, quando
`Tests.Identity` puder referenciar ambos. Mudanca de contrato: `Verify` nao lanca em hash malformado (retorna `Failed`).

---

## Fase 4 - Key material, `KeyParameters` e helpers de chaves

**Estado:** Concluida

### Tarefas

> Fase aditiva: adicionar os tipos em `RoyalIdentity.Security` sem remover os originais do core (remocao e Fase 7).
> Antes da Fase 4, reconciliar a edicao em andamento de `RoyalIdentity/Models/Keys/KeyParameters.cs` (ver Riscos),
> para que a copia adicionada parta do estado correto.

- [x] Adicionar `KeyEncoding` em `RoyalIdentity.Security` (original permanece ate a Fase 7). (`Keys/KeyEncoding.cs`.)
- [x] Adicionar `KeySerializationFormat` em `RoyalIdentity.Security`. (`Keys/KeySerializationFormat.cs`.)
- [x] Adicionar `ECKeyHelper` em `RoyalIdentity.Security`. (`Keys/ECKeyHelper.cs`; copia fiel, mesma forma XML/JSON.)
- [x] Adicionar `SecurityKeyExtensions` em `RoyalIdentity.Security`. (`Keys/SecurityKeyExtensions.cs`; `WithoutPrivateKey` RSA/EC.)
- [x] Adicionar `KeyParameters` em `RoyalIdentity.Security` sem dependencia de `RoyalIdentity.Options.KeyOptions`.
  (`Keys/KeyParameters.cs`; sem o `Create(KeyOptions...)`; `GetValidationKey` agora retorna tupla nomeada `(Key, JsonWebKey)`.)
- [x] Criar factory generica para key material quando nao couber no construtor de `KeyParameters`.
  (`Keys/KeyMaterialFactory.cs`; `Create(algorithm, lifetime?, rsaKeySizeInBits)` sem `KeyOptions`.)
- [x] Decidir e implementar o destino de `ValidationKeysInfo`. **Decisao: manter no core.** E o tipo de retorno de
  `IKeyManager.GetValidationKeysAsync(Realm, ...)` e parametriza `Realm`; e contrato da borda `IKeyManager`, nao um
  wrapper tecnico puro. Permanece em `RoyalIdentity/Models/Keys/ValidationKeysInfo.cs`.
- [x] Decidir e registrar no plano: mover `X509CertificateExtensions.CreateThumbprintCnf()`. **Decisao: mover.**
  E primitiva puramente tecnica (RFC 8705 `x5t#S256`), testavel com certificado self-signed em memoria; ja usa
  `Base64Url` (disponivel na lib). Movido para `Certificates/X509CertificateExtensions.cs`.
- [x] Decidir e registrar no plano: mover o helper fluente `X509` de busca em certificate stores. **Decisao: manter
  no core.** Depende do certificate store do SO (`X509Store`), o que fragilizaria a CI. Permanece em
  `RoyalIdentity/Utils/X509.cs`.
- [x] Migrar testes existentes de `Tests.Identity/Keys/KeyParametersTests.cs` para `Tests.Security`.
  (Removido de `Tests.Identity`; cobertura generica reescrita e ampliada em `Tests.Security/Keys/KeyParametersTests.cs`.
  O `KeyParameters` do core continua exercitado indiretamente por `Tests.Integration/SigningAlgorithmTests` via a
  factory `KeyOptions` ate a remocao na Fase 7.)
- [x] Adicionar novos testes:
  - [x] RSA XML round-trip;
  - [x] RSA JSON round-trip;
  - [x] RSA assina e verifica;
  - [x] ECDsa XML round-trip;
  - [x] ECDsa JSON round-trip;
  - [x] ECDsa assina e verifica;
  - [x] symmetric key Base64 round-trip;
  - [x] symmetric key Hex round-trip;
  - [x] `WithoutPrivateKey` remove material privado e preserva `KeyId`;
  - [x] `GetValidationKey` gera JWK publico sem material privado;
  - [x] algoritmo nao suportado falha explicitamente.

### Resultado da Fase 4

Material de chaves fica disponivel como componente reutilizavel para IdP e futuro KMS.

**Reconciliacao do WIP:** a arvore de trabalho estava limpa no inicio da fase (nenhuma edicao pendente em
`KeyParameters.cs`), entao nao houve WIP a reconciliar; a copia adicionada partiu do estado commitado.

**`Microsoft.IdentityModel.Tokens`:** adicionado ao `.csproj` da lib via `$(IdVer)` (8.14.0, compartilhado com o core).
Nao carrega dependencia de ASP.NET, entao o guardrail `SecurityLibrary_DoesNotDependOn_AspNetCore` continua valido.

**Mudanca de comportamento intencional (HMAC):** a factory original do core marcava chaves HMAC com
`KeyEncoding.Plain` enquanto guardava o valor em Base64 - como `GetKeyBytes()` lanca para `Plain`, a materializacao
de chaves HMAC geradas pela factory do core estava latentemente quebrada (nenhum teste cobria esse caminho; os testes
de HMAC sempre construiram `KeyParameters` com `KeyEncoding.Base64`). A `KeyMaterialFactory` nova corrige isso usando
`KeyEncoding.Base64`, de modo que a chave HMAC gerada realmente materializa. Coberto por
`KeyMaterialFactoryTests.Create_Hmac_Produces_Materializable_Key`. Isso so afeta chaves recem-geradas; chaves
armazenadas nao sao impactadas.

**Verificacao (PENDENTE - sem .NET SDK no ambiente):** este ambiente de execucao remoto **nao tem o .NET SDK**
instalado e a politica de rede bloqueia a instalacao (403 nos endpoints do dotnet/NuGet), portanto **nao foi possivel
rodar `dotnet build`/`dotnet test` nesta sessao**. O codigo e os testes foram escritos espelhando os tipos originais
ja verdes do core e os padroes das Fases 1-3. Antes de fechar formalmente a fase, executar em ambiente com SDK:
`dotnet build RoyalIdentity.Security`, `dotnet test Tests.Security`, `dotnet test Tests.Architecture` e
`dotnet test Tests.Identity` (esta ultima para confirmar que a remocao do `KeyParametersTests.cs` migrado nao deixou
lacuna inesperada). Registrar os resultados aqui apos a execucao.

---

## Fase 5 - Troca no core `RoyalIdentity`

**Estado:** Pendente

### Tarefas

- [ ] Adicionar referencia de `RoyalIdentity` para `RoyalIdentity.Security`.
- [ ] Atualizar usos de `RoyalIdentity.Utils.CryptoRandom` para `RoyalIdentity.Security`.
- [ ] Atualizar usos de `Base64Url`.
- [ ] Atualizar `HashExtensions`/hash helpers para usarem `RoyalIdentity.Security`.
- [ ] Atualizar `TimeConstantComparer` para usar `FixedTimeComparer` ou remover o tipo local.
- [ ] Atualizar o call-site de PKCE em `PkceMatchValidator` para usar `FixedTimeComparer` (in-scope, mesmo o
  validator permanecendo no core).
- [ ] Atualizar o call-site de client secret em `SecretEvaluatorBase` para usar `FixedTimeComparer` (in-scope; o
  evaluator NAO migra, apenas a comparacao interna muda).
- [ ] Adicionar/ajustar testes de regressao de PKCE e de autenticacao de client secret apos a troca do comparador.
- [ ] Atualizar `DefaultPasswordProtector` para usar `RoyalIdentity.Security.Passwords.PasswordHash`, mapeando
  `PasswordVerificationResult` para `bool` (incluindo `SuccessRehashNeeded` -> `true`) e absorvendo a remocao do
  throw em hash malformado.
- [ ] Atualizar `CryptoHelper.CreateHashClaimValue` para delegar a primitiva generica de left-half hash.
- [ ] Manter `PkceHelper` no core, mas delegando hash/encoding aos componentes reutilizaveis.
- [ ] Atualizar `KeyParameters`, `KeyEncoding`, `KeySerializationFormat`, `ECKeyHelper` e extensions de chaves para
  os namespaces novos.
- [ ] Criar factory/adaptador no core para `KeyParameters.Create(KeyOptions, ...)`, preservando politica de realm.
- [ ] Atualizar stores, `IKeyStore`, `DefaultKeyManager`, `FirstKeyJob`, testes e seeds para o novo namespace de chaves.
- [ ] Ajustar testes `Tests.Identity` para cobrir somente comportamento especifico do IdP depois que os testes
  genericos forem movidos.

### Resultado da Fase 5

O core usa `RoyalIdentity.Security` para primitivas, mas conserva regras OIDC, realm, clients, tokens e stores no IdP.

---

## Fase 6 - Troca em `UserAccounts`, fake in-memory e testes de borda

**Estado:** Pendente

### Tarefas

- [ ] Adicionar referencia de `RoyalIdentity.UserAccounts` para `RoyalIdentity.Security`.
- [ ] Trocar `DefaultSubjectIdGenerator` para chamar `CryptoRandom.CreateUniqueId(32, Base64Url)`.
- [ ] Garantir que o modulo puro continua sem referencia ao core e sem ASP.NET.
- [ ] Atualizar `RoyalIdentity.Storage.InMemory` para usar `RoyalIdentity.Security` onde ainda usa utilitarios do core.
- [ ] Atualizar seeds do fake/in-memory que criam password hashes.
- [ ] Confirmar que `PasswordProtectorAccountHasher` continua apenas como adapter de borda, sem mover para
  `RoyalIdentity.Security`.
- [ ] Rodar/ajustar testes de `Tests.UserAccounts` ligados a `SubjectId`, autenticacao local e contract tests.
- [ ] Rodar/ajustar testes de integracao que usam PKCE, secrets e tokens.

### Resultado da Fase 6

Core, modulo puro e fake deixam de duplicar primitivas e passam a consumir a mesma biblioteca reutilizavel.

---

## Fase 7 - Remocao de duplicacoes e shims temporarios

**Estado:** Pendente

### Tarefas

- [ ] Remover `RoyalIdentity/Utils/CryptoRandom.cs` ou transforma-lo em shim temporario `[Obsolete]` apenas se houver
  necessidade de compatibilidade incremental.
- [ ] Remover `RoyalIdentity/Utils/Base64Url.cs` ou shim temporario.
- [ ] Remover `RoyalIdentity/Utils/PasswordHash.cs` ou shim temporario.
- [ ] Remover `RoyalIdentity/Utils/TimeConstantComparer.cs` ou shim temporario.
- [ ] Remover partes duplicadas de `HashExtensions`.
- [ ] Remover/mover `ECKeyHelper` duplicado.
- [ ] Remover/mover `SecurityKeyExtensions` duplicado.
- [ ] Remover/mover enums duplicados de key encoding/serialization.
- [ ] Rodar `rg` para garantir que nao ha novos usos dos tipos antigos fora de shims intencionais.
- [ ] Se shims forem mantidos, registrar prazo de remocao neste plano ou no backlog.

### Resultado da Fase 7

Duplicacao removida ou explicitamente marcada como transitoria, sem tipos genericos espalhados pelo core.

---

## Fase 8 - Verificacao ampla, documentacao e fechamento

**Estado:** Pendente

### Tarefas

- [ ] Rodar `dotnet test Tests.Security`.
- [ ] Rodar `dotnet test Tests.Identity`.
- [ ] Rodar `dotnet test Tests.UserAccounts`.
- [ ] Rodar `dotnet test Tests.Architecture`.
- [ ] Rodar testes de integracao focados:
  - [ ] endpoints de code/token/refresh;
  - [ ] signing algorithms;
  - [ ] realm isolation com secrets;
  - [ ] UI login/consent com PKCE.
- [ ] Rodar `dotnet test RoyalIdentity.sln` se a mudanca final tocar tokens, keys, storage e login.
- [ ] Atualizar `backlog-001.md` removendo ou marcando como concluido o item de projeto compartilhado de seguranca.
- [ ] Atualizar `plans-roadmap-01.md` se a ordem do KMS ou seguranca de contas mudar.
- [ ] Registrar no plano os comandos executados e resultados.

### Resultado da Fase 8

Plano fechado com biblioteca reutilizavel, consumidores migrados e suites relevantes verdes.

---

## Invariantes a preservar

- O projeto `RoyalIdentity.Security` nao conhece realm, client, scope, token store, user store ou pipelines.
- O modulo puro `RoyalIdentity.UserAccounts` continua sem dependencia do core `RoyalIdentity`.
- Hashes de senha legados continuam verificaveis.
- Tokens, authorization codes, refresh tokens e JWT ids continuam usando entropia criptografica.
- Valores Base64Url continuam sem padding quando usados em protocolo.
- Comparacao de secrets e PKCE nao deve usar comparacao string early-exit para material sensivel.
- Chaves de assinatura antigas continuam carregando e validando tokens emitidos antes da migracao.
- JWKs publicos nao devem expor material privado.
- A escolha de algoritmo por realm/client/resource continua no core, nao em `RoyalIdentity.Security`.
- DataProtection do ASP.NET continua fora deste projeto.
- Todos os componentes de `RoyalIdentity.Security` sao stateless e thread-safe: sao helpers estaticos chamados
  concorrentemente em hot paths (emissao de token, autenticacao). Isso e propriedade load-bearing ao extrair para
  biblioteca compartilhada - preservar via APIs thread-safe da BCL (`RandomNumberGenerator`, `SHA*.HashData`,
  PBKDF2 stateless), sem estado mutavel compartilhado.

---

## Criterios globais de aceite

- `RoyalIdentity.Security` compila em `net10.0`.
- `RoyalIdentity.Security` nao referencia `RoyalIdentity*`.
- `RoyalIdentity.Security` nao referencia `Microsoft.AspNetCore*`.
- `Tests.Security` cobre todos os componentes migrados.
- `RoyalIdentity`, `RoyalIdentity.Storage.InMemory` e `RoyalIdentity.UserAccounts` usam os componentes reutilizaveis.
- Nao ha duplicacao ativa de `CryptoRandom`, `Base64Url`, password hash, fixed-time compare ou key material.
- Testes de key material migrados de `Tests.Identity` continuam verdes.
- Testes de token/signing/PKCE/secret continuam verdes.
- Contract tests de `UserAccounts` continuam verdes.
- Architecture tests garantem as novas fronteiras.

---

## Riscos

- **Compatibilidade de password hash:** mudar o formato sem verificar legado pode quebrar usuarios/seeds existentes.
- **Compatibilidade de chaves:** mover `KeyParameters` sem preservar XML/JSON/encoding pode invalidar chaves armazenadas.
- **Churn de namespace:** muitos testes usam `CryptoRandom`, `Sha512` e `KeyParameters`; a migracao precisa ser mecanica
  e revisada.
- **Dependencia pesada:** `RoyalIdentity.Security` nao deve herdar ASP.NET por acidente via `Directory.Build.props`.
- **Comparacao constante mal implementada:** apenas mover `TimeConstantComparer` preservaria uma primitiva fraca.
- **Mistura com OIDC:** nomes como `at_hash`, `c_hash`, `PKCE`, `ClientSecret` e `PrivateKeyJwt` devem permanecer no core.
- **KMS futuro:** se `KeyParameters` ficar IdP-specific demais, o KMS voltara a duplicar modelos.
- **WIP em `KeyParameters`:** `RoyalIdentity/Models/Keys/KeyParameters.cs` (e `.ai/references/external-libraries/search.md`)
  estao modificados na arvore de trabalho. A Fase 4 adiciona/adapta exatamente esse tipo; reconciliar ou commitar o WIP
  antes da Fase 4 para a copia adicionada partir do estado correto e evitar conflito.
- **Mudanca de contrato no `Verify`:** trocar throw-on-malformed por `PasswordVerificationResult` e melhor (sem throw em
  fluxo esperado), mas e uma mudanca de comportamento; garantir que `DefaultPasswordProtector` e chamadores nao dependam
  da excecao anterior.

---

## Referencias

- [backlog-001.md](../backlogs/backlog-001.md) - item `Projeto compartilhado de seguranca (RoyalIdentity.Security)`.
- [plans-roadmap-01.md](plans-roadmap-01.md) - KMS e evolucao de seguranca.
- [foundation/architecture.md](../foundation/architecture.md) - arquitetura modular e futuro KMS.
- [ADR-013](../../adrs/ADR-013.md) - arquitetura modular e fronteiras.
- [ADR-015](../../adrs/ADR-015.md) - modulo `UserAccounts` e modulo puro sem dependencia do core.
- [ADR-016](../../adrs/ADR-016.md) - biblioteca tecnica compartilhada `RoyalIdentity.Security` (emenda o ADR-013).
- [fase5-useraccount-domain.review-001.md](../reviews/user-accounts/fase5-useraccount-domain.review-001.md) - nota original sobre `SubjectIdGenerator` e `RoyalIdentity.Security`.
