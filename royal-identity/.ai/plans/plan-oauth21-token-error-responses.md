# Plan: Conformidade das respostas de erro do token endpoint com OAuth 2.1 (`plan-oauth21-token-error-responses`)

## Status: EM EXECUÇÃO - decisões fechadas (DF1-DF20); nenhuma pergunta aberta; Fases 1-2 concluídas

## Progresso

`██░░` **50%** - 2 de 4 fases

| Fase | Estado |
|---|---|
| Fase 1 - Contrato explícito de erro e asserções exatas | Concluida |
| Fase 2 - Forma da requisição e autenticação do client | Concluida |
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
  — baseline OAuth 2.1 vigente em 2026-07-31; preserva a taxonomia do RFC 6749 e classifica
  `code_verifier` enviado sem `code_challenge` como `invalid_request`.
- [OAuth 2.1 draft-15 §4.1.3](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-15#section-4.1.3)
  — exige presença de `code_verifier` se, e somente se, houve `code_challenge` e rejeição do downgrade.
- [RFC 7636 §4.6](https://www.rfc-editor.org/rfc/rfc7636.html#section-4.6) — falha na verificação do
  `code_verifier` contra o challenge resulta em `invalid_grant`.
- [RFC 7523 §3.2](https://www.rfc-editor.org/rfc/rfc7523.html#section-3.2) — JWT usado para autenticação de
  client que não é válido retorna `invalid_client`, não `invalid_request`.
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

### Estado atual do código (verificado em 2026-07-31)

- **Payload base existente:** `ErrorResponseParameters` serializa `error`, `error_description` e `error_uri`;
  `ErrorResponseResult` responde JSON, usa status configurável e adiciona headers contra cache.
- **Status sem headers adicionais:** `ResponseHandler.Error(...)` recebe `statusCode`, mas o resultado não aceita
  headers específicos; `invalid_client` usa sempre o default 400 e nunca escreve `WWW-Authenticate`.
- **Overload ambíguo:** `ResponseHandlerExtensions.InvalidRequest(string, string?)` trata o primeiro argumento
  como descrição e sempre escreve `error=invalid_request`.
- **Código correto perdido em dois formatos:** `ClientResourceDecorator`, `ResourcesDecorator`,
  `ResourcesValidator`, `AuthorizeMainValidator`, `AuthorizationResourcesValidator`, `LoadClient` e
  `RedirectUriValidator` passam constantes `Oidc.*.Errors.*` ao helper `InvalidRequest`. Há chamadas com um
  argumento, nas quais o código vira descrição, e chamadas com dois argumentos, nas quais o primeiro código
  também é tratado como descrição e `error` permanece `invalid_request`.
- **Testes permissivos:** vários testes de client credentials, refresh, code e `private_key_jwt` usam
  `Assert.Contains("<error>", body)` e podem passar quando somente a descrição contém o texto esperado.
- **Taxonomia parcial:** grant ausente e grant não suportado já retornam, respectivamente, `invalid_request` e
  `unsupported_grant_type`; authorization code/refresh token inexistente ou expirado retorna `invalid_grant`.
- **Grant não autorizado incorreto:** `GrantTypeValidator` usa `invalid_grant` quando um client autenticado não
  está autorizado para o grant; o código normativo é `unauthorized_client`.
- **Parâmetro obrigatório incorreto:** `LoadCode` usa `invalid_grant` quando `code` está ausente; ausência de
  parâmetro obrigatório pertence a `invalid_request`.
- **PKCE incompleto:** `PkceMatchValidator` aceita code sem challenge mesmo quando o request envia
  `code_verifier`; quando o code possui challenge e o verifier está ausente usa `invalid_grant`. O plano RFC
  9700 já possui DF18 com a classificação correta e dependência deste plano; a tarefa executável que ainda
  duplicava o primeiro cenário foi convertida nesta revisão em consumo da regressão entregue por este plano.
- **Método PKCE persistido desconhecido:** o branch `default` de `PkceMatchValidator` transforma um
  `code_challenge_method` desconhecido já armazenado no authorization code em `invalid_grant`. DF18 confirma o
  código; o que falta é a descrição genérica indistinguível e o log do método recusado.
- **Duplicidade preservada, mas não validada:** `AsNameValueCollection` chama `Add` para cada `StringValues`, e
  `NameValueCollection.GetValues` ainda distingue ocorrências. O problema é que `TryGet`/indexers e `Load` tratam
  parâmetros como escalares sem impor cardinalidade; a validação precisa ocorrer antes dessas leituras e antes
  da criação/dispatch do context, não necessariamente antes da conversão.
- **Mecanismos múltiplos podem ser aceitos:** `DefaultClientSecretChecker` percorre evaluators e interrompe no
  primeiro segredo encontrado; Basic válido pode vencer mesmo quando o body também contém outra credencial.
- **Precedência já duplicada no evaluator TLS:** `TlsClientAuthSecretEvaluator` deixa de avaliar certificado
  quando encontra assertion, secret ou header `Authorization`; o preflight precisa substituir essa decisão
  espalhada, não coexistir com uma segunda regra divergente.
- **Evaluators com efeito observável:** `private_key_jwt` registra o `jti` no replay store durante a avaliação;
  uma requisição malformada com mecanismos múltiplos precisa ser rejeitada antes desse efeito.
- **Assertion JWT mistura forma e autenticação:** par assertion/type incompleto cai hoje em `invalid_client`,
  embora seja `invalid_request`; após o par selecionar `private_key_jwt`, JWT não parseável, `sub` ausente,
  client desconhecido e validação criptográfica falha também convergem em `invalid_client`. Para estes últimos,
  RFC 7523 §3.2 confirma `invalid_client`; diferenciá-los criaria uma classificação incorreta e potencial oracle.
- **Extensões suportadas:** o projeto usa `invalid_target` para RFC 8707 e admite extension grants por
  `IExtensionGrant`/`IExtensionsGrantsProvider`; a solução não pode restringir `error` a um enum fechado.
- **HTTP pré-protocolo:** método diferente de POST e media type incompatível retornam 405/415 por
  `EndpointErrorResults`; esses casos ocorrem antes da criação de um token context.
- **405 sem `Allow`:** `EndpointErrorResults.MethodNotAllowed` define status 405, mas
  `ErrorResponseResult` não escreve `Allow`; adicionar `Allow: POST` será mudança corretiva, não preservação.
- **Semântica protocolar no projeto neutro:** `RoyalIdentity.Pipelines/Abstractions/EndpointErrorResults.cs`
  hardcoda `invalid_request`, `method_not_allowed`, `not_found` e `Invalid_content_type` no payload genérico. Isso
  já viola a intenção de DF5 e é corrigido por DF19, que move a seleção para `RoyalIdentity`.
- **Infraestrutura de testes ausente:** `Tests.Identity` não possui fixtures dos contexts/validators deste plano;
  os filtros `ClientSecret`, `TokenEndpoint`, `GrantType` e `Resources` não selecionam testes, enquanto `Pkce`
  seleciona somente `PkceHelperTests`. Também não existem ainda `TokenErrorTests` em `Tests.Integration` nem
  `ErrorResponseResultTests` em `Tests.Pipelines`.
- **Helper parcial existente:** `CodeSingleUseTests.ReadErrorAsync` já desserializa `error` e descrição, mas é
  privado e não verifica status/headers; deve ser extraído, não duplicado.

### Lacunas, conflitos e restrições

- **Maioria não é novidade do OAuth 2.1:** `unauthorized_client`, duplicidade, mecanismos múltiplos,
  `invalid_client` 401 e parâmetros obrigatórios já são requisitos do RFC 6749; este plano corrige a baseline
  OAuth 2.0 antes de aplicar a única adição explícita de PKCE do draft.
- **Draft evolutivo:** OAuth 2.1 ainda é Internet-Draft; a versão normativa fica fixada em `draft-15`, e uma
  versão posterior exige diff normativo documentado antes de alterar implementação ou aceite.
- **Core extensível:** erros definidos por RFCs de extensão e extension grants continuam strings válidas; não
  criar enum que impeça `invalid_target` ou códigos futuros.
- **Borda compartilhada:** `ResponseHandler`/`ErrorResponseResult` atendem também revocation, UserInfo,
  End Session e Protected Resource Metadata. `ResourcesValidator` é ainda mais sensível: a mesma instância
  genérica valida `AuthorizeContext`, `AuthorizeValidateContext` e `ClientCredentialsContext`, portanto a
  correção do campo `error` no token endpoint necessariamente toca authorize; DF20 assume esse ajuste mínimo em
  vez de separar o validator. `AuthorizationCodeContext` e `RefreshTokenContext` não passam por ele.
- **Transporte de erro do authorize já incorreto:** validators do authorize produzem hoje o mesmo JSON direto de
  `ErrorResponseResult`; depois de validar um `redirect_uri`, RFC 6749 §4.1.2.1 normalmente exige redirecionar o
  erro ao client. Esta dívida é real, mas não precisa ser absorvida pela correção do token endpoint.
- **Anti-oracle vigente:** corrigir a categoria de um parâmetro ausente não autoriza diferenciar code
  inexistente, consumido, expirado ou com binding divergente quando um valor foi apresentado.
- **Sem compatibilidade externa:** não há clients de produção; corrigir respostas e testes diretamente, sem
  feature flags, aliases de erro ou período de dupla semântica.

### Superfícies impactadas a mapear

- `RoyalIdentity.Pipelines/Abstractions` e `Defaults` — payload, `EndpointErrorResults`, status e headers de
  respostas genéricas, conforme DF19.
- `RoyalIdentity/Endpoints/TokenEndpoint.cs` — método/media type, leitura do form, duplicidade e dispatch de grant.
- `RoyalIdentity/Extensions/ResponseHandlerExtensions.cs` — construção explícita dos erros OAuth.
- `RoyalIdentity/Contexts/Decorators` e `Validators` — classificação por condição do request/grant, inclusive a
  borda compartilhada `ResourcesValidator` conforme DF20.
- `RoyalIdentity/Contracts/Defaults/SecretsEvaluators` — detecção e avaliação de autenticação de client.
- `Tests.Pipelines`, `Tests.Integration` e `Tests.Architecture` — writer genérico, matriz HTTP e guard de
  boundaries; `Tests.Identity` só recebe testes se surgir unidade pura que não replique a composição HTTP.
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

- Auditar integralmente a taxonomia e o transporte de erro do authorization endpoint, revocation,
  introspection, UserInfo ou protected resources; no authorize entra somente a correção do campo `error` do
  `ResourcesValidator` compartilhado (DF20), e as demais superfícies recebem apenas regressões provocadas por
  helpers compartilhados.
- Alterar a semântica atômica ou persistência de authorization codes, refresh tokens ou replay handles.
- Implementar novos grants, DPoP, PAR, JAR/JARM, Device Authorization ou Token Exchange.
- Remover extension grants ou o erro `invalid_target` do RFC 8707.
- Implementar os demais requisitos do RFC 9700; destino:
  [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).
- Implementar Check Session ou telas administrativas.
- Transformar `error_description` em contrato público estável, salvo as igualdades anti-oracle já decididas.

---

> **Perguntas ao humano:** nenhuma aberta. Q1, Q2 e Q3 foram respondidas em 2026-07-31 e promovidas a DF20,
> DF18 e DF19; o registro está em `Histórico de decisões`.

---

## Decisões fechadas

- **DF1 — Baseline normativa versionada:** implementar RFC 6749 §5.2 com as alterações presentes no OAuth 2.1
  `draft-15` §3.2.4; antes do primeiro edit, se existir draft posterior, registrar o diff e atualizar este plano
  somente quando a nova versão mudar o requisito. Fonte: pedido humano + documento vigente em 2026-07-31.
- **DF2 — Contrato pelo campo `error`:** conformidade é determinada pelo valor JSON exato de `error`, status e
  headers normativos; encontrar um texto dentro de `error_description` não satisfaz o contrato. Fonte:
  RFC 6749 §5.2 + OAuth 2.1 draft-15 §3.2.4.
- **DF3 — Taxonomia extensível:** manter os seis erros base como constantes, mas não fechar o contrato em enum;
  erros definidos por extensões, incluindo `invalid_target`, continuam permitidos. Fonte: RFC 6749 §8.5,
  RFC 8707 e extension grants existentes.
- **DF4 — Construção explícita:** toda chamada que escolhe um erro informa separadamente `error`,
  `error_description`, status e headers necessários; remover tanto o overload de dois `string` quanto helpers de
  descrição com um único `string` capazes de aceitar silenciosamente uma constante `Oidc.*.Errors.*`. Fonte:
  callers verificados no código atual.
- **DF5 — Pipelines neutro ao protocolo:** suporte genérico a status/headers pode permanecer em
  `RoyalIdentity.Pipelines`, mas regras OAuth e seleção dos códigos ficam em `RoyalIdentity`; DF19 fixa a forma
  exata dessa fronteira. Fonte: dependency rules do repositório.
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
- **DF12 — HTTP antes do protocolo:** método inválido permanece HTTP 405, mas passa a incluir `Allow: POST`, hoje
  ausente; media type inválido permanece HTTP 415. São falhas HTTP anteriores a um token request válido e não
  usam códigos OAuth inventados para aparentar §3.2.4. Fonte: semântica HTTP + código atual verificado.
- **DF13 — Descrição não é discriminador:** `error_description` permanece genérica, sem client secrets, codes,
  assertions ou tokens, e as equivalências anti-oracle de code continuam preservadas. Fonte:
  `plan-data-operational-storage.md` + segurança do produto.
- **DF14 — Breaking change direto:** atualizar helpers, callers e testes sem switches ou compatibilidade com os
  códigos incorretos atuais. Fonte: `AGENTS.md` + decisão humana de aceitar breaking changes.
- **DF15 — Assertion JWT inválida:** presença incompleta ou repetida do par
  `client_assertion`/`client_assertion_type` é forma inválida e retorna `invalid_request`; par completo com tipo
  de autenticação não suportado, JWT não parseável, claims obrigatórias ausentes, client desconhecido ou
  validação criptográfica falha retorna `invalid_client` com descrição genérica e sem distinguir a causa. Fonte:
  OAuth 2.1 draft-15 §3.2.4 + RFC 7523 §3.2.
- **DF16 — Topologia dos testes:** criar `ErrorResponseResultTests` em `Tests.Pipelines`, um único
  `Tests.Integration/Endpoints/TokenErrorTests.cs` para a matriz HTTP e extrair o `ReadErrorAsync` existente para
  um helper compartilhado que também valide status/headers. Não criar fixture de contexts em `Tests.Identity`
  apenas para satisfazer filtros; esse projeto recebe somente unidades puras que surgirem do design. Fonte:
  infraestrutura de testes verificada.
- **DF17 — Ownership do downgrade PKCE:** este plano implementa e testa a classificação do token endpoint; o
  plano RFC 9700 apenas consome a baseline concluída e mantém seus aceites de hardening, sem segunda tarefa de
  implementação. Fonte: `plan-rfc9700-security-hardening.md` DF18 + ordem do roadmap.
- **DF18 — `code_challenge_method` persistido desconhecido:** o branch `default` de `PkceMatchValidator` responde
  `invalid_grant` genérico com HTTP 400, tratando o authorization code inteiro como inválido e mantendo o método
  recusado fora da resposta; o detalhe fica somente em log. Resposta 5xx é reservada a falha de infraestrutura ou
  bug, não a um artefato que o client apresentou e o servidor não pode honrar. A descrição usa a mesma forma
  genérica dos demais códigos recusados, preservando a equivalência anti-oracle. Fonte: Q2 resposta humana
  (opção B) + DF9/DF13.
- **DF19 — Fronteira de `EndpointErrorResults`:** `RoyalIdentity.Pipelines` mantém somente writers e resultados
  HTTP neutros; as factories que escolhem códigos OAuth passam para `RoyalIdentity`, e nenhum código protocolar
  (`invalid_request`, `method_not_allowed`, `not_found`, `Invalid_content_type`) permanece hardcoded no projeto
  neutro. Fonte: Q3 resposta humana (opção A) + DF5.
- **DF20 — `ResourcesValidator` permanece compartilhado:** corrigir o validator genérico para escrever o código
  exato de `invalid_scope`/`invalid_target` no campo `error` em ambos os contexts, sem separá-lo por endpoint.
  As oito regras do validator são idênticas para authorize e client credentials, e os dois códigos são
  normativos nos dois endpoints, então a correção move o valor de `error_description` para `error` sem alterar a
  classificação de nenhum dos lados. O ajuste mínimo do authorize entra neste plano; o transporte redirect versus
  JSON continua diferido. Fonte: Q1 resposta humana (opção A) + `Pipes.cs` + RFC 6749 §§4.1.2.1/5.2 + RFC 8707
  §§2.1/2.2.

---

## Histórico de decisões

**Discussão de aderência OAuth 2.1:**

- **Taxonomia OAuth 2.0 versus OAuth 2.1:** foi verificado que os seis códigos e quase todos os mapeamentos já
  existiam no RFC 6749.
  - **Resposta humana:** criar plano para corrigir o problema e alinhar as respostas ao draft do OAuth 2.1.
  - **Conclusão:** DF1-DF2 e plano próprio anterior ao hardening RFC 9700.
- **Diferença de PKCE:** o draft adiciona explicitamente verifier sem challenge a `invalid_request`.
  - **Conclusão:** DF11 é a baseline implementável; o plano RFC 9700 já referencia essa classificação por DF18
    e deve remover somente a tarefa executável duplicada (DF17).
- **Compatibilidade:** o projeto está em desenvolvimento sem clients externos que exijam os códigos atuais.
  - **Conclusão:** DF14.

**Revisão externa de 2026-07-31:**

- **Estado factual e fronteiras:** foram corrigidos o estado do plano RFC 9700, o alcance dos helpers ambíguos,
  a ausência atual de `Allow: POST`, a semântica protocolar hardcoded em `EndpointErrorResults` e o uso
  compartilhado de `ResourcesValidator`.
  - **Conclusão:** Q1/Q3, DF4-DF5, DF12 e DF17.
- **Assertion malformada:** a proposta de responder `invalid_request` para JWT não parseável foi rejeitada. Uma
  vez selecionado o método de client authentication por assertion, RFC 7523 §3.2 exige `invalid_client`; somente
  forma incompleta/repetida do par de parâmetros permanece `invalid_request`.
  - **Conclusão:** DF15; não foi aberta pergunta para escolher contra um requisito normativo.
- **Testes:** os filtros antigos realmente não exercitam os validators pretendidos. Neste SDK, porém, filtro sem
  correspondência encerra com sucesso, não com exit code 1; por isso o problema é falso verde, não falha do
  comando. A localização dos testes foi fechada pela infraestrutura existente, sem criar uma fixture artificial.
  - **Conclusão:** DF16.
- **Cardinalidade do form:** a conversão atual preserva ocorrências via `Add`/`GetValues`; a exigência correta é
  validar cardinalidade antes de leitura escalar e efeitos, não necessariamente antes da conversão.
  - **Conclusão:** DF7-DF8.

**Respostas humanas de 2026-07-31:**

- **Q2 — `code_challenge_method` desconhecido no code persistido:** A) 5xx por violação de invariante versus
  B) `invalid_grant` genérico.
  - **Resposta Q2:** opção B.
  - **Considerações Q2:** o client apresentou um artefato que o servidor não pode honrar, e isso é resposta de
    protocolo; HTTP 5xx sinaliza bug ou indisponibilidade do sistema e não deve ser emitido por dado de request
    recusado. A escolha também mantém o branch alinhado à equivalência anti-oracle já aplicada aos demais
    cenários de authorization code recusado.
  - **Conclusão Q2:** DF18; a recomendação anterior por 5xx foi descartada e a Invariante 10 deixou de precisar
    de exceção.
- **Q3 — `EndpointErrorResults` no projeto Pipelines:** A) mover a seleção semântica para `RoyalIdentity` versus
  B) manter factory genérica em Pipelines recebendo o código do caller.
  - **Resposta Q3:** opção A.
  - **Conclusão Q3:** DF19; Pipelines fica restrito a writers/resultados HTTP neutros.
