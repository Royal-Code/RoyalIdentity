# Plan: Conformidade das respostas de erro do token endpoint com OAuth 2.1 (`plan-oauth21-token-error-responses`)

## Status: RASCUNHO - baseline normativa e fases definidas; implementação não iniciada

## Progresso

`░░░░` **0%** - 0 de 4 fases

| Fase | Estado |
|---|---|
| Fase 1 - Contrato explícito de erro e asserções exatas | Pendente |
| Fase 2 - Forma da requisição e autenticação do client | Pendente |
| Fase 3 - Taxonomia dos grants, scopes, resources e PKCE | Pendente |
| Fase 4 - Auditoria transversal, regressão e fechamento | Pendente |

> **Manutenção deste plano:** ao concluir as tarefas de uma fase, marque cada tarefa com `- [x]`,
> troque o **Estado** da fase para `Concluida` na tabela acima e atualize a barra de progresso
> (um bloco `█` por fase concluída, `%` e `X de 4`). Antes de fechar uma fase, confirme que decisões,
> critérios de aceite, testes e invariantes relacionados foram aplicados.

---

## Contexto

### Fontes verificadas

- [RFC 6749 §5.2](https://www.rfc-editor.org/rfc/rfc6749.html#section-5.2) — define a resposta de erro
  do token endpoint OAuth 2.0, os seis códigos base, HTTP 401 para autenticação via `Authorization` e
  `WWW-Authenticate`.
- [OAuth 2.1 draft-15 §3.2.4](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-15#section-3.2.4)
  — baseline OAuth 2.1 vigente em 2026-07-30; preserva a taxonomia do RFC 6749 e classifica
  `code_verifier` enviado sem `code_challenge` como `invalid_request`.
- [OAuth 2.1 draft-15 §4.1.3](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-15#section-4.1.3)
  — exige presença de `code_verifier` se, e somente se, houve `code_challenge` e rejeição do downgrade.
- [RFC 7636 §4.6](https://www.rfc-editor.org/rfc/rfc7636.html#section-4.6) — falha na verificação do
  `code_verifier` contra o challenge resulta em `invalid_grant`.
- [RFC 8707 §§2.1/2.2](https://www.rfc-editor.org/rfc/rfc8707.html#section-2) — permite múltiplas ocorrências
  de `resource` e define a extensão `invalid_target`.
- [product.md](../foundation/product.md), [tech.md](../foundation/tech.md) e
  [structure.md](../foundation/structure.md) — o token endpoint é realm-aware, usa endpoint/context/pipeline,
  validators sinalizam falhas em `context.Response` e `RoyalIdentity.Pipelines` permanece neutro ao protocolo.
- [plan-data-operational-storage.md](plan-data-operational-storage.md) — authorization code inválido, ausente,
  consumido ou com binding divergente deve continuar indistinguível depois que um valor de code foi apresentado;
  o código OAuth desse conjunto permanece `invalid_grant`.
- [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md) — corrige o downgrade de PKCE e passa
  a depender da baseline de erros deste plano antes da sua Fase 3.
- [plans-roadmap-02.md](plans-roadmap-02.md) — posiciona este plano antes do hardening RFC 9700.

### Estado atual do código (verificado em 2026-07-30)

- **Payload base existente:** `ErrorResponseParameters` serializa `error`, `error_description` e `error_uri`;
  `ErrorResponseResult` responde JSON, usa status configurável e adiciona headers contra cache.
- **Status sem headers adicionais:** `ResponseHandler.Error(...)` recebe `statusCode`, mas o resultado não aceita
  headers específicos; `invalid_client` usa sempre o default 400 e nunca escreve `WWW-Authenticate`.
- **Overload ambíguo:** `ResponseHandlerExtensions.InvalidRequest(string, string?)` trata o primeiro argumento
  como descrição e sempre escreve `error=invalid_request`.
- **Código correto perdido:** `ClientResourceDecorator` e `ResourcesValidator` chamam esse overload com
  `invalid_scope`/`invalid_target`; o valor aparece em `error_description`, não necessariamente em `error`.
- **Testes permissivos:** vários testes de client credentials, refresh, code e `private_key_jwt` usam
  `Assert.Contains("<error>", body)` e podem passar quando somente a descrição contém o texto esperado.
- **Taxonomia parcial:** grant ausente e grant não suportado já retornam, respectivamente, `invalid_request` e
  `unsupported_grant_type`; authorization code/refresh token inexistente ou expirado retorna `invalid_grant`.
- **Grant não autorizado incorreto:** `GrantTypeValidator` usa `invalid_grant` quando um client autenticado não
  está autorizado para o grant; o código normativo é `unauthorized_client`.
- **Parâmetro obrigatório incorreto:** `LoadCode` usa `invalid_grant` quando `code` está ausente; ausência de
  parâmetro obrigatório pertence a `invalid_request`.
- **PKCE incompleto:** `PkceMatchValidator` aceita code sem challenge e o plano RFC 9700 ainda atribuía
  `invalid_grant` ao cenário `code_verifier` sem `code_challenge`.
- **Duplicidade não preservada semanticamente:** `IFormCollection` é convertido para `NameValueCollection`;
  valores repetidos permanecem agregados, mas não existe validação explícita de parâmetros não repetíveis antes
  da criação do contexto.
- **Mecanismos múltiplos podem ser aceitos:** `DefaultClientSecretChecker` percorre evaluators e interrompe no
  primeiro segredo encontrado; Basic válido pode vencer mesmo quando o body também contém outra credencial.
- **Evaluators com efeito observável:** `private_key_jwt` registra o `jti` no replay store durante a avaliação;
  uma requisição malformada com mecanismos múltiplos precisa ser rejeitada antes desse efeito.
- **Extensões suportadas:** o projeto usa `invalid_target` para RFC 8707 e admite extension grants por
  `IExtensionGrant`/`IExtensionsGrantsProvider`; a solução não pode restringir `error` a um enum fechado.
- **HTTP pré-protocolo:** método diferente de POST e media type incompatível retornam 405/415 por
  `EndpointErrorResults`; esses casos ocorrem antes da criação de um token context.

### Lacunas, conflitos e restrições

- **Maioria não é novidade do OAuth 2.1:** `unauthorized_client`, duplicidade, mecanismos múltiplos,
  `invalid_client` 401 e parâmetros obrigatórios já são requisitos do RFC 6749; este plano corrige a baseline
  OAuth 2.0 antes de aplicar a única adição explícita de PKCE do draft.
- **Draft evolutivo:** OAuth 2.1 ainda é Internet-Draft; a versão normativa fica fixada em `draft-15`, e uma
  versão posterior exige diff normativo documentado antes de alterar implementação ou aceite.
- **Core extensível:** erros definidos por RFCs de extensão e extension grants continuam strings válidas; não
  criar enum que impeça `invalid_target` ou códigos futuros.
- **Borda compartilhada:** `ResponseHandler`/`ErrorResponseResult` também atendem outros endpoints; alterações
  precisam de regressão, mas a auditoria normativa completa de authorize/revocation/userinfo está fora do escopo.
- **Anti-oracle vigente:** corrigir a categoria de um parâmetro ausente não autoriza diferenciar code
  inexistente, consumido, expirado ou com binding divergente quando um valor foi apresentado.
- **Sem compatibilidade externa:** não há clients de produção; corrigir respostas e testes diretamente, sem
  feature flags, aliases de erro ou período de dupla semântica.

### Superfícies impactadas a mapear

- `RoyalIdentity.Pipelines/Abstractions` e `Defaults` — payload, status e headers de respostas genéricas.
- `RoyalIdentity/Endpoints/TokenEndpoint.cs` — método/media type, leitura do form, duplicidade e dispatch de grant.
- `RoyalIdentity/Extensions/ResponseHandlerExtensions.cs` — construção explícita dos erros OAuth.
- `RoyalIdentity/Contexts/Decorators` e `Validators` — classificação por condição do request/grant.
- `RoyalIdentity/Contracts/Defaults/SecretsEvaluators` — detecção e avaliação de autenticação de client.
- `Tests.Pipelines`, `Tests.Identity` e `Tests.Integration` — contratos do writer, validators e fluxos HTTP.
- Extension grants e RFC 8707 — preservação da extensibilidade e de parâmetros explicitamente repetíveis.

---

## Objetivo

1. Fazer toda resposta protocolar de erro do token endpoint usar o valor exato de `error` definido pelo
   RFC 6749, OAuth 2.1 draft-15 ou uma extensão suportada.
2. Distinguir forma inválida da requisição, autenticação inválida, autorização do client, grant inválido,
   grant não suportado, scope inválido e target inválido sem depender de texto de descrição.
3. Responder falha de autenticação tentada via header com `invalid_client`, HTTP 401 e
   `WWW-Authenticate` correspondente.
4. Rejeitar parâmetros não repetíveis, credenciais múltiplas e mecanismos de autenticação múltiplos antes de
   validações com I/O ou efeito observável.
5. Classificar corretamente as combinações de PKCE incorporadas pelo OAuth 2.1.
6. Tornar os testes de protocolo exatos para JSON, status e headers, preservando anti-oracle e extensibilidade.

## Fora de escopo

- Auditar integralmente a taxonomia do authorization endpoint, revocation, introspection, UserInfo ou protected
  resources; executar somente regressões provocadas por helpers compartilhados.
- Alterar a semântica atômica ou persistência de authorization codes, refresh tokens ou replay handles.
- Implementar novos grants, DPoP, PAR, JAR/JARM, Device Authorization ou Token Exchange.
- Remover extension grants ou o erro `invalid_target` do RFC 8707.
- Implementar os demais requisitos do RFC 9700; destino:
  [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).
- Implementar Check Session ou telas administrativas.
- Transformar `error_description` em contrato público estável, salvo as igualdades anti-oracle já decididas.

---

## Decisões fechadas

- **DF1 — Baseline normativa versionada:** implementar RFC 6749 §5.2 com as alterações presentes no OAuth 2.1
  `draft-15` §3.2.4; antes do primeiro edit, se existir draft posterior, registrar o diff e atualizar este plano
  somente quando a nova versão mudar o requisito. Fonte: pedido humano + documento vigente em 2026-07-30.
- **DF2 — Contrato pelo campo `error`:** conformidade é determinada pelo valor JSON exato de `error`, status e
  headers normativos; encontrar um texto dentro de `error_description` não satisfaz o contrato. Fonte:
  RFC 6749 §5.2 + OAuth 2.1 draft-15 §3.2.4.
- **DF3 — Taxonomia extensível:** manter os seis erros base como constantes, mas não fechar o contrato em enum;
  erros definidos por extensões, incluindo `invalid_target`, continuam permitidos. Fonte: RFC 6749 §8.5,
  RFC 8707 e extension grants existentes.
- **DF4 — Construção explícita:** toda chamada que escolhe um erro informa separadamente `error`,
  `error_description`, status e headers necessários; remover overloads cuja posição possa ser interpretada como
  código ou descrição. Fonte: review do código atual.
- **DF5 — Pipelines neutro ao protocolo:** suporte genérico a status/headers pode permanecer em
  `RoyalIdentity.Pipelines`, mas regras OAuth e seleção dos códigos ficam em `RoyalIdentity`. Fonte:
  dependency rules do repositório.
- **DF6 — Basic inválido:** tentativa de autenticação por `Authorization: Basic` que falha retorna
  `invalid_client`, HTTP 401 e `WWW-Authenticate` com scheme `Basic`; falhas de mecanismos no body usam 400,
  salvo requisito mais específico do mecanismo. Fonte: RFC 6749 §5.2 / draft-15 §3.2.4.
- **DF7 — Validação antes de efeitos:** duplicidade e mecanismos múltiplos são rejeitados antes dos evaluators,
  em especial antes de registrar `jti` de `private_key_jwt`. Fonte: definição de `invalid_request` + invariante
  de replay.
- **DF8 — Repetição declarada:** parâmetros core conhecidos e não repetíveis falham com `invalid_request`;
  `resource` permanece multivalorado conforme RFC 8707; parâmetros desconhecidos/de extensão são preservados
  para validação pelo extension grant que os define. Fonte: draft-15 §3.2.4 + RFC 8707.
- **DF9 — Forma versus valor do grant:** parâmetro obrigatório ausente, repetido ou sintaticamente malformado
  retorna `invalid_request`; grant/code/refresh token apresentado com forma aceitável, mas inválido, expirado,
  revogado ou com binding divergente retorna `invalid_grant`. Fonte: RFC 6749 §5.2.
- **DF10 — Autorização do grant:** client autenticado que não pode usar o grant retorna
  `unauthorized_client`; grant não implementado pelo servidor retorna `unsupported_grant_type`. Fonte:
  RFC 6749 §5.2.
- **DF11 — PKCE por condição:** presença divergente entre verifier e challenge retorna `invalid_request`;
  verifier presente que não corresponde ao challenge retorna `invalid_grant`. Fonte: draft-15 §§3.2.4/4.1.3
  + RFC 7636 §4.6.
- **DF12 — HTTP antes do protocolo:** método inválido continua HTTP 405 com `Allow: POST` e media type inválido
  continua HTTP 415; são falhas HTTP anteriores a um token request válido e não usam códigos OAuth inventados
  para aparentar §3.2.4. Fonte: semântica HTTP + posição atual no endpoint.
- **DF13 — Descrição não é discriminador:** `error_description` permanece genérica, sem client secrets, codes,
  assertions ou tokens, e as equivalências anti-oracle de code continuam preservadas. Fonte:
  `plan-data-operational-storage.md` + segurança do produto.
- **DF14 — Breaking change direto:** atualizar helpers, callers e testes sem switches ou compatibilidade com os
  códigos incorretos atuais. Fonte: `AGENTS.md` + decisão humana de aceitar breaking changes.

---

## Histórico de decisões

**Discussão de aderência OAuth 2.1:**

- **Taxonomia OAuth 2.0 versus OAuth 2.1:** foi verificado que os seis códigos e quase todos os mapeamentos já
  existiam no RFC 6749.
  - **Resposta humana:** criar plano para corrigir o problema e alinhar as respostas ao draft do OAuth 2.1.
  - **Conclusão:** DF1-DF2 e plano próprio anterior ao hardening RFC 9700.
- **Diferença de PKCE:** o draft adiciona explicitamente verifier sem challenge a `invalid_request`.
  - **Conclusão:** DF11 substitui a expectativa anterior de `invalid_grant` no plano RFC 9700.
- **Compatibilidade:** o projeto está em desenvolvimento sem clients externos que exijam os códigos atuais.
  - **Conclusão:** DF14.

---

## Design alvo

### Contratos e bordas

- `ErrorResponseParameters`: continua sendo o payload genérico `error`/`error_description`/`error_uri`.
- `ErrorResponseResult`/`ResponseHandler`: aceitam status e headers HTTP explícitos sem importar constantes
  OAuth para `RoyalIdentity.Pipelines`.
- `ResponseHandlerExtensions`: expõe construção inequívoca de erro protocolar; wrappers como
  `InvalidRequest`, `InvalidGrant` e `InvalidClient` não possuem overload código/descrição ambíguo.
- Validação estrutural dos parâmetros core do token request ocorre sobre os valores originais de
  `IFormCollection` antes de `NameValueCollection` perder a distinção útil entre uma ocorrência e múltiplas
  ocorrências.
- Detecção de mecanismos de autenticação ocorre antes de `IClientSecretChecker`; avaliação criptográfica,
  storage e replay só começam depois que a forma da requisição é válida.
- `IExtensionGrant` continua podendo produzir códigos próprios documentados, usando o mesmo writer.

### Matriz normativa alvo

| Condição observável | `error` | HTTP/header |
|---|---|---|
| `grant_type` ausente, vazio, repetido ou malformado | `invalid_request` | 400 |
| Parâmetro core obrigatório ausente ou não repetível repetido | `invalid_request` | 400 |
| Credenciais múltiplas ou mais de um mecanismo de autenticação | `invalid_request` | 400 |
| `client_assertion`/`client_assertion_type` com forma incompleta | `invalid_request` | 400 |
| Autenticação do client ausente/inválida fora de header | `invalid_client` | 400 |
| Autenticação tentada via `Authorization` e inválida | `invalid_client` | 401 + `WWW-Authenticate` correspondente |
| Client autenticado não autorizado para o grant | `unauthorized_client` | 400 |
| Grant não suportado pelo servidor | `unsupported_grant_type` | 400 |
| `code`/`refresh_token` ausente ou sintaticamente malformado | `invalid_request` | 400 |
| Code/refresh apresentado, mas inválido, expirado, revogado ou com binding divergente | `invalid_grant` | 400 |
| Scope inválido, desconhecido, malformado ou acima do concedido | `invalid_scope` | 400 |
| Resource indicator inválido ou não autorizado | `invalid_target` | 400; extensão RFC 8707 |
| `code_verifier` presente sem challenge ou challenge presente sem verifier | `invalid_request` | 400 |
| `code_verifier` presente e diferente do challenge | `invalid_grant` | 400 |

Regras adicionais:

- `resource` pode ocorrer mais de uma vez e cada valor é preservado na ordem recebida.
- Parâmetros desconhecidos continuam sujeitos às regras de extensibilidade; não rejeitar extensão apenas por não
  pertencer ao conjunto core.
- Método/media type inválidos são respostas HTTP 405/415, fora da matriz OAuth acima.
- `error_description` auxilia diagnóstico sem carregar valores sensíveis e não substitui `error`.

### Modelo, dados e persistência

```text
Nenhuma entidade, tabela, migration, payload de configuração ou snapshot novo.

ErrorResponseParameters
  Error string required
  ErrorDescription string nullable
  ErrorUri string nullable

Resposta HTTP
  StatusCode int
  Headers coleção explícita e imutável durante a construção
  Body ErrorResponseParameters quando for erro OAuth
```

### Arquitetura alvo

```text
RoyalIdentity.Pipelines/
  Abstractions + Defaults
    writer genérico de JSON/status/headers, sem semântica OAuth

RoyalIdentity/
  Endpoints/TokenEndpoint
    validação HTTP e estrutural anterior ao context
  Contexts/Decorators
    autenticação e carregamento de grants
  Contexts/Validators
    autorização, resources/scopes e PKCE
  Extensions/ResponseHandlerExtensions
    seleção explícita de códigos OAuth

Tests.Pipelines/
  contrato do writer genérico

Tests.Identity + Tests.Integration/
  matriz de erro por validator e fluxo HTTP
```

### Segurança, concorrência e confiabilidade

- Nenhum evaluator de segredo, lookup de grant ou registro de replay ocorre em request estruturalmente inválido.
- Comparações PKCE continuam em tempo constante quando há verifier e challenge.
- Code apresentado e recusado não revela existência, consumo, client, redirect ou expiração por descrições
  diferentes onde o plano Operational exige equivalência.
- Falha de infraestrutura não é convertida em erro OAuth de credencial; continua propagando para a borda 5xx.
- Headers de autenticação nunca ecoam credenciais; `WWW-Authenticate` contém somente o scheme e parâmetros
  públicos necessários.
- Respostas OAuth continuam com `Cache-Control: no-store`; `Pragma: no-cache` pode permanecer por compatibilidade
  HTTP e não faz parte do critério de erro.

### Compatibilidade, migração e rollout

- Alteração imediata do contrato observável dos casos hoje incorretos.
- Atualizar testes e consumidores internos no mesmo commit/fase.
- Não adicionar aliases, flags por realm/client ou opção “OAuth 2.0 versus OAuth 2.1”.
- Não existe migração de dados.
- Extension grants customizados devem ser compilados/testados contra a assinatura final do writer quando forem
  consumidores públicos afetados.

---

## Ordem de execução

1. **Fase 1 (contrato explícito)** — elimina a ambiguidade e fornece asserções capazes de detectar erro real.
2. **Fase 2 (request/auth)** — rejeita forma e mecanismos múltiplos antes de efeitos observáveis.
3. **Fase 3 (taxonomia dos grants)** — corrige classificações sobre uma borda de resposta já confiável.
4. **Fase 4 (fechamento)** — reaudita callers, extensões e regressões compartilhadas.

Build/test padrão:

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
```

---

## Fase 1 - Contrato explícito de erro e asserções exatas

**Depende de:** DF2-DF5, DF13-DF14.

**Escopo:** `RoyalIdentity.Pipelines/Abstractions`, `RoyalIdentity.Pipelines/Defaults`,
`RoyalIdentity/Extensions/ResponseHandlerExtensions.cs`, callers de `InvalidRequest`, `Tests.Pipelines`,
helper de assertions em `Tests.Integration`.

**O que/como:** tornar status/headers parte explícita do resultado genérico, remover o overload ambíguo e migrar
callers para separar código e descrição. Criar uma única assertion de integração que desserialize o JSON e
verifique `error`, status, content type, cache e headers opcionais.

**Tarefas:**

- [ ] Inventariar todos os callers de `ResponseHandler.Error`, `context.Error`, `InvalidRequest`,
  `InvalidGrant` e `InvalidClient`.
- [ ] Estender o resultado genérico com headers explícitos sem introduzir referência OAuth em
  `RoyalIdentity.Pipelines`.
- [ ] Remover o overload `InvalidRequest(string, string?)` ou substituí-lo por assinatura não ambígua.
- [ ] Migrar callers de `invalid_scope`/`invalid_target` para definir o campo `error` corretamente.
- [ ] Preservar `error_uri` e serialização source-generated.
- [ ] Criar testes unitários para payload, status, headers, content type e cache.
- [ ] Criar `AssertTokenErrorAsync` equivalente que compare o valor JSON exato e aceite expectativa opcional de
  `WWW-Authenticate`.
- [ ] Substituir assertions por substring nos casos tocados pela fase.
- [ ] Executar regressão dos endpoints que compartilham o writer.

**Critérios de aceite:** nenhum método público/interno aceita dois `string` posicionais que possam significar
indistintamente código e descrição; `invalid_scope` e `invalid_target` aparecem no campo JSON `error`; status e
headers configurados chegam à resposta; testes falham se o código existir somente em `error_description`.

**Testes:**

```powershell
dotnet test Tests.Pipelines --filter "FullyQualifiedName~ErrorResponse"
dotnet test Tests.Integration --filter "FullyQualifiedName~ClientToken|FullyQualifiedName~RefreshToken|FullyQualifiedName~CodeToken"
```

### Resultado da Fase 1

*a preencher*

---

## Fase 2 - Forma da requisição e autenticação do client

**Depende de:** Fase 1, DF6-DF8, DF12-DF13.

**Escopo:** `TokenEndpoint`, conversão de form, `EvaluateClient`, `DefaultClientSecretChecker`,
secret evaluators, resultado HTTP genérico, `Tests.Identity`, `Tests.Integration`.

**O que/como:** validar cardinalidade e combinação de credenciais a partir do form original, antes da criação ou
avaliação com efeitos. Preservar parâmetros multivalorados declarados e produzir o status/header correto quando a
autenticação via `Authorization` falhar.

**Tarefas:**

- [ ] Definir a lista de parâmetros core não repetíveis e a exceção multivalorada `resource`.
- [ ] Validar repetição antes de achatar o form em `NameValueCollection`.
- [ ] Preservar todos os valores de `resource` e deixar parâmetros desconhecidos para validação pelo extension
  grant proprietário.
- [ ] Detectar Basic, post secret, client assertion e demais mecanismos suportados sem validar credenciais.
- [ ] Rejeitar múltiplas credenciais/mecanismos com `invalid_request` antes de chamar evaluators.
- [ ] Tratar pares incompletos de client assertion como request malformado.
- [ ] Garantir que request rejeitado não consulta client store, não valida JWT e não grava replay handle.
- [ ] Produzir `invalid_client` 401 e `WWW-Authenticate: Basic...` para tentativa Basic inválida/malformada.
- [ ] Manter `invalid_client` 400 para falha de autenticação no body, salvo requisito específico.
- [ ] Alinhar 405 com `Allow: POST` e 415 sem códigos OAuth inventados.
- [ ] Adicionar testes de parâmetros repetidos, resource repetido, Basic+post, Basic+assertion,
  post+assertion, assertion incompleta e ausência de autenticação obrigatória.

**Critérios de aceite:** nenhum request com dois mecanismos chega a um evaluator; `resource` repetido continua
funcional; parâmetros core repetidos retornam `invalid_request`; Basic inválido retorna exatamente 401 com
`WWW-Authenticate` Basic; requests rejeitados por forma não consomem `jti`; método/media type mantêm 405/415.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~ClientSecret|FullyQualifiedName~TokenEndpoint"
dotnet test Tests.Integration --filter "FullyQualifiedName~TokenError|FullyQualifiedName~PrivateKeyJwt|FullyQualifiedName~ClientToken"
```

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - Taxonomia dos grants, scopes, resources e PKCE

**Depende de:** Fases 1-2, DF9-DF11, conclusão de
[plan-replay-protection.md](plan-replay-protection.md) quando os testes de `private_key_jwt` exigirem o backing
final.

**Escopo:** `TokenEndpoint`, `GrantTypeValidator`, `LoadCode`, `LoadRefreshToken`, `PkceMatchValidator`,
resource decorators/validators, handlers de extension grant, `Tests.Identity`, `Tests.Integration`,
[plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).

**O que/como:** aplicar a matriz normativa a cada grant suportado, distinguindo ausência/má-formação de valor
apresentado e inválido. Corrigir PKCE sem enfraquecer consumo single-use ou comparação em tempo constante.

**Tarefas:**

- [ ] Retornar `unauthorized_client` quando o client autenticado não permite o grant.
- [ ] Preservar `unsupported_grant_type` para grant não implementado.
- [ ] Retornar `invalid_request` quando `code` ou `refresh_token` obrigatório estiver ausente/malformado.
- [ ] Preservar `invalid_grant` para code/refresh apresentado, mas inválido, expirado, revogado ou com binding
  divergente.
- [ ] Preservar equivalência anti-oracle dos cenários recusados de authorization code.
- [ ] Retornar `invalid_scope` no campo correto em client credentials e refresh/downscope.
- [ ] Preservar `invalid_target` no campo correto para RFC 8707.
- [ ] Rejeitar verifier sem challenge e challenge sem verifier com `invalid_request`.
- [ ] Preservar `invalid_grant` para verifier incorreto contra challenge existente.
- [ ] Confirmar que falha PKCE após consumo não torna authorization code reutilizável.
- [ ] Auditar extension grants para que usem código core ou de extensão documentado.
- [ ] Atualizar a Fase 3 do plano RFC 9700 para consumir esta baseline, sem duplicar o redesign de erros.
- [ ] Adicionar testes table-driven para cada linha da matriz normativa alvo nos três grants suportados.

**Critérios de aceite:** cada condição da matriz tem ao menos um teste que verifica `error` exato; ausência e
valor inválido não são confundidos; client sem autorização usa `unauthorized_client`; PKCE presence mismatch usa
`invalid_request`, mismatch criptográfico usa `invalid_grant`; single-use e anti-oracle permanecem verdes.

**Testes:**

```powershell
dotnet test Tests.Identity --filter "FullyQualifiedName~GrantType|FullyQualifiedName~Pkce|FullyQualifiedName~Resources"
dotnet test Tests.Integration --filter "FullyQualifiedName~TokenError|FullyQualifiedName~CodeToken|FullyQualifiedName~RefreshToken|FullyQualifiedName~ClientToken"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Auditoria transversal, regressão e fechamento

**Depende de:** Fases 1-3, DF1-DF14.

**Escopo:** todos os callers de resposta de erro, discovery quando anunciar auth methods, extension grants,
testes amplos, documentação, roadmap e plano RFC 9700.

**O que/como:** reexecutar o inventário semântico, remover assertions permissivas restantes nas respostas do token
endpoint, validar extensibilidade e registrar a versão do draft efetivamente implementada.

**Tarefas:**

- [ ] Repetir busca por todos os callers e classificar cada erro do token endpoint contra a matriz.
- [ ] Remover `Assert.Contains` e equivalentes quando o teste pretende validar o campo `error`.
- [ ] Confirmar que `error_description` e logs não contêm secret, assertion, code, verifier, refresh token ou
  replay handle.
- [ ] Confirmar que falhas de backing/infraestrutura continuam 5xx e não viram `invalid_client`/`invalid_grant`.
- [ ] Confirmar que discovery anuncia somente métodos de autenticação realmente testados.
- [ ] Validar extension grant de teste com código de erro próprio para provar que o contrato não foi fechado.
- [ ] Comparar o draft OAuth 2.1 vigente no início da fase com `draft-15` e registrar qualquer delta.
- [ ] Atualizar roadmap e o plano RFC 9700 com o status final e remover sobreposição executável.
- [ ] Executar build, suíte completa e `git diff --check`.

**Critérios de aceite:** não resta assertion por substring para validar `error` do token endpoint; os seis códigos
base e `invalid_target` têm cobertura exata; código de extensão continua serializável; nenhuma falha de
infraestrutura é mascarada; documentação identifica a versão normativa; solução completa está verde.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test RoyalIdentity.sln
git diff --check
```

### Resultado da Fase 4

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Código JSON exato | 1, 3, 4 | DF2-DF4 | `error` nunca depende da descrição | ErrorResponse + TokenError |
| HTTP 401/header | 1-2 | DF5-DF7 | Basic inválido retorna 401 + `WWW-Authenticate` | ClientSecret + TokenError |
| Forma validada antes de efeitos | 2 | DF7-DF8 | duplicidade/mecanismos múltiplos não chegam aos evaluators | TokenError + PrivateKeyJwt |
| Taxonomia dos grants | 3 | DF9-DF10 | ausência, autorização, suporte e grant inválido distintos | Code/Refresh/ClientToken |
| PKCE OAuth 2.1 | 3 | DF11 | presence mismatch e verifier incorreto têm códigos distintos | Pkce + CodeToken |
| Extensibilidade | 1, 3-4 | DF3, DF8 | `invalid_target` e erro custom continuam válidos | Resources + extension grant |
| Anti-oracle e sigilo | 3-4 | DF13 | respostas equivalentes e nenhum valor sensível | CodeSingleUse + logs |

---

## Invariantes a preservar

1. Toda avaliação de client, code, token, resource e replay continua realm-scoped.
2. Validators/decorators sinalizam falhas esperadas por `context.Response`, sem lançar.
3. `RoyalIdentity.Pipelines` permanece sem dependência do core ou de semântica OAuth.
4. Authorization codes permanecem single-use e a rejeição não cria oracle de existência/binding.
5. `private_key_jwt` continua fail-closed e não consome replay handle em request estruturalmente inválido.
6. `resource` continua multivalorado conforme RFC 8707.
7. Extension grants e códigos de erro de extensões continuam possíveis.
8. `error_description`, headers e logs nunca expõem credenciais ou artifacts.
9. Falhas de infraestrutura não são traduzidas em falhas de credencial/grant.
10. Não criar flag de compatibilidade, enum fechado de erros ou opção por client/realm.
11. Não alterar storage, migrations ou semânticas atômicas fechadas neste plano.
12. Não reintroduzir password grant.

---

## Critérios globais de conclusão

- Todas as linhas da matriz normativa alvo possuem teste HTTP ou unitário com `error` exato.
- Basic inválido possui cobertura de HTTP 401 e `WWW-Authenticate`.
- Duplicidade e múltiplos mecanismos são recusados antes de I/O/efeitos.
- OAuth 2.1 PKCE presence mismatch retorna `invalid_request`; verifier incorreto retorna `invalid_grant`.
- `invalid_target` e um erro de extension grant provam que o writer permanece extensível.
- Não restam assertions por substring para o contrato `error` do token endpoint.
- Plano RFC 9700 depende desta baseline e não contradiz sua classificação de PKCE.
- `dotnet build RoyalIdentity.sln`, `dotnet test RoyalIdentity.sln` e `git diff --check` estão verdes.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Draft muda durante execução | nova versão altera §3.2.4/PKCE | implementação nasce desatualizada | gate DF1 no início da Fase 4 | Aberto |
| Writer genérico ganha semântica OAuth | constantes OAuth entram em Pipelines | quebra de boundary | DF5 + teste de arquitetura | Aberto |
| Detecção múltipla ocorre após evaluator | `jti` é registrado antes do `invalid_request` | retry legítimo parece replay | preflight DF7 + teste negativo do store | Aberto |
| Certificado TLS é contado indevidamente | conexão possui certificado usado para outro fim | request Basic válido é recusado | detectar somente mecanismos que a composição trata como client auth | Aberto |
| Validação de duplicidade bloqueia RFC 8707 | regra genérica rejeita `resource` repetido | quebra de resource indicators | allowlist DF8 + regressão multiresource | Aberto |
| Enum fecha erros | tipo aceita somente seis valores | extension grants/RFC 8707 quebram | strings + teste de extensão DF3 | Aberto |
| Correção revela detalhes de code | descrições divergem por causa | oracle de existência/binding | preservar igualdade Operational + DF13 | Aberto |
| Testes continuam falsos positivos | assertion busca texto no body | regressão passa sem conformidade | helper único e auditoria Fase 4 | Aberto |
| Falha de backing vira erro OAuth | catch amplo em evaluator/handler | indisponibilidade mascarada como credencial inválida | teste 5xx e invariant 9 | Aberto |
| Alteração compartilhada afeta authorize | helper comum muda payload/redirect | regressão OIDC fora do token endpoint | suíte ampla na Fase 1/4 | Aberto |

---

## Diferidos e backlog

- **Taxonomia completa do authorization endpoint** — destino: fase própria futura se a regressão compartilhada
  revelar lacunas fora do escopo.
- **Erros de revocation/UserInfo/protected resources** — destino: planos dos respectivos endpoints; não ampliar
  este plano sem decisão explícita.
- **Demais requisitos OAuth 2.1/RFC 9700** — destino:
  [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).
- **Novos métodos de client authentication e sender-constrained tokens** — destino: hardening RFC 9700 ou plano
  específico da extensão.

---

## Referências

- [RFC 6749 — The OAuth 2.0 Authorization Framework](https://www.rfc-editor.org/rfc/rfc6749.html).
- [OAuth 2.1 draft-15](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-15).
- [RFC 7636 — Proof Key for Code Exchange](https://www.rfc-editor.org/rfc/rfc7636.html).
- [RFC 8707 — Resource Indicators for OAuth 2.0](https://www.rfc-editor.org/rfc/rfc8707.html).
- [plan-data-operational-storage.md](plan-data-operational-storage.md).
- [plan-replay-protection.md](plan-replay-protection.md).
- [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- `RoyalIdentity/Endpoints/TokenEndpoint.cs`.
- `RoyalIdentity/Extensions/ResponseHandlerExtensions.cs`.
- `RoyalIdentity.Pipelines/Defaults/ErrorResponseResult.cs`.
- `RoyalIdentity/Contexts/Decorators/EvaluateClient.cs`.
- `RoyalIdentity/Contracts/Defaults/DefaultClientSecretChecker.cs`.
- `RoyalIdentity/Contexts/Validators/GrantTypeValidator.cs`.
- `RoyalIdentity/Contexts/Validators/PkceMatchValidator.cs`.
- `Tests.Integration/Endpoints/ClientTokenTests.cs`.
- `Tests.Integration/Endpoints/CodeTokenTests.cs`.
- `Tests.Integration/Endpoints/RefreshTokenTests.cs`.
