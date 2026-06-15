# Planejamento para definição de arquitetura de módulos

Este planejamento é para melhorar o `.ai\references\architecture\arquitetura_modular_ddd_hexagonal_features.md` gerando uma nova versão aperfeiçuada;

## Objetivos

Analisar `arquitetura_modular_ddd_hexagonal_features` e recriar aplicando as correções deste documento de planejamento.

Criar nova versão `vertical-features-modular-architecture.md`.

## Fortes da arquitetura

- Modularidade por domínio
- DDD no núcleo interno
- Hexagonal Architecture como princípio, não como nomenclatura física
- Vertical Application Slices

Apresentação de "Excesso de camadas" contra "Vertical Slice solta demais".

Objetivos do "meio termo".

Declaração da regra central: Módulo é a unidade de negócio. Feature é a unidade operacional. Domain é a unidade de regra. Infrastructure é a unidade técnica. Web é a unidade de entrada.

Outro ponto são os exemplos, eles usam bibliotecas externas que tem conteúdo documentado em `.ai\references\external-libraries\`.

## Assertos, erros e correções a serem aplicadas

### Forma estrutural

A forma estrutural resumida é certa como apresentada:

```text
Module/
  Domain/
  Features/
  Web/
  Infrastructure/
```

### Estrutura de solução

A estrutura da solução é certa:

```text
Company.Solution/
  src/
    Apps/
      Company.Solution.Api/
      Company.Solution.Web/

    Modules/
      Company.Solution.Products/
      Company.Solution.Sales/
      Company.Solution.Billing/
      Company.Solution.Identity/

    Common/
      Company.Solution.Common/
      Company.Solution.EfMigrations/

  tests/
    Company.Solution.UnitTests/
    Company.Solution.IntegrationTests/
    Company.Solution.ArchitectureTests/

  docs/
    architecture.md
```

As outras subseções também são certas.

### Estrutura dos módulos

A estrutura apresentada é certa:

```text
{Company}.{Solution}.{Module}/
  Domain/
  Features/
  Web/
  Infrastructure/
```

Esta apresentada acima é uma estrutura mais "explícita".

Outra variação válida é mais "gritante":

```text
{Company}.{Solution}.{Module}/
  Features/
    {Aggregate}/
      Domain/
      {Feature}/
  Web/
  Infrastructure/
```

---

A "Estrutura expandida" apresentada não é válida, ela quebra o domínio em tipos de objetos, e isso não é recomendado.

As quebras devem ser orientadas as camadas, sendo o estilo explícito, ou orientado ao domínio, sendo estilo gritante.

Então, uma estrutura expandida válida seria:

```text
{Company}.{Solution}.{Module}/
  Domain/
    {Aggretate}s/
    Support/
    Commons/

  Features/
    {Aggretate}s/
      Commons/
      {Feature}/
    {Feature}/

  Web/
    Api/
    Razor/

  Infrastructure/
    Data/
    Searches/
    Messaging/
    Gateways/
    Security/
```

Dentro de `Domain/` deve ser denominados os aggregates, no plural. Pode ter também uma pasta de `Support` ou `Commons` para tipos/classes que podem ser úteis para múltiplos agregados, ou entidades simples de suporte.

Dentro de `Features/` devem reaparecer os aggregates, no plural. As features de cada agregado devem ser separadas em duas categorias. As features mais CRUD-like devem ficar em `Commons/` assim como objetos de contrato comuns. Para features que representam intenção de negócio e não é CRUD-like, deve ser criado uma pasta para ela.

Outras features que representam intenção de negócio e não se enquadra como operação de um agregado, deve ter uma pasta dedicada para ela dentro do `Features/`. 

Pode ser usado o termo `ExternalServices`, mas `Gateways` também podem ser usados, pois as classes para acessar serviços externos são como gateways.

---

Outra estrutura expandida válida seria uma variação mais "gritante":

```text
{Company}.{Solution}.{Module}/
  Features/
    {Aggregate}/
      Domain/
      Commons/
      {Feature}/
    {Feature}/

  Web/
    Api/
    Razor/

  Infrastructure/
    Data/
    Searches/
    Messaging/
    Gateways/
    Security/
```

### Estrutura de Domain

Os exemplos de estrutura apresentados no outro documento são inválidos.

Os exemplos de estrutura corretos são apresentados abaixo:

Exemplo do modo mais "explícito":

```text
MyCompany.MySolution.Products
  Domain/
    Products/
      Product.cs
      ProductCode.cs
      ProductName.cs
      ProductPrice.cs
      ProductStatus.cs
      ProductPricingService.cs
      ProductActivationPolicy.cs
      Events/
        ProductCreated.cs
        ProductPriceChanged.cs
```

Exemplo do modo mais "gritante":

```text
MyCompany.MySolution.Products
  Features/
    Products/
      Domain/
        Product.cs
        ProductCode.cs
        ProductName.cs
        ProductPrice.cs
        ProductStatus.cs
        ProductPricingService.cs
        ProductActivationPolicy.cs
      Events/
        ProductCreated.cs
        ProductPriceChanged.cs
```

### Estrutura de Features

Para a camada de aplicação, onde há as features, handlers e outros objetos, não há mudança entre o modo mais "explícito" e o modo mais "gritante".

A mudança consiste em que o diretório de `domain` na "explícita" fica na raiz do projeto e há uma organização por agregados lá; já na "gritante" fica dentro da orgranização por agregados dentro de `Features/`.

Para os exemplos de estrutura de Features, a parte de `domain` não é omitida.

A seguir temos um padrão de como features devem ser organizadas:

```text
{Company}.{Solution}.{Module}/
  Features/
    Products/
      Commons/
        CreateProduct.cs
        GetProductDetails.cs
        UpdateProduct.cs
        ProductDetails.cs
        ProductFilters.cs
        ProductSummary.cs
        SearchProducts.cs
      ChangePrice/
        ChangeProductPrice.cs
        ChangeProductPriceHandler.cs
        IChangeProductPriceHandler.cs
    {Feature}/

```

Um dos acertos do outro documento é `Preferir nomes semânticos`. Usar coisas Request e Response, Command, Query, Event, devem ser evitados.

## Observações finais

O resto do documento é bem orientado, visando o uso de bibliotecas do `RoyalCode.SmartCommands` entre outras do `RoyalCode`.

Só é necessário reorientar o documento com as estruturas criadas de forma certa, e avaliando sob as duas lentes, "explícito" e "gritante".

A "arquitetura explícita" é do Herberto Graça, acessável em `https://herbertograca.com/tag/explicit-architecture/`.

A "arquitetura gritante" é do Robert C. Martin ("Screaming Architecture"), apresentado no livro "Clean Architecture".

Outros nomes que influenciam esta arquitetura são: Eric Evans, Vaughn Vernon, Jimmy Bogard.