- **Q1 — `ResourcesValidator` compartilhado:** A) corrigir o validator compartilhado e assumir o ajuste mínimo do
  authorize versus B) separar validators de authorization e token.
  - **Resposta Q1:** opção A.
  - **Considerações Q1:** `Pipes.cs` mostra `ResourcesValidator` registrado em `AuthorizeContext`,
    `AuthorizeValidateContext` e `ClientCredentialsContext`, e nenhuma das suas oito regras tem branch por
    endpoint — a única diferenciação é o `switch` final que marca `ResourcesValidated()`. As regras específicas
    já vivem fora dele: `ResourcesDecorator` (identity scope exige `openid`) para authorize,
    `ClientResourceDecorator` (identity scope e `offline_access` proibidos, scopes default) para client
    credentials e `AuthorizationResourcesValidator` (`response_type` × scope) só para authorize. `invalid_scope`
    é normativo nos dois endpoints (RFC 6749 §4.1.2.1 e §5.2) e `invalid_target` também (RFC 8707 §§2.1/2.2);
    `Oidc.Authorize.Errors.*` e `Oidc.Token.Errors.*` têm valores de string idênticos. Separar duplicaria oito
    regras iguais sem ganho normativo. `AuthorizationCodeContext` e `RefreshTokenContext` não usam o validator:
    resolvem subset no handler por `context.Error(...)`, que já escreve o campo `error` corretamente.
  - **Conclusão Q1:** DF20; o escopo do authorize neste plano é somente o campo `error`.

