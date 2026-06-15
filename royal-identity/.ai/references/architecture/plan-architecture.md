# Planejamento para definição de arquitetura de módulos

Este planejamento é para melhorar o `.ai\references\architecture\arquitetura_modular_ddd_hexagonal_features.md` gerando uma nova versão aperfeiçuada;

## Fortes da arquitetura

- Modularidade por domínio
- DDD no núcleo interno
- Hexagonal Architecture como princípio, não como nomenclatura física
- Vertical Application Slices

Apresentação de "Excesso de camadas" contra "Vertical Slice solta demais".

Objetivos do "meio termo".

Declaração da regra central: Módulo é a unidade de negócio. Feature é a unidade operacional. Domain é a unidade de regra. Infrastructure é a unidade técnica. Web é a unidade de entrada.

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
Company.Solution.Products/
  Domain/
  Features/
  Web/
  Infrastructure/
```

Esta apresentada acima é uma estrutura mais "explícita".

Outra variação válida é mais "gritante":

```text
Company.Solution.Products/
  Features/
    {Aggregate}/
      Domain/
      {Feature}/
  Web/
  Infrastructure/
```

A "Estrutura expandida" apresentada não é válida, ela quebra o domínio em tipos de objetos, e isso não é recomendado.

As quebras devem ser orientadas as camadas, sendo o estilo explícito, ou orientado ao domínio, sendo estilo gritante.

Então, uma estrutura expandida válida seria:

```text
Company.Solution.Products/
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