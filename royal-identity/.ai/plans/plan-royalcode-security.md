# Plan: Projeto compartilhado de seguranca (`RoyalCode.Security`)

## Status: PLANEJADO - 0 de 8 fases concluidas

## Progresso

`----------` **0%** - 0 de 8 fases

| Fase | Estado |
|---|---|
| Fase 1 - Esqueleto, solution folders e guardrails de dependencia | Pendente |
| Fase 2 - Random, Base64Url, hashing basico e comparacao constante | Pendente |
| Fase 3 - Password hashing reutilizavel e compatibilidade legado | Pendente |
| Fase 4 - Key material, `KeyParameters` e helpers de chaves | Pendente |
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

Este plano ajusta o nome do projeto para **`RoyalCode.Security`**, porque o escopo desejado e uma biblioteca
reutilizavel de componentes tecnicos, nao um modulo de dominio do IdP. A decisao de introduzir o tier `RoyalCode.*`
(distinto de `RoyalIdentity.*`) esta registrada no [ADR-016](../../adrs/ADR-016.md), que emenda o ADR-013.

O projeto deve conter apenas componentes genericos: cripto utilitaria, identificadores opacos, encoding, hashing,
comparacao constante, password hashing reutilizavel e material de chaves. Ele nao deve conter fluxos OIDC,
conhecimento de realm, clients, stores, pipelines, ASP.NET DataProtection, nem abstracoes de borda do IdP.

---

## Objetivo

1. Criar o projeto `RoyalCode.Security` dentro da solution `RoyalIdentity.sln`.
2. Colocar `RoyalCode.Security` no virtual folder **`src`** da solution.
3. Criar o projeto de testes `Tests.RoyalCode.Security`.
4. Colocar `Tests.RoyalCode.Security` no virtual folder **`test`** da solution.
5. Migrar para `RoyalCode.Security` as primitivas genericas atualmente espalhadas no core:
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

- Criar um modulo de dominio `RoyalIdentity.Security`. O projeto e `RoyalCode.Security` e nao conhece o IdP.
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

- Projeto de componentes: `RoyalCode.Security`.
- Projeto de testes: `Tests.RoyalCode.Security`.
- Fisicamente, ambos ficam no diretorio raiz da solution, seguindo o padrao atual dos projetos:

```text
RoyalCode.Security/
Tests.RoyalCode.Security/
```

- Virtual folders na solution:
  - `RoyalCode.Security` fica em `src`.
  - `Tests.RoyalCode.Security` fica em `test`.

### Dependencias

- `RoyalCode.Security` nao referencia `RoyalIdentity`, `RoyalIdentity.Pipelines`, `RoyalIdentity.UserAccounts`,
  storage, Razor, Server ou ASP.NET.
- `RoyalCode.Security` remove o `FrameworkReference` global `Microsoft.AspNetCore.App` no `.csproj`, como o modulo
  puro `RoyalIdentity.UserAccounts` ja faz.
- Dependencias permitidas:
  - BCL (`System.Security.Cryptography`, `System.Text`, etc.);
  - `Microsoft.IdentityModel.Tokens`, quando necessario para `SecurityKey`, `SigningCredentials` e `JsonWebKey`.
- `RoyalIdentity`, `RoyalIdentity.Storage.InMemory` e `RoyalIdentity.UserAccounts` podem referenciar
  `RoyalCode.Security`.
- `RoyalIdentity.Pipelines` continua sem dependencia de dominio ou seguranca do IdP.

### Visibilidade da API

- A API de `RoyalCode.Security` e `public`. O projeto e consumido por tres projetos hoje (core, storage, modulo puro)
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
RoyalCode.Security.Cryptography.CryptoRandom
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
RoyalCode.Security.Encoding.Base64Url
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
- O .NET 9+ ja expoe `System.Buffers.Text.Base64Url`. Decisao: `RoyalCode.Security.Encoding.Base64Url` e uma
  fachada fina que delega ao tipo da BCL (mesma semantica de no-padding), em vez de reimplementar conversao manual.
  A fachada preserva o nome curto consumido pelo core e evita colisao de `using` com `System.Buffers.Text.Base64Url`.
  Dentro do namespace `RoyalCode.Security.Encoding`, qualquer uso de `System.Text.Encoding` deve ser totalmente
  qualificado ou via alias para evitar ambiguidade.

#### Hashing basico

Componentes:

```text
RoyalCode.Security.Cryptography.Hashing
RoyalCode.Security.Cryptography.HashExtensions
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
RoyalCode.Security.Cryptography.FixedTimeComparer
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
RoyalCode.Security.Passwords.PasswordHash
RoyalCode.Security.Passwords.PasswordHashOptions
RoyalCode.Security.Passwords.PasswordVerificationResult
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
  pertence ao consumidor/dominio de contas, **nao** a `RoyalCode.Security`. Este plano apenas entrega `NeedsRehash`;
  A orquestracao fica **deferido para o backlog** (registrar item). Sem isso, `NeedsRehash` permanece disponivel
  porem nao adotado para usuarios existentes - o que e aceitavel para o escopo deste plano, desde que registrado.
- Password policy e lockout permanecem no dominio de contas.

#### Key material e `KeyParameters`

Componentes:

```text
RoyalCode.Security.Keys.KeyParameters
RoyalCode.Security.Keys.KeySerializationFormat
RoyalCode.Security.Keys.KeyEncoding
RoyalCode.Security.Keys.ECKeyHelper
RoyalCode.Security.Keys.SecurityKeyExtensions
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
  - mover para `RoyalCode.Security.Keys` se for apenas wrapper tecnico;
  - manter no IdP se continuar sendo contrato da borda `IKeyManager`.

#### X509

Componentes candidatos:

```text
RoyalCode.Security.Certificates.X509CertificateExtensions
RoyalCode.Security.Certificates.X509
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
| `RoyalIdentity/Utils/CryptoRandom.cs` | `RoyalCode.Security.Cryptography.CryptoRandom` | Preservar semantica de tamanho em bytes e formatos. |
| `RoyalIdentity.UserAccounts/.../DefaultSubjectIdGenerator.cs` | wrapper sobre `CryptoRandom` | O contrato `ISubjectIdGenerator` fica no modulo. |
| `RoyalIdentity/Utils/Base64Url.cs` | `RoyalCode.Security.Encoding.Base64Url` | Usar nativo do .NET se possivel. |
| `RoyalIdentity/Extensions/HashExtensions.cs` | `RoyalCode.Security.Cryptography.HashExtensions` | Chamar `Hashing` central. |
| `RoyalIdentity/Utils/CryptoHelper.cs` | parte em `Hashing`, parte fica no IdP | OIDC hash claims continuam no core. |
| `RoyalIdentity/Utils/PkceHelper.cs` | fica no IdP, usando `Hashing`/`Base64Url` | PKCE e protocolo OIDC. |
| `RoyalIdentity/Utils/TimeConstantComparer.cs` | `RoyalCode.Security.Cryptography.FixedTimeComparer` | Corrigir implementacao. |
| `RoyalIdentity/Utils/PasswordHash.cs` | `RoyalCode.Security.Passwords.PasswordHash` | Adicionar formato versionado e verificar legado. |
| `RoyalIdentity/Users/Defaults/DefaultPasswordProtector.cs` | fica no IdP, usa `PasswordHash` | Adapter local, sem interface compartilhada nova. |
| `RoyalIdentity/Models/Keys/KeyParameters.cs` | `RoyalCode.Security.Keys.KeyParameters` | Remover dependencia de `KeyOptions`; factory do IdP fica no core. |
| `RoyalIdentity/Models/Keys/KeyEncoding.cs` | `RoyalCode.Security.Keys.KeyEncoding` | Mover. |
| `RoyalIdentity/Models/Keys/KeySerializationFormat.cs` | `RoyalCode.Security.Keys.KeySerializationFormat` | Mover. |
| `RoyalIdentity/Utils/ECKeyHelper.cs` | `RoyalCode.Security.Keys.ECKeyHelper` | Mover com testes. |
| `RoyalIdentity/Extensions/SecurityKeyExtensions.cs` | `RoyalCode.Security.Keys.SecurityKeyExtensions` | Mover com testes de public key. |
| `RoyalIdentity/Utils/X509.cs` | `RoyalCode.Security.Certificates.*` | Mover thumbprint primeiro; store finder se valer o custo. |

---

## Arquitetura alvo

```text
RoyalIdentity.Pipelines

RoyalCode.Security
  - sem referencia ao IdP
  - sem ASP.NET
  - componentes tecnicos reutilizaveis

RoyalIdentity
  -> RoyalIdentity.Pipelines
  -> RoyalCode.Security

RoyalIdentity.Storage.InMemory
  -> RoyalIdentity
  -> RoyalCode.Security (se necessario diretamente)

RoyalIdentity.UserAccounts
  -> RoyalCode.Security
  - nao referencia RoyalIdentity
  - nao referencia ASP.NET