---

## Design alvo

### Contratos e bordas

- `ErrorResponseParameters`: continua sendo o payload genérico `error`/`error_description`/`error_uri`.
- `ErrorResponseResult`/`ResponseHandler`: aceitam status e headers HTTP explícitos sem importar constantes
  OAuth para `RoyalIdentity.Pipelines`.
- `EndpointErrorResults`: deixa de selecionar/hardcodar semântica OAuth dentro de Pipelines; as factories que
  escolhem códigos migram para `RoyalIdentity` conforme DF19.
- `ResponseHandlerExtensions`: expõe construção inequívoca de erro protocolar; não existe helper de um ou dois
  `string` em que uma constante de código possa ocupar silenciosamente a posição de descrição.
- Validação estrutural dos parâmetros core do token request ocorre sobre `IFormCollection` ou
  `NameValueCollection.GetValues` antes de qualquer leitura escalar/`Load`; a conversão atual preserva as
  ocorrências e não precisa ser substituída apenas por esse motivo.
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
| Par de assertion completo com tipo/método não suportado | `invalid_client` | 400, sem distinguir causa |
| Par de assertion seleciona `private_key_jwt`, mas JWT/client/claims/assinatura são inválidos | `invalid_client` | 400, sem distinguir causa |
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
| `code_challenge_method` desconhecido já persistido no code | `invalid_grant` | 400, descrição genérica (DF18) |

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

Tests.Integration/Endpoints/TokenErrorTests.cs
  matriz HTTP exata do token endpoint

Tests.Architecture/
  guard da neutralidade de Pipelines e das assinaturas ambíguas
