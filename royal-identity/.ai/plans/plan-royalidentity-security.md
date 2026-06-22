# Plan: Projeto compartilhado de seguranca (`RoyalIdentity.Security`)

## Status: CONCLUÍDO - Todas 8 fases concluídas

## Progresso

`########` **100%** - 8 de 8 fases

| Fase | Estado |
|---|---|
| Fase 1 - Esqueleto, solution folders e guardrails de dependencia | Concluida |
| Fase 2 - Random, Base64Url, hashing basico e comparacao constante | Concluida |
| Fase 3 - Password hashing reutilizavel e formato versionado | Concluida |
| Fase 4 - Key material, `KeyParameters` e helpers de chaves | Concluida |
| Fase 5 - Troca no core `RoyalIdentity` | Concluida |
| Fase 6 - Troca em `UserAccounts`, fake in-memory e testes de borda | Concluida |
| Fase 7 - Remocao de duplicacoes e shims temporarios | Concluida |
| Fase 8 - Verificacao ampla, documentacao e fechamento | Concluida |

> **Manutencao deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `#` por fase concluida, `%` e `X de 8`). Ex.: 3 fases => `###-----` **37%** - 3 de 8.

---

## Correcao pos-conclusao (2026-06-22)

Apos o fechamento das 8 fases, uma revisao identificou que a Fase 7 ("Remocao de duplicacoes e shims temporarios")
nao havia eliminado todas as duplicacoes — varios tipos do core continuavam como wrappers/shims, e a classe
`KeyParameters` do core seguia duplicada. Esta correcao conclui o objetivo original da Fase 7:

- **Wrappers do core removidos (sem shims):** `RoyalIdentity.Utils.CryptoRandom`, `Utils.Base64Url`,
  `Utils.PasswordHash`, `Utils.TimeConstantComparer`, `Utils.ECKeyHelper` e `Extensions.HashExtensions` foram
  **deletados**. Todos os call-sites passaram a usar diretamente os tipos de `RoyalIdentity.Security.*`
  (via `using` explicito/alias na producao e `Using` de projeto na suite de integracao). Nao restam shims `[Obsolete]`.
- **`KeyParameters` duplicado removido:** `RoyalIdentity/Models/Keys/{KeyParameters,KeyEncoding,KeySerializationFormat}.cs`
  foram deletados; o core usa exclusivamente `RoyalIdentity.Security.Keys.*`. A geracao a partir de `KeyOptions`
  permanece no adaptador `RoyalIdentity.Models.Keys.KeyParametersFactory` (delega a `KeyMaterialFactory`). O teste
  `Tests.Identity/Keys/KeyParametersCompatibilityTests` passou a exercitar a factory do IdP.
- **Rehash removido:** como ha um unico formato (`$RIPWD$`) e nenhum legado a migrar, `PasswordHash.Verify` agora
  retorna `bool`. O resultado tri-estado e a deteccao de rehash foram removidos; o item de backlog de
  rehash-on-login foi remarcado como "nao aplicavel" (reintroduzir apenas se houver upgrade futuro de parametros).