RoyalIdentity.UserAccounts.Integration
  -> RoyalIdentity
  -> RoyalIdentity.UserAccounts

RoyalIdentity.UserAccounts.PostgreSql / Sqlite
  -> RoyalIdentity.UserAccounts
```

Guardrails:

- `RoyalCode.Security` nunca referencia `RoyalIdentity*`.
- `RoyalCode.Security` nunca referencia `Microsoft.AspNetCore*`.
- `RoyalIdentity.UserAccounts` pode referenciar `RoyalCode.Security`, mas continua sem referencia ao core.
- O core continua sem referencia ao modulo `UserAccounts`.

---

## Ordem de execucao

> **Protocolo aditivo-depois-deleta (vale para todo o plano):** as Fases 2 a 4 **adicionam** os componentes em
> `RoyalCode.Security` sem remover nada do core - os tipos originais continuam compilando e em uso. A Fase 5/6 troca
> os consumidores para os tipos novos. A **remocao** dos tipos originais do core acontece **exclusivamente na Fase 7**.
> Onde a tabela de mapeamento ou as tarefas dizem "mover", leia-se "adicionar em `RoyalCode.Security` agora, remover
> do core na Fase 7". Isso evita janela de build quebrado entre fases (ex.: core sem `KeyParameters` antes da troca).

1. Criar projetos e guardrails antes de adicionar codigo.
2. Adicionar primitivas pequenas primeiro (`Base64Url`, `CryptoRandom`, hash, fixed-time compare).
3. Adicionar password hashing com compatibilidade legado antes de trocar `DefaultPasswordProtector`.
4. Adicionar key material depois que a suite de utilitarios estiver verde.
5. Atualizar consumidores em ondas: core, storage/fake, modulo puro.
6. Remover duplicacoes apenas quando as suites focadas estiverem verdes (Fase 7).

---

## Fase 1 - Esqueleto, solution folders e guardrails de dependencia

**Estado:** Pendente

### Tarefas

- [ ] Criar projeto `RoyalCode.Security/RoyalCode.Security.csproj`.
- [ ] Remover `FrameworkReference Include="Microsoft.AspNetCore.App"` do projeto.
- [ ] Adicionar package/reference minimo para `Microsoft.IdentityModel.Tokens` apenas se a Fase 4 precisar.
- [ ] Criar marker interno/publico simples para testes de arquitetura, se necessario.
- [ ] Criar projeto `Tests.RoyalCode.Security/Tests.RoyalCode.Security.csproj`.
- [ ] Referenciar `RoyalCode.Security` no projeto de testes.
- [ ] Adicionar os projetos na solution.
- [ ] Colocar `RoyalCode.Security` no virtual folder `src`.
- [ ] Colocar `Tests.RoyalCode.Security` no virtual folder `test`.
- [ ] Estender o projeto existente `Tests.Architecture` (`ModuleBoundaryTests.cs` ja usa `GetReferencedAssemblies()`
  + parsing de `.csproj`; seguir esse mesmo padrao) garantindo:
  - [ ] `RoyalCode.Security` nao referencia `RoyalIdentity`;
  - [ ] `RoyalCode.Security` nao referencia `RoyalIdentity.UserAccounts`;
  - [ ] `RoyalCode.Security` nao referencia `Microsoft.AspNetCore*`;
  - [ ] `RoyalIdentity.UserAccounts` pode referenciar `RoyalCode.Security` sem quebrar a regra de modulo puro.

### Resultado da Fase 1

Projetos criados, solution organizada e guardrails prontos antes de qualquer migracao de comportamento.

---

## Fase 2 - Random, Base64Url, hashing basico e comparacao constante

**Estado:** Pendente

### Tarefas

- [ ] Implementar `CryptoRandom` em `RoyalCode.Security`.
- [ ] Implementar `Base64Url` em `RoyalCode.Security`.
- [ ] Implementar `Hashing` e `HashExtensions` genericos.
- [ ] Implementar `FixedTimeComparer` usando `CryptographicOperations.FixedTimeEquals`.
- [ ] Adicionar testes de `CryptoRandom`:
  - [ ] tamanho em bytes preservado para `CreateRandomKey`;
  - [ ] `CreateUniqueId` em Base64Url sem padding;
  - [ ] `CreateUniqueId` em Base64;
  - [ ] `CreateUniqueId` em Hex;
  - [ ] `Next` e ranges basicos;
  - [ ] teste de sanidade de unicidade sem depender de probabilidade fragil.
- [ ] Adicionar testes de `Base64Url`:
  - [ ] vetores conhecidos de round-trip;
  - [ ] inputs com e sem padding;
  - [ ] input invalido em `Decode` e `TryDecode`.
- [ ] Adicionar testes de hashing:
  - [ ] SHA256/SHA512 com vetores conhecidos;
  - [ ] extensoes retornam o mesmo formato legado esperado;
  - [ ] left-half hash base64url com algoritmo 256/384/512.
- [ ] Adicionar testes de `FixedTimeComparer`:
  - [ ] igualdade verdadeira;
  - [ ] igualdade falsa;
  - [ ] tamanhos diferentes;
  - [ ] comparacao UTF-8;
  - [ ] comparacao Base64.

### Resultado da Fase 2

Primitivas pequenas existem em `RoyalCode.Security` com testes proprios, ainda sem trocar consumidores.

---

## Fase 3 - Password hashing reutilizavel e compatibilidade legado

**Estado:** Pendente

### Tarefas

- [ ] Implementar `PasswordHashOptions`.
- [ ] Implementar `PasswordVerificationResult`.
- [ ] Implementar `PasswordHash.Create(...)` com formato novo versionado.
- [ ] Implementar `PasswordHash.Verify(...)`.
- [ ] Implementar verificacao do formato legado atual `$PBKDF2$.{salt}.{hash}`.
- [ ] Implementar `NeedsRehash(...)`.
- [ ] Registrar no backlog a orquestracao de rehash-on-login (consumidor/dominio de contas), deixando claro que
  `NeedsRehash` e entregue aqui mas a adocao para hashes legados fica fora do escopo deste plano.
- [ ] Adicionar testes:
  - [ ] senha correta valida hash novo;
  - [ ] senha errada falha;
  - [ ] hash malformado retorna falha (nao lanca, nao autentica);
  - [ ] formato legado atual continua verificavel;
  - [ ] `NeedsRehash` detecta iteracoes/algoritmo antigos;
  - [ ] salts diferentes geram hashes diferentes para a mesma senha;
  - [ ] hash gerado nao contem a senha em texto claro.

### Resultado da Fase 3

Password hashing reutilizavel pronto, sem interface compartilhada nova e com compatibilidade para hashes existentes.

---

## Fase 4 - Key material, `KeyParameters` e helpers de chaves

**Estado:** Pendente

### Tarefas

> Fase aditiva: adicionar os tipos em `RoyalCode.Security` sem remover os originais do core (remocao e Fase 7).
> Antes da Fase 4, reconciliar a edicao em andamento de `RoyalIdentity/Models/Keys/KeyParameters.cs` (ver Riscos),
> para que a copia adicionada parta do estado correto.

- [ ] Adicionar `KeyEncoding` em `RoyalCode.Security` (original permanece ate a Fase 7).
- [ ] Adicionar `KeySerializationFormat` em `RoyalCode.Security`.
- [ ] Adicionar `ECKeyHelper` em `RoyalCode.Security`.
- [ ] Adicionar `SecurityKeyExtensions` em `RoyalCode.Security`.
- [ ] Adicionar `KeyParameters` em `RoyalCode.Security` sem dependencia de `RoyalIdentity.Options.KeyOptions`.
- [ ] Criar factory generica para key material quando nao couber no construtor de `KeyParameters`.
- [ ] Decidir e implementar o destino de `ValidationKeysInfo`.
- [ ] Decidir e registrar no plano: mover `X509CertificateExtensions.CreateThumbprintCnf()` (se puramente tecnico e
  testavel com certificado em memoria) ou manter no core.
- [ ] Decidir e registrar no plano: mover o helper fluente `X509` de busca em certificate stores (so se nao exigir
  store do SO / nao fragilizar CI) ou manter no core.
- [ ] Migrar testes existentes de `Tests.Identity/Keys/KeyParametersTests.cs` para `Tests.RoyalCode.Security`.
- [ ] Adicionar novos testes:
  - [ ] RSA XML round-trip;
  - [ ] RSA JSON round-trip;
  - [ ] RSA assina e verifica;
  - [ ] ECDsa XML round-trip;
  - [ ] ECDsa JSON round-trip;
  - [ ] ECDsa assina e verifica;
  - [ ] symmetric key Base64 round-trip;
  - [ ] symmetric key Hex round-trip;
  - [ ] `WithoutPrivateKey` remove material privado e preserva `KeyId`;
  - [ ] `GetValidationKey` gera JWK publico sem material privado;
  - [ ] algoritmo nao suportado falha explicitamente.

### Resultado da Fase 4

Material de chaves fica disponivel como componente reutilizavel para IdP e futuro KMS.

---

## Fase 5 - Troca no core `RoyalIdentity`

**Estado:** Pendente

### Tarefas

- [ ] Adicionar referencia de `RoyalIdentity` para `RoyalCode.Security`.
- [ ] Atualizar usos de `RoyalIdentity.Utils.CryptoRandom` para `RoyalCode.Security`.
- [ ] Atualizar usos de `Base64Url`.
- [ ] Atualizar `HashExtensions`/hash helpers para usarem `RoyalCode.Security`.
- [ ] Atualizar `TimeConstantComparer` para usar `FixedTimeComparer` ou remover o tipo local.
- [ ] Atualizar o call-site de PKCE em `PkceMatchValidator` para usar `FixedTimeComparer` (in-scope, mesmo o
  validator permanecendo no core).
- [ ] Atualizar o call-site de client secret em `SecretEvaluatorBase` para usar `FixedTimeComparer` (in-scope; o
  evaluator NAO migra, apenas a comparacao interna muda).
- [ ] Adicionar/ajustar testes de regressao de PKCE e de autenticacao de client secret apos a troca do comparador.
- [ ] Atualizar `DefaultPasswordProtector` para usar `RoyalCode.Security.Passwords.PasswordHash`, mapeando
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

O core usa `RoyalCode.Security` para primitivas, mas conserva regras OIDC, realm, clients, tokens e stores no IdP.

---

## Fase 6 - Troca em `UserAccounts`, fake in-memory e testes de borda

**Estado:** Pendente

### Tarefas

- [ ] Adicionar referencia de `RoyalIdentity.UserAccounts` para `RoyalCode.Security`.
- [ ] Trocar `DefaultSubjectIdGenerator` para chamar `CryptoRandom.CreateUniqueId(32, Base64Url)`.
- [ ] Garantir que o modulo puro continua sem referencia ao core e sem ASP.NET.
- [ ] Atualizar `RoyalIdentity.Storage.InMemory` para usar `RoyalCode.Security` onde ainda usa utilitarios do core.
- [ ] Atualizar seeds do fake/in-memory que criam password hashes.
- [ ] Confirmar que `PasswordProtectorAccountHasher` continua apenas como adapter de borda, sem mover para
  `RoyalCode.Security`.
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

- [ ] Rodar `dotnet test Tests.RoyalCode.Security`.
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

- O projeto `RoyalCode.Security` nao conhece realm, client, scope, token store, user store ou pipelines.
- O modulo puro `RoyalIdentity.UserAccounts` continua sem dependencia do core `RoyalIdentity`.
- Hashes de senha legados continuam verificaveis.
- Tokens, authorization codes, refresh tokens e JWT ids continuam usando entropia criptografica.
- Valores Base64Url continuam sem padding quando usados em protocolo.
- Comparacao de secrets e PKCE nao deve usar comparacao string early-exit para material sensivel.
- Chaves de assinatura antigas continuam carregando e validando tokens emitidos antes da migracao.
- JWKs publicos nao devem expor material privado.
- A escolha de algoritmo por realm/client/resource continua no core, nao em `RoyalCode.Security`.
- DataProtection do ASP.NET continua fora deste projeto.
- Todos os componentes de `RoyalCode.Security` sao stateless e thread-safe: sao helpers estaticos chamados
  concorrentemente em hot paths (emissao de token, autenticacao). Isso e propriedade load-bearing ao extrair para
  biblioteca compartilhada - preservar via APIs thread-safe da BCL (`RandomNumberGenerator`, `SHA*.HashData`,
  PBKDF2 stateless), sem estado mutavel compartilhado.

---

## Criterios globais de aceite

- `RoyalCode.Security` compila em `net10.0`.
- `RoyalCode.Security` nao referencia `RoyalIdentity*`.
- `RoyalCode.Security` nao referencia `Microsoft.AspNetCore*`.
- `Tests.RoyalCode.Security` cobre todos os componentes migrados.
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
- **Dependencia pesada:** `RoyalCode.Security` nao deve herdar ASP.NET por acidente via `Directory.Build.props`.
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
- [ADR-016](../../adrs/ADR-016.md) - tier tecnico compartilhado `RoyalCode.*` (emenda o ADR-013).
- [fase5-useraccount-domain.review-001.md](../reviews/user-accounts/fase5-useraccount-domain.review-001.md) - nota original sobre `SubjectIdGenerator` e `RoyalIdentity.Security`.