```

### Segurança, concorrência e confiabilidade

- Nenhum evaluator de segredo, lookup de grant ou registro de replay ocorre em request estruturalmente inválido.
- Comparações PKCE continuam em tempo constante quando há verifier e challenge.
- Code apresentado e recusado não revela existência, consumo, client, redirect ou expiração por descrições
  diferentes onde o plano Operational exige equivalência.
- Assertion JWT inválida, client desconhecido e falha criptográfica convergem em `invalid_client` genérico;
  somente a forma do par de parâmetros é recusada antes como `invalid_request`.
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

**Depende de:** DF2-DF5, DF13-DF17, DF19-DF20.

**Escopo:** `RoyalIdentity.Pipelines/Abstractions`, `RoyalIdentity.Pipelines/Defaults`,
`EndpointErrorResults`, `RoyalIdentity/Extensions/ResponseHandlerExtensions.cs`, todos os callers de
`ResponseHandler.Error`/`context.Error`/`InvalidRequest`/`InvalidGrant`/`InvalidClient`, `Tests.Pipelines`,
`Tests.Integration` e `Tests.Architecture`.

**O que/como:** tornar status/headers parte explícita do resultado genérico, remover o overload ambíguo e migrar
callers para separar código e descrição. Criar uma única assertion de integração que desserialize o JSON e
verifique `error`, status, content type, cache e headers opcionais.

**Tarefas:**

- [x] Inventariar todos os callers de `ResponseHandler.Error`, `context.Error`, `InvalidRequest`,
  `InvalidGrant` e `InvalidClient`, registrando endpoint/context e efeito esperado.
- [x] Estender o resultado genérico com headers explícitos sem introduzir referência OAuth em
  `RoyalIdentity.Pipelines`.
- [x] Mover para `RoyalIdentity` as factories que escolhem códigos e remover de Pipelines toda seleção
  hardcoded conforme DF19, inclusive o typo `Invalid_content_type`.
- [x] Remover os overloads de um e dois `string` de `InvalidRequest` que aceitam uma constante de erro como
  descrição, substituindo-os por construção inequívoca.
- [x] Migrar todos os callers afetados, inclusive `ResourcesDecorator`, `ResourcesValidator`,
  `AuthorizeMainValidator`, `AuthorizationResourcesValidator`, `LoadClient` e `RedirectUriValidator`, mantendo o
  `ResourcesValidator` compartilhado conforme DF20.
- [x] Adicionar regressão de authorize provando que `invalid_scope`/`invalid_target` passam a aparecer no campo
  `error` também nesse endpoint e que o caso de signing algorithm continua `invalid_request`.
- [x] Preservar `error_uri` e serialização source-generated.
- [x] Criar `Tests.Pipelines/Defaults/ErrorResponseResultTests.cs` para payload, status, headers, content type e
  cache.
- [x] Extrair `CodeSingleUseTests.ReadErrorAsync` para um helper compartilhado de integração e estendê-lo com
  status, content type, cache e headers opcionais.
- [x] Criar `Tests.Integration/Endpoints/TokenErrorTests.cs` e mover para ele a baseline table-driven da matriz.
- [x] Substituir assertions por substring nos casos tocados pela fase.
- [x] Criar `Tests.Architecture/ProtocolErrorBoundaryTests.cs` para impedir semântica OAuth hardcoded em
  Pipelines e a reintrodução das assinaturas ambíguas.
- [x] Executar regressão dos endpoints que compartilham o writer.

**Critérios de aceite:** nenhum helper aceita um `string` livre na posição em que uma constante de erro possa ser
tratada como descrição; guard impede constantes `Oidc.*.Errors.*` em parâmetros de descrição, tanto posicionais
quanto nomeados; `invalid_scope` e `invalid_target` aparecem no campo JSON `error` no token endpoint e no
authorize (DF20), sem que o `ResourcesValidator` seja duplicado; **os demais callers migrados do authorize
(`unsupported_response_type`, `unsupported_response_mode`, `unauthorized_client`) também expõem o código no
campo `error`, com regressão própria, e nenhum deles muda condição, ordem ou transporte**; status e headers
chegam à resposta e a coleção de headers é imutável e não permite sobrescrever o `no-store` obrigatório; testes
falham se o código existir somente em `error_description`; Pipelines não seleciona códigos OAuth e não contém
nenhuma string de erro protocolar (DF19), com a lista de códigos do guard derivada de `Constants.Oidc.*.Errors`
por reflexão em vez de mantida à mão.

**Testes:**

```powershell
dotnet test Tests.Pipelines --filter "FullyQualifiedName~ErrorResponseResultTests"
dotnet test Tests.Integration --filter "FullyQualifiedName~TokenErrorTests|FullyQualifiedName~ClientToken|FullyQualifiedName~RefreshToken|FullyQualifiedName~CodeToken|FullyQualifiedName~CodeAuthorize"
dotnet test Tests.Architecture --filter "FullyQualifiedName~ProtocolErrorBoundaryTests"
```

### Resultado da Fase 1

**Concluída em 2026-07-31**, com uma segunda rodada no mesmo dia respondendo a uma revisão externa da
implementação. Os quatro achados da revisão procederam e foram verificados antes de aceitos: o guard de códigos
protocolares tinha duas lacunas reais (lista à mão desatualizada e entrada `content_type` que não casava com
literal nenhum; regex cega a argumento nomeado); a invariante 11 contradizia o escopo que a própria tarefa de
migração exigia; a coleção de headers era copiada mas não imutável e permitia sobrescrever o `no-store`; e o
inventário publicado estava aritmeticamente errado. Todos corrigidos nesta seção e no corpo do plano.

Build e suíte completa verdes: **1267 aprovados, 50 ignorados** (opt-in PostgreSQL/Aspire), **0 falhas**.
Comandos da fase: `ErrorResponseResultTests` 21/21, o filtro de integração 103/103,
`ProtocolErrorBoundaryTests` 8/8. `git diff --check` limpo.

**Inventário (tarefa 1).** Contado contra o commit anterior à fase (`0c0eb22`), excluindo a fiação interna do
próprio helper: a auto-delegação em `ResponseHandlerExtensions`; a chamada interna de
`EndpointErrorResults.InvalidRequest` para `BadRequest` já não integra a busca qualificada por callers:
**105 call sites**.

| API | Sites | Situação |
|---|---|---|
| `context.InvalidRequest(...)` | 52 | 27 passavam constante na posição de descrição |
| `context.InvalidGrant(...)` | 16 | código correto, descrição correta |
| `context.InvalidClient(...)` | 6 | idem |
| `context.Error(...)` | 6 | já explícitos |
| `ResponseHandler.Error(...)` direto | 1 | `RevocationHandler` |
| `EndpointErrorResults.*` | 24 | fronteira DF19 |

Dos 27 que passavam constante, **25 tinham classificação efetivamente diferente**: `AuthorizeMainValidator:76` e
`RedirectUriValidator:60` passavam a própria `Errors.InvalidRequest`, então o campo `error` já saía correto e
apenas a descrição carregava o prefixo redundante.

Onde o código estava e para onde foi:

| Caller | Context/endpoint | Sites | `error` antes | `error` depois |
|---|---|---|---|---|
| `ResourcesValidator` | authorize + client credentials (compartilhado, DF20) | 7 | `invalid_request` | `invalid_scope` (5), `invalid_target` (2) |
| `AuthorizeMainValidator` | authorize | 7 | `invalid_request` | `unsupported_response_type` (3), `unsupported_response_mode` (1), `invalid_scope` (2), `invalid_request` (1, inalterado) |
| `ClientResourceDecorator` | client credentials | 4 | `invalid_request` | `invalid_target` (1), `invalid_scope` (3) |
| `ResourcesDecorator` | authorize | 3 | `invalid_request` | `invalid_target` (1), `invalid_scope` (2) |
| `AuthorizationResourcesValidator` | authorize | 3 | `invalid_request` | `invalid_scope` (3) |
| `RedirectUriValidator` | authorize + code | 2 | `invalid_request` | `unauthorized_client` (1), `invalid_request` (1, inalterado) |
| `LoadClient` | todos os contexts com client | 1 | `invalid_request` | `unauthorized_client` |

Os outros 77 call sites já produziam o código correto — implícito no nome do helper (`InvalidGrant`,
`InvalidClient`) ou explícito no argumento (`context.Error`, `EndpointErrorResults`) — e mudaram apenas de API,
sem alteração observável.

**Borda genérica.** `ErrorResponseResult` e `ResponseHandler.Error` passaram a aceitar
`IReadOnlyDictionary<string, string>? headers`. A coleção é **congelada na construção** (`FrozenDictionary`,
incluindo o caso vazio): mutar o dicionário do caller depois de o erro ter sido classificado não muda o que vai
para a resposta, e a coleção exposta não tem implementação mutável para a qual voltar por cast. `Cache-Control`,
`Pragma`, `Content-Type` e `Content-Length` são **reservados**: o writer os escreve e recusa recebê-los, porque
um caller capaz de mandar `Cache-Control: public` desligaria em silêncio o `no-store` de que toda resposta de
erro depende. Nenhuma constante OAuth entrou em `RoyalIdentity.Pipelines`.

**DF19.** `EndpointErrorResults` ficou com duas factories neutras — `Error(httpContext, error, description,
statusCode, headers)` e `BadRequest(httpContext, error, description)` — ambas recebendo o código do caller. As
três falhas de nível HTTP mudaram para `RoyalIdentity/Endpoints/EndpointErrors.cs` (`MethodNotAllowed`,
`UnsupportedMediaType`, `NotFound`), com o typo `Invalid_content_type` corrigido para `invalid_content_type`.
O nome mudou de propósito: `EndpointErrorResults` e `EndpointErrors` coexistiriam ambiguamente se
compartilhassem o nome, porque todo endpoint importa `RoyalIdentity.Pipelines.Abstractions`.

A fase encontrou **um quinto ponto de seleção hardcoded que o plano não previa**:
`RoyalIdentity.Pipelines/Defaults/ProblemsExtensions.ToErrorResult` usava `problemDetails.Title ??
"invalid_request"`. Sem callers, mas violava o critério de aceite. O fallback virou parâmetro obrigatório
(`ToErrorResult(this ProblemDetails, string fallbackError)`), mantendo a escolha do código com o core.

**DF4.** `InvalidRequest`, `InvalidGrant` e `InvalidClient` foram removidos por inteiro em vez de terem
overloads podados. Restou uma única forma — `context.Error(error, description, statusCode, headers)` — em que a
posição 0 é sempre o código. O critério "nenhum helper aceita um `string` livre na posição em que uma constante
de erro possa ser tratada como descrição" fica satisfeito por construção: essa posição deixou de existir.
`context.Error(ErrorDetails)` permanece para resultados já classificados, e o `?? Oidc.Authorize.Errors.
InvalidRequest` morto (o membro é `required string`) foi retirado.

**Comportamento.** Os 25 sítios com classificação diferente passaram a expor o código correto no campo `error`,
em ambos os endpoints. Nada mais mudou: as três
correções de taxonomia (`GrantTypeValidator` → `unauthorized_client`, `LoadCode` → `invalid_request`,
`PkceMatchValidator` verifier ausente → `invalid_request`) continuam com a classificação atual e são da Fase 3,
com testes que asseveram o valor de hoje e nomeiam a fase que os corrige.

**Testes.** `ErrorResponseResultTests` (21) cobre campo exato, `error_uri`, status default e explícito,
content type, cache, headers explícitos, ausência de header quando nenhum foi dado, snapshot na construção,
recusa de cada header reservado, preservação do `no-store` ao lado de um header permitido, imutabilidade sob
cast (com e sem headers) e código de extensão. `Tests.Integration/Prepare/ProtocolErrorResponse.cs` substituiu o
`ReadErrorAsync` privado:
`ProtocolError` carrega `Error`, `Description`, `Uri`, `StatusCode`, `ContentType`, `CacheControl` e `Headers`,
e expõe `Answer` como o par usado nas igualdades anti-oracle — assim uma asserção anti-oracle nunca passa por
acidente só porque status e headers coincidem. `AssertErrorAsync` confere código, status, content type e
`Cache-Control` de uma vez. `TokenErrorTests` tem 19 casos; `CodeAuthorizeTests` ganhou seis regressões — as
duas de DF20 (`invalid_scope`/`invalid_target`) e as quatro dos demais callers migrados
(`unsupported_response_type`, `unsupported_response_mode`, `unauthorized_client` e o contraste com
`invalid_request` para `client_id` ausente, que prova que os dois não convergiram); 11 assertions por substring
viraram asserções de campo exato em `ClientTokenTests` (7), `RefreshTokenTests`, `CodeTokenTests`,
`SigningAlgorithmTests` e `CodeAuthorizeTests`.

**Guard.** `ProtocolErrorBoundaryTests` (8) tem quatro travas mais dois testes de prova. As travas: Pipelines não
referencia o core; nenhum código protocolar aparece como literal no fonte de Pipelines; o core não declara
`InvalidRequest`/`InvalidGrant`/`InvalidClient` em `ResponseHandlerExtensions`; e nenhuma chamada põe uma
constante `Oidc.*.Errors.*` na posição de descrição, posicional **ou nomeada** (`errorDescription:`).

A lista de códigos é **derivada por reflexão** de todo grupo `Errors` sob `Constants.Oidc` — em qualquer
profundidade, o que inclui `Oidc.Errors.Revocation` — somada às constantes privadas de `EndpointErrors`. Uma
lista mantida à mão não serve para isto por duas razões que a revisão externa expôs: ela envelhece (não cobria
`server_error`, `temporarily_unavailable`, `invalid_request_uri`, `request_not_supported`) e uma entrada que não
casa exatamente com o literal nunca casa com nada — a entrada `content_type` não detectava nem o typo
`"Invalid_content_type"` nem a grafia corrigida, porque a busca é pelo literal entre aspas. Um teste assevera
que a reflexão devolve pelo menos 30 códigos, para o guard não virar vácuo em silêncio se o walk quebrar.

Os dois testes de prova exercitam os detectores contra texto sintético, nos dois sentidos: o scan de literais
casa com o typo, com a grafia corrigida e com `server_error`, e não casa com um código chegando como argumento;
a regex casa com constante na segunda posição, com a forma quebrada em múltiplas linhas e com o argumento
nomeado, e não casa com nenhuma das formas corretas.

**Achado entregue à Fase 2.** Cliente desconhecido e segredo errado convergem em `invalid_client` 400, mas
**não na descrição**: `"No client identified"` versus `"Client secret validation failed"`. É um oráculo de
existência de client, dentro de DF15/Invariante 6 e do critério de aceite da Fase 2, não da Fase 1. O teste
`Post_WithUnknownClient_And_WithWrongSecret_Must_ShareCodeAndStatus` assevera hoje o que já é garantido (código
e status) e registra que a Fase 2 o aperta para igualdade de `Answer`.

---

## Fase 2 - Forma da requisição e autenticação do client

**Depende de:** Fase 1, DF6-DF8, DF12-DF13, DF15-DF16.

**Escopo:** `TokenEndpoint`, conversão de form, `EvaluateClient`, `DefaultClientSecretChecker`,
secret evaluators, em especial `TlsClientAuthSecretEvaluator`/`PrivateKeyJwtSecretEvaluator`, resultado HTTP
genérico e `Tests.Integration/Endpoints/TokenErrorTests.cs`.

**O que/como:** validar cardinalidade e combinação de credenciais no form original ou por `GetValues`, antes de
leituras escalares, criação do context ou avaliação com efeitos. Preservar parâmetros multivalorados declarados e
produzir o status/header correto quando a autenticação via `Authorization` falhar.

**Tarefas:**

- [x] Definir a lista de parâmetros core não repetíveis e a exceção multivalorada `resource`.
- [x] Validar repetição antes de `TryGet`/indexers/`Load`, reutilizando a cardinalidade que
  `AsNameValueCollection` já preserva ou operando diretamente no form.
- [x] Preservar todos os valores de `resource` e deixar parâmetros desconhecidos para validação pelo extension
  grant proprietário.
- [x] Detectar Basic, post secret, client assertion e demais mecanismos suportados sem validar credenciais.
- [x] Rejeitar múltiplas credenciais/mecanismos com `invalid_request` antes de chamar evaluators.
- [x] Centralizar no preflight a precedência hoje duplicada em `TlsClientAuthSecretEvaluator`, removendo ou
  tornando inalcançável a regra local divergente.
- [x] Tratar pares incompletos de client assertion como `invalid_request` e tipo/método completo não suportado
  como `invalid_client`.
- [x] Tratar assertion selecionada, mas não parseável, sem `sub`, de client desconhecido ou criptograficamente
  inválida como `invalid_client`, com descrição indistinguível conforme DF15.
- [x] Fechar o oráculo de existência de client achado na Fase 1: `EvaluateClient` responde hoje
  `"No client identified"` para client desconhecido e `"Client secret validation failed"` para segredo errado,
  com o mesmo código e status. Unificar a descrição e apertar
  `TokenErrorTests.Post_WithUnknownClient_And_WithWrongSecret_Must_ShareCodeAndStatus` para igualdade de
  `ProtocolError.Answer`.
- [x] Garantir que request rejeitado não consulta client store, não valida JWT e não grava replay handle.
- [x] Produzir `invalid_client` 401 e `WWW-Authenticate: Basic...` para tentativa Basic inválida/malformada.
- [x] Manter `invalid_client` 400 para falha de autenticação no body, salvo requisito específico.
- [x] Alinhar 405 com `Allow: POST` e 415 sem códigos OAuth inventados.
- [x] Adicionar testes de parâmetros repetidos, resource repetido, Basic+post, Basic+assertion,
  post+assertion, assertion incompleta e ausência de autenticação obrigatória.
- [x] Classificar **qualquer** header `Authorization` presente como tentativa in-band, com cardinalidade
  própria, para que um esquema não suportado nunca vire `None` e caia em certificado/no-secret.
- [x] Repetir as regressões negativas de forma e autenticação no endpoint de revocation, que compartilha o
  preflight, e incluir `RevocationTests` no comando da fase.
- [x] Cobrir o contrato HTTP de DF12 em todos os endpoints alterados, não só no token: valor de `Allow`,
  `application/problem+json` e ausência do campo `error` em 405/415/404.

**Critérios de aceite:** nenhum request com dois mecanismos chega a um evaluator; a regra de precedência não
permanece duplicada no TLS evaluator; `resource` repetido continua funcional; parâmetros core repetidos e par de
assertion incompleto retornam `invalid_request`; assertion JWT/client inválido converge em `invalid_client` sem
oracle; Basic inválido retorna exatamente 401 com `WWW-Authenticate` Basic; requests rejeitados por forma não
consomem `jti`; método/media type retornam 405 com `Allow: POST`/415.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~TokenErrorTests|FullyQualifiedName~PrivateKeyJwt|FullyQualifiedName~ClientToken|FullyQualifiedName~RevocationTests|FullyQualifiedName~HttpFailureTests"
```