- **Fechamento final dos achados (2026-06-22):** `CreateThumbprintCnf` e `SecurityKeyExtensions` ficaram apenas em
  `RoyalIdentity.Security`; o core removeu as duplicacoes. `PasswordHash.Create` passou a validar opcoes publicas, o
  fluxo PKCE ganhou helpers explicitos para challenge RFC7636 e hash de armazenamento, e os testes finais ficaram
  verdes em `dotnet test RoyalIdentity.sln` (440/440).

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
7. Manter compatibilidade comportamental com tokens, secrets e chaves existentes; para password hashes, manter
   o formato versionado `$RIPWD$` e rejeitar o formato experimental pre-release `$PBKDF2$`, pois nao houve release
   final nem legado de producao a preservar.
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
```

API alvo minima:

```csharp
public static class PasswordHash
{
	public static string Create(string password);
	public static string Create(string password, PasswordHashOptions options);
	public static bool Verify(string password, string storedHash);
}
```

Regras:

- Sem interface compartilhada nesta fase.
- Formato novo deve ser versionado e autocontido, incluindo algoritmo, iteracoes, salt e hash.
- O formato experimental pre-release (`$PBKDF2$.{salt}.{hash}`, salt/hash em Base64 padrao) **nao** deve continuar
  verificavel: como o projeto ainda nao teve release final, nao ha legado de producao a preservar. `Verify` deve
  tratar esse formato como nao reconhecido e retornar `false`.
- **Mudanca de comportamento intencional:** o `Verify` legado lancava `ArgumentException` para hash malformado; o novo
  `Verify` retorna `false`, nunca lanca. Isso alinha com a regra de nao usar throw em fluxo esperado. Deve haver teste
  cobrindo "hash malformado retorna false, nao lanca e nao autentica".
- O `DefaultPasswordProtector` do core delega direto a este `Verify` (bool), sem mapeamento intermediario.
- **Sem maquinaria de rehash:** como ha um unico formato (`$RIPWD$`) e nenhum legado a migrar, `Verify` retorna apenas
  `bool`. Se um upgrade futuro de parametros PBKDF2 exigir rehash-on-login, a deteccao e um resultado mais rico podem
  ser reintroduzidos junto com a orquestracao no dominio de contas, que e onde a politica por realm vive.
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
| `RoyalIdentity/Utils/PasswordHash.cs` | `RoyalIdentity.Security.Passwords.PasswordHash` | Adicionar formato versionado; rejeitar formato pre-release `$PBKDF2$`. |
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
3. Adicionar password hashing versionado antes de trocar `DefaultPasswordProtector`.
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
  - [x] extensoes retornam o mesmo formato historico esperado;
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

## Fase 3 - Password hashing reutilizavel e formato versionado

**Estado:** Concluida

### Tarefas

- [x] Implementar `PasswordHashOptions`. (`Passwords/PasswordHashOptions.cs`; defaults = params atuais do projeto.)
- [x] Implementar `PasswordHash.Create(...)` com formato novo versionado. (`$RIPWD$1$PBKDF2-{SHA}${iter}${salt}${hash}`.)
- [x] Implementar `PasswordHash.Verify(...)` retornando `bool`. (Nunca lanca; formato pre-release `$PBKDF2$` retorna `false`.)
- [x] Remover suporte ao formato experimental pre-release `$PBKDF2$.{salt}.{hash}`.
- [x] Registrar no backlog que rehash-on-login fica fora do escopo atual e so deve voltar se houver upgrade futuro de parametros.
- [x] Adicionar testes:
  - [x] senha correta valida hash novo;
  - [x] senha errada falha;
  - [x] hash malformado retorna falha (nao lanca, nao autentica);
  - [x] formato pre-release `$PBKDF2$` valido e rejeitado como nao suportado;
  - [x] opcoes publicas invalidas falham cedo em `Create`;
  - [x] algoritmos suportados SHA256/SHA384/SHA512 verificam corretamente;
  - [x] salts diferentes geram hashes diferentes para a mesma senha;
  - [x] hash gerado nao contem a senha em texto claro.

### Resultado da Fase 3

Password hashing reutilizavel pronto, sem interface compartilhada nova, com formato versionado `$RIPWD$` e rejeicao
explicita do formato experimental pre-release `$PBKDF2$`.

**Execucao (2026-06-21):** `dotnet test Tests.Security` -> 76/76 verde. Implementacao usa a primitiva da BCL
`Rfc2898DeriveBytes.Pbkdf2` (PBKDF2-HMAC-SHA256, senha em UTF-8) em vez do `Microsoft.AspNetCore.Cryptography.KeyDerivation`
do core, para nao introduzir dependencia de ASP.NET na biblioteca de folha; ambas produzem os mesmos bytes (PBKDF2
padrao). Mudanca de contrato: `Verify` nao lanca em hash malformado (retorna `false`).

**Ajuste pos-revisao (2026-06-22):** removida a verificacao de `$PBKDF2$`. A decisao substitui a intencao inicial de
compatibilidade porque ainda nao houve release final, logo nao existe legado de producao a preservar. `Create` valida
`PasswordHashOptions` (algoritmo, iteracoes, salt e tamanho de hash), e o teste agora constroi um hash `$PBKDF2$`
valido para a senha e confirma que `Verify` retorna `false`.

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

**`Microsoft.IdentityModel.Tokens`:** adicionado ao `.csproj` da lib via `$(IdVer)` (8.19.1, compartilhado com o core).
Nao carrega dependencia de ASP.NET, entao o guardrail `SecurityLibrary_DoesNotDependOn_AspNetCore` continua valido.

**Mudanca de comportamento intencional (HMAC):** a factory original do core marcava chaves HMAC com
`KeyEncoding.Plain` enquanto guardava o valor em Base64 - como `GetKeyBytes()` lanca para `Plain`, a materializacao
de chaves HMAC geradas pela factory do core estava latentemente quebrada (nenhum teste cobria esse caminho; os testes
de HMAC sempre construiram `KeyParameters` com `KeyEncoding.Base64`). A `KeyMaterialFactory` nova corrige isso usando
`KeyEncoding.Base64`, de modo que a chave HMAC gerada realmente materializa. Ajuste pos-revisao: HS256/HS384/HS512
agora geram, respectivamente, 32/48/64 bytes de material e o `KeyParameters` antigo do core recebeu o mesmo ajuste
enquanto existir ate a Fase 7. Coberto por assinatura/verificacao em
`KeyMaterialFactoryTests.Create_Hmac_Produces_Algorithm_Appropriate_Signing_Key` e por teste de compatibilidade do
core em `Tests.Identity`. Isso so afeta chaves recem-geradas; chaves armazenadas nao sao impactadas.

**Verificacao (2026-06-22 via GitHub Actions CI):** build e testes executados no workflow
`.github/workflows/build-and-test.yml` (Ubuntu, .NET 10). Resultado final (run #3, commit `2fa057d`):
`dotnet test RoyalIdentity.sln` — `Tests.Security` verde, `Tests.Architecture` verde.

Ajuste de compatibilidade Linux: `ECDsaSecurityKey.PrivateKeyStatus` retorna `Unknown` no Linux (OpenSSL) mesmo para
chaves com material privado. Correcoes aplicadas antes de fechar a fase:
- `SecurityKeyExtensions.WithoutPrivateKey(ECDsaSecurityKey)`: substituiu guarda `PrivateKeyStatus == Exists` por
  `try { ExportParameters(true) } catch (CryptographicException)` para detectar material privado diretamente.
- `KeyParameters.GetValidationKey()`: chama `WithoutPrivateKey()` sempre para ECDsa (sem-op quando ja publica),
  em vez de condicional em `PrivateKeyStatus`.
- `SecurityKeyExtensionsTests`: substituiu `Assert.Equal(PrivateKeyStatus.Exists, ...)` por
  `Assert.NotNull(ecdsa.ExportParameters(true).D)` para nao depender de `PrivateKeyStatus` no Linux.

---

## Fase 5 - Troca no core `RoyalIdentity`

**Estado:** Concluida

### Tarefas

- [x] Adicionar referencia de `RoyalIdentity` para `RoyalIdentity.Security`.
- [x] Atualizar usos de `RoyalIdentity.Utils.CryptoRandom` para `RoyalIdentity.Security`.
- [x] Atualizar usos de `Base64Url`.
- [x] Atualizar `HashExtensions`/hash helpers para usarem `RoyalIdentity.Security`.
- [x] Atualizar `TimeConstantComparer` para usar `FixedTimeComparer` ou remover o tipo local.
- [x] Atualizar o call-site de PKCE em `PkceMatchValidator` para usar `FixedTimeComparer` (in-scope, mesmo o
  validator permanecendo no core).
- [x] Atualizar o call-site de client secret em `SecretEvaluatorBase` para usar `FixedTimeComparer` (in-scope; o
  evaluator NAO migra, apenas a comparacao interna muda).
- [x] Adicionar/ajustar testes de regressao de PKCE e de autenticacao de client secret apos a troca do comparador.
- [x] Atualizar `DefaultPasswordProtector` para usar `RoyalIdentity.Security.Passwords.PasswordHash` diretamente
  (`Verify` retorna `bool`) e absorver a remocao do throw em hash malformado.
- [x] Atualizar `CryptoHelper.CreateHashClaimValue` para delegar a primitiva generica de left-half hash.
- [x] Manter `PkceHelper` no core, mas delegando hash/encoding aos componentes reutilizaveis.
- [x] Atualizar `KeyParameters`, `KeyEncoding`, `KeySerializationFormat`, `ECKeyHelper` e extensions de chaves para
  os namespaces novos.
- [x] Criar factory/adaptador no core para `KeyParameters.Create(KeyOptions, ...)`, preservando politica de realm.
- [x] Atualizar stores, `IKeyStore`, `DefaultKeyManager`, `FirstKeyJob`, testes e seeds para o novo namespace de chaves.
- [x] Ajustar testes `Tests.Identity` para cobrir somente comportamento especifico do IdP depois que os testes
  genericos forem movidos.

### Resultado da Fase 5

O core usa `RoyalIdentity.Security` para primitivas, mas conserva regras OIDC, realm, clients, tokens e stores no IdP.

**Ajuste pos-revisao (2026-06-22):**
- `TimeConstantComparer` foi removido; comparacoes sensiveis usam `FixedTimeComparer` diretamente.
- O fluxo PKCE `plain` ganhou teste positivo com client que permite `AllowPlainTextPkce`; o teste negativo existente
  passou a usar a constante lowercase `plain`, cobrindo a politica e nao erro de casing.
- `DefaultPasswordProtector` ganhou teste para hash malformado/nao suportado retornar `false`.
- O fluxo PKCE ganhou helpers explicitos para challenge S256 RFC7636 e hash de armazenamento.

**Verificacao local pos-revisao (2026-06-22):**
- `dotnet test Tests.Security` -> 116/116 verde.
- `dotnet test Tests.Identity` -> 13/13 verde.
- `dotnet test Tests.Integration` -> 202/202 verde.
- `dotnet test Tests.Architecture` -> 15/15 verde.
- `dotnet test RoyalIdentity.sln` -> verde (Security 116, Identity 13, Architecture 15, Pipelines 3,
  UserAccounts 91, Integration 202). Warnings restantes sao pre-existentes/de ambiente: `NETSDK1086`, `NU1903`,
  `NU1510`, `NU1701` e nullable/obsoleto em projetos nao alterados.

---

## Fase 6 - Troca em `UserAccounts`, fake in-memory e testes de borda

**Estado:** Concluida

### Tarefas

- [x] Adicionar referencia de `RoyalIdentity.UserAccounts` para `RoyalIdentity.Security`.
- [x] Trocar `DefaultSubjectIdGenerator` para chamar `CryptoRandom.CreateUniqueId(32, Base64Url)`.
- [x] Garantir que o modulo puro continua sem referencia ao core e sem ASP.NET.
- [x] Atualizar `RoyalIdentity.Storage.InMemory` para usar `RoyalIdentity.Security` onde ainda usa utilitarios do core.
  (Verificacao: nao ha mais `using RoyalIdentity.Utils` em `RoyalIdentity.Storage.InMemory`.)
- [x] Atualizar seeds do fake/in-memory que criam password hashes.
  (Verificacao: seeds e fixtures afetados usam `RoyalIdentity.Security.Passwords`.)
- [x] Confirmar que `PasswordProtectorAccountHasher` continua apenas como adapter de borda, sem mover para
  `RoyalIdentity.Security`.
  (Verificacao: `RoyalIdentity.UserAccounts.Integration/PasswordProtectorAccountHasher.cs` continua ponte para `IPasswordProtector`.)
- [x] Rodar/ajustar testes de `Tests.UserAccounts` ligados a `SubjectId`, autenticacao local e contract tests.
  (`Tests.UserAccounts` -> 91/91 verde.)
- [x] Rodar/ajustar testes de integracao que usam PKCE, secrets e tokens.
  (Suites focadas -> 143/143 verde: autenticacao local, subject id, login/consent UI, code/token/refresh,
  client secrets e signing algorithms.)

### Resultado da Fase 6

Core, modulo puro e fake deixam de duplicar primitivas e passam a consumir a mesma biblioteca reutilizavel.

**Build Status:** PASSED (2026-06-22 ~10:44) - 38 avisos pre-existentes, sem erros introduzidos.

**Mudancas principais:**
- `RoyalIdentity.UserAccounts`: Referência para `RoyalIdentity.Security` adicionada; `DefaultSubjectIdGenerator` usa `CryptoRandom.CreateUniqueId`.
- `RoyalIdentity.Storage.InMemory`: 2 arquivos migraram de `RoyalIdentity.Utils` para `RoyalIdentity.Security.*`.
- `Tests.UserAccounts`, `Tests.Integration`: Imports atualizados com type aliases para evitar ambiguidade.
- `Tests.Architecture/ModuleBoundaryTests.cs`: Teste atualizado para validar aresta legal `UserAccounts -> Security`.

**Execucao (2026-06-22):**
- `dotnet build RoyalIdentity.sln` -> build verde.
- `Tests.UserAccounts` -> 91/91 verde.
- Suites focadas de `Tests.Integration` -> 143/143 verde.
- `ModuleBoundaryTests` -> 12/12 verde.

---

## Fase 7 - Remocao de duplicacoes e shims temporarios

**Estado:** Concluida

### Tarefas

- [x] Remover `RoyalIdentity/Utils/CryptoRandom.cs`.
- [x] Remover `RoyalIdentity/Utils/Base64Url.cs`.
- [x] Remover `RoyalIdentity/Utils/PasswordHash.cs`.
- [x] Remover `RoyalIdentity/Utils/TimeConstantComparer.cs`.
- [x] Remover partes duplicadas de `HashExtensions`.
- [x] Remover/mover `ECKeyHelper` duplicado.
- [x] Remover/mover `SecurityKeyExtensions` duplicado.
- [x] Remover/mover `X509CertificateExtensions.CreateThumbprintCnf` duplicado.
- [x] Remover/mover enums duplicados de key encoding/serialization.
- [x] Rodar `rg` para garantir que nao ha novos usos dos tipos antigos.
- [x] Adicionar teste de arquitetura garantindo que o core nao expoe os tipos duplicados removidos.

### Resultado da Fase 7

Concluida em 2026-06-22. Os tipos migrados foram removidos do core, sem shims delegadores:
- `CryptoRandom`, `Base64Url`, `PasswordHash`, `TimeConstantComparer`, `HashExtensions` e `ECKeyHelper` foram removidos.
- `SecurityKeyExtensions` existe apenas em `RoyalIdentity.Security.Keys`.
- `CreateThumbprintCnf` existe apenas em `RoyalIdentity.Security.Certificates`.
- `KeyParameters`, `KeyEncoding` e `KeySerializationFormat` existem apenas em `RoyalIdentity.Security.Keys`.

O core preserva apenas responsabilidades de IdP: stores, realm, tokens, adapters e factory baseada em `KeyOptions`.
`ModuleBoundaryTests` garante que `RoyalIdentity` nao expoe mais os tipos duplicados de certificado/chaves.

---

## Fase 8 - Verificacao ampla, documentacao e fechamento

**Estado:** Concluida

### Tarefas

- [x] Rodar `dotnet test Tests.Security` - 116 testes aprovados
- [x] Rodar `dotnet test Tests.Identity` - 13 testes aprovados
- [x] Rodar `dotnet test Tests.UserAccounts` - 91 testes aprovados
- [x] Rodar `dotnet test Tests.Architecture` - 15 testes aprovados
- [x] Rodar testes de integração focados - 202 testes aprovados (Tests.Integration)
- [x] Rodar testes de pipelines - 3 testes aprovados
- [x] Rodar `dotnet test RoyalIdentity.sln` suite completa - 440 testes aprovados
- [x] Atualizar `backlog-001.md` - projeto marcado concluído
- [x] Registrar comandos e resultados neste plano

### Resultado da Fase 8

Concluida em 2026-06-22. Biblioteca reutilizavel entregue e consumidores migrados com validacao completa.

**Resumo de execução:**
- Build/test completo: 0 erros; warnings pre-existentes/de ambiente (`NETSDK1086`, `NU1903`, `NU1510`, `NU1701` e nullable/obsoleto em projetos nao alterados)
- Testes por suíte:
  - Tests.Security: 116/116
  - Tests.Identity: 13/13
  - Tests.Pipelines: 3/3
  - Tests.UserAccounts: 91/91
  - Tests.Integration: 202/202
  - Tests.Architecture: 15/15
  - **Total: 440/440 testes aprovados**

**Invariantes preservados:**
- RoyalIdentity.Security não referencia RoyalIdentity*, ASP.NET Core ou domínio de IdP
- Hashes de senha $RIPWD$ continuam verificáveis
- Tokens, authorization codes e refresh tokens mantêm entropia criptográfica
- Chaves antigas continuam validando assinaturas
- Comparação constante protege material sensível
- Todos os componentes são stateless e thread-safe
- Base64Url sem padding em contextos de protocolo
- Realm/client/resource isolation mantido no core, não em RoyalIdentity.Security

**Critérios de aceite cumpridos:**
- RoyalIdentity.Security compila em net10.0.
- Nenhuma referencia a RoyalIdentity*, Microsoft.AspNetCore*.
- Tests.Security cobre todos os componentes (116 testes).
- RoyalIdentity, Storage.InMemory e UserAccounts usam componentes reutilizaveis.
- Zero duplicacao ativa de tipos criptograficos no core.
- Testes de key material continuam verdes.
- Testes de token/signing/PKCE/secret continuam verdes.
- Contract tests de UserAccounts continuam verdes.
- Architecture tests garantem as novas fronteiras.

---

## Invariantes a preservar

- O projeto `RoyalIdentity.Security` nao conhece realm, client, scope, token store, user store ou pipelines.
- O modulo puro `RoyalIdentity.UserAccounts` continua sem dependencia do core `RoyalIdentity`.
- Hashes de senha `$RIPWD$` continuam verificaveis; o formato experimental pre-release `$PBKDF2$` e rejeitado.
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
- Nao ha duplicacao ativa de `CryptoRandom`, `Base64Url`, password hash, fixed-time compare, certificados ou key material.
- Testes de key material migrados de `Tests.Identity` continuam verdes.
- Testes de token/signing/PKCE/secret continuam verdes.
- Contract tests de `UserAccounts` continuam verdes.
- Architecture tests garantem as novas fronteiras.

---

## Riscos

- **Compatibilidade de password hash:** `$PBKDF2$` era formato experimental pre-release e foi removido antes da release
  final; seeds/testes devem usar `$RIPWD$`. Upgrades futuros de parametros devem introduzir deteccao de rehash junto
  com a orquestracao no dominio de contas.
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
- **Mudanca de contrato no `Verify`:** trocar throw-on-malformed por `bool` e melhor (sem throw em fluxo esperado),
  mas e uma mudanca de comportamento; garantir que `DefaultPasswordProtector` e chamadores nao dependam da excecao
  anterior.

---

## Referencias

- [backlog-001.md](../backlogs/backlog-001.md) - item `Projeto compartilhado de seguranca (RoyalIdentity.Security)`.
- [plans-roadmap-01.md](plans-roadmap-01.md) - KMS e evolucao de seguranca.
- [foundation/architecture.md](../foundation/architecture.md) - arquitetura modular e futuro KMS.
- [ADR-013](../../adrs/ADR-013.md) - arquitetura modular e fronteiras.
- [ADR-015](../../adrs/ADR-015.md) - modulo `UserAccounts` e modulo puro sem dependencia do core.
- [ADR-016](../../adrs/ADR-016.md) - biblioteca tecnica compartilhada `RoyalIdentity.Security` (emenda o ADR-013).
- [fase5-useraccount-domain.review-001.md](../reviews/user-accounts/fase5-useraccount-domain.review-001.md) - nota original sobre `SubjectIdGenerator` e `RoyalIdentity.Security`.