> `RevocationTests` e `HttpFailureTests` entraram no filtro nesta fase. Revocation é o segundo endpoint cujo
> pipeline chega a `EvaluateClient` e passou a depender estruturalmente do preflight; `HttpFailureTests` cobre o
> contrato HTTP de DF12 em todos os endpoints, não só no token.

### Resultado da Fase 2

**Concluída em 2026-07-31**, com uma segunda rodada no mesmo dia respondendo a uma revisão externa da
implementação. Os três achados procederam; o primeiro era um defeito funcional real, reproduzido antes de
qualquer correção. Build e suíte completa verdes: **1336 aprovados, 50 ignorados** (opt-in PostgreSQL/Aspire),
**0 falhas**. Comando da fase: 120 aprovados, 2 ignorados. `git diff --check` limpo.

Uma terceira rodada, também no mesmo dia, fechou três resíduos apontados por uma segunda revisão: a decisão do
header ainda olhava o valor, faltava o 404 do JWKS e a contagem de revocation estava errada.

**Preflight.** `RoyalIdentity/Endpoints/DirectRequestPreflight.cs` roda sobre a `NameValueCollection` original,
antes de qualquer leitura escalar, da criação do context e de qualquer efeito. Ele decide três coisas:
cardinalidade, forma do par de assertion e qual mecanismo de autenticação a requisição apresentou. Está ligado
em `TokenEndpoint` e `RevocationEndpoint` — os dois únicos endpoints cujos pipelines chegam a `EvaluateClient`
(`AuthorizationCodeContext`, `RefreshTokenContext`, `ClientCredentialsContext`, `RevocationContext`). No
`TokenEndpoint` o resultado é gravado **depois** do switch de grant, para cobrir também o context que um
extension grant constrói por conta própria.

Dez parâmetros core são single-valued; `resource` fica deliberadamente de fora e continua multivalorado
conforme RFC 8707 §2.1. Presença é a chave existir, não o valor ser útil — `client_secret=` vazio é um cliente
tentando autenticar com segredo, que é como os evaluators sempre leram.

**Precedência.** Esta é a mudança estrutural da fase. `DefaultClientSecretChecker` deixou de percorrer todos os
evaluators e ficar com o primeiro que achou algo: agora consulta apenas os que declaram a fonte detectada
(`IClientSecretEvaluator.Source`). Com isso as regras locais de `TlsClientAuthSecretEvaluator` e
`NoSecretEvaluator` — cada uma verificando assertion/secret/header por conta própria — foram **removidas**, não
apenas contornadas: elas passaram a ser inalcançáveis, e manter duas fontes de verdade sobre precedência era
exatamente o defeito.

O certificado de conexão **não** conta como mecanismo apresentado. Ele é propriedade da conexão, não credencial
da requisição, e uma implantação pode terminar mTLS por motivos alheios à autenticação de client; contá-lo
transformaria toda conexão com certificado em segundo mecanismo e recusaria requisições Basic legítimas. Ele
decide somente quando nada foi apresentado in-band (`Source.None`, onde TLS e NoSecret são consultados na ordem
de registro).

**Correção da primeira rodada (achado externo, Alta).** A detecção do header foi escrita com prefixo — só
`Basic ` contava — e isso reabria pela porta dos fundos exatamente o fallback que a fase existe para fechar:
`Authorization: Bearer x`, `Authorization: Basic` sem credenciais, `Negotiate`, `Digest` e qualquer outro
esquema caíam em `Source.None` e chegavam ao certificado da conexão ou ao `NoSecretEvaluator` — que, para um
client público, **autenticava**. Pior que o estado anterior à fase, porque as guardas locais dos dois
evaluators, que barravam qualquer header presente, tinham acabado de ser removidas. O defeito foi reproduzido
antes de corrigido: os quatro esquemas falharam o teste novo, e o controle com client público sem header
passou.

A fonte passou a ser `AuthorizationHeader`, decidida por **presença da chave**, não pelo esquema nem pelo
conteúdo: um header que o endpoint não sabe usar continua sendo um client tentando autenticar, e classificá-lo
como "nada apresentado" é que era o erro. A primeira correção ainda testava o valor com `IsPresent()`, e a
segunda revisão mostrou que isso deixava passar `Authorization: "   "` — reproduzido antes de corrigido. A
lição é a mesma do defeito original: qualquer teste baseado em conteúdo tem uma forma que ele não reconhece, e
a resposta para forma não reconhecida tem de ser recusar, nunca supor que nada foi apresentado. Um header de
valor inteiramente vazio não tem caso de teste porque não é transmissível — o `HttpClient` não o envia, a
requisição chega sem a chave e é, corretamente, um pedido comum de client público. `BasicSecretEvaluator` responde por todo o header e devolve `null` para qualquer esquema que
não seja Basic, de modo que a recusa sai por um caminho único — `EvaluateClient` com 401 e challenge. Mais de
um header `Authorization` é `invalid_request`, pela mesma razão de cardinalidade dos parâmetros: escolher um
entre vários deixaria a credencial que autentica diferente da que foi inspecionada.

O ganho concreto: header Basic malformado, esquema não suportado e header duplicado agora são **recusados**, em
vez de caírem silenciosamente no certificado ou no caminho sem segredo.

**Anti-oracle.** `EvaluateClient` responde `"Client authentication failed"` para os quatro caminhos: client
desconhecido, segredo errado, client que exige segredo e não mandou nenhum, client desabilitado. O oráculo de
existência achado na Fase 1 está fechado, e três testes provam a convergência — desconhecido versus segredo
errado, sem credencial versus segredo errado, e header versus body (onde só o status difere).

**DF6.** Falha de autenticação tentada via `Authorization` responde 401 com
`WWW-Authenticate: Basic realm="{realm}"`; pelo body continua 400. O challenge carrega apenas o esquema e o
espaço de proteção, nunca nada que a requisição forneceu.

**DF12.** As três falhas de nível HTTP deixaram de fingir taxonomia OAuth. 405, 415 e 404 agora respondem
`application/problem+json` via `HttpFailureResult`, e os códigos inventados `method_not_allowed`,
`Invalid_content_type` e `not_found` desapareceram — não foram renomeados. O 405 passou a incluir `Allow`, com
os métodos declarados por cada endpoint (`POST` no token e no revocation, `GET` na descoberta/JWKS/check
session/authorize callback, `GET, POST` no UserInfo/end session/authorize).

Isto quebrou dois testes do guard da Fase 1, que estavam fixados nos códigos inventados por reflexão sobre
`EndpointErrors` — a falha certa, no lugar certo. O guard passou a tratá-los como **códigos aposentados**: duas
travas novas asseveram que `EndpointErrors` não declara constante de código nenhuma e que os três literais não
reaparecem nem no core nem em Pipelines. `ProtocolErrorBoundaryTests` foi de 8 para 10.

**Cobertura das outras duas superfícies (achados externos, Médias).** As regressões negativas existiam só no
token endpoint, embora a fase tivesse alterado dez endpoints e tornado revocation estruturalmente dependente do
preflight.

`RevocationTests` ganhou dez casos em seis métodos — parâmetro repetido em teoria de quatro, dois mecanismos, par de assertion
incompleto, Basic inválido com 401 e challenge, ausência do segredo exigido, e o header inutilizável nas duas
formas. Revocation é onde a falha do achado 1 mordia mais fundo: `demo_client` é público, então um header
`Authorization` classificado como "nada apresentado" revogava com sucesso. RFC 7009 §2.2 torna token
desconhecido um sucesso, o que deixa "aceito" e "recusado" nitidamente distinguíveis nesse endpoint.

`Tests.Integration/Endpoints/HttpFailureTests.cs` cobre o contrato de DF12 em todos os endpoints roteados: o
valor exato de `Allow` por endpoint (`POST`, `GET`, `GET, POST`), `application/problem+json` e ausência dos
campos `error`/`error_description` em 405, 415 e 404. O guard arquitetural prova que os pseudocódigos não
voltaram; esta suíte prova o contrato que os substituiu.

Os três callers de `EndpointErrors.NotFound` estão cobertos, cada um com o interruptor que de fato o desliga:
discovery e protected resource metadata por `Endpoints.EnableDiscoveryEndpoint`, e o JWKS por
`Discovery.ShowKeySet`. Um interruptor único deixaria o JWKS respondendo 200 e o caso não provaria nada.

Escrever essa suíte revelou que **`CheckSessionEndpoint` não tem rota**: `MapOpenIdConnectProviderEndpoints`
não o mapeia, e um método não suportado responde 404 por ausência de rota, não 405. Não é defeito desta fase —
mapeá-lo pertence a [plan-oidc-session-management.md](plan-oidc-session-management.md) — e a exclusão está
anotada na própria tabela de casos para não parecer esquecimento.

**Testes.** `TokenErrorTests` foi de 19 para 50 casos: dez parâmetros core repetidos em teoria, `resource`
repetido que precisa continuar funcionando, os três pares de mecanismos simultâneos, par de assertion incompleto
nos dois sentidos, tipo de assertion não suportado, Basic inválido e Basic malformado com challenge, secret de
body sem challenge, as três convergências anti-oracle, os cinco esquemas de header inutilizável, header
duplicado, o controle provando que client público sem header continua funcionando, `Allow` no 405 e a prova de
que 405/415 não têm campo
`error`. Em `PrivateKeyJwtReplayProtectionTests`, um teste novo apresenta a mesma assertion primeiro junto de um
`client_secret` (malformada) e depois sozinha: a segunda tem de ser aceita, o que só acontece se o `jti` não
tiver sido gravado pela primeira. A asserção por substring do arquivo virou `AssertErrorAsync`.

**Ponto de atenção registrado.** Um erro de composição — um pipeline que autentica client sem que o endpoint
rode o preflight — lança `InvalidOperationException` em vez de assumir um mecanismo. Assumir reintroduziria
exatamente a adivinhação que o preflight existe para eliminar, e hoje os quatro contexts afetados nascem dos
dois endpoints cobertos.

---

## Fase 3 - Taxonomia dos grants, scopes, resources e PKCE

**Depende de:** Fases 1-2, DF9-DF11, DF16-DF18, conclusão de
[plan-replay-protection.md](plan-replay-protection.md) quando os testes de `private_key_jwt` exigirem o backing
final.

**Escopo:** `TokenEndpoint`, `GrantTypeValidator`, `LoadCode`, `LoadRefreshToken`, `PkceMatchValidator`,
resource decorators/validators conforme DF20, handlers de extension grant e
`Tests.Integration/Endpoints/TokenErrorTests.cs`.

**O que/como:** aplicar a matriz normativa a cada grant suportado, distinguindo ausência/má-formação de valor
apresentado e inválido. Corrigir PKCE sem enfraquecer consumo single-use ou comparação em tempo constante.

**Tarefas:**

- [ ] Retornar `unauthorized_client` quando o client autenticado não permite o grant.
- [ ] Preservar `unsupported_grant_type` para grant não implementado.
- [ ] Corrigir `LoadCode` para `invalid_request` quando `code` estiver ausente ou acima do limite e preservar a
  classificação já correta de `LoadRefreshToken` para refresh ausente/acima do limite.
- [ ] Preservar `invalid_grant` para code/refresh apresentado, mas inválido, expirado, revogado ou com binding
  divergente.
- [ ] Preservar equivalência anti-oracle dos cenários recusados de authorization code.
- [ ] Verificar que `invalid_scope` permanece no campo correto em client credentials e refresh/downscope depois
  da migração única da Fase 1.
- [ ] Verificar que `invalid_target` permanece no campo correto para RFC 8707 depois da migração única da Fase 1.
- [ ] Rejeitar verifier sem challenge e challenge sem verifier com `invalid_request`.
- [ ] Preservar `invalid_grant` para verifier incorreto contra challenge existente.
- [ ] Responder `invalid_grant` 400 genérico no branch `default` de `PkceMatchValidator` conforme DF18,
  registrando o método recusado somente em log e mantendo a descrição indistinguível das demais recusas de code.
- [ ] Confirmar que falha PKCE após consumo não torna authorization code reutilizável.
- [ ] Auditar extension grants para que usem código core ou de extensão documentado.
- [ ] Adicionar testes table-driven para cada linha da matriz normativa alvo nos três grants suportados.

**Critérios de aceite:** cada condição da matriz, inclusive o método persistido desconhecido, tem ao menos um
teste que verifica `error`/status exato; ausência e valor inválido não são confundidos; client sem autorização usa
`unauthorized_client`; PKCE presence mismatch usa `invalid_request`, mismatch criptográfico e método persistido
desconhecido usam `invalid_grant` 400 com descrições indistinguíveis; nenhum caminho do token endpoint emite 5xx
por dado apresentado pelo client; single-use e anti-oracle permanecem verdes; nenhuma segunda implementação do
downgrade existe no plano RFC 9700.

**Testes:**

```powershell
dotnet test Tests.Integration --filter "FullyQualifiedName~TokenErrorTests|FullyQualifiedName~CodeToken|FullyQualifiedName~CodeSingleUse|FullyQualifiedName~RefreshToken|FullyQualifiedName~ClientToken"
```

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Auditoria transversal, regressão e fechamento

**Depende de:** Fases 1-3 e DF1-DF20.

**Escopo:** todos os callers de resposta de erro, discovery quando anunciar auth methods, extension grants,
`Tests.Architecture`, testes amplos, documentação, roadmap e plano RFC 9700.

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
- [ ] Atualizar roadmap e o status do plano RFC 9700, confirmando que a sobreposição executável já foi removida
  por DF17.
- [ ] Executar o guard arquitetural de neutralidade de Pipelines e assinaturas de erro.
- [ ] Executar build, suíte completa e `git diff --check`.

**Critérios de aceite:** não resta assertion por substring para validar `error` do token endpoint; os seis códigos
base e `invalid_target` têm cobertura exata; código de extensão continua serializável; nenhuma falha de
infraestrutura é mascarada; documentação identifica a versão normativa; solução completa está verde.

**Testes:**

```powershell
dotnet build RoyalIdentity.sln
dotnet test Tests.Architecture --filter "FullyQualifiedName~ProtocolErrorBoundaryTests"
dotnet test RoyalIdentity.sln
git diff --check
```

### Resultado da Fase 4

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão(es) | Critério(s) de aceite | Teste(s) |
|---|---|---|---|---|
| Objetivo 1 — código JSON exato | 1, 3, 4 | DF2-DF5, DF19-DF20 | `error` nunca depende da descrição; Pipelines neutro | ErrorResponseResultTests + TokenErrorTests + CodeAuthorize + architecture |
| Objetivo 2 — taxonomia completa | 2-3 | DF3, DF9-DF10, DF15, DF18 | forma, auth, autorização, grant, scope e target distintos | TokenErrorTests + Code/Refresh/ClientToken |
| Objetivo 3 — HTTP 401/header | 1-2 | DF6, DF12 | Basic inválido retorna 401 + `WWW-Authenticate`; 405 inclui Allow | TokenErrorTests |
| Objetivo 4 — forma antes de efeitos | 2 | DF7-DF8, DF15 | duplicidade/mecanismos múltiplos não chegam aos evaluators | TokenErrorTests + PrivateKeyJwt |
| Objetivo 5 — PKCE OAuth 2.1 | 3 | DF11, DF17-DF18 | presence mismatch usa `invalid_request`; mismatch criptográfico e método persistido usam `invalid_grant` indistinguível | TokenErrorTests + CodeSingleUse |
| Objetivo 6 — testes exatos | 1-4 | DF2, DF16 | helper único; sem assertions por substring/filtros vazios | ErrorResponseResultTests + TokenErrorTests + solution |
| Extensibilidade transversal | 1, 3-4 | DF3, DF8, DF20 | `invalid_target` e erro custom continuam válidos | TokenErrorTests + extension grant |
| Anti-oracle e sigilo | 2-4 | DF13, DF15 | code/client auth indistinguíveis e nenhum valor sensível | CodeSingleUse + PrivateKeyJwt + logs |

---

## Invariantes a preservar

1. Toda avaliação de client, code, token, resource e replay continua realm-scoped.
2. Validators/decorators sinalizam falhas esperadas por `context.Response`, sem lançar.
3. `RoyalIdentity.Pipelines` permanece sem dependência do core ou de semântica OAuth.
4. Authorization codes permanecem single-use e a rejeição não cria oracle de existência/binding.
5. `private_key_jwt` continua fail-closed e não consome replay handle em request estruturalmente inválido.
6. Assertion JWT/client inválido converge em `invalid_client` sem revelar parse, existência, claim, chave,
   assinatura, lifetime ou replay como causa.
7. `resource` continua multivalorado conforme RFC 8707.
8. Extension grants e códigos de erro de extensões continuam possíveis.
9. `error_description`, headers e logs nunca expõem credenciais ou artifacts.
10. Falhas de infraestrutura não são traduzidas em falhas de credencial/grant, e o inverso também vale: nenhum
    dado apresentado pelo client — inclusive `code_challenge_method` persistido desconhecido — produz 5xx
    (DF18). 5xx permanece exclusivo de bug ou indisponibilidade.
11. O impacto em authorize limita-se ao **campo `error`** de todos os callers migrados; nenhuma condição, ordem
    de avaliação ou transporte muda incidentalmente, e o transporte redirect versus JSON continua diferido.
    DF20 é o caso que exigiu decisão humana — o `ResourcesValidator` é compartilhado com o token endpoint e não
    poderia ser corrigido de um lado só — mas a varredura de DF4 alcança necessariamente todo caller do
    authorize que passava a constante como descrição: `AuthorizeMainValidator`
    (`unsupported_response_type`, `unsupported_response_mode`), `LoadClient` e `RedirectUriValidator`
    (`unauthorized_client`), `ResourcesDecorator` e `AuthorizationResourcesValidator`
    (`invalid_scope`/`invalid_target`). Cada um desses códigos tem regressão própria em `CodeAuthorizeTests`.
12. Não criar flag de compatibilidade, enum fechado de erros ou opção por client/realm.
13. Não alterar storage, migrations ou semânticas atômicas fechadas neste plano.
14. Não reintroduzir password grant.

---

## Critérios globais de conclusão

- Nenhuma pergunta permanece aberta: Q1/Q2/Q3 estão registradas no histórico e promovidas a DF20/DF18/DF19.
- `ResourcesValidator` continua único e o authorize passa a expor no campo `error` todos os códigos que antes
  ficavam na descrição — `invalid_scope`, `invalid_target`, `unsupported_response_type`,
  `unsupported_response_mode` e `unauthorized_client` — sem alteração de transporte nem de qualquer outra
  condição do endpoint.
- Todas as linhas da matriz normativa alvo possuem teste HTTP ou unitário com `error` exato.
- Basic inválido possui cobertura de HTTP 401 e `WWW-Authenticate`.
- Método inválido possui cobertura de HTTP 405 e `Allow: POST`.
- Duplicidade e múltiplos mecanismos são recusados antes de I/O/efeitos.
- Assertion malformada após selecionar `private_key_jwt` usa `invalid_client` conforme RFC 7523 e não cria
  oracle de client.
- OAuth 2.1 PKCE presence mismatch retorna `invalid_request`; verifier incorreto retorna `invalid_grant`.
- Método PKCE persistido desconhecido retorna `invalid_grant` 400 indistinguível das demais recusas de code, e
  nenhum caminho do token endpoint responde 5xx por dado apresentado pelo client.
- `invalid_target` e um erro de extension grant provam que o writer permanece extensível.
- Não restam assertions por substring para o contrato `error` do token endpoint.
- Não restam comandos obrigatórios cujo filtro selecione zero testes no baseline da fase correspondente.
- Guard de arquitetura prova que Pipelines não seleciona códigos OAuth e que helpers ambíguos não retornaram.
- Plano RFC 9700 depende desta baseline e não contradiz sua classificação de PKCE.
- `dotnet build RoyalIdentity.sln`, `dotnet test RoyalIdentity.sln` e `git diff --check` estão verdes.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| Draft muda durante execução | nova versão altera §3.2.4/PKCE | implementação nasce desatualizada | gate DF1 no início da Fase 4 | Aberto |
| Writer genérico ganha semântica OAuth | constantes OAuth entram em Pipelines | quebra de boundary | DF5 + teste de arquitetura | Mitigado na Fase 1 — `ProtocolErrorBoundaryTests` varre o fonte de Pipelines por 17 códigos protocolares |
| `EndpointErrorResults` preserva códigos hardcoded | factory não migra para o core e helper permanece no projeto neutro | DF5 continua violada apesar do novo writer | DF19 + guard da Fase 1 | Fechado na Fase 1 — factories em `RoyalIdentity/Endpoints/EndpointErrors.cs`; o quinto ponto não previsto (`ProblemsExtensions`) virou parâmetro |
| Detecção múltipla ocorre após evaluator | `jti` é registrado antes do `invalid_request` | retry legítimo parece replay | preflight DF7 + teste negativo do store | Fechado na Fase 2 — `AMalformedRequestCarryingAnAssertion_RegistersNoHandle` apresenta a mesma assertion malformada e depois sozinha |
| Certificado TLS é contado indevidamente | conexão possui certificado usado para outro fim | request Basic válido é recusado | detectar somente mecanismos que a composição trata como client auth | Mitigado na Fase 2 — o certificado não é fonte apresentada; só decide em `Source.None` |
| Validação de duplicidade bloqueia RFC 8707 | regra genérica rejeita `resource` repetido | quebra de resource indicators | allowlist DF8 + regressão multiresource | Fechado na Fase 2 — `resource` fora da lista single-valued, com regressão própria e a multiresource de `ClientTokenTests` verde |
| Enum fecha erros | tipo aceita somente seis valores | extension grants/RFC 8707 quebram | strings + teste de extensão DF3 | Aberto |
| Correção revela detalhes de code | descrições divergem por causa | oracle de existência/binding | preservar igualdade Operational + DF13 | Aberto |
| Correção revela existência do client | assertion inválida e client desconhecido recebem códigos/descrições distintos | oracle de client e violação RFC 7523 | DF15 + matriz indistinguível | Fechado na Fase 2 — descrição única em `EvaluateClient` e três testes de convergência |
| Correção do validator vaza para além do campo `error` | edição do `ResourcesValidator` altera condição, ordem ou transporte no authorize | regressão OIDC fora do escopo declarado | DF20 limita a mudança ao campo `error` + regressão `CodeAuthorize` na Fase 1 | Mitigado na Fase 1 — suíte completa verde e as duas regressões de authorize passam |
| Método PKCE desconhecido some da observabilidade | `invalid_grant` genérico é emitido sem log do método recusado | corrupção de dado ou bug de seed passa despercebido | DF18 exige log do método + teste que prova descrição indistinguível | Aberto |
| Recusa de dado do client vira 5xx | branch novo lança em vez de responder `invalid_grant` | erro de protocolo aparece como indisponibilidade e polui alerta | DF18 + invariante 10 + teste de status exato | Aberto |
| Testes continuam falsos positivos | assertion busca texto no body | regressão passa sem conformidade | helper único e auditoria Fase 4 | Aberto |
| Filtro obrigatório executa zero testes | arquivo/classe planejado não foi criado ou nome divergiu | comando verde sem verificar critério | DF16 + nomes de classe explícitos | Aberto |
| Falha de backing vira erro OAuth | catch amplo em evaluator/handler | indisponibilidade mascarada como credencial inválida | teste 5xx e invariant 9 | Aberto |
| Alteração compartilhada afeta authorize | helper comum muda payload/redirect | regressão OIDC fora do token endpoint | suíte ampla na Fase 1/4 | Mitigado na Fase 1 — 1252 aprovados, 0 falhas; reavaliar na Fase 4 |

---

## Diferidos e backlog

- **Taxonomia e transporte completos do authorization endpoint** — destino: plano próprio. Em particular,
  depois de `redirect_uri` válido, erros hoje saem como JSON direto em vez de redirect com `error`/`state`
  conforme RFC 6749 §4.1.2.1; DF20 entrega somente o campo `error` correto, não o transporte.
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
- [RFC 7523 — JWT Profile for OAuth 2.0 Client Authentication](https://www.rfc-editor.org/rfc/rfc7523.html).
- [RFC 8707 — Resource Indicators for OAuth 2.0](https://www.rfc-editor.org/rfc/rfc8707.html).
- [plan-data-operational-storage.md](plan-data-operational-storage.md).
- [plan-replay-protection.md](plan-replay-protection.md).
- [plan-rfc9700-security-hardening.md](plan-rfc9700-security-hardening.md).
- [plans-roadmap-02.md](plans-roadmap-02.md).
- `RoyalIdentity/Endpoints/TokenEndpoint.cs`.
- `RoyalIdentity/Extensions/ResponseHandlerExtensions.cs`.
- `RoyalIdentity.Pipelines/Defaults/ErrorResponseResult.cs`.
- `RoyalIdentity.Pipelines/Abstractions/EndpointErrorResults.cs`.
- `RoyalIdentity/Contexts/Decorators/EvaluateClient.cs`.
- `RoyalIdentity/Contracts/Defaults/DefaultClientSecretChecker.cs`.
- `RoyalIdentity/Contexts/Validators/GrantTypeValidator.cs`.
- `RoyalIdentity/Contexts/Validators/PkceMatchValidator.cs`.
- `RoyalIdentity/Contexts/Validators/ResourcesValidator.cs`.
- `Tests.Architecture/ModuleBoundaryTests.cs`.
- `Tests.Integration/Endpoints/CodeSingleUseTests.cs`.
- `Tests.Integration/Endpoints/ClientTokenTests.cs`.
- `Tests.Integration/Endpoints/CodeTokenTests.cs`.
- `Tests.Integration/Endpoints/RefreshTokenTests.cs`.
